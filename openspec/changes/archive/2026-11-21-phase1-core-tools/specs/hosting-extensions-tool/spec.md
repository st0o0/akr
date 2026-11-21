## ADDED Requirements

### Requirement: MCP tool for hosting extension method discovery

The system SHALL expose an MCP tool named `hosting_extensions` that searches for
`With*` and `Add*` extension methods in cached Akka.Hosting source code.

#### Scenario: List all hosting extensions

- **WHEN** calling `hosting_extensions` without a filter
- **THEN** the tool SHALL return all public static extension methods matching `With*` or `Add*`
- **AND** results SHALL be grouped by source file

#### Scenario: Filter by keyword

- **WHEN** calling `hosting_extensions` with filter `Persistence`
- **THEN** the tool SHALL return only extension methods from files whose path contains `Persistence`

#### Scenario: No matches

- **WHEN** calling `hosting_extensions` with a filter that matches nothing
- **THEN** the tool SHALL return a message indicating no extensions were found

### Requirement: Return signature-only output

Each extension method SHALL be shown as its full signature line without the method body.
The `this` parameter type SHALL be preserved to show what it extends.

#### Scenario: Extension method signature format

- **WHEN** an extension method is found
- **THEN** its signature SHALL include return type, name, and all parameters
- **AND** the method body SHALL NOT be included
