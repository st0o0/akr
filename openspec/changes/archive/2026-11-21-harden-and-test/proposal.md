## Why

A test audit revealed that `GitHubClient` — the entry point for all 6 tools — has only 2 tests
and a critical bug: failed downloads leave incomplete cache directories that are never retried.
Additionally, `CacheRoot` is static mutable state causing potential test parallelism issues,
error handling for HTTP failures is missing, and the SmokeTests duplicate unit tests 1:1.

## What Changes

- **Fix partial cache bug**: Extract to temp dir, rename atomically on success. Failed downloads
  leave no trace.
- **Make CacheRoot instance-based**: Constructor parameter with default, not static property.
  Eliminates shared mutable state.
- **Harden error handling**: HTTP 429/403 (rate limit), 500 (server error), network failures
  during download, missing `tag_name` in API response.
- **Invalid regex handling**: `search_source` catches `RegexParseException` and returns a
  user-friendly error instead of crashing.
- **Add missing GitHubClient tests**: Tag fallback, cache hit, error propagation, resolve
  failure, atomic extraction.
- **Delete SmokeTests**: Redundant with unit tests + MCP integration tests.
- **Clean up test isolation**: Each test creates its own temp cache root.

## Capabilities

### Modified Capabilities

- `source-fetcher`: Atomic extraction, error handling for HTTP failures and rate limits.
- `search-source-tool`: Invalid regex returns error message instead of exception.

## Impact

- **Modified**: `GitHubClient.cs` (atomic cache, instance CacheRoot, error handling)
- **Modified**: `StreamTools.cs` (regex error handling in search_source)
- **Modified**: `McpSetup.cs` (register GitHubClient with CacheRoot)
- **Deleted**: `SmokeTests.cs`
- **Rewritten**: `GitHubClientTests.cs` (comprehensive coverage)
- **Modified**: `McpServerTests.cs` (use instance CacheRoot)
- **Net test count**: similar (remove 7 SmokeTests, add ~10 GitHubClient tests)
