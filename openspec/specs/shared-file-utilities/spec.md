## Purpose

Code quality improvements: source-generated regex, deduplicated MSBuild properties, aligned package versions. File enumeration and HOCON comment stripping are now part of FileService (see file-service spec).

## Requirements

### Requirement: Convert static regex to GeneratedRegex

All static `new Regex(...)` fields in `HostingTools`, `StreamTools`, and `ConfigMigrationTools` SHALL be replaced with `[GeneratedRegex]` partial methods.

#### Scenario: HostingTools method pattern

- **WHEN** `HostingTools` matches extension method signatures
- **THEN** it SHALL use a `[GeneratedRegex]` source-generated pattern instead of a runtime-compiled `Regex`

#### Scenario: StreamTools class pattern

- **WHEN** `StreamTools` matches class declarations for stream stages
- **THEN** it SHALL use a `[GeneratedRegex]` source-generated pattern

#### Scenario: ConfigMigrationTools patterns

- **WHEN** `ConfigMigrationTools` matches HOCON keys, method patterns, entry patterns, ordering patterns, or DI hints
- **THEN** each SHALL use a `[GeneratedRegex]` source-generated pattern

### Requirement: Remove duplicate MSBuild properties

`Akr.csproj` SHALL NOT duplicate `DebugType` and `EmbedUntrackedSources` properties that are already defined in `Directory.Build.props`.

#### Scenario: Properties inherited from Directory.Build.props

- **WHEN** building the `Akr` project
- **THEN** `DebugType` and `EmbedUntrackedSources` SHALL be inherited from `Directory.Build.props`
- **AND** `Akr.csproj` SHALL NOT contain these properties

### Requirement: Align Microsoft.Extensions.Hosting version

`Microsoft.Extensions.Hosting` package version SHALL match the target framework generation. For net10.0, the version SHALL be 10.x.

#### Scenario: Package version matches framework

- **WHEN** inspecting `Directory.Packages.props`
- **THEN** `Microsoft.Extensions.Hosting` version SHALL be in the 10.x range

### Requirement: No section comments in source files
Source files SHALL NOT contain section-divider comments of the form `// ---- ... ----` or `// ---------- ... ----------`. Method signatures and class structure provide sufficient organization.

#### Scenario: Section comments removed from all files
- **WHEN** the codebase is searched for `// -+.*-+` patterns
- **THEN** zero matches are found

#### Scenario: No behavior change after removal
- **WHEN** section comments are removed
- **THEN** all existing tests pass without modification
