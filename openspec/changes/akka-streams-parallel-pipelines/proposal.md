## Why

GitHub source fetching in `GitHubClient.EnsureAllSourcesForVersionAsync` runs sequentially — 4 repos × ~15s = ~60s, exceeding the MCP tool timeout. This blocks `source_diff` and `migration_guide` for uncached versions. Additionally, docs index building (`DocsIndex.Build`) is synchronous and blocks the calling thread.

akr is the Akka.NET source intelligence tool but has zero Akka dependencies. Adding Akka.Streams 1.6.0-beta7 (AOT-compatible, net10.0-only) via Akka.Hosting solves the parallelization problem while dogfooding the framework akr serves.

## What Changes

- Add `Akka.Hosting` 1.6.0-beta7 as first Akka dependency (brings `Akka.Streams` transitively)
- Register minimal `ActorSystem` via `services.AddAkka("akr")` — no HOCON, no actors, just Streams materializer
- Create `Pipelines/SourcePipeline` — parallel repo fetching via `Source.From(repos).SelectAsyncUnordered(4, fetch).RunWith(Sink.Seq)`
- Create `Pipelines/DocsPipeline` — parallel markdown parsing via `Source.From(mdFiles).SelectAsync(4, parse).RunWith(Sink.Last)`
- Refactor `GitHubClient` to pure HTTP+cache worker; pipelines handle orchestration
- Update all 37 MCP tools to route through pipelines instead of `GitHubClient` directly

## Capabilities

### New Capabilities
- `akka-hosting-setup`: ActorSystem registration via Akka.Hosting, DI wiring, lifecycle management with CoordinatedShutdown
- `source-pipeline`: Parallel fetch orchestration — HTTP downloads parallel (4x), disk extraction sequential (1x) to avoid I/O contention
- `docs-pipeline`: Parallel markdown parsing and incremental index building replacing synchronous DocsIndex.Build

### Modified Capabilities
- `source-caching`: GitHubClient loses orchestration responsibility; EnsureAll* methods move to SourcePipeline
- `version-pinning`: Version-specific fetches routed through SourcePipeline instead of GitHubClient directly

## Impact

- **Dependencies**: `Akka.Hosting` 1.6.0-beta7 added to `Directory.Packages.props` (prerelease)
- **Code**: New `Pipelines/` folder with `SourcePipeline.cs` and `DocsPipeline.cs`; `GitHubClient.cs` simplified; `McpSetup.cs` gains `AddAkka`; all 7 tool classes updated
- **Binary size**: Increases due to Akka.NET runtime (~5-10MB)
- **Startup**: Slightly slower due to ActorSystem initialization
- **Performance**: Fetch time drops from ~60s to ~15s for uncached versions
