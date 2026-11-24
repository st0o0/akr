## ADDED Requirements

### Requirement: MCP tool to extract code examples from documentation

The system SHALL expose a `docs_example` MCP tool that accepts a topic string and returns code examples found in documentation for that topic. The tool SHALL extract fenced code blocks (```csharp, ```hocon) from matching documentation pages.

#### Scenario: Extract C# examples for a topic

- **WHEN** `docs_example(topic: "ask pattern")` is called
- **THEN** the system SHALL find documentation pages related to the ask pattern
- **AND** return all fenced code blocks from those pages with their surrounding context (preceding heading and description paragraph)

#### Scenario: Multiple code blocks from one page

- **WHEN** a documentation page contains multiple code examples for the requested topic
- **THEN** the system SHALL return each code block separately with its own context heading

#### Scenario: No code examples found

- **WHEN** documentation exists for a topic but contains no fenced code blocks
- **THEN** the system SHALL return a message indicating docs were found but no code examples are present
- **AND** SHALL include the doc section text as a fallback

#### Scenario: Filter by language

- **WHEN** `docs_example(topic: "persistence", language: "hocon")` is called
- **THEN** the system SHALL return only code blocks tagged as `hocon`, not `csharp` or other languages
