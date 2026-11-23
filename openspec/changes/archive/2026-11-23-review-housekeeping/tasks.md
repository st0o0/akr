## 1. Scan Cache Memory Leak

- [x] 1.1 Change `_scanCache` in `DeepIntelligenceTools` from `static ConcurrentDictionary` to instance `Dictionary<string, List<TypeDecl>>`
- [x] 1.2 Verify `DeepIntelligenceTools` is registered as singleton in `McpSetup.cs` (if not, register it)

## 2. Test Helpers Deduplication

- [x] 2.1 Create `src/Akr.Tests/TestHelpers.cs` with shared `JsonBody` and `CreateTestTarGz` methods
- [x] 2.2 Update `GitHubClientTests.cs` to use shared helpers, remove duplicated methods
- [x] 2.3 Update `GitHubClientCachingTests.cs` to use shared helpers, remove duplicated methods

## 3. UserAgent Version

- [x] 3.1 Replace hardcoded `"akr/0.1"` in `McpSetup.cs` with assembly version from `typeof(McpSetup).Assembly.GetName().Version`

## 4. Rate-Limit Error Message

- [x] 4.1 Update `ThrowIfFailed` in `GitHubClient.cs` to reference `AKR_CACHE_TTL_HOURS` instead of `GITHUB_TOKEN`
- [x] 4.2 Remove "Support authenticated requests" requirement from `openspec/specs/source-fetcher/spec.md`

## 5. Verification

- [x] 5.1 Run full test suite — all 152 tests must pass
- [x] 5.2 Run `dotnet format --verify-no-changes`
