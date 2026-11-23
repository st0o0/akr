## REMOVED Requirements

### Requirement: Support authenticated requests

**Reason**: Never implemented. Analysis shows unauthenticated GitHub API limits (60 req/h) are sufficient given the 24h cache TTL and only 4 API calls per refresh cycle. The error message that referenced GITHUB_TOKEN is updated to point to AKR_CACHE_TTL_HOURS instead.

**Migration**: No migration needed — the feature never existed. Users hitting rate limits should increase `AKR_CACHE_TTL_HOURS`.
