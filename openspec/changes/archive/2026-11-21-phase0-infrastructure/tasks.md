## 1. Repo Registry

- [x] 1.1 Create `PackageEntry` record (repo, source path) and `RepoRegistry` static class with the 24-package mapping
- [x] 1.2 Add `GetEntry(packageName)` returning `PackageEntry?` and `GetAllRepos()` returning distinct repos
- [x] 1.3 Add tag resolution logic: try `{version}`, fall back to `v{version}`
- [x] 1.4 Unit tests for registry lookups, unknown packages, and tag resolution

## 2. Version Configuration

- [x] 2.1 Create `VersionConfig` that parses `AKKA_VERSIONS` env var into a dictionary of package→version
- [x] 2.2 Validate package names against `RepoRegistry`, log warnings for unknown packages
- [x] 2.3 Handle edge cases: empty/missing env var, malformed entries, whitespace
- [x] 2.4 Register as singleton in DI (`IVersionConfig`)
- [x] 2.5 Unit tests for parsing, validation, and edge cases

## 3. Source Fetcher

- [x] 3.1 Create `ISourceFetcher` interface with `FetchAsync(repo, tag, targetPath)` method
- [x] 3.2 Implement `GitHubArchiveFetcher`: download tarball via `HttpClient`, extract with `System.Formats.Tar`
- [x] 3.3 Strip the archive prefix directory during extraction (e.g. `akka.net-1.5.30/` → root)
- [x] 3.4 Support `GITHUB_TOKEN` env var for authenticated requests (optional `Authorization` header)
- [x] 3.5 Implement latest version resolution via GitHub Releases API (`/repos/{owner}/{repo}/releases/latest`)
- [x] 3.6 Unit tests for tag fallback logic; integration test for archive extraction with a small test tarball

## 4. Local Cache

- [x] 4.1 Create `ISourceCache` interface: `GetPath(repo, version)`, `Exists(repo, version)`, `GetCacheRoot()`
- [x] 4.2 Implement `FileSystemSourceCache` with `~/.akka-mcp/sources/{repo}/{version}/` layout
- [x] 4.3 Ensure immutability: `Exists` check prevents re-download
- [x] 4.4 Unit tests for path construction and existence checks

## 5. Source Provider (Facade)

- [x] 5.1 Create `ISourceProvider` with `GetSourceRootAsync(packageName, versionOverride?)` and `GetAllCachedSourceRoots()`
- [x] 5.2 Implement orchestration: registry lookup → version resolution → cache check → fetch if missing → return path
- [x] 5.3 Map package to subdirectory within cached repo (e.g. `Akka.Remote` → `{cache}/src/core/Akka.Remote`)
- [x] 5.4 `EnsureAllConfiguredAsync()` to pre-fetch all configured packages (called by tools on first use)
- [x] 5.5 Register in DI as singleton
- [x] 5.6 Unit tests with mocked fetcher/cache for orchestration logic

## 6. MCP Tool: search_source

- [x] 6.1 Create `SearchSourceTool` MCP tool class with parameters: `query`, `filePattern?`, `maxResults?`, `version?`
- [x] 6.2 Implement file enumeration + regex matching across all cached source roots
- [x] 6.3 Format results: file path (relative), line number, matching line, 2 lines context before/after
- [x] 6.4 Truncation: cap at `maxResults` (default 20), indicate when truncated
- [x] 6.5 Trigger `EnsureAllConfiguredAsync()` on first call
- [x] 6.6 Register tool in MCP server (`AkkaMcp/Program.cs`)
- [x] 6.7 Unit tests for regex matching, file filtering, result limiting, context lines

## 7. MCP Tool: lookup_type

- [x] 7.1 Create `LookupTypeTool` MCP tool class with parameters: `type`, `fullSource?`, `version?`
- [x] 7.2 Implement type finder: scan `.cs` files for `(class|interface|record|struct|enum)\s+{TypeName}[\s<({:]` with word boundary
- [x] 7.3 Default output: declaration line, namespace, base types, member signatures (strip method bodies)
- [x] 7.4 `fullSource: true` mode: return complete file contents
- [x] 7.5 Handle multiple matches: partial classes, same name in different namespaces
- [x] 7.6 Trigger `EnsureAllConfiguredAsync()` on first call
- [x] 7.7 Register tool in MCP server
- [x] 7.8 Unit tests for type matching, word boundary, summary extraction, multi-match

## 8. Wiring & Smoke Test

- [x] 8.1 Wire all DI registrations in `Program.cs`: VersionConfig, SourceCache, SourceFetcher, SourceProvider
- [x] 8.2 Add `HttpClient` registration for GitHub API calls
- [x] 8.3 Architecture test: AkkaMcp.Core SHALL NOT reference MCP SDK packages
- [x] 8.4 Manual smoke test: run server, call `search_source` and `lookup_type` via MCP inspector
