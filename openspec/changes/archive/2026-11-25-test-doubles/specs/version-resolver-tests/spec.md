## ADDED Requirements

### Requirement: VersionResolver uses cached version when TTL is fresh
Tests SHALL verify that `ResolveLatestAsync` returns the cached version without HTTP calls when the cache TTL has not expired, using FakeTimeProvider.

#### Scenario: Cache is fresh
- **WHEN** metadata was written at T0 with version "1.5.75", cache TTL is 1 hour, and current fake time is T0 + 30 minutes
- **THEN** `ResolveLatestAsync` returns "1.5.75" without making an HTTP request

### Requirement: VersionResolver fetches when TTL is expired
Tests SHALL verify that `ResolveLatestAsync` makes an HTTP call when cache TTL has expired.

#### Scenario: Cache is stale
- **WHEN** metadata was written at T0 with version "1.5.75", cache TTL is 1 hour, and current fake time is T0 + 2 hours
- **THEN** `ResolveLatestAsync` sends an HTTP request to resolve the version

### Requirement: VersionResolver prefers project-detected version
Tests SHALL verify that project scanner results take priority over cached or remote versions.

#### Scenario: Project version overrides cache
- **WHEN** ProjectScanner.Current has Akka version "1.5.75" and cache has version "1.5.70"
- **THEN** `ResolveLatestAsync` returns "1.5.75" without making an HTTP request
