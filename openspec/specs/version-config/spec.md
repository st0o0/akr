## Purpose

Parse and validate Akka.NET package version configuration from server environment variables.

## Requirements

### Requirement: Parse version configuration from environment

The system SHALL read Akka package versions from the `AKKA_VERSIONS` environment variable.
The format SHALL be comma-separated `Package=Version` pairs (e.g.
`Akka=1.5.30,Akka.Hosting=1.5.71`). Whitespace around separators SHALL be trimmed.
When `AKKA_VERSIONS` is not set or empty, the system SHALL operate in latest-mode:
resolving the latest release version for each repo on first use.

#### Scenario: Valid configuration with multiple packages

- **WHEN** `AKKA_VERSIONS` is set to `Akka=1.5.30,Akka.Hosting=1.5.71`
- **THEN** the system SHALL resolve `Akka` to version `1.5.30` and `Akka.Hosting` to version `1.5.71`

#### Scenario: Single package configured

- **WHEN** `AKKA_VERSIONS` is set to `Akka=1.5.30`
- **THEN** the system SHALL resolve `Akka` to version `1.5.30`
- **AND** unconfigured packages SHALL resolve to no version (not fetched until requested)

#### Scenario: Empty or missing configuration uses latest

- **WHEN** `AKKA_VERSIONS` is not set or empty
- **THEN** the system SHALL resolve the latest release version per repo on first tool call
- **AND** cache the resolved versions for the duration of the session

#### Scenario: Malformed configuration entry

- **WHEN** `AKKA_VERSIONS` contains an entry without `=` separator (e.g. `Akka1.5.30`)
- **THEN** the system SHALL log a warning for the malformed entry and skip it
- **AND** valid entries in the same value SHALL still be parsed

### Requirement: Validate package names against registry

The system SHALL validate that configured package names exist in the repo registry.
Unknown package names SHALL be logged as warnings and ignored.

#### Scenario: Unknown package name

- **WHEN** `AKKA_VERSIONS` contains `FakePackage=1.0.0`
- **THEN** the system SHALL log a warning that `FakePackage` is not a known Akka package
- **AND** the entry SHALL be ignored

### Requirement: Support optional version override per tool call

Each MCP tool SHALL accept an optional `version` parameter. When provided, it SHALL override
the configured version for that specific call. The override applies to the repo that owns the
relevant package.

#### Scenario: Tool call with version override

- **WHEN** configured version for `Akka` is `1.5.30`
- **AND** a tool is called with `version: "1.5.75"`
- **THEN** the tool SHALL use version `1.5.75` for the akka.net repo for that call only
- **AND** subsequent calls without override SHALL continue using `1.5.30`
