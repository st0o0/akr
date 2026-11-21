# akr

## Project

akr (Akka Reference) - MCP server providing structured, token-efficient access to Akka.NET
source code for AI agents. Replaces ILSpy decompilation with source intelligence: fetches
Akka.NET source from GitHub (latest by default), caches locally, and exposes type lookups,
config references, stream stages, and more via MCP tools. Hosting-first: always suggests
`Akka.Hosting` APIs before HOCON config.

## Language

All code, specs, docs, and communication in English.

## Solution structure

```
src/
  akr.slnx
  Akr/                            # Everything: MCP server, tools, GitHub client, registry
  Akr.Tests/                      # Unit tests (xUnit v3)
```

## Architecture guardrails

- **Single project.** Everything in one project — no unnecessary abstraction layers.
  `GitHubClient` handles fetching/caching, tools call it directly.
- **Hosting-first.** Tools and responses always prefer `Akka.Hosting` APIs over HOCON.
  HOCON is the fallback, not the default.
- **Token-efficient.** Return signatures and summaries by default, full source on demand.
- **Latest-by-default.** No config needed — server resolves latest Akka versions automatically.
  `AKKA_VERSIONS` env var is optional for pinning specific versions.
- **Native AOT.** The server publishes as a small native binary. No reflection, no
  `RegexOptions.Compiled`, explicit MCP tool registration.

## Build & test

All commands run from repo root (never cd into src/):

```powershell
dotnet build src/akr.slnx
dotnet format src/akr.slnx --verify-no-changes
```

Tests are xUnit v3 on Microsoft.Testing.Platform - `dotnet run`, **not** `dotnet test`:

```powershell
dotnet run --project src/Akr.Tests/Akr.Tests.csproj
```

**NSubstitute** for mocking plain interfaces. **Verify** for snapshot testing.

## Versioning

Version 0.x - breaking changes are fine without migration code or compatibility shims.
Managed by release-please; never edit `<Version>` in `src/Directory.Build.props`.

## Conventions

- Central package management: versions only in `src/Directory.Packages.props`; use `dotnet add package`.
- `TreatWarningsAsErrors` is on. Never add `<NoWarn>` or `#pragma warning disable` to silence warnings.
- `EnforceCodeStyleInBuild` is on. Follow `.editorconfig`.
- Target framework: net10.0.
- **Slopwatch** runs as a PostToolUse hook on every edit. Zero tolerance for new slop
  (disabled tests, empty catches, warning suppression, arbitrary delays, CPM bypass).
