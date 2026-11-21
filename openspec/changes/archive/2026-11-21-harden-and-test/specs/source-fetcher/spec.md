## MODIFIED Requirements

### Requirement: Extract archive to local cache

The system SHALL extract downloaded tarballs atomically: first to a temporary directory,
then renamed to the final cache path on success. Failed extractions SHALL NOT leave
partial cache directories.

#### Scenario: Failed extraction leaves no trace

- **WHEN** archive extraction fails mid-way (network error, corrupt archive)
- **THEN** the cache directory for that version SHALL NOT exist
- **AND** the next request for that version SHALL attempt a fresh download

#### Scenario: Successful extraction is atomic

- **WHEN** archive extraction succeeds
- **THEN** the cache directory SHALL appear atomically via directory rename
- **AND** concurrent readers SHALL never see a partial directory

### Requirement: Handle HTTP errors gracefully

The system SHALL handle non-success HTTP responses with specific error messages instead of
generic exceptions.

#### Scenario: Rate limit response

- **WHEN** GitHub returns HTTP 403 or 429
- **THEN** the system SHALL throw a SourceFetchException indicating rate limiting

#### Scenario: Server error response

- **WHEN** GitHub returns HTTP 500 or other server error
- **THEN** the system SHALL throw a SourceFetchException with the status code
