## MODIFIED Requirements

### Requirement: Map NuGet packages to GitHub repositories

The system SHALL maintain a static mapping from NuGet package names to GitHub repository
identifiers and source paths within those repositories. The mapping SHALL cover the Akka.NET
ecosystem repositories listed in the project's IDEA.md.

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
