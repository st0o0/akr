## ADDED Requirements

### Requirement: Centralized configuration via AkrOptions

The system SHALL define an `AkrOptions` record in `Akr.Configuration` as the single source of truth for all configuration values. The record SHALL have the following properties:
- `CacheRoot` (string): path to the source cache directory, defaulting to `~/.akr/sources`
- `CacheTtlHours` (int): cache freshness TTL in hours, defaulting to 24
- `ProjectPath` (string?): optional explicit project path for version detection

#### Scenario: Default values when no environment variables set

- **WHEN** no `AKR_CACHE_TTL_HOURS`, `AKR_PROJECT_PATH`, or `AKR_CACHE_ROOT` environment variables are set
- **THEN** `CacheRoot` SHALL default to `{UserProfile}/.akr/sources`
- **AND** `CacheTtlHours` SHALL default to 24
- **AND** `ProjectPath` SHALL be null

#### Scenario: Environment variables override defaults

- **WHEN** `AKR_CACHE_TTL_HOURS` is set to `12`
- **AND** `AKR_PROJECT_PATH` is set to `/my/project`
- **THEN** `CacheTtlHours` SHALL be 12
- **AND** `ProjectPath` SHALL be `/my/project`

#### Scenario: Invalid TTL value uses default

- **WHEN** `AKR_CACHE_TTL_HOURS` is set to `abc`
- **THEN** `CacheTtlHours` SHALL default to 24
- **AND** the system SHALL log a warning

### Requirement: Register AkrOptions via IOptions pattern

The system SHALL register `AkrOptions` using `services.Configure<AkrOptions>()` so that any service can inject `IOptions<AkrOptions>`. No service SHALL read environment variables directly.

#### Scenario: IOptions injection in GitHubClient

- **WHEN** `GitHubClient` is resolved from DI
- **THEN** it SHALL receive `IOptions<AkrOptions>` with the bound configuration
- **AND** SHALL NOT call `Environment.GetEnvironmentVariable`

### Requirement: Register TimeProvider in DI

The system SHALL register `TimeProvider.System` as a singleton. Services needing time SHALL inject `TimeProvider` instead of using `TimeProvider.System` directly.

#### Scenario: TimeProvider available via DI

- **WHEN** any service requires `TimeProvider`
- **THEN** DI SHALL resolve `TimeProvider.System` in production
- **AND** tests SHALL be able to substitute a fake implementation
