## Context

akr is a stdio-based MCP server that serves 33 tools. Each tool call triggers `EnsureAllSourcesAsync` which resolves all repo roots, and many tools then scan every `.cs` file in those roots. Within a single MCP session, the source tree never changes — it's fetched once and cached on disk. Yet every tool call re-reads thousands of files and re-builds collections from scratch.

The codebase also has three independent `EnumerateFiles` helpers, two identical `StripComment` methods, static regex fields that could be source-generated, and a package version lagging behind the target framework.

## Goals / Non-Goals

**Goals:**
- Eliminate redundant I/O by caching source roots and parsed type data per `GitHubClient` lifetime
- Remove code duplication across tool classes
- Improve regex performance via source generation
- Align package versions with target framework

**Non-Goals:**
- Changing MCP tool APIs or response formats
- Async file I/O conversion (low-impact for stdio single-request server)
- Adding new tools or changing tool behavior
- Cross-version source comparison (future work)

## Decisions

### 1. Source roots caching in GitHubClient

Cache the `IReadOnlyList<string>` result of `EnsureAllSourcesAsync` in a nullable field on `GitHubClient`. Since the GitHubClient is registered as a typed HttpClient (scoped per-request by default in DI), and source fetching is idempotent within a session, caching is safe.

**Alternative considered:** `ConcurrentDictionary` or `Lazy<Task>` — unnecessary complexity for a single-threaded stdio server.

### 2. ScanTypes caching via static ConcurrentDictionary

Cache `ScanTypes` results keyed by a composite of sorted root paths. Use `ConcurrentDictionary<string, List<TypeDecl>>` as a module-level static. The key is the joined+sorted root paths string, so different root sets get different cache entries.

**Alternative considered:** Instance-level caching via DI — would require making `DeepIntelligenceTools` non-static or adding a service. Static cache is simpler and matches the tool's static method pattern.

### 3. FileEnumerator as shared utility

Create `Tools/FileEnumerator.cs` with a single `EnumerateFiles(roots, pattern)` method that includes the `/obj/`+`/bin/` exclusion from `EcosystemTools`. All three current implementations converge on the same logic; only `EcosystemTools` excludes build output directories, which should be the default everywhere.

### 4. HoconHelper for shared StripComment

Create `Tools/HoconHelper.cs` with the shared `StripComment` method. Both `HostingTools` and `ConfigMigrationTools` have identical implementations.

### 5. GeneratedRegex conversion

Make `HostingTools`, `StreamTools`, and `ConfigMigrationTools` partial classes and convert their `static readonly Regex` fields to `[GeneratedRegex]` partial methods. `SourceSearcher` keeps runtime regex since its pattern comes from user input.

### 6. GetAllRepos pre-computation

Change `RepoRegistry.GetAllRepos()` to return a static field computed once at class initialization via the `FrozenDictionary` that's already built at startup.

## Risks / Trade-offs

- **[Static ScanTypes cache grows unbounded]** → Acceptable: in practice there's only one set of roots per session, so at most one cache entry. The cache lives for the process lifetime which matches the MCP server's single-session model.
- **[EnsureAllSourcesAsync cache prevents picking up new versions mid-session]** → Acceptable: MCP sessions are short-lived, and version resolution already has TTL-based caching. Users restart the server to pick up new versions.
- **[Making tool classes partial]** → No risk: `TypeTools`, `DeepIntelligenceTools`, `StreamPersistenceTools`, and `EcosystemTools` are already partial. Only `HostingTools`, `StreamTools`, and `ConfigMigrationTools` need the modifier added.
