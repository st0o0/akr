## 1. Update RepoRegistry entries

- [x] 1.1 Move 5 existing Hosting entries from `akkadotnet/Akka.Hosting` to `akkadotnet/akka.net` with `src/contrib/hosting/` paths
- [x] 1.2 Add `Akka.Persistence.Embedded.Hosting` entry (`akkadotnet/akka.net`, `src/contrib/hosting/Akka.Persistence.Embedded.Hosting`)
- [x] 1.3 Add `Akka.Hosting.TestKit.Xunit2` entry (`akkadotnet/akka.net`, `src/contrib/hosting/Akka.Hosting.TestKit.Xunit2`)
- [x] 1.4 Add 5 Akka.Reminders entries (`akkadotnet/akka.net`, `src/contrib/reminders/Akka.Reminders[.*]`)

## 2. Update tests

- [x] 2.1 Update `RepoRegistryTests.cs` to verify Hosting packages resolve to `akkadotnet/akka.net`
- [x] 2.2 Add test cases for new packages (Embedded.Hosting, TestKit.Xunit2, Reminders)
- [x] 2.3 Update repo-count assertion from 5 to 4

## 3. Verify

- [x] 3.1 Run `dotnet build src/akr.slnx` — no warnings
- [x] 3.2 Run `dotnet run --project src/Akr.Tests/Akr.Tests.csproj` — all tests pass
- [x] 3.3 Run `dotnet format src/akr.slnx --verify-no-changes` — no formatting issues
