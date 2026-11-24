## ADDED Requirements

### Requirement: Thread-safe ProjectContext service

The system SHALL provide a `ProjectContext` service that holds the current `ProjectScanResult`. The `Current` property SHALL be thread-safe using volatile semantics. `ProjectScanner` SHALL NOT hold mutable state.

#### Scenario: Read and write current context

- **WHEN** `ProjectContext.Current` is set to a scan result with `Akka=1.5.75`
- **THEN** any subsequent read of `ProjectContext.Current` SHALL return that result
- **AND** reads from other threads SHALL see the updated value

#### Scenario: Default state is empty

- **WHEN** `ProjectContext` is first created
- **THEN** `Current` SHALL be `ProjectScanResult.Empty`

### Requirement: Initialize project context via IHostedService

The system SHALL provide a `ProjectContextInitializer : IHostedService` that runs during application startup. It SHALL:
1. Read `AkrOptions.ProjectPath` — if set, scan that path
2. If no result, scan `Environment.CurrentDirectory`
3. Set `ProjectContext.Current` with the result

#### Scenario: AKR_PROJECT_PATH configured

- **WHEN** `AkrOptions.ProjectPath` is set to a valid path with Akka references
- **THEN** the initializer SHALL set `ProjectContext.Current` with the detected packages

#### Scenario: Working directory scan fallback

- **WHEN** `AkrOptions.ProjectPath` is not set
- **AND** the working directory contains a .NET project with Akka references
- **THEN** the initializer SHALL set `ProjectContext.Current` from the CWD scan

#### Scenario: No project found

- **WHEN** neither `AkrOptions.ProjectPath` nor the working directory contain Akka references
- **THEN** `ProjectContext.Current` SHALL remain `ProjectScanResult.Empty`

### Requirement: No business logic in DI factory lambdas

DI registration SHALL only wire services. No environment variable reads, no filesystem scans, no conditional logic SHALL occur during service registration or factory resolution.

#### Scenario: McpSetup contains only registrations

- **WHEN** inspecting `McpSetup.Setup`
- **THEN** it SHALL contain only `services.Add*` and `.With*` calls
- **AND** no `Environment.GetEnvironmentVariable`, `new ProjectScanner(...)`, or `InitializeProjectContext` calls
