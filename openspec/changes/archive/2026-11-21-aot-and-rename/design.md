## Context

akka-mcp is a working MCP server with 2 tools, 5-repo registry, GitHub archive fetcher,
and local cache. It currently requires `AKKA_VERSIONS` env var, carries unused Roslyn and
Serilog dependencies, and is not AOT-compatible. This design covers making it zero-config,
AOT-native, and properly named.

## Goals / Non-Goals

**Goals:**

- Zero-config startup: `akr` just works, resolves latest Akka versions automatically
- Native AOT binary under 15 MB, startup under 50ms
- Clean rename from AkkaMcp to Akr throughout
- Remove all AOT-incompatible dependencies

**Non-Goals:**

- Roslyn integration (Phase 1.5, will be a separate non-AOT build target)
- Cross-compilation (build on each target platform)
- Custom Serilog sinks or structured logging beyond console

## Decisions

### 1. Rename strategy: AkkaMcp → Akr

Rename all projects, namespaces, and the solution:

```
AkkaMcp/           → Akr/               (host)
AkkaMcp.Core/      → Akr.Core/          (domain)
AkkaMcp.Tests/     → Akr.Tests/
AkkaMcp.Tests.Shared/ → Akr.Tests.Shared/
AkkaMcp.Architecture.Tests/ → Akr.Architecture.Tests/
akka-mcp.slnx      → akr.slnx
```

NuGet PackageId: `Akr`. Tool command: `akr`.

**Why full rename, not just PackageId:** Namespace `AkkaMcp.Core.Registry` reads awkwardly.
`Akr.Core.Registry` is clean and consistent. Doing it now before any external consumers exist.

### 2. Latest-by-default version resolution

```
Startup:
  AKKA_VERSIONS set?
    YES → parse as before (pinned versions)
    NO  → all repos start with version = null

First tool call:
  SourceProvider.EnsureAllConfiguredAsync():
    For each of the 5 repos:
      → Fetch latest release tag via GitHub API (one call per repo)
      → Cache the resolved version for the session
      → Fetch source archive for that version
```

5 API calls to resolve latest, 5 archive downloads on first use. All cached after that.
No config file, no env var needed.

**Why not resolve at startup:** Lazy resolution avoids blocking server startup with network
calls. The server is ready instantly; the first tool call pays the fetch cost.

### 3. AOT build configuration

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

`InvariantGlobalization` eliminates ~10 MB of ICU data. We don't need locale-specific
formatting — source code is ASCII/UTF-8.

### 4. Drop Serilog, keep built-in logging

The host already has `builder.Logging.AddConsole()` which logs to stderr (MCP convention).
Serilog adds 4 packages and is not AOT-marked. The built-in console logger is AOT-safe
and sufficient for an MCP server that only needs diagnostic output.

### 5. Explicit MCP tool registration

```csharp
// BEFORE (requires reflection):
.WithToolsFromAssembly()

// AFTER (AOT-safe):
.WithTools<SearchSourceTool>()
.WithTools<LookupTypeTool>()
```

Each new tool needs one line in McpSetup. Acceptable trade-off for AOT compatibility.

### 6. Regex AOT strategy

Two categories:

**Static patterns** (TypeFinder): Use `[GeneratedRegex]` source generator — compiled at
build time, zero runtime cost, fully AOT-safe.

**Dynamic patterns** (SearchSourceTool, user input): Use `RegexOptions.NonBacktracking`
instead of `RegexOptions.Compiled`. NonBacktracking is AOT-safe, has guaranteed linear
time complexity, and doesn't require JIT. Slightly slower for simple patterns, faster
for pathological ones.

### 7. JSON serialization for GitHub API

The `ResolveLatestVersionAsync` method parses a small JSON response (`tag_name` field).
Current code uses `JsonDocument` (read-only DOM) which is fully AOT-safe — no source
generator needed. Keep as-is.

## Risks / Trade-offs

- **[Terp not AOT-marked]** Terp 0.2.1 has no `IsAotCompatible` marker, but its code
  is reflection-free pattern matching. → Mitigation: test with `PublishAot`, watch for
  trim warnings. Can inline the setup if needed (it's one interface).
- **[NonBacktracking regex slower for simple patterns]** ~2x slower than Compiled for
  trivial patterns. → Mitigation: search is I/O-bound (file reads), regex speed is
  irrelevant. The linear-time guarantee is actually better for user-supplied patterns.
- **[5 extra API calls on first use]** Latest resolution adds 5 calls to github.com/api.
  → Mitigation: one-time per session, <1s total, results cached.
