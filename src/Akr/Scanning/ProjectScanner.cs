using System.Collections.Concurrent;
using System.Xml.Linq;
using Akr.Registry;
using Microsoft.Extensions.Logging;

namespace Akr.Scanning;

public sealed partial class ProjectScanner
{
    private const string DirectoryPackagesProps = "Directory.Packages.props";
    private const string DirectoryBuildProps = "Directory.Build.props";

    private readonly FileService _fileService;
    private readonly ILogger<ProjectScanner> _logger;
    private readonly ConcurrentDictionary<string, ProjectScanResult> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProjectScanner(FileService fileService, ILogger<ProjectScanner> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    public ProjectScanResult Current { get; set; } = ProjectScanResult.Empty;

    public void Initialize(string? projectPath)
    {
        if (!string.IsNullOrEmpty(projectPath))
        {
            var result = Scan(projectPath);
            if (result.HasPackages)
            {
                Current = result;
                return;
            }
        }

        var cwdResult = Scan(Environment.CurrentDirectory);
        if (cwdResult.HasPackages)
        {
            Current = cwdResult;
        }
    }

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
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    Log.DetectedPackages(_logger, current, string.Join(", ", packages.Select(p => $"{p.Key}={p.Value}")));
                }

                return new ProjectScanResult(current, packages);
            }

            if (foundProjectFiles)
            {
                Log.FoundProjectFilesNoPackages(_logger, current);
                break;
            }

            if (stop is not null &&
                string.Equals(_fileService.GetFullPath(current), stop, StringComparison.OrdinalIgnoreCase))
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

        var dpp = _fileService.Combine(directory, DirectoryPackagesProps);
        if (_fileService.FileExists(dpp))
        {
            foundFiles = true;
            ScanPackageElements(dpp, "PackageVersion", packages);
            if (packages.Count > 0)
            {
                return (packages, true);
            }
        }

        var dbp = _fileService.Combine(directory, DirectoryBuildProps);
        if (_fileService.FileExists(dbp))
        {
            foundFiles = true;
            ScanPackageElements(dbp, "PackageReference", packages);
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

    private void ScanPackageElements(string path, string elementName, Dictionary<string, string> packages)
    {
        try
        {
            var xml = XDocument.Parse(_fileService.ReadAllText(path));
            foreach (var el in xml.Descendants().Where(e =>
                         e.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase)))
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
            Log.FailedToParse(_logger, ex, path, ex.Message);
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
                ScanPackageElements(file, "PackageReference", packages);
            }
        }
        catch (Exception ex) when (ex is IOException)
        {
            Log.FailedToEnumerateCsproj(_logger, ex, directory, ex.Message);
        }

        return found;
    }

    private static bool IsAkkaPackage(string packageName) =>
        RepoRegistry.GetEntry(packageName) is not null;

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Detected Akka.NET packages in {Path}: {Packages}")]
        public static partial void DetectedPackages(ILogger logger, string path, string packages);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Found project files in {Path} but no Akka.NET packages")]
        public static partial void FoundProjectFilesNoPackages(ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse {Path}: {Error}")]
        public static partial void FailedToParse(ILogger logger, Exception ex, string path, string error);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to enumerate csproj files in {Path}: {Error}")]
        public static partial void FailedToEnumerateCsproj(ILogger logger, Exception ex, string path, string error);
    }
}

public sealed record ProjectScanResult(string? ProjectPath, IReadOnlyDictionary<string, string> Packages)
{
    public static readonly ProjectScanResult Empty = new(null, new Dictionary<string, string>());
    public bool HasPackages => Packages.Count > 0;
}
