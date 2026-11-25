## ADDED Requirements

### Requirement: HoconParser gains pure parsing helpers
`HoconParser` SHALL be extended with `ParseConfig(IReadOnlyList<string> lines)` returning `IReadOnlyList<ConfigEntry>`, `PathMatches(string key, string query)` returning `bool`, and two `GeneratedRegex` patterns (`NamedMethodPattern`, `HoconKeyPattern`). It SHALL remain a static partial class with no I/O dependencies.

#### Scenario: ParseConfig extracts HOCON key-value pairs
- **WHEN** `HoconParser.ParseConfig(lines)` is called with HOCON content
- **THEN** it SHALL return ConfigEntry records with dotted paths and values

#### Scenario: PathMatches checks HOCON path containment
- **WHEN** `HoconParser.PathMatches("akka.cluster.roles", "akka.cluster")` is called
- **THEN** it SHALL return true

### Requirement: HoconScanner collects config from disk
`HoconScanner` in `Akr.Scanning` SHALL be a Singleton registered in DI, taking `FileService` via constructor. It SHALL own `CollectConfig(IEnumerable<string> roots)` and `CollectHostingKeys(IEnumerable<string> roots)`.

#### Scenario: CollectConfig scans reference.conf files
- **WHEN** `CollectConfig(roots)` is called
- **THEN** it SHALL enumerate config files via FileService and parse each with `HoconParser.ParseConfig`

#### Scenario: CollectHostingKeys finds hosting method-to-HOCON mappings
- **WHEN** `CollectHostingKeys(roots)` is called
- **THEN** it SHALL scan .cs files for `With*/Add*` methods and extract HOCON keys used within them

### Requirement: Duplicated HOCON helpers removed from tools
`CollectConfig`, `CollectHostingKeys`, `ParseConfig`, `PathMatches`, `NamedMethodPattern`, and `HoconKeyPattern` SHALL be removed from `ConfigTools` and `HostingTools`. Both tools SHALL use `HoconParser` (static) and `HoconScanner` (injected) instead.

#### Scenario: No HOCON helper duplication remains
- **WHEN** the codebase is searched for duplicate method definitions
- **THEN** `CollectConfig`, `CollectHostingKeys`, `ParseConfig`, and `PathMatches` SHALL each exist in exactly one location
