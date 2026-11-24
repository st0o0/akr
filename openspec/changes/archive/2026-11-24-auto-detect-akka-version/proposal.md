## Why

akr currently requires either manual `AKKA_VERSIONS` configuration or falls back to the latest GitHub release. Most users already reference specific Akka.NET versions in their project files (`Directory.Packages.props` or `*.csproj`). akr should detect this automatically so the source intelligence matches the exact version the project uses — zero config, zero friction.

## What Changes

- Auto-detect Akka.NET package versions by scanning .NET project files from the working directory upward
- Introduce layered version resolution: project detection → `AKR_PROJECT_PATH` env → `AKKA_VERSIONS` env → latest release fallback
- Add `set_project_context` MCP tool for dynamic project path switching
- Add `akr://context` MCP resource exposing current resolution state
- Add `ServerInstructions` guiding AI agents to provide project context

## Capabilities

### New Capabilities

- `project-version-detection`: Scan .NET project files (`Directory.Packages.props`, `Directory.Build.props`, `*.csproj`) to auto-detect Akka.NET package versions. Walk-up-directory logic from working directory or explicit path.
- `project-context-tool`: MCP tool `set_project_context` for dynamically setting the project path at runtime, and MCP resource `akr://context` for exposing current resolution state.

### Modified Capabilities

- `version-config`: Resolution order changes — project-detected versions become the first layer, before `AKKA_VERSIONS` env variable and latest-release fallback.
- `source-fetcher`: `ResolveLatestAsync` gains a new resolution path that checks project-detected versions before querying GitHub.

## Impact

- `GitHubClient` — version resolution logic changes to consult project detection first
- `McpSetup` — registers new tool, resource, and `ServerInstructions`
- New `ProjectScanner` class in `Akr/Analysis/`
- New `AKR_PROJECT_PATH` environment variable
- No breaking changes — without a .NET project in scope, behavior is identical to today
