## 1. config_reference tool

- [x] 1.1 Create `Tools/ConfigReferenceTool.cs` MCP tool with parameters: `path?` (optional HOCON path filter)
- [x] 1.2 Enumerate all `reference.conf` files across cached source roots
- [x] 1.3 When `path` given, extract the matching brace-delimited HOCON block
- [x] 1.4 Group output by source module (file path relative to root)
- [x] 1.5 Register in McpSetup
- [x] 1.6 Unit tests for HOCON block extraction logic
- [x] 1.7 MCP integration test with fake reference.conf

## 2. stream_stages tool

- [x] 2.1 Create `Tools/StreamStagesTool.cs` MCP tool with parameters: `filter?` (optional keyword)
- [x] 2.2 Scan `.cs` files in Akka.Streams source for GraphStage/stage base class declarations
- [x] 2.3 Classify by shape: Source, Flow, Sink, Fan-In, Fan-Out, Custom
- [x] 2.4 Optional keyword filter on stage name or category
- [x] 2.5 Register in McpSetup
- [x] 2.6 Unit tests for stage detection and shape classification
- [x] 2.7 MCP integration test with fake stream stage source

## 3. Verify

- [x] 3.1 Build, test, format, slopwatch
