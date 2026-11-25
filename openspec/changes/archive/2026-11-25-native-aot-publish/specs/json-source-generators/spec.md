## ADDED Requirements

### Requirement: Source-generated JSON serialization for RepoMetadata
`GitHubClient` SHALL use a `[JsonSerializable(typeof(RepoMetadata))]` source-generated `JsonSerializerContext` for all `JsonSerializer.Serialize` and `JsonSerializer.Deserialize` calls involving `RepoMetadata`. The reflection-based `JsonSerializerOptions` overloads SHALL be replaced with `JsonTypeInfo<T>` overloads.

#### Scenario: RepoMetadata serialization produces no trim warnings
- **WHEN** the project is built with `PublishAot=true`
- **THEN** no IL2026 or IL3050 warnings SHALL be emitted for `GitHubClient.cs`

#### Scenario: RepoMetadata round-trips correctly
- **WHEN** a `RepoMetadata` instance is serialized and deserialized using the source generator
- **THEN** all properties (Version, Etag, LastChecked, TagFormat, CachedVersions) SHALL be preserved

### Requirement: Source-generated JSON serialization for project context
`ProjectContextTools` SHALL use a `[JsonSerializable]` source-generated `JsonSerializerContext` for the project context serialization. The anonymous type currently used in `GetContext()` SHALL be replaced with a named record type.

#### Scenario: ProjectContext serialization produces no trim warnings
- **WHEN** the project is built with `PublishAot=true`
- **THEN** no IL2026 or IL3050 warnings SHALL be emitted for `ProjectContextTools.cs`

#### Scenario: Named record preserves existing JSON output shape
- **WHEN** `GetContext()` returns the serialized project context
- **THEN** the JSON output SHALL have the same property names and structure as before
