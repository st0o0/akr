# E2E Test Results — 2026-11-22 (Final)

## Summary

| Section | Total | Pass | Fail |
|---------|-------|------|------|
| S0: Preflight | 2 | 2 | 0 |
| S1: Type Intelligence | 11 | 11 | 0 |
| S2: Config & Hosting | 13 | 13 | 0 |
| S3: Streams & Ecosystem | 16 | 16 | 0 |
| S4: Multi-Tool Scenarios | 4 | 4 | 0 |
| **Total** | **46** | **46** | **0** |

## S0: Preflight

| # | Check | Status | Detail |
|---|-------|--------|--------|
| S0.1 | akr MCP tools available | PASS | 33 mcp__akr__* tools discovered |
| S0.2 | lookup_type(Props) smoke test | PASS | `class Props` in `Akka.Actor` namespace |

## S1: Type Intelligence

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S1.1 | lookup_type | ActorBase | PASS | `public abstract partial class ActorBase`, namespace `Akka.Actor` |
| S1.2 | lookup_type | IActorRef | PASS | `public interface IActorRef : ICanTell, ...` |
| S1.3 | lookup_type | ZzzNonExistentType999 | PASS | "Type not found in cached Akka.NET source." |
| S1.4 | lookup_type | Props, fullSource: true | PASS | Full source with method bodies returned |
| S1.5 | type_members | Props | PASS | All three sections: Properties, Methods, Constructors |
| S1.6 | type_members | IActorRef | PASS | `Tell` found under `Inherited from ICanTell:` section |
| S1.7 | type_hierarchy | ActorBase | PASS | 89K chars output with ancestors/descendants |
| S1.8 | find_implementations | IActorRef | PASS | 4 implementations (ActorRefBase, InternalActorRefBase, etc.) |
| S1.9 | virtual_methods | ActorBase | PASS | All 5: PreStart, PostStop, AroundReceive, PreRestart, PostRestart |
| S1.10 | supervision_defaults | (default) | PASS | Contains SupervisorStrategy and OneForOneStrategy |
| S1.11 | search_source | `Props\.Create` | PASS | 20 matches with file paths |

## S2: Config & Hosting

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S2.1 | config_reference | akka.actor.provider | PASS | `provider = "Akka.Actor.LocalActorRefProvider"` from `akka.conf` |
| S2.2 | config_reference | akka.persistence.journal | PASS | Journal HOCON blocks from core `persistence.conf` + MongoDb `reference.conf` |
| S2.3 | config_reference | zzz.nonexistent.path | PASS | "No config found for path: zzz.nonexistent.path" |
| S2.4 | config_reference | (no path) | PASS | 208K chars of config data (saved to file due to size) |
| S2.5 | hosting_extensions | (no filter) | PASS | Many With*/Add* signatures |
| S2.6 | hosting_extensions | Persistence | PASS | All file paths contain Persistence |
| S2.7 | hosting_extensions | ZzzNonexistentFilter999 | PASS | "No hosting extensions found matching filter" |
| S2.8 | hosting_to_hocon | WithClustering | PASS | `WithClustering → akka.cluster (curated)` |
| S2.9 | config_tree | (no path) | PASS | Root `akka` with nested sections (actor, cluster, persistence, remote, etc.) |
| S2.10 | hosting_startup_order | (no filter) | PASS | Recommended registration order with constraints |
| S2.11 | config_diff | (no args) | PASS | 811 config keys listed |
| S2.12 | changelog | (no filter) | PASS | RELEASE_NOTES content |
| S2.13 | hosting_di_patterns | (no args) | PASS | IRequiredActor<T>, ActorRegistry, DependencyResolver |

## S3: Streams & Ecosystem

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S3.1 | stream_stages | (no filter) | PASS | Throttle, Merge, Broadcast, Balance, ZipWith, Concat, Buffer grouped by shape |
| S3.2 | stage_detail | Throttle | PASS | Throttle.cs source |
| S3.3 | graph_dsl_operators | (none) | PASS | 40 operators with signatures |
| S3.4 | stream_attributes | (none) | PASS | 4 attributes with `default:` annotations (AsyncBoundary Instance, Attributes None) |
| S3.5 | journal_plugins | (none) | PASS | 5 plugins with file paths |
| S3.6 | snapshot_plugins | (none) | PASS | 8 plugins with file paths |
| S3.7 | persistence_query | (none) | PASS | 4 ReadJournal providers |
| S3.8 | event_adapter_chain | (none) | PASS | 46 event adapters listed |
| S3.9 | serializer_bindings | (none) | PASS | 5 bindings: System.Object=json, System.Byte[]=bytes, Int32/Int64/String=primitive |
| S3.10 | serializer_detail | Json | PASS | NewtonSoftJsonSerializer source |
| S3.11 | serialization_manifest | (none) | PASS | Manifest methods across Persistence code |
| S3.12 | hosting_testkit | (none) | PASS | Both Akka.Hosting.TestKit and Akka.TestKit present |
| S3.13 | testkit_helpers | (none) | PASS | All 5: ExpectMsg, AwaitCondition, Watch, ExpectNoMsg, Within |
| S3.14 | cluster_events | (none) | PASS | All 5: MemberUp, MemberRemoved, MemberLeft, MemberExited, MemberJoined |
| S3.15 | extension_points | (none) | PASS | IActorProducerPlugin, IClusteringProvider, IReadJournalProvider, IExtension |
| S3.16 | dependency_graph | (none) | PASS | Many packages with dependency edges |

## S4: Multi-Tool Scenarios

| # | Scenario | Status | Detail |
|---|----------|--------|--------|
| S4.1 | HOCON Migration | PASS | config_reference returns full Cluster.conf, hosting_extensions has WithClustering, hosting_to_hocon maps to akka.cluster |
| S4.2 | Persistence Setup | PASS | journal_plugins (5), config_reference returns core + MongoDb persistence config, hosting_extensions all consistent |
| S4.3 | Actor Hierarchy | PASS | lookup_type, type_hierarchy (89K), virtual_methods (12), type_members all consistent; inherited IInternalActor members shown |
| S4.4 | Cross-Reference Consistency | PASS | dependency_graph (100+ packages), extension_points (16 interfaces), testkit_helpers (100+ methods) all non-empty |

E2E complete: 46/46 passed.
