## MODIFIED Requirements

### Requirement: Match type declarations via pattern

The tool SHALL find types by matching declaration patterns using `[GeneratedRegex]`
source-generated regular expressions for AOT compatibility. The namespace extraction
pattern SHALL also use `[GeneratedRegex]`.

#### Scenario: Type patterns are source-generated

- **WHEN** the server is compiled with Native AOT
- **THEN** all static regex patterns in TypeFinder SHALL be source-generated
- **AND** no `RegexOptions.Compiled` SHALL be used
