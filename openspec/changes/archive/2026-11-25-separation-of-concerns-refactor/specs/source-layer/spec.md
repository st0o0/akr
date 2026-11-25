## ADDED Requirements

### Requirement: SourceFetcher handles HTTP and extraction
`SourceFetcher` in `Akr.Source` SHALL handle HTTP downloading of tar.gz archives from GitHub, GZip decompression, Tar extraction with path traversal protection, and atomic temp-dir + move pattern. It SHALL be stateless — no caching, no metadata, no version resolution.

#### Scenario: Fetch a tagged archive
- **WHEN** `SourceFetcher.FetchAsync(repo, version, tagFormat, targetPath, ct)` is called
- **THEN** it SHALL download the tar.gz, extract to a temp directory, and atomically move to targetPath

#### Scenario: Tag format detection consolidated
- **WHEN** tagFormat is unknown
- **THEN** SourceFetcher SHALL try plain tag first, then v-prefixed, returning which format succeeded

### Requirement: SourceCache handles metadata and LRU
`SourceCache` in `Akr.Source` SHALL manage on-disk metadata (RepoMetadata JSON), LRU eviction, version access tracking (Touch/Record), and path resolution. It SHALL own the `_cacheRoot` and all disk-state operations.

#### Scenario: Read and write metadata
- **WHEN** `SourceCache.ReadMetadata(repoName)` is called
- **THEN** it SHALL return the deserialized RepoMetadata or null if not found

#### Scenario: LRU eviction
- **WHEN** `SourceCache.EvictOldVersions(repoName)` is called and cached versions exceed MaxCachedVersions
- **THEN** the least recently accessed versions SHALL be removed from disk and metadata

### Requirement: VersionResolver handles version detection
`VersionResolver` in `Akr.Source` SHALL resolve the latest version for a repo using ProjectScanner (project-detected), cached metadata (TTL-based), or GitHub Releases API (ETag conditional requests). It SHALL be the only class making GitHub API calls for version information.

#### Scenario: Project-detected version takes priority
- **WHEN** ProjectScanner has a detected version for a repo
- **THEN** VersionResolver SHALL return that version without any HTTP calls

#### Scenario: TTL-based cache
- **WHEN** cached metadata exists and is within TTL
- **THEN** VersionResolver SHALL return the cached version without HTTP calls

### Requirement: SourcePipeline moves to Source/
`SourcePipeline` SHALL move from `Akr.Pipelines` to `Akr.Source`. It SHALL use `SourceFetcher` and `SourceCache` instead of `GitHubClient` directly.

#### Scenario: Parallel fetch uses SourceFetcher
- **WHEN** SourcePipeline fetches 4 repos in parallel
- **THEN** each repo fetch SHALL go through SourceFetcher, not GitHubClient

### Requirement: GitHubClient removed
`GitHubClient.cs` SHALL be deleted. All its responsibilities are split across SourceFetcher, SourceCache, and VersionResolver. `HttpClient` registration in McpSetup SHALL target `SourceFetcher` instead.

#### Scenario: No GitHubClient references remain
- **WHEN** the codebase is searched for `GitHubClient`
- **THEN** zero references SHALL be found outside of test helpers and git history
