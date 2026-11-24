## MODIFIED Requirements

### Requirement: Track multiple cached versions

The cache metadata system SHALL track metadata for each cached version per repo, not just the latest resolved version. Each cached version SHALL have its own last-accessed timestamp.

#### Scenario: Metadata exists for pinned version
- **WHEN** version 1.5.52 is fetched and cached
- **THEN** metadata SHALL record its cache path and last-accessed timestamp

#### Scenario: Access updates timestamp
- **WHEN** a tool accesses a previously cached version
- **THEN** the version's last-accessed timestamp SHALL be updated

#### Scenario: Metadata survives across sessions
- **WHEN** akr restarts after caching multiple versions
- **THEN** all cached version metadata SHALL be readable from disk
