## ADDED Requirements

### Requirement: Curated fallback mapping for hosting methods

The `hosting_to_hocon` tool SHALL maintain a curated fallback mapping for well-known hosting methods that set HOCON configuration programmatically via Options objects rather than string literals.

The fallback SHALL only be used when the regex scanner finds no HOCON keys for a given method.

#### Scenario: WithClustering maps to akka.cluster
- **WHEN** `hosting_to_hocon(query: "WithClustering")` is called
- **THEN** the result SHALL contain a mapping from `WithClustering` to `akka.cluster.*` paths

#### Scenario: WithRemoting maps to akka.remote
- **WHEN** `hosting_to_hocon(query: "WithRemoting")` is called
- **THEN** the result SHALL contain a mapping from `WithRemoting` to `akka.remote.*` paths

#### Scenario: Regex-discovered mappings take precedence
- **WHEN** a hosting method has HOCON string literals in its source
- **THEN** the regex-scanned keys SHALL be used and the fallback mapping SHALL NOT replace them

#### Scenario: Unknown method still returns no-match
- **WHEN** `hosting_to_hocon(query: "WithZzzNonexistent")` is called
- **THEN** the result SHALL indicate no mapping was found (no false positives from the fallback)

### Requirement: Minimum curated mappings

The fallback mapping SHALL include at minimum:

| Method | HOCON root |
|--------|-----------|
| WithClustering | akka.cluster |
| WithRemoting | akka.remote |
| WithDistributedData | akka.cluster.distributed-data |
| WithSingleton | akka.cluster.singleton |
| WithShardRegion | akka.cluster.sharding |
| WithDistributedPubSub | akka.cluster.pub-sub |

#### Scenario: All minimum mappings are present
- **WHEN** each method in the minimum mapping table is queried via `hosting_to_hocon`
- **THEN** each SHALL return at least its documented HOCON root path
