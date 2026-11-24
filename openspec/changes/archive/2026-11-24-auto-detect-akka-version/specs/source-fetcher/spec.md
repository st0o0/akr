## MODIFIED Requirements

### Requirement: Resolve latest version

The system SHALL resolve the version for a repository by first checking project-detected
versions, then environment-configured versions, before falling back to the GitHub API.
When using project-detected or environment-configured versions, the system SHALL skip the
GitHub latest-release API call entirely. When no configured version exists, the system SHALL
check disk-cached metadata with TTL-based freshness, using conditional requests with ETag
when cached metadata is stale.

#### Scenario: Version resolved from project detection

- **WHEN** a tool requests source for a package
- **AND** a project-detected version exists for that package's repo
- **THEN** the system SHALL use the project-detected version without any GitHub API call

#### Scenario: Version resolved from environment configuration

- **WHEN** a tool requests source for a package
- **AND** no project-detected version exists
- **AND** an `AKKA_VERSIONS` configured version exists for that package
- **THEN** the system SHALL use the configured version without any GitHub API call

#### Scenario: Latest version resolution from fresh cache

- **WHEN** no version is configured or detected for a package
- **AND** a tool requests source for that package
- **AND** cached metadata exists and is fresh (within TTL)
- **THEN** the system SHALL return the cached version without making any GitHub API call

#### Scenario: Latest version resolution with stale cache

- **WHEN** no version is configured or detected for a package
- **AND** a tool requests source for that package
- **AND** cached metadata exists but is stale (beyond TTL)
- **THEN** the system SHALL send a conditional request with the cached ETag
- **AND** use the response (304 → keep cached version, 200 → use new version)

#### Scenario: Latest version resolution without cache

- **WHEN** no version is configured or detected for a package
- **AND** a tool requests source for that package
- **AND** no cached metadata exists
- **THEN** the system SHALL query the GitHub API for the latest release tag
- **AND** persist the metadata to disk
