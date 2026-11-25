## ADDED Requirements

### Requirement: SourceCache metadata round-trip
Tests SHALL verify that `WriteMetadata` and `ReadMetadata` produce consistent results using MockFileSystem.

#### Scenario: Write then read metadata
- **WHEN** `WriteMetadata("repo", metadata)` is called followed by `ReadMetadata("repo")`
- **THEN** the returned metadata matches the written metadata

#### Scenario: Read nonexistent metadata
- **WHEN** `ReadMetadata("repo")` is called with no metadata file on disk
- **THEN** null is returned

### Requirement: SourceCache touch access updates timestamp
Tests SHALL verify `TouchVersionAccess` updates `LastAccessed` using FakeTimeProvider.

#### Scenario: Touch updates LastAccessed to current fake time
- **WHEN** FakeTimeProvider is at T0 and a version is recorded, then time advances to T1 and `TouchVersionAccess` is called
- **THEN** the version's `LastAccessed` equals T1

#### Scenario: Touch nonexistent version is no-op
- **WHEN** `TouchVersionAccess` is called for a version not in metadata
- **THEN** metadata remains unchanged

### Requirement: SourceCache LRU eviction
Tests SHALL verify `EvictOldVersions` removes least-recently-accessed versions using FakeTimeProvider.

#### Scenario: Evict oldest when over limit
- **WHEN** `MaxCachedVersions` is 2 and 3 versions exist with different LastAccessed timestamps
- **THEN** `EvictOldVersions` removes the version with the oldest LastAccessed
- **THEN** the version's directory is deleted from MockFileSystem

#### Scenario: No eviction when under limit
- **WHEN** `MaxCachedVersions` is 3 and only 2 versions exist
- **THEN** `EvictOldVersions` removes nothing

### Requirement: SourceCache record version access
Tests SHALL verify `RecordVersionAccess` creates or updates version entries.

#### Scenario: Record new version
- **WHEN** `RecordVersionAccess("repo", "1.5.0", "v{0}")` is called
- **THEN** metadata contains a version entry for "1.5.0" with LastAccessed at the current fake time

#### Scenario: Record existing version updates timestamp
- **WHEN** a version is recorded at T0, then time advances to T1 and the same version is recorded again
- **THEN** the version's LastAccessed equals T1
