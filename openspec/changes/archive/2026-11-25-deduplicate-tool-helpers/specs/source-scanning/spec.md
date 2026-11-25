## ADDED Requirements

### Requirement: TypeScanner gains shared C# parsing helpers
`TypeScanner` SHALL be extended with `HasBase(string bases, string[] names)`, `CollectBlockLines(string[] lines, int start, int maxLines)`, `ClassifyTestKit(IReadOnlyList<string> lines)`, and regex patterns (`ClassPattern`, `MethodPattern`, `WhitespacePattern`, `StripGenericsPattern`, `BaseClassPattern`). It SHALL become a partial class for `GeneratedRegex` support.

#### Scenario: HasBase checks inheritance
- **WHEN** `TypeScanner.HasBase("ActorBase, IDisposable", ["ActorBase"])` is called
- **THEN** it SHALL return true

#### Scenario: CollectBlockLines extracts code blocks
- **WHEN** `CollectBlockLines(lines, startIndex)` is called at a line containing `{`
- **THEN** it SHALL return all lines until the matching closing `}`

### Requirement: Duplicated C# parsing helpers removed from tools
`HasBase` SHALL be removed from `StreamTools` and `PersistenceTools`. `CollectBlockLines` and `MaxClassLines` SHALL be removed from `SerializationTools` and `ClusterTools`. `ClassifyTestKit` SHALL be removed from `HostingTools` and `TestKitTools`. All shared regex patterns SHALL be removed from tool files. Tools SHALL reference `TypeScanner` instead.

#### Scenario: No C# parsing helper duplication remains
- **WHEN** the codebase is searched for duplicate method definitions
- **THEN** `HasBase`, `CollectBlockLines`, and `ClassifyTestKit` SHALL each exist in exactly one location
