## ADDED Requirements

### Requirement: Delete old source version after update

When a new version is successfully downloaded and extracted, the system SHALL delete the
previous version's source directory. Only one version per repo SHALL exist on disk at a time.

#### Scenario: Old version removed after successful update

- **WHEN** version `1.5.75` is cached for repo `akka.net`
- **AND** version `1.5.76` is successfully downloaded and extracted
- **THEN** the system SHALL delete `{CacheRoot}/akka.net/1.5.75/`
- **AND** only `{CacheRoot}/akka.net/1.5.76/` SHALL remain

#### Scenario: Cleanup failure does not block operation

- **WHEN** deletion of the old version directory fails (e.g., locked file)
- **THEN** the system SHALL log a warning
- **AND** SHALL NOT throw an exception
- **AND** the new version SHALL still be usable

#### Scenario: No old version to clean up

- **WHEN** a version is downloaded for a repo that has no previous cached version
- **THEN** no cleanup SHALL occur and no error SHALL be raised
