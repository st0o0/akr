# akr - Roadmap

## Phase 0: Infrastructure (done)

- [x] GitHub source fetcher - download tagged archives from akkadotnet repos
- [x] Local cache - `~/.akr/sources/{repo}/{version}/` with atomic extraction
- [x] Multi-repo registry - 24 packages across 5 repos
- [x] Latest-by-default - no config needed, resolves latest on first use

## Phase 1: Core Tools (done)

- [x] `search_source` - regex search across all cached Akka source
- [x] `lookup_type` - find type, return source
- [x] `type_members` - compact member signatures without bodies
- [x] `hosting_extensions` - `With*`/`Add*` extension methods from Akka.Hosting
- [x] `config_reference` - HOCON defaults from `reference.conf`
- [x] `stream_stages` - built-in stage catalog with shape classification

## Phase 2: Deep Intelligence (done)

- [x] `type_hierarchy` - full inheritance trees
- [x] `find_implementations` - all implementations of an interface
- [x] `find_messages` - message types handled by an actor
- [x] `message_protocol` - request/response pairs and events
- [x] `virtual_methods` - overridable methods with lifecycle ordering
- [x] `supervision_defaults` - default strategies per actor type

## Phase 3: Streams & Persistence (done)

- [x] `stage_detail` - full source of a specific stream stage
- [x] `graph_dsl_operators` - GraphDSL operators with shapes
- [x] `stream_attributes` - configurable attributes with defaults
- [x] `journal_plugins` - journal plugin catalog with config
- [x] `snapshot_plugins` - snapshot store catalog with config
- [x] `persistence_query` - ReadJournal implementations
- [x] `event_adapter_chain` - adapter configuration

## Phase 4: Config & Migration (done)

- [x] `config_tree` - hierarchical config with Hosting/HOCON layers
- [x] `hosting_to_hocon` - bidirectional mapping for migration
- [x] `hosting_startup_order` - registration ordering constraints
- [x] `config_diff` - config key listing (version comparison planned)
- [x] `changelog` - changelog/release notes search

## Phase 5: Ecosystem (done)

- [x] `serializer_bindings` - serializer-to-type mapping
- [x] `serializer_detail` - serializer source and config
- [x] `serialization_manifest` - persistence event manifests
- [x] `hosting_di_patterns` - DI integration patterns
- [x] `hosting_testkit` - TestKit comparison and setup
- [x] `testkit_helpers` - TestKit utilities per feature
- [x] `cluster_events` - cluster event lifecycle
- [x] `extension_points` - provider/plugin interfaces
- [x] `dependency_graph` - package dependency map

## Future Ideas

- [ ] Roslyn-powered semantic search (find usages, call graphs)
- [ ] Real `config_diff` between two versions
- [ ] HOCON schema validation tool
- [ ] Actor hierarchy visualization (Mermaid output)
- [ ] Integration with akka-skills for combined intelligence
- [ ] NuGet package publishing
