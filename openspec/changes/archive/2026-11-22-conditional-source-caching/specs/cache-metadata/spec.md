## ADDED Requirements

### Requirement: Persist version metadata to disk

The system SHALL persist resolved version metadata for each repo as a JSON file at
`{CacheRoot}/meta/{repoName}.json`. The metadata SHALL contain the resolved version string,
the ETag from the GitHub response, the UTC timestamp of the last check, and the tag format
used (plain or v-prefixed).

#### Scenario: Metadata written after successful version resolution

- **WHEN** the system resolves the latest version for repo `akkadotnet/akka.net`
- **AND** GitHub responds with version `1.5.75` and ETag `W/"abc123"`
- **THEN** the system SHALL write `{CacheRoot}/meta/akka.net.json` containing
  `version: "1.5.75"`, `etag: "W/\"abc123\""`, `lastChecked` as the current UTC time,
  and `tagFormat: "plain"`

#### Scenario: Metadata written atomically

- **WHEN** the system writes metadata for a repo
- **THEN** it SHALL write to a temporary file first and rename to the final path
- **AND** a crash during write SHALL NOT corrupt existing metadata

### Requirement: Read cached metadata for version resolution

The system SHALL read cached metadata from disk before making any GitHub API call.
If the metadata file exists and is valid JSON, its contents SHALL be used for freshness
checks.

#### Scenario: Valid metadata on disk

- **WHEN** `{CacheRoot}/meta/akka.net.json` exists with valid JSON
- **THEN** the system SHALL parse it and use the cached version and ETag

#### Scenario: Missing or corrupt metadata

- **WHEN** the metadata file does not exist or contains invalid JSON
- **THEN** the system SHALL proceed as if no cache exists (full API request)
- **AND** SHALL NOT throw an error

### Requirement: TTL-based freshness check

The system SHALL consider cached metadata fresh when `lastChecked` is within the configured
TTL window. When metadata is fresh, the system SHALL return the cached version without
making any HTTP request.

#### Scenario: Metadata is fresh

- **WHEN** cached metadata has `lastChecked` less than the TTL ago
- **THEN** the system SHALL return the cached `version` immediately
- **AND** SHALL NOT make any GitHub API call

#### Scenario: Metadata is stale

- **WHEN** cached metadata has `lastChecked` more than the TTL ago
- **THEN** the system SHALL proceed to conditional HTTP validation

### Requirement: Conditional HTTP validation with ETag

When cached metadata is stale, the system SHALL send a conditional request to
`GET /repos/{repo}/releases/latest` with the `If-None-Match` header set to the cached ETag.

#### Scenario: Version unchanged (304 Not Modified)

- **WHEN** the system sends a conditional request with the cached ETag
- **AND** GitHub responds with 304 Not Modified
- **THEN** the system SHALL update `lastChecked` to the current UTC time
- **AND** SHALL return the cached version

#### Scenario: New version available (200 OK)

- **WHEN** the system sends a conditional request
- **AND** GitHub responds with 200 OK and a new `tag_name`
- **THEN** the system SHALL update the metadata with the new version, ETag, and timestamp
- **AND** SHALL return the new version

### Requirement: Cache tag format

The system SHALL store the tag format (`plain` or `v-prefixed`) in metadata after a
successful source fetch. On subsequent fetches, the system SHALL use the cached format
to avoid fallback attempts.

#### Scenario: Tag format cached after successful fetch

- **WHEN** source is successfully fetched using tag `1.5.75` (without v-prefix)
- **THEN** metadata SHALL record `tagFormat: "plain"`

#### Scenario: Cached tag format used on next fetch

- **WHEN** metadata contains `tagFormat: "plain"` and a new version `1.5.76` is resolved
- **THEN** the system SHALL attempt tag `1.5.76` first without trying `v1.5.76`
