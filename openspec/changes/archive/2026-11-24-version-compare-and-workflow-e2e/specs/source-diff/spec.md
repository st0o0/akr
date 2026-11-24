## ADDED Requirements

### Requirement: Diff types between versions

A new `source_diff` MCP tool SHALL compare a type's source between two Akka.NET versions and return a unified diff.

#### Scenario: Compare Throttle between versions
- **WHEN** `source_diff(type: "Throttle", versionA: "1.5.52", versionB: "1.5.74")` is called
- **THEN** the result SHALL contain a unified diff showing lines added, removed, and changed between the two versions

#### Scenario: Type exists in one version only
- **WHEN** a type exists in versionB but not versionA
- **THEN** the result SHALL indicate the type was added in versionB and show its full source as additions

#### Scenario: No changes between versions
- **WHEN** a type's source is identical in both versions
- **THEN** the result SHALL indicate "No changes" rather than returning an empty diff

### Requirement: Diff config between versions

The `source_diff` tool SHALL also support comparing HOCON config paths between two versions.

#### Scenario: Compare config keys
- **WHEN** `source_diff(configPath: "akka.cluster", versionA: "1.5.52", versionB: "1.5.74")` is called
- **THEN** the result SHALL show added, removed, and changed config keys between the two versions

### Requirement: Diff uses existing config_diff logic

The config diff mode SHALL reuse `ConfigMigrationTools.DiffConfig` with actual two-version comparison instead of the current "current vs empty" placeholder.

#### Scenario: config_diff tool also supports two versions
- **WHEN** `config_diff(versionA: "1.5.52", versionB: "1.5.74")` is called
- **THEN** the result SHALL show actual differences between the two versions' config keys
