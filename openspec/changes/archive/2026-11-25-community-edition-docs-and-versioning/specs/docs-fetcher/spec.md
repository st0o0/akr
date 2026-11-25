## ADDED Requirements

### Requirement: Fetch and cache getakka.net documentation sources

The system SHALL fetch Markdown documentation from the `akkadotnet/getakka.net` GitHub repository and cache it locally alongside source code. Documentation SHALL be fetched as a tar.gz archive from GitHub, extracted, and stored under `{CacheRoot}/getakka.net/{version}/`. The documentation root SHALL be the `src/docs/` directory within the archive.

#### Scenario: First fetch downloads and caches docs

- **WHEN** a docs tool is called and no docs are cached
- **THEN** the system SHALL download the getakka.net archive from GitHub
- **AND** extract the `src/docs/` directory to `{CacheRoot}/getakka.net/{version}/`
- **AND** subsequent docs tool calls SHALL use the cached content

#### Scenario: Cached docs are reused within TTL

- **WHEN** docs were fetched less than `CacheTtlHours` ago
- **THEN** the system SHALL use the cached docs without contacting GitHub

#### Scenario: Docs fetched independently from source code

- **WHEN** source code repos are fetched for a specific Akka.NET version
- **THEN** docs fetching SHALL NOT be coupled to source version resolution
- **AND** docs SHALL always use the latest available version from getakka.net

### Requirement: Index documentation by topic for fast lookup

The system SHALL build an in-memory index of documentation files on first access. The index SHALL map topic keywords (derived from filenames and directory names) to file paths. The index SHALL be cached per `GitHubClient` lifetime, consistent with the source caching pattern.

#### Scenario: Index built on first docs access

- **WHEN** a docs tool is called for the first time
- **THEN** the system SHALL scan all `.md` files in the docs cache
- **AND** build a topic-to-filepath index from filenames and directory structure

#### Scenario: Index reused on subsequent calls

- **WHEN** a docs tool is called after the index is built
- **THEN** the system SHALL return results from the cached index without re-scanning
