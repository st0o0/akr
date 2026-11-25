using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Pipelines;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class EcosystemTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;
    private const int MaxClassLines = 200;
    private const int MaxMatchesPerCategory = 15;

    public EcosystemTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

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

    private static readonly (string Title, Regex Pattern)[] _diCategories =
    [
        ("Resolve actors via DI (IRequiredActor<T>)", new Regex(@"IRequiredActor<", RegexOptions.NonBacktracking)),
        ("ActorRegistry", new Regex(@"\bActorRegistry\b", RegexOptions.NonBacktracking)),
        ("Register services and actors", new Regex(@"\b(?:WithActors|AddAkka|WithActorRegistry)\b", RegexOptions.NonBacktracking)),
        ("Props with DI", new Regex(@"DependencyResolver|\bPropsFor\b|GetRequiredService|\bProps<|\.Props\(", RegexOptions.NonBacktracking))
    ];

    [GeneratedRegex(@"serialization-bindings\s*(=|\{|:)", RegexOptions.NonBacktracking)]
    private static partial Regex BindingsKeyRegex();

    [GeneratedRegex(@"^\s*""?([^""=\s]+)""?\s*=\s*""?([\w\-.]+)""?", RegexOptions.NonBacktracking)]
    private static partial Regex BindingLineRegex();

    [GeneratedRegex(@"\b(?:class|record)\s+(\w+)[^{;]*?:\s*([^{;]*)", RegexOptions.NonBacktracking)]
    private static partial Regex ClassWithBasesRegex();

    [GeneratedRegex(@"\binterface\s+(I\w*(?:Provider|Plugin|Extension))\b", RegexOptions.NonBacktracking)]
    private static partial Regex ExtensionInterfaceRegex();

    [GeneratedRegex(@"ISerializationManifest|\bManifest\s*\(|\bManifest\s*=|SerializerWithStringManifest|IWithManifest|const\s+string\s+\w*Manifest\w*\s*=", RegexOptions.NonBacktracking)]
    private static partial Regex ManifestRegex();

    [GeneratedRegex(@"\bpublic\s+(?:(?:virtual|static|async|override|sealed|new)\s+)*[\w<>\[\],.?]+\s+(\w+)\s*(?:<[^>(]*>)?\(", RegexOptions.NonBacktracking)]
    private static partial Regex PublicMethodRegex();

    [GeneratedRegex(@"\bpublic\s+(?:(?:abstract|sealed|static|partial)\s+)*class\s+(\w+)(?:<[^>]*>)?(?:\s*:\s*([^{\r\n]*))?", RegexOptions.NonBacktracking)]
    private static partial Regex PublicClassRegex();

    [GeneratedRegex(@"\bpublic\s+(?:(?:abstract|sealed|static|partial)\s+)*(?:class|interface|record|struct)\s+(\w+)", RegexOptions.NonBacktracking)]
    private static partial Regex PublicTypeRegex();

    [GeneratedRegex(@"<(ProjectReference|PackageReference)\b[^>]*?\bInclude\s*=\s*""([^""]+)""", RegexOptions.NonBacktracking)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.NonBacktracking)]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex WhitespaceRegex();

    // ---------- serializer_bindings ----------

    [McpServerTool(Name = "serializer_bindings"), Description("Show Akka.NET serialization-bindings (type to serializer mapping) from reference.conf files.")]
    public async Task<string> SerializerBindingsAsync(
        [Description("Optional filter on type or serializer name")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindSerializerBindings(roots, filter);
    }

    public string FindSerializerBindings(IEnumerable<string> roots, string? filter = null)
    {
        var rows = new List<(string File, string Type, string Serializer)>();
        foreach (var (full, relative) in _fileService.EnumerateConfigFiles(roots))
        {
            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!BindingsKeyRegex().IsMatch(lines[i]))
                {
                    continue;
                }

                foreach (var line in CollectBlockLines(lines, i).Skip(1))
                {
                    var m = BindingLineRegex().Match(line);
                    if (m.Success)
                    {
                        rows.Add((relative, m.Groups[1].Value, m.Groups[2].Value));
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            rows = [.. rows.Where(r => r.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.Serializer.Contains(filter, StringComparison.OrdinalIgnoreCase))];
        }

        if (rows.Count == 0)
        {
            return "No serialization bindings found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("| Type | Serializer | Source |");
        sb.AppendLine("|------|------------|--------|");
        foreach (var r in rows.OrderBy(r => r.File, StringComparer.Ordinal).ThenBy(r => r.Type, StringComparer.Ordinal))
        {
            sb.AppendLine($"| {r.Type} | {r.Serializer} | {r.File} |");
        }

        return sb.ToString();
    }

    // ---------- serializer_detail ----------

    [McpServerTool(Name = "serializer_detail"), Description("Show source and reference.conf config for Akka.NET serializer classes (Serializer / SerializerWithStringManifest).")]
    public async Task<string> SerializerDetailAsync(
        [Description("Serializer class name or fragment (e.g. Json, Protobuf)")] string name,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindSerializerDetail(roots, name);
    }

    public string FindSerializerDetail(IEnumerable<string> roots, string name)
    {
        var rootList = roots.ToList();
        var sb = new StringBuilder();
        var classNames = new List<string>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(rootList, "*.cs"))
        {
            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = ClassWithBasesRegex().Match(lines[i]);
                if (!m.Success
                    || !m.Groups[1].Value.Contains(name, StringComparison.OrdinalIgnoreCase)
                    || !IsSerializerBase(m.Groups[2].Value))
                {
                    continue;
                }

                classNames.Add(m.Groups[1].Value);
                sb.AppendLine($"--- {relative}:{i + 1} ---");
                sb.AppendLine(string.Join('\n', CollectBlockLines(lines, i, MaxClassLines)));
                sb.AppendLine();
            }
        }

        if (classNames.Count == 0)
        {
            return $"No serializer class found matching: {name}";
        }

        foreach (var (full, relative) in _fileService.EnumerateFiles(rootList, "reference.conf"))
        {
            var hits = _fileService.ReadAllLines(full)
                .Where(l => classNames.Any(c => l.Contains(c, StringComparison.Ordinal)))
                .Select(l => l.Trim())
                .ToList();
            if (hits.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"--- config: {relative} ---");
            foreach (var hit in hits)
            {
                sb.AppendLine(hit);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool IsSerializerBase(string bases) =>
        bases.Split(',', StringSplitOptions.TrimEntries)
            .Any(b => b is "Serializer" or "SerializerWithStringManifest"
                || b.EndsWith(".Serializer", StringComparison.Ordinal)
                || b.EndsWith(".SerializerWithStringManifest", StringComparison.Ordinal));

    // ---------- serialization_manifest ----------

    [McpServerTool(Name = "serialization_manifest"), Description("Find serialization manifest mappings (ISerializationManifest, Manifest methods, manifest constants) in persistence code.")]
    public async Task<string> SerializationManifestAsync(
        [Description("Optional file path filter (default: Persistence)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindManifests(roots, filter);
    }

    public string FindManifests(IEnumerable<string> roots, string? filter = null, int maxResults = 100)
    {
        var pathFilter = string.IsNullOrWhiteSpace(filter) ? "Persistence" : filter;
        var sb = new StringBuilder();
        var count = 0;

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!relative.Contains(pathFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!ManifestRegex().IsMatch(lines[i]))
                {
                    continue;
                }

                sb.AppendLine($"{relative}:{i + 1}: {lines[i].Trim()}");
                if (++count >= maxResults)
                {
                    return sb.ToString();
                }
            }
        }

        return count == 0 ? "No serialization manifest patterns found." : sb.ToString();
    }

    // ---------- hosting_di_patterns ----------

    [McpServerTool(Name = "hosting_di_patterns"), Description("Catalog of dependency injection patterns in Akka.Hosting source (IRequiredActor, ActorRegistry, Props with DI).")]
    public async Task<string> HostingDiPatternsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindDiPatterns(roots);
    }

    public string FindDiPatterns(IEnumerable<string> roots)
    {
        var buckets = _diCategories.Select(_ => new List<string>()).ToArray();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!relative.Contains("Hosting", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("TestKit", StringComparison.OrdinalIgnoreCase)
                || relative.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                for (var c = 0; c < _diCategories.Length; c++)
                {
                    if (buckets[c].Count < MaxMatchesPerCategory && _diCategories[c].Pattern.IsMatch(lines[i]))
                    {
                        var snippet = string.Join('\n', lines[i..Math.Min(lines.Length, i + 3)].Select(l => "    " + l.TrimEnd()));
                        buckets[c].Add($"  {relative}:{i + 1}\n{snippet}");
                    }
                }
            }
        }

        if (buckets.All(b => b.Count == 0))
        {
            return "No Akka.Hosting DI patterns found.";
        }

        var sb = new StringBuilder();
        for (var c = 0; c < _diCategories.Length; c++)
        {
            if (buckets[c].Count == 0)
            {
                continue;
            }

            sb.AppendLine($"## {_diCategories[c].Title}");
            foreach (var entry in buckets[c])
            {
                sb.AppendLine(entry);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ---------- hosting_testkit ----------

    [McpServerTool(Name = "hosting_testkit"), Description("Compare Akka.TestKit and Akka.Hosting.TestKit: base classes and assertion methods.")]
    public async Task<string> HostingTestKitAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return CompareTestKits(roots);
    }

    public string CompareTestKits(IEnumerable<string> roots)
    {
        var rows = new List<(string Package, string Class, string Base, string Assertions)>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            var package = ClassifyTestKit(relative);
            if (package is null)
            {
                continue;
            }

            var text = _fileService.ReadAllText(full);
            var assertions = PublicMethodRegex().Matches(text)
                .Select(m => m.Groups[1].Value)
                .Where(n => n.StartsWith("Expect", StringComparison.Ordinal)
                    || n.StartsWith("Assert", StringComparison.Ordinal)
                    || n.StartsWith("Await", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            foreach (Match m in PublicClassRegex().Matches(text))
            {
                var baseType = m.Groups[2].Success && m.Groups[2].Value.Trim().Length > 0
                    ? WhitespaceRegex().Replace(m.Groups[2].Value, " ").Trim()
                    : "-";
                rows.Add((package, m.Groups[1].Value, baseType, assertions.Count == 0 ? "-" : string.Join(", ", assertions)));
            }
        }

        if (rows.Count == 0)
        {
            return "No TestKit classes found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("| Package | Class | Base | Assertion methods |");
        sb.AppendLine("|---------|-------|------|-------------------|");
        foreach (var r in rows.OrderBy(r => r.Package, StringComparer.Ordinal).ThenBy(r => r.Class, StringComparer.Ordinal))
        {
            sb.AppendLine($"| {r.Package} | {r.Class} | {r.Base} | {r.Assertions} |");
        }

        return sb.ToString();
    }

    private static string? ClassifyTestKit(string relative)
    {
        if (relative.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (relative.Contains("Akka.Hosting.TestKit", StringComparison.OrdinalIgnoreCase))
        {
            return "Akka.Hosting.TestKit";
        }

        return relative.Contains("Akka.TestKit", StringComparison.OrdinalIgnoreCase) ? "Akka.TestKit" : null;
    }

    // ---------- testkit_helpers ----------

    [McpServerTool(Name = "testkit_helpers"), Description("Catalog public utility and assertion methods in Akka TestKit source, grouped by purpose.")]
    public async Task<string> TestKitHelpersAsync(
        [Description("Optional method name filter")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindTestKitHelpers(roots, filter);
    }

    public string FindTestKitHelpers(IEnumerable<string> roots, string? filter = null)
    {
        var groups = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (ClassifyTestKit(relative) is null)
            {
                continue;
            }

            var currentType = _fileService.GetFileNameWithoutExtension(relative);
            foreach (var line in _fileService.ReadAllLines(full))
            {
                var t = PublicTypeRegex().Match(line);
                if (t.Success)
                {
                    currentType = t.Groups[1].Value;
                    continue;
                }

                var m = PublicMethodRegex().Match(line);
                if (!m.Success)
                {
                    continue;
                }

                var method = m.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(filter) && !method.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var category = CategorizeHelper(method);
                if (!groups.TryGetValue(category, out var set))
                {
                    groups[category] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                set.Add($"{currentType}.{method}");
            }
        }

        if (groups.Count == 0)
        {
            return "No TestKit helpers found.";
        }

        var sb = new StringBuilder();
        foreach (var (category, methods) in groups)
        {
            sb.AppendLine($"## {category}");
            foreach (var method in methods)
            {
                sb.AppendLine($"  {method}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string CategorizeHelper(string method) => method switch
    {
        _ when method.StartsWith("Expect", StringComparison.Ordinal) => "Expect (message assertions)",
        _ when method.StartsWith("Assert", StringComparison.Ordinal) => "Assert",
        _ when method.StartsWith("Await", StringComparison.Ordinal) => "Await (polling and waiting)",
        _ when method.StartsWith("Within", StringComparison.Ordinal)
            || method.StartsWith("Intercept", StringComparison.Ordinal) => "Timing and interception",
        _ when method.StartsWith("Create", StringComparison.Ordinal)
            || method.StartsWith("Child", StringComparison.Ordinal) => "Creation",
        _ when method.StartsWith("Watch", StringComparison.Ordinal)
            || method.StartsWith("Unwatch", StringComparison.Ordinal) => "Death watch",
        _ => "Other"
    };

    // ---------- cluster_events ----------

    [McpServerTool(Name = "cluster_events"), Description("List Akka.Cluster ClusterEvent types ordered by member lifecycle (Joining, Up, Leaving, Exiting, Down, Removed) with XML doc descriptions.")]
    public async Task<string> ClusterEventsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindClusterEvents(roots);
    }

    public string FindClusterEvents(IEnumerable<string> roots)
    {
        var events = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (full, _) in _fileService.EnumerateFiles(roots, "ClusterEvent.cs"))
        {
            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = PublicTypeRegex().Match(lines[i]);
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
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    // ---------- extension_points ----------

    [McpServerTool(Name = "extension_points"), Description("Catalog Akka.NET provider/plugin/extension interfaces (I*Provider, I*Plugin, I*Extension) with implementors.")]
    public async Task<string> ExtensionPointsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindExtensionPoints(roots);
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
                var m = ClassWithBasesRegex().Match(line);
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

    // ---------- dependency_graph ----------

    [McpServerTool(Name = "dependency_graph"), Description("Build an Akka package dependency map from .csproj ProjectReference and PackageReference entries.")]
    public async Task<string> DependencyGraphAsync(
        [Description("Optional project name filter (e.g. Akka.Cluster)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return BuildDependencyGraph(roots, filter);
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

    // ---------- helpers ----------

    private static List<string> CollectBlockLines(string[] lines, int start, int maxLines = int.MaxValue)
    {
        var collected = new List<string>();
        var depth = 0;
        var opened = false;

        for (var i = start; i < lines.Length && collected.Count < maxLines; i++)
        {
            collected.Add(lines[i]);
            foreach (var c in lines[i])
            {
                if (c == '#')
                {
                    break;
                }

                if (c == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (c == '}')
                {
                    depth--;
                }
            }

            if (opened && depth <= 0)
            {
                break;
            }

            if (!opened && i > start + 3)
            {
                break;
            }
        }

        return collected;
    }
}
