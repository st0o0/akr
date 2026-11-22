## MODIFIED Requirements

### Requirement: Resolve latest version

The system SHALL resolve the latest release version for a repository by first checking
disk-cached metadata with TTL-based freshness. When metadata is fresh (within TTL), the
system SHALL return the cached version without any HTTP request. When metadata is stale
or missing, the system SHALL query the GitHub API — using a conditional request with ETag
when cached metadata exists.

#### Scenario: Latest version resolution from fresh cache

- **WHEN** no version is configured for a package
- **AND** a tool requests source for that package
- **AND** cached metadata exists and is fresh (within TTL)
- **THEN** the system SHALL return the cached version without making any GitHub API call

#### Scenario: Latest version resolution with stale cache

- **WHEN** no version is configured for a package
- **AND** a tool requests source for that package
- **AND** cached metadata exists but is stale (beyond TTL)
- **THEN** the system SHALL send a conditional request with the cached ETag
- **AND** use the response (304 → keep cached version, 200 → use new version)

#### Scenario: Latest version resolution without cache

- **WHEN** no version is configured for a package
- **AND** a tool requests source for that package
- **AND** no cached metadata exists
- **THEN** the system SHALL query the GitHub API for the latest release tag
- **AND** persist the metadata to disk

### Requirement: Cache is immutable

- **WHEN** source for a version is already cached
- **THEN** the system SHALL NOT re-download or re-extract it
- **AND** SHALL return the existing cache path
