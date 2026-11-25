## ADDED Requirements

### Requirement: Existing tests use MockFileSystem
All test classes that currently use `new FileSystem()` with temp directories SHALL be migrated to `MockFileSystem`.

#### Scenario: ProjectScannerTests uses MockFileSystem
- **WHEN** ProjectScannerTests runs
- **THEN** no temp directories are created on disk and the class does not implement IDisposable

#### Scenario: ProjectContextToolsTests uses MockFileSystem
- **WHEN** ProjectContextToolsTests runs
- **THEN** no temp directories are created on disk and the class does not implement IDisposable

#### Scenario: Config tool tests use MockFileSystem
- **WHEN** ConfigToolsTests, ConfigReferenceToolTests, ConfigFilesTests run
- **THEN** no temp directories are created on disk

#### Scenario: Source and type tool tests use MockFileSystem
- **WHEN** SourceSearcherTests, DeepIntelligenceToolsTests, TypeFinderTests run
- **THEN** no temp directories are created on disk

#### Scenario: Stream and hosting tool tests use MockFileSystem
- **WHEN** StreamStagesToolTests, StreamPersistenceToolsTests, HostingExtensionsToolTests, EcosystemToolsTests run
- **THEN** no temp directories are created on disk

### Requirement: Test behavior preserved after migration
Migrated tests SHALL pass with identical assertions as before.

#### Scenario: All existing tests pass after migration
- **WHEN** `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` runs
- **THEN** all tests pass with zero failures
