## Context

akka-mcp is a greenfield MCP server (C# / .NET 10, stdio transport) that currently has no tools.
The server host (`AkkaMcp`) wires up the MCP SDK; domain logic lives in `AkkaMcp.Core`. This
design covers the foundational infrastructure: how the server knows which Akka versions to serve,
where to find source code, how to cache it, and how the first two MCP tools (`search_source`,
`lookup_type`) consume it.

## Goals / Non-Goals

**Goals:**

- Config-driven version management — versions come from server environment, not project scanning
- Fetch and cache Akka.NET source from GitHub for 5 repos (core, Hosting, Management,
  Persistence.Sql, Persistence.MongoDB)
- Provide a `SourceProvider` facade that all MCP tools use to access source
- Ship two usable MCP tools: `search_source` (grep) and `lookup_type` (find type, return source)
- Lazy fetching — download source on first access, not at startup

**Non-Goals:**

- Roslyn / semantic analysis (Phase 1.5+)
- Project scanning / auto-detecting versions from user's codebase
- Incremental git-based updates (Future Ideas)
- Tools beyond `search_source` and `lookup_type` (Phase 1 follow-up)
- HOCON config parsing (Phase 1: `config_reference`)

## Decisions

### 1. Version configuration via environment variables

Versions are passed as a single env var `AKKA_VERSIONS` with `Package=Version` pairs:

```
AKKA_VERSIONS=Akka=1.5.30,Akka.Hosting=1.5.71,Akka.Management=1.5.73
```

Unconfigured packages default to latest release (fetched once via GitHub API). No version =
no fetching for that repo until a tool requests it.

**Why env var over config file:** MCP servers are configured in `mcp.json` which supports `env`
but not arbitrary config files. Env vars are the standard MCP configuration mechanism.

**Alternative considered:** Per-tool version parameter. Rejected because it pushes cognitive load
onto the agent for every call. Config-once is simpler. Tools may still accept an optional
`version` override for comparison scenarios.

### 2. GitHub archive download, not git clone

Source is fetched via GitHub's tarball archive endpoint:
`https://github.com/{owner}/{repo}/archive/refs/tags/{tag}.tar.gz`

**Why not git clone:** No git CLI dependency, smaller downloads (no `.git`), simpler cache
(immutable: a released version never changes). Trade-off: no incremental updates, but versions
are immutable anyway.

**Tag resolution:** Try `{version}` first (current convention), fall back to `v{version}`
(legacy pre-1.5). Fail if neither exists.

### 3. Static registry as code, not config

The package-to-repo mapping is a static dictionary in `AkkaMcp.Core`. It changes only when
Akka.NET adds new repos (rare, ~yearly). No need for external config or runtime updates.

```
# akkadotnet/akka.net (mono-repo)
"Akka"                     → Path: "src/core/Akka"
"Akka.Remote"              → Path: "src/core/Akka.Remote"
"Akka.Cluster"             → Path: "src/core/Akka.Cluster"
"Akka.Streams"             → Path: "src/core/Akka.Streams"
"Akka.Persistence"         → Path: "src/core/Akka.Persistence"
"Akka.Persistence.Query"   → Path: "src/core/Akka.Persistence.Query"
"Akka.Coordination"        → Path: "src/core/Akka.Coordination"
"Akka.Discovery"           → Path: "src/core/Akka.Discovery"
"Akka.TestKit"             → Path: "src/core/Akka.TestKit"

# akkadotnet/Akka.Hosting (archived, read-only)
"Akka.Hosting"             → Path: "src/Akka.Hosting"
"Akka.Hosting.TestKit"     → Path: "src/Akka.Hosting.TestKit"
"Akka.Cluster.Hosting"     → Path: "src/Akka.Cluster.Hosting"
"Akka.Remote.Hosting"      → Path: "src/Akka.Remote.Hosting"
"Akka.Persistence.Hosting" → Path: "src/Akka.Persistence.Hosting"

# akkadotnet/Akka.Management (nested layout)
"Akka.Management"              → Path: "src/management/Akka.Management"
"Akka.Discovery.KubernetesApi" → Path: "src/discovery/kubernetes/Akka.Discovery.KubernetesApi"
"Akka.Discovery.AwsApi"        → Path: "src/discovery/aws/Akka.Discovery.AwsApi"

# akkadotnet/Akka.Persistence.Sql
"Akka.Persistence.Sql"         → Path: "src/Akka.Persistence.Sql"
"Akka.Persistence.Sql.Hosting" → Path: "src/Akka.Persistence.Sql.Hosting"

# akkadotnet/Akka.Persistence.MongoDB
"Akka.Persistence.MongoDb"         → Path: "src/Akka.Persistence.MongoDb"
"Akka.Persistence.MongoDb.Hosting" → Path: "src/Akka.Persistence.MongoDb.Hosting"
```

24 packages across 5 repos. Note: Akka.Hosting repo is archived (Nov 2026, read-only) —
source is still accessible but won't change. The Management repo has a non-standard nested
layout under `src/management/` and `src/discovery/<provider>/`.

**Why not JSON config:** One more file to manage, no real benefit. The mapping is stable and
small (~24 entries). Code is easier to test.

### 4. Cache layout: `~/.akka-mcp/sources/{repo}/{version}/`

```
~/.akka-mcp/
  sources/
    akka.net/
      1.5.30/
        src/core/Akka/
        src/core/Akka.Remote/
        ...
    Akka.Hosting/
      1.5.71/
        src/Akka.Hosting/
        ...
```

Immutable cache — once extracted, never updated. Delete to invalidate. No TTL needed because
released versions don't change.

**Why home directory:** The cache is user-scoped, not project-scoped. Multiple projects share
the same cached source for the same version.

### 5. Lazy fetching with Source Provider facade

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│ MCP Tool    │────▶│ Source       │────▶│ Cache       │
│ search/     │     │ Provider     │     │ (filesystem)│
│ lookup      │     │              │     └──────┬──────┘
└─────────────┘     │  if not      │            │ miss
                    │  cached:     │     ┌──────▼──────┐
                    │  fetch+cache │────▶│ Fetcher     │
                    └──────────────┘     │ (HTTP)      │
                                         └─────────────┘
```

Tools call `SourceProvider.GetSourceRoot(package)` which returns a path to the cached source
directory. If not cached, the provider fetches the archive, extracts it, and returns the path.
First call per version pays the fetch cost; subsequent calls are instant.

### 6. Tool design: search_source

```
Name: search_source
Parameters:
  - query: string (required) — regex pattern
  - filePattern: string (optional) — glob filter, e.g. "*.cs" (default: "*.cs")
  - maxResults: int (optional) — limit results (default: 20)
Returns:
  - List of { file, line, lineNumber, context }
```

Searches across all cached source repos. Uses in-process file enumeration + regex matching
(no external grep dependency). Token-efficient: returns matching lines with surrounding
context, not full files.

### 7. Tool design: lookup_type

```
Name: lookup_type
Parameters:
  - type: string (required) — type name, e.g. "ActorBase"
  - fullSource: bool (optional) — return full file (default: false, returns signature area)
Returns:
  - { file, namespace, declaration, source }
```

Finds a type by scanning `.cs` files for `class/interface/record/struct/enum {TypeName}`.
Regex-based, not Roslyn — good enough for type lookup by name. Searches all cached repos.
When multiple matches exist (e.g. partial classes), returns all.

## Risks / Trade-offs

- **[GitHub rate limit]** Archive downloads are unauthenticated → 60 req/hour.
  → Mitigation: caching makes this a one-time cost per version. Typical setup: 3-5 repos × 1
  version = 3-5 requests. Optional: support `GITHUB_TOKEN` env var for authenticated requests.
- **[Large downloads]** The akka.net mono-repo archive is ~30-50 MB.
  → Mitigation: extract only the `src/` subtree, discard the rest. First-use delay is acceptable
  for a tool that saves thousands of tokens per subsequent call.
- **[Regex type lookup is imprecise]** `class ActorBase` could match comments or strings.
  → Mitigation: anchor regex to declaration keywords. Good enough for Phase 0; Roslyn replaces
  this in Phase 1.5.
- **[No Windows path issues]** Cache in home dir works cross-platform.
  → `Environment.GetFolderPath(SpecialFolder.UserProfile)` + `.akka-mcp/sources/`.

## Open Questions

- Should `search_source` support searching a specific repo/package only, or always search all?
  Leaning toward: always all, with optional `package` filter parameter.
- Exact extraction strategy for the mono-repo tarball: extract full archive then use, or
  stream-extract only `src/` entries? Leaning toward: extract full, simpler implementation.
