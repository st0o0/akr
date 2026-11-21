## Why

AI agents working with Akka.NET codebases constantly decompile assemblies via ILSpy to understand
types, config, and internals. This is token-expensive (~2000-4000 tokens per lookup), slow, lossy
(no doc-comments, no cross-references), and repetitive across conversations. An MCP server that
provides structured, token-efficient access to Akka.NET source code  -  matched to configured
versions  -  eliminates this overhead entirely.

## What Changes

- New MCP server infrastructure: config-driven version management, source fetching, and local caching
- Multi-repo registry mapping NuGet packages to GitHub repos and source paths
- GitHub archive fetcher with local cache at `~/.akka-mcp/sources/{repo}/{version}/`
- Tag resolution with fallback (`{version}` → `v{version}` for legacy)
- First MCP tools: `search_source` (grep-based) and `lookup_type` (file-based)
- Server config via environment variables: `AKKA_VERSIONS=Akka=1.5.30,Akka.Hosting=1.5.71,...`
- No project scanning  -  versions are configured statically, the agent never needs to pass them

## Capabilities

### New Capabilities

- `version-config`: Parse and validate Akka package versions from server configuration (env vars).
  Resolve package names to repos. Provide defaults (latest) when unconfigured.
- `repo-registry`: Static mapping of NuGet package names to GitHub repos, source paths within
  repos, and tag patterns. Covers core mono-repo + Hosting, Management, Persistence.Sql,
  Persistence.MongoDB.
- `source-fetcher`: Download GitHub release archives, extract to local cache, handle tag resolution
  (`{version}` first, `v{version}` fallback). Cache invalidation by version (immutable releases).
- `source-provider`: Facade that orchestrates registry + fetcher + cache. Single entry point:
  "give me the source tree for package X at configured version."
- `search-source-tool`: MCP tool  -  grep/regex search across all cached Akka source. Returns
  matching lines with file paths and line numbers. Token-efficient output.
- `lookup-type-tool`: MCP tool  -  find a type by name across cached source, return its full
  source file. Searches all loaded repos.

### Modified Capabilities

(none  -  greenfield)

## Impact

- **New code in AkkaMcp.Core**: registry, fetcher, cache manager, source provider
- **New code in AkkaMcp**: MCP tool registrations, config parsing from env vars
- **External dependency**: HTTP client for GitHub archive downloads (no git CLI needed)
- **Filesystem**: creates `~/.akka-mcp/sources/` cache directory
- **Network**: fetches from `github.com` on first use per version (then cached)
- **No breaking changes**: additive, the server currently has no tools
