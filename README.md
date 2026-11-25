# akr

[![Release](https://img.shields.io/github/v/release/st0o0/akr?sort=semver&style=flat-square)](https://github.com/st0o0/akr/releases)
[![NuGet](https://img.shields.io/nuget/v/Akr?style=flat-square)](https://www.nuget.org/packages/Akr)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

Akka.NET source intelligence for AI agents. An [MCP](https://modelcontextprotocol.io/) server that gives agents fast, token-efficient access to Akka.NET source code and documentation directly from GitHub - no ILSpy decompilation needed.

Zero config. Resolves latest Akka.NET versions automatically. Supports version pinning and cross-version comparison. Powered by Akka.Streams for parallel source fetching. Distributed as a dotnet tool.

## Quick Start

### 1. Add to your project

```bash
dotnet tool install akr
```

This adds akr to your `.config/dotnet-tools.json`. Run `dotnet tool restore` on any machine to install it.

### 2. Add MCP config

Create `.mcp.json` in your repo root:

```json
{
  "mcpServers": {
    "akr": {
      "command": "dotnet",
      "args": ["tool", "run", "akr"]
    }
  }
}
```

Your MCP client (Claude Code, VS Code, Cursor) discovers this automatically. Done.

### Alternative: global install

```bash
dotnet tool install --global Akr
```

Then configure your MCP client with `"command": "akr"` directly.

## Tools (37)

### Source Intelligence

| Tool | What it does |
|------|-------------|
| `search_source` | Regex search across all Akka.NET source |
| `lookup_type` | Find a type, return its source file |
| `type_members` | Compact member signatures (no bodies) |
| `type_hierarchy` | Full inheritance trees (up and down) |
| `find_implementations` | All implementations of an interface |
| `find_messages` | Message types handled by an actor |
| `message_protocol` | Request/response pairs and events |
| `virtual_methods` | Overridable methods with lifecycle ordering |
| `supervision_defaults` | Default supervision strategies |

### Documentation

| Tool | What it does |
|------|-------------|
| `docs_search` | Search getakka.net documentation by topic |
| `docs_example` | Find code examples from docs with language filter |

### Version Comparison

| Tool | What it does |
|------|-------------|
| `source_diff` | Unified diff of a type or config between versions |
| `migration_guide` | Structured upgrade guide combining changelog, config diffs, and docs |

### Config & Hosting

| Tool | What it does |
|------|-------------|
| `config_reference` | HOCON defaults from `reference.conf` |
| `config_tree` | Hierarchical config with Hosting/HOCON layers |
| `config_diff` | Config key listing per version |
| `hosting_extensions` | List `With*`/`Add*` Akka.Hosting methods |
| `hosting_to_hocon` | Bidirectional Hosting API to HOCON mapping |
| `hosting_startup_order` | Registration ordering constraints |
| `hosting_di_patterns` | DI integration patterns |
| `changelog` | Changelog/release notes search |

### Streams

| Tool | What it does |
|------|-------------|
| `stream_stages` | Catalog of Akka.Streams built-in stages |
| `stage_detail` | Full source of a specific stream stage |
| `graph_dsl_operators` | GraphDSL operators with shapes |
| `stream_attributes` | Configurable stream attributes with defaults |

### Persistence

| Tool | What it does |
|------|-------------|
| `journal_plugins` | Journal plugin catalog with config |
| `snapshot_plugins` | Snapshot store catalog with config |
| `persistence_query` | ReadJournal implementations |
| `event_adapter_chain` | Event adapter configuration |

### Serialization & Ecosystem

| Tool | What it does |
|------|-------------|
| `serializer_bindings` | Serializer-to-type mapping |
| `serializer_detail` | Serializer source and config |
| `serialization_manifest` | Persistence event manifests |
| `hosting_testkit` | TestKit comparison (classic vs Hosting) |
| `testkit_helpers` | TestKit utility catalog |
| `cluster_events` | Cluster event lifecycle |
| `extension_points` | Provider/plugin interfaces |
| `dependency_graph` | Package dependency map |

All source tools accept an optional `version` parameter to target a specific Akka.NET version (e.g. `lookup_type(type: "Props", version: "1.5.30")`).

## Usage Examples

```
lookup_type(type: "Props", version: "1.5.30")
  → Props source from Akka.NET 1.5.30 (version pinning)

docs_search(query: "cluster sharding")
  → 5 relevant pages from getakka.net documentation

source_diff(typeName: "Props", versionA: "1.5.30", versionB: "1.5.35")
  → Unified diff showing what changed between versions

migration_guide(fromVersion: "1.5.0", toVersion: "1.5.74")
  → Structured upgrade guide: changelog + config changes + relevant docs

docs_example(topic: "persistence", language: "csharp")
  → Code examples extracted from documentation

hosting_extensions(filter: "Persistence")
  → WithSqlPersistence, WithJournalAndSnapshot, ... signatures
```

## Covered Repositories

| Repository | Packages |
|------------|----------|
| [akkadotnet/akka.net](https://github.com/akkadotnet/akka.net) | Akka, Remote, Cluster, Streams, Persistence, TestKit |
| [akkadotnet/Akka.Hosting](https://github.com/akkadotnet/Akka.Hosting) | Hosting, Hosting.TestKit, Cluster.Hosting |
| [akkadotnet/Akka.Management](https://github.com/akkadotnet/Akka.Management) | Management, Discovery.KubernetesApi |
| [akkadotnet/Akka.Persistence.Sql](https://github.com/akkadotnet/Akka.Persistence.Sql) | Persistence.Sql, Persistence.Sql.Hosting |
| [akkadotnet/Akka.Persistence.MongoDB](https://github.com/akkadotnet/Akka.Persistence.MongoDB) | Persistence.MongoDb, Persistence.MongoDb.Hosting |

## How It Works

1. On first tool call, akr fetches the latest release archive from each repo (~44 MB total)
2. Archives are extracted atomically to `~/.akr/sources/{repo}/{version}/`
3. All subsequent calls use the local cache - no network requests
4. Tools search cached source using regex and text parsing

No GitHub token required. Archives for public repos are freely downloadable.

## Keeping akr updated

akr is a local dotnet tool pinned in `.config/dotnet-tools.json`. When a new version is released:

```bash
dotnet tool update akr
```

If you use [Renovate](https://github.com/renovatebot/renovate), it bumps the version automatically.

## Development

```bash
git clone https://github.com/st0o0/akr.git
cd akr
dotnet tool restore
dotnet build src/akr.slnx
dotnet run --project src/Akr.Tests/Akr.Tests.csproj
dotnet run --project src/Akr/Akr.csproj
```

## License

[MIT](LICENSE)
