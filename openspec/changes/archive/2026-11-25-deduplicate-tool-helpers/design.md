## Context

After the separation-of-concerns refactor, 10 helper methods and 7 regex patterns are duplicated across tool files. The duplication was intentional during the refactor (keep each class self-contained), but should be cleaned up now that the structure is stable.

## Goals / Non-Goals

**Goals:**
- Eliminate all duplicated helper methods and regex patterns
- Place helpers in Scanning/ classes where they semantically belong
- Keep the pure/I/O split clean

**Non-Goals:**
- Changing any tool behavior
- Refactoring the helpers themselves (just moving them)

## Decisions

### 1. HoconParser stays static, gains pure parsing helpers
**Choice**: Add `ParseConfig(lines)`, `PathMatches(key, query)`, `NamedMethodPattern`, `HoconKeyPattern` to `HoconParser`. It becomes a `partial` class for `GeneratedRegex` support but stays static — no state, no I/O.
**Why**: These are pure functions that transform HOCON text. They belong with `StripComment` which is already in HoconParser. Static means no DI overhead for pure logic.

### 2. New HoconScanner for I/O-based config collection
**Choice**: Create `HoconScanner` as a Singleton in Scanning/ with constructor `(FileService)`. It owns `CollectConfig(roots)` and `CollectHostingKeys(roots)` which read files from disk.
**Why**: These methods need `FileService` (I/O). Putting them on a static class would require passing FileService as parameter — DI singleton is cleaner. Follows the same pattern as TypeScanner (Singleton that scans files).

### 3. TypeScanner extended with C# parsing helpers
**Choice**: Add `HasBase`, `CollectBlockLines`, `ClassifyTestKit`, and 5 regex patterns (`ClassPattern`, `MethodPattern`, `WhitespacePattern`, `StripGenericsPattern`, `BaseClassPattern`) to `TypeScanner`. It becomes `partial` for `GeneratedRegex`.
**Why**: These are all C# source parsing operations — checking base classes, collecting code blocks, classifying types. TypeScanner already does C# source scanning. Natural home.

### 4. ConfigEntry record moves to HoconParser
**Choice**: The `ConfigEntry(string Path, string? Value)` record used by `ParseConfig` moves from ConfigTools to HoconParser (or a companion file).
**Why**: ConfigEntry is a parsing output type, not a tool type.
