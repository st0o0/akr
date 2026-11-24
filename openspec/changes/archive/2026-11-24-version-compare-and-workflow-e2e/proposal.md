## Why

Analysis of 48 real ILSpy decompilation calls across FunkArr and Njord sessions reveals two
things: (1) users regularly compare Akka.NET behavior across versions (Njord decompiled across
12 versions from 1.5.0 to 1.5.75), and akr only supports latest — there's no way to look up
a specific version or diff two versions; (2) the current E2E tests validate tools in isolation
("call each tool once") but don't test realistic multi-tool workflows that match how developers
actually use the tools together.

## What Changes

- **Version pinning**: Allow tools to target a specific Akka.NET version, not just latest.
  `GitHubClient` already supports fetching any version — the missing piece is a tool parameter
  to specify it and the ability to cache multiple versions simultaneously.
- **Version diff**: New `source_diff` tool that shows what changed in a type or config between
  two versions (leveraging the existing `config_diff` stub that currently only lists keys).
- **E2E test restructure**: Replace isolated tool-call tests with workflow-based scenarios
  derived from real session patterns: "build a custom stream stage", "migrate HOCON to Hosting",
  "debug actor behavior".

## Capabilities

### New Capabilities

- `version-pinning`: Target specific Akka.NET versions in tool calls, cache multiple versions
- `source-diff`: Compare types, configs, or search results between two Akka.NET versions
- `workflow-e2e-tests`: Session-based E2E test scenarios that validate multi-tool workflows

### Modified Capabilities

- `source-cleanup`: Change cleanup behavior to retain pinned versions, not just latest
- `cache-metadata`: Track metadata for multiple cached versions per repo

## Impact

- `GitHubClient.cs`: Multi-version caching, skip cleanup of pinned versions
- `RepoMetadata`: Track multiple versions per repo
- All tool methods: Add optional `version` parameter
- New tool: `source_diff` in a new or existing tool file
- `e2e/` skill and test plan: Complete restructure around workflow scenarios
- Cache storage: ~/.akr/sources may hold multiple version directories per repo
