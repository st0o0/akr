using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Akr.Tools;

public sealed record ConfigEntry(string Path, string? Value);

public sealed record ConfigDiffResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Changed);

[McpServerToolType]
public sealed class ConfigMigrationTools
{
    private const int MaxSectionLines = 40;

    private static readonly Regex _methodPattern = new(
        @"public\s+static\s+\S+\s+(?<name>(With|Add)\w+)\s*[\(<]",
        RegexOptions.NonBacktracking);

    private static readonly Regex _hoconKeyPattern = new(
        @"\bakka(?:\.[A-Za-z0-9_\-]+)+",
        RegexOptions.NonBacktracking);

    private static readonly Regex _entryPattern = new(
        @"^(?<key>[A-Za-z0-9_\.\-""]+)\s*(?<op>[={:])\s*(?<rest>.*)$",
        RegexOptions.NonBacktracking);

    private static readonly Regex _orderingPattern = new(
        @"(must|should|needs? to|has to)\s+be\s+(called|registered|configured|invoked|set)|(before|after)\s+(calling|registering|configuring)|call(ed)?\s+(this\s+)?(before|after)|requires?\s+(that\s+)?\w*(With|Add)\w+|order\s+(matters|of)",
        RegexOptions.NonBacktracking | RegexOptions.IgnoreCase);

    private static readonly Regex _diHintPattern = new(
        @"GetRequiredService<|GetService<|ActorRegistry|registry\.(Try)?Get<|DependencyResolver",
        RegexOptions.NonBacktracking);

    private static readonly string[] _recommendedOrder =
    [
        "1. Remote / transport (WithRemoting)",
        "2. Cluster (WithClustering, WithClusterBootstrap, Akka.Management)",
        "3. Persistence (WithJournal, WithSnapshotStore, WithSqlPersistence)",
        "4. Sharding / Singleton / Distributed PubSub (WithShardRegion, WithSingleton, WithDistributedPubSub)",
        "5. Actors (WithActors) - last, so ActorRegistry dependencies from earlier steps are available",
    ];

    [McpServerTool(Name = "config_tree"), Description("Hierarchical view of HOCON config paths from reference.conf files, annotated with the Akka.Hosting With* method that sets them.")]
    public static async Task<string> ConfigTreeAsync(
        GitHubClient github,
        [Description("Optional HOCON path prefix to filter (e.g. akka.cluster)")] string? path = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return BuildConfigTree(roots, path);
    }

    [McpServerTool(Name = "hosting_to_hocon"), Description("Bidirectional mapping between Akka.Hosting With*/Add* methods and HOCON config keys.")]
    public static async Task<string> HostingToHoconAsync(
        GitHubClient github,
        [Description("Hosting method (e.g. WithClustering) or HOCON path (e.g. akka.cluster.roles)")] string query,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return MapQuery(roots, query);
    }

    [McpServerTool(Name = "hosting_startup_order"), Description("Registration ordering constraints and DI resolution hints from Akka.Hosting source.")]
    public static async Task<string> StartupOrderAsync(
        GitHubClient github,
        [Description("Optional keyword to filter by file path")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return FindStartupOrder(roots, filter);
    }

    [McpServerTool(Name = "config_diff"), Description("Config key changes between versions. Currently lists all config keys of the current cached version (diff against empty).")]
    public static async Task<string> ConfigDiffAsync(
        GitHubClient github,
        [Description("Older version (reserved; only the latest cached version is currently supported)")] string? versionA = null,
        [Description("Newer version (reserved; only the latest cached version is currently supported)")] string? versionB = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
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
    public static async Task<string> ChangelogAsync(
        GitHubClient github,
        [Description("Optional keyword to filter changelog entries")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
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
            var line = StripComment(raw).Trim();
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

            var m = _entryPattern.Match(line);
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

    public static Dictionary<string, string?> CollectConfig(IEnumerable<string> roots)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "reference.conf", SearchOption.AllDirectories))
            {
                foreach (var entry in ParseConfig(File.ReadAllLines(file)))
                {
                    result[entry.Path] = entry.Value;
                }
            }
        }

        return result;
    }

    // ---- hosting mapping ----

    public static Dictionary<string, SortedSet<string>> CollectHostingKeys(IEnumerable<string> roots)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string? current = null;
                foreach (var line in File.ReadLines(file))
                {
                    var m = _methodPattern.Match(line);
                    if (m.Success)
                    {
                        current = m.Groups["name"].Value;
                    }

                    if (current is null)
                    {
                        continue;
                    }

                    foreach (Match k in _hoconKeyPattern.Matches(line))
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
        }

        return map;
    }

    public static string MapQuery(IEnumerable<string> roots, string query)
    {
        var rootList = roots.ToList();
        var map = CollectHostingKeys(rootList);
        var q = query.Trim();
        var sb = new StringBuilder();

        if (q.StartsWith("akka", StringComparison.Ordinal))
        {
            var config = CollectConfig(rootList);
            sb.AppendLine("| HOCON path | Hosting method |");
            sb.AppendLine("|---|---|");
            var rows = 0;
            foreach (var (method, keys) in map.OrderBy(kv => kv.Key, StringComparer.Ordinal))
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
        foreach (var (method, keys) in map.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (method.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"| {method} | {string.Join(", ", keys)} |");
                found++;
            }
        }

        return found == 0 ? $"No HOCON keys found for Hosting method: {q}" : sb.ToString();
    }

    private static bool PathMatches(string key, string query) =>
        key == query
        || key.StartsWith(query + ".", StringComparison.Ordinal)
        || query.StartsWith(key + ".", StringComparison.Ordinal);

    // ---- tree ----

    public static string BuildConfigTree(IEnumerable<string> roots, string? path)
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

    public static string FindStartupOrder(IEnumerable<string> roots, string? filter)
    {
        var constraints = new List<string>();
        var hints = new List<string>();

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!string.IsNullOrEmpty(filter) && !relative.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                var isHosting = relative.Contains("Hosting", StringComparison.OrdinalIgnoreCase);
                for (var i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    var isComment = t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith('*');
                    if (isComment && _orderingPattern.IsMatch(t))
                    {
                        constraints.Add($"{relative}:{i + 1}: {t.TrimStart('/', '*', ' ')}");
                    }
                    else if (isHosting && !isComment && hints.Count < 50 && _diHintPattern.IsMatch(t))
                    {
                        hints.Add($"{relative}:{i + 1}: {t}");
                    }
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

    public static string FindChangelog(IEnumerable<string> roots, string? filter)
    {
        var sb = new StringBuilder();
        var limit = string.IsNullOrWhiteSpace(filter) ? 5 : 10;

        foreach (var root in roots.Where(Directory.Exists))
        {
            var files = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
                .Where(f => IsChangelogName(Path.GetFileName(f)))
                .OrderBy(f => f, StringComparer.Ordinal);

            foreach (var file in files)
            {
                var sections = SplitSections(File.ReadAllLines(file));
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    sections = [.. sections.Where(s => s.Any(l => l.Contains(filter, StringComparison.OrdinalIgnoreCase)))];
                }

                if (sections.Count == 0)
                {
                    continue;
                }

                sb.AppendLine($"--- {Path.GetRelativePath(root, file).Replace('\\', '/')} ---");
                foreach (var section in sections.Take(limit))
                {
                    foreach (var l in section.Take(MaxSectionLines))
                    {
                        sb.AppendLine(l);
                    }

                    sb.AppendLine();
                }
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

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && (c == '#' || (c == '/' && i + 1 < line.Length && line[i + 1] == '/')))
            {
                return line[..i];
            }
        }

        return line;
    }
}
