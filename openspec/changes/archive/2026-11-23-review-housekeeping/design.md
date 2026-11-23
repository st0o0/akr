## Context

Three cleanup items from project review. All are internal refactoring with no behavior changes.

## Goals / Non-Goals

**Goals:**
- Eliminate the memory leak from the static scan cache.
- Remove test code duplication.
- Make UserAgent version track releases automatically.
- Fix the misleading rate-limit error message and remove the dead spec requirement.

**Non-Goals:**
- Changing any public behavior or tool output.
- Adding GITHUB_TOKEN support (unauthenticated limits are sufficient).
- Restructuring the project beyond these targeted fixes.

## Decisions

### 1. Static cache → instance field

Remove `static` from `DeepIntelligenceTools._scanCache`. The field becomes a plain `Dictionary<string, List<TypeDecl>>` (no longer needs `ConcurrentDictionary` since MCP tool calls are serialized per-session). Cache lives as long as the DI-registered `DeepIntelligenceTools` instance — which is the session lifetime. Old entries for previous source versions get GC'd when the instance is collected.

### 2. Shared test helpers

Create `src/Akr.Tests/TestHelpers.cs` with `internal static` methods:
- `JsonBody(object obj)` → `byte[]`
- `CreateTestTarGz(string prefix, (string path, string content)[] files)` → `byte[]`

The `FakeHandler` classes differ between the two test files (one returns `(Status, Body)`, the other `(Status, Body, Etag)`), so they stay in their respective test files.

### 3. UserAgent from assembly version

Replace `"akr/0.1"` with `$"akr/{typeof(McpSetup).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}"` in the HttpClient configuration. The version comes from `Directory.Build.props` which release-please manages.

### 4. Rate-limit error message fix

Change the `ThrowIfFailed` message from referencing `GITHUB_TOKEN` (never implemented) to `AKR_CACHE_TTL_HOURS` (which actually reduces API calls). Remove the "Support authenticated requests" requirement from `openspec/specs/source-fetcher/spec.md` since it was never implemented and the analysis shows unauthenticated limits (60 req/h) are sufficient given the 24h TTL.

## Risks / Trade-offs

- **[Risk] Scan cache no longer shared across tool instances** → Not a risk: MCP tools are instantiated per-call by DI but `DeepIntelligenceTools` is registered as singleton, so the cache is effectively session-scoped. Same behavior as before, minus the leak.
