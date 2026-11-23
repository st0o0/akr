## Purpose

Download and extract Akka.NET source archives from GitHub.

## Requirements

### Requirement: Download source archive from GitHub

The system SHALL download source archives from GitHub using the tarball archive URL:
`https://github.com/{owner}/{repo}/archive/refs/tags/{tag}.tar.gz`

#### Scenario: Successful download for existing tag

- **WHEN** fetching source for repo `akkadotnet/akka.net` at tag `1.5.30`
- **THEN** the system SHALL download the tarball from the archive URL
- **AND** extract the contents to the local cache directory

#### Scenario: Tag not found with fallback

- **WHEN** fetching source for a version where tag `{version}` does not exist
- **AND** tag `v{version}` does exist
- **THEN** the system SHALL fall back to the `v{version}` tag and download successfully

#### Scenario: Tag not found at all

- **WHEN** fetching source for a version where neither `{version}` nor `v{version}` exists
- **THEN** the system SHALL return an error indicating the version was not found

### Requirement: Extract archive to local cache

The system SHALL extract downloaded tarballs to `{UserProfile}/.akka-mcp/sources/{repo}/{version}/`.
The cache directory SHALL be created if it does not exist.

#### Scenario: Cache directory structure

- **WHEN** extracting repo `akka.net` version `1.5.30`
- **THEN** source files SHALL be available under `~/.akka-mcp/sources/akka.net/1.5.30/`
- **AND** the internal archive prefix directory (e.g. `akka.net-1.5.30/`) SHALL be stripped

#### Scenario: Cache is immutable

- **WHEN** source for a version is already cached
- **THEN** the system SHALL NOT re-download or re-extract it
- **AND** SHALL return the existing cache path

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
