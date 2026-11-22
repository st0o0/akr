## Purpose

Cache source roots and parsed type declarations within a session to avoid redundant I/O on every tool call.

## Requirements

### Requirement: Cache source roots per GitHubClient lifetime

`GitHubClient.EnsureAllSourcesAsync` SHALL cache its result after the first successful call. Subsequent calls SHALL return the cached list without re-resolving repositories or re-checking the filesystem.

#### Scenario: First call fetches and caches

- **WHEN** `EnsureAllSourcesAsync` is called for the first time on a `GitHubClient` instance
- **THEN** it SHALL resolve all repos via `RepoRegistry.GetAllRepos()` and return their source root paths
- **AND** store the result for subsequent calls

#### Scenario: Subsequent calls return cached result

- **WHEN** `EnsureAllSourcesAsync` is called a second time on the same `GitHubClient` instance
- **THEN** it SHALL return the previously cached list without calling `EnsureSourceAsync` for each repo again

#### Scenario: Cache is per-instance

- **WHEN** two different `GitHubClient` instances call `EnsureAllSourcesAsync`
- **THEN** each SHALL maintain its own independent cache

### Requirement: Cache parsed type declarations per source tree

`DeepIntelligenceTools` SHALL cache the result of `ScanTypes` keyed by the set of source root paths. When called with the same set of roots, it SHALL return the cached result without re-reading files.

#### Scenario: First ScanTypes call parses all files

- **WHEN** `ScanTypes` is called with a set of source roots for the first time
- **THEN** it SHALL read all `.cs` files, parse type declarations, and return the results
- **AND** cache the results keyed by the root paths

#### Scenario: Subsequent ScanTypes calls return cached results

- **WHEN** `ScanTypes` is called with the same set of source roots
- **THEN** it SHALL return the cached list of `TypeDecl` without re-reading any files

#### Scenario: Different root sets get independent cache entries

- **WHEN** `ScanTypes` is called with root set A, then with root set B
- **THEN** each SHALL have its own cached result
