## Why

The MCP server should be distributable as a small native executable (`akr.exe` / `akr`)
that works out of the box with zero config  -  like `rg` or `fd`. Currently it requires
manual version configuration via `AKKA_VERSIONS`, ships with unused dependencies
(Roslyn, Serilog), and the name `akka-mcp` is generic. This change makes the server
lean, AOT-compiled, zero-config, and properly named.

## What Changes

- **Rename** from `AkkaMcp` to `Akr` (Akka Reference) across solution, projects, namespaces
- **Latest-by-default**: `AKKA_VERSIONS` becomes optional; server resolves latest release
  per repo on first use when unconfigured
- **Native AOT**: `PublishAot=true`, trimming, single-file native binary (~10-15 MB)
- **Drop Microsoft.CodeAnalysis.CSharp**: unused (Roslyn is Phase 1.5), AOT-incompatible
- **Drop Serilog stack** (4 packages): replace with built-in `Microsoft.Extensions.Logging`
  console provider (already AOT-safe, already logs to stderr per MCP convention)
- **Explicit MCP tool registration**: `WithTools<T>()` instead of `WithToolsFromAssembly()`
  (the assembly-scanning overload is marked `[RequiresUnreferencedCode]`)
- **AOT-safe regex**: `RegexOptions.NonBacktracking` for runtime patterns, `[GeneratedRegex]`
  for static patterns in TypeFinder

## Capabilities

### New Capabilities

- `aot-publishing`: Native AOT build configuration, trimming, platform-specific RIDs,
  and package metadata for the `akr` dotnet tool.

### Modified Capabilities

- `version-config`: `AKKA_VERSIONS` becomes optional. No config = latest for all repos.
  Server resolves latest on first tool call, caches the result for the session.
- `search-source-tool`: Regex uses `RegexOptions.NonBacktracking` instead of `Compiled`.
- `lookup-type-tool`: Static patterns use `[GeneratedRegex]` attribute.

## Impact

- **Rename**: all project names, namespaces, assembly names, solution file, csproj files,
  NuGet PackageId, test projects, AGENTS.md, CLAUDE.md
- **Removed packages**: Microsoft.CodeAnalysis.CSharp, Serilog, Serilog.Extensions.Hosting,
  Serilog.Settings.Configuration, Serilog.Sinks.Console
- **New MSBuild properties**: `PublishAot`, `InvariantGlobalization`, `JsonSerializerIsReflectionEnabledByDefault`
- **MCP tool registration**: explicit generic calls in McpSetup
- **Binary size**: ~60-80 MB (self-contained) → ~10-15 MB (native AOT)
- **Startup time**: ~200ms (JIT) → ~20ms (AOT)
- **No breaking changes** to MCP tool interface  -  tools work identically
