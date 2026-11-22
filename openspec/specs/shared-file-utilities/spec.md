## Purpose

Consolidated file enumeration and HOCON comment stripping utilities, replacing duplicate implementations across tool classes.

## Requirements

### Requirement: Shared file enumeration utility

The system SHALL provide a single `FileEnumerator.EnumerateFiles` method that all tool classes use to enumerate files across source roots.

#### Scenario: Enumerate files with pattern

- **WHEN** `EnumerateFiles` is called with a list of source roots and a file pattern (e.g. `*.cs`)
- **THEN** it SHALL return tuples of `(FullPath, RelativePath)` for all matching files across all roots
- **AND** relative paths SHALL use forward slashes

#### Scenario: Exclude build output directories

- **WHEN** enumerating files across source roots
- **THEN** files under `/obj/` or `/bin/` directories SHALL be excluded from results

#### Scenario: Skip non-existent roots

- **WHEN** a source root path does not exist on the filesystem
- **THEN** that root SHALL be skipped without error

#### Scenario: Consistent ordering

- **WHEN** enumerating files
- **THEN** results SHALL be ordered by relative path within each root

### Requirement: Shared HOCON comment stripping

The system SHALL provide a single `HoconHelper.StripComment` method used by all tools that need to strip HOCON-style comments from lines.

#### Scenario: Strip line comment

- **WHEN** `StripComment` is called with a line containing `#` outside a string
- **THEN** it SHALL return the portion of the line before the `#`

#### Scenario: Strip double-slash comment

- **WHEN** `StripComment` is called with a line containing `//` outside a string
- **THEN** it SHALL return the portion of the line before the `//`

#### Scenario: Preserve comments inside strings

- **WHEN** `StripComment` is called with a line where `#` or `//` appears inside double quotes
- **THEN** it SHALL return the full line unchanged

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

`Microsoft.Extensions.Hosting` package version SHALL match the target framework generation. For net10.0, the version SHALL be 11.x.

#### Scenario: Package version matches framework

- **WHEN** inspecting `Directory.Packages.props`
- **THEN** `Microsoft.Extensions.Hosting` version SHALL be in the 11.x range
