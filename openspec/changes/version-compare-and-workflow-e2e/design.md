## Context

akr currently fetches the latest Akka.NET version and cleans up old versions on each fetch.
`GitHubClient.CleanupOldVersions` deletes all directories except the current one. This means
only one version is ever cached, and tools have no parameter to target a different version.

The E2E test skill (`e2e/`) runs 46 isolated tool checks. These caught real bugs (6 failures)
but don't reflect how developers actually chain tools together. Session analysis from FunkArr
and Njord shows three dominant workflow patterns: stream stage exploration, HOCON migration,
and actor debugging — each involving 4-5 tool calls in sequence where later calls depend on
earlier results.

## Goals / Non-Goals

**Goals:**
- Users can look up types/config for a specific Akka.NET version (e.g., 1.5.52)
- Users can diff types or configs between two versions
- E2E tests validate realistic multi-tool workflows, not just individual tools
- Cache can hold 2-3 versions simultaneously without ballooning disk usage

**Non-Goals:**
- Automatic version detection from user's project (nuget.assets.json parsing)
- Side-by-side UI for version comparison
- Keeping every version ever requested (bounded cache with LRU eviction)

## Decisions

### Decision 1: Optional `version` parameter on all tools

**Choice**: Add an optional `version` string parameter to every tool's MCP registration.
When omitted, behavior is unchanged (latest). When provided, `GitHubClient.EnsureSourceAsync`
fetches that specific version instead of resolving latest.

**Rationale**: Minimal API surface change. Every tool already calls `EnsureAllSourcesAsync` →
`EnsureSourceAsync` → `ResolveLatestAsync`. The version parameter short-circuits the resolution
step. No new tools needed for version-specific lookups.

**Alternative considered**: A `set_version` tool that changes a session-wide default — rejected
because MCP tools should be stateless per call; session state across calls is fragile.

### Decision 2: Bounded multi-version cache (max 3 per repo)

**Choice**: Modify `CleanupOldVersions` to keep the N most recently accessed versions (default
3). Track access timestamps in metadata.

**Rationale**: Users comparing two versions need both cached. The common pattern is "latest +
one older version", so 3 gives headroom. Disk impact is bounded: ~50MB per Akka.NET version ×
3 = ~150MB per repo, with 5 repos = ~750MB max.

**Alternative considered**: Keep versions indefinitely with manual cleanup command — rejected
because cache growth would be unbounded and users forget to clean up.

### Decision 3: `source_diff` tool using unified diff

**Choice**: New tool `source_diff(type, versionA, versionB)` that fetches both versions,
runs `TypeTools.FindType` on each, and returns a unified diff of the source. Also supports
`source_diff(configPath, versionA, versionB)` for HOCON comparison. Reuses existing
`config_diff` logic but actually compares two versions instead of "current vs empty".

**Rationale**: This is the #1 missing feature from the session analysis. Njord needed to
compare Throttle across versions — today that requires two separate `lookup_type` calls and
manual comparison. A dedicated diff tool is more token-efficient and actionable.

### Decision 4: Workflow-based E2E test scenarios

**Choice**: Restructure E2E tests around three workflow scenarios derived from session data:

1. **Stream stage exploration**: `stream_stages` → `stage_detail("Throttle")` →
   `lookup_type("TimerGraphStageLogic")` → `type_hierarchy("GraphStage")` →
   `search_source("OnPush.*OnPull")`
2. **HOCON→Hosting migration**: `config_reference("akka.cluster")` →
   `hosting_to_hocon("WithClustering")` → `hosting_extensions("Cluster")` →
   `hosting_startup_order()`
3. **Actor debugging**: `lookup_type("ReceiveActor")` → `virtual_methods("ActorBase")` →
   `supervision_defaults()` → `find_messages(actor)` → `message_protocol(namespace)`

Each scenario validates not just individual tool output but cross-tool consistency (e.g.,
types found by `stage_detail` should also appear in `type_hierarchy`).

**Rationale**: Session analysis showed these three patterns cover >80% of real tool usage.
Testing them as workflows catches integration bugs that isolated tool tests miss (like S4.1
where the HOCON migration workflow was broken but individual tools appeared to work).

Keep existing isolated tool tests as a "smoke test" tier, add workflow tests as a second tier.

## Risks / Trade-offs

- **Cache disk usage**: 3 versions × 5 repos × ~50MB = ~750MB max → Acceptable for a dev
  tool; add `--cache-limit` env var if users complain
- **GitHub API rate limits**: Fetching old versions hits the archive endpoint, not the API —
  no rate limit concern for downloads, only for version resolution
- **E2E test maintenance**: Workflow tests are more complex to maintain than isolated tool
  tests → Mitigated by keeping both tiers; workflow tests are higher-signal, isolated tests
  are lower-maintenance
- **Version resolution for old versions**: `ResolveLatestAsync` only knows about the latest
  release. Fetching a specific version needs the tag name, which may differ (plain vs v-prefixed)
  → Already handled by `FetchArchiveAsync`'s fallback logic
