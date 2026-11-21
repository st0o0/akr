## ADDED Requirements

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

#### Scenario: Independent repos

- **WHEN** looking up `Akka.Hosting`
- **THEN** the registry SHALL return repo `akkadotnet/Akka.Hosting` with its source path

#### Scenario: Unknown package

- **WHEN** looking up a package name not in the registry
- **THEN** the registry SHALL return no result (not throw)

### Requirement: Provide tag pattern per repository

The registry SHALL define the tag pattern for each repository. All current Akka.NET repos
use the pattern `{version}` (unprefixed). Legacy releases used `v{version}`.

#### Scenario: Tag resolution order

- **WHEN** resolving a tag for version `1.5.30`
- **THEN** the system SHALL try `1.5.30` first
- **AND** fall back to `v1.5.30` if the first tag does not exist

### Requirement: Registry covers all 5 ecosystem repos

The registry SHALL include entries for packages from:
- `akkadotnet/akka.net` (Akka, Akka.Remote, Akka.Cluster, Akka.Streams, Akka.Persistence, Akka.TestKit)
- `akkadotnet/Akka.Hosting` (Akka.Hosting, Akka.Hosting.TestKit)
- `akkadotnet/Akka.Management` (Akka.Management, Akka.Cluster.Bootstrap)
- `akkadotnet/Akka.Persistence.Sql`
- `akkadotnet/Akka.Persistence.MongoDB`

#### Scenario: All repos represented

- **WHEN** enumerating all registry entries
- **THEN** at least one package SHALL map to each of the 5 repos listed above
