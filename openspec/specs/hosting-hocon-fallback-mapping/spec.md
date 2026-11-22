# hosting-hocon-fallback-mapping Specification

## Purpose
Curated HOCON key mappings for Akka.Hosting extension methods that configure HOCON programmatically without literal key strings.

## Requirements
### Requirement: Curated fallback mapping for hosting methods

The `hosting_to_hocon` tool SHALL maintain a curated mapping for well-known hosting methods that set HOCON configuration programmatically via Options objects rather than string literals.

The curated mapping SHALL always be merged into results — not only as a fallback when the regex scanner finds nothing. When a method appears in both the regex scan and the curated map, the key sets SHALL be unioned. Curated-only entries SHALL be labeled `(curated)` in the output.

#### Scenario: WithClustering maps to akka.cluster
- **WHEN** `hosting_to_hocon(query: "WithClustering")` is called
- **THEN** the result SHALL contain a mapping from `WithClustering` to `akka.cluster` paths

#### Scenario: WithRemoting maps to akka.remote
- **WHEN** `hosting_to_hocon(query: "WithRemoting")` is called
- **THEN** the result SHALL contain a mapping from `WithRemoting` to `akka.remote` paths

#### Scenario: Curated entries merge with regex-discovered keys
- **WHEN** a hosting method has HOCON string literals in its source AND appears in the curated map
- **THEN** the result SHALL contain the union of regex-scanned keys and curated keys

#### Scenario: Unknown method still returns no-match
- **WHEN** `hosting_to_hocon(query: "WithZzzNonexistent")` is called
- **THEN** the result SHALL indicate no mapping was found

### Requirement: Minimum curated mappings

The curated mapping SHALL include at minimum:

| Method | HOCON root |
|--------|-----------|
| WithClustering | akka.cluster |
| WithRemoting | akka.remote |
| WithDistributedData | akka.cluster.distributed-data |
| WithSingleton | akka.cluster.singleton |
| WithShardRegion | akka.cluster.sharding |
| WithDistributedPubSub | akka.cluster.pub-sub |
| WithJournal | akka.persistence.journal |
| WithSnapshotStore | akka.persistence.snapshot-store |
| WithCoordinatedShutdown | akka.coordinated-shutdown |

#### Scenario: All minimum mappings are present
- **WHEN** each method in the minimum mapping table is queried via `hosting_to_hocon`
- **THEN** each SHALL return at least its documented HOCON root path
