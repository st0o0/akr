## ADDED Requirements

### Requirement: Native AOT publishing

The server project SHALL be configured for Native AOT publishing with trimming enabled.
The published binary SHALL be a self-contained single-file native executable.

#### Scenario: AOT build succeeds without warnings

- **WHEN** running `dotnet publish` with the AOT profile
- **THEN** the build SHALL succeed without trim or AOT warnings

#### Scenario: Published binary size

- **WHEN** the server is published as a native AOT binary
- **THEN** the binary size SHALL be under 20 MB

### Requirement: No AOT-incompatible dependencies

The server host project SHALL NOT reference packages that are incompatible with Native AOT.
Specifically: no Microsoft.CodeAnalysis.CSharp, no packages using runtime reflection for
serialization or type discovery.

#### Scenario: No trim warnings from dependencies

- **WHEN** the solution is built with `PublishAot=true`
- **THEN** there SHALL be zero `IL2xxx` or `IL3xxx` trim warnings from project code

### Requirement: Explicit MCP tool registration

MCP tools SHALL be registered using generic `WithTools<T>()` calls instead of
assembly-scanning `WithToolsFromAssembly()`.

#### Scenario: Tools registered explicitly

- **WHEN** the MCP server starts
- **THEN** `search_source` and `lookup_type` tools SHALL be available
- **AND** no reflection-based tool discovery SHALL be used
