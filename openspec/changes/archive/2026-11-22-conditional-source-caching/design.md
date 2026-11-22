## Context

`GitHubClient.ResolveLatestAsync` queries `GET /repos/{repo}/releases/latest` for every
unique repo on every MCP session start. The resolved version is held in an in-memory
dictionary (`_latestVersions`) that is lost when the process exits. With 5-6 repos registered,
this means 5-6 API calls per session — even though Akka.NET releases happen every few weeks.

The source archives themselves are already cached on disk (directory-exists check), but the
version resolution step is not.

## Goals / Non-Goals

**Goals:**

- Eliminate GitHub API calls when the cached version is known-fresh
- Use conditional HTTP requests (ETag) when freshness expires, so unchanged repos cost
  almost nothing
- Clean up old source directories after a successful version update
- Make the cache TTL configurable for different deployment scenarios

**Non-Goals:**

- Pre-release or branch tracking — only stable releases via `/releases/latest`
- Background refresh or scheduled polling — cache validation happens on demand
- Multi-version support — only the latest version is kept per repo
- NuGet-based version resolution — stay with GitHub Releases API

## Decisions

### Decision 1: TTL + conditional request (not always-conditional)

**Choice**: Check a TTL first; only make a conditional HTTP request when the TTL expires.

**Alternatives considered**:
- *Always conditional*: Every session start sends 5-6 conditional requests. Even though
  304s are cheap, they still add latency (~200ms each) and consume rate limit.
- *TTL only (no conditional)*: After TTL expires, full request. Wastes bandwidth re-downloading
  the full response body when nothing changed.

**Rationale**: TTL + conditional gives zero API calls during the fresh window and minimal
cost (304) when stale. Best of both worlds.

### Decision 2: Per-repo JSON metadata files

**Choice**: Store metadata in `~/.akr/sources/meta/{repoName}.json` with structure:
```json
{
  "version": "1.5.75",
  "etag": "W/\"abc123...\"",
  "lastChecked": "2026-11-22T10:00:00Z",
  "tagFormat": "plain"
}
```

**Alternatives considered**:
- *Single metadata file for all repos*: Simpler but creates contention if we ever parallelize,
  and partial corruption loses all metadata.
- *SQLite*: Overkill for 5-6 entries.

**Rationale**: One file per repo is atomic (write-tmp + rename), isolated, and trivially
human-inspectable.

### Decision 3: Default TTL of 24 hours

**Choice**: 24-hour default, configurable via `AKR_CACHE_TTL_HOURS` environment variable.

**Rationale**: Akka.NET releases are infrequent (weeks apart). 24h means at most one
conditional check per day per repo. Users who need fresher data can set a lower value.
Setting TTL to 0 forces a conditional check on every session.

### Decision 4: Atomic metadata writes

**Choice**: Write metadata to a `.tmp` file then rename, same pattern used for source extraction.

**Rationale**: Prevents corrupted metadata if the process is killed mid-write.

### Decision 5: Tag format caching

**Choice**: Store `tagFormat` ("plain" or "v-prefixed") in metadata after the first successful
fetch for a repo.

**Rationale**: The current code tries the plain tag first, then falls back to `v{version}`.
Caching the format avoids one failed HTTP request per update cycle. All current Akka repos
use plain tags, but the fallback is preserved for future repos.

## Risks / Trade-offs

- **[Stale cache after manual deletion]** → If a user deletes the source directory but not
  the metadata, the system sees a fresh TTL but no source. Mitigation: `EnsureSourceAsync`
  already checks `Directory.Exists` and re-downloads if missing — metadata freshness only
  controls version resolution, not source presence.

- **[Clock skew]** → If system clock is wrong, TTL checks misbehave. Mitigation: Use UTC
  throughout; this is an edge case not worth over-engineering.

- **[Metadata format evolution]** → Future changes may need additional fields. Mitigation:
  Use `JsonSerializerOptions` with `JsonIgnoreCondition.WhenWritingNull` so unknown fields
  are silently ignored. Keep the format flat and simple.
