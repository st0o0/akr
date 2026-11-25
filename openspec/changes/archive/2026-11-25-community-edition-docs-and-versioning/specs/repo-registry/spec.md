## MODIFIED Requirements

### Requirement: Registry covers all 4 ecosystem repos

The registry SHALL include entries for packages from:
- `akkadotnet/akka.net` (core, hosting, reminders)
- `akkadotnet/Akka.Management` (Akka.Management, discovery plugins)
- `akkadotnet/Akka.Persistence.Sql`
- `akkadotnet/Akka.Persistence.MongoDB`

Additionally, the registry SHALL include `akkadotnet/getakka.net` as a documentation-type repository entry, distinct from source-code repositories.

#### Scenario: All source repos represented

- **WHEN** enumerating all source-type registry entries
- **THEN** at least one package SHALL map to each of the 4 source repos listed above

#### Scenario: Docs repo registered

- **WHEN** querying the registry for the docs repository
- **THEN** it SHALL return `akkadotnet/getakka.net` with a type indicator distinguishing it from source repos

#### Scenario: No entries for archived repos

- **WHEN** enumerating all source-type registry entries
- **THEN** no package SHALL map to `akkadotnet/Akka.Hosting` (archived)
