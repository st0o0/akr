---
name: e2e-test
description: Run end-to-end tests against all 37 akr MCP tools with strict validation. Opus orchestrates, 4 Haiku subagents test in parallel. Results go to e2e/E2E-TEST-RESULTS-<date>.md.
---

# akr E2E Test Skill

Run a full E2E test of all akr MCP tools. Read `e2e/E2E-TEST-PLAN.md` for the complete
test plan with validation criteria.

## Important Rules

1. **No SKIP** — every check is PASS or FAIL. If a tool errors, it's FAIL.
2. **Never edit the test plan** during a run.
3. **Re-check FAILs** — before recording a FAIL from a Haiku subagent, re-run the tool
   yourself (Opus) to confirm. Only confirmed FAILs are recorded.
4. **Subagent results** — each Haiku returns ONLY a markdown table. Parse it, validate it.

## Execution Flow

### S0: Preflight

1. Load akr MCP tools:

```
ToolSearch with query "+mcp__akr"
```

If no `mcp__akr__*` tools are found:

> **E2E test aborted.** akr is not configured as an MCP server in this session.
>
> To configure: ensure `.mcp.json` contains `{"mcpServers":{"akr":{"command":"akr"}}}`
> and restart Claude Code.

Record S0.1 as PASS or FAIL.

2. Smoke test — call `lookup_type(type: "Props")`:
   - PASS: result contains `class Props` AND `Akka.Actor`
   - FAIL: abort the entire run, write results with only S0

Record S0.2 as PASS or FAIL. If FAIL, skip everything else and write report.

### S1–S4: Parallel Tool Checks

Launch exactly **4 Haiku agents in a single message** using the Agent tool:

#### Haiku #1: Type Intelligence

```
Agent({
  name: "e2e-type-intelligence",
  model: "haiku",
  description: "E2E Type Intelligence checks",
  prompt: <see prompt template below>
})
```

**Haiku #1 Prompt:**

You are testing akr MCP tools. For each check below, call the tool with the given input,
then validate the result against the criteria. Report results as a markdown table ONLY.

Format: `| # | Tool | Input | Expected | Status | Detail |`

Checks (read the test plan for full criteria):

- S1.1: `lookup_type(type: "ActorBase")` — PASS if contains `class ActorBase` AND namespace `Akka.Actor`
- S1.2: `lookup_type(type: "IActorRef")` — PASS if contains `interface IActorRef`
- S1.3: `lookup_type(type: "ZzzNonExistentType999")` — PASS if contains `not found`
- S1.4: `lookup_type(type: "Props", fullSource: true)` — PASS if result contains method bodies (long output with curly braces)
- S1.5: `type_members(type: "Props")` — PASS if contains at least 2 of: `Properties:`, `Methods:`, `Constructors:`
- S1.6: `type_members(type: "IActorRef")` — PASS if contains `Tell` or `Ask`
- S1.7: `type_hierarchy(type: "ActorBase")` — PASS if contains ActorBase with ancestors or descendants
- S1.8: `find_implementations(type: "IActorRef")` — PASS if at least 1 implementing class
- S1.9: `virtual_methods(type: "ActorBase")` — PASS if at least 2 of: PreStart, PostStop, AroundReceive, PreRestart, PostRestart
- S1.10: `supervision_defaults()` — PASS if contains SupervisorStrategy or OneForOneStrategy
- S1.11: `search_source(pattern: "Props\\.Create")` — PASS if at least 1 match with file path

Return ONLY the markdown table. No other text.

#### Haiku #2: Config & Hosting

```
Agent({
  name: "e2e-config-hosting",
  model: "haiku",
  description: "E2E Config & Hosting checks",
  prompt: <see prompt template below>
})
```

**Haiku #2 Prompt:**

You are testing akr MCP tools. For each check below, call the tool with the given input,
then validate the result against the criteria. Report results as a markdown table ONLY.

Format: `| # | Tool | Input | Expected | Status | Detail |`

Checks:

- S2.1: `config_reference(path: "akka.actor.provider")` — PASS if contains HOCON value
- S2.2: `config_reference(path: "akka.persistence.journal")` — PASS if contains `plugin` or `journal` in HOCON
- S2.3: `config_reference(path: "zzz.nonexistent.path")` — PASS if contains `No config found`
- S2.4: `config_reference()` — PASS if contains `reference.conf` AND length > 500
- S2.5: `hosting_extensions()` — PASS if at least 5 `With*`/`Add*` signatures
- S2.6: `hosting_extensions(filter: "Persistence")` — PASS if all file paths contain `Persistence` AND at least 1 signature
- S2.7: `hosting_extensions(filter: "ZzzNonexistentFilter999")` — PASS if contains `No hosting extensions found` or empty
- S2.8: `hosting_to_hocon()` — PASS if contains mapping with `With*`/`Add*` and `akka.*` path
- S2.9: `config_tree()` — PASS if contains `akka` root with nested structure
- S2.10: `hosting_startup_order()` — PASS if non-empty with ordering content
- S2.11: `config_diff()` — PASS if contains at least 1 dotted config key
- S2.12: `changelog()` — PASS if non-empty (content or "no changelog" — must not error)
- S2.13: `hosting_di_patterns()` — PASS if contains IRequiredActor or ActorRegistry or DependencyResolver

Return ONLY the markdown table. No other text.

#### Haiku #3: Streams, Persistence & Ecosystem

```
Agent({
  name: "e2e-streams-ecosystem",
  model: "haiku",
  description: "E2E Streams & Ecosystem checks",
  prompt: <see prompt template below>
})
```

**Haiku #3 Prompt:**

You are testing akr MCP tools. For each check below, call the tool with the given input,
then validate the result against the criteria. Report results as a markdown table ONLY.

