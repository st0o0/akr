## Why

Akka.Hosting was archived on Nov 13, 2026 and merged into the akkadotnet/akka.net monorepo
under `src/contrib/hosting/`. Our `RepoRegistry.cs` still points to the archived repo, so
Hosting tools serve stale source (1.5.71 from Aug 2026) while core is at 1.5.75. The archived
repo will never get new releases. Additionally, new packages shipped since the merge
(Akka.Reminders, Akka.Persistence.Embedded.Hosting) are missing entirely.

## What Changes

- Update 5 Hosting package entries: repo from `akkadotnet/Akka.Hosting` to `akkadotnet/akka.net`,
  paths from `src/Akka.*` to `src/contrib/hosting/Akka.*`
- Add new Hosting packages: `Akka.Persistence.Embedded.Hosting`, `Akka.Hosting.TestKit.Xunit2`
- Add Akka.Reminders packages (new monorepo module since 1.5.75)
- Update `repo-registry` spec to reflect 4 ecosystem repos instead of 5
- Update tests to match

## Capabilities

### New Capabilities

(none — this is a data correction, not new functionality)

### Modified Capabilities

- `repo-registry`: Package-to-repo mappings change — Hosting moves to monorepo, new packages
  added, ecosystem repo count drops from 5 to 4

## Impact

- `src/Akr/Registry/RepoRegistry.cs` — entry data changes
- `src/Akr.Tests/Registry/RepoRegistryTests.cs` — test expectations
- `openspec/specs/repo-registry/spec.md` — spec scenarios updated
- No API changes, no breaking changes for consumers
- GitHubClient and version resolution are unaffected (tag format already compatible)
