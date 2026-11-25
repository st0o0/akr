# akr E2E Test Plan

End-to-end validation of all 37 akr MCP tools through realistic agent usage scenarios.
Invoked via `/e2e-test`. Opus orchestrates, 4 Haiku subagents test in parallel.

## S0: Preflight

### S0.1 Tool availability

- **Action**: ToolSearch for `mcp__akr__*` tools
- **PASS**: At least 37 tools discovered
- **FAIL**: Tools not available → abort with setup instructions

### S0.2 Smoke test

- **Action**: `lookup_type(type: "Props")`
- **PASS**: Result contains `class Props` AND contains `Akka.Actor`
- **FAIL**: Abort entire test run

---

## S1: Type Intelligence (Haiku #1)

### S1.1 lookup_type — known type

- **Action**: `lookup_type(type: "ActorBase")`
- **PASS**: Contains `class ActorBase` AND namespace contains `Akka.Actor`

### S1.2 lookup_type — interface

- **Action**: `lookup_type(type: "IActorRef")`
- **PASS**: Contains `interface IActorRef` AND namespace contains `Akka.Actor`

### S1.3 lookup_type — not found

- **Action**: `lookup_type(type: "ZzzNonExistentType999")`
- **PASS**: Contains `not found` (case-insensitive)

### S1.4 lookup_type — full source

- **Action**: `lookup_type(type: "Props", fullSource: true)`
- **PASS**: Result is significantly longer than S0.2 smoke result AND contains method bodies (curly braces with code inside)

### S1.5 type_members

- **Action**: `type_members(type: "Props")`
- **PASS**: Contains at least 2 of: `Properties:`, `Methods:`, `Constructors:` as section headers

### S1.6 type_members — interface

- **Action**: `type_members(type: "IActorRef")`
- **PASS**: Contains `Tell` or `Ask` in method signatures

### S1.7 type_hierarchy

- **Action**: `type_hierarchy(type: "ActorBase")`
- **PASS**: Contains `ActorBase` AND at least one descendant or ancestor type name

### S1.8 find_implementations

- **Action**: `find_implementations(type: "IActorRef")`
- **PASS**: Contains at least 1 implementing class name

### S1.9 virtual_methods

- **Action**: `virtual_methods(type: "ActorBase")`
- **PASS**: Contains at least 2 of: `PreStart`, `PostStop`, `AroundReceive`, `PreRestart`, `PostRestart`

### S1.10 supervision_defaults

- **Action**: `supervision_defaults()`
- **PASS**: Contains `SupervisorStrategy` or `OneForOneStrategy` or `AllForOneStrategy`

### S1.11 search_source

- **Action**: `search_source(pattern: "Props\\.Create")`
- **PASS**: Contains at least 1 match with file path and line content

---

## S2: Config & Hosting (Haiku #2)

### S2.1 config_reference — specific path

- **Action**: `config_reference(path: "akka.actor.provider")`
- **PASS**: Contains a HOCON value (e.g. `local` or `remote` or `cluster` or `=` assignment)

### S2.2 config_reference — persistence

- **Action**: `config_reference(path: "akka.persistence.journal")`
- **PASS**: Contains `plugin` or `journal` in a HOCON block

### S2.3 config_reference — not found

- **Action**: `config_reference(path: "zzz.nonexistent.path")`
- **PASS**: Contains `No config found` (case-insensitive)

### S2.4 config_reference — no filter

- **Action**: `config_reference()`
- **PASS**: Contains `reference.conf` in output AND result length > 500 characters

### S2.5 hosting_extensions — unfiltered

- **Action**: `hosting_extensions()`
- **PASS**: Contains at least 5 signatures matching `With*` or `Add*` pattern

### S2.6 hosting_extensions — filtered

- **Action**: `hosting_extensions(filter: "Persistence")`
- **PASS**: Every file path in output contains `Persistence` (case-insensitive) AND at least 1 signature present

### S2.7 hosting_extensions — no match

- **Action**: `hosting_extensions(filter: "ZzzNonexistentFilter999")`
- **PASS**: Contains `No hosting extensions found` or result is empty/minimal

### S2.8 hosting_to_hocon

- **Action**: `hosting_to_hocon()`
- **PASS**: Contains at least 1 mapping with both a `With*`/`Add*` method name and an `akka.*` config path

### S2.9 config_tree

- **Action**: `config_tree()`
- **PASS**: Contains hierarchical output with `akka` as root AND at least 2 levels of indentation or nesting

