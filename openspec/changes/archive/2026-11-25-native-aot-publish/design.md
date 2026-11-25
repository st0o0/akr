## Context

akr is a .NET 10 dotnet tool (MCP server) that spawns a new process per Claude Code session. JIT startup adds ~500ms latency. .NET 10 supports Native AOT for dotnet tools via `PublishAot=true` + `ToolPackageRuntimeIdentifiers`. A spike confirmed all dependencies are AOT-compatible — only 3 System.Text.Json call sites produce trim warnings.

## Goals / Non-Goals

**Goals:**
- Enable Native AOT publishing for akr
- Fix all trim/AOT warnings to achieve clean build
- Maintain `dotnet tool install` distribution model

**Non-Goals:**
- Changing any tool behavior or APIs
- Supporting platforms beyond win-x64, linux-x64, osx-arm64
- Optimizing binary size (default AOT settings are acceptable at ~23.5 MB)

## Decisions

### 1. PublishAot=true directly, no IsAotCompatible step
**Choice**: Skip the intermediate `IsAotCompatible=true` step and go straight to `PublishAot=true`.
**Why**: Spike proved AOT works end-to-end. No need for an incremental approach when the result is already validated.

### 2. JsonSerializerContext per serialization site
**Choice**: Add `[JsonSerializable]` source generator contexts close to where they're used — one in `GitHubClient.cs` for `RepoMetadata`, one in `ProjectContextTools.cs` for the anonymous context type.
**Why**: Keeps the source generator output co-located with usage. Only 2 types need serialization: `RepoMetadata` and the project context anonymous type (which needs to become a named record for AOT).
**Alternative**: Single shared `AkrJsonContext` file. Viable but adds coupling for only 2 types.

### 3. ToolPackageRuntimeIdentifiers for 3 platforms
**Choice**: `win-x64;linux-x64;osx-arm64` — the three primary Claude Code platforms.
**Why**: Covers Windows, Linux (WSL, CI), and macOS Apple Silicon. Intel Mac users can use Rosetta. Additional RIDs can be added later without breaking changes.

### 4. Replace anonymous type with named record in ProjectContextTools
**Choice**: The `JsonSerializer.Serialize(new { ... })` call in `ProjectContextTools` uses an anonymous type which can't have a `[JsonSerializable]` attribute. Replace with a named record.
**Why**: Source generators require concrete types. A simple internal record is cleaner than trying to work around anonymous type limitations.

## Risks / Trade-offs

- **[NuGet package size increase]** → Three RID-specific packages at ~23.5 MB each instead of one ~200KB portable package. Acceptable for a tool installed once.
- **[CI build time increase]** → AOT compilation is slower than JIT (~30-60s per RID). Mitigated: AOT publish only runs in release/pack CI step, not on every build.
- **[vswhere.exe PATH issue on Windows]** → Native linker needs Visual Studio tools in PATH. CI environments typically have this. Local dev may need VS Developer Command Prompt. Mitigated: only affects `dotnet publish`, not `dotnet build` or `dotnet run`.