Format: `| # | Tool | Input | Expected | Status | Detail |`

Checks:

- S3.1: `stream_stages()` — PASS if at least 3 of: Throttle, Merge, Broadcast, Balance, Zip, Concat, Buffer AND shape info
- S3.2: `stage_detail(stage: "Throttle")` — PASS if contains C# source with Throttle
- S3.3: `graph_dsl_operators()` — PASS if non-empty with operator signatures
- S3.4: `stream_attributes()` — PASS if at least 1 attribute with default value
- S3.5: `journal_plugins()` — PASS if at least 1 journal plugin with file path
- S3.6: `snapshot_plugins()` — PASS if at least 1 snapshot plugin with file path
- S3.7: `persistence_query()` — PASS if contains ReadJournal or query type
- S3.8: `event_adapter_chain()` — PASS if non-empty (must not error)
- S3.9: `serializer_bindings()` — PASS if contains System.Object or binding entry
- S3.10: `serializer_detail()` — PASS if contains Serializer class info, non-empty
- S3.11: `serialization_manifest()` — PASS if contains Manifest content, non-empty
- S3.12: `hosting_testkit()` — PASS if contains both TestKit and Hosting.TestKit
- S3.13: `testkit_helpers()` — PASS if at least 2 of: ExpectMsg, AwaitCondition, Watch, ExpectNoMsg, Within
- S3.14: `cluster_events()` — PASS if at least 3 of: MemberUp, MemberRemoved, MemberLeft, MemberExited, MemberJoined
- S3.15: `extension_points()` — PASS if at least 1 interface with Provider/Plugin/Extension
- S3.16: `dependency_graph()` — PASS if at least 2 package names with dependency relationships

Return ONLY the markdown table. No other text.

#### Haiku #4: Docs, Versioning & Pipelines

```
Agent({
  name: "e2e-docs-versioning",
  model: "haiku",
  description: "E2E Docs & Versioning checks",
  prompt: <see prompt template below>
})
```

**Haiku #4 Prompt:**

You are testing akr MCP tools. For each check below, call the tool with the given input,
then validate the result against the criteria. Report results as a markdown table ONLY.

Format: `| # | Tool | Input | Expected | Status | Detail |`

Checks:

- S4.1: `docs_search(query: "cluster sharding")` — PASS if at least 1 result with file name and excerpt about sharding or cluster
- S4.2: `docs_search(query: "ZzzCompletelyNonexistentTopic999")` — PASS if contains `No documentation found`
- S4.3: `docs_example(topic: "persistence", language: "csharp")` — PASS if contains at least 1 C# code block (var, class, public, or curly braces)
- S4.4: `docs_example(topic: "remoting", language: "hocon")` — PASS if contains at least 1 HOCON code block (akka or { block)
- S4.5: `docs_example(topic: "ZzzNonexistentTopic999")` — PASS if contains `No documentation` or `No code examples`
- S4.6: `source_diff(typeName: "Props", versionA: "1.5.30", versionB: "1.5.30")` — PASS if contains `No changes`
- S4.7: `source_diff(typeName: "Props")` — PASS if contains `required` or error about missing version parameters
- S4.8: `migration_guide(fromVersion: "1.5.30", toVersion: "1.5.30")` — PASS if contains `Migration Guide` AND (`No changelog` or `No configuration changes`)
- S4.9: `lookup_type(type: "Props", version: "1.5.30")` — PASS if contains `class Props` and `Akka.Actor`
- S4.10: `docs_example(topic: "supervision strategy")` — PASS if contains at least 2 code examples
- S4.11: `docs_search(query: "ask pattern timeout")` — PASS if at least 1 result (non-empty)

Return ONLY the markdown table. No other text.

### Result Validation

After all 4 Haiku agents complete:

1. **Parse** each agent's markdown table
2. **Re-check FAILs**: For every FAIL row, call the tool yourself with the same input.
   If your result meets the criteria, record PASS. If it still fails, record FAIL.
3. Collect all results into the S1/S2/S3/S4 sections

### S5: Multi-Tool Scenarios (Opus sequential)

Run these yourself — do not delegate:

**S5.1 HOCON Migration:**
1. Call `config_reference(path: "akka.cluster")`
2. Call `hosting_extensions(filter: "Cluster")`
3. Call `hosting_to_hocon()`
4. PASS if Step 2 has `WithClustering`/`AddClustering` AND Step 3 maps it to `akka.cluster.*`

**S5.2 Persistence Setup:**
1. Call `journal_plugins()`
2. Call `config_reference(path: "akka.persistence")`
3. Call `hosting_extensions(filter: "Persistence")`
4. PASS if all 3 return relevant, consistent content

**S5.3 Actor Hierarchy:**
1. Call `lookup_type(type: "ActorBase")`
2. Call `type_hierarchy(type: "ActorBase")`
3. Call `virtual_methods(type: "ActorBase")`
4. Call `type_members(type: "ActorBase")`
5. PASS if hierarchy contains the type AND virtual methods appear in members

**S5.4 Cross-Reference Consistency:**
1. Call `dependency_graph()`
2. Call `extension_points()`
3. Call `testkit_helpers()`
4. PASS if all non-empty AND dependency_graph contains known Akka package names

### Report Generation

Write results to `e2e/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` using the format from the test
plan's S6 section. Include:

- Summary table with PASS/FAIL totals
- S0 preflight table
- S1 Type Intelligence table
- S2 Config & Hosting table
- S3 Streams & Ecosystem table
- S4 Docs, Versioning & Pipelines table
- S5 Multi-Tool Scenarios table

End with a one-line summary: "E2E complete: N/M passed."
