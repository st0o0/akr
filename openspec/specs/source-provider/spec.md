## Purpose

Unified facade for accessing cached Akka.NET source code.

## Requirements

### Requirement: Provide unified source access

The SourceProvider SHALL be the single entry point for all MCP tools to access Akka.NET source
code. It SHALL orchestrate the registry, version config, fetcher, and cache to return a path
to the cached source directory for a given package.

#### Scenario: Source available in cache

- **WHEN** a tool requests source for package `Akka` at the configured version
- **AND** the source is already cached
- **THEN** the provider SHALL return the cached source root path without any network requests

#### Scenario: Source not cached yet

- **WHEN** a tool requests source for package `Akka` at the configured version
- **AND** the source is not cached
- **THEN** the provider SHALL trigger the fetcher to download and cache the source
- **AND** return the source root path after caching completes

#### Scenario: Version override

- **WHEN** a tool requests source with an explicit version override
- **THEN** the provider SHALL use the override version instead of the configured version

### Requirement: Search across all loaded source

The SourceProvider SHALL provide a method to enumerate all cached source directories for
cross-repo searching. This enables tools like `search_source` to search across all loaded repos.

#### Scenario: Multiple repos cached

- **WHEN** source for `Akka` (akka.net repo) and `Akka.Hosting` (Akka.Hosting repo) is cached
- **AND** a tool requests all source roots
- **THEN** the provider SHALL return paths to both cached source directories

#### Scenario: No source cached

- **WHEN** no source has been fetched yet
- **THEN** the provider SHALL return an empty collection

### Requirement: Resolve package to source path

The SourceProvider SHALL map a package name to the specific source subdirectory within the
cached repo. For mono-repos (e.g. akka.net), this means returning the package-specific path
(e.g. `src/core/Akka/`), not the repo root.

#### Scenario: Mono-repo package path

- **WHEN** requesting source for package `Akka.Remote`
- **THEN** the provider SHALL return the path to the `Akka.Remote` subdirectory within the cached akka.net repo

#### Scenario: Single-package repo

- **WHEN** requesting source for package `Akka.Persistence.Sql`
- **THEN** the provider SHALL return the path to the main project directory within the cached repo
