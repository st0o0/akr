## ADDED Requirements

### Requirement: SourcePipeline as fetch orchestrator
The system SHALL provide a `SourcePipeline` class in `Pipelines/` that orchestrates parallel fetching of GitHub source archives. It SHALL accept `GitHubClient` and `ActorSystem` via constructor injection and be registered as a singleton in DI.

#### Scenario: Singleton registration
- **WHEN** the DI container is built
- **THEN** `SourcePipeline` SHALL be resolvable as a singleton

### Requirement: Parallel repo fetching via Akka.Streams
`SourcePipeline.EnsureAllForVersionAsync(string? version, CancellationToken)` SHALL fetch all repos in parallel using `Source.From(repos).SelectAsyncUnordered(parallelism, fetch).RunWith(Sink.Seq, actorSystem)`. The default parallelism SHALL be 4 (one per repo).

#### Scenario: Parallel fetch of 4 repos for uncached version
- **WHEN** `EnsureAllForVersionAsync("1.5.35")` is called and version 1.5.35 is not cached
- **THEN** all 4 repo fetches SHALL execute concurrently
- **THEN** total wall-clock time SHALL be approximately the duration of the slowest single fetch (~15s), not the sum (~60s)

#### Scenario: Cached version returns immediately
- **WHEN** `EnsureAllForVersionAsync("1.5.30")` is called and version 1.5.30 is already cached
- **THEN** the cached roots SHALL be returned without materializing a stream

#### Scenario: Null version delegates to latest
- **WHEN** `EnsureAllForVersionAsync(null)` is called
- **THEN** the pipeline SHALL delegate to the latest-version fetch path using `EnsureAllSourcesAsync`

### Requirement: Extraction throttling
Within the fetch stream, HTTP downloads SHALL run with parallelism 4 (`SelectAsyncUnordered`) but disk extraction SHALL run with parallelism 1 (`SelectAsync(1, ...)`) to avoid I/O contention.

#### Scenario: Downloads overlap but extractions are sequential
- **WHEN** 4 repos are being fetched in parallel
- **THEN** HTTP downloads SHALL overlap
- **THEN** tar.gz extractions to disk SHALL execute one at a time

### Requirement: Short-lived stream lifecycle
Each call to `EnsureAllForVersionAsync` SHALL materialize a new stream, run it to completion, and let it be garbage collected. No persistent stream infrastructure SHALL be maintained between calls.

#### Scenario: Stream is not retained after completion
- **WHEN** `EnsureAllForVersionAsync` completes
- **THEN** no stream actors or materialized resources SHALL remain active

### Requirement: Cache and metadata integration
After successful parallel fetch, `SourcePipeline` SHALL update the version cache (`_cachedRootsByVersion`) and trigger LRU eviction via the existing `GitHubClient` metadata mechanisms.

#### Scenario: Version recorded in metadata after parallel fetch
- **WHEN** a parallel fetch completes for version "1.5.35"
- **THEN** the version SHALL be recorded in `RepoMetadata.CachedVersions` with a current `LastAccessed` timestamp

### Requirement: Tools route through SourcePipeline
All 37 MCP tool methods that currently call `GitHubClient.EnsureAllSourcesForVersionAsync` SHALL be updated to call `SourcePipeline.EnsureAllForVersionAsync` instead. The method signature SHALL remain compatible (`string? version, CancellationToken`).

#### Scenario: Tool calls pipeline instead of GitHubClient
- **WHEN** any MCP tool needs source code for a version
- **THEN** it SHALL call `SourcePipeline.EnsureAllForVersionAsync(version, ct)` instead of `GitHubClient.EnsureAllSourcesForVersionAsync(version, ct)`
