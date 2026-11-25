## 1. Multi-version cache infrastructure

- [x] 1.1 Extend `RepoMetadata` with `CachedVersions` list (Version, LastAccessed, TagFormat) replacing single Version field
- [x] 1.2 Add `MaxCachedVersions` (default 3) to `AkrOptions` with `AKR_MAX_CACHED_VERSIONS` env binding
- [x] 1.3 Add `EnsureSourceForVersionAsync(string repo, string version)` to `GitHubClient` that skips `ResolveLatestAsync` and fetches specific tag
- [x] 1.4 Rewrite `CleanupOldVersions` to LRU eviction: keep N most recently accessed, evict oldest when limit exceeded
- [x] 1.5 Update `EnsureSourceAsync` to record last-accessed timestamp in metadata on every access
- [x] 1.6 Unit tests: multi-version caching, LRU eviction, latest never evicted, metadata persistence across restarts

## 2. Version pinning on existing tools

- [x] 2.1 Add `EnsureAllSourcesForVersionAsync(string? version)` overload that routes to version-specific or latest fetch
- [x] 2.2 Add optional `version` string parameter to all tool methods in TypeTools, StreamTools, StreamPersistenceTools
- [x] 2.3 Add optional `version` string parameter to all tool methods in ConfigMigrationTools, HostingTools, EcosystemTools, DeepIntelligenceTools
- [x] 2.4 Route `version` parameter through to `GitHubClient` — when present, call version-specific method; when absent, use existing latest
- [x] 2.5 Update source-caching to key cached results by version (version-specific results cached independently)
- [x] 2.6 Unit tests: tool with version parameter fetches correct version, tool without version uses latest, unknown version returns error

## 3. Docs fetcher and registry

- [x] 3.1 Add `akkadotnet/getakka.net` to `RepoRegistry` with a `RepoType.Docs` discriminator distinguishing it from source repos
- [x] 3.2 Extend `GitHubClient` to fetch docs repo archive and extract `src/docs/` directory to cache
- [x] 3.3 Build in-memory docs index on first access: scan `.md` files, map topic keywords from filenames and H1/H2 headings to file paths
- [x] 3.4 Cache docs index per `GitHubClient` lifetime, consistent with source caching pattern
- [x] 3.5 Unit tests: docs fetch, extraction of src/docs/ only, index building from filenames and headings

## 4. Docs tools

- [x] 4.1 Create `DocsTools` class with MCP tool registration pattern (singleton + `.WithTools<DocsTools>()`)
- [x] 4.2 Implement `docs_search(query)`: match query against topic index, return up to 5 results with heading, excerpt (~500 tokens), and source filename
- [x] 4.3 Implement `docs_example(topic, language?)`: find matching docs pages, extract fenced code blocks with surrounding context, optional language filter
- [x] 4.4 Register `DocsTools` in `McpSetup.cs`
- [x] 4.5 Unit tests: search with matches, search with no matches, example extraction, language filtering

## 5. Version comparison tools

- [x] 5.1 Create `VersionTools` class with MCP tool registration pattern
- [x] 5.2 Implement `source_diff(typeName?, configPath?, versionA, versionB)`: fetch both versions, run FindType or CollectConfig on each, return unified diff
- [x] 5.3 Handle edge cases: type missing in one version, no changes, diff truncation for large types
- [x] 5.4 Implement `migration_guide(fromVersion, toVersion, area?)`: parse CHANGELOG between versions, run config diff, search docs for affected areas, assemble structured guide
- [x] 5.5 Register `VersionTools` in `McpSetup.cs`
- [x] 5.6 Unit tests: type diff with changes, type added/removed between versions, config diff, migration guide assembly, area filtering

## 6. Build and verify

- [x] 6.1 Run `dotnet build src/akr.slnx` and `dotnet format src/akr.slnx --verify-no-changes`
- [x] 6.2 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [ ] 6.3 Manual smoke test: `docs_search("cluster sharding")` returns relevant docs (requires MCP server restart)
- [ ] 6.4 Manual smoke test: `lookup_type("Throttle", version: "1.5.52")` returns v1.5.52 source (requires MCP server restart)
- [ ] 6.5 Manual smoke test: `source_diff(typeName: "Throttle", versionA: "1.5.52", versionB: "1.5.74")` returns diff (requires MCP server restart)
- [ ] 6.6 Manual smoke test: `migration_guide(fromVersion: "1.5.0", toVersion: "1.5.74")` returns structured guide (requires MCP server restart)
