## MODIFIED Requirements

### Requirement: Cache source roots per GitHubClient lifetime

`GitHubClient.EnsureAllSourcesAsync` SHALL cache its result after the first successful call. The cache root path SHALL come from `IOptions<AkrOptions>.Value.CacheRoot`, not from a constructor parameter.

All filesystem operations in `GitHubClient` SHALL use `FileService` instead of direct `System.IO` calls. `JsonSerializerOptions` instances SHALL be `static readonly` fields, not allocated per call.

#### Scenario: First call fetches and caches

- **WHEN** `EnsureAllSourcesAsync` is called for the first time
- **THEN** it SHALL resolve all repos and return their source root paths
- **AND** store the result for subsequent calls

#### Scenario: Subsequent calls return cached result

- **WHEN** `EnsureAllSourcesAsync` is called a second time
- **THEN** it SHALL return the previously cached list without calling `EnsureSourceAsync`

#### Scenario: Cache root from options

- **WHEN** `GitHubClient` is constructed
- **THEN** it SHALL read `CacheRoot` from `IOptions<AkrOptions>.Value.CacheRoot`
- **AND** SHALL NOT call `Environment.GetFolderPath` or use string defaults
