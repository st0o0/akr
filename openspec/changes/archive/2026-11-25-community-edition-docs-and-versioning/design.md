## Context

akr currently fetches source code from 4 Akka.NET GitHub repos, caches one version per repo, and exposes 33 MCP tools. All intelligence comes from C# source parsing and `reference.conf` files. The `auto-detect-akka-version` change just landed, adding `ProjectScanner`, `ProjectContext`, `AkrOptions` with `IOptions<T>`, and `set_project_context` tool. `GitHubClient` already supports fetching arbitrary versions via tag, but `CleanupOldVersions` deletes everything except the current version.

The getakka.net documentation lives in `akkadotnet/getakka.net` on GitHub (archived 2019, read-only). Documentation Markdown files are under `src/docs/` with ~64+ files organized in topic subdirectories (clustering, concepts, persistence, streams, testing, working-with-actors). The repo uses an older Assemble/Handlebars static site generator — only the Markdown matters for akr.

## Goals / Non-Goals

**Goals:**
- Docs tools work out of the box with zero config (fetch on first use, like source)
- Multi-version cache allows comparing two versions without re-downloading
- Version parameter is optional and non-breaking on all existing tools
- `source_diff` and `migration_guide` combine multiple data sources into actionable output
- Token-efficient: docs search returns excerpts, not full pages

**Non-Goals:**
- Scraping the rendered getakka.net website (use Markdown sources from GitHub)
- Full-text search indexing (keyword/filename matching is sufficient for v1)
- Docs version pinning (docs are concept-focused, not version-specific — always use latest)
- Interactive version comparison UI
- Supporting non-Akka.NET documentation

## Decisions

### Decision 1: Reuse GitHubClient for docs fetching

**Choice**: Extend `GitHubClient` to handle docs repos rather than creating a separate `DocsClient`. The docs repo follows the same fetch-cache-extract pattern as source repos. Add a `RepoType` discriminator (Source vs Docs) to `RepoRegistry` entries so tools know which repos contain what.

**Rationale**: The existing `FetchArchiveAsync` → extract → cache pipeline works identically for docs. A separate client would duplicate the HTTP, caching, and cleanup logic. The only difference is what tools do *after* fetching — source tools parse C#, docs tools scan Markdown.

**Alternative considered**: Separate `DocsClient` class — rejected as it would duplicate ~80% of `GitHubClient` with only the content-scanning logic differing.

### Decision 2: Simple keyword matching for docs search (v1)

**Choice**: Build a topic index from Markdown filenames and H1/H2 headings. `docs_search` matches the query against this index using case-insensitive substring matching, ranked by: exact filename match > heading match > body keyword match.

**Rationale**: The docs corpus is small (~64+ files). Full-text indexing (Lucene, etc.) adds a heavy dependency for marginal gain. Filename-based matching works well because getakka.net files are named descriptively (`Cluster Sharding.md`, `Ask Pattern.md`, `Persistence.md`). Heading extraction adds another useful signal without complexity.

**Alternative considered**: Embedding-based semantic search — rejected as overkill for a small corpus and would require an additional dependency or external API call.

### Decision 3: Multi-version metadata in RepoMetadata

**Choice**: Replace the single `Version` field in `RepoMetadata` with a `CachedVersions` list of `{Version, LastAccessed, TagFormat}` entries. `CleanupOldVersions` becomes LRU-based, evicting when the list exceeds `MaxCachedVersions` (configurable via `AkrOptions`, default 3).

**Rationale**: Minimal schema change. The metadata file already tracks per-repo state — extending it to track multiple versions is natural. LRU with a small bound (3) keeps disk usage predictable: ~50MB/version × 3 × 5 repos = ~750MB max.

**Alternative considered**: Unlimited cache with manual cleanup command — rejected because users forget to clean up and disk usage becomes unpredictable.

### Decision 4: Version parameter threaded through EnsureSourceAsync

**Choice**: Add an optional `version` parameter to `EnsureSourceAsync` and `EnsureAllSourcesAsync`. When provided, skip `ResolveLatestAsync` and directly fetch the archive for the specified tag. Each tool method adds `version` as an optional MCP parameter and passes it through.

**Rationale**: The version flows through a single code path: tool → `EnsureAllSourcesAsync(version)` → `EnsureSourceAsync(repo, version)` → `FetchArchiveAsync(repo, tag)`. The last step already works for arbitrary tags. The only new logic is skipping the resolution step and keying the cache by version.

### Decision 5: source_diff uses existing tool methods internally

**Choice**: `source_diff` for types calls `TypeTools.FindType` on each version's source tree and diffs the results. For config, it calls `ConfigMigrationTools` logic on each version. The diff output is unified diff format.

**Rationale**: Reuses existing parsing logic rather than duplicating it. The only new code is the diff computation, which is a straightforward line-by-line comparison.

### Decision 6: migration_guide aggregates existing tools

**Choice**: `migration_guide` is a composite tool that orchestrates: (1) parse CHANGELOG/RELEASE_NOTES between versionA and versionB, (2) run `source_diff` on config, (3) search docs for affected areas. It assembles the results into a structured Markdown response.

**Rationale**: Each piece already exists or is being added in this change. The migration guide's value is in the *combination* and *filtering*, not in new data sources.

### Decision 7: New tool classes — DocsTools and VersionTools

**Choice**: Two new tool classes registered in `McpSetup.cs`:
- `DocsTools`: `docs_search`, `docs_example`
- `VersionTools`: `source_diff`, `migration_guide`

**Rationale**: Follows the existing pattern of grouping related tools into classes. Version tools are cross-cutting (they operate on multiple versions of source *and* docs) so they belong in their own class rather than being added to existing tool classes.

## Risks / Trade-offs

- **getakka.net repo is archived (2019)**: Documentation may be outdated for newer Akka.NET features. Mitigation: docs are supplementary, not authoritative — source tools remain the primary intelligence. If Petabridge publishes docs elsewhere in the future, the docs fetcher can be pointed at a new repo.

- **Docs corpus quality**: Some Markdown files may have broken formatting or outdated examples. Mitigation: return raw Markdown excerpts and let the AI agent interpret them. No rendering or validation needed.

- **Cache disk usage growth**: Multi-version cache increases max disk from ~250MB to ~750MB. Mitigation: `MaxCachedVersions` is configurable; default of 3 is conservative. Add a log message when evicting versions.

- **Version parameter on 33 tools**: Adding a parameter to every tool is a large surface change. Mitigation: the parameter is optional with no default change — zero risk of breaking existing behavior. The routing is centralized in `EnsureSourceAsync`, not duplicated per tool.

- **Diff quality for large types**: Some Akka.NET types span hundreds of lines. A unified diff may be large and token-expensive. Mitigation: truncate diff output at a configurable max length with a note indicating truncation.
