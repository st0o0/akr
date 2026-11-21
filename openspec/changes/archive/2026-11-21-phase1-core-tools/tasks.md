## 1. MemberExtractor

- [x] 1.1 Create `Tools/MemberExtractor.cs` with regex-based member signature extraction
- [x] 1.2 Extract properties, methods, constructors, events as structured data
- [x] 1.3 Format output grouped by kind (Properties, Methods, Constructors, Events)
- [x] 1.4 Unit tests for MemberExtractor with representative Akka-style source

## 2. type_members tool

- [x] 2.1 Create `Tools/TypeMembersTool.cs` MCP tool
- [x] 2.2 Use TypeFinder to locate type, MemberExtractor to parse members
- [x] 2.3 Merge members from partial classes
- [x] 2.4 Include type declaration with base types in output header
- [x] 2.5 Register in McpSetup
- [x] 2.6 MCP integration test: type_members returns structured output

## 3. hosting_extensions tool

- [x] 3.1 Create `Tools/HostingExtensionsTool.cs` MCP tool
- [x] 3.2 Search for `public static` methods named `With*`/`Add*` in Hosting repos
- [x] 3.3 Group results by source file path
- [x] 3.4 Optional filter parameter for keyword matching on file path
- [x] 3.5 Register in McpSetup
- [x] 3.6 MCP integration test: hosting_extensions lists extensions

## 4. Verify

- [x] 4.1 Build, test, format, slopwatch
