## Context

Akr has a working MCP server split across Akr (host) and Akr.Core (domain). The Core
project has 4 interfaces each with one implementation, plus a facade. This refactor
collapses everything into the host project with one `GitHubClient` class.

## Goals / Non-Goals

**Goals:**
- One project (Akr), no Akr.Core
- One class (GitHubClient) replaces 4 interfaces + 4 implementations + 1 facade
- Tools call GitHubClient directly
- Always latest, no version config

**Non-Goals:**
- Changing tool behavior or MCP interface
- Adding new tools

## Decisions

### 1. Single GitHubClient class

```csharp
public sealed class GitHubClient(HttpClient httpClient, ILogger<GitHubClient> logger)
{
    // Resolve latest tag, cached per session
    public async Task<string> ResolveLatestAsync(string repo, CancellationToken ct)

    // Ensure source is cached, return root path
    public async Task<string> EnsureSourceAsync(string repo, CancellationToken ct)

    // Ensure all 5 repos cached, return all root paths
    public async Task<IReadOnlyList<string>> EnsureAllSourcesAsync(CancellationToken ct)
}
```

Cache dir: `~/.akr/sources/{repoName}/{version}/`. Check `Directory.Exists`  -  that's the
whole cache layer, no interface needed.

### 2. Delete Akr.Core, merge into Akr

Move files:
- `Registry/` → `Akr/Registry/`
- `Tools/SourceSearcher.cs`, `TypeFinder.cs`, records → `Akr/Tools/`
- `Fetching/SourceFetchException.cs` → `Akr/`
- Delete everything else from Core

Update solution: remove Akr.Core, Akr.Tests.Shared. Tests reference Akr directly.

### 3. No version config

Drop `AKKA_VERSIONS` env var support entirely. The server always resolves latest.
If a user needs a specific version in the future, we add it back as a tool parameter.
