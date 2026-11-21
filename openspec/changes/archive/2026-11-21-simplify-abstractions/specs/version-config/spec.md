## REMOVED Requirements

### Requirement: Parse version configuration from environment
**Reason**: Always-latest mode eliminates need for version configuration
**Migration**: Server resolves latest automatically, no env var needed

### Requirement: Validate package names against registry
**Reason**: No version config means no package name validation needed
**Migration**: None

### Requirement: Support optional version override per tool call
**Reason**: Simplified to always-latest
**Migration**: Re-add as tool parameter if needed in the future
