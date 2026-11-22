# inherited-member-display Specification

## Purpose
TBD - created by archiving change fix-e2e-failures. Update Purpose after archive.
## Requirements
### Requirement: Display inherited interface members

The `type_members` tool SHALL include members inherited from parent interfaces, displayed in a separate section per parent type.

#### Scenario: IActorRef shows Tell from ICanTell
- **WHEN** `type_members(type: "IActorRef")` is called
- **THEN** the result SHALL contain `Tell` in an inherited members section attributed to `ICanTell`

#### Scenario: Direct members remain in their own section
- **WHEN** `type_members(type: "IActorRef")` is called
- **THEN** `ActorPath Path { get; }` SHALL appear in the direct Properties section, not in an inherited section

#### Scenario: System interfaces are excluded
- **WHEN** inherited members are resolved
- **THEN** members from `IEquatable<T>`, `IComparable<T>`, `IComparable`, and `ISurrogated` SHALL NOT be included

### Requirement: Single-level inheritance only

The tool SHALL resolve only direct parent interfaces (one level), not transitive ancestors.

#### Scenario: No transitive resolution
- **WHEN** `type_members` is called on a type whose parent also has parents
- **THEN** only the direct parent's members SHALL be included, not grandparent members

