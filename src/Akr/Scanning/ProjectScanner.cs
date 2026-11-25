using System.Collections.Concurrent;
using System.Xml.Linq;
using Akr.Registry;
using Akr.Tools;
using Microsoft.Extensions.Logging;

namespace Akr.Scanning;

public sealed class ProjectScanner
{
    private readonly FileService _fileService;
    private readonly ILogger<ProjectScanner> _logger;
    private readonly ConcurrentDictionary<string, ProjectScanResult> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProjectScanner(FileService fileService, ILogger<ProjectScanner> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    public ProjectScanResult Current { get; set; } = ProjectScanResult.Empty;

    public ProjectScanResult Scan(string startDirectory, string? stopDirectory = null)
    {
        var normalized = _fileService.GetFullPath(startDirectory);
        var key = stopDirectory is not null ? $"{normalized}|{_fileService.GetFullPath(stopDirectory)}" : normalized;
        return _cache.GetOrAdd(key, _ => ScanCore(normalized, stopDirectory));
    }

    private ProjectScanResult ScanCore(string directory, string? stopDirectory)
    {
        var current = directory;
        var stop = stopDirectory is not null ? _fileService.GetFullPath(stopDirectory) : null;

        while (current is not null)
        {
            var (packages, foundProjectFiles) = ScanDirectory(current);
            if (packages.Count > 0)
            {
                _logger.LogInformation("Detected Akka.NET packages in {Path}: {Packages}",
                    current, string.Join(", ", packages.Select(p => $"{p.Key}={p.Value}")));
                return new ProjectScanResult(current, packages);
            }

            if (foundProjectFiles)
            {
                _logger.LogDebug("Found project files in {Path} but no Akka.NET packages", current);
                break;
            }

            if (stop is not null && string.Equals(_fileService.GetFullPath(current), stop, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = _fileService.GetDirectoryName(current);
        }

        return ProjectScanResult.Empty;
    }

    private (Dictionary<string, string> Packages, bool FoundProjectFiles) ScanDirectory(string directory)
    {
        var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var foundFiles = false;

        var dpp = _fileService.Combine(directory, "Directory.Packages.props");
        if (_fileService.FileExists(dpp))
        {
            foundFiles = true;
            ScanPackagesProps(dpp, packages);
            if (packages.Count > 0)
            {
                return (packages, true);
            }
        }

        var dbp = _fileService.Combine(directory, "Directory.Build.props");
        if (_fileService.FileExists(dbp))
        {
            foundFiles = true;
            ScanPackageReferences(dbp, packages);
            if (packages.Count > 0)
            {
                return (packages, true);
            }
        }

        if (ScanCsprojFiles(directory, packages))
        {
            foundFiles = true;
        }

        return (packages, foundFiles);
    }

    private void ScanPackagesProps(string path, Dictionary<string, string> packages)
    {
        try
        {
            var xml = XDocument.Parse(_fileService.ReadAllText(path));
            foreach (var el in xml.Descendants().Where(e =>
                e.Name.LocalName.Equals("PackageVersion", StringComparison.OrdinalIgnoreCase)))
            {
                var include = el.Attribute("Include")?.Value;
                var version = el.Attribute("Version")?.Value;
                if (include is not null && version is not null && IsAkkaPackage(include))
                {
                    packages.TryAdd(include, version);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            _logger.LogWarning("Failed to parse {Path}: {Error}", path, ex.Message);
        }
    }

    private void ScanPackageReferences(string path, Dictionary<string, string> packages)
    {
        try
        {
            var xml = XDocument.Parse(_fileService.ReadAllText(path));
            foreach (var el in xml.Descendants().Where(e =>
                e.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase)))
            {
                var include = el.Attribute("Include")?.Value;
                var version = el.Attribute("Version")?.Value;
                if (include is not null && version is not null && IsAkkaPackage(include))
                {
                    packages.TryAdd(include, version);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            _logger.LogWarning("Failed to parse {Path}: {Error}", path, ex.Message);
        }
    }

    private bool ScanCsprojFiles(string directory, Dictionary<string, string> packages)
    {
        var found = false;
        try
        {
            foreach (var file in _fileService.EnumerateDirectoryFiles(directory, "*.csproj"))
            {
                found = true;
                ScanPackageReferences(file, packages);
            }
        }
        catch (Exception ex) when (ex is IOException)
        {
            _logger.LogWarning("Failed to enumerate csproj files in {Path}: {Error}", directory, ex.Message);
        }

        return found;
    }

    private static bool IsAkkaPackage(string packageName) =>
        RepoRegistry.GetEntry(packageName) is not null;

    public Dictionary<string, string> MapToRepoVersions(IReadOnlyDictionary<string, string> packages)
    {
        var repoVersions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (packageName, version) in packages)
        {
            var entry = RepoRegistry.GetEntry(packageName);
            if (entry is null)
            {
                continue;
            }

            repoVersions.TryAdd(entry.Repo, version);
        }

        return repoVersions;
    }
}

public sealed record ProjectScanResult(string? ProjectPath, IReadOnlyDictionary<string, string> Packages)
{
    public static readonly ProjectScanResult Empty = new(null, new Dictionary<string, string>());
    public bool HasPackages => Packages.Count > 0;
}
