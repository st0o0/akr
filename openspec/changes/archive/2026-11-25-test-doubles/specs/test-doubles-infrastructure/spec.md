## ADDED Requirements

### Requirement: FakeTimeProvider package available
The test project SHALL reference `Microsoft.Extensions.TimeProvider.Testing` via central package management.

#### Scenario: Package resolves at build time
- **WHEN** `dotnet build src/akr.slnx` runs
- **THEN** `Microsoft.Extensions.TimeProvider.Testing` resolves and the build succeeds

### Requirement: MockFileSystem used through FileService
Tests SHALL construct `new FileService(new MockFileSystem(...))` rather than using `MockFileSystem` directly, to test through the same abstraction as production code.

#### Scenario: FileService wraps MockFileSystem
- **WHEN** a test creates `new FileService(new MockFileSystem(files))`
- **THEN** `FileService.FileExists`, `ReadAllText`, `Combine`, and other methods operate on the in-memory filesystem
