## Why

Project review identified four low-risk cleanup items: a static cache that leaks memory, duplicated test helpers, a hardcoded UserAgent version, and a misleading error message. None affect correctness but all improve maintainability.

## What Changes

- Change `DeepIntelligenceTools._scanCache` from `static ConcurrentDictionary` to an instance field so the cache is scoped to the DI lifetime and old entries get GC'd.
- Extract duplicated test helpers (`JsonBody`, `CreateTestTarGz`, `FakeHandler`) from `GitHubClientTests` and `GitHubClientCachingTests` into a shared `TestHelpers` class.
- Replace hardcoded `"akr/0.1"` UserAgent in `McpSetup.cs` with the assembly version so it tracks release-please bumps automatically.
- Fix rate-limit error message in `GitHubClient.ThrowIfFailed` to reference `AKR_CACHE_TTL_HOURS` instead of non-existent `GITHUB_TOKEN` support.
- Remove the never-implemented `GITHUB_TOKEN` requirement from the `source-fetcher` spec.

## Capabilities

### New Capabilities

(none — purely internal refactoring)

### Modified Capabilities

- `source-fetcher`: Remove the never-implemented "Support authenticated requests" requirement and fix the rate-limit error message to reference `AKR_CACHE_TTL_HOURS`.

## Impact

- `src/Akr/Tools/DeepIntelligenceTools.cs` — `_scanCache` becomes instance field
- `src/Akr/GitHubClient.cs` — error message in `ThrowIfFailed` updated
- `src/Akr/Configuration/McpSetup.cs` — UserAgent reads from assembly version
- `src/Akr.Tests/GitHubClientTests.cs` — helpers extracted
- `src/Akr.Tests/GitHubClientCachingTests.cs` — helpers extracted
- `src/Akr.Tests/TestHelpers.cs` — new shared file
- No API changes, no behavior changes for consumers.
