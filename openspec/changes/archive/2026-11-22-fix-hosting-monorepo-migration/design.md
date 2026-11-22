## Context

Akka.Hosting was merged into the akkadotnet/akka.net monorepo under `src/contrib/hosting/`
and the standalone repo was archived on Nov 13, 2026. The last standalone release was 1.5.71
(Aug 2026). Since then, Hosting packages ship with the monorepo releases (1.5.72+) under a
single version tag. Additionally, Akka.Reminders was added as a new monorepo module in 1.5.75
under `src/contrib/reminders/`.

Our `RepoRegistry.cs` is a static `FrozenDictionary` mapping package names to `(repo, path)`
entries. The `GitHubClient` resolves versions per-repo via `/releases/latest` and downloads
source archives by tag. Since Hosting now shares the monorepo's tag, no changes to version
resolution or archive fetching are needed — only the registry data.

## Goals / Non-Goals

**Goals:**
- Correct Hosting package entries to point to the monorepo with updated paths
- Add new packages that shipped since the merge (Embedded.Hosting, TestKit.Xunit2, Reminders)
- Update the repo-registry spec to reflect 4 ecosystem repos instead of 5
- Keep tests green

**Non-Goals:**
- Dynamic registry or config-driven package mapping (premature for v0.x)
- Handling future monorepo migrations of Management/Persistence.Sql/Persistence.MongoDB
- Changes to GitHubClient, version resolution, or caching logic

## Decisions

**Data-only change in RepoRegistry.cs**: Update the dictionary literal entries. No structural
changes. The `FrozenDictionary` and `PackageEntry` types are unchanged.

**Monorepo path convention**: Hosting packages moved from `src/<PackageName>` (old repo layout)
to `src/contrib/hosting/<PackageName>`. Reminders are at `src/contrib/reminders/<PackageName>`.

**Reminders scope**: Include the 5 shipping packages (Akka.Reminders, .Sql, .SqlServer,
.PostgreSql, .Sqlite). Exclude test/benchmark/migration projects — those aren't NuGet packages.

**Ecosystem repo count**: Drops from 5 to 4. `akkadotnet/Akka.Hosting` is removed.
The 4 repos: akka.net, Akka.Management, Akka.Persistence.Sql, Akka.Persistence.MongoDB.

## Risks / Trade-offs

[Monorepo archive size is larger] → Acceptable. The archive is downloaded once per version
and cached. The monorepo tarball is bigger than the old Hosting-only tarball, but Hosting
entries already share the monorepo download with core packages — no additional fetch.

[Other repos may migrate next] → Out of scope. When Management or Persistence repos move
to the monorepo, we'll do the same kind of registry update. No need to pre-engineer.
