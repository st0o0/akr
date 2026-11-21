## Context

Research of GitHub MCP, Azure MCP, Excel MCP, and SDK samples shows that the dominant
pattern is domain-grouped tools: one `[McpServerToolType]` class per domain with multiple
`[McpServerTool]` methods, sharing private helper logic.

## Goals / Non-Goals

**Goals:**
- 3 domain-grouped tool classes instead of 6 individual ones
- Helper logic as private methods, not separate files
- Same MCP tool names, parameters, and behavior

**Non-Goals:**
- Changing tool behavior or output format
- Adding a service layer (overkill for 6 tools)

## Decisions

### 1. Domain grouping

| Class | Tools | Private helpers |
|-------|-------|-----------------|
| `TypeTools` | `lookup_type`, `type_members` | type finding, member extraction, namespace extraction |
| `HostingTools` | `hosting_extensions`, `config_reference` | extension method finding, HOCON block extraction |
| `StreamTools` | `search_source`, `stream_stages` | stage finding, stage classification |

### 2. SourceSearcher stays shared

`SourceSearcher.Search()` is the core grep utility used by `search_source` directly and
potentially by other tools for ad-hoc searches. It stays as a standalone static class.

### 3. Records stay as separate files

`SearchResult` and `TypeLookupResult` are return types  -  small files that are fine standalone.

### 4. Tool classes are partial for GeneratedRegex

TypeTools and StreamTools use `[GeneratedRegex]` attributes, so their classes must be `partial`.
