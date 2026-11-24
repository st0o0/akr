## ADDED Requirements

### Requirement: Stream stage exploration workflow test

The E2E test suite SHALL include a workflow scenario that chains stream-related tools in the order developers actually use them, validating cross-tool consistency.

#### Scenario: Full stream stage exploration
- **WHEN** the stream stage exploration workflow runs
- **THEN** it SHALL execute in order: `stream_stages()` → `stage_detail("Throttle")` → `lookup_type("TimerGraphStageLogic")` → `type_hierarchy("GraphStage")` → `search_source("OnPush")`
- **AND** types discovered by `stage_detail` SHALL also be findable by `lookup_type`
- **AND** the stage's base class SHALL appear in the `type_hierarchy` result

### Requirement: HOCON migration workflow test

The E2E test suite SHALL include a workflow scenario that validates the HOCON-to-Hosting migration path end-to-end.

#### Scenario: Full HOCON migration workflow
- **WHEN** the HOCON migration workflow runs
- **THEN** it SHALL execute: `config_reference("akka.cluster")` → `hosting_to_hocon("WithClustering")` → `hosting_extensions("Cluster")` → `hosting_startup_order()`
- **AND** HOCON paths returned by `config_reference` SHALL appear in `hosting_to_hocon` results
- **AND** hosting methods from `hosting_to_hocon` SHALL appear in `hosting_extensions` results

### Requirement: Actor debugging workflow test

The E2E test suite SHALL include a workflow scenario that validates the actor investigation path.

#### Scenario: Full actor debugging workflow
- **WHEN** the actor debugging workflow runs
- **THEN** it SHALL execute: `lookup_type("ReceiveActor")` → `virtual_methods("ActorBase")` → `supervision_defaults()` → `find_messages("ReceiveActor")`
- **AND** virtual methods listed SHALL include lifecycle methods (PreStart, PostStop)
- **AND** the type found by `lookup_type` SHALL be consistent with `virtual_methods` results

### Requirement: Existing isolated tests retained

The existing isolated tool-call tests (S0-S3) SHALL be retained as a smoke test tier alongside the new workflow tests.

#### Scenario: Both test tiers run
- **WHEN** the E2E test suite runs
- **THEN** both isolated tool tests and workflow tests SHALL execute
- **AND** results from both tiers SHALL be reported in the final markdown report
