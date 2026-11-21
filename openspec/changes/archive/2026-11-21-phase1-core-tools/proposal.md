## Why

`lookup_type` returns full files or rough summaries  -  agents typically want just the member
signatures. `type_members` saves ~700 tokens per call by returning structured, compact output.
`hosting_extensions` is the second most requested lookup pattern  -  agents constantly need to
know which `With*`/`Add*` extension methods exist for configuring Akka.NET via Hosting API.

## What Changes

- **`type_members` tool**: Returns compact member signatures for a C# type  -  properties, methods,
  constructors, events, grouped and formatted. No method bodies, no attributes noise. Builds on
  existing `TypeFinder` infrastructure.
- **`hosting_extensions` tool**: Searches Akka.Hosting source for `With*`/`Add*` extension methods,
  returns signatures grouped by feature area (Persistence, Clustering, Remote, etc.). Optionally
  filtered by keyword.
- **`MemberExtractor`**: New utility in `Tools/` that parses a C# source file and extracts
  structured member information (name, return type, parameters, access modifier, kind).

## Capabilities

### New Capabilities

- `type-members-tool`: MCP tool for compact member signature extraction from Akka.NET types.
- `hosting-extensions-tool`: MCP tool for searching Akka.Hosting extension methods.

### Modified Capabilities

(none)

## Impact

- **New files**: `MemberExtractor.cs`, `TypeMembersTool.cs`, `HostingExtensionsTool.cs` in `Akr/Tools/`
- **Modified**: `McpSetup.cs` (register 2 new tools)
- **New tests**: MCP integration tests + unit tests for MemberExtractor
- **No breaking changes**: additive, existing tools unchanged
