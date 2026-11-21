## ADDED Requirements

### Requirement: MCP tool for type member signatures

The system SHALL expose an MCP tool named `type_members` that returns compact member
signatures for a C# type found in cached Akka.NET source.

#### Scenario: Get members of a class

- **WHEN** calling `type_members` with type `Props`
- **THEN** the tool SHALL return the type declaration with base types
- **AND** list properties, methods, and constructors as signature-only lines
- **AND** method bodies SHALL NOT be included

#### Scenario: Type not found

- **WHEN** calling `type_members` with a type name that does not exist
- **THEN** the tool SHALL return a message indicating the type was not found

### Requirement: Group members by kind

The output SHALL group members into sections: Properties, Methods, Constructors, Events.
Empty sections SHALL be omitted.

#### Scenario: Type with properties and methods

- **WHEN** a type has both properties and methods
- **THEN** the output SHALL show Properties section before Methods section

#### Scenario: Type with no events

- **WHEN** a type has no event members
- **THEN** the Events section SHALL be omitted from output

### Requirement: Merge partial class members

When a type is defined as partial across multiple files, the tool SHALL merge members
from all files into a single output.

#### Scenario: Partial class across files

- **WHEN** a type is split across two files
- **THEN** the output SHALL include members from both files