### S2.10 hosting_startup_order

- **Action**: `hosting_startup_order()`
- **PASS**: Result is non-empty AND contains ordering or registration-related content

### S2.11 config_diff

- **Action**: `config_diff()`
- **PASS**: Contains at least 1 config key (dotted path like `akka.*`)

### S2.12 changelog

- **Action**: `changelog()`
- **PASS**: Result is non-empty (may contain changelog entries or "no changelog" message — both valid, but must not error)

### S2.13 hosting_di_patterns

- **Action**: `hosting_di_patterns()`
- **PASS**: Contains at least 1 of: `IRequiredActor`, `ActorRegistry`, `DependencyResolver`, `Props`

---

## S3: Streams, Persistence & Ecosystem (Haiku #3)

### S3.1 stream_stages

- **Action**: `stream_stages()`
- **PASS**: Contains at least 3 of: `Throttle`, `Merge`, `Broadcast`, `Balance`, `Zip`, `Concat`, `Buffer` AND contains shape classification info (e.g. `Source`, `Flow`, `Sink`, `Fan`)

### S3.2 stage_detail

- **Action**: `stage_detail(stage: "Throttle")`
- **PASS**: Contains `class` or `Throttle` with C# source code (curly braces, method bodies)

### S3.3 graph_dsl_operators

- **Action**: `graph_dsl_operators()`
- **PASS**: Contains builder or operator signatures AND result is non-empty

### S3.4 stream_attributes

- **Action**: `stream_attributes()`
- **PASS**: Contains at least 1 attribute with a default value or configuration option

### S3.5 journal_plugins

- **Action**: `journal_plugins()`
- **PASS**: Contains at least 1 journal plugin with file path

### S3.6 snapshot_plugins

- **Action**: `snapshot_plugins()`
- **PASS**: Contains at least 1 snapshot plugin with file path

### S3.7 persistence_query

- **Action**: `persistence_query()`
- **PASS**: Contains `ReadJournal` or query-related type name

### S3.8 event_adapter_chain

- **Action**: `event_adapter_chain()`
- **PASS**: Result is non-empty (adapter info or "no adapters" — both valid, must not error)

### S3.9 serializer_bindings

- **Action**: `serializer_bindings()`
- **PASS**: Contains `System.Object` or a serializer binding entry with type-to-serializer mapping

### S3.10 serializer_detail

- **Action**: `serializer_detail()`
- **PASS**: Contains `Serializer` class source or serializer class names AND result is non-empty

### S3.11 serialization_manifest

- **Action**: `serialization_manifest()`
- **PASS**: Contains `Manifest` or manifest-related content AND result is non-empty

### S3.12 hosting_testkit

- **Action**: `hosting_testkit()`
- **PASS**: Contains both `TestKit` and `Hosting.TestKit` or comparison between the two

### S3.13 testkit_helpers

- **Action**: `testkit_helpers()`
- **PASS**: Contains at least 2 of: `ExpectMsg`, `AwaitCondition`, `Watch`, `ExpectNoMsg`, `Within`

### S3.14 cluster_events

- **Action**: `cluster_events()`
- **PASS**: Contains at least 3 of: `MemberUp`, `MemberRemoved`, `MemberLeft`, `MemberExited`, `MemberJoined`, `UnreachableMember`, `ReachableMember`

### S3.15 extension_points

- **Action**: `extension_points()`
- **PASS**: Contains at least 1 interface with `Provider`, `Plugin`, or `Extension` in the name

### S3.16 dependency_graph

- **Action**: `dependency_graph()`
- **PASS**: Contains at least 2 package names AND shows dependency relationships (references or arrows)

---

## S4: Docs, Versioning & Pipelines (Haiku #4)

### S4.1 docs_search — basic query

- **Action**: `docs_search(query: "cluster sharding")`
- **PASS**: Contains at least 1 result with file name AND excerpt text about sharding or cluster

### S4.2 docs_search — no results

- **Action**: `docs_search(query: "ZzzCompletelyNonexistentTopic999")`
- **PASS**: Contains `No documentation found` (case-insensitive)

### S4.3 docs_example — with language filter

- **Action**: `docs_example(topic: "persistence", language: "csharp")`
- **PASS**: Contains at least 1 code block with C# code (e.g. `var`, `class`, `public`, or curly braces)

### S4.4 docs_example — hocon filter

