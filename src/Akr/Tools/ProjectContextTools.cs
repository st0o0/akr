using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Akr.Scanning;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class ProjectContextTools
{
    private readonly ProjectScanner _scanner;
    private readonly FileService _fileService;

    public ProjectContextTools(ProjectScanner scanner, FileService fileService)
    {
        _scanner = scanner;
        _fileService = fileService;
    }

    [McpServerTool(Name = "set_project_context"), Description("Set the .NET project path for Akka.NET version detection. akr scans the path for NuGet references and uses the detected Akka version for source lookups.")]
    public string SetProjectContext(
        [Description("Absolute path to the .NET project directory")] string path)
    {
        if (!_fileService.DirectoryExists(path))
        {
            return $"Error: directory does not exist: {path}";
        }

        var result = _scanner.Scan(path);
        _scanner.Current = result;

        if (!result.HasPackages)
        {
            return $"No Akka.NET packages found in {path} or its parent directories. Version resolution will use AKKA_VERSIONS environment variable or latest GitHub release.";
        }

        var repoVersions = _scanner.MapToRepoVersions(result.Packages);
        return $"Project context set to {result.ProjectPath}\n\nDetected packages:\n"
            + string.Join("\n", result.Packages.Select(p => $"  {p.Key} = {p.Value}"))
            + "\n\nResolved repo versions:\n"
            + string.Join("\n", repoVersions.Select(r => $"  {r.Key} = {r.Value}"));
    }
}

internal sealed record ProjectContextInfo(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("projectPath")] string? ProjectPath,
    [property: JsonPropertyName("packages")] IReadOnlyDictionary<string, string> Packages,
    [property: JsonPropertyName("resolvedVersions")] Dictionary<string, string> ResolvedVersions);

[McpServerResourceType]
public sealed class ProjectContextResources
{
    private readonly ProjectScanner _scanner;

    public ProjectContextResources(ProjectScanner scanner)
    {
        _scanner = scanner;
    }

    [McpServerResource(UriTemplate = "akr://context", Name = "akr-context", MimeType = "application/json"),
     Description("Current akr version resolution state: detected Akka.NET version, source, project path, and packages.")]
    public string GetContext()
    {
        var current = _scanner.Current;
        ProjectContextInfo context;

        if (current.HasPackages)
        {
            context = new ProjectContextInfo(
                Source: "project",
                ProjectPath: current.ProjectPath,
                Packages: current.Packages,
                ResolvedVersions: _scanner.MapToRepoVersions(current.Packages));
        }
        else
        {
            context = new ProjectContextInfo(
                Source: "latest",
                ProjectPath: null,
                Packages: new Dictionary<string, string>(),
                ResolvedVersions: []);
        }

        return JsonSerializer.Serialize(context, AkrJsonContext.Default.ProjectContextInfo);
    }
}
