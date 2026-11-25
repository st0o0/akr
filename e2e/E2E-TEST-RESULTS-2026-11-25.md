# E2E Test Results — 2026-11-25

## Summary

| Section | Checks | Passed | Failed |
|---------|--------|--------|--------|
| S0: Preflight | 2 | 2 | 0 |
| S1: Type Intelligence | 11 | 11 | 0 |
| S2: Config & Hosting | 13 | 13 | 0 |
| S3: Streams & Ecosystem | 16 | 16 | 0 |
| S4: Docs, Versioning & Pipelines | 11 | 11 | 0 |
| S5: Multi-Tool Scenarios | 4 | 4 | 0 |
| **Total** | **57** | **57** | **0** |

## S0: Preflight

| # | Check | Status | Detail |
|---|-------|--------|--------|
| S0.1 | Tool availability | PASS | All 37 akr tools loaded |
| S0.2 | Smoke test | PASS | Contains `class Props` and `Akka.Actor` |

## S1: Type Intelligence

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S1.1 | lookup_type | ActorBase | PASS | `public abstract partial class ActorBase`, namespace `Akka.Actor` |
| S1.2 | lookup_type | IActorRef | PASS | `public interface IActorRef : ICanTell` |
| S1.3 | lookup_type | ZzzNonExistentType999 | PASS | "not found in cached Akka.NET source" |
| S1.4 | lookup_type | Props, fullSource=true | PASS | Full source with method bodies |
| S1.5 | type_members | Props | PASS | Properties, Methods, Constructors all present |
| S1.6 | type_members | IActorRef | PASS | Contains `Tell` |
| S1.7 | type_hierarchy | ActorBase | PASS | Ancestors (IInternalActor) and descendants |
| S1.8 | find_implementations | IActorRef | PASS | 4 implementations found |
| S1.9 | virtual_methods | ActorBase | PASS | All 5: PreStart, PostStop, AroundReceive, PreRestart, PostRestart |
| S1.10 | supervision_defaults | — | PASS | SupervisorStrategy and OneForOneStrategy |
| S1.11 | search_source | Props\.Create | PASS | 20 matches with file paths |

## S2: Config & Hosting

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S2.1 | config_reference | akka.actor.provider | PASS | `provider = "Akka.Actor.LocalActorRefProvider"` |
| S2.2 | config_reference | akka.persistence.journal | PASS | Contains `plugin` in HOCON |
| S2.3 | config_reference | zzz.nonexistent.path | PASS | "No config found for path" |
| S2.4 | config_reference | (all) | PASS | reference.conf present (re-checked: Haiku hit token limit on large output) |
| S2.5 | hosting_extensions | (all) | PASS | Well over 5 With*/Add* signatures |
| S2.6 | hosting_extensions | Persistence | PASS | All paths contain Persistence, 14+ signatures |
| S2.7 | hosting_extensions | ZzzNonexistentFilter999 | PASS | "No hosting extensions found" |
| S2.8 | hosting_to_hocon | WithClustering | PASS | Maps to `akka.cluster (curated)` |
| S2.9 | config_tree | — | PASS | `akka` root with nested structure |
| S2.10 | hosting_startup_order | — | PASS | Recommended registration order |
| S2.11 | config_diff | — | PASS | Dotted config keys present (re-checked: Haiku hit token limit on large output) |
| S2.12 | changelog | — | PASS | RELEASE_NOTES content returned |
| S2.13 | hosting_di_patterns | — | PASS | IRequiredActor and ActorRegistry |

## S3: Streams, Persistence & Ecosystem

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S3.1 | stream_stages | — | PASS | Throttle, Merge, Broadcast, Balance, Concat, Buffer + shapes |
| S3.2 | stage_detail | Throttle | PASS | Full Throttle.cs source |
| S3.3 | graph_dsl_operators | — | PASS | 40 operators with signatures |
| S3.4 | stream_attributes | — | PASS | AsyncBoundary with defaults |
| S3.5 | journal_plugins | — | PASS | 5 plugins with file paths |
| S3.6 | snapshot_plugins | — | PASS | 8 plugins with file paths |
| S3.7 | persistence_query | — | PASS | 4 ReadJournal providers |
| S3.8 | event_adapter_chain | — | PASS | 37 event adapters |
| S3.9 | serializer_bindings | — | PASS | System.Object -> json |
| S3.10 | serializer_detail | Json | PASS | NewtonSoftJsonSerializer source (re-checked: Haiku used too broad name) |
| S3.11 | serialization_manifest | — | PASS | Many Manifest matches |
| S3.12 | hosting_testkit | — | PASS | TestKit and Hosting.TestKit present |
| S3.13 | testkit_helpers | — | PASS | All 5: ExpectMsg, AwaitCondition, Watch, ExpectNoMsg, Within |
| S3.14 | cluster_events | — | PASS | All 5: MemberUp, MemberRemoved, MemberLeft, MemberExited, MemberJoined |
| S3.15 | extension_points | — | PASS | 17 interfaces with Provider/Plugin/Extension |
| S3.16 | dependency_graph | — | PASS | Many Akka packages with dependency relationships |

## S4: Docs, Versioning & Pipelines

| # | Tool | Input | Status | Detail |
|---|------|-------|--------|--------|
| S4.1 | docs_search | "cluster sharding" | PASS | 5 results incl. cluster-sharding.md |
| S4.2 | docs_search | "ZzzCompletelyNonexistentTopic999" | PASS | "No documentation found" |
| S4.3 | docs_example | persistence, lang=csharp | PASS | 12 C# code examples |
| S4.4 | docs_example | remoting, lang=hocon | PASS | 5 HOCON examples with `akka {` |
| S4.5 | docs_example | ZzzNonexistentTopic999 | PASS | "No documentation or code examples found" |
| S4.6 | source_diff | Props, 1.5.30 vs 1.5.30 | PASS | "No changes to type 'Props'" |
| S4.7 | source_diff | Props (no versions) | PASS | "Both versionA and versionB are required" |
| S4.8 | migration_guide | 1.5.30 → 1.5.30 | PASS | "Migration Guide" + "No changelog entries" + "No configuration changes" |
| S4.9 | lookup_type | Props, version=1.5.30 | PASS | `class Props` in `Akka.Actor` namespace |
| S4.10 | docs_example | supervision strategy | PASS | 64 code examples |
| S4.11 | docs_search | "ask pattern timeout" | PASS | 5 results |

## S5: Multi-Tool Scenarios

| # | Scenario | Status | Detail |
|---|----------|--------|--------|
| S5.1 | HOCON Migration | PASS | WithClustering maps to akka.cluster.*, config has cluster HOCON |
| S5.2 | Persistence Setup | PASS | journal_plugins, config_reference, hosting_extensions all consistent |
| S5.3 | Actor Hierarchy | PASS | Hierarchy has ActorBase, virtual methods appear in members |
| S5.4 | Cross-Reference Consistency | PASS | dependency_graph has Akka packages, extension_points has 17 interfaces |

---

E2E complete: 57/57 passed.
