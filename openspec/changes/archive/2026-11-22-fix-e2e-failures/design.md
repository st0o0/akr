## Context

akr tools enumerate config files from cached Akka.NET source repos. The current implementation hardcodes `reference.conf` as the only filename glob. This works for extension repos (Akka.Hosting, Akka.Persistence.Sql, Akka.Persistence.MongoDB) but misses the core akka.net mono-repo which uses project-specific names: `akka.conf`, `Cluster.conf`, `Remote.conf`, `persistence.conf`.

Additionally, `hosting_to_hocon` relies on regex scanning for HOCON string literals in method bodies — a technique that fails when hosting methods build config programmatically via Options objects. `type_members` only extracts members declared directly in a type body. `stream_attributes` only matches `static readonly` default fields.

## Goals / Non-Goals

**Goals:**
- All well-known Akka.NET HOCON config files are discoverable by tools that need them
- `hosting_to_hocon` returns useful mappings for common hosting methods like `WithClustering`
- `type_members` for interfaces surfaces inherited members or points users to parent types
- `stream_attributes` shows default values when present

**Non-Goals:**
- Full HOCON parser (we continue with regex/line-based extraction)
- Exhaustive hosting→HOCON mapping for every method (curated for common ones)
- Deep transitive inheritance resolution in type_members (one level of parent interfaces)

## Decisions

### D1: Shared config file enumeration helper

**Decision**: Extract a shared `EnumerateConfigFiles(roots)` helper that returns all HOCON config files, used by `HostingTools.FindConfig`, `EcosystemTools.FindSerializerBindings`, `ConfigMigrationTools.CollectConfig`, and `ConfigMigrationTools.BuildConfigTree`.

**Pattern**: Enumerate `reference.conf` (existing) PLUS files matching these criteria in `src/core/*/`:
- `*.conf` files directly in `Configuration/` subdirectories
- Well-known root-level config files: `persistence.conf`

This covers the actual file layout in akka.net:
```
src/core/Akka/Configuration/akka.conf          → akka.actor, akka.io, akka.serialization
src/core/Akka.Cluster/Configuration/Cluster.conf → akka.cluster
src/core/Akka.Remote/Configuration/Remote.conf   → akka.remote
src/core/Akka.Persistence/persistence.conf       → akka.persistence (core)
src/core/Akka.Streams/reference.conf             → akka.stream (already found)
```

**Alternative considered**: Glob `*.conf` everywhere — rejected because it would pick up `app.conf` examples, test configs, and `resolv.conf` files.

**Where to put it**: New static method on a shared helper, or on `HostingTools` since it already has the `ExtractHoconBlock` logic. Given the single-project architecture guardrail, a static helper class `ConfigFiles` in the Tools namespace is appropriate.

### D2: Curated hosting→HOCON fallback mapping

**Decision**: Add a static dictionary of well-known hosting method → HOCON key mappings for methods where the regex scanner finds nothing. The scanner runs first; the fallback fills gaps.

```
WithClustering      → akka.cluster.*
WithRemoting        → akka.remote.*
WithDistributedData → akka.cluster.distributed-data.*
WithSingleton       → akka.cluster.singleton.*
WithShardRegion     → akka.cluster.sharding.*
```

**Alternative considered**: Parse Options classes to infer mappings — rejected as fragile and complex for the return. A curated map is easy to maintain and covers the 80% case.

### D3: Inherited member display in type_members

**Decision**: After extracting direct members, resolve the type's parent interfaces from the declaration line (e.g. `IActorRef : ICanTell, IEquatable<IActorRef>`), look them up via `FindType`, extract their members, and append them under a separate `Inherited from <ParentType>:` section.

Limit to one level of parent interfaces — no transitive resolution. This handles the `IActorRef` → `ICanTell` case without explosion.

**Alternative considered**: Just append a "See also: type_members(ICanTell)" hint — simpler but less useful. Since we already have the `FindType` + `Extract` machinery, actually including them is straightforward.

### D4: Broader attribute default extraction

**Decision**: Extend `FormatAttributes` to also look for `public static` / `internal static` members that return or construct the attribute class, and for `Instance` / `Default` named members regardless of the `readonly` modifier. The pattern:

```
public static readonly? <AttrClass> (Default|Instance|None)\w* = ...
```

Also check for parameterized constructor calls: `new <AttrClass>(...)` as inline defaults.

## Risks / Trade-offs

- **[Stale curated map]** → D2's fallback mapping can go stale when new hosting methods are added. Mitigation: map is small, easy to update, and the regex scanner still runs first for methods that do use literal keys.
- **[Config file noise]** → D1's broader enumeration might pick up test configs. Mitigation: exclude paths containing `/test/`, `/Test/`, `/example/`, `/Examples/`.
- **[Inherited member explosion]** → D3 could produce large output for types with many parents. Mitigation: limit to one level, skip system interfaces (`IEquatable`, `IComparable`, `ISurrogated`).
