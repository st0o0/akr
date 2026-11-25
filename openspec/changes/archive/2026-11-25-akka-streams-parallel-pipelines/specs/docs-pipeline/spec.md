## ADDED Requirements

### Requirement: DocsPipeline for parallel index building
The system SHALL provide a `DocsPipeline` class in `Pipelines/` that builds the docs index using parallel markdown parsing via Akka.Streams. It SHALL accept `DocsIndex`, `FileService`, and `ActorSystem` via constructor injection and be registered as a singleton in DI.

#### Scenario: Singleton registration
- **WHEN** the DI container is built
- **THEN** `DocsPipeline` SHALL be resolvable as a singleton

### Requirement: Parallel markdown parsing
`DocsPipeline.BuildIndexAsync(string docsRoot, CancellationToken)` SHALL scan markdown files and parse them in parallel using `Source.From(mdFiles).SelectAsync(4, ParseMarkdown).RunWith(Sink.Seq, actorSystem)`. The parsed entries SHALL be added to `DocsIndex`.

#### Scenario: Parallel parsing of markdown files
- **WHEN** `BuildIndexAsync` is called with a docs root containing 50 markdown files
- **THEN** up to 4 files SHALL be parsed concurrently

#### Scenario: DocsIndex populated after pipeline completes
- **WHEN** `BuildIndexAsync` completes
- **THEN** `DocsIndex.Search` SHALL return results for topics found in the parsed markdown files

### Requirement: DocsIndex becomes pure data structure
`DocsIndex.Build(string root)` synchronous method SHALL be replaced by pipeline-driven population. `DocsIndex` SHALL retain its search and example-finding methods but SHALL NOT perform file I/O itself.

#### Scenario: DocsIndex has no file I/O dependency
- **WHEN** `DocsIndex` is constructed
- **THEN** it SHALL not require a `FileService` for construction — file scanning is done by `DocsPipeline`

### Requirement: DocsTools routes through DocsPipeline
`DocsTools` methods that currently call `DocsIndex.Build` SHALL be updated to call `DocsPipeline.BuildIndexAsync` instead. The pipeline SHALL handle docs fetching via `GitHubClient.EnsureDocsAsync` before building the index.

#### Scenario: docs_search triggers pipeline
- **WHEN** `docs_search("cluster sharding")` is called
- **THEN** `DocsPipeline` SHALL ensure docs are fetched and index is built before searching
