## ADDED Requirements

### Requirement: Skill preflight checks akr availability

The skill SHALL verify that akr MCP tools are available in the current session before
running any tests. If tools are not available, the skill SHALL display setup instructions
and abort.

#### Scenario: akr tools available

- **WHEN** the skill is invoked
- **AND** akr MCP tools are available via ToolSearch
- **THEN** the skill SHALL proceed with the test run

#### Scenario: akr tools not available

- **WHEN** the skill is invoked
- **AND** akr MCP tools are not available
- **THEN** the skill SHALL display instructions to configure akr as MCP server
- **AND** SHALL NOT attempt any tool calls

### Requirement: Smoke test validates basic operation

The skill SHALL run a smoke test (lookup_type with a known type) before launching parallel
agents. If the smoke test fails, the skill SHALL abort with FAIL and not launch subagents.

#### Scenario: Smoke test passes

- **WHEN** the smoke test calls lookup_type("Props")
- **AND** the result contains "class Props" and namespace "Akka.Actor"
- **THEN** the skill SHALL proceed to the parallel phase

#### Scenario: Smoke test fails

- **WHEN** the smoke test call returns an error or unexpected result
- **THEN** the skill SHALL record FAIL for the smoke test
- **AND** SHALL abort the test run with an error summary

### Requirement: Parallel Haiku subagents test disjoint tool groups

The skill SHALL launch exactly 3 Haiku subagents in a single message to run tool checks
in parallel. Each agent SHALL test a disjoint set of tools. No tool SHALL be tested by
more than one agent.

#### Scenario: Three agents launched in parallel

- **WHEN** the smoke test passes
- **THEN** the skill SHALL launch 3 Haiku agents in one message
- **AND** each agent SHALL receive the test plan section for its tool group
- **AND** each agent SHALL return a markdown table with columns: # | Tool | Input | Expected | Status | Detail

### Requirement: Opus validates subagent results

After all subagents complete, Opus SHALL parse each result table. Any FAIL result SHALL
be re-checked by Opus calling the tool directly. Only confirmed FAILs SHALL be recorded.

#### Scenario: Subagent reports FAIL that is actually PASS

- **WHEN** a subagent reports FAIL for a tool check
- **AND** Opus re-runs the tool and the result meets the validation criteria
- **THEN** the skill SHALL record PASS for that check

#### Scenario: Subagent FAIL confirmed

- **WHEN** a subagent reports FAIL for a tool check
- **AND** Opus re-runs the tool and the result still fails validation
- **THEN** the skill SHALL record FAIL with detail from the re-check

### Requirement: Multi-tool scenarios validate cross-tool consistency

After parallel checks complete, Opus SHALL run multi-tool scenarios that validate tools
compose correctly across groups.

#### Scenario: HOCON migration scenario

- **WHEN** running the HOCON migration scenario
- **THEN** the skill SHALL call config_reference, hosting_extensions, and hosting_to_hocon
- **AND** SHALL validate that hosting extensions reference config paths from config_reference

#### Scenario: Persistence setup scenario

- **WHEN** running the persistence scenario
- **THEN** the skill SHALL call journal_plugins, config_reference, and hosting_extensions
- **AND** SHALL validate that journal plugins reference persistence config paths

#### Scenario: Actor hierarchy scenario

- **WHEN** running the actor hierarchy scenario
- **THEN** the skill SHALL call lookup_type, type_hierarchy, virtual_methods, and type_members
- **AND** SHALL validate that virtual methods from type_hierarchy appear in type_members

#### Scenario: Cross-reference consistency scenario

- **WHEN** running the cross-reference scenario
- **THEN** the skill SHALL call dependency_graph, extension_points, and testkit_helpers
- **AND** SHALL validate that the results are non-empty and structurally consistent

### Requirement: Results written to timestamped report

The skill SHALL write results to `e2e/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with a summary
header (total PASS/FAIL counts) and per-section tables.

#### Scenario: Report written on completion

- **WHEN** all checks (parallel and sequential) complete
- **THEN** the skill SHALL write the results file
- **AND** the summary SHALL include total PASS and FAIL counts
- **AND** each section SHALL have a table with # | Tool | Input | Expected | Status | Detail

#### Scenario: No SKIP status allowed

- **WHEN** any tool check cannot be completed
- **THEN** the skill SHALL record FAIL, never SKIP
