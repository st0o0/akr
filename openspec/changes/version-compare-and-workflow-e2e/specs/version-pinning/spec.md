## ADDED Requirements

### Requirement: Optional version parameter on all tools

Every MCP tool that accesses Akka.NET source SHALL accept an optional `version` string parameter. When provided, the tool SHALL use source from that specific version instead of the latest resolved version.

#### Scenario: Lookup type at specific version
- **WHEN** `lookup_type(type: "Throttle", version: "1.5.52")` is called
- **THEN** the result SHALL contain the Throttle source from Akka.NET version 1.5.52

#### Scenario: Default behavior unchanged when version omitted
- **WHEN** `lookup_type(type: "Props")` is called without a version parameter
- **THEN** the result SHALL use the latest resolved version (existing behavior)

#### Scenario: Invalid version returns clear error
- **WHEN** a tool is called with `version: "99.99.99"` (non-existent)
- **THEN** the result SHALL contain an error message indicating the version was not found

### Requirement: Version parameter reaches GitHubClient

The version parameter SHALL short-circuit `ResolveLatestAsync` and directly request the specified version via `EnsureSourceAsync`.

#### Scenario: Specific version is fetched and cached
- **WHEN** a tool is called with `version: "1.5.52"` for the first time
- **THEN** `GitHubClient` SHALL download and cache that version's source
- **AND** subsequent calls with the same version SHALL use the cached copy
