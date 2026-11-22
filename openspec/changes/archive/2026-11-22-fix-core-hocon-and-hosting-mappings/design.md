## Context

Three config-related tools are broken because `ConfigFiles.EnumerateConfigFiles` doesn't find
reference.conf files in the core akka.net mono-repo. The filter logic requires either
`reference.conf` as filename (which works) AND for non-reference.conf files requires paths
under `src/core/`. The actual problem is more subtle: the reference.conf files ARE found by
filename match, but the `_excludeSegments` filter or path structure may exclude them.

Additionally, `hosting_to_hocon` has a logic bug: `CollectHostingKeys` scans for literal
`akka.*` strings in method bodies via regex, but many Hosting extension methods (like
`WithClustering`) build config programmatically without literal HOCON path strings. The
fallback map `_fallbackHostingMap` exists for exactly this case but is only consulted when
`found == 0` — after the entire regex scan fails. The fallback should be merged into results.

## Goals / Non-Goals

**Goals:**
- `config_reference("akka.actor.provider")` returns the core reference.conf block
- `config_reference("akka.cluster")` returns cluster config
- `hosting_to_hocon("WithClustering")` returns `akka.cluster` keys
- `serializer_bindings()` returns core serialization-bindings
- All config-related E2E tests pass (S2.1, S2.8, S3.9, S4.1)

**Non-Goals:**
- Adding new tools or new parameters
- Multi-version support (separate change)
- Restructuring tool architecture

## Decisions

### Decision 1: Fix ConfigFiles scanning by relaxing IsKnownConfigFile

**Choice**: Remove the `ContainsCorePrefix` guard on `reference.conf` — any `reference.conf`
anywhere under a source root (that isn't in test/example/obj/bin) should be indexed.

**Rationale**: The current code has two paths in `IsKnownConfigFile`:
1. `reference.conf` → returns true (should work for all repos)
2. Configuration/*.conf in `src/core/` → special handling for core split configs

Path (1) should catch everything, but we need to verify the actual directory structure after
extraction to understand why core reference.conf files aren't being found. The tarball
extraction creates a structure like `{version}/src/core/Akka/...`. The `EnumerateConfigFiles`
walks `SearchOption.AllDirectories` from the root, so reference.conf should be found.

**Investigation needed**: Run the tool with debug logging to see which files are found vs
filtered out. The bug may be in `_excludeSegments` matching something unexpected, or the
`IsExcluded` check hitting before `IsKnownConfigFile`.

**Alternative considered**: Adding core repo paths to a whitelist — rejected because
`reference.conf` matching by filename should be sufficient and repo-agnostic.

### Decision 2: Merge fallback map into hosting_to_hocon primary results

**Choice**: In `MapQuery`, always merge `_fallbackHostingMap` entries into the results from
`CollectHostingKeys`. When a method appears in both, union the key sets. Only label fallback
entries as `(curated)` when they don't overlap with regex-extracted keys.

**Rationale**: The current logic uses fallback as a last resort, but the fallback map contains
the most reliable mappings. Hosting methods like `WithClustering` don't contain literal
`akka.cluster` strings — they use `Config.Empty`, builder patterns, and programmatic
construction. The regex can't catch these, so the curated map should always contribute.

**Alternative considered**: Improving the regex to follow string constants and builder
patterns — rejected as too fragile and complex for diminishing returns.

### Decision 3: Expand the fallback hosting map

**Choice**: Add mappings for all commonly-used Hosting extension methods that don't use
literal HOCON paths:

```csharp
["WithClustering"] = ["akka.cluster"],
["WithRemoting"] = ["akka.remote"],
["WithDistributedData"] = ["akka.cluster.distributed-data"],
["WithSingleton"] = ["akka.cluster.singleton"],
["WithShardRegion"] = ["akka.cluster.sharding"],
["WithDistributedPubSub"] = ["akka.cluster.pub-sub"],
["WithJournal"] = ["akka.persistence.journal"],
["WithSnapshotStore"] = ["akka.persistence.snapshot-store"],
["WithCoordinatedShutdown"] = ["akka.coordinated-shutdown"],
```

**Rationale**: These are the methods most commonly looked up during HOCON→Hosting migration.
Extracting from source works for some (persistence methods often have literal HOCON strings),
but having them in the fallback ensures consistent results.

## Risks / Trade-offs

- **Config file volume**: Relaxing filters may surface test/example reference.conf files that
  the exclude list doesn't catch → Mitigated by existing `_excludeSegments` filter
- **Stale fallback map**: Curated HOCON mappings may drift from actual Akka.NET behavior →
  Mitigated by labeling curated entries; can be validated in E2E tests
- **Performance**: More reference.conf files to parse → Negligible, these are small text files
