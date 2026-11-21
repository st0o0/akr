# akka-mcp — Idea

## Problem

AI agents working with Akka.NET codebases constantly decompile assemblies (via ILSpy) to understand types, config, and internals. This is:

- **Token-expensive**: Full decompiled source includes compiler-generated noise, attributes, and boilerplate
- **Slow**: Each lookup requires finding the assembly, decompiling, and filtering
- **Lossy**: Decompiled code lacks doc-comments, intent, and cross-references
- **Repetitive**: The same types get decompiled across conversations

## Solution

An MCP server that provides **structured, token-efficient access to Akka.NET source code** directly from the GitHub repositories, matched to the exact version the project uses.

## Core Principles

1. **Hosting-first**: Always suggest `Akka.Hosting` APIs before HOCON config. HOCON is the fallback, not the default.
2. **Version-aware**: Reads the project's NuGet references to fetch the matching source. Falls back to latest.
3. **Token-efficient**: Returns signatures and summaries by default, full source on demand.
4. **Multi-repo**: Covers the full Akka.NET ecosystem, not just core.

## Covered Repositories

| Repository | Packages |
|------------|----------|
| `akkadotnet/akka.net` | Core, Remote, Cluster, Streams, Persistence, DI, TestKit |
| `akkadotnet/Akka.Hosting` | Akka.Hosting, Akka.Hosting.TestKit |
| `akkadotnet/Akka.Management` | Management, Cluster.Bootstrap, Discovery |
| `akkadotnet/Akka.Persistence.Sql` | Persistence.Sql |
| `akkadotnet/Akka.Persistence.MongoDB` | Persistence.MongoDb |

## Tool Categories (25 tools across 9 categories)

### 1. Type Intelligence
- `lookup_type` — Full source of a type + inheritance chain + interfaces
- `type_hierarchy` — Complete inheritance tree (e.g. all ActorBase variants)
- `find_implementations` — All implementations of an interface/abstract class
- `type_members` — Compact overview: properties, methods, events — signatures only, no bodies

### 2. Message & Protocol Discovery
- `find_messages` — All message types an actor handles (scans Receive<T>, Command, Match)
- `message_protocol` — Request/Response pairs and published events for a namespace/actor
- `cluster_events` — All cluster membership events with lifecycle ordering

### 3. Streams
- `stream_stages` — Catalog of all built-in stages (Source/Flow/Sink/Fan-In/Fan-Out) with signatures
- `stage_detail` — Full source + docs for a specific stage
- `graph_dsl_operators` — All GraphDSL operators with Inlet/Outlet shapes
- `stream_attributes` — All configurable stream attributes with defaults

### 4. Hosting & Config (Hosting-first!)
- `hosting_extensions` — All `AddAkka` / `With*` extension methods with signatures (primary config reference)
- `config_reference` — Hosting API first, then HOCON fallback for keys without Hosting equivalent
- `config_tree` — Hosting builder chain first, then HOCON fallback hierarchy
- `hosting_to_hocon` — Bidirectional mapping Hosting API <-> HOCON keys (migration helper)
- `hosting_startup_order` — Builder chain ordering: what must be registered before what

### 5. Persistence
- `journal_plugins` — Available journal plugins with config schema
- `snapshot_plugins` — Snapshot store plugins with config schema
- `persistence_query` — ReadJournal implementations and their query capabilities
- `event_adapter_chain` — Event adapter configuration and chaining

### 6. Extension Points
- `virtual_methods` — All overridable methods with lifecycle ordering (PreStart, PostStop, etc.)
- `extension_points` — Provider/plugin interfaces per module
- `supervision_defaults` — Default supervision strategy per actor type + how to change it

### 7. Serialization
- `serializer_bindings` — Which serializer handles which type
- `serializer_detail` — Source + config of a serializer
- `serialization_manifest` — Manifest mapping for persistence events

### 8. DI & Testing
- `hosting_di_patterns` — IRequiredActor<T>, ActorRegistry, Props with DI
- `hosting_testkit` — Akka.Hosting.TestKit vs classic TestKit — when to use which
- `testkit_helpers` — Available TestKit utilities per feature

### 9. Cross-Cutting
- `search_source` — Freetext/regex search across all Akka source
- `changelog` — Breaking changes and new features between two versions
- `dependency_graph` — Package dependency map (helps with upgrade planning)
- `config_diff` — Config differences between two versions

## Token Savings Estimate

| Scenario | Without MCP | With MCP | Savings |
|----------|-------------|----------|---------|
| Look up actor lifecycle methods | ~2000 tokens (decompile ActorBase) | ~300 tokens (virtual_methods) | ~85% |
| Find stream stage | ~3000 tokens (decompile + search) | ~200 tokens (stream_stages) | ~93% |
| Check HOCON config defaults | ~1500 tokens (find reference.conf) | ~150 tokens (config_reference) | ~90% |
| Understand type hierarchy | ~4000 tokens (multiple decompiles) | ~400 tokens (type_hierarchy) | ~90% |
