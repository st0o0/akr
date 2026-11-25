## ADDED Requirements

### Requirement: Native AOT publishing enabled
`Akr.csproj` SHALL include `<PublishAot>true</PublishAot>` in a PropertyGroup. The project SHALL compile and publish as a native AOT binary without errors.

#### Scenario: AOT publish succeeds for win-x64
- **WHEN** `dotnet publish -r win-x64 --self-contained` is run
- **THEN** a native executable SHALL be produced in the publish output directory

#### Scenario: Zero trim/AOT warnings
- **WHEN** the project is built or published with AOT
- **THEN** zero IL2026 or IL3050 warnings SHALL be emitted

### Requirement: RID-specific tool packages
`Akr.csproj` SHALL include `<ToolPackageRuntimeIdentifiers>win-x64;linux-x64;osx-arm64</ToolPackageRuntimeIdentifiers>` to produce per-platform NuGet packages. Users SHALL continue to install via `dotnet tool install akr`.

#### Scenario: dotnet pack produces RID-specific packages
- **WHEN** `dotnet pack` is run
- **THEN** NuGet packages for win-x64, linux-x64, and osx-arm64 SHALL be produced

### Requirement: Description updated for AOT
The `<Description>` in `Akr.csproj` SHALL be updated to reflect the current tool count (37 tools).

#### Scenario: Package description is accurate
- **WHEN** the NuGet package metadata is inspected
- **THEN** the description SHALL mention 37 MCP tools
