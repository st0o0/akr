# akka-mcp — Roadmap

## Phase 0: Infrastructure (foundation)

- [ ] Project version detection — scan `Directory.Packages.props` and `.csproj` for `Akka.*` packages
- [ ] GitHub source fetcher — clone/fetch tagged version from akkadotnet repos
- [ ] Version fallback — use `dev`/`main` branch when no matching tag exists, latest release when no project version found
- [ ] Local cache — `~/.akka-mcp/sources/{repo}/{version}/` with cache invalidation
- [ ] Multi-repo registry — map NuGet package names to GitHub repos and source paths
- [ ] Roslyn workspace — load cached source into a Roslyn `AdhocWorkspace` for semantic analysis

## Phase 1: Core Tools (biggest token savings)

- [ ] `type_members` — signatures only, no bodies (highest token savings per call)
- [ ] `hosting_extensions` — all `With*`/`AddAkka` extension methods with parameters
- [ ] `config_reference` — Hosting-first, HOCON fallback with defaults
- [ ] `stream_stages` — built-in stage catalog with signatures and shape info
- [ ] `search_source` — freetext/regex across all cached Akka source
- [ ] `lookup_type` — full source with inheritance chain

## Phase 2: Deep Intelligence

- [ ] `type_hierarchy` — full inheritance trees
- [ ] `find_implementations` — all implementations of an interface
- [ ] `find_messages` — message types handled by an actor
- [ ] `message_protocol` — request/response pairs and events
- [ ] `virtual_methods` — overridable methods with lifecycle ordering
- [ ] `supervision_defaults` — default strategies per actor type

## Phase 3: Streams & Persistence

- [ ] `stage_detail` — full source of a specific stream stage
- [ ] `graph_dsl_operators` — GraphDSL operators with shapes
- [ ] `stream_attributes` — configurable attributes with defaults
- [ ] `journal_plugins` — journal plugin catalog with config
- [ ] `snapshot_plugins` — snapshot store catalog with config
- [ ] `persistence_query` — ReadJournal implementations
- [ ] `event_adapter_chain` — adapter configuration

## Phase 4: Config & Migration

- [ ] `config_tree` — hierarchical config with Hosting/HOCON layers
- [ ] `hosting_to_hocon` — bidirectional mapping for migration
- [ ] `hosting_startup_order` — registration ordering constraints
- [ ] `config_diff` — config changes between versions
- [ ] `changelog` — breaking changes between versions

## Phase 5: Ecosystem

- [ ] `serializer_bindings` — serializer-to-type mapping
- [ ] `serializer_detail` — serializer source and config
- [ ] `serialization_manifest` — persistence event manifests
- [ ] `hosting_di_patterns` — DI integration patterns
- [ ] `hosting_testkit` — TestKit comparison and setup
- [ ] `testkit_helpers` — TestKit utilities per feature
- [ ] `cluster_events` — cluster event lifecycle
- [ ] `extension_points` — provider/plugin interfaces
- [ ] `dependency_graph` — package dependency map

## Future Ideas

- [ ] Incremental source updates (git pull instead of full clone)
- [ ] Roslyn-powered semantic search (find usages, call graphs)
- [ ] HOCON schema validation tool
- [ ] Actor hierarchy visualization (Mermaid output)
- [ ] Integration with akka-skills for combined intelligence
