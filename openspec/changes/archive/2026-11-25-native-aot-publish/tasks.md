## 1. JSON source generators

- [x] 1.1 Add `[JsonSerializable(typeof(RepoMetadata))]` context in `GitHubClient.cs`
- [x] 1.2 Replace `JsonSerializer.Deserialize<RepoMetadata>(json, _jsonOptions)` with source-generated overload
- [x] 1.3 Replace `JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions)` with source-generated overload
- [x] 1.4 Replace anonymous type in `ProjectContextTools.GetContext()` with named record
- [x] 1.5 Add `[JsonSerializable]` context in `ProjectContextTools.cs` for the named record
- [x] 1.6 Replace `JsonSerializer.Serialize(context, _jsonOptions)` with source-generated overload
- [x] 1.7 Remove unused `_jsonOptions` fields if no longer needed

## 2. AOT publish configuration

- [x] 2.1 Add `<PublishAot>true</PublishAot>` to `Akr.csproj`
- [x] 2.2 Add `<ToolPackageRuntimeIdentifiers>win-x64;linux-x64;osx-arm64</ToolPackageRuntimeIdentifiers>` to `Akr.csproj`
- [x] 2.3 Update `<Description>` to mention 37 MCP tools

## 3. Build and verify

- [x] 3.1 Run `dotnet build src/akr.slnx` — zero errors, zero warnings
- [x] 3.2 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 3.3 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [x] 3.4 Run `dotnet publish src/Akr/Akr.csproj -r win-x64 --self-contained` — AOT publish succeeds with zero trim warnings
