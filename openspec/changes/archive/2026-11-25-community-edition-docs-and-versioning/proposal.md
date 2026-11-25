## Why

akr provides precise source intelligence (types, configs, streams) but all 33 tools draw from a single source: GitHub C# code. For a community tool, this leaves two gaps: (1) newcomers need conceptual explanations, code examples, and "how do I..." answers that live in the getakka.net documentation, not in source signatures; (2) developers upgrading between Akka.NET versions need to compare types and configs across versions, but akr only caches one version at a time and has no diff capability. Adding getakka.net docs as a second data source and multi-version support with diffs makes akr the only tool that combines source intelligence, documentation context, and version comparison for Akka.NET.

## What Changes

- **getakka.net docs as second data source**: Fetch Markdown sources from `akkadotnet/getakka.net` GitHub repo, cache alongside source code, and expose via new tools
- **New tool `docs_search`**: Find relevant documentation pages for a topic or question, return token-efficient excerpts with source links
- **New tool `docs_example`**: Extract code examples from documentation for a given topic (e.g., "cluster sharding", "ask pattern")
- **Multi-version cache**: Replace single-version `CleanupOldVersions` with LRU cache retaining up to 3 versions per repo
- **Optional `version` parameter on all existing tools**: When provided, target a specific Akka.NET version instead of resolved latest
- **New tool `source_diff`**: Unified diff of a type or config key between two Akka.NET versions
- **New tool `migration_guide`**: Combine source diffs, changelog entries, and relevant docs into an upgrade guide for version A→B

## Capabilities

### New Capabilities

- `docs-fetcher`: Fetch and cache getakka.net Markdown sources from GitHub, structured for topic-based lookup
- `docs-search-tool`: MCP tool to search documentation by topic/question and return relevant excerpts
- `docs-example-tool`: MCP tool to extract and return code examples from documentation for a given topic
- `multi-version-cache`: LRU-based cache supporting multiple Akka.NET versions per repo (max 3), with access-time tracking
- `version-pinning`: Optional `version` parameter on all existing tool methods, routing to version-specific source
- `source-diff-tool`: MCP tool comparing a type or config between two Akka.NET versions as unified diff
- `migration-guide-tool`: MCP tool combining source diffs, changelog, and docs into a version upgrade guide

### Modified Capabilities

- `source-cleanup`: Change from "delete all except current" to LRU eviction retaining N most recent versions
- `source-caching`: Extend metadata to track multiple cached versions with last-accessed timestamps
- `repo-registry`: Add `akkadotnet/getakka.net` as a docs-type repo entry

## Impact

- `GitHubClient.cs`: Multi-version caching, LRU cleanup, version-specific fetch path
- `RepoMetadata`: Multi-version tracking with access timestamps
- `RepoRegistry.cs`: New docs repo entry
- `AkrOptions.cs`: New config for max cached versions, docs cache settings
- `McpSetup.cs`: Register new tool classes (DocsTools, VersionTools)
- All existing tool classes: Add optional `version` parameter, route through version-aware fetch
- New files: `DocsClient.cs` (or extend GitHubClient), `DocsTools.cs`, `VersionTools.cs`
- Cache structure: `~/.akr/sources/` grows to hold multiple version dirs per repo + docs content
