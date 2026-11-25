## Why

The tool reorganization (separation-of-concerns-refactor) copied shared helper methods into multiple tool files to keep each class self-contained. This created 10 duplicated methods and 7 duplicated regex patterns across 2-6 files. Any bug fix or improvement must be applied in every copy.

## What Changes

- Extend `HoconParser` (static, pure) with `ParseConfig`, `PathMatches`, and HOCON regex patterns
- Create `HoconScanner` (Singleton, DI) for I/O-based config collection: `CollectConfig`, `CollectHostingKeys`
- Extend `TypeScanner` (existing Singleton) with `HasBase`, `CollectBlockLines`, `ClassifyTestKit`, and C# regex patterns
- Remove all duplicated copies from ConfigTools, HostingTools, StreamTools, PersistenceTools, SerializationTools, ClusterTools, TestKitTools
- Tools call Scanning/ classes via constructor injection or static methods instead of duplicating

## Capabilities

### New Capabilities
- `hocon-scanning`: Extend HoconParser with pure parsing helpers, add HoconScanner for I/O-based config collection
- `source-scanning`: Extend TypeScanner with shared C# source parsing helpers

### Modified Capabilities

## Impact

- **Scanning/**: HoconParser.cs grows (~60 lines), new HoconScanner.cs (~50 lines), TypeScanner.cs grows (~80 lines)
- **Tools/**: 7 tool files lose duplicated methods (net deletion ~400 lines). Some gain new constructor parameters (HoconScanner, TypeScanner)
- **DI**: HoconScanner registered as singleton in McpSetup.cs
- **Tests**: Constructor changes for tools that gain HoconScanner/TypeScanner dependency
