# akr

[![Release](https://img.shields.io/github/v/release/st0o0/akr?sort=semver&style=flat-square)](https://github.com/st0o0/akr/releases)
[![NuGet](https://img.shields.io/nuget/v/Akr?style=flat-square)](https://www.nuget.org/packages/Akr)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

Akka.NET source intelligence for AI agents. An [MCP](https://modelcontextprotocol.io/) server that gives agents fast, token-efficient access to Akka.NET source code directly from GitHub - no ILSpy decompilation needed.

Zero config. Resolves latest Akka.NET versions automatically. Distributed as a dotnet tool.

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

## Tools (33)

### Core (Phase 1)

| Tool | What it does |
|------|-------------|
| `search_source` | Regex search across all Akka.NET source |
| `lookup_type` | Find a type, return its source file |
| `type_members` | Compact member signatures (no bodies) |
| `hosting_extensions` | List `With*`/`Add*` Akka.Hosting methods |
| `config_reference` | HOCON defaults from `reference.conf` |
| `stream_stages` | Catalog of Akka.Streams built-in stages |

### Deep Intelligence (Phase 2)

| Tool | What it does |
|------|-------------|
| `type_hierarchy` | Full inheritance trees (up and down) |
| `find_implementations` | All implementations of an interface |
| `find_messages` | Message types handled by an actor |
| `message_protocol` | Request/response pairs and events |
| `virtual_methods` | Overridable methods with lifecycle ordering |
| `supervision_defaults` | Default supervision strategies |

### Streams & Persistence (Phase 3)

| Tool | What it does |
|------|-------------|
| `stage_detail` | Full source of a specific stream stage |
| `graph_dsl_operators` | GraphDSL operators with shapes |
| `stream_attributes` | Configurable stream attributes with defaults |
| `journal_plugins` | Journal plugin catalog with config |
| `snapshot_plugins` | Snapshot store catalog with config |
| `persistence_query` | ReadJournal implementations |
| `event_adapter_chain` | Event adapter configuration |

### Config & Migration (Phase 4)

| Tool | What it does |
|------|-------------|
| `config_tree` | Hierarchical config with Hosting/HOCON layers |
| `hosting_to_hocon` | Bidirectional Hosting API ↔ HOCON mapping |
| `hosting_startup_order` | Registration ordering constraints |
| `config_diff` | Config key listing per version |
| `changelog` | Changelog/release notes search |

### Ecosystem (Phase 5)

| Tool | What it does |
|------|-------------|
| `serializer_bindings` | Serializer-to-type mapping |
| `serializer_detail` | Serializer source and config |
| `serialization_manifest` | Persistence event manifests |
| `hosting_di_patterns` | DI integration patterns |
| `hosting_testkit` | TestKit comparison (classic vs Hosting) |
| `testkit_helpers` | TestKit utility catalog |
| `cluster_events` | Cluster event lifecycle |
| `extension_points` | Provider/plugin interfaces |
| `dependency_graph` | Package dependency map |

## Usage Examples

```
search_source(query: "Receive<T>")
  → Finds all Receive<T> calls across Akka source, 2 lines context

type_members(type: "Props")
  → Props : IEquatable<Props>, ISurrogated
    Properties: Type Type { get; }
    Methods: static Props Create<T>(), static Props Create(Type type)
    Constructors: Props(Type type)

hosting_extensions(filter: "Persistence")
  → WithSqlPersistence, WithJournalAndSnapshot, ... signatures

config_reference(path: "akka.persistence")
  → Full HOCON config block with all defaults

find_implementations(type: "IActorRef")
  → ActorRefBase, LocalActorRef, RemoteActorRef, ...

type_hierarchy(type: "ActorBase")
  → UntypedActor, ReceiveActor, ActorBase → IInternalActor
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
