## Purpose

Map NuGet package names to GitHub repositories, source paths, and tag patterns.

## Requirements

### Requirement: Map NuGet packages to GitHub repositories

The system SHALL maintain a static mapping from NuGet package names to GitHub repository
identifiers and source paths within those repositories. The mapping SHALL cover the Akka.NET
ecosystem repositories listed in the project's IDEA.md.

`GetAllRepos()` SHALL return a pre-computed static collection instead of computing the distinct repo list on each call.

#### Scenario: Core mono-repo packages

- **WHEN** looking up package `Akka`
- **THEN** the registry SHALL return repo `akkadotnet/akka.net` with the source path for the core Akka project

#### Scenario: Multi-package mono-repo

- **WHEN** looking up packages `Akka`, `Akka.Remote`, `Akka.Cluster`, `Akka.Streams`, `Akka.Persistence`
- **THEN** all SHALL resolve to repo `akkadotnet/akka.net` with their respective source paths within that repo

#### Scenario: Hosting packages in mono-repo

- **WHEN** looking up `Akka.Hosting`
- **THEN** the registry SHALL return repo `akkadotnet/akka.net` with source path `src/contrib/hosting/Akka.Hosting`

#### Scenario: All Hosting packages resolve to mono-repo

- **WHEN** looking up packages `Akka.Hosting`, `Akka.Hosting.TestKit`, `Akka.Hosting.TestKit.Xunit2`, `Akka.Cluster.Hosting`, `Akka.Remote.Hosting`, `Akka.Persistence.Hosting`, `Akka.Persistence.Embedded.Hosting`
- **THEN** all SHALL resolve to repo `akkadotnet/akka.net` with paths under `src/contrib/hosting/`

#### Scenario: Reminders packages in mono-repo

- **WHEN** looking up packages `Akka.Reminders`, `Akka.Reminders.Sql`, `Akka.Reminders.SqlServer`, `Akka.Reminders.PostgreSql`, `Akka.Reminders.Sqlite`
- **THEN** all SHALL resolve to repo `akkadotnet/akka.net` with paths under `src/contrib/reminders/`

#### Scenario: Independent repos

- **WHEN** looking up `Akka.Management`
- **THEN** the registry SHALL return repo `akkadotnet/Akka.Management` with its source path

#### Scenario: Unknown package

- **WHEN** looking up a package name not in the registry
- **THEN** the registry SHALL return no result (not throw)

#### Scenario: GetAllRepos returns pre-computed result

- **WHEN** `GetAllRepos()` is called multiple times
- **THEN** it SHALL return the same static collection instance each time without recomputing

### Requirement: Provide tag pattern per repository

The registry SHALL define the tag pattern for each repository. All current Akka.NET repos
use the pattern `{version}` (unprefixed). Legacy releases used `v{version}`.

#### Scenario: Tag resolution order

- **WHEN** resolving a tag for version `1.5.30`
- **THEN** the system SHALL try `1.5.30` first
- **AND** fall back to `v1.5.30` if the first tag does not exist

### Requirement: Registry covers all 4 ecosystem repos

The registry SHALL include entries for packages from:
- `akkadotnet/akka.net` (core, hosting, reminders)
- `akkadotnet/Akka.Management` (Akka.Management, discovery plugins)
- `akkadotnet/Akka.Persistence.Sql`
- `akkadotnet/Akka.Persistence.MongoDB`

#### Scenario: All repos represented

- **WHEN** enumerating all registry entries
- **THEN** at least one package SHALL map to each of the 4 repos listed above

#### Scenario: No entries for archived repos

- **WHEN** enumerating all registry entries
- **THEN** no package SHALL map to `akkadotnet/Akka.Hosting` (archived)
