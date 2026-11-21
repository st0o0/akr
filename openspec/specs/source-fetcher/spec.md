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

### Requirement: Support authenticated requests

The system SHALL use the `GITHUB_TOKEN` environment variable for authenticated GitHub API
requests when available. Without a token, requests SHALL be unauthenticated.

#### Scenario: Authenticated request increases rate limit

- **WHEN** `GITHUB_TOKEN` is set
- **THEN** HTTP requests to GitHub SHALL include the `Authorization: Bearer {token}` header

#### Scenario: Unauthenticated fallback

- **WHEN** `GITHUB_TOKEN` is not set
- **THEN** HTTP requests SHALL proceed without authentication (60 req/hour limit)

### Requirement: Resolve latest version

The system SHALL resolve the latest release version for a repository via the GitHub Releases
API when no version is configured.

#### Scenario: Latest version resolution

- **WHEN** no version is configured for a package
- **AND** a tool requests source for that package
- **THEN** the system SHALL query the GitHub API for the latest release tag
- **AND** use that tag for fetching
