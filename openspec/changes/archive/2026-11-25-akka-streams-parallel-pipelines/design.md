## Context

akr is an MCP server (net10.0) providing Akka.NET source intelligence. It fetches source archives from GitHub, caches them locally, and exposes 37 MCP tools for type lookups, config references, and more. Currently it has zero Akka.NET runtime dependencies.

`GitHubClient.EnsureAllSourcesForVersionAsync` fetches 4 repos sequentially via `foreach`/`await`, each taking ~15s (HTTP download + tar.gz extraction). Total ~60s exceeds the MCP tool timeout. `DocsIndex.Build` synchronously scans markdown files on a single thread.

## Goals / Non-Goals

**Goals:**
- Parallelize GitHub source fetching to stay within MCP timeout (~15s)
- Introduce Akka.Streams as a pipeline engine for I/O orchestration
- Use Akka.Hosting for idiomatic DI and lifecycle management
- Dogfood Akka.NET 1.6 beta in the tool that serves Akka.NET intelligence
- Create a `Pipelines/` layer separating orchestration from HTTP+cache concerns

**Non-Goals:**
- Full actor system (no custom actors, no clustering, no remoting)
- AOT activation (`PublishAot=true`) — deferred to follow-up
- Background cache warmup stream (Phase 2)
- Streaming type search pipeline (Phase 2)
- Long-lived streams or `Source.Queue` patterns — all streams are short-lived, materialized on-demand

## Decisions

### 1. Akka.Hosting 1.6.0-beta7 over manual ActorSystem
**Choice**: `services.AddAkka("akr", builder => { })` with empty builder.
**Why**: akr preaches Hosting-first for its users. Using it ourselves is consistent. Akka.Hosting provides `IHostedService` lifecycle, `CoordinatedShutdown` integration, and DI registration for free.
**Alternative**: Manual `ActorSystem.Create()` + custom `IHostedService`. Less code but inconsistent with akr's own guidance.

### 2. Pipelines/ folder over Streams/
**Choice**: `src/Akr/Pipelines/` namespace.
**Why**: Framework-agnostic naming. The folder describes what the code does (orchestrates pipelines) not what library it uses. If Akka.Streams were ever swapped, the folder name still makes sense.
**Alternative**: `Streams/` — more discoverable for Akka developers but couples naming to implementation.

### 3. SourcePipeline wraps GitHubClient
**Choice**: `SourcePipeline` becomes the new facade for tools. It takes `GitHubClient` as a dependency and delegates single-repo fetch+cache to it. Tools call `SourcePipeline.EnsureAllForVersionAsync()` instead of `GitHubClient.EnsureAllSourcesForVersionAsync()`.
**Why**: Minimal tool changes (same signature), clean separation of orchestration (Pipeline) from HTTP+cache (GitHubClient).
**Alternative**: Add Akka.Streams directly into GitHubClient. Simpler but mixes concerns further in an already large class.

### 4. HTTP parallel (4x), extraction sequential (1x)
**Choice**: `SelectAsyncUnordered(4, FetchArchive)` followed by `SelectAsync(1, ExtractArchive)`.
**Why**: HTTP downloads are I/O-bound and benefit from full parallelism. Disk extraction of 4 tar.gz archives simultaneously causes I/O contention and temporary disk space spikes. Sequential extraction is still fast (~2-3s per archive).
**Alternative**: Full parallel extraction. Faster in theory but risks disk I/O bottleneck on slower drives.

### 5. Short-lived streams materialized on-demand
**Choice**: Each call to `EnsureAllForVersionAsync` creates, runs, and disposes a stream. No persistent stream infrastructure.
**Why**: akr handles ~1-10 tool calls per session. The overhead of stream materialization (~1ms) is negligible vs the ~15s fetch time. Simpler lifecycle, no state management.
**Alternative**: Long-lived `Source.Queue` like FunkArr's MediathekViewWeb pattern. Better for high-throughput scenarios but over-engineered for 4-repo fetches.

### 6. DocsPipeline replaces synchronous DocsIndex.Build
**Choice**: `DocsPipeline.BuildIndexAsync()` using `Source.From(mdFiles).SelectAsync(4, ParseMarkdown).RunWith(Sink.Seq)`. DocsIndex becomes a pure data structure built by the pipeline.
**Why**: Markdown parsing is CPU-bound and benefits from parallelism on multi-core. Keeps DocsIndex focused on search logic, not file I/O.
**Alternative**: Keep synchronous Build. Works but blocks the thread and doesn't scale with large doc sets.

### 7. Prerelease dependency acceptable
**Choice**: Take `Akka.Hosting` 1.6.0-beta7 as a dependency.
**Why**: akr is version 0.x with no stability guarantees. The Streams API surface we use (`Source.From`, `SelectAsyncUnordered`, `Sink.Seq`) is stable across Akka versions. We only need to track breaking changes in Akka.Hosting's DI integration, which is minimal.
**Alternative**: Wait for 1.6 GA. Delays the fix for an unknown duration.

## Risks / Trade-offs

- **[Beta dependency instability]** → Pin exact version in `Directory.Packages.props`. Track Akka.NET 1.6 release notes. akr is 0.x so breaking changes are acceptable.
- **[Binary size increase]** → Akka.NET runtime adds ~5-10MB. Acceptable for a dotnet tool. Monitor with `dotnet publish` size checks.
- **[Startup time increase]** → ActorSystem initialization adds ~200-500ms. MCP server starts once per session, so this is acceptable.
- **[GitHubClient refactor scope]** → Moving `EnsureAll*` methods to SourcePipeline touches all 7 tool classes. Mitigated by keeping the same method signature.
- **[Extraction throttling assumption]** → Sequential extraction assumes disk I/O is the bottleneck. If profiling shows otherwise, can increase parallelism later.
