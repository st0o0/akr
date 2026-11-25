## Why

akr starts a new process per Claude Code session. JIT startup adds ~500ms before the first MCP tool response. Native AOT reduces this to <50ms and produces a self-contained binary that eliminates the .NET runtime dependency for end users.

A spike confirmed: AOT compiles and runs successfully with all dependencies (MCP SDK 2.4.0, Akka.Hosting 1.6.0-beta7, Terp, System.IO.Abstractions). Only 3 System.Text.Json call sites need source generators.

## What Changes

- Replace 3 reflection-based `JsonSerializer` calls with source-generated `JsonSerializerContext` variants
- Add `PublishAot=true` and `ToolPackageRuntimeIdentifiers` (win-x64, linux-x64, osx-arm64) to `Akr.csproj`
- Resulting binary: ~23.5 MB self-contained, <50ms startup
- Users still install via `dotnet tool install` — SDK picks the correct RID-specific package automatically

## Capabilities

### New Capabilities
- `json-source-generators`: Replace reflection-based System.Text.Json serialization with compile-time source generators for AOT compatibility
- `aot-publish-config`: Configure Native AOT publishing with per-platform RID packages for the dotnet tool

### Modified Capabilities

## Impact

- **Binary size**: ~200KB framework-dependent → ~23.5 MB self-contained per platform
- **NuGet package**: One package per RID instead of single portable package
- **Startup**: ~500ms → <50ms (10x improvement)
- **Runtime dependency**: .NET 10 runtime no longer required on target machine
- **Code**: 3 files changed (GitHubClient.cs, ProjectContextTools.cs, Akr.csproj)
