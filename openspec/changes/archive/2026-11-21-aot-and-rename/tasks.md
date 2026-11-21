## 1. Rename AkkaMcp → Akr

- [x] 1.1 Rename solution file: `akka-mcp.slnx` → `akr.slnx`
- [x] 1.2 Rename project directories: `AkkaMcp/` → `Akr/`, `AkkaMcp.Core/` → `Akr.Core/`, etc.
- [x] 1.3 Rename `.csproj` files to match new directory names
- [x] 1.4 Update all project references in `.csproj` files and `.slnx`
- [x] 1.5 Rename namespaces: `AkkaMcp` → `Akr` across all `.cs` files
- [x] 1.6 Update `PackageId`, `Description` in host `.csproj`
- [x] 1.7 Update `InternalsVisibleTo` attributes
- [x] 1.8 Update CLAUDE.md and AGENTS.md references
- [x] 1.9 Build and verify all tests pass

## 2. Drop unused/AOT-incompatible dependencies

- [x] 2.1 Remove `Microsoft.CodeAnalysis.CSharp` from `Akr.Core.csproj` and `Directory.Packages.props`
- [x] 2.2 Remove Serilog packages: `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Settings.Configuration`, `Serilog.Sinks.Console` from host `.csproj` and `Directory.Packages.props`
- [x] 2.3 Remove Serilog usings/config from `Program.cs` (keep `builder.Logging.AddConsole()`)
- [x] 2.4 Build and verify no Serilog references remain

## 3. Latest-by-default version resolution

- [x] 3.1 Update `EnvironmentVersionConfig`: empty config = all repos resolve to latest
- [x] 3.2 Update `SourceProvider.EnsureAllConfiguredAsync()`: when no versions configured, resolve latest for all 5 repos
- [x] 3.3 Cache resolved latest versions for the session (don't re-resolve per call)
- [x] 3.4 Update tests for new default behavior
- [x] 3.5 Remove `GITHUB_TOKEN` from `McpSetup` HttpClient config (not needed for public repos)

## 4. AOT build configuration

- [x] 4.1 Add `PublishAot`, `InvariantGlobalization`, `JsonSerializerIsReflectionEnabledByDefault=false` to host `.csproj`
- [x] 4.2 Switch `WithToolsFromAssembly()` to `WithTools<SearchSourceTool>().WithTools<LookupTypeTool>()`
- [x] 4.3 Update `UserAgent` header from `akka-mcp/0.1` to `akr/0.1`

## 5. AOT-safe regex

- [x] 5.1 `SourceSearcher`: change `RegexOptions.Compiled` to `RegexOptions.NonBacktracking`
- [x] 5.2 `TypeFinder`: convert static patterns to `[GeneratedRegex]` partial methods
- [x] 5.3 Update tests to verify patterns still match correctly

## 6. Verify AOT

- [x] 6.1 Run `dotnet publish` with AOT and check for trim warnings (zero warnings, linker fails on local vswhere PATH only)
- [x] 6.2 Run all tests after changes
- [x] 6.3 Format check: `dotnet format --verify-no-changes`
- [x] 6.4 Slopwatch check
- [x] 6.5 Smoke test: start the server, verify it boots as "Akr"
