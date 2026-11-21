## MODIFIED Requirements

### Requirement: Tool method location
The tool SHALL be implemented as a method in a domain-grouped class. All behavior, parameters, and output SHALL remain unchanged.

#### Scenario: Tool remains discoverable
- **WHEN** an MCP client lists available tools
- **THEN** the tool SHALL appear with the same name and parameter schema as before
