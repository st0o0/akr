## 1. Create domain-grouped tool classes

- [x] 1.1 Create `TypeTools.cs`: merge LookupTypeTool + TypeMembersTool + TypeFinder + MemberExtractor into one partial class
- [x] 1.2 Create `HostingTools.cs`: merge HostingExtensionsTool + ConfigReferenceTool + ExtensionMethodFinder + HoconBlockExtractor
- [x] 1.3 Create `StreamTools.cs`: merge StreamStagesTool + SearchSourceTool + StageFinder

## 2. Delete old files

- [x] 2.1 Delete: LookupTypeTool.cs, TypeMembersTool.cs, TypeFinder.cs, MemberExtractor.cs
- [x] 2.2 Delete: HostingExtensionsTool.cs, ConfigReferenceTool.cs, ExtensionMethodFinder.cs, HoconBlockExtractor.cs
- [x] 2.3 Delete: StreamStagesTool.cs, SearchSourceTool.cs, StageFinder.cs

## 3. Update wiring and tests

- [x] 3.1 Update McpSetup: 3 WithTools registrations instead of 6
- [x] 3.2 Update McpServerTests: 3 WithTools registrations
- [x] 3.3 Update unit test imports (if helper classes were directly tested, fold into tool tests)
- [x] 3.4 Build, test, format, slopwatch
