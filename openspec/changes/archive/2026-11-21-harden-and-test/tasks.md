## 1. Fix GitHubClient

- [x] 1.1 Make CacheRoot an instance field via constructor parameter (default to ~/.akr/sources/)
- [x] 1.2 Atomic extraction: extract to {path}.tmp, Directory.Move on success, delete .tmp on failure
- [x] 1.3 HTTP error handling: 403/429 → rate limit message, 5xx → server error message
- [x] 1.4 ResolveLatestAsync: handle missing tag_name, HTTP errors
- [x] 1.5 Update McpSetup to pass CacheRoot (or use default)

## 2. Fix search_source regex handling

- [x] 2.1 Catch RegexParseException in StreamTools.search_source, return user-friendly error

## 3. Rewrite GitHubClient tests

- [x] 3.1 Test tag fallback: first tag 404, v-prefixed succeeds
- [x] 3.2 Test cache hit: pre-created dir skips HTTP call
- [x] 3.3 Test atomic extraction: failed download leaves no cache dir
- [x] 3.4 Test rate limit error (HTTP 429)
- [x] 3.5 Test resolve latest failure (HTTP 500, missing tag_name)
- [x] 3.6 Test EnsureAllSourcesAsync fetches each repo once
- [x] 3.7 Test invalid regex returns error message via MCP integration test

## 4. Clean up tests

- [x] 4.1 Delete SmokeTests.cs
- [x] 4.2 Update McpServerTests to use instance CacheRoot (no static state)
- [x] 4.3 Build, test, format, slopwatch
