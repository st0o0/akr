## ADDED Requirements

### Requirement: Extract attribute defaults from sealed nested classes

The `stream_attributes` tool SHALL extract default values from Akka.Streams attribute classes that define defaults via static members (with or without `readonly`), `Instance` properties, or constructor parameters.

#### Scenario: At least one default value is shown
- **WHEN** `stream_attributes()` is called
- **THEN** at least one attribute SHALL include a `default:` annotation with a value

#### Scenario: Sealed class pattern is matched
- **WHEN** an attribute class defines a default via `public static Name Instance = new Name(...)` or `public static readonly Name Default = ...`
- **THEN** the default SHALL be extracted and displayed

### Requirement: Existing attributes remain listed

The broadened default extraction SHALL NOT remove or change the existing attribute class discovery (classes inheriting from `IAttribute`).

#### Scenario: All previously found attributes still appear
- **WHEN** `stream_attributes()` is called
- **THEN** `Name`, `LogLevels`, `AsyncBoundary`, and `Delay` SHALL still be listed
