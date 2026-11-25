## ADDED Requirements

### Requirement: Optional version parameter on all source tools

All existing MCP tools that query source code SHALL accept an optional `version` string parameter. When provided, the tool SHALL target that specific Akka.NET version instead of the resolved latest. When omitted, behavior SHALL be unchanged (use resolved latest).

#### Scenario: Tool called with explicit version

- **WHEN** `lookup_type(typeName: "Throttle", version: "1.5.52")` is called
- **THEN** the system SHALL ensure version `1.5.52` is fetched and cached
- **AND** search for `Throttle` in the 1.5.52 source tree only

#### Scenario: Tool called without version uses resolved latest

- **WHEN** `lookup_type(typeName: "Throttle")` is called (no version parameter)
- **THEN** behavior SHALL be identical to current behavior (project-detected or latest)

#### Scenario: Requested version not found on GitHub

- **WHEN** a tool is called with `version: "99.99.99"` which does not exist
- **THEN** the system SHALL return an error indicating the version was not found
- **AND** SHALL NOT fall back to latest silently

### Requirement: Version parameter routes through version-specific fetch

When a `version` parameter is provided, the system SHALL call a version-specific fetch method that skips latest-resolution and directly downloads the specified tag's archive.

#### Scenario: Version-specific fetch skips resolution

- **WHEN** `EnsureSourceForVersionAsync("akka.net", "1.5.52")` is called
- **THEN** the system SHALL NOT call `ResolveLatestAsync`
- **AND** SHALL directly fetch the archive for tag `1.5.52` (or `v1.5.52` as fallback)
