## ADDED Requirements

### Requirement: ActorSystem registration via Akka.Hosting
The system SHALL register an `ActorSystem` named "akr" using `services.AddAkka("akr", builder => { })` in `McpSetup.cs`. The builder SHALL have no HOCON configuration, no actor registrations, and no cluster setup. The ActorSystem is used exclusively for Streams materialization.

#### Scenario: ActorSystem available via DI
- **WHEN** the MCP server starts
- **THEN** an `ActorSystem` named "akr" SHALL be resolvable from the DI container

#### Scenario: Empty builder configuration
- **WHEN** the Akka.Hosting builder is configured
- **THEN** no HOCON overrides, no actor registrations, and no remoting/cluster configuration SHALL be applied

### Requirement: Lifecycle management via Akka.Hosting
Akka.Hosting SHALL manage the ActorSystem lifecycle as an `IHostedService`. The ActorSystem SHALL start with the host and shut down gracefully via `CoordinatedShutdown` when the host stops.

#### Scenario: Graceful shutdown on host stop
- **WHEN** the MCP server host stops
- **THEN** `CoordinatedShutdown` SHALL terminate the ActorSystem after all in-flight streams complete

#### Scenario: ActorSystem starts before tools are invoked
- **WHEN** the MCP server has completed startup
- **THEN** the ActorSystem SHALL be fully initialized and ready to materialize streams

### Requirement: Dependency registration in McpSetup
`McpSetup.cs` SHALL add `Akka.Hosting` 1.6.0-beta7 to the service collection. The `Akka.Hosting` package version SHALL be managed in `Directory.Packages.props`. The `Akka.Streams` package SHALL be pulled transitively through `Akka.Hosting`.

#### Scenario: Package added to central package management
- **WHEN** `Directory.Packages.props` is inspected
- **THEN** `Akka.Hosting` SHALL be listed with version `1.6.0-beta7`

#### Scenario: No direct Akka.Streams package reference
- **WHEN** `Akr.csproj` is inspected
- **THEN** `Akka.Streams` SHALL NOT appear as a direct `PackageReference` — it comes transitively via `Akka.Hosting`
