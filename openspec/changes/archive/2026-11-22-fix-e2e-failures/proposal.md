## Why

The E2E test run (2026-11-22) scored 40/46 — 6 failures across 3 root causes. The highest-impact issue is that core Akka.NET config files (`akka.conf`, `Cluster.conf`, `Remote.conf`, `persistence.conf`) are invisible to tools that only glob for `reference.conf`. This breaks the HOCON lookup, serialization bindings, and the full migration workflow. The remaining failures are a regex gap in `hosting_to_hocon` and two minor extraction limitations.

## What Changes

- **Config file discovery**: Broaden config file enumeration in `HostingTools`, `EcosystemTools`, and `ConfigMigrationTools` to include well-known core config filenames beyond `reference.conf` — specifically `akka.conf`, `Cluster.conf`, `Remote.conf`, `persistence.conf`, and any `*.conf` under `Configuration/` directories.
- **hosting_to_hocon mapping**: `CollectHostingKeys` only finds HOCON keys written as string literals in method bodies. Methods like `WithClustering` that build config programmatically via Options objects produce no matches. Add a curated fallback mapping for well-known hosting methods that set config indirectly.
- **type_members inherited members**: `Extract()` only returns members declared directly in the type body. For interfaces like `IActorRef`, inherited members (e.g. `Tell` from `ICanTell`) are absent. Add parent interface members or an explicit "see also" note.
- **stream_attributes defaults**: `DefaultPattern` matches `public static readonly ... Default* = ...` but Akka.Streams attributes use sealed nested classes, not static fields. Broaden the default extraction pattern.

## Capabilities

### New Capabilities

- `config-file-discovery`: Enumerate all well-known Akka.NET HOCON config files (not just `reference.conf`) across cached source roots. Shared by config_reference, serializer_bindings, config_tree, config_diff, and hosting_to_hocon.
- `hosting-hocon-fallback-mapping`: Curated mapping table for hosting methods that set HOCON config programmatically (via Options objects) rather than through string literals.
- `inherited-member-display`: Show inherited interface members (or a "see also" hint) in type_members output.
- `attribute-default-extraction`: Extract default values from Akka.Streams attribute classes that use sealed nested class patterns.

### Modified Capabilities

(none — no existing spec-level requirements change)

## Impact

- **Files**: `HostingTools.cs`, `EcosystemTools.cs`, `ConfigMigrationTools.cs`, `TypeTools.cs`, `StreamPersistenceTools.cs`
- **Shared helper**: Config file enumeration is duplicated across tools — a shared helper should be extracted to avoid inconsistency.
- **Tests**: Existing unit tests for config parsing, serializer bindings, and type_members will need new test cases covering the core config files.
- **E2E**: Fixes S2.1, S2.8, S3.4, S3.9, S4.1 directly; S1.6 improved with inherited member display.
