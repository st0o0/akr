## Why

Every MCP session start triggers up to 6 GitHub API calls (one per unique repo) to resolve
the latest release version — even though Akka.NET releases happen only every few weeks.
The resolved version is cached in-memory but lost between sessions. This wastes API rate
limit, adds latency to the first tool call, and makes the server fragile when GitHub is
slow or rate-limited.

## What Changes

- Persist resolved version metadata (version, ETag, timestamp) to disk per repo
- Skip GitHub API calls entirely when cached metadata is fresh (< configurable TTL, default 24h)
- Use conditional HTTP requests (If-None-Match with ETag) when TTL expires — GitHub returns
  304 Not Modified when nothing changed, avoiding re-download
- Delete old source version directories after successfully fetching a new version
- Cache the tag format (plain vs v-prefixed) to avoid fallback requests on subsequent fetches

## Capabilities

### New Capabilities

- `cache-metadata`: Disk-persisted metadata per repo (version, ETag, lastChecked, tagFormat)
  with TTL-based freshness checks and conditional HTTP validation
- `source-cleanup`: Automatic deletion of old source version directories after successful
  update to a new version

### Modified Capabilities

- `source-fetcher`: ResolveLatestAsync gains disk cache awareness — checks metadata freshness
  before making API calls, uses conditional requests when stale
- `version-config`: Add `AKR_CACHE_TTL_HOURS` environment variable (default 24) to control
  how long cached version metadata is considered fresh

## Impact

- **Code**: `GitHubClient` — main changes to `ResolveLatestAsync` and `EnsureSourceAsync`
- **Disk layout**: New `~/.akr/sources/meta/` directory with per-repo JSON metadata files
- **Environment**: New optional `AKR_CACHE_TTL_HOURS` env var
- **Tests**: New tests for metadata persistence, TTL logic, conditional requests, cleanup
- **Behavior**: First tool call in a session becomes near-instant when cache is fresh
