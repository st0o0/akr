## ADDED Requirements

### Requirement: Test plan covers all 33 MCP tools

The test plan SHALL define at least one check for each of the 33 registered MCP tools.
Each check SHALL specify the tool name, input parameters, and explicit validation criteria.

#### Scenario: Every tool has a check

- **WHEN** the test plan is reviewed
- **THEN** every tool registered in McpSetup SHALL have at least one check entry
- **AND** each entry SHALL specify: tool name, input, expected content patterns

### Requirement: Validation criteria are strict and specific

Each tool check SHALL have validation criteria that go beyond "returns a result". Criteria
SHALL check for specific content strings, section headers, minimum match counts, or
structural patterns.

#### Scenario: Type lookup validation

- **WHEN** the check calls lookup_type("ActorBase")
- **THEN** validation SHALL require the result contains "class ActorBase"
- **AND** contains a namespace starting with "Akka"

#### Scenario: Config reference validation

- **WHEN** the check calls config_reference("akka.actor.provider")
- **THEN** validation SHALL require the result contains a HOCON block

#### Scenario: Error case validation

- **WHEN** the check calls lookup_type with a non-existent type
- **THEN** validation SHALL require the result contains "not found"

### Requirement: Tool checks grouped into 3 disjoint parallel groups

The test plan SHALL organize checks into exactly 3 groups that can run in parallel:
Type Intelligence, Config & Hosting, Streams & Persistence & Ecosystem. No tool SHALL
appear in more than one group.

#### Scenario: Groups are disjoint

- **WHEN** the three parallel groups are examined
- **THEN** no tool name SHALL appear in more than one group
- **AND** the union of all groups SHALL equal the full set of 33 tools

### Requirement: Multi-tool scenarios defined with cross-validation

The test plan SHALL define at least 4 multi-tool scenarios that call tools from different
groups and validate consistency between their results.

#### Scenario: Scenario defines cross-tool validation

- **WHEN** the HOCON migration scenario is examined
- **THEN** it SHALL specify which tools to call in sequence
- **AND** SHALL define what consistency check to perform across results
