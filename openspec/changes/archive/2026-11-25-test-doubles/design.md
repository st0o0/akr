## Context

Tests currently use `new FileSystem()` (real disk) with temp directories and `IDisposable` cleanup. `MockFileSystem` from `System.IO.Abstractions.TestingHelpers` is already a dependency but unused. `TimeProvider` is injected into `SourceCache` and `VersionResolver` but untestable without a fake clock. `FileService` wraps `IFileSystem` and is the filesystem abstraction used everywhere.

## Goals / Non-Goals

**Goals:**
- Enable deterministic time-based tests for SourceCache and VersionResolver
- Eliminate disk I/O and temp dir boilerplate in existing tests
- Use official Microsoft test doubles, no custom fakes

**Non-Goals:**
- Changing production code (FileService, SourceCache, VersionResolver signatures are fine as-is)
- Testing SourceFetcher or SourcePipeline (these need HTTP mocking, separate concern)
- 100% coverage of SourceCache/VersionResolver (focus on the time-dependent and filesystem paths)

## Decisions

### Use FakeTimeProvider over NSubstitute mock
FakeTimeProvider is purpose-built for `TimeProvider` testing with `Advance()` and `SetUtcNow()`. An NSubstitute mock would need manual stubbing of `GetUtcNow()` returns and wouldn't support timer-based scenarios. FakeTimeProvider is an official MS package that matches the BCL contract exactly.

### Pass MockFileSystem through FileService, don't bypass it
Production code uses `FileService`, not `IFileSystem` directly. Tests should construct `new FileService(new MockFileSystem(...))` to test through the same abstraction. This catches bugs in FileService's wrapping logic.

### Migrate existing tests in-place, don't create parallel test classes
Keeps test surface clean. Each existing test class switches from `new FileSystem()` to `new MockFileSystem()` and drops `IDisposable`/temp dir patterns. The test logic stays the same, only setup changes.

### Pre-populate MockFileSystem via constructor dictionary
MockFileSystem accepts a `Dictionary<string, MockFileData>` in its constructor. Use this for test fixtures rather than calling `AddFile` repeatedly. Cleaner setup, all test data visible at construction.

## Risks / Trade-offs

[MockFileSystem behavior differences] The in-memory filesystem may behave differently from real disk on edge cases (path casing on Windows, long paths). Mitigation: existing tests already pass on real FS, so any MockFileSystem-only failure reveals a test bug, not a prod bug.

[Large migration surface] ~14 test file locations need changes. Mitigation: each file is a mechanical transformation (replace FileSystem with MockFileSystem, remove temp dir + Dispose). Low risk per file.
