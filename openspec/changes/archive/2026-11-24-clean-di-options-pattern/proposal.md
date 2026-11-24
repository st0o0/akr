## Why

akr has configuration scattered across raw `Environment.GetEnvironmentVariable` calls, constructor parameters with nullable defaults, and business logic inside DI factory lambdas. This violates .NET best practices and makes the code harder to test, harder to understand, and fragile under DI resolution.

## What Changes

- Introduce `AkrOptions` as the single source of truth for all configuration (cache root, TTL, project path)
- Replace all `Environment.GetEnvironmentVariable` calls with `IOptions<AkrOptions>`
- Refactor `GitHubClient` constructor from raw optional params to `IOptions<AkrOptions>` + DI-resolved services
- Extract mutable project context state from `ProjectScanner` into a dedicated thread-safe `ProjectContext` service
- Move project scanning initialization from DI factory lambda into `IHostedService`
- Register `TimeProvider` properly in DI
- Use `static readonly JsonSerializerOptions` instead of per-call allocation

## Capabilities

### New Capabilities

- `akr-options`: Centralized configuration via `AkrOptions` record with `IOptions<T>` pattern, replacing scattered env var reads.
- `project-context-service`: Thread-safe service managing mutable project detection state, extracted from `ProjectScanner`.

### Modified Capabilities

- `source-fetcher`: `GitHubClient` constructor changes from raw params to `IOptions<AkrOptions>` injection.
- `version-config`: Environment variable parsing moves from inline static methods to centralized options binding.
- `source-caching`: Cache root path comes from `AkrOptions` instead of constructor parameter.

## Impact

- `GitHubClient` — constructor signature changes, all callers (including tests) must adapt
- `McpSetup` — rewritten: clean service registrations, `IHostedService` for initialization
- `ProjectScanner` — loses mutable `Current` property, becomes stateless scanner only
- `ProjectContextTools` / `ProjectContextResources` — inject `ProjectContext` instead of `ProjectScanner`
- All test files creating `GitHubClient` — adapt to new constructor
- No behavioral changes — purely structural refactor
