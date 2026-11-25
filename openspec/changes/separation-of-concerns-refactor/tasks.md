## 1. Source layer — split GitHubClient

- [x] 1.1 Create `src/Akr/Source/` folder
- [x] 1.2 Create `SourceCache.cs` — extract ReadMetadata, WriteMetadata, MetaPath, TouchVersionAccess, RecordVersionAccess, CleanupOldVersions, path resolution from GitHubClient
- [x] 1.3 Create `SourceFetcher.cs` — extract FetchArchiveAsync, ExtractTarAsync, ThrowIfFailed, StripFirstSegment, atomic temp-dir pattern. Consolidate duplicate tag-format detection from EnsureSourceAsync and EnsureSourceForVersionAsync
- [x] 1.4 Create `VersionResolver.cs` — extract ResolveLatestAsync with ProjectScanner, TTL, GitHub API, ETag logic
- [x] 1.5 Move `SourcePipeline.cs` from `Pipelines/` to `Source/`, update namespace to `Akr.Source`, wire to SourceFetcher+SourceCache
- [x] 1.6 Update `McpSetup.cs` — register SourceFetcher (with HttpClient), SourceCache, VersionResolver as singletons. Remove GitHubClient registration
- [x] 1.7 Delete `GitHubClient.cs`
- [x] 1.8 Delete `Pipelines/` folder (now empty)

## 2. Scanning layer — rename Analysis

- [x] 2.1 Create `src/Akr/Scanning/` folder
- [x] 2.2 Move `TypeScanner.cs` from `Analysis/` to `Scanning/`, update namespace
- [x] 2.3 Move `ProjectScanner.cs` from `Analysis/` to `Scanning/`, update namespace
- [x] 2.4 Move `HoconParser.cs` from `Analysis/` to `Scanning/`, update namespace
- [x] 2.5 Move `DocsIndex.cs` from `Analysis/` to `Scanning/`, update namespace
- [x] 2.6 Move `SourceSearcher.cs` from `Tools/` to `Scanning/`, update namespace
- [x] 2.7 Move `FileService.cs` from `Tools/` to root `src/Akr/`, update namespace to `Akr`
- [x] 2.8 Delete `Analysis/` folder (now empty)
- [x] 2.9 Update all `using` statements across codebase for new namespaces

## 3. Docs from mono-repo

- [x] 3.1 Remove `DocsRepo` and `DocsSourcePath` constants from `RepoRegistry.cs`
- [x] 3.2 Remove `EnsureDocsAsync` method from SourceFetcher (or don't port it from GitHubClient)
- [x] 3.3 Update `DocsIndex` to scan `docs/articles/` from cached akka.net source root instead of `src/docs/` from getakka.net
- [x] 3.4 Update `DocsTools` to pass akka.net cached root to DocsIndex instead of calling DocsPipeline
- [x] 3.5 Delete `DocsPipeline.cs`
- [x] 3.6 Remove DocsPipeline registration from `McpSetup.cs`

## 4. Tool reorganization

- [x] 4.1 Create `TypeTools.cs` — merge lookup_type, type_members + type_hierarchy, find_implementations, find_messages, message_protocol, virtual_methods, supervision_defaults
- [x] 4.2 Create `SearchTools.cs` — move search_source from StreamTools
- [x] 4.3 Create `ConfigTools.cs` — move config_reference + config_tree, config_diff, changelog
- [x] 4.4 Update `HostingTools.cs` — keep hosting_extensions, add hosting_to_hocon, hosting_startup_order, hosting_di_patterns, hosting_testkit
- [x] 4.5 Update `StreamTools.cs` — keep stream_stages, add stage_detail, graph_dsl_operators, stream_attributes
- [x] 4.6 Create `PersistenceTools.cs` — move journal_plugins, snapshot_plugins, persistence_query, event_adapter_chain
- [x] 4.7 Create `SerializationTools.cs` — move serializer_bindings, serializer_detail, serialization_manifest
- [x] 4.8 Create `ClusterTools.cs` — move cluster_events, extension_points, dependency_graph
- [x] 4.9 Create `TestKitTools.cs` — move testkit_helpers
- [x] 4.10 Delete old files: `EcosystemTools.cs`, `DeepIntelligenceTools.cs`, `ConfigMigrationTools.cs`, `StreamPersistenceTools.cs`
- [x] 4.11 Update `McpSetup.cs` — replace old `.WithTools<>()` registrations with new tool classes

## 5. Test updates

- [x] 5.1 Update test `using` statements for new namespaces
- [x] 5.2 Update test constructors for split Source classes (SourceFetcher, SourceCache, VersionResolver instead of GitHubClient)
- [x] 5.3 Update tool test constructors for reorganized tool classes
- [x] 5.4 Rename/split test files to match new tool file names where needed

## 6. Build and verify

- [x] 6.1 Run `dotnet build src/akr.slnx` — zero errors, zero warnings
- [x] 6.2 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 6.3 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [x] 6.4 Verify no old namespace/class references remain (grep for GitHubClient, Analysis, Pipelines, EcosystemTools, DeepIntelligenceTools, ConfigMigrationTools, StreamPersistenceTools, getakka.net)
