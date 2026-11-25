## ADDED Requirements

### Requirement: Docs sourced from akkadotnet/akka.net
Documentation SHALL be read from `docs/articles/` in the cached `akkadotnet/akka.net` source root. The archived `akkadotnet/getakka.net` repo SHALL no longer be fetched.

#### Scenario: DocsIndex uses mono-repo docs
- **WHEN** `docs_search("cluster sharding")` is called
- **THEN** results SHALL come from `docs/articles/` in the akka.net cache, not from getakka.net

#### Scenario: Version-matched docs
- **WHEN** `docs_search` is called with a pinned version (e.g. 1.5.30)
- **THEN** docs SHALL be from that version's cached archive, matching the source code version

### Requirement: getakka.net removed
`DocsRepo` constant, `EnsureDocsAsync` method, and all branch-based fetching logic for getakka.net SHALL be removed. `RepoRegistry` SHALL not reference `akkadotnet/getakka.net`.

#### Scenario: No getakka.net references
- **WHEN** the codebase is searched for `getakka.net`
- **THEN** zero references SHALL be found outside of git history

### Requirement: DocsPipeline removed
`DocsPipeline.cs` SHALL be deleted. The docs index build happens inline — DocsTools calls DocsIndex to scan the cached source root directly. No separate pipeline or fetch step needed.

#### Scenario: No DocsPipeline references
- **WHEN** the codebase is searched for `DocsPipeline`
- **THEN** zero references SHALL be found
