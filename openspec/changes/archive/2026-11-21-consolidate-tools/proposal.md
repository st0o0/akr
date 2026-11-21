## Why

Tools/ has 14 files — 6 tool classes + 5 standalone helpers + 3 records. Each tool has its
own helper class (TypeFinder, MemberExtractor, ExtensionMethodFinder, HoconBlockExtractor,
StageFinder), but each helper is used by exactly one tool. Following the GitHub MCP pattern
(domain-grouped tools), related tools share a class and helpers become private methods.

## What Changes

- **TypeTools.cs**: Merge `LookupTypeTool` + `TypeMembersTool` + `TypeFinder` + `MemberExtractor`
  into one `[McpServerToolType]` class with `lookup_type` and `type_members` as methods.
  Type finding and member extraction become private methods.
- **HostingTools.cs**: Merge `HostingExtensionsTool` + `ConfigReferenceTool` +
  `ExtensionMethodFinder` + `HoconBlockExtractor` into one class with `hosting_extensions`
  and `config_reference` as methods.
- **StreamTools.cs**: Merge `StreamStagesTool` + `SearchSourceTool` + `StageFinder` into one
  class with `stream_stages` and `search_source` as methods.
- **Keep**: `SourceSearcher.cs` (shared by multiple tools), `SearchResult.cs`,
  `TypeLookupResult.cs` (records used in return types)
- **Delete**: 9 files (6 old tool classes + TypeFinder, MemberExtractor, ExtensionMethodFinder,
  HoconBlockExtractor, StageFinder)
- **Update**: McpSetup (3 registrations instead of 6), tests (update namespaces/imports)

## Capabilities

### Modified Capabilities

- `search-source-tool`: Tool method moves into StreamTools class. Behavior unchanged.
- `lookup-type-tool`: Tool method moves into TypeTools class. Behavior unchanged.
- `type-members-tool`: Tool method moves into TypeTools class. Behavior unchanged.
- `hosting-extensions-tool`: Tool method moves into HostingTools class. Behavior unchanged.
- `config-reference-tool`: Tool method moves into HostingTools class. Behavior unchanged.
- `stream-stages-tool`: Tool method moves into StreamTools class. Behavior unchanged.

## Impact

- **Net files**: 14 → 6 in Tools/ (3 tool classes + SourceSearcher + 2 records)
- **McpSetup**: `.WithTools<TypeTools>().WithTools<HostingTools>().WithTools<StreamTools>()`
- **Tests**: imports change, test logic unchanged
- **MCP interface**: zero changes — tool names and parameters stay identical
