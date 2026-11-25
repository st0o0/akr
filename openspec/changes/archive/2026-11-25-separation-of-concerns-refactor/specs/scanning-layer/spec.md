## ADDED Requirements

### Requirement: Analysis/ renamed to Scanning/
The `Akr.Analysis` namespace and folder SHALL be renamed to `Akr.Scanning`. All classes (TypeScanner, ProjectScanner, HoconParser, DocsIndex) SHALL use the `Akr.Scanning` namespace.

#### Scenario: No Akr.Analysis references remain
- **WHEN** the codebase is searched for `Akr.Analysis`
- **THEN** zero references SHALL be found

### Requirement: DocsIndex moves to Scanning/
`DocsIndex` SHALL move from `Akr.Analysis` to `Akr.Scanning`. It scans cached files for headings and code blocks — the same domain as TypeScanner and SourceSearcher.

#### Scenario: DocsIndex in Scanning namespace
- **WHEN** DocsIndex is constructed
- **THEN** it SHALL be in the `Akr.Scanning` namespace

### Requirement: SourceSearcher moves to Scanning/
`SourceSearcher` SHALL move from `Akr.Tools` to `Akr.Scanning`. It searches cached source files using regex — a scanning concern, not a tool.

#### Scenario: SourceSearcher in Scanning namespace
- **WHEN** SourceSearcher is constructed
- **THEN** it SHALL be in the `Akr.Scanning` namespace

### Requirement: FileService moves to root namespace
`FileService` SHALL move from `Akr.Tools` to `Akr` (root namespace). It is a shared infrastructure service used by Source/, Scanning/, and Tools/.

#### Scenario: FileService in root namespace
- **WHEN** FileService is used by any layer
- **THEN** it SHALL be referenced as `Akr.FileService` without requiring a `using Akr.Tools` statement
