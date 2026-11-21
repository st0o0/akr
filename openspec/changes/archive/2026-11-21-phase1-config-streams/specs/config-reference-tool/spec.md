## ADDED Requirements

### Requirement: MCP tool for HOCON config reference

The system SHALL expose an MCP tool named `config_reference` that returns HOCON config
defaults from `reference.conf` files in cached Akka.NET source.

#### Scenario: List all config from a module

- **WHEN** calling `config_reference` without a path filter
- **THEN** the tool SHALL return the full content of all `reference.conf` files
- **AND** each file's content SHALL be labeled with its source module path

#### Scenario: Filter by config path

- **WHEN** calling `config_reference` with path `akka.persistence`
- **THEN** the tool SHALL return only the HOCON block matching that path
- **AND** nested config within that block SHALL be included

#### Scenario: No reference.conf found

- **WHEN** no `reference.conf` files exist in cached source
- **THEN** the tool SHALL return a message indicating no config was found

### Requirement: Extract brace-delimited HOCON blocks

When a path filter is provided, the tool SHALL locate the config key in the file and
extract the complete brace-delimited block including all nested content.

#### Scenario: Nested config block extraction

- **WHEN** searching for path `akka.actor.default-dispatcher`
- **THEN** the tool SHALL return the full block from the opening brace to the matching closing brace
