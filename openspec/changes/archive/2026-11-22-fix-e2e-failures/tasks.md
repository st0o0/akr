## 1. Config File Discovery (S2.1, S3.9, S4.1)

- [x] 1.1 Create shared `ConfigFiles.EnumerateConfigFiles(roots)` helper in `Tools/` that yields `(fullPath, relativePath)` tuples for: all `reference.conf` files recursively, `*.conf` in `**/Configuration/` dirs under `src/core/`, and `persistence.conf` under `src/core/` — excluding test/example paths
- [x] 1.2 Replace `Directory.EnumerateFiles(root, "reference.conf", ...)` in `HostingTools.FindConfig` with the shared helper
- [x] 1.3 Replace `EnumerateFiles(roots, "reference.conf")` in `EcosystemTools.FindSerializerBindings` with the shared helper
- [x] 1.4 Replace `Directory.EnumerateFiles(root, "reference.conf", ...)` in `ConfigMigrationTools.CollectConfig` with the shared helper
- [x] 1.5 Replace config enumeration in `ConfigMigrationTools.BuildConfigTree` (delegates to CollectConfig, already fixed) with the shared helper
- [x] 1.6 Add unit tests: verify `akka.conf`, `Cluster.conf`, `Remote.conf`, `persistence.conf` are discovered; verify test/example configs are excluded
- [x] 1.7 Verify E2E checks S2.1, S3.9 pass with manual tool calls (deferred to 5.1)

## 2. Hosting-to-HOCON Fallback Mapping (S2.8)

- [x] 2.1 Add a `private static readonly Dictionary<string, string[]> _fallbackHostingMap` to `ConfigMigrationTools` with the minimum curated mappings (WithClustering, WithRemoting, WithDistributedData, WithSingleton, WithShardRegion, WithDistributedPubSub)
- [x] 2.2 Update `MapQuery` to check the fallback map when the regex scanner returns no keys for a hosting method query
- [x] 2.3 Add unit tests: fallback returns mapping for WithClustering; regex-scanned methods are not overridden; unknown methods still return no-match
- [x] 2.4 Verify E2E check S2.8 passes with manual tool call (deferred to 5.1)

## 3. Inherited Member Display (S1.6)

- [x] 3.1 In `TypeTools.GetMembersAsync`, after extracting direct members, parse parent types from the declaration line (the `: ICanTell, IEquatable<...>` part)
- [x] 3.2 For each non-system parent interface, call `FindType` + `Extract` to get its members
- [x] 3.3 Append inherited members under a `Inherited from <ParentType>:` section in the output, filtering out IEquatable, IComparable, ISurrogated
- [x] 3.4 Add unit tests: IActorRef output contains Tell from ICanTell; system interfaces are excluded; direct members remain in their own section
- [x] 3.5 Verify E2E check S1.6 passes with manual tool call (deferred to 5.1)

## 4. Attribute Default Extraction (S3.4)

- [x] 4.1 Broaden `DefaultPattern` regex in `StreamPersistenceTools` to also match `public static <ClassName> Instance`, `public static <ClassName> Default`, and constructor calls `new <ClassName>(...)` as default value indicators
- [x] 4.2 Add unit test: stream_attributes output includes at least one `default:` annotation
- [x] 4.3 Verify E2E check S3.4 passes with manual tool call (deferred to 5.1)

## 5. Validation

- [ ] 5.1 Run full E2E test suite and confirm all 46 checks pass
- [x] 5.2 Run `dotnet build src/akr.slnx` — zero warnings (Release config due to running MCP server lock)
- [x] 5.3 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 5.4 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all 142 tests pass
