## MODIFIED Requirements

### Requirement: Parse version configuration from environment

The system SHALL read Akka package versions from the `AKKA_VERSIONS` environment variable.
The format SHALL be comma-separated `Package=Version` pairs. When `AKKA_VERSIONS` is not
set or empty, the system SHALL operate in latest-mode: resolving the latest release version
for each repo on first use.

#### Scenario: Empty or missing configuration uses latest

- **WHEN** `AKKA_VERSIONS` is not set or empty
- **THEN** the system SHALL resolve the latest release version per repo on first tool call
- **AND** cache the resolved versions for the duration of the session
