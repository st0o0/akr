using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Analysis;
using Akr.Models;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class ConfigMigrationTools
{
    private readonly FileService _fileService;
    private const int MaxSectionLines = 40;

    public ConfigMigrationTools(FileService fileService)
    {
        _fileService = fileService;
    }

    [GeneratedRegex(@"public\s+static\s+\S+\s+(?<name>(With|Add)\w+)\s*[\(<]", RegexOptions.NonBacktracking)]
    private static partial Regex MethodPattern();

    [GeneratedRegex(@"\bakka(?:\.[A-Za-z0-9_\-]+)+", RegexOptions.NonBacktracking)]
    private static partial Regex HoconKeyPattern();

    [GeneratedRegex(@"^(?<key>[A-Za-z0-9_\.\-""]+)\s*(?<op>[={:])\s*(?<rest>.*)$", RegexOptions.NonBacktracking)]
    private static partial Regex EntryPattern();

    [GeneratedRegex(@"(must|should|needs? to|has to)\s+be\s+(called|registered|configured|invoked|set)|(before|after)\s+(calling|registering|configuring)|call(ed)?\s+(this\s+)?(before|after)|requires?\s+(that\s+)?\w*(With|Add)\w+|order\s+(matters|of)", RegexOptions.NonBacktracking | RegexOptions.IgnoreCase)]
    private static partial Regex OrderingPattern();

    [GeneratedRegex(@"GetRequiredService<|GetService<|ActorRegistry|registry\.(Try)?Get<|DependencyResolver", RegexOptions.NonBacktracking)]
    private static partial Regex DiHintPattern();

    private static readonly Dictionary<string, string[]> _fallbackHostingMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WithClustering"] = ["akka.cluster"],
        ["WithRemoting"] = ["akka.remote"],
        ["WithDistributedData"] = ["akka.cluster.distributed-data"],
        ["WithSingleton"] = ["akka.cluster.singleton"],
        ["WithShardRegion"] = ["akka.cluster.sharding"],
        ["WithDistributedPubSub"] = ["akka.cluster.pub-sub"],
        ["WithJournal"] = ["akka.persistence.journal"],
        ["WithSnapshotStore"] = ["akka.persistence.snapshot-store"],
        ["WithCoordinatedShutdown"] = ["akka.coordinated-shutdown"],
    };

    private static readonly string[] _recommendedOrder =
    [
        "1. Remote / transport (WithRemoting)",
        "2. Cluster (WithClustering, WithClusterBootstrap, Akka.Management)",
        "3. Persistence (WithJournal, WithSnapshotStore, WithSqlPersistence)",
        "4. Sharding / Singleton / Distributed PubSub (WithShardRegion, WithSingleton, WithDistributedPubSub)",
        "5. Actors (WithActors) - last, so ActorRegistry dependencies from earlier steps are available",
    ];

    [McpServerTool(Name = "config_tree"), Description("Hierarchical view of HOCON config paths from reference.conf files, annotated with the Akka.Hosting With* method that sets them.")]
    public async Task<string> ConfigTreeAsync(
        GitHubClient github,
        [Description("Optional HOCON path prefix to filter (e.g. akka.cluster)")] string? path = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return BuildConfigTree(roots, path);
    }

    [McpServerTool(Name = "hosting_to_hocon"), Description("Bidirectional mapping between Akka.Hosting With*/Add* methods and HOCON config keys.")]
    public async Task<string> HostingToHoconAsync(
        GitHubClient github,
        [Description("Hosting method (e.g. WithClustering) or HOCON path (e.g. akka.cluster.roles)")] string query,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return MapQuery(roots, query);
    }

    [McpServerTool(Name = "hosting_startup_order"), Description("Registration ordering constraints and DI resolution hints from Akka.Hosting source.")]
    public async Task<string> StartupOrderAsync(
        GitHubClient github,
        [Description("Optional keyword to filter by file path")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return FindStartupOrder(roots, filter);
    }

    [McpServerTool(Name = "config_diff"), Description("Config key changes between versions. Currently lists all config keys of the current cached version (diff against empty).")]
    public async Task<string> ConfigDiffAsync(
        GitHubClient github,
        [Description("Older version (reserved; only the latest cached version is currently supported)")] string? versionA = null,
        [Description("Newer version (reserved; only the latest cached version is currently supported)")] string? versionB = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        var current = CollectConfig(roots);
        var diff = DiffConfig(new Dictionary<string, string?>(), current);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(versionA) || !string.IsNullOrEmpty(versionB))
        {
            sb.AppendLine("Note: explicit version comparison is not yet supported; showing all keys of the latest version.");
        }

        sb.Append(FormatDiff(diff, current));
        return sb.ToString();
    }

    [McpServerTool(Name = "changelog"), Description("Search CHANGELOG / RELEASE_NOTES files in cached source for breaking changes.")]
    public async Task<string> ChangelogAsync(
        GitHubClient github,
        [Description("Optional keyword to filter changelog entries")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return FindChangelog(roots, filter);
    }

    // ---- config parsing ----

    public static IReadOnlyList<ConfigEntry> ParseConfig(IReadOnlyList<string> lines)
    {
        var entries = new List<ConfigEntry>();
        var stack = new Stack<string>();
        var bracketDepth = 0;

        foreach (var raw in lines)
        {
            var line = HoconParser.StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (bracketDepth > 0)
            {
                bracketDepth += line.Count(c => c == '[') - line.Count(c => c == ']');
                continue;
            }

            while (line.StartsWith('}'))
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                line = line[1..].TrimStart();
            }

            if (line.Length == 0)
            {
                continue;
            }

            var m = EntryPattern().Match(line);
            if (!m.Success)
            {
                continue;
            }

            var key = m.Groups["key"].Value.Replace("\"", string.Empty, StringComparison.Ordinal);
            var rest = m.Groups["rest"].Value.Trim();
            var prefix = stack.Count > 0 ? stack.Peek() : string.Empty;
            var full = prefix.Length > 0 ? $"{prefix}.{key}" : key;
            var op = m.Groups["op"].Value;

            if (op == "{" || rest.StartsWith('{'))
            {
                var body = op == "{" ? rest : rest[1..];
                var opens = 1 + body.Count(c => c == '{');
                var closes = body.Count(c => c == '}');
                if (opens - closes <= 0)
                {
                    entries.Add(new ConfigEntry(full, "{ " + body.TrimEnd('}').Trim() + " }"));
                }
                else
                {
                    stack.Push(full);
                }

                continue;
            }

            bracketDepth = Math.Max(0, rest.Count(c => c == '[') - rest.Count(c => c == ']'));
            entries.Add(new ConfigEntry(full, rest));
        }

        return entries;
    }

    public Dictionary<string, string?> CollectConfig(IEnumerable<string> roots)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (full, _) in _fileService.EnumerateConfigFiles(roots))
        {
            foreach (var entry in ParseConfig(_fileService.ReadAllLines(full)))
            {
                result[entry.Path] = entry.Value;
            }
        }

        return result;
    }

    // ---- hosting mapping ----

    public Dictionary<string, SortedSet<string>> CollectHostingKeys(IEnumerable<string> roots)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (full, _) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            string? current = null;
            foreach (var line in _fileService.ReadLines(full))
            {
                var m = MethodPattern().Match(line);
                if (m.Success)
                {
                    current = m.Groups["name"].Value;
                }

                if (current is null)
                {
                    continue;
                }

                foreach (Match k in HoconKeyPattern().Matches(line))
                {
                    if (!map.TryGetValue(current, out var set))
                    {
                        set = new SortedSet<string>(StringComparer.Ordinal);
                        map[current] = set;
                    }

                    set.Add(k.Value);
                }
            }
        }

        return map;
    }

    public string MapQuery(IEnumerable<string> roots, string query)
    {
        var rootList = roots.ToList();
        var scannedMap = CollectHostingKeys(rootList);
        var merged = MergeWithFallback(scannedMap);
        var q = query.Trim();
        var sb = new StringBuilder();

        if (q.StartsWith("akka", StringComparison.Ordinal))
        {
            var config = CollectConfig(rootList);
            sb.AppendLine("| HOCON path | Hosting method |");
            sb.AppendLine("|---|---|");
            var rows = 0;
            foreach (var (method, keys) in merged.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                foreach (var key in keys.Where(k => PathMatches(k, q)))
                {
                    var inRef = config.Keys.Any(c => c == key || c.StartsWith(key + ".", StringComparison.Ordinal)) ? string.Empty : " (not in reference.conf)";
                    sb.AppendLine($"| {key}{inRef} | {method} |");
                    rows++;
                }
            }

            return rows == 0 ? $"No Hosting method found for HOCON path: {q}" : sb.ToString();
        }

        sb.AppendLine("| Hosting method | HOCON keys |");
        sb.AppendLine("|---|---|");
        var found = 0;
        foreach (var (method, keys) in merged.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!method.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var curatedOnly = !scannedMap.ContainsKey(method);
            var label = curatedOnly ? " (curated)" : "";
            sb.AppendLine($"| {method} | {string.Join(", ", keys)}{label} |");
            found++;
        }

        return found == 0 ? $"No HOCON keys found for Hosting method: {q}" : sb.ToString();
    }

    private static Dictionary<string, SortedSet<string>> MergeWithFallback(
        Dictionary<string, SortedSet<string>> scanned)
    {
        var merged = new Dictionary<string, SortedSet<string>>(scanned, StringComparer.Ordinal);
        foreach (var (method, keys) in _fallbackHostingMap)
        {
            if (!merged.TryGetValue(method, out var set))
            {
                set = new SortedSet<string>(StringComparer.Ordinal);
                merged[method] = set;
            }

            foreach (var key in keys)
            {
                set.Add(key);
            }
        }

        return merged;
    }

    private static bool PathMatches(string key, string query) =>
        key == query
        || key.StartsWith(query + ".", StringComparison.Ordinal)
        || query.StartsWith(key + ".", StringComparison.Ordinal);

    // ---- tree ----

    public string BuildConfigTree(IEnumerable<string> roots, string? path)
    {
        var rootList = roots.ToList();
        var config = CollectConfig(rootList);
        var hosting = CollectHostingKeys(rootList);
        var methodsByKey = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (method, keys) in hosting)
        {
            foreach (var key in keys)
            {
                if (!methodsByKey.TryGetValue(key, out var set))
                {
                    set = new SortedSet<string>(StringComparer.Ordinal);
                    methodsByKey[key] = set;
                }

                set.Add(method);
            }
        }

        var paths = config.Keys
            .Where(p => string.IsNullOrWhiteSpace(path) || PathMatches(p, path.Trim()))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (paths.Count == 0)
        {
            return string.IsNullOrWhiteSpace(path) ? "No reference.conf config found." : $"No config found for path: {path}";
        }

        var sb = new StringBuilder();
        var printed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in paths)
        {
            var segments = p.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                var prefix = string.Join('.', segments.Take(i + 1));
                if (!printed.Add(prefix))
                {
                    continue;
                }

                var indent = new string(' ', i * 2);
                var line = indent + segments[i];
                if (i == segments.Length - 1 && config[p] is { Length: > 0 } value)
                {
                    line += $" = {value}";
                }

                if (methodsByKey.TryGetValue(prefix, out var methods))
                {
                    line += $"  [Hosting: {string.Join(", ", methods)}]";
                }

                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    // ---- diff ----

    public static ConfigDiffResult DiffConfig(IReadOnlyDictionary<string, string?> a, IReadOnlyDictionary<string, string?> b)
    {
        var added = b.Keys.Where(k => !a.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var removed = a.Keys.Where(k => !b.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var changed = a.Keys.Where(k => b.TryGetValue(k, out var v) && v != a[k]).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return new ConfigDiffResult(added, removed, changed);
    }

    public static string FormatDiff(ConfigDiffResult diff, IReadOnlyDictionary<string, string?> current)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Added: {diff.Added.Count}, Removed: {diff.Removed.Count}, Changed: {diff.Changed.Count}");
        foreach (var k in diff.Added)
        {
            sb.AppendLine($"+ {k} = {current.GetValueOrDefault(k)}");
        }

        foreach (var k in diff.Removed)
        {
            sb.AppendLine($"- {k}");
        }

        foreach (var k in diff.Changed)
        {
            sb.AppendLine($"~ {k}");
        }

        return sb.ToString();
    }

    // ---- startup order ----

    public string FindStartupOrder(IEnumerable<string> roots, string? filter)
    {
        var constraints = new List<string>();
        var hints = new List<string>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!string.IsNullOrEmpty(filter) && !relative.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = _fileService.ReadAllLines(full);
            var isHosting = relative.Contains("Hosting", StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                var isComment = t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith('*');
                if (isComment && OrderingPattern().IsMatch(t))
                {
                    constraints.Add($"{relative}:{i + 1}: {t.TrimStart('/', '*', ' ')}");
                }
                else if (isHosting && !isComment && hints.Count < 50 && DiHintPattern().IsMatch(t))
                {
                    hints.Add($"{relative}:{i + 1}: {t}");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("Recommended registration order:");
        foreach (var step in _recommendedOrder)
        {
            sb.AppendLine($"  {step}");
        }

        sb.AppendLine();
        sb.AppendLine(constraints.Count == 0 ? "No ordering comments found." : "Ordering constraints found in source:");
        foreach (var c in constraints.Take(50))
        {
            sb.AppendLine($"  {c}");
        }

        if (hints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DI resolution hints:");
            foreach (var h in hints)
            {
                sb.AppendLine($"  {h}");
            }
        }

        return sb.ToString();
    }

    // ---- changelog ----

    public string FindChangelog(IEnumerable<string> roots, string? filter)
    {
        var sb = new StringBuilder();
        var limit = string.IsNullOrWhiteSpace(filter) ? 5 : 10;

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.md"))
        {
            if (!IsChangelogName(_fileService.GetFileName(full)))
            {
                continue;
            }

            var sections = SplitSections(_fileService.ReadAllLines(full));
            if (!string.IsNullOrWhiteSpace(filter))
            {
                sections = [.. sections.Where(s => s.Any(l => l.Contains(filter, StringComparison.OrdinalIgnoreCase)))];
            }

            if (sections.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"--- {relative} ---");
            foreach (var section in sections.Take(limit))
            {
                foreach (var l in section.Take(MaxSectionLines))
                {
                    sb.AppendLine(l);
                }

                sb.AppendLine();
            }
        }

        if (sb.Length == 0)
        {
            return string.IsNullOrWhiteSpace(filter)
                ? "No changelog files found."
                : $"No changelog entries found matching: {filter}";
        }

        return sb.ToString();
    }

    private static bool IsChangelogName(string name) =>
        name.StartsWith("CHANGELOG", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("RELEASE_NOTES", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("RELEASENOTES", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("RELEASE-NOTES", StringComparison.OrdinalIgnoreCase);

    private static List<List<string>> SplitSections(string[] lines)
    {
        var sections = new List<List<string>>();
        List<string>? current = null;
        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                current = [line];
                sections.Add(current);
            }
            else
            {
                current?.Add(line);
            }
        }

        return sections;
    }

}
