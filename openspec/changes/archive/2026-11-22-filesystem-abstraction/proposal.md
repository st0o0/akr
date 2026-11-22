## Why

The codebase has 85 direct `System.IO` calls (`File.*`, `Directory.*`, `Path.*`) spread across 11 files. This makes cross-platform behavior implicit (path separators, case sensitivity), unit testing requires real filesystem fixtures, and common patterns (enumerate+relativize+normalize, atomic write via temp+move, HOCON comment stripping) are scattered across multiple utility classes (`FileEnumerator`, `HoconHelper`, `ConfigFiles`) and inline in tool methods. A single injectable `FileService` backed by `System.IO.Abstractions` consolidates all filesystem operations, enables `MockFileSystem` in tests, and guarantees consistent cross-platform path handling.

## What Changes

- Add `System.IO.Abstractions` NuGet package (v23.x)
- Introduce `FileService` class that wraps `IFileSystem` and provides all domain-specific file operations
- Absorb `FileEnumerator`, `HoconHelper`, and `ConfigFiles` static classes into `FileService` methods
- Replace all 85 direct `System.IO` calls in `GitHubClient` and tool classes with `FileService` calls
- Register `FileService` in DI (`McpSetup`), inject into `GitHubClient` and MCP tool classes
- MCP tool classes become non-static (required for DI constructor injection)

## Capabilities

### New Capabilities

- `file-service`: Injectable service consolidating all filesystem operations with cross-platform path handling

### Modified Capabilities

- `source-caching`: `GitHubClient` and `DeepIntelligenceTools` use `FileService` instead of direct I/O
- `shared-file-utilities`: `FileEnumerator`, `HoconHelper`, `ConfigFiles` are absorbed into `FileService` and deleted

## Impact

- **Files changed**: All 11 `.cs` files with direct I/O, plus `McpSetup.cs`, `Directory.Packages.props`
- **Files deleted**: `FileEnumerator.cs`, `HoconHelper.cs`, `ConfigFiles.cs`
- **Files created**: `FileService.cs`
- **Dependencies**: `System.IO.Abstractions` added, `System.IO.Abstractions.TestingHelpers` added (test project)
- **Breaking changes**: None (internal refactoring only, MCP tool APIs unchanged)
- **Test impact**: Existing tests continue to work (real filesystem); new tests can use `MockFileSystem`
