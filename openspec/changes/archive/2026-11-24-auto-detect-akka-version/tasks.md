## 1. ProjectScanner

- [x] 1.1 Create `ProjectScanner` class in `Akr/Analysis/` with `IFileSystem` dependency
- [x] 1.2 Implement `Directory.Packages.props` parsing — extract `<PackageVersion Include="Akka*" Version="..."/>` entries
- [x] 1.3 Implement `*.csproj` parsing — extract `<PackageReference Include="Akka*" Version="..."/>` entries (skip versionless)
- [x] 1.4 Implement `Directory.Build.props` parsing — same logic as csproj
- [x] 1.5 Implement walk-up directory search with priority order (Packages.props → Build.props → csproj)
- [x] 1.6 Implement result caching per scanned path
- [x] 1.7 Add `ScanResult` record: `ProjectPath`, `Packages` (dict of name→version), `Source` (project/env/latest)

## 2. Version Resolution Integration

- [x] 2.1 Add `AKR_PROJECT_PATH` env variable reading in `GitHubClient` or a new resolution service
- [x] 2.2 Modify `ResolveLatestAsync` to check project-detected versions before GitHub API
- [x] 2.3 Implement layered resolution: AKR_PROJECT_PATH scan → CWD scan → AKKA_VERSIONS → latest
- [x] 2.4 Map detected package versions to repo versions using `RepoRegistry`

## 3. MCP Tool and Resource

- [x] 3.1 Create `set_project_context` MCP tool in a new `ProjectContextTools` class
- [x] 3.2 Implement `akr://context` MCP resource returning JSON with current resolution state
- [x] 3.3 Register tool and resource in `McpSetup`
- [x] 3.4 Add `ServerInstructions` to `McpSetup` describing auto-detection and `set_project_context`

## 4. Tests

- [x] 4.1 Unit tests for `ProjectScanner` — CPM project (Directory.Packages.props)
- [x] 4.2 Unit tests for `ProjectScanner` — inline PackageReference in csproj
- [x] 4.3 Unit tests for `ProjectScanner` — Directory.Build.props
- [x] 4.4 Unit tests for `ProjectScanner` — walk-up directory search
- [x] 4.5 Unit tests for `ProjectScanner` — no project files found
- [x] 4.6 Unit tests for `ProjectScanner` — result caching
- [x] 4.7 Integration tests for layered version resolution (project → env → latest)
- [x] 4.8 Unit tests for `set_project_context` tool
- [x] 4.9 Unit tests for `akr://context` resource
