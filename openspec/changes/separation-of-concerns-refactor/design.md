## Context

akr is a single-project MCP server with 37 tools, ~5400 lines of C# across 20 files. The codebase grew organically — GitHubClient accumulated responsibilities, tool files were grouped by when they were added rather than by domain, and namespace names describe implementation mechanisms. akkadotnet/akka.net now has current docs in `docs/articles/` making the archived getakka.net (2019) redundant.

## Goals / Non-Goals

**Goals:**
- Split GitHubClient into classes with single responsibilities
- Group tools by Akka.NET domain (types, config, hosting, streams, persistence, serialization, cluster, testkit)
- Name folders after domain concepts, Akka-style (`Source/`, `Scanning/`) not mechanisms (`Pipelines/`, `Analysis/`)
- Replace stale getakka.net docs with current mono-repo docs
- Maintain all 37 tool names, signatures, and behavior unchanged

**Non-Goals:**
- Changing any MCP tool behavior or output format
- Adding new tools or removing tools
- Changing the testing strategy
- Performance optimizations (beyond removing one unnecessary fetch)

## Decisions

### 1. GitHubClient splits into 3 classes in Source/
**Choice**: `SourceFetcher` (HTTP + GZip + Tar extraction + atomic temp-dir), `SourceCache` (metadata JSON persistence, LRU eviction, path resolution, version access tracking), `VersionResolver` (GitHub Releases API, ETag caching, TTL, ProjectScanner integration).
**Why**: Each class has one reason to change. SourceFetcher changes when HTTP/extraction logic changes. SourceCache changes when caching strategy changes. VersionResolver changes when version detection changes. Currently all three change together in one 509-line file.
**Risk**: More cross-class communication. Mitigated by SourceFetcher being stateless (pure I/O), SourceCache owning all disk state, and VersionResolver being the only GitHub API consumer.

### 2. SourcePipeline stays, DocsPipeline goes
**Choice**: `SourcePipeline` moves from `Pipelines/` to `Source/` — it orchestrates parallel fetching using the new SourceFetcher. `DocsPipeline` is deleted — docs come from the cached akka.net repo, so DocsIndex just scans already-downloaded files. No separate fetch, no separate pipeline.
**Why**: DocsPipeline's only purpose was fetching getakka.net and building the index. With docs in the mono-repo, the fetch step disappears and the index build is a simple scan of cached source roots.

### 3. Analysis/ → Scanning/
**Choice**: Rename folder and namespace from `Akr.Analysis` to `Akr.Scanning`.
**Why**: "Analysis" is vague — could mean anything. "Scanning" describes what happens: these classes scan cached source files for types, patterns, headings, and config. Matches Akka.NET naming philosophy where `Dispatch/` = dispatching, `Routing/` = routing, `Scanning/` = scanning.

### 4. Tools grouped by Akka domain
**Choice**: 12 tool files, each named after the Akka.NET domain it covers. EcosystemTools (733 lines) splits into SerializationTools, ClusterTools, TestKitTools. StreamPersistenceTools splits into StreamTools and PersistenceTools. `search_source` moves from StreamTools to SearchTools. Hosting tools consolidate from 3 files into 1.
**Why**: A developer looking for `serializer_bindings` should find it in `SerializationTools.cs`, not in a 733-line file called "Ecosystem". Each file answers "what domain is this?" by its name.

### 5. FileService moves to root namespace
**Choice**: `FileService.cs` moves from `Akr.Tools` to `Akr` (root namespace).
**Why**: FileService is a shared infrastructure service used by Source/, Scanning/, and Tools/. It's not a tool. Putting it in `Akr.Tools` forces `Source/` to reference `Tools/` which creates a circular-feeling dependency.

### 6. getakka.net replaced by mono-repo docs
**Choice**: Remove `DocsRepo` constant, `EnsureDocsAsync`, and all branch-based fetching. Change DocsIndex to scan `docs/articles/` from the cached akka.net source root. Docs become version-matched (each version's docs come from that version's archive).
**Why**: akkadotnet/akka.net has 158 current markdown files under `docs/articles/` with DocFX structure. getakka.net has 140 files from 2019 missing 5 years of content (Hosting, Aspire, Reminders, modern persistence). The mono-repo docs are strictly superior and already cached.

### 7. Duplicate fetch logic consolidated
**Choice**: `EnsureSourceAsync` and `EnsureSourceForVersionAsync` share ~80% identical code (tag-format detection, temp-dir, error handling). Consolidate into a single `SourceFetcher.FetchAsync(repo, version, tagFormat)` method that both paths call.
**Why**: Eliminates 50+ lines of duplication and the subtle divergence risk between the two methods.

## Risks / Trade-offs

- **[Every file changes]** → Namespace renames touch every `using` statement. Mitigated: purely mechanical, no logic changes. IDE rename + format handles most of it.
- **[Test breakage]** → Constructor signatures change for split classes. Mitigated: tests use `null!` for pipeline injection already, same pattern for new classes.
- **[Docs path change]** → `src/docs/` → `docs/articles/`. Mitigated: DocsIndex already abstracts the path. Only the root path changes.
- **[Git blame lost]** → File moves lose history in `git blame`. Mitigated: `git log --follow` still tracks. Version 0.x, acceptable.
