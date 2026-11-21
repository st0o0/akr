## ADDED Requirements

### Requirement: MCP tool for stream stage catalog

The system SHALL expose an MCP tool named `stream_stages` that catalogs built-in
Akka.Streams stages from cached source code.

#### Scenario: List all stages

- **WHEN** calling `stream_stages` without a filter
- **THEN** the tool SHALL return all discovered stage classes grouped by shape category

#### Scenario: Filter by keyword

- **WHEN** calling `stream_stages` with filter `fan`
- **THEN** the tool SHALL return only stages whose name or category contains the keyword

#### Scenario: No stages found

- **WHEN** no Akka.Streams source is cached
- **THEN** the tool SHALL return a message indicating no stages were found

### Requirement: Classify stages by shape

Each stage SHALL be classified into one of: Source, Flow, Sink, Fan-In, Fan-Out, or
Custom based on its shape type parameter.

#### Scenario: Shape detection from base class

- **WHEN** a class extends `GraphStage<FlowShape<TIn, TOut>>`
- **THEN** it SHALL be classified as a Flow stage
