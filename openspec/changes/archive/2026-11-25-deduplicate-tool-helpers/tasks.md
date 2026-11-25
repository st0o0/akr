## 1. HoconParser extension (pure, static)

- [x] 1.1 Make `HoconParser` a `static partial class`
- [x] 1.2 Move `ParseConfig(IReadOnlyList<string> lines)` and `ConfigEntry` record from ConfigTools to HoconParser
- [x] 1.3 Move `PathMatches(string key, string query)` from ConfigTools/HostingTools to HoconParser
- [x] 1.4 Move `NamedMethodPattern` and `HoconKeyPattern` GeneratedRegex patterns to HoconParser
- [x] 1.5 Move `ExtractHoconBlock` from ConfigTools to HoconParser (static, pure)

## 2. HoconScanner (new Singleton, I/O)

- [x] 2.1 Create `Scanning/HoconScanner.cs` with constructor taking `FileService`
- [x] 2.2 Move `CollectConfig(IEnumerable<string> roots)` from ConfigTools/HostingTools to HoconScanner
- [x] 2.3 Move `CollectHostingKeys(IEnumerable<string> roots)` from ConfigTools/HostingTools to HoconScanner
- [x] 2.4 Register `HoconScanner` as singleton in `McpSetup.cs`

## 3. TypeScanner extension

- [x] 3.1 Make `TypeScanner` a `partial class` for GeneratedRegex
- [x] 3.2 Move `HasBase(string bases, string[] names)` from StreamTools/PersistenceTools to TypeScanner
- [x] 3.3 Move `CollectBlockLines(string[] lines, int start, int maxLines)` and `MaxClassLines` from SerializationTools/ClusterTools to TypeScanner
- [x] 3.4 Move `ClassifyTestKit` from HostingTools/TestKitTools to TypeScanner
- [x] 3.5 Move shared regexes (ClassPattern, MethodPattern, WhitespacePattern, StripGenericsPattern, BaseClassPattern) to TypeScanner

## 4. Tool cleanup

- [x] 4.1 Update ConfigTools — remove duplicates, inject HoconScanner, use HoconParser static methods
- [x] 4.2 Update HostingTools — remove duplicates, inject HoconScanner+TypeScanner, use HoconParser static methods
- [x] 4.3 Update StreamTools — remove HasBase+regexes, use TypeScanner
- [x] 4.4 Update PersistenceTools — remove HasBase+regexes, use TypeScanner
- [x] 4.5 Update SerializationTools — remove CollectBlockLines+regexes, use TypeScanner
- [x] 4.6 Update ClusterTools — remove CollectBlockLines+regexes, use TypeScanner
- [x] 4.7 Update TestKitTools — remove ClassifyTestKit+regexes, use TypeScanner

## 5. Build and verify

- [x] 5.1 Run `dotnet build src/akr.slnx` — zero errors, zero warnings
- [x] 5.2 Run `dotnet format src/akr.slnx --verify-no-changes`
- [x] 5.3 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [x] 5.4 Grep for duplicated method names — each SHALL exist in exactly one file
