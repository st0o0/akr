## Context

akr currently reads configuration from environment variables inline (`Environment.GetEnvironmentVariable`), passes config as raw constructor parameters (`string? cacheRoot`, `TimeSpan? cacheTtl`), and runs initialization logic inside DI factory lambdas. The `ProjectScanner` singleton holds mutable state via a public `Current` property without thread safety. `JsonSerializerOptions` are allocated per call.

## Goals / Non-Goals

**Goals:**
- Single `AkrOptions` record for all configuration, bound from environment variables
- All services fully DI-resolvable with no optional constructor params
- Stateless `ProjectScanner`, mutable state in dedicated `ProjectContext`
- Initialization logic in `IHostedService`, not DI factories
- Thread-safe mutable state

**Non-Goals:**
- Adding `appsettings.json` support (env vars are correct for a CLI tool)
- Changing any external behavior or tool signatures
- Refactoring tool classes themselves

## Decisions

### Decision 1: AkrOptions as a plain record

**Choice:** `AkrOptions` record in `Akr/Configuration/` with properties: `CacheRoot` (string), `CacheTtlHours` (int), `ProjectPath` (string?). Populated from env vars in a static factory method, registered as `IOptions<AkrOptions>` via `services.Configure<AkrOptions>`.

**Rationale:** A dotnet tool has no `appsettings.json` — env vars are the config source. Using `IOptions<T>` still gives testability (inject `Options.Create(new AkrOptions(...))` in tests) and a single binding point. The record is immutable after construction.

**Alternative:** `IConfiguration` with env var provider — overkill for 3 config values in a CLI tool.

### Decision 2: ProjectContext as a separate service

**Choice:** New `ProjectContext` class holding the current `ProjectScanResult` with thread-safe read/write. `ProjectScanner` becomes a pure stateless scanner. `GitHubClient`, `ProjectContextTools`, and `ProjectContextResources` inject `ProjectContext`.

**Rationale:** Separates "scanning" (stateless, cacheable) from "current state" (mutable, shared). Thread safety via `volatile` field — no locks needed since writes are atomic reference swaps and reads never see partial state.

### Decision 3: ProjectContextInitializer as IHostedService

**Choice:** `ProjectContextInitializer : IHostedService` reads `AkrOptions.ProjectPath` and CWD, calls `ProjectScanner.Scan`, sets `ProjectContext.Current`. Registered via `services.AddHostedService<>()`.

**Rationale:** `IHostedService.StartAsync` runs before the MCP server accepts connections — guaranteed initialization order. No business logic in DI factories.

### Decision 4: GitHubClient takes IOptions<AkrOptions> + ProjectContext

**Choice:** Constructor becomes `GitHubClient(HttpClient, ILogger, FileService, ProjectContext, IOptions<AkrOptions>, TimeProvider)`. No optional params. `CacheRoot` and `CacheTtl` come from options. Tests use `Options.Create(...)`.

### Decision 5: TimeProvider registered in DI

**Choice:** `services.AddSingleton(TimeProvider.System)`. Tests override with `FakeTimeProvider` via DI.

### Decision 6: Static JsonSerializerOptions

**Choice:** `private static readonly JsonSerializerOptions` fields wherever `JsonSerializer` is called. Follows the .NET performance guidance.

## Risks / Trade-offs

**[Test churn]** → Every test creating `GitHubClient` must change. Mitigation: One-time cost, tests become simpler (no named args guessing).

**[Startup order]** → `IHostedService` runs during host startup. If scanning is slow, it delays server readiness. Mitigation: Scanning is sub-millisecond (XML parse of 1-2 files).
