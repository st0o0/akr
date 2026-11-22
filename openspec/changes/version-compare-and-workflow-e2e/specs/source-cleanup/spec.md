## MODIFIED Requirements

### Requirement: Cleanup old source versions

The system SHALL retain the N most recently accessed versions per repo (configurable, default 3) instead of only keeping the latest version. Versions beyond the limit SHALL be deleted oldest-first based on last access time.

#### Scenario: Two versions are retained during comparison
- **WHEN** a user has accessed version 1.5.52 and 1.5.74 and the cache limit is 3
- **THEN** both versions SHALL remain cached after cleanup

#### Scenario: Oldest version evicted when limit exceeded
- **WHEN** a 4th version is fetched and the cache limit is 3
- **THEN** the least recently accessed version SHALL be deleted

#### Scenario: Latest version is never evicted
- **WHEN** cleanup runs
- **THEN** the latest resolved version SHALL never be evicted regardless of access time
