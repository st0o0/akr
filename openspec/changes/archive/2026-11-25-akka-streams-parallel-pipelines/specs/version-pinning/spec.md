## MODIFIED Requirements

### Requirement: Version-specific fetch via SourcePipeline
Version-pinned tool calls SHALL route through `SourcePipeline.EnsureAllForVersionAsync(version, ct)` instead of `GitHubClient.EnsureAllSourcesForVersionAsync(version, ct)`. The version parameter semantics SHALL remain unchanged — `null` means latest/project-detected, non-null means fetch that specific version.

#### Scenario: Version-pinned call uses parallel pipeline
- **WHEN** `lookup_type("Props", version: "1.5.35")` is called and 1.5.35 is not cached
- **THEN** `SourcePipeline` SHALL fetch all 4 repos for version 1.5.35 in parallel via Akka.Streams

#### Scenario: Latest-version call uses parallel pipeline
- **WHEN** `lookup_type("Props")` is called without a version and sources are not cached
- **THEN** `SourcePipeline` SHALL fetch all repos for the latest version in parallel

### Requirement: LRU eviction unchanged
LRU cache eviction logic SHALL remain in `GitHubClient` and continue to work as before. `SourcePipeline` SHALL trigger eviction after successful parallel fetch by delegating to `GitHubClient`'s existing metadata update methods.

#### Scenario: LRU eviction after parallel fetch
- **WHEN** a parallel fetch completes for a new version and the cache exceeds `MaxCachedVersions`
- **THEN** the least recently accessed version SHALL be evicted as before
