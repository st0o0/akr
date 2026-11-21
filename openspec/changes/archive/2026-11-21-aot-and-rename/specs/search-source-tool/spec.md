## MODIFIED Requirements

### Requirement: MCP tool for regex source search

The system SHALL expose an MCP tool named `search_source` that searches across all cached
Akka.NET source code using regex patterns. Regex SHALL use `RegexOptions.NonBacktracking`
for AOT compatibility and guaranteed linear-time execution.

#### Scenario: Regex uses non-backtracking mode

- **WHEN** calling `search_source` with any regex pattern
- **THEN** the regex engine SHALL use non-backtracking mode
- **AND** pathological patterns SHALL complete in bounded time
