# scanning-layer Specification

## Purpose
TBD - created by archiving change separation-of-concerns-refactor. Update Purpose after archive.
## Requirements
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

### Requirement: Project context initialization
`ProjectScanner` SHALL own its initialization lifecycle. There SHALL be no separate `ProjectContextInitializer` class. Initialization SHALL happen during DI singleton factory registration via an `Initialize(string? projectPath)` method.

#### Scenario: Startup with AKR_PROJECT_PATH set
- **WHEN** the server starts with `AKR_PROJECT_PATH` environment variable set
- **THEN** `ProjectScanner` scans that path during DI setup and `Current` is populated before any tool handles a request

#### Scenario: Startup without AKR_PROJECT_PATH
- **WHEN** the server starts without `AKR_PROJECT_PATH`
- **THEN** `ProjectScanner` scans `Environment.CurrentDirectory` during DI setup

#### Scenario: set_project_context tool overrides
- **WHEN** an MCP client calls `set_project_context` with a new path
- **THEN** `ProjectScanner` rescans and updates `Current`

### Requirement: MapToRepoVersions is a static method on RepoRegistry
`MapToRepoVersions` SHALL be a `public static` method on `RepoRegistry`, not an instance method on `ProjectScanner`. It uses only `RepoRegistry.GetEntry()`.

#### Scenario: Mapping detected packages to repo versions
- **WHEN** a scan result contains Akka.NET package references
- **THEN** `RepoRegistry.MapToRepoVersions(packages)` returns the corresponding repo-version mapping

### Requirement: Scan methods are deduplicated
`ScanPackagesProps` and `ScanPackageReferences` SHALL be merged into a single `ScanPackageElements(string filePath, string elementName)` method.

#### Scenario: Scanning Directory.Packages.props
- **WHEN** scanning a `Directory.Packages.props` file
- **THEN** the shared method is called with element name `"PackageVersion"`

#### Scenario: Scanning csproj PackageReferences
- **WHEN** scanning a `.csproj` file
- **THEN** the shared method is called with element name `"PackageReference"`

### Requirement: File name constants
Hardcoded file names (`"Directory.Packages.props"`, `"Directory.Build.props"`) SHALL be `private const string` fields.

#### Scenario: File name used in scan and walk-up
- **WHEN** `ProjectScanner` looks for package files
- **THEN** it references the constant, not a string literal

