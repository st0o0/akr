## 1. Diagnose config file discovery

- [x] 1.1 Add debug logging to ConfigFiles.EnumerateConfigFiles to trace which files are found/excluded in the core akka.net repo — verify whether reference.conf files are being found but their HOCON content doesn't match the path query, or if they're not being enumerated at all
- [x] 1.2 Check the extracted tarball directory structure under ~/.akr/sources/akka.net/ to confirm reference.conf locations vs Configuration/*.conf locations

## 2. Fix ConfigFiles scanning

- [x] 2.1 Fix `ConfigFiles.IsKnownConfigFile` to ensure all reference.conf files pass (verify the filename check works across all repo layouts) and that Configuration/*.conf files from the core mono-repo are included regardless of the `ContainsCorePrefix` path check
- [x] 2.2 Add unit tests: enumerate config files from a mock directory structure that mirrors the core akka.net repo layout, verify reference.conf and Configuration/*.conf are found

## 3. Fix hosting_to_hocon mapping

- [x] 3.1 Modify `ConfigMigrationTools.MapQuery` to always merge `_fallbackHostingMap` entries into regex-scanned results, labeling curated-only entries with `(curated)`
- [x] 3.2 Expand `_fallbackHostingMap` with WithJournal, WithSnapshotStore, WithCoordinatedShutdown mappings
- [x] 3.3 Add unit tests: verify WithClustering returns akka.cluster, verify unknown methods return no-match, verify curated+regex results are merged

## 4. Verify fixes

- [x] 4.1 Run `dotnet build src/akr.slnx` and `dotnet format src/akr.slnx --verify-no-changes`
- [x] 4.2 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [x] 4.3 Re-run E2E tests for S2.1 (config_reference akka.actor.provider), S2.8 (hosting_to_hocon WithClustering), S3.9 (serializer_bindings), S4.1 (HOCON migration workflow) — all pass
