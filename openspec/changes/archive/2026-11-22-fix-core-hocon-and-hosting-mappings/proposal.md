## Why

The HOCON→Hosting migration workflow is broken end-to-end. `config_reference` cannot resolve
core Akka paths like `akka.actor.provider` or `akka.cluster` because it only indexes satellite
repos (Persistence.Sql, MongoDb, Hosting) — not the core `akka.net` mono-repo's reference.conf
files. `hosting_to_hocon` fails to find any HOCON keys for key methods like `WithClustering`
because its regex-based extraction misses HOCON key strings that aren't literal `akka.*` patterns
in the method body — it only finds them in the fallback map, which isn't consulted when the
primary scan yields nothing of interest. `serializer_bindings` returns empty because core
serialization-bindings live in the core repo's reference.conf, which isn't being scanned.

Analysis of 48 ILSpy decompilation calls across FunkArr and Njord sessions shows these tools
(config lookup, hosting-to-HOCON mapping) are exactly what users fall back to ILSpy for.

## What Changes

- **config_reference / config_tree / config_diff**: Fix `ConfigFiles.EnumerateConfigFiles` to
  find reference.conf files in ALL cached repos including the core akka.net mono-repo (the
  `ContainsCorePrefix` guard is too restrictive for non-core-prefixed paths)
- **hosting_to_hocon**: Fix `CollectHostingKeys` to also scan the fallback map when the regex
  scan finds no results, and expand the fallback map to cover common Hosting methods
- **serializer_bindings**: Fix by ensuring core reference.conf files are scanned (same root
  cause as config_reference)

## Capabilities

### New Capabilities

(none — these are fixes to existing tools)

### Modified Capabilities

(none — no spec-level behavior changes, just bug fixes in config file scanning)

## Impact

- `ConfigFiles.cs`: Relaxing the `IsKnownConfigFile` / `ContainsCorePrefix` filter
- `ConfigMigrationTools.cs`: `MapQuery` and `CollectHostingKeys` — integrating fallback map
  into primary results, not just as a last resort when nothing else matches
- `EcosystemTools.cs`: `FindSerializerBindings` — benefits from ConfigFiles fix
- `HostingTools.cs`: `FindConfig` — benefits from ConfigFiles fix
- All config-related E2E tests (S2.1, S2.8, S3.9, S4.1) should turn green
