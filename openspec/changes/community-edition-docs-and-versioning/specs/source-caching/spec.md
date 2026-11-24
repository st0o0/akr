## MODIFIED Requirements

### Requirement: Cache source roots per GitHubClient lifetime

`GitHubClient.EnsureAllSourcesAsync` SHALL cache its result after the first successful call. When a `version` parameter is provided, the cache key SHALL include the version to allow caching results for multiple versions independently. Subsequent calls with the same version (or no version) SHALL return the cached list without re-resolving.

All filesystem operations in `GitHubClient` SHALL use `FileService` instead of direct `System.IO` calls.

#### Scenario: First call fetches and caches

- **WHEN** `EnsureAllSourcesAsync` is called for the first time on a `GitHubClient` instance with no version
- **THEN** it SHALL resolve all repos via `RepoRegistry.GetAllRepos()` and return their source root paths
- **AND** store the result for subsequent calls

#### Scenario: Subsequent calls return cached result

- **WHEN** `EnsureAllSourcesAsync` is called a second time with no version on the same instance
- **THEN** it SHALL return the previously cached list without calling `EnsureSourceAsync` for each repo again

#### Scenario: Version-specific results cached independently

- **WHEN** `EnsureAllSourcesAsync` is called with version `1.5.52`
- **AND** then called again with version `1.5.74`
- **THEN** each SHALL have its own cached result keyed by version
- **AND** a third call with `1.5.52` SHALL return the first cached result

#### Scenario: Cache is per-instance

- **WHEN** two different `GitHubClient` instances call `EnsureAllSourcesAsync`
- **THEN** each SHALL maintain its own independent cache
