## Context

akr currently has 85 direct `System.IO` calls across 11 files. Three static utility classes (`FileEnumerator`, `HoconHelper`, `ConfigFiles`) were extracted in the previous refactoring but are still stateless wrappers around `System.IO`. Tool classes use static methods, making DI injection impossible without changing their shape. `GitHubClient` does the most complex I/O (tar extraction, atomic metadata writes, directory cleanup).

## Goals / Non-Goals

**Goals:**
- Single `FileService` class as the only place that touches the filesystem
- All tool classes and `GitHubClient` receive `FileService` via DI
- Cross-platform path handling (forward slashes, relative paths) built into `FileService`
- `MockFileSystem` available for future test improvements

**Non-Goals:**
- Rewriting existing tests to use `MockFileSystem` (existing real-filesystem tests stay as-is)
- Async file I/O (sync is fine for stdio MCP server)
- Abstracting `TarReader`/`GZipStream` (stream-level, not filesystem)

## Decisions

### 1. FileService as a concrete class, not an interface

`FileService` is a concrete class wrapping `IFileSystem`. We don't need `IFileService` — tests that need to mock filesystem behavior use `MockFileSystem` passed into `FileService`'s constructor. Adding an interface doubles the surface area for no benefit since the abstraction boundary is `IFileSystem`.

**Alternative considered:** `IFileService` interface — rejected because `IFileSystem` already provides the test seam. If we ever need to mock `FileService` itself, we can extract the interface then.

### 2. FileService API surface

Methods consolidated from the current codebase:

| Method | Replaces | Called by |
|--------|----------|----------|
| `ReadAllText(path)` | `File.ReadAllText` | All tool classes |
| `ReadAllLines(path)` | `File.ReadAllLines` | All tool classes |
| `ReadAllBytes(path)` | `File.ReadAllBytes` | GitHubClient |
| `WriteAtomic(path, bytes)` | temp+`File.WriteAllBytes`+`File.Move` | GitHubClient |
| `FileExists(path)` | `File.Exists` | GitHubClient |
| `DirectoryExists(path)` | `Directory.Exists` | GitHubClient, tools |
| `CreateDirectory(path)` | `Directory.CreateDirectory` | GitHubClient |
| `DeleteDirectory(path, recursive)` | `Directory.Delete` | GitHubClient |
| `MoveDirectory(src, dst)` | `Directory.Move` | GitHubClient |
| `EnumerateFiles(roots, pattern)` | `FileEnumerator.EnumerateFiles` | All tool classes |
| `EnumerateConfigFiles(roots)` | `ConfigFiles.EnumerateConfigFiles` | HostingTools, ConfigMigrationTools, EcosystemTools |
| `EnumerateDirectories(path)` | `Directory.EnumerateDirectories` | GitHubClient |
| `GetRelativePosixPath(root, path)` | `Path.GetRelativePath` + `.Replace('\\', '/')` | Everywhere (recurring pattern) |
| `Combine(params paths)` | `Path.Combine` | GitHubClient |
| `GetFullPath(path)` | `Path.GetFullPath` | GitHubClient |
| `GetFileName(path)` | `Path.GetFileName` | Multiple |
| `GetFileNameWithoutExtension(path)` | `Path.GetFileNameWithoutExtension` | EcosystemTools |
| `GetDirectoryName(path)` | `Path.GetDirectoryName` | GitHubClient |
| `StripHoconComment(line)` | `HoconHelper.StripComment` | HostingTools, ConfigMigrationTools |
| `ExtractToFileAsync(entry, path, ct)` | `entry.ExtractToFileAsync` | GitHubClient |

### 3. MCP tool classes become non-static with instance methods

Currently tool methods are `static` and receive `GitHubClient` via MCP DI parameter injection. With `FileService`, tools need two injected services. The `ModelContextProtocol` SDK supports constructor injection on `[McpServerToolType]` classes — make them non-static with a constructor that takes `FileService`, while `GitHubClient` continues to come via method parameter injection.

**Alternative considered:** Pass `FileService` through `GitHubClient` — rejected because tools also read files directly (not through GitHubClient), so they need their own reference.

### 4. DI registration

```csharp
services.AddSingleton<IFileSystem>(new FileSystem());
services.AddSingleton<FileService>();
```

`FileService` is singleton — it holds no mutable state, just wraps `IFileSystem`. `GitHubClient` stays as typed HttpClient (transient per HttpClientFactory).

### 5. GitHubClient receives FileService via constructor

`GitHubClient` already has a constructor with multiple parameters. Add `FileService` as a new parameter. The `cacheRoot` default still uses `Environment.GetFolderPath` — that stays as-is since it's a default value, not a filesystem operation.

## Risks / Trade-offs

- **[Tool classes become non-static]** → Required for constructor injection. The ModelContextProtocol SDK supports this. Method signatures stay the same for public/test-facing APIs; only the tool entry points change from `static` to instance.
- **[Large diff across 11 files]** → Mechanical replacement, low risk. Each file's changes are independent.
- **[FileService becomes a god object]** → Mitigated by keeping methods thin (1-2 lines delegating to `IFileSystem`). The class is a facade, not a logic container. Domain logic stays in the callers.
