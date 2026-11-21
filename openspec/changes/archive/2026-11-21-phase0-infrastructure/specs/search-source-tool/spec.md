## ADDED Requirements

### Requirement: MCP tool for regex source search

The system SHALL expose an MCP tool named `search_source` that searches across all cached
Akka.NET source code using regex patterns.

#### Scenario: Simple text search

- **WHEN** calling `search_source` with query `Receive<T>`
- **THEN** the tool SHALL return matching lines from cached source files
- **AND** each result SHALL include the file path (relative to repo root), line number, and the matching line

#### Scenario: Regex search

- **WHEN** calling `search_source` with query `public\s+override\s+void\s+PreStart`
- **THEN** the tool SHALL match lines using the regex pattern

#### Scenario: No matches

- **WHEN** calling `search_source` with a query that matches nothing
- **THEN** the tool SHALL return an empty result set with a message indicating no matches

### Requirement: Filter by file pattern

The tool SHALL accept an optional `filePattern` parameter to filter which files are searched.
Default SHALL be `*.cs`.

#### Scenario: Default file pattern

- **WHEN** calling `search_source` without `filePattern`
- **THEN** only `.cs` files SHALL be searched

#### Scenario: Custom file pattern

- **WHEN** calling `search_source` with `filePattern: "reference.conf"`
- **THEN** only files matching that pattern SHALL be searched

### Requirement: Limit result count

The tool SHALL accept an optional `maxResults` parameter. Default SHALL be 20.

#### Scenario: Results exceed limit

- **WHEN** a search matches 50 lines
- **AND** `maxResults` is 20
- **THEN** the tool SHALL return only the first 20 matches
- **AND** indicate that results were truncated

### Requirement: Include context lines

Each search result SHALL include 2 lines of context before and after the matching line
to help the agent understand the match without fetching the full file.

#### Scenario: Context around match

- **WHEN** a match is found at line 42
- **THEN** the result SHALL include lines 40-44 (2 before, the match, 2 after)
- **AND** the matching line SHALL be clearly indicated

### Requirement: Trigger lazy fetch

If no source is cached when `search_source` is called, the tool SHALL trigger fetching for
all configured packages before searching.

#### Scenario: First call triggers fetch

- **WHEN** `search_source` is called and no source is cached
- **AND** `AKKA_VERSIONS` is configured
- **THEN** the tool SHALL fetch source for all configured packages
- **AND** then perform the search
