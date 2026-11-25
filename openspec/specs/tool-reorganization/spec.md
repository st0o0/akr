# tool-reorganization Specification

## Purpose
TBD - created by archiving change separation-of-concerns-refactor. Update Purpose after archive.
## Requirements
### Requirement: Tools grouped by Akka domain
Tool files SHALL be organized by Akka.NET domain. Each file SHALL contain only tools that belong to that domain. The following grouping SHALL be used:

- `TypeTools.cs`: lookup_type, type_members, type_hierarchy, find_implementations, virtual_methods, find_messages, message_protocol, supervision_defaults (8 tools)
- `SearchTools.cs`: search_source (1 tool)
- `ConfigTools.cs`: config_reference, config_tree, config_diff, changelog (4 tools)
- `HostingTools.cs`: hosting_extensions, hosting_to_hocon, hosting_startup_order, hosting_di_patterns, hosting_testkit (5 tools)
- `StreamTools.cs`: stream_stages, stage_detail, graph_dsl_operators, stream_attributes (4 tools)
- `PersistenceTools.cs`: journal_plugins, snapshot_plugins, persistence_query, event_adapter_chain (4 tools)
- `SerializationTools.cs`: serializer_bindings, serializer_detail, serialization_manifest (3 tools)
- `ClusterTools.cs`: cluster_events, extension_points, dependency_graph (3 tools)
- `TestKitTools.cs`: testkit_helpers (1 tool)
- `DocsTools.cs`: docs_search, docs_example (2 tools)
- `VersionTools.cs`: source_diff, migration_guide (2 tools)
- `ProjectContextTools.cs`: set_project_context (1 tool)

#### Scenario: EcosystemTools no longer exists
- **WHEN** the codebase is searched for `EcosystemTools`
- **THEN** zero references SHALL be found

#### Scenario: StreamPersistenceTools no longer exists
- **WHEN** the codebase is searched for `StreamPersistenceTools`
- **THEN** zero references SHALL be found

#### Scenario: DeepIntelligenceTools no longer exists
- **WHEN** the codebase is searched for `DeepIntelligenceTools`
- **THEN** zero references SHALL be found

#### Scenario: ConfigMigrationTools no longer exists
- **WHEN** the codebase is searched for `ConfigMigrationTools`
- **THEN** zero references SHALL be found

### Requirement: All 37 MCP tool names unchanged
Every MCP tool `Name` attribute SHALL remain identical. No tool is added, removed, or renamed. Only the class and file they live in changes.

#### Scenario: Tool count preserved
- **WHEN** all `[McpServerTool]` attributes are counted
- **THEN** exactly 37 tools SHALL exist (excluding set_project_context resource)

