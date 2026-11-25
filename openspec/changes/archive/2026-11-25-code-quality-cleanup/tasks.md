## 1. Remove section comments

- [x] 1.1 Remove all `// ---- ... ----` and `// ---------- ... ----------` section comments from all source files (~14+ files)
- [x] 1.2 Verify all tests pass after removal

## 2. Eliminate ProjectContextInitializer

- [x] 2.1 Add `Initialize(string? projectPath)` method to `ProjectScanner`
- [x] 2.2 Update DI registration to use singleton factory that calls `Initialize`
- [x] 2.3 Delete `ProjectContextInitializer` class and its `AddHostedService` registration
- [x] 2.4 Verify startup behavior with and without `AKR_PROJECT_PATH`

## 3. ProjectScanner cleanup

- [x] 3.1 Move `MapToRepoVersions` to `RepoRegistry` as `public static` method
- [x] 3.2 Update all call sites to use `RepoRegistry.MapToRepoVersions()`
- [x] 3.3 Extract `DirectoryPackagesProps` and `DirectoryBuildProps` as `private const string`
- [x] 3.4 Merge `ScanPackagesProps` and `ScanPackageReferences` into `ScanPackageElements(string filePath, string elementName)`
- [x] 3.5 Update tests for refactored scan methods

## 4. Fix regex warnings (SYSLIB1044)

- [x] 4.1 Add `RegexOptions.NonBacktracking` to all patterns in TypeTools.cs that lack it
- [x] 4.2 Fix lazy quantifier (`*?`) in TypeScanner.cs to be compatible with `NonBacktracking`
- [x] 4.3 Decompose `TypeDeclPattern` and `DeclPattern` into multi-step parsing if still triggering SYSLIB1044
- [x] 4.4 Build and verify zero SYSLIB1044 warnings

## 5. Modernize logging with LoggerMessage source generators

- [x] 5.1 Add `private static partial class Log` with `[LoggerMessage]` methods to each class that logs
- [x] 5.2 Replace all direct `_logger.Log*()` calls with generated log methods
- [x] 5.3 Fix eager `string.Join` in ProjectScanner.cs:39-40
- [x] 5.4 Build and verify zero CA1873 warnings
