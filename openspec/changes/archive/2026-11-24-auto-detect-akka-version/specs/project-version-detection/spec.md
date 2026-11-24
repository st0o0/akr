## ADDED Requirements

### Requirement: Scan Directory.Packages.props for Akka package versions

The system SHALL scan `Directory.Packages.props` files for `<PackageVersion>` elements with `Include` attributes matching Akka.NET package names from the repo registry. The `Version` attribute value SHALL be extracted as the detected version.

#### Scenario: CPM project with Akka packages

- **WHEN** scanning a directory containing `Directory.Packages.props` with `<PackageVersion Include="Akka" Version="1.5.75" />`
- **THEN** the system SHALL detect version `1.5.75` for the `Akka` package

#### Scenario: CPM project with multiple Akka packages

- **WHEN** scanning a directory containing `Directory.Packages.props` with entries for `Akka`, `Akka.Hosting`, and `Akka.Remote`
- **THEN** the system SHALL detect versions for all matched packages

#### Scenario: CPM project with no Akka packages

- **WHEN** scanning a directory containing `Directory.Packages.props` with no Akka-related entries
- **THEN** the system SHALL return no detected versions

### Requirement: Scan csproj files for inline PackageReference versions

The system SHALL scan `*.csproj` files for `<PackageReference>` elements with both an `Include` attribute matching Akka.NET package names and a `Version` attribute. PackageReference elements without a `Version` attribute (CPM-managed) SHALL be ignored.

#### Scenario: Inline PackageReference with version

- **WHEN** scanning a directory containing `MyApp.csproj` with `<PackageReference Include="Akka" Version="1.5.75" />`
- **THEN** the system SHALL detect version `1.5.75` for the `Akka` package

#### Scenario: PackageReference without version (CPM-managed)

- **WHEN** scanning a directory containing a `.csproj` with `<PackageReference Include="Akka" />` (no Version attribute)
- **THEN** the system SHALL NOT extract a version from that element

#### Scenario: Multiple csproj files with same package

- **WHEN** scanning a directory containing multiple `.csproj` files referencing `Akka` with different versions
- **THEN** the system SHALL use the first version found

### Requirement: Scan Directory.Build.props for PackageReference versions

The system SHALL scan `Directory.Build.props` files for `<PackageReference>` elements with Akka.NET package names, using the same extraction logic as csproj scanning.

#### Scenario: Shared PackageReference in Directory.Build.props

- **WHEN** scanning a directory containing `Directory.Build.props` with `<PackageReference Include="Akka" Version="1.5.75" />`
- **THEN** the system SHALL detect version `1.5.75` for the `Akka` package

### Requirement: Walk up directory tree to find project files

The system SHALL search for project files starting from the given directory and walking up parent directories. The search SHALL stop at the first directory containing any of: `Directory.Packages.props`, `Directory.Build.props`, or `*.csproj` files. The search order within a directory SHALL be: `Directory.Packages.props` first, then `Directory.Build.props`, then `*.csproj`.

#### Scenario: Project files in current directory

- **WHEN** scanning from `/repo/src/MyApp/` and `Directory.Packages.props` exists in `/repo/src/MyApp/`
- **THEN** the system SHALL scan that file and NOT walk further up

#### Scenario: Project files in parent directory

- **WHEN** scanning from `/repo/src/MyApp/Controllers/` and no project files exist there
- **AND** `Directory.Packages.props` exists in `/repo/src/`
- **THEN** the system SHALL find and scan the file in `/repo/src/`

#### Scenario: No project files found

- **WHEN** scanning from a directory with no .NET project files in any ancestor
- **THEN** the system SHALL return no detected versions

#### Scenario: Filesystem root reached

- **WHEN** walking up from a directory and reaching the filesystem root without finding project files
- **THEN** the system SHALL return no detected versions

### Requirement: Use core Akka package as reference version

The system SHALL use the version of the `Akka` core package as the primary reference version for fetching source from the `akkadotnet/akka.net` repository. Other detected package versions SHALL be used for their respective repositories when available.

#### Scenario: Core Akka package found

- **WHEN** project files contain `Akka` version `1.5.75` and `Akka.Management` version `1.5.10`
- **THEN** the system SHALL use `1.5.75` for the `akkadotnet/akka.net` repo
- **AND** SHALL use `1.5.10` for the `akkadotnet/Akka.Management` repo

#### Scenario: Only satellite packages found

- **WHEN** project files contain `Akka.Hosting` version `1.5.71` but no core `Akka` package
- **THEN** the system SHALL use `1.5.71` for the `akkadotnet/akka.net` repo (same repo as Akka.Hosting)

### Requirement: Cache scan results

The system SHALL cache the scan results after the first scan for a given path. Subsequent requests for the same path SHALL return cached results without re-reading the filesystem.

#### Scenario: Repeated scan of same path

- **WHEN** `ProjectScanner.ScanAsync` is called twice with the same starting path
- **THEN** the second call SHALL return cached results without filesystem access

#### Scenario: Scan of different path

- **WHEN** `ProjectScanner.ScanAsync` is called with path A, then path B
- **THEN** each SHALL perform independent filesystem scans
