## MODIFIED Requirements

### Requirement: MCP tool for regex source search

The tool SHALL handle invalid regex patterns gracefully by returning a user-friendly error
message instead of throwing an exception.

#### Scenario: Invalid regex pattern

- **WHEN** calling `search_source` with an invalid regex like `[unclosed`
- **THEN** the tool SHALL return an error message containing "Invalid regex pattern"
- **AND** the tool SHALL NOT throw an exception
