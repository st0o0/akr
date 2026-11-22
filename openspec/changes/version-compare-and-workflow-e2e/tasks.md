## 1. Version pinning infrastructure

- [ ] 1.1 Add `EnsureSourceForVersionAsync(string repo, string version)` to `GitHubClient` that skips `ResolveLatestAsync` and directly fetches/caches the specified version
- [ ] 1.2 Modify `RepoMetadata` to track multiple cached versions with last-accessed timestamps (e.g., `CachedVersions` dictionary)
- [ ] 1.3 Modify `CleanupOldVersions` to retain N most recently accessed versions (default 3) instead of only latest
- [ ] 1.4 Add `EnsureAllSourcesForVersionAsync(string version)` that fetches all repos at a specific version
- [ ] 1.5 Unit tests: multi-version caching, LRU eviction, latest never evicted

## 2. Version parameter on tools

- [ ] 2.1 Add optional `version` parameter to all tool methods in TypeTools, StreamTools, StreamPersistenceTools, ConfigMigrationTools, HostingTools, EcosystemTools, DeepIntelligenceTools
- [ ] 2.2 Route version parameter through to `GitHubClient` — when present, call version-specific method; when absent, use existing latest behavior
- [ ] 2.3 Unit tests: tool with version parameter fetches correct version, tool without version uses latest

## 3. Source diff tool

- [ ] 3.1 Implement `source_diff` MCP tool with two modes: type diff and config diff
- [ ] 3.2 Type diff: fetch both versions, run `FindType` on each, produce unified diff
- [ ] 3.3 Config diff: fetch both versions, run `CollectConfig` on each, use existing `DiffConfig` logic
- [ ] 3.4 Wire up existing `config_diff` tool's versionA/versionB parameters to actually compare two versions
- [ ] 3.5 Unit tests: diff with changes, diff with no changes, type missing in one version

## 4. Workflow E2E tests

- [ ] 4.1 Design stream stage exploration workflow test: stream_stages → stage_detail → lookup_type → type_hierarchy → search_source with cross-tool validation
- [ ] 4.2 Design HOCON migration workflow test: config_reference → hosting_to_hocon → hosting_extensions → hosting_startup_order with cross-tool validation
- [ ] 4.3 Design actor debugging workflow test: lookup_type → virtual_methods → supervision_defaults → find_messages with cross-tool validation
- [ ] 4.4 Add workflow tests as new S5 section in E2E test skill, keeping existing S0-S4 as smoke tier
- [ ] 4.5 Run full E2E suite — both smoke and workflow tiers pass

## 5. Build and verify

- [ ] 5.1 Run `dotnet build src/akr.slnx` and `dotnet format src/akr.slnx --verify-no-changes`
- [ ] 5.2 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [ ] 5.3 Manual smoke test: `lookup_type("Throttle", version: "1.5.52")` returns v1.5.52 source
- [ ] 5.4 Manual smoke test: `source_diff(type: "Throttle", versionA: "1.5.52", versionB: "1.5.74")` returns diff
