## Why

The codebase has accumulated structural noise and suppressed warnings that hurt readability, AOT compatibility, and performance. Section comments add no value over well-named methods, `ProjectContextInitializer` is unnecessary ceremony for a synchronous call, several methods are instance methods without instance state, regex patterns trigger SYSLIB1044 source generator limitations, and logging bypasses the `[LoggerMessage]` source generators that .NET recommends for AOT and performance.

## What Changes

- Remove all `// ---- ... ----` section comments across 14+ files (pure noise deletion)
- Eliminate `ProjectContextInitializer` — move initialization into `ProjectScanner` itself (lazy/on-demand pattern)
- Move `MapToRepoVersions` out of `ProjectScanner` to `RepoRegistry` as a static method (it only calls `RepoRegistry.GetEntry()`)
- Extract hardcoded file name strings as constants in `ProjectScanner`
- Deduplicate `ScanPackagesProps`/`ScanPackageReferences` into a single parameterized method
- Add `RegexOptions.NonBacktracking` to ~20 regex patterns in TypeTools.cs; fix lazy quantifier incompatibility in TypeScanner.cs; simplify mega-regexes that exceed source generator capability
- Convert all 21 logging call sites to `[LoggerMessage]` source generator partial methods for AOT compatibility and conditional evaluation

## Capabilities

### New Capabilities

- `logging-source-generators`: LoggerMessage source generator pattern for all logging across the project
- `regex-source-generation`: Ensure all GeneratedRegex patterns compile fully via source generator (no SYSLIB1044 fallbacks)

### Modified Capabilities

- `scanning-layer`: ProjectScanner owns its initialization (no more ProjectContextInitializer); `MapToRepoVersions` moves to `RepoRegistry`; scan methods deduplicated
- `shared-file-utilities`: Section comments removed from FileService and all tool classes

## Impact

- **Code**: All files with section comments (~14+), ProjectScanner, ProjectContextInitializer (deleted), TypeScanner, TypeTools, RepoRegistry, McpSetup/DI registration, all classes with logging
- **APIs**: No public API changes — all MCP tools behave identically
- **Dependencies**: No new dependencies — `[LoggerMessage]` and `[GeneratedRegex]` are built-in source generators