- **Action**: `docs_example(topic: "remoting", language: "hocon")`
- **PASS**: Contains at least 1 code block with HOCON content (e.g. `akka` or `{` block)

### S4.5 docs_example — no results topic

- **Action**: `docs_example(topic: "ZzzNonexistentTopic999")`
- **PASS**: Contains `No documentation` or `No code examples` (case-insensitive)

### S4.6 source_diff — same version

- **Action**: `source_diff(typeName: "Props", versionA: "1.5.30", versionB: "1.5.30")`
- **PASS**: Contains `No changes` (case-insensitive)

### S4.7 source_diff — missing parameters

- **Action**: `source_diff(typeName: "Props")`
- **PASS**: Contains `required` or error message about missing version parameters

### S4.8 migration_guide — same version

- **Action**: `migration_guide(fromVersion: "1.5.30", toVersion: "1.5.30")`
- **PASS**: Contains `Migration Guide` AND `No changelog` or `No configuration changes` (indicates valid but empty migration)

### S4.9 lookup_type — version pinning

- **Action**: `lookup_type(type: "Props", version: "1.5.30")`
- **PASS**: Contains `class Props` AND `Akka.Actor` (proves version-pinned fetch works)

### S4.10 docs_example — unfiltered

- **Action**: `docs_example(topic: "supervision strategy")`
- **PASS**: Contains at least 2 code examples (multiple ``` blocks or "code example(s)")

### S4.11 docs_search — ask pattern

- **Action**: `docs_search(query: "ask pattern timeout")`
- **PASS**: Contains at least 1 result (non-empty)

---

## S5: Multi-Tool Scenarios (Opus)

### S5.1 HOCON-to-Hosting Migration

1. `config_reference(path: "akka.cluster")`
2. `hosting_extensions(filter: "Cluster")`
3. `hosting_to_hocon()`
- **PASS**: Step 2 contains a `WithClustering` or `AddClustering` extension AND Step 3 maps a clustering method to an `akka.cluster.*` config path

### S5.2 Persistence Setup

1. `journal_plugins()`
2. `config_reference(path: "akka.persistence")`
3. `hosting_extensions(filter: "Persistence")`
- **PASS**: Step 1 names a journal plugin AND Step 2 contains persistence config AND Step 3 contains a persistence-related `With*`/`Add*` method

### S5.3 Actor Hierarchy Exploration

1. `lookup_type(type: "ActorBase")`
2. `type_hierarchy(type: "ActorBase")`
3. `virtual_methods(type: "ActorBase")`
4. `type_members(type: "ActorBase")`
- **PASS**: Type from Step 1 appears in Step 2 hierarchy AND at least 1 virtual method from Step 3 appears in Step 4 members output

### S5.4 Cross-Reference Consistency

1. `dependency_graph()`
2. `extension_points()`
3. `testkit_helpers()`
- **PASS**: All 3 results are non-empty AND dependency_graph contains at least 2 known Akka package names (e.g. `Akka`, `Akka.Remote`, `Akka.Cluster`, `Akka.Persistence`, `Akka.Streams`)

---

## S6: Results Format

### Report file

Write to: `e2e/E2E-TEST-RESULTS-<YYYY-MM-DD>.md`

### Report structure

```markdown
# akr E2E Test Results — <YYYY-MM-DD>

## Summary

| Metric | Count |
|--------|-------|
| PASS   | N     |
| FAIL   | N     |
| Total  | N     |

## S0: Preflight

| # | Check | Status | Detail |
|---|-------|--------|--------|
| S0.1 | Tool availability | PASS/FAIL | ... |
| S0.2 | Smoke test | PASS/FAIL | ... |

## S1: Type Intelligence

| # | Tool | Input | Expected | Status | Detail |
|---|------|-------|----------|--------|--------|
| S1.1 | lookup_type | ActorBase | class ActorBase, Akka.Actor | PASS/FAIL | ... |
...

## S2: Config & Hosting
(same table format)

## S3: Streams, Persistence & Ecosystem
(same table format)

## S4: Multi-Tool Scenarios

| # | Scenario | Tools Used | Validation | Status | Detail |
|---|----------|------------|------------|--------|--------|
| S4.1 | HOCON Migration | config_reference, hosting_extensions, hosting_to_hocon | Cross-ref | PASS/FAIL | ... |
...
```

### Rules

- **No SKIP**: Every check is PASS or FAIL
- **No editing the test plan during a run**
- **Re-check FAILs**: Opus re-runs any FAIL reported by a Haiku agent before recording it
