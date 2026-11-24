## MODIFIED Requirements

### Requirement: Parse version configuration from environment

The system SHALL read Akka package versions from the `AKKA_VERSIONS` environment variable.
The format SHALL be comma-separated `Package=Version` pairs (e.g.
`Akka=1.5.30,Akka.Hosting=1.5.71`). Whitespace around separators SHALL be trimmed.
When `AKKA_VERSIONS` is not set or empty, the system SHALL check for project-detected versions before falling back to latest-mode.

The resolution order SHALL be:
1. `AKR_PROJECT_PATH` environment variable — scan the specified path for .NET project files
2. Working directory — scan the server's working directory for .NET project files
3. `AKKA_VERSIONS` environment variable — explicit version mapping
4. Latest-mode — resolve latest GitHub release per repo on first use

#### Scenario: Valid configuration with multiple packages

- **WHEN** `AKKA_VERSIONS` is set to `Akka=1.5.30,Akka.Hosting=1.5.71`
- **THEN** the system SHALL resolve `Akka` to version `1.5.30` and `Akka.Hosting` to version `1.5.71`

#### Scenario: Single package configured

- **WHEN** `AKKA_VERSIONS` is set to `Akka=1.5.30`
- **THEN** the system SHALL resolve `Akka` to version `1.5.30`
- **AND** unconfigured packages SHALL resolve to no version (not fetched until requested)

#### Scenario: Empty or missing configuration uses project detection

- **WHEN** `AKKA_VERSIONS` is not set or empty
- **AND** the working directory contains a .NET project referencing `Akka` version `1.5.75`
- **THEN** the system SHALL use the project-detected version `1.5.75`

#### Scenario: Empty configuration with no project falls back to latest

- **WHEN** `AKKA_VERSIONS` is not set or empty
- **AND** no .NET project files are found in the working directory or ancestors
- **THEN** the system SHALL resolve the latest release version per repo on first tool call

#### Scenario: Malformed configuration entry

- **WHEN** `AKKA_VERSIONS` contains an entry without `=` separator (e.g. `Akka1.5.30`)
- **THEN** the system SHALL log a warning for the malformed entry and skip it
- **AND** valid entries in the same value SHALL still be parsed

#### Scenario: AKKA_VERSIONS overrides project detection

- **WHEN** `AKKA_VERSIONS` is set to `Akka=1.5.30`
- **AND** the working directory contains a .NET project referencing `Akka` version `1.5.75`
- **THEN** the system SHALL use `1.5.30` from `AKKA_VERSIONS` (env var takes precedence over project for the same package)

## ADDED Requirements

### Requirement: Read project path from environment variable

The system SHALL read the `AKR_PROJECT_PATH` environment variable. When set, the system SHALL scan the specified path for .NET project files to detect Akka.NET versions. This takes the highest priority in the resolution order.

#### Scenario: AKR_PROJECT_PATH set to valid project

- **WHEN** `AKR_PROJECT_PATH` is set to `/other/project`
- **AND** that path contains `Directory.Packages.props` with `Akka` version `1.6.0-preview.1`
- **THEN** the system SHALL use version `1.6.0-preview.1` for the `akkadotnet/akka.net` repo

#### Scenario: AKR_PROJECT_PATH set to invalid path

- **WHEN** `AKR_PROJECT_PATH` is set to a non-existent path
- **THEN** the system SHALL log a warning
- **AND** fall through to working directory scan

#### Scenario: AKR_PROJECT_PATH overrides working directory

- **WHEN** `AKR_PROJECT_PATH` is set to `/project-a` with `Akka` version `1.6.0`
- **AND** the working directory contains a project with `Akka` version `1.5.75`
- **THEN** the system SHALL use version `1.6.0` from `AKR_PROJECT_PATH`
