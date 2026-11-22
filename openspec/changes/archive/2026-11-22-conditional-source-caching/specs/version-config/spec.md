## ADDED Requirements

### Requirement: Configure cache TTL via environment variable

The system SHALL read the `AKR_CACHE_TTL_HOURS` environment variable to configure the
cache freshness TTL. The value SHALL be parsed as a non-negative integer representing hours.
When not set or empty, the default SHALL be 24 hours.

#### Scenario: Custom TTL configured

- **WHEN** `AKR_CACHE_TTL_HOURS` is set to `12`
- **THEN** cached metadata SHALL be considered fresh for 12 hours after `lastChecked`

#### Scenario: TTL set to zero forces conditional check

- **WHEN** `AKR_CACHE_TTL_HOURS` is set to `0`
- **THEN** cached metadata SHALL always be considered stale
- **AND** the system SHALL make a conditional request on every version resolution

#### Scenario: Default TTL when not configured

- **WHEN** `AKR_CACHE_TTL_HOURS` is not set or empty
- **THEN** the system SHALL use a default TTL of 24 hours

#### Scenario: Invalid TTL value

- **WHEN** `AKR_CACHE_TTL_HOURS` is set to a non-numeric or negative value
- **THEN** the system SHALL log a warning and use the default TTL of 24 hours
