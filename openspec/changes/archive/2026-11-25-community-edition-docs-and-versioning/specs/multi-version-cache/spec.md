## ADDED Requirements

### Requirement: Cache multiple versions per repository

The system SHALL support caching up to `MaxCachedVersions` (default 3) source versions per repository simultaneously. When a new version is fetched and the cache exceeds the limit, the least recently accessed version SHALL be evicted.

#### Scenario: Second version cached alongside first

- **WHEN** version `1.5.74` is cached for repo `akka.net`
- **AND** version `1.5.52` is requested
- **THEN** both `{CacheRoot}/akka.net/1.5.74/` and `{CacheRoot}/akka.net/1.5.52/` SHALL exist on disk

#### Scenario: LRU eviction when cache limit exceeded

- **WHEN** 3 versions are cached for a repo (1.5.52, 1.5.74, 1.5.75)
- **AND** a 4th version `1.5.76` is fetched
- **THEN** the least recently accessed version SHALL be evicted
- **AND** the remaining 3 versions SHALL be the 3 most recently accessed

#### Scenario: Accessing a version updates its last-accessed timestamp

- **WHEN** a tool call targets a cached version
- **THEN** the system SHALL update that version's last-accessed timestamp in metadata
- **AND** the version SHALL be treated as most recently used for eviction purposes

### Requirement: Track per-version metadata

`RepoMetadata` SHALL track a list of cached versions with their last-accessed timestamps, replacing the single-version tracking. The metadata file format SHALL remain JSON at `{CacheRoot}/meta/{repoName}.json`.

#### Scenario: Metadata lists all cached versions

- **WHEN** versions 1.5.52 and 1.5.74 are cached for repo `akka.net`
- **THEN** `meta/akka.net.json` SHALL contain entries for both versions with their last-accessed times

#### Scenario: Metadata survives process restart

- **WHEN** the akr process restarts
- **THEN** previously cached version metadata SHALL be readable from disk
- **AND** the system SHALL not re-download versions that are already cached
