## Context

Test audit found GitHubClient at 2/5 quality with a real cache corruption bug and missing
error paths. This change hardens the core infrastructure.

## Goals / Non-Goals

**Goals:**
- Atomic cache extraction (no partial directories)
- Instance-based CacheRoot (no static mutable state)
- Robust HTTP error handling
- GitHubClient test coverage from 2/5 to 5/5
- Clean test isolation

**Non-Goals:**
- Retry logic with backoff (future enhancement)
- Offline mode / cache-only mode
- Changing tool behavior or output format

## Decisions

### 1. Atomic extraction via temp directory

```
BEFORE:
  EnsureSourceAsync → Directory.CreateDirectory(targetPath) → extract → (fail = partial dir)

AFTER:
  EnsureSourceAsync → extract to {targetPath}.tmp → Directory.Move → (fail = tmp cleaned up)
```

`Directory.Move` is atomic on the same filesystem. On failure, delete the `.tmp` dir in a
finally block. The `Directory.Exists(targetPath)` check in the cache hit path remains correct
because a `.tmp` dir is never renamed to `targetPath` on failure.

### 2. Instance-based CacheRoot

```csharp
public sealed class GitHubClient(HttpClient httpClient, ILogger<GitHubClient> logger, string? cacheRoot = null)
{
    private readonly string _cacheRoot = cacheRoot
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akr", "sources");
```

Tests pass their own temp directory. Production uses the default. No more static state.
McpSetup registers it via the `AddHttpClient<GitHubClient>` factory.

### 3. Error handling strategy

| HTTP Status | Behavior |
|-------------|----------|
| 404 | Tag not found → try v-prefix fallback → `SourceFetchException` |
| 403 / 429 | Rate limited → `SourceFetchException` with message about rate limit |
| 5xx | Server error → `SourceFetchException` with status code |
| Network error | `HttpRequestException` propagates → tool returns error message |

Tools catch `SourceFetchException` and `HttpRequestException` and return user-friendly
error strings instead of letting exceptions crash the MCP tool call.

### 4. Invalid regex handling

```csharp
// In StreamTools.search_source:
try { new Regex(query, RegexOptions.NonBacktracking, ...); }
catch (RegexParseException ex) { return $"Invalid regex pattern: {ex.Message}"; }
```

### 5. Delete SmokeTests

SmokeTests test `SourceSearcher.Search` and `TypeTools.FindType` with slightly different
fixture data than the unit tests. The MCP integration tests already cover the full stack.
No unique coverage is lost.
