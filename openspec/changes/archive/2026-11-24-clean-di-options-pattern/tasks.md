## 1. AkrOptions

- [x] 1.1 Create `AkrOptions` record in `Akr/Configuration/` with `CacheRoot`, `CacheTtlHours`, `ProjectPath` properties and defaults
- [x] 1.2 Add static `BindFromEnvironment` method that reads env vars and returns configured `Action<AkrOptions>`
- [x] 1.3 Register `AkrOptions` via `services.Configure<AkrOptions>(...)` in `McpSetup`
- [x] 1.4 Register `TimeProvider.System` as singleton in `McpSetup`

## 2. ProjectContext Service

- [x] 2.1 Create `ProjectContext` class with volatile `Current` property (thread-safe read/write)
- [x] 2.2 Remove `Current` property and `SetResult` method from `ProjectScanner`
- [x] 2.3 Create `ProjectContextInitializer : IHostedService` — reads options, scans, sets ProjectContext
- [x] 2.4 Register `ProjectContext` as singleton and `ProjectContextInitializer` as hosted service

## 3. GitHubClient Refactor

- [x] 3.1 Change `GitHubClient` constructor: replace `string? cacheRoot`, `TimeSpan? cacheTtl`, `TimeProvider?` with `IOptions<AkrOptions>`, `ProjectContext`, `TimeProvider`
- [x] 3.2 Remove `ParseCacheTtl` static method — TTL comes from options
- [x] 3.3 Read `_cacheRoot` from `options.Value.CacheRoot` and `_cacheTtl` from `TimeSpan.FromHours(options.Value.CacheTtlHours)`
- [x] 3.4 Replace `_projectScanner?.Current` with `_projectContext.Current` in `ResolveLatestAsync`
- [x] 3.5 Make `JsonSerializerOptions` a `static readonly` field (already exists, verify)

## 4. McpSetup Cleanup

- [x] 4.1 Rewrite `McpSetup.Setup` — only `services.Add*` and `.With*` calls, no factory lambdas with logic
- [x] 4.2 Remove `InitializeProjectContext` method from `McpSetup`
- [x] 4.3 Clean `AddHttpClient<GitHubClient>` — no `sp` parameter needed in lambda

## 5. Update Consumers

- [x] 5.1 Update `ProjectContextTools` to inject `ProjectContext` instead of `ProjectScanner`
- [x] 5.2 Update `ProjectContextResources` to inject `ProjectContext` and `IOptions<AkrOptions>` — remove inline env var reads
- [x] 5.3 Make `JsonSerializerOptions` in `ProjectContextResources` a `static readonly` field

## 6. Fix Tests

- [x] 6.1 Update `GitHubClientTests.CreateClient` — use `Options.Create(new AkrOptions(...))` and `ProjectContext`
- [x] 6.2 Update `GitHubClientCachingTests.CreateClient` — same pattern
- [x] 6.3 Update `VersionResolutionTests` — use new constructor with Options and ProjectContext
- [x] 6.4 Update `ProjectScannerTests` — remove tests for `Current`/`SetResult` (moved to ProjectContext)
- [x] 6.5 Update `ProjectContextToolsTests` — inject `ProjectContext` instead of checking `_scanner.Current`
- [x] 6.6 Update `ProjectContextResourcesTests` — use `ProjectContext` and `IOptions<AkrOptions>`
- [x] 6.7 Add unit tests for `AkrOptions` binding (defaults, env override, invalid values)
- [x] 6.8 Add unit test for `ProjectContextInitializer`
- [x] 6.9 Run full test suite and format check
