## Why

The current codebase has 4 interfaces, 4 implementations, a facade, and a separate Core
project for ~200 lines of actual logic. This is over-engineered for a CLI tool. The
abstractions don't earn their keep — there's exactly one implementation of each interface,
and the layering makes the code harder to follow without adding testability (the real
tests use temp directories, not mocks).

## What Changes

- **Delete Akr.Core project** — merge everything into the Akr host project
- **Replace 4 interfaces + facade with one `GitHubClient`** class:
  - `EnsureSourceCachedAsync(repo)` → resolve latest, download + extract if not cached, return path
  - `GetAllSourceRootsAsync()` → ensure all 5 repos cached, return paths
  - Cache path logic (inline, no class needed)
- **Drop `AKKA_VERSIONS` entirely** — always latest, no version config
- **Tools call `GitHubClient` directly** — no orchestration layer
- **Delete**: `ISourceProvider`, `ISourceFetcher`, `ISourceCache`, `IVersionConfig`,
  `SourceProvider`, `GitHubArchiveFetcher`, `FileSystemSourceCache`,
  `EnvironmentVersionConfig`, `Placeholder`
- **Keep**: `RepoRegistry` (static lookup table), `SourceSearcher`, `TypeFinder`,
  `SearchResult`, `TypeLookupResult`, `SourceFetchException`
- **Flatten test structure** to match

## Capabilities

### Modified Capabilities

- `version-config`: Eliminated — always latest, no config needed
- `source-fetcher`: Merged into `GitHubClient` methods
- `source-provider`: Eliminated — tools call `GitHubClient` directly

## Impact

- **Deleted project**: Akr.Core (merged into Akr)
- **Deleted project**: Akr.Tests.Shared (empty after merge)
- **Deleted files**: 4 interfaces, 4 implementations, Placeholder
- **Modified**: solution file, architecture tests, all test files, McpSetup
- **New file**: `GitHubClient.cs` consolidating all fetch/cache/version logic
- **Net result**: ~50% fewer files, zero interfaces, one project fewer
