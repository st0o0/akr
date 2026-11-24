## Context

akr resolves Akka.NET source versions via `GitHubClient.ResolveLatestAsync`, which queries GitHub's `/releases/latest` API. Users can override with `AKKA_VERSIONS` env var (per `version-config` spec). Both approaches require explicit configuration or accept whatever is latest.

Most akr users already have Akka.NET referenced in their .NET project files. The version is right there — akr just doesn't read it yet. MCP servers receive a working directory from the client transport (stdio), making project file discovery feasible without additional configuration.

## Goals / Non-Goals

**Goals:**
- Auto-detect Akka.NET version from .NET project files with zero configuration
- Support all common NuGet version declaration patterns (CPM, inline PackageReference, Directory.Build.props)
- Provide runtime project context switching via MCP tool
- Expose current resolution state via MCP resource
- Maintain full backward compatibility — no behavior change without a .NET project in scope

**Non-Goals:**
- NuGet version range resolution (e.g. `[1.5.*, )`) — take the literal version string
- Scanning `packages.config` (legacy format, pre-SDK-style projects)
- Multi-project version conflict resolution — use the first `Akka` core package version found
- Automatic re-scanning on file changes (no file watcher)

## Decisions

### Decision 1: Layered resolution order

**Choice:** `AKR_PROJECT_PATH` scan → working directory scan → `AKKA_VERSIONS` env → `/releases/latest`

**Rationale:** Environment variable path override takes highest precedence because it's an explicit user action. Working directory scan is next as the zero-config default. `AKKA_VERSIONS` remains as a non-project override. Latest release is the final fallback.

**Alternative considered:** Project scan after `AKKA_VERSIONS` — rejected because if you're in a project directory, the project version is almost certainly what you want. `AKKA_VERSIONS` is a coarser tool.

### Decision 2: XML parsing with System.Xml.Linq

**Choice:** Use `XDocument` to parse MSBuild XML files directly.

**Rationale:** .NET built-in, no extra dependency. MSBuild project files are well-formed XML. We only need to extract `Include` and `Version` attributes from `PackageVersion` and `PackageReference` elements — no need for a full MSBuild evaluation engine.

**Alternative considered:** `dotnet list package` CLI — rejected because it requires the .NET SDK to be installed, the project to restore successfully, and is slow (~seconds vs milliseconds for XML parsing).

### Decision 3: Walk-up directory search

**Choice:** From the starting directory, walk up parent directories looking for `Directory.Packages.props`, then `Directory.Build.props`, then `*.csproj` files. Stop at the first directory containing any of these.

**Rationale:** Matches MSBuild's own import resolution behavior. CPM files (`Directory.Packages.props`) are checked first because they're the most reliable single source of truth. The walk-up handles projects where the MCP server starts in a subdirectory.

### Decision 4: Core Akka package as reference version

**Choice:** Use the version of the `Akka` core package as the reference for fetching source from all repos.

**Rationale:** Every Akka.NET project references the core `Akka` package. Satellite repos (Management, Persistence.Sql) have their own independent version schemes, but their versions are discovered from their own `latest` release — only the core `akkadotnet/akka.net` repo version comes from the project. Satellite repos continue using latest-resolution unless their specific package is also found in the project files.

### Decision 5: ProjectScanner as a stateless service

**Choice:** `ProjectScanner` in `Akr/Analysis/` as a singleton service using `IFileSystem` for testability.

**Rationale:** Consistent with the existing `TypeScanner` and `SourceSearcher` patterns. Stateless scanning makes it easy to call from both startup (working directory) and the `set_project_context` tool (dynamic path). Uses `IFileSystem` from `System.IO.Abstractions` like the rest of the codebase.

### Decision 6: MCP resource for observability

**Choice:** `akr://context` resource returning JSON with detected version, source (project/env/latest), project path, and found packages.

**Rationale:** Gives AI agents and users visibility into which version akr resolved and why. Useful for debugging ("why is akr showing 1.5 source when I'm on 1.6?"). Read-only, lightweight, no subscriptions needed.

## Risks / Trade-offs

**[Stale project files]** → Project files may reference an old version if the user hasn't restored/updated. Mitigation: This is the user's declared dependency — matching it is correct behavior even if newer versions exist.

**[No project found in CWD]** → MCP client may start the server in a non-project directory. Mitigation: Graceful fallback to existing behavior (AKKA_VERSIONS → latest). The `set_project_context` tool provides a manual escape hatch.

**[MSBuild conditions/imports]** → Project files may use `<Import>` or conditional `<PackageReference>` elements. Mitigation: We parse the XML literally without evaluating conditions. This covers 95%+ of real projects. Edge cases fall back to latest-resolution.

**[Performance]** → Directory walk-up and XML parsing on every server start. Mitigation: Parsing a few XML files is sub-millisecond. Results are cached in `ProjectScanner` after first scan.
