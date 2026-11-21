# akr

[![Release](https://img.shields.io/github/v/release/st0o0/akr?sort=semver&style=flat-square)](https://github.com/st0o0/akr/releases)
[![NuGet](https://img.shields.io/nuget/v/Akr?style=flat-square)](https://www.nuget.org/packages/Akr)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

Akka.NET source intelligence for AI agents. An [MCP](https://modelcontextprotocol.io/) server that gives agents fast, token-efficient access to Akka.NET source code directly from GitHub - no ILSpy decompilation needed.

Zero config. Resolves latest Akka.NET versions automatically. Publishes as a small native AOT binary.

## Tools

| Tool | What it does | Token savings vs ILSpy |
|------|-------------|------------------------|
| `search_source` | Regex search across all Akka.NET source | ~85% |
| `lookup_type` | Find a type, return its source file | ~80% |
| `type_members` | Compact member signatures (no bodies) | ~90% |
| `hosting_extensions` | List `With*`/`Add*` Akka.Hosting methods | ~90% |
| `config_reference` | HOCON defaults from `reference.conf` | ~90% |
| `stream_stages` | Catalog of Akka.Streams built-in stages | ~93% |

## Quick Start

### Install

```bash
dotnet tool install --global Akr
```

### Configure your MCP client

```json
{
  "mcpServers": {
    "akr": {
      "command": "akr"
    }
  }
}
```

That's it. No API keys, no config files, no version pinning needed.

## Usage Examples

```
Agent: search_source(query: "Receive<T>")
  → Finds all Receive<T> calls across Akka source, with 2 lines context

Agent: type_members(type: "Props")
  → Props : object
    Properties: Type Type { get; }
    Methods: static Props Create<T>(), static Props Create(Type type)
    Constructors: Props(Type type)

Agent: hosting_extensions(filter: "Persistence")
  → Lists all WithSqlPersistence, WithJournalAndSnapshot, ... signatures

Agent: config_reference(path: "akka.persistence")
  → Returns the full HOCON config block with all defaults

Agent: stream_stages(filter: "fan")
  → Fan-In: Merge, MergePreferred, ...
    Fan-Out: Broadcast, Balance, ...
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

1. On first tool call, akr fetches the latest release archive from each repo
2. Archives are extracted to `~/.akr/sources/{repo}/{version}/`
3. All subsequent calls use the local cache - no network requests
4. Tools search cached source using regex and text parsing (no Roslyn, no reflection)

No GitHub token required. Archives for public repos are freely downloadable.

## Configuration

akr works out of the box with no configuration. Optional settings:

| Variable | Description |
|----------|-------------|
| `AKKA_VERSIONS` | Pin specific versions: `Akka=1.5.30,Akka.Hosting=1.5.71` |

## Development

```bash
git clone https://github.com/st0o0/akr.git
cd akr
dotnet build src/akr.slnx
dotnet run --project src/Akr.Tests/Akr.Tests.csproj    # run tests
dotnet run --project src/Akr/Akr.csproj                 # run server
```

## License

[MIT](LICENSE)
