## Context

akr is a single-project MCP server (.NET 10, Native AOT). It has accumulated structural noise from rapid iteration: section comments, an unnecessary `IHostedService` for synchronous init, instance methods without instance state, regex patterns that fall back to interpreted mode, and logging that doesn't leverage source generators. The project has `TreatWarningsAsErrors` and `PublishAot=true`, making SYSLIB1044 and CA1873 build-breaking when analyzer levels advance.

## Goals / Non-Goals

**Goals:**
- Zero section comments — method signatures speak for themselves
- `ProjectScanner` owns its lifecycle — no external initializer class
- `MapToRepoVersions` lives on `RepoRegistry` as a static method (it only calls `RepoRegistry.GetEntry()`)
- All `[GeneratedRegex]` patterns compile fully via source generator — no SYSLIB1044 fallbacks
- All logging uses `[LoggerMessage]` source generators — AOT-safe, zero-cost when level disabled

**Non-Goals:**
- Refactoring HoconScanner/SourceSearcher to static (consistent DI pattern, low value)
- Moving TypeScanner's static utility methods to a separate class (already static, works fine)
- Changing any MCP tool behavior or public API

## Decisions

### 1. ProjectContextInitializer → ProjectScanner.Initialize()

**Choice:** Delete `ProjectContextInitializer`. Add `Initialize(string? projectPath)` to `ProjectScanner`. Call it from DI singleton factory registration.

**Why not lazy:** Lazy init on first `Current` access would add null-check complexity to every consumer. Eager init during DI setup is simpler and fails fast.

**Why not keep IHostedService:** It's a synchronous one-liner that sets a property. The ceremony adds a class, a registration, and an ordering concern for no benefit.

### 2. MapToRepoVersions → RepoRegistry.MapToRepoVersions()

**Choice:** Move to `RepoRegistry` as `public static`. It only calls `RepoRegistry.GetEntry()` — it belongs with the registry, not the scanner.

**Alternative:** Extension method on `Dictionary<string, string>`. Rejected — too generic, less discoverable.

### 3. Regex: NonBacktracking everywhere + decompose mega-patterns

**Choice:** Add `NonBacktracking` to all patterns in TypeTools.cs that lack it. For `TypeDeclPattern` and `DeclPattern` that may still exceed source generator capability: decompose into multi-step string operations (match type keyword first, then parse modifiers in code).

**Risk:** `NonBacktracking` doesn't support lazy quantifiers (`*?`). TypeScanner.cs line 301 uses `[^{;]*?` — replace with greedy equivalent or restructure the match.

### 4. LoggerMessage source generators

**Choice:** One `static partial class Log` per class that logs (nested or file-scoped). Each logging call becomes a `[LoggerMessage]` partial method.

**Why not a single shared Log class:** Methods need different `ILogger` categories for filtering. Per-class keeps the category.

**Pattern:**
```csharp
private static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Detected Akka.NET packages in {Path}: {Packages}")]
    public static partial void DetectedPackages(ILogger logger, string path, string packages);
}
```

### 5. Scan method deduplication

**Choice:** Merge `ScanPackagesProps` and `ScanPackageReferences` into `ScanPackageElements(string filePath, string elementName)`. Both have identical XML-scanning logic, only the element name differs.

## Risks / Trade-offs

- **[Regex decomposition may change match behavior]** → Verify with existing snapshot tests; add targeted regex unit tests for edge cases before changing patterns
- **[LoggerMessage adds boilerplate]** → Trade-off accepted: AOT safety and conditional evaluation outweigh the extra partial method declarations
- **[DI factory init runs before IHostedService]** → This is actually earlier than before, which is fine — scanning is synchronous and fast
