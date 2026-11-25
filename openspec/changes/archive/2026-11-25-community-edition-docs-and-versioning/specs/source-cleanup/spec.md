## MODIFIED Requirements

### Requirement: Delete old source version after update

When a new version is successfully downloaded and extracted, the system SHALL evict the least recently accessed version if the cache exceeds `MaxCachedVersions` (default 3) per repo. Multiple versions SHALL coexist on disk up to the configured limit.

#### Scenario: Old version retained when under cache limit

- **WHEN** version `1.5.75` is cached for repo `akka.net`
- **AND** version `1.5.76` is successfully downloaded and extracted
- **AND** only 2 versions are cached (under the limit of 3)
- **THEN** both `{CacheRoot}/akka.net/1.5.75/` and `{CacheRoot}/akka.net/1.5.76/` SHALL remain

#### Scenario: LRU version evicted when over cache limit

- **WHEN** 3 versions are cached for repo `akka.net`
- **AND** a 4th version is successfully downloaded and extracted
- **THEN** the system SHALL delete the least recently accessed version's directory
- **AND** only 3 version directories SHALL remain

#### Scenario: Cleanup failure does not block operation

- **WHEN** deletion of an evicted version directory fails (e.g., locked file)
- **THEN** the system SHALL log a warning
- **AND** SHALL NOT throw an exception
- **AND** the new version SHALL still be usable

#### Scenario: No old version to clean up

- **WHEN** a version is downloaded for a repo that has no previous cached version
- **THEN** no cleanup SHALL occur and no error SHALL be raised
