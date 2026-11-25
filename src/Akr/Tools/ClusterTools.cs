using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class ClusterTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;

    public ClusterTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

    private const int MaxMatchesPerCategory = 15;

    private static readonly string[] _clusterLifecycle =
    [
        "MemberJoined", "MemberWeaklyUp", "MemberUp", "MemberLeft", "MemberExited", "MemberDowned", "MemberRemoved"
    ];

    private static readonly Dictionary<string, string> _lifecycleLabels = new(StringComparer.Ordinal)
    {
        ["MemberJoined"] = "Joining",
        ["MemberWeaklyUp"] = "WeaklyUp",
        ["MemberUp"] = "Up",
        ["MemberLeft"] = "Leaving",
        ["MemberExited"] = "Exiting",
        ["MemberDowned"] = "Down",
        ["MemberRemoved"] = "Removed"
    };

    [GeneratedRegex(@"\binterface\s+(I\w*(?:Provider|Plugin|Extension))\b", RegexOptions.NonBacktracking)]
    private static partial Regex ExtensionInterfaceRegex();

    [GeneratedRegex(@"<(ProjectReference|PackageReference)\b[^>]*?\bInclude\s*=\s*""([^""]+)""", RegexOptions.NonBacktracking)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.NonBacktracking)]
    private static partial Regex XmlTagRegex();

    [McpServerTool(Name = "cluster_events"), Description("List Akka.Cluster ClusterEvent types ordered by member lifecycle (Joining, Up, Leaving, Exiting, Down, Removed) with XML doc descriptions.")]
    public async Task<string> ClusterEventsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(FindClusterEvents(roots));
    }

    public string FindClusterEvents(IEnumerable<string> roots)
    {
        var events = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (full, _) in _fileService.EnumerateFiles(roots, "ClusterEvent.cs"))
        {
            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = TypeScanner.PublicTypePattern().Match(lines[i]);
                if (!m.Success)
                {
                    continue;
                }

                events.TryAdd(m.Groups[1].Value, ReadXmlSummary(lines, i));
            }
        }

        if (events.Count == 0)
        {
            return "No cluster events found.";
        }

        var ordered = _clusterLifecycle.Where(events.ContainsKey)
            .Concat(events.Keys.Except(_clusterLifecycle).Order(StringComparer.Ordinal));

        var sb = new StringBuilder();
        var n = 1;
        foreach (var name in ordered)
        {
            var label = _lifecycleLabels.TryGetValue(name, out var l) ? $" [{l}]" : string.Empty;
            var doc = events[name].Length == 0 ? "(no documentation)" : events[name];
            sb.AppendLine($"{n++}. {name}{label}: {doc}");
        }

        return sb.ToString();
    }

    private static string ReadXmlSummary(string[] lines, int declLine)
    {
        var doc = new List<string>();
        for (var i = declLine - 1; i >= 0; i--)
        {
            var t = lines[i].Trim();
            if (t.StartsWith("///", StringComparison.Ordinal))
            {
                doc.Insert(0, t[3..]);
            }
            else if (!t.StartsWith('['))
            {
                break;
            }
        }

        var text = XmlTagRegex().Replace(string.Join(' ', doc), string.Empty);
        return TypeScanner.WhitespacePattern().Replace(text, " ").Trim();
    }

    [McpServerTool(Name = "extension_points"), Description("Catalog Akka.NET provider/plugin/extension interfaces (I*Provider, I*Plugin, I*Extension) with implementors.")]
    public async Task<string> ExtensionPointsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(FindExtensionPoints(roots));
    }

    public string FindExtensionPoints(IEnumerable<string> roots)
    {
        var files = _fileService.EnumerateFiles(roots, "*.cs")
            .Where(f => !f.Relative.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
            .Select(f => (f.Relative, Lines: _fileService.ReadAllLines(f.Full)))
            .ToList();

        var interfaces = new SortedDictionary<string, (string File, string Doc)>(StringComparer.Ordinal);
        foreach (var (relative, lines) in files)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var m = ExtensionInterfaceRegex().Match(lines[i]);
                if (m.Success)
                {
                    interfaces.TryAdd(m.Groups[1].Value, (relative, ReadXmlSummary(lines, i)));
                }
            }
        }

        if (interfaces.Count == 0)
        {
            return "No extension point interfaces found.";
        }

        var implementors = interfaces.Keys.ToDictionary(k => k, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (_, lines) in files)
        {
            foreach (var line in lines)
            {
                var m = TypeScanner.ClassWithBasesPattern().Match(line);
                if (!m.Success)
                {
                    continue;
                }

                var bases = m.Groups[2].Value.Split([',', ' ', '<', '>', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var b in bases)
                {
                    if (implementors.TryGetValue(b, out var set))
                    {
                        set.Add(m.Groups[1].Value);
                    }
                }
            }
        }

        var sb = new StringBuilder();
        foreach (var (name, info) in interfaces)
        {
            sb.AppendLine($"{name} ({info.File})");
            if (info.Doc.Length > 0)
            {
                sb.AppendLine($"  {info.Doc}");
            }

            var impls = implementors[name];
            sb.AppendLine(impls.Count == 0 ? "  Implementors: (none found)" : $"  Implementors: {string.Join(", ", impls)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "dependency_graph"), Description("Build an Akka package dependency map from .csproj ProjectReference and PackageReference entries.")]
    public async Task<string> DependencyGraphAsync(
        [Description("Optional project name filter (e.g. Akka.Cluster)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(BuildDependencyGraph(roots, filter));
    }

    public string BuildDependencyGraph(IEnumerable<string> roots, string? filter = null)
    {
        var graph = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (full, _) in _fileService.EnumerateFiles(roots, "*.csproj"))
        {
            var project = _fileService.GetFileNameWithoutExtension(full);
            if (!project.StartsWith("Akka", StringComparison.Ordinal))
            {
                continue;
            }

            if (!graph.TryGetValue(project, out var deps))
            {
                graph[project] = deps = new SortedSet<string>(StringComparer.Ordinal);
            }

            foreach (Match m in ReferenceRegex().Matches(_fileService.ReadAllText(full)))
            {
                var isProject = m.Groups[1].Value == "ProjectReference";
                var name = isProject
                    ? _fileService.GetFileNameWithoutExtension(m.Groups[2].Value.Replace('\\', '/'))
                    : m.Groups[2].Value;

                if (name.StartsWith("Akka", StringComparison.Ordinal))
                {
                    deps.Add(isProject ? $"{name} (project)" : $"{name} (package)");
                }
            }
        }

        var selected = graph
            .Where(kv => string.IsNullOrWhiteSpace(filter) || kv.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (selected.Count == 0)
        {
            return string.IsNullOrWhiteSpace(filter)
                ? "No Akka projects found."
                : $"No Akka projects found matching: {filter}";
        }

        var sb = new StringBuilder();
        foreach (var (project, deps) in selected)
        {
            sb.AppendLine(project);
            foreach (var dep in deps)
            {
                sb.AppendLine($"  +-- {dep}");
            }
        }

        return sb.ToString();
    }

}
