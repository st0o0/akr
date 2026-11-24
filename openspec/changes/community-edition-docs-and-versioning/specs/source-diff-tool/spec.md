## ADDED Requirements

### Requirement: MCP tool to diff types between versions

The system SHALL expose a `source_diff` MCP tool that compares a type's source code between two Akka.NET versions and returns a unified diff.

#### Scenario: Diff a type that changed between versions

- **WHEN** `source_diff(typeName: "Throttle", versionA: "1.5.52", versionB: "1.5.74")` is called
- **THEN** the system SHALL fetch both versions (if not cached)
- **AND** find the type in both source trees
- **AND** return a unified diff showing additions, removals, and changes

#### Scenario: Diff a type that exists in only one version

- **WHEN** `source_diff(typeName: "NewFeature", versionA: "1.4.0", versionB: "1.5.0")` is called
- **AND** `NewFeature` does not exist in version `1.4.0`
- **THEN** the system SHALL indicate the type was added in `1.5.0`
- **AND** return the full source from `1.5.0` as added content

#### Scenario: Diff a type with no changes

- **WHEN** the type source is identical in both versions
- **THEN** the system SHALL return a message indicating no changes were found

### Requirement: MCP tool to diff config between versions

The `source_diff` tool SHALL also support comparing `reference.conf` entries between two versions when called with a config path instead of a type name.

#### Scenario: Diff a config key between versions

- **WHEN** `source_diff(configPath: "akka.cluster.sharding", versionA: "1.5.0", versionB: "1.5.74")` is called
- **THEN** the system SHALL compare the config subtree at that path between both versions
- **AND** return a unified diff of the HOCON content
