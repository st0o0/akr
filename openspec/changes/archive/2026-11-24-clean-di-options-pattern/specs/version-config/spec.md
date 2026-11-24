## MODIFIED Requirements

### Requirement: Parse version configuration from environment

The system SHALL read Akka package versions from the `AKKA_VERSIONS` environment variable
via `AkrOptions` binding, not via direct `Environment.GetEnvironmentVariable` calls in service code.
The format SHALL be comma-separated `Package=Version` pairs. When not set, the system SHALL use project detection or latest-mode.

#### Scenario: Valid configuration with multiple packages

- **WHEN** `AKKA_VERSIONS` is set to `Akka=1.5.30,Akka.Hosting=1.5.71`
- **THEN** the system SHALL resolve `Akka` to version `1.5.30` and `Akka.Hosting` to version `1.5.71`

#### Scenario: Empty or missing configuration

- **WHEN** `AKKA_VERSIONS` is not set
- **THEN** the system SHALL check project detection before falling back to latest

### Requirement: Configure cache TTL via environment variable

The system SHALL read `AKR_CACHE_TTL_HOURS` via `AkrOptions.CacheTtlHours` (bound from env var during options configuration). No service SHALL parse this env var directly.

#### Scenario: Custom TTL configured

- **WHEN** `AKR_CACHE_TTL_HOURS` is set to `12`
- **THEN** `AkrOptions.CacheTtlHours` SHALL be 12
- **AND** `GitHubClient` SHALL use this value from `IOptions<AkrOptions>`

#### Scenario: Default TTL

- **WHEN** `AKR_CACHE_TTL_HOURS` is not set
- **THEN** `AkrOptions.CacheTtlHours` SHALL default to 24
