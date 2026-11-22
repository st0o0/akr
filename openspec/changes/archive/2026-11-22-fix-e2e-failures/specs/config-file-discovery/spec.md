## ADDED Requirements

### Requirement: Enumerate all well-known Akka.NET config files

The system SHALL discover HOCON config files beyond `reference.conf` in cached source roots. Specifically, it SHALL find:
- `reference.conf` (all directories, recursive) — existing behavior
- `*.conf` files in `Configuration/` subdirectories under `src/core/`
- `persistence.conf` files under `src/core/`

The system SHALL exclude config files in paths containing `/test/`, `/Test/`, `/example/`, or `/Examples/` (case-insensitive).

#### Scenario: Core akka.conf is discovered
- **WHEN** `config_reference(path: "akka.actor.provider")` is called
- **THEN** the result SHALL contain the value `Akka.Actor.LocalActorRefProvider` from `src/core/Akka/Configuration/akka.conf`

#### Scenario: Cluster.conf is discovered
- **WHEN** `config_reference(path: "akka.cluster")` is called
- **THEN** the result SHALL contain cluster configuration from `src/core/Akka.Cluster/Configuration/Cluster.conf`

#### Scenario: Remote.conf is discovered
- **WHEN** `config_reference(path: "akka.remote")` is called
- **THEN** the result SHALL contain remote transport configuration from `src/core/Akka.Remote/Configuration/Remote.conf`

#### Scenario: persistence.conf is discovered
- **WHEN** `config_reference(path: "akka.persistence")` is called
- **THEN** the result SHALL include core persistence config from `src/core/Akka.Persistence/persistence.conf` in addition to any extension-repo configs

#### Scenario: Test configs are excluded
- **WHEN** config files are enumerated
- **THEN** files under test or example directories SHALL NOT be included

### Requirement: Serializer bindings use broadened config discovery

The `serializer_bindings` tool SHALL use the same broadened config file enumeration as `config_reference`.

#### Scenario: Core serialization bindings are found
- **WHEN** `serializer_bindings()` is called with no filter
- **THEN** the result SHALL contain at least `"System.Object" = json` and `"System.Byte[]" = bytes` from core `akka.conf`

### Requirement: Config tree and config diff use broadened discovery

The `config_tree` and `config_diff` tools SHALL use the same broadened config file enumeration.

#### Scenario: Config tree includes core akka paths
- **WHEN** `config_tree()` is called
- **THEN** the result SHALL contain `akka.actor` with nested keys from core `akka.conf`

#### Scenario: Config diff includes core keys
- **WHEN** `config_diff()` is called
- **THEN** the added keys SHALL include core paths like `akka.actor.provider`

### Requirement: Shared config enumeration helper

Config file enumeration logic SHALL be implemented in a single shared helper method, not duplicated across tools.

#### Scenario: All config-consuming tools use the same helper
- **WHEN** any tool that reads HOCON config files is invoked
- **THEN** it SHALL delegate to the shared `EnumerateConfigFiles` helper
