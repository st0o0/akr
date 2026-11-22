## 1. Directory Structure

- [x] 1.1 Create `e2e/` directory
- [x] 1.2 Create `.claude/skills/e2e-test/` directory

## 2. Test Plan

- [x] 2.1 Write `e2e/E2E-TEST-PLAN.md` with S0 preflight section (tool availability, smoke test)
- [x] 2.2 Write S1 Type Intelligence group — 11 checks with strict validation criteria for: lookup_type, type_members, type_hierarchy, find_implementations, find_messages, message_protocol, virtual_methods, supervision_defaults, search_source (incl. error case)
- [x] 2.3 Write S2 Config & Hosting group — 11 checks with strict validation criteria for: config_reference, hosting_extensions, hosting_to_hocon, config_tree, hosting_startup_order, config_diff, changelog, hosting_di_patterns (incl. error case)
- [x] 2.4 Write S3 Streams, Persistence & Ecosystem group — 16 checks with strict validation criteria for: stream_stages, stage_detail, graph_dsl_operators, stream_attributes, journal_plugins, snapshot_plugins, persistence_query, event_adapter_chain, serializer_bindings, serializer_detail, serialization_manifest, hosting_testkit, testkit_helpers, cluster_events, extension_points, dependency_graph
- [x] 2.5 Write S4 Multi-tool scenarios section — 4 scenarios (HOCON migration, persistence setup, actor hierarchy, cross-reference consistency) with cross-validation criteria
- [x] 2.6 Write S5 Results format section — report template, summary format, table structure

## 3. Skill Definition

- [x] 3.1 Write `.claude/skills/e2e-test/skill.md` frontmatter (name, description, trigger)
- [x] 3.2 Write S0 preflight flow — ToolSearch for akr tools, smoke test, abort on failure
- [x] 3.3 Write Haiku #1 prompt template — Type Intelligence group with tool calls and validation
- [x] 3.4 Write Haiku #2 prompt template — Config & Hosting group with tool calls and validation
- [x] 3.5 Write Haiku #3 prompt template — Streams & Ecosystem group with tool calls and validation
- [x] 3.6 Write parallel launch block — 3 agents in one message, result collection
- [x] 3.7 Write result validation flow — parse subagent tables, re-check FAILs with Opus
- [x] 3.8 Write S4 multi-tool scenario execution (Opus sequential)
- [x] 3.9 Write report generation — assemble results, write to e2e/E2E-TEST-RESULTS-<date>.md
- [x] 3.10 Write important rules section (no SKIP, no editing test plan, re-check FAILs)
