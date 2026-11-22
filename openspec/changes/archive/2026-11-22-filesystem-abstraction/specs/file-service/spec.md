## ADDED Requirements

### Requirement: FileService provides unified filesystem access

The system SHALL provide a `FileService` class registered as a singleton in DI that wraps `IFileSystem` from `System.IO.Abstractions`. All filesystem operations in the codebase SHALL go through `FileService`.

#### Scenario: Read file text

- **WHEN** `ReadAllText(path)` is called
- **THEN** it SHALL return the full text content of the file at the given path

#### Scenario: Read file lines

- **WHEN** `ReadAllLines(path)` is called
- **THEN** it SHALL return all lines of the file as a string array

#### Scenario: Read file bytes

- **WHEN** `ReadAllBytes(path)` is called
- **THEN** it SHALL return the raw bytes of the file

#### Scenario: Atomic write

- **WHEN** `WriteAtomic(path, bytes)` is called
- **THEN** it SHALL write bytes to a temporary file (path + ".tmp")
- **AND** move the temporary file to the target path with overwrite
- **AND** create the parent directory if it does not exist

#### Scenario: File exists check

- **WHEN** `FileExists(path)` is called
- **THEN** it SHALL return true if the file exists, false otherwise

#### Scenario: Directory exists check

- **WHEN** `DirectoryExists(path)` is called
- **THEN** it SHALL return true if the directory exists, false otherwise

#### Scenario: Create directory

- **WHEN** `CreateDirectory(path)` is called
- **THEN** it SHALL create the directory and all parent directories

#### Scenario: Delete directory

- **WHEN** `DeleteDirectory(path, recursive)` is called
- **THEN** it SHALL delete the directory, recursively if requested

#### Scenario: Move directory

- **WHEN** `MoveDirectory(source, destination)` is called
- **THEN** it SHALL move the directory from source to destination

### Requirement: FileService provides file enumeration with exclusions

`FileService.EnumerateFiles` SHALL enumerate files across multiple source roots, excluding build output directories, with POSIX-style relative paths.

#### Scenario: Enumerate with glob pattern

- **WHEN** `EnumerateFiles(roots, pattern)` is called with roots and a file pattern
- **THEN** it SHALL return tuples of `(FullPath, RelativePath)` for all matching files
- **AND** relative paths SHALL use forward slashes

#### Scenario: Exclude build output

- **WHEN** enumerating files
- **THEN** files under `/obj/` or `/bin/` directories SHALL be excluded

#### Scenario: Skip missing roots

- **WHEN** a root path does not exist
- **THEN** it SHALL be skipped without error

### Requirement: FileService provides config file discovery

`FileService.EnumerateConfigFiles` SHALL enumerate Akka.NET config files (reference.conf and known config patterns), excluding test directories.

#### Scenario: Discover reference.conf files

- **WHEN** `EnumerateConfigFiles(roots)` is called
- **THEN** it SHALL return all `reference.conf` files outside test/example directories

#### Scenario: Exclude test directories

- **WHEN** enumerating config files
- **THEN** files under `/test/`, `/tests/`, `.Tests/`, `.TestKit/`, `/example/`, `/obj/`, `/bin/` SHALL be excluded

### Requirement: FileService provides cross-platform path utilities

`FileService` SHALL provide path manipulation methods that ensure consistent cross-platform behavior.

#### Scenario: Relative POSIX path

- **WHEN** `GetRelativePosixPath(root, fullPath)` is called
- **THEN** it SHALL return the relative path with forward slashes regardless of OS

#### Scenario: Path combine

- **WHEN** `Combine(paths)` is called
- **THEN** it SHALL combine path segments using the OS-appropriate separator

#### Scenario: Get full path

- **WHEN** `GetFullPath(path)` is called
- **THEN** it SHALL return the absolute path

### Requirement: FileService provides HOCON comment stripping

`FileService.StripHoconComment` SHALL strip HOCON-style comments from a line, respecting string literals.

#### Scenario: Strip hash comment

- **WHEN** `StripHoconComment(line)` is called with `#` outside a string
- **THEN** it SHALL return the portion before the `#`

#### Scenario: Strip double-slash comment

- **WHEN** `StripHoconComment(line)` is called with `//` outside a string
- **THEN** it SHALL return the portion before the `//`

#### Scenario: Preserve comments in strings

- **WHEN** `#` or `//` appears inside double quotes
- **THEN** the full line SHALL be returned unchanged

### Requirement: FileService is injectable via DI

`FileService` SHALL be registered as a singleton in the DI container. `IFileSystem` SHALL be registered as a singleton backed by the real `FileSystem` implementation.

#### Scenario: DI registration

- **WHEN** the MCP server starts
- **THEN** `IFileSystem` and `FileService` SHALL be available for injection

#### Scenario: Constructor injection in tool classes

- **WHEN** an MCP tool class needs filesystem access
- **THEN** it SHALL receive `FileService` via constructor injection

#### Scenario: Constructor injection in GitHubClient

- **WHEN** `GitHubClient` is constructed
- **THEN** it SHALL receive `FileService` via constructor parameter
