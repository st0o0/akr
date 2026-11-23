using System.Formats.Tar;
using System.IO.Abstractions;

namespace Akr.Tools;

public sealed class FileService
{
    private static readonly string[] _buildExcludes = ["/obj/", "/bin/"];

    private static readonly string[] _configExcludes =
    [
        "/test/", "/tests/", "/Test/", "/Tests/",
        ".Tests/", ".Tests.",
        ".TestKit/", ".TestKit.",
        "/example/", "/examples/", "/Example/", "/Examples/",
        "/obj/", "/bin/"
    ];

    private readonly IFileSystem _fs;

    public FileService(IFileSystem fileSystem)
    {
        _fs = fileSystem;
    }

    // ---- File operations ----

    public string ReadAllText(string path) => _fs.File.ReadAllText(path);

    public string[] ReadAllLines(string path) => _fs.File.ReadAllLines(path);

    public byte[] ReadAllBytes(string path) => _fs.File.ReadAllBytes(path);

    public IEnumerable<string> ReadLines(string path) => _fs.File.ReadLines(path);

    public bool FileExists(string path) => _fs.File.Exists(path);

    public void WriteAtomic(string path, byte[] bytes)
    {
        var dir = _fs.Path.GetDirectoryName(path)!;
        _fs.Directory.CreateDirectory(dir);

        var tempPath = path + ".tmp";
        _fs.File.WriteAllBytes(tempPath, bytes);
        _fs.File.Move(tempPath, path, overwrite: true);
    }

    // ---- Directory operations ----

    public bool DirectoryExists(string path) => _fs.Directory.Exists(path);

    public void CreateDirectory(string path) => _fs.Directory.CreateDirectory(path);

    public void DeleteDirectory(string path, bool recursive) => _fs.Directory.Delete(path, recursive);

    public void MoveDirectory(string source, string destination) => _fs.Directory.Move(source, destination);

    public IEnumerable<string> EnumerateDirectories(string path) => _fs.Directory.EnumerateDirectories(path);

    // ---- Path operations ----

    public string Combine(params string[] paths) => _fs.Path.Combine(paths);

    public string GetFullPath(string path) => _fs.Path.GetFullPath(path);

    public string? GetDirectoryName(string path) => _fs.Path.GetDirectoryName(path);

    public string GetFileName(string path) => _fs.Path.GetFileName(path);

    public string GetFileNameWithoutExtension(string path) => _fs.Path.GetFileNameWithoutExtension(path);

    public string GetRelativePosixPath(string root, string fullPath) =>
        _fs.Path.GetRelativePath(root, fullPath).Replace('\\', '/');

    // ---- Tar extraction ----

    public async Task ExtractTarEntryAsync(TarEntry entry, string fullPath, CancellationToken cancellationToken)
    {
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(fullPath)!);
        await entry.ExtractToFileAsync(fullPath, overwrite: false, cancellationToken);
    }

    // ---- Enumeration with exclusions ----

    public IEnumerable<(string Full, string Relative)> EnumerateFiles(
        IEnumerable<string> roots,
        string pattern)
    {
        foreach (var root in roots)
        {
            if (!_fs.Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in _fs.Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            {
                var relative = GetRelativePosixPath(root, file);
                if (ContainsAny(relative, _buildExcludes))
                {
                    continue;
                }

                yield return (file, relative);
            }
        }
    }

    public IEnumerable<(string Full, string Relative)> EnumerateConfigFiles(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (!_fs.Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in _fs.Directory.EnumerateFiles(root, "*.conf", SearchOption.AllDirectories))
            {
                var relative = GetRelativePosixPath(root, file);

                if (ContainsAny(relative, _configExcludes))
                {
                    continue;
                }

                if (!IsKnownConfigFile(relative))
                {
                    continue;
                }

                yield return (file, relative);
            }
        }
    }

    // ---- Private helpers ----

    private bool IsKnownConfigFile(string relative)
    {
        var fileName = _fs.Path.GetFileName(relative);

        if (fileName.Equals("reference.conf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (relative.Contains("/Configuration/", StringComparison.OrdinalIgnoreCase)
            && ContainsCorePrefix(relative))
        {
            return true;
        }

        if (fileName.Equals("persistence.conf", StringComparison.OrdinalIgnoreCase)
            && ContainsCorePrefix(relative))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsCorePrefix(string relative) =>
        relative.Contains("/src/core/", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("src/core/", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, string[] segments)
    {
        foreach (var segment in segments)
        {
            if (value.Contains(segment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
