## Why

Agents constantly look up HOCON config defaults by decompiling `reference.conf` files or
searching docs — ~1500 tokens per lookup. `config_reference` returns the relevant config
section directly from source. Stream stage discovery requires decompiling multiple Akka.Streams
namespaces (~3000 tokens) — `stream_stages` returns a compact catalog.

## What Changes

- **`config_reference` tool**: Searches `reference.conf` files in cached Akka source, returns
  HOCON config blocks with defaults. Optionally filtered by path prefix (e.g. `akka.persistence`,
  `akka.remote`). Uses existing `SourceSearcher`-style file enumeration with `*.conf` pattern.
- **`stream_stages` tool**: Scans Akka.Streams source for stage classes (inheriting from
  `GraphStage`, `SimpleLinearGraphStage`, source/flow/sink base types), returns a catalog of
  stage names with their shape (Source/Flow/Sink/Fan-In/Fan-Out). Optionally filtered by keyword.

## Capabilities

### New Capabilities

- `config-reference-tool`: MCP tool for HOCON config lookup from `reference.conf` files.
- `stream-stages-tool`: MCP tool for Akka.Streams built-in stage catalog.

### Modified Capabilities

(none)

## Impact

- **New files**: `ConfigReferenceTool.cs`, `StreamStagesTool.cs` in `Akr/Tools/`
- **Modified**: `McpSetup.cs` (register 2 new tools)
- **New tests**: MCP integration tests + unit tests
- **No breaking changes**: additive
