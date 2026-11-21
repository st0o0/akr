## ADDED Requirements

### Requirement: MCP tool for type lookup

The system SHALL expose an MCP tool named `lookup_type` that finds a C# type by name across
all cached Akka.NET source code and returns its source.

#### Scenario: Find a class by name

- **WHEN** calling `lookup_type` with type `ActorBase`
- **THEN** the tool SHALL locate the file containing the `ActorBase` class declaration
- **AND** return the file path, namespace, and source content

#### Scenario: Find an interface

- **WHEN** calling `lookup_type` with type `IActorRef`
- **THEN** the tool SHALL locate the interface declaration and return its source

#### Scenario: Type not found

- **WHEN** calling `lookup_type` with a type name that does not exist in cached source
- **THEN** the tool SHALL return a message indicating the type was not found

### Requirement: Match type declarations via pattern

The tool SHALL find types by matching declaration patterns: `class`, `interface`, `record`,
`struct`, `enum` followed by the type name. The match SHALL be word-boundary aware to avoid
partial matches (e.g. `Actor` should not match `ActorBase`).

#### Scenario: Exact name match

- **WHEN** searching for type `Actor`
- **THEN** the tool SHALL match `class Actor` but NOT `class ActorBase` or `class ActorSystem`

#### Scenario: Generic type

- **WHEN** searching for type `ReceiveActor`
- **THEN** the tool SHALL match `class ReceiveActor` including any generic parameters

### Requirement: Return token-efficient output by default

By default, the tool SHALL return a summary: the type declaration line, its namespace,
inheritance chain, and member signatures (without method bodies). An optional `fullSource`
parameter SHALL return the complete file content.

#### Scenario: Default summary output

- **WHEN** calling `lookup_type` with type `Props` and no `fullSource` parameter
- **THEN** the tool SHALL return the declaration, namespace, base types, and member signatures
- **AND** method bodies SHALL be omitted

#### Scenario: Full source output

- **WHEN** calling `lookup_type` with type `Props` and `fullSource: true`
- **THEN** the tool SHALL return the entire file contents

### Requirement: Handle multiple matches

When a type name matches multiple declarations (e.g. partial classes across files, or same
name in different namespaces), the tool SHALL return all matches.

#### Scenario: Partial class across files

- **WHEN** a type is defined as `partial class` in multiple files
- **THEN** the tool SHALL return all matching files

#### Scenario: Same name in different namespaces

- **WHEN** type name `Config` exists in multiple namespaces
- **THEN** the tool SHALL return all matches with their full namespace paths

### Requirement: Trigger lazy fetch

If no source is cached when `lookup_type` is called, the tool SHALL trigger fetching for
all configured packages before searching.

#### Scenario: First call triggers fetch

- **WHEN** `lookup_type` is called and no source is cached
- **AND** `AKKA_VERSIONS` is configured
- **THEN** the tool SHALL fetch source for all configured packages
- **AND** then perform the lookup
