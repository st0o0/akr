## Context

Akr has 4 tools. The remaining Phase 1 tools are config lookup and stream stage catalog —
both are text-search based, no Roslyn needed.

## Goals / Non-Goals

**Goals:**
- `config_reference` returns HOCON config sections from `reference.conf` files
- `stream_stages` returns a catalog of built-in Akka.Streams stages
- Both AOT-safe, regex-based

**Non-Goals:**
- HOCON parsing into a structured tree (just return the raw config text)
- Stream stage documentation or examples (just names + shapes)

## Decisions

### 1. config_reference: search reference.conf files

Akka.NET ships `reference.conf` embedded in each module. In the source tree they live as
plain files (e.g. `src/core/Akka/reference.conf`, `src/core/Akka.Remote/reference.conf`).

The tool:
1. Enumerates all `reference.conf` files across cached source roots
2. If a `path` filter is given (e.g. `akka.persistence`), finds the HOCON block matching
   that path by searching for the key and extracting the brace-delimited block
3. If no filter, returns the full content of all `reference.conf` files, labeled by module
4. Output is grouped by source file path

Block extraction: find the line containing the path key, then read lines tracking brace
depth until the block closes. This handles nested HOCON without a full parser.

### 2. stream_stages: scan for stage classes

Akka.Streams stages are classes that extend base types like `GraphStage<T>`,
`SimpleLinearGraphStage<T>`, or implement specific shapes. They live in
`src/core/Akka.Streams/Dsl/` and `src/core/Akka.Streams/`.

The tool:
1. Searches `.cs` files in Akka.Streams source for class declarations
2. Identifies stage type by inheritance: `GraphStage`, `SimpleLinearGraphStage`,
   `ISourceStage`, `IFlowStage`, `ISinkStage`, or shape in generic parameter
3. Classifies as Source, Flow, Sink, Fan-In, or Fan-Out based on shape type
4. Returns catalog: `[Shape] ClassName` with optional keyword filter
5. Groups by shape category

Shape detection: parse the base class generic parameter for `SourceShape`, `FlowShape`,
`SinkShape`, `UniformFanInShape`, `UniformFanOutShape`, `FanInShape`, `FanOutShape`.
