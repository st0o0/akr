## ADDED Requirements

### Requirement: MCP tool to generate version migration guidance

The system SHALL expose a `migration_guide` MCP tool that combines source diffs, changelog entries, and relevant documentation into a structured upgrade guide for migrating from version A to version B.

#### Scenario: Generate migration guide between two versions

- **WHEN** `migration_guide(fromVersion: "1.5.0", toVersion: "1.5.74")` is called
- **THEN** the system SHALL return a structured guide containing:
  - Breaking changes extracted from CHANGELOG/RELEASE_NOTES between the two versions
  - Config changes (keys added, removed, or changed defaults)
  - Relevant documentation links or excerpts for affected areas

#### Scenario: Migration guide for a specific area

- **WHEN** `migration_guide(fromVersion: "1.5.0", toVersion: "1.5.74", area: "persistence")` is called
- **THEN** the system SHALL filter the guide to only persistence-related changes
- **AND** include persistence-specific changelog entries, config diffs, and docs

#### Scenario: No breaking changes between versions

- **WHEN** no breaking changes exist between the two versions
- **THEN** the system SHALL indicate no breaking changes were found
- **AND** still include any config changes and relevant docs as informational content

#### Scenario: Changelog not available for a version

- **WHEN** CHANGELOG or RELEASE_NOTES files cannot be parsed for one of the versions
- **THEN** the system SHALL return whatever information is available (config diffs, docs)
- **AND** note that changelog data was unavailable
