## MODIFIED Requirements

### Requirement: GitHubClient as HTTP and cache worker
`GitHubClient` SHALL retain responsibility for single-repo HTTP fetching (`EnsureSourceAsync`, `EnsureSourceForVersionAsync`), tar.gz extraction, and local cache management. It SHALL lose responsibility for multi-repo orchestration — `EnsureAllSourcesAsync` and `EnsureAllSourcesForVersionAsync` SHALL be removed or made internal, replaced by `SourcePipeline`.

#### Scenario: Single-repo fetch still works via GitHubClient
- **WHEN** `SourcePipeline` calls `GitHubClient.EnsureSourceForVersionAsync("akkadotnet/akka.net", "1.5.35")` for a single repo
- **THEN** `GitHubClient` SHALL download, extract, and cache the archive as before

#### Scenario: Multi-repo orchestration moved to SourcePipeline
- **WHEN** a tool needs all repos for a version
- **THEN** it SHALL call `SourcePipeline.EnsureAllForVersionAsync`, NOT `GitHubClient.EnsureAllSourcesForVersionAsync`

### Requirement: Cache lookup remains in GitHubClient
`GitHubClient` SHALL continue to manage the version cache dictionary (`_cachedRootsByVersion`) and metadata persistence. `SourcePipeline` SHALL query the cache via `GitHubClient` before materializing a fetch stream.

#### Scenario: Cache hit skips stream materialization
- **WHEN** `SourcePipeline` checks for a cached version and it exists
- **THEN** no Akka.Streams pipeline SHALL be materialized
