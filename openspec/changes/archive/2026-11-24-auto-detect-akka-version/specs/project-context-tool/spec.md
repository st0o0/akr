## ADDED Requirements

### Requirement: MCP tool to set project context

The system SHALL expose an MCP tool named `set_project_context` that accepts a `path` parameter. When called, it SHALL scan the given path for .NET project files and update the resolved Akka.NET versions for subsequent tool calls.

#### Scenario: Valid project path with Akka references

- **WHEN** `set_project_context` is called with `path: "/repo/src"`
- **AND** the path contains `Directory.Packages.props` with `Akka` version `1.5.75`
- **THEN** the tool SHALL return a success message listing detected packages and versions
- **AND** subsequent akr tool calls SHALL use version `1.5.75` for the `akkadotnet/akka.net` repo

#### Scenario: Valid path without Akka references

- **WHEN** `set_project_context` is called with a path containing no Akka NuGet references
- **THEN** the tool SHALL return a message indicating no Akka packages were found
- **AND** version resolution SHALL fall back to `AKKA_VERSIONS` env or latest release

#### Scenario: Invalid path

- **WHEN** `set_project_context` is called with a non-existent path
- **THEN** the tool SHALL return an error message indicating the path does not exist

#### Scenario: Overrides working directory detection

- **WHEN** `set_project_context` is called after the server already detected versions from the working directory
- **THEN** the new path's versions SHALL replace the working directory versions

### Requirement: MCP resource for context state

The system SHALL expose an MCP resource at URI `akr://context` that returns JSON describing the current version resolution state.

#### Scenario: Project-detected version

- **WHEN** akr detected versions from a .NET project
- **THEN** the resource SHALL return JSON containing:
  - `source`: `"project"`
  - `projectPath`: the path that was scanned
  - `packages`: map of detected package names to versions
  - `resolvedVersions`: map of repo names to resolved versions

#### Scenario: Environment variable configuration

- **WHEN** akr is using `AKKA_VERSIONS` environment variable
- **THEN** the resource SHALL return JSON with `source`: `"environment"`

#### Scenario: Latest release fallback

- **WHEN** akr is using the latest GitHub release
- **THEN** the resource SHALL return JSON with `source`: `"latest"`

### Requirement: Server instructions for agents

The system SHALL set `ServerInstructions` on the MCP server to guide AI agents. The instructions SHALL mention that akr auto-detects Akka.NET versions from the project context and that `set_project_context` is available for switching projects.

#### Scenario: Server instructions present

- **WHEN** an MCP client connects to akr
- **THEN** the server SHALL include instructions describing auto-detection and the `set_project_context` tool
