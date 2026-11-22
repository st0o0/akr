## Context

akr is a dotnet tool MCP server used by AI agents to explore Akka.NET source code. It
exposes 33 tools across 7 tool classes. The existing test suite covers tool logic via unit
tests with fake filesystems, but nothing validates the MCP protocol layer, DI wiring,
or real-world tool behavior against actual Akka.NET source.

FunkArr and Njord both use a proven pattern: a Claude Code skill orchestrates E2E tests
with Opus handling setup/teardown and sequential steps, while Haiku subagents run disjoint
test groups in parallel. Results are captured in a markdown report.

akr's case is simpler than FunkArr/Njord — no Docker stack, no external services. akr is
already available as an MCP server in the Claude Code session. The skill just calls the
tools directly, the way any agent would.

## Goals / Non-Goals

**Goals:**

- Validate every one of the 33 MCP tools with strict pass/fail criteria
- Test realistic multi-tool workflows (HOCON migration, persistence setup, etc.)
- Run tool groups in parallel via Haiku subagents for speed
- Produce a timestamped results report for tracking quality over time
- Catch regressions: broken tools, missing data, format changes

**Non-Goals:**

- Automated CI integration — this is manual, on-demand
- Performance benchmarking — focus is correctness
- Testing GitHub API interactions — that's covered by unit tests
- Testing against specific Akka.NET versions — uses whatever is cached

## Decisions

### Decision 1: Skill calls MCP tools directly (no subprocess)

**Choice**: The skill uses the akr MCP tools that are already available in the Claude Code
session via `.mcp.json`.

**Alternatives considered**:
- *Start akr as subprocess*: More isolated, but adds complexity and doesn't test the real
  MCP integration path.
- *In-process StreamTransport tests*: Automated but doesn't test the real stdio transport
  or the actual agent experience.

**Rationale**: Testing the way the end user (an AI agent) actually uses it. If akr works
in the skill, it works for real.

### Decision 2: 3 parallel Haiku subagents with disjoint tool groups

**Choice**: Split the 33 tools into 3 groups of ~11 tools each. Each Haiku subagent tests
one group independently. Groups are disjoint — no tool is tested by two agents.

| Agent | Group | Tools |
|-------|-------|-------|
| Haiku #1 | Type Intelligence | lookup_type, type_members, type_hierarchy, find_implementations, find_messages, message_protocol, virtual_methods, supervision_defaults, search_source |
| Haiku #2 | Config & Hosting | config_reference, hosting_extensions, hosting_to_hocon, config_tree, hosting_startup_order, config_diff, changelog, hosting_di_patterns |
| Haiku #3 | Streams, Persistence & Ecosystem | stream_stages, stage_detail, graph_dsl_operators, stream_attributes, journal_plugins, snapshot_plugins, persistence_query, event_adapter_chain, serializer_bindings, serializer_detail, serialization_manifest, hosting_testkit, testkit_helpers, cluster_events, extension_points, dependency_graph |

**Rationale**: All tools are read-only, so parallelism is safe. Haiku is fast and cheap
for validation work. Three agents keeps each prompt focused and under context limits.

### Decision 3: Strict validation with explicit criteria per tool

**Choice**: Every tool call has specific pass/fail criteria — not just "returns something"
but "contains class ActorBase" or "contains MemberUp, MemberRemoved". String matching,
section presence, minimum counts.

**Rationale**: The E2E test represents the quality of the MCP server. Loose validation
would miss regressions. Strict criteria catch format changes, missing data, broken parsing.

### Decision 4: Multi-tool scenarios after parallel phase

**Choice**: Opus runs 4 cross-tool scenarios after all parallel checks pass:
- HOCON-to-Hosting migration
- Persistence setup
- Actor hierarchy exploration
- Cross-reference consistency

**Rationale**: Individual tool checks validate correctness; scenarios validate that tools
compose into useful workflows. A tool might return valid data that doesn't cross-reference
correctly with another tool.

### Decision 5: Opus validates subagent results before recording

**Choice**: Opus parses each subagent's result table, re-runs any FAIL that could be
a subagent misread, and only then records the final status.

**Rationale**: Learned from FunkArr — Haiku can occasionally misparse tool output. One
Opus re-check is cheap insurance against false negatives.

### Decision 6: No SKIP allowed

**Choice**: Every test must be PASS or FAIL. If a tool is unavailable or broken, it's FAIL.

**Rationale**: Consistent with FunkArr pattern. SKIPs hide problems.

## Risks / Trade-offs

- **[Akka source changes break validation]** → Criteria reference specific type names
  (ActorBase, Props) that are stable across Akka versions. If Akka.NET removes these core
  types, the test plan needs updating — but that would be a major breaking change.

- **[Tool output format changes]** → If a tool's output format changes (e.g., different
  section headers), validation criteria need updating. Mitigation: criteria check for
  content presence, not exact format.

- **[Haiku context limits]** → 16 tools in Haiku #3 is the most. Mitigation: each tool
  check is a simple call+validate, so context stays manageable. Split further if needed.

- **[akr not configured in session]** → Skill preflight checks for tool availability and
  gives clear instructions if akr is not available.
