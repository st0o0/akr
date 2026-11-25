## 1. Dependency setup

- [x] 1.1 Add `Akka.Hosting` 1.6.0-beta7 to `src/Directory.Packages.props`
- [x] 1.2 Add `<PackageReference Include="Akka.Hosting" />` to `src/Akr/Akr.csproj`
- [x] 1.3 Verify `dotnet build src/akr.slnx` succeeds with new dependency
- [x] 1.4 Verify Akka.Streams is available transitively (no direct package reference needed)

## 2. Akka.Hosting setup

- [x] 2.1 Add `services.AddAkka("akr", builder => { })` to `McpSetup.cs`
- [x] 2.2 Verify ActorSystem is resolvable from DI in a unit test
- [x] 2.3 Verify graceful shutdown — ActorSystem terminates when host stops

## 3. SourcePipeline

- [x] 3.1 Create `src/Akr/Pipelines/SourcePipeline.cs` with constructor taking `GitHubClient` and `ActorSystem`
- [x] 3.2 Implement `EnsureAllForVersionAsync(string? version, CancellationToken)` with cache-first check
- [x] 3.3 Implement parallel fetch stream: `Source.From(repos).SelectAsyncUnordered(4, fetch).RunWith(Sink.Seq)`
- [x] 3.4 Split stream into parallel HTTP download (4x) and sequential extraction (1x)
- [x] 3.5 Integrate cache update and LRU eviction after successful parallel fetch
- [x] 3.6 Register `SourcePipeline` as singleton in `McpSetup.cs`
- [ ] 3.7 Unit tests: parallel fetch, cache hit skips stream, null version delegates to latest

## 4. DocsPipeline

- [x] 4.1 Create `src/Akr/Pipelines/DocsPipeline.cs` with constructor taking `GitHubClient`, `DocsIndex`, `FileService`, and `ActorSystem`
- [x] 4.2 Implement `BuildIndexAsync(CancellationToken)` — ensure docs fetched, then parallel markdown parse via Akka.Streams
- [x] 4.3 Refactor `DocsIndex` to pure data structure — remove `Build(string root)` and `FileService` dependency from constructor
- [x] 4.4 Add entries via new `DocsIndex.AddEntry`/`DocsIndex.AddEntries` methods instead of internal file scanning
- [x] 4.5 Register `DocsPipeline` as singleton in `McpSetup.cs`
- [x] 4.6 Unit tests: parallel parse populates index, search works after pipeline build

## 5. Tool migration

- [x] 5.1 Update `TypeTools` — inject `SourcePipeline`, replace `github.EnsureAllSourcesForVersionAsync` calls
- [x] 5.2 Update `StreamTools` — same pattern
- [x] 5.3 Update `StreamPersistenceTools` — same pattern
- [x] 5.4 Update `ConfigMigrationTools` — same pattern
- [x] 5.5 Update `HostingTools` — same pattern
- [x] 5.6 Update `EcosystemTools` — same pattern
- [x] 5.7 Update `DeepIntelligenceTools` — same pattern
- [x] 5.8 Update `DocsTools` — inject `DocsPipeline`, replace `DocsIndex.Build` calls
- [x] 5.9 Update `VersionTools` — inject `SourcePipeline`, replace `github.EnsureAllSourcesForVersionAsync` calls
- [x] 5.10 Remove `EnsureAllSourcesAsync` and `EnsureAllSourcesForVersionAsync` from `GitHubClient` public API (or make internal)

## 6. GitHubClient cleanup

- [x] 6.1 Remove `_cachedRoots` field and `EnsureAllSourcesAsync` method (moved to SourcePipeline)
- [x] 6.2 Remove `_cachedRootsByVersion` dictionary (moved to SourcePipeline)
- [x] 6.3 Keep `EnsureSourceAsync`, `EnsureSourceForVersionAsync`, `EnsureDocsAsync` as public single-repo methods
- [x] 6.4 Expose cache-check methods for SourcePipeline to query before materializing streams
- [x] 6.5 Unit tests: verify existing GitHubClient caching tests still pass

## 7. Build and verify

- [x] 7.1 Run `dotnet build src/akr.slnx` — zero errors, zero warnings
- [x] 7.2 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 7.3 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [ ] 7.4 Manual smoke test: `source_diff(typeName: "Props", versionA: "1.5.30", versionB: "1.5.35")` completes within MCP timeout
- [ ] 7.5 Manual smoke test: `docs_search("cluster sharding")` works via DocsPipeline
- [ ] 7.6 Manual smoke test: `lookup_type("Props", version: "1.5.35")` returns correct version via SourcePipeline
