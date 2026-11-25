## Why

Tests for `SourceCache` and `VersionResolver` don't exist because `TimeProvider` can't be controlled. All existing tests use the real filesystem with temp dirs and `IDisposable` cleanup despite `MockFileSystem` already being a project dependency. Official test doubles from Microsoft solve both problems.

## What Changes

- Add `Microsoft.Extensions.TimeProvider.Testing` package to test project
- Write new `SourceCache` unit tests (metadata read/write, touch access, LRU eviction) using `FakeTimeProvider` + `MockFileSystem`
- Write new `VersionResolver` unit tests (TTL cache freshness, project version detection) using `FakeTimeProvider` + `MockFileSystem`
- Migrate existing test classes from `new FileSystem()` + temp dirs to `MockFileSystem`, removing `IDisposable` patterns

## Capabilities

### New Capabilities
- `test-doubles-infrastructure`: Package setup and shared test helper patterns for FakeTimeProvider and MockFileSystem
- `source-cache-tests`: Unit tests for SourceCache using FakeTimeProvider and MockFileSystem
- `version-resolver-tests`: Unit tests for VersionResolver using FakeTimeProvider and MockFileSystem
- `mock-filesystem-migration`: Migration of existing tests from real FileSystem to MockFileSystem

### Modified Capabilities

## Impact

- `src/Akr.Tests/Akr.Tests.csproj`: new package reference
- `src/Akr.Tests/Analysis/ProjectScannerTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/ProjectContextToolsTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/Config*.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/SourceSearcherTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/DeepIntelligenceToolsTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/TypeFinderTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/StreamStagesToolTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/HostingExtensionsToolTests.cs`: migrate to MockFileSystem
- `src/Akr.Tests/Tools/EcosystemToolsTests.cs`: migrate to MockFileSystem
- New test files for SourceCache and VersionResolver
