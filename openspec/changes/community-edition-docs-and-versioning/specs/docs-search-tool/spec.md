## ADDED Requirements

### Requirement: MCP tool to search documentation by topic

The system SHALL expose a `docs_search` MCP tool that accepts a query string and returns relevant documentation excerpts. Results SHALL be token-efficient: headings, key paragraphs, and a source file reference — not full page content.

#### Scenario: Search by topic keyword

- **WHEN** `docs_search(query: "cluster sharding")` is called
- **THEN** the system SHALL return matching documentation sections from files related to clustering/sharding
- **AND** each result SHALL include the section heading, a content excerpt (max ~500 tokens), and the source filename

#### Scenario: Search returns multiple matches ranked by relevance

- **WHEN** a query matches content in multiple documentation files
- **THEN** the system SHALL return up to 5 results ordered by relevance (filename match > heading match > body match)

#### Scenario: No results found

- **WHEN** a query matches no documentation content
- **THEN** the system SHALL return an empty result with a message indicating no docs were found for the query

#### Scenario: Search works without prior source fetch

- **WHEN** `docs_search` is called before any source code tool has been used
- **THEN** the system SHALL fetch and cache the docs repository automatically
- **AND** return results normally
