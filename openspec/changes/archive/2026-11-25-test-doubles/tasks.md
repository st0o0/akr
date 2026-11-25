## 1. Infrastructure

- [x] 1.1 Add `Microsoft.Extensions.TimeProvider.Testing` to `src/Directory.Packages.props`
- [x] 1.2 Add `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />` to `src/Akr.Tests/Akr.Tests.csproj`
- [x] 1.3 Verify build succeeds with new package

## 2. New SourceCache Tests

- [x] 2.1 Create `src/Akr.Tests/Source/SourceCacheTests.cs` with MockFileSystem + FakeTimeProvider setup
- [x] 2.2 Test metadata round-trip (write then read)
- [x] 2.3 Test ReadMetadata returns null for nonexistent file
- [x] 2.4 Test TouchVersionAccess updates LastAccessed to current fake time
- [x] 2.5 Test TouchVersionAccess is no-op for nonexistent version
- [x] 2.6 Test RecordVersionAccess creates new entry
- [x] 2.7 Test RecordVersionAccess updates existing entry timestamp
- [x] 2.8 Test EvictOldVersions removes oldest when over limit
- [x] 2.9 Test EvictOldVersions is no-op when under limit

## 3. New VersionResolver Tests

- [x] 3.1 Create `src/Akr.Tests/Source/VersionResolverTests.cs` with MockFileSystem + FakeTimeProvider + mocked HttpClient
- [x] 3.2 Test returns cached version when TTL is fresh
- [x] 3.3 Test makes HTTP request when TTL is expired
- [x] 3.4 Test prefers project-detected version over cache

## 4. Migrate Existing Tests to MockFileSystem

- [x] 4.1 Migrate `ProjectScannerTests` - replace FileSystem with MockFileSystem, remove IDisposable
- [x] 4.2 Migrate `ProjectContextToolsTests` and `ProjectContextResourcesTests` - replace FileSystem with MockFileSystem, remove IDisposable
- [x] 4.3 Migrate `ConfigFilesTests` - replace FileSystem with MockFileSystem
- [x] 4.4 Migrate `ConfigReferenceToolTests` - replace FileSystem with MockFileSystem
- [x] 4.5 Migrate `ConfigToolsTests` - replace FileSystem with MockFileSystem
- [x] 4.6 Migrate `SourceSearcherTests` - replace FileSystem with MockFileSystem
- [x] 4.7 Migrate `DeepIntelligenceToolsTests` - replace FileSystem with MockFileSystem
- [x] 4.8 Migrate `TypeFinderTests` - replace FileSystem with MockFileSystem
- [x] 4.9 Migrate `StreamStagesToolTests` - replace FileSystem with MockFileSystem
- [x] 4.10 Migrate `StreamPersistenceToolsTests` - replace FileSystem with MockFileSystem
- [x] 4.11 Migrate `HostingExtensionsToolTests` - replace FileSystem with MockFileSystem
- [x] 4.12 Migrate `EcosystemToolsTests` - replace FileSystem with MockFileSystem

## 5. Verification

- [x] 5.1 Run full test suite, all tests pass
- [x] 5.2 Run `dotnet format src/akr.slnx --verify-no-changes`
