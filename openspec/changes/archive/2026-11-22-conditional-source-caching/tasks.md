## 1. Cache Metadata Model

- [x] 1.1 Add `RepoMetadata` record with `Version`, `Etag`, `LastChecked` (DateTimeOffset), `TagFormat` properties
- [x] 1.2 Add metadata read/write methods to `GitHubClient` — read JSON from `{CacheRoot}/meta/{repoName}.json`, write atomically via tmp+rename
- [x] 1.3 Add `AKR_CACHE_TTL_HOURS` parsing (default 24, warn on invalid) — accept via constructor or configuration

## 2. Conditional Version Resolution

- [x] 2.1 Modify `ResolveLatestAsync` to check disk metadata first — if fresh (within TTL), return cached version without HTTP call
- [x] 2.2 When metadata is stale, send conditional request with `If-None-Match` header using cached ETag
- [x] 2.3 Handle 304 (update `lastChecked`, return cached version) and 200 (parse new version, update metadata)
- [x] 2.4 On missing/corrupt metadata, fall through to full request and persist new metadata

## 3. Source Cleanup

- [x] 3.1 After successful new version extraction in `EnsureSourceAsync`, identify and delete old version directories for the same repo
- [x] 3.2 Wrap cleanup in try/catch — log warning on failure, never throw

## 4. Tag Format Caching

- [x] 4.1 After successful `FetchArchiveAsync`, record tag format (plain or v-prefixed) in metadata
- [x] 4.2 On subsequent fetches, use cached tag format to skip fallback attempt

## 5. Tests

- [x] 5.1 Test fresh metadata returns cached version without HTTP call
- [x] 5.2 Test stale metadata triggers conditional request, 304 updates timestamp
- [x] 5.3 Test stale metadata with 200 response downloads new version and updates metadata
- [x] 5.4 Test missing/corrupt metadata falls through to full request
- [x] 5.5 Test old version directory deleted after successful new version fetch
- [x] 5.6 Test cleanup failure logs warning but does not throw
- [x] 5.7 Test `AKR_CACHE_TTL_HOURS` parsing (valid, zero, invalid, missing)
- [x] 5.8 Test cached tag format avoids fallback request
