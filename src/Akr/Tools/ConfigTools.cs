using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class ConfigTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;

    public ConfigTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

    [McpServerTool(Name = "config_reference"), Description("Look up HOCON config defaults from Akka.NET reference.conf files.")]
    public async Task<string> LookupConfigAsync(
        [Description("HOCON path to filter (e.g. akka.persistence, akka.remote). Omit for all config.")] string? path = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindConfig(roots, path);
    }

    public string FindConfig(IEnumerable<string> roots, string? path)
    {
        var sb = new StringBuilder();
        var hasPath = !string.IsNullOrWhiteSpace(path);

        var files = _fileService.EnumerateConfigFiles(roots)
            .OrderBy(f => f.Relative, StringComparer.Ordinal);

        foreach (var (full, relative) in files)
        {
            string content;
            if (hasPath)
            {
                var block = ExtractHoconBlock(_fileService.ReadAllLines(full), path!);
                if (block is null)
                {
                    continue;
                }

                content = block;
            }
            else
            {
                content = _fileService.ReadAllText(full).TrimEnd();
            }

            sb.AppendLine($"--- {relative} ---");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        if (sb.Length == 0)
        {
            return hasPath
                ? $"No config found for path: {path}"
                : "No reference.conf files found.";
        }

        return sb.ToString();
    }

    public static string? ExtractHoconBlock(IReadOnlyList<string> lines, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var index = 0;
        var remaining = segments.Length;
        var keyLine = -1;

        while (remaining > 0)
        {
            var found = false;
            for (var take = remaining; take >= 1 && !found; take--)
            {
                var key = string.Join('.', segments.Skip(segments.Length - remaining).Take(take));
                var match = FindKeyLine(lines, index, key);
                if (match >= 0)
                {
                    keyLine = match;
                    index = match + 1;
                    remaining -= take;
                    found = true;
                }
            }

            if (!found)
            {
                return null;
            }
        }

        return CollectBlock(lines, keyLine);
    }

    private static int FindKeyLine(IReadOnlyList<string> lines, int start, string key)
    {
        for (var i = start; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Length == key.Length)
            {
                return i;
            }

            var next = trimmed[key.Length];
            if (char.IsWhiteSpace(next) || next is '{' or '=' or ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static string CollectBlock(IReadOnlyList<string> lines, int keyLine)
    {
        var collected = new List<string>();
        var depth = 0;
        var opened = false;

        for (var i = keyLine; i < lines.Count; i++)
        {
            collected.Add(lines[i]);
            foreach (var c in HoconParser.StripComment(lines[i]))
            {
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

            if (depth <= 0 && (opened || i == keyLine))
            {
                break;
            }
        }

        return string.Join('\n', collected);
    }

    private const int MaxSectionLines = 40;

    [GeneratedRegex(@"^(?<key>[A-Za-z0-9_\.\-""]+)\s*(?<op>[={:])\s*(?<rest>.*)$", RegexOptions.NonBacktracking)]
    private static partial Regex EntryPattern();

    [McpServerTool(Name = "config_tree"),
     Description(
         "Hierarchical view of HOCON config paths from reference.conf files, annotated with the Akka.Hosting With* method that sets them.")]
    public async Task<string> ConfigTreeAsync(
        [Description("Optional HOCON path prefix to filter (e.g. akka.cluster)")]
        string? path = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")]
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return BuildConfigTree(roots, path);
    }

    [McpServerTool(Name = "config_diff"),
     Description(
         "Config key changes between versions. Currently lists all config keys of the current cached version (diff against empty).")]
    public async Task<string> ConfigDiffAsync(
        [Description("Older version (reserved; only the latest cached version is currently supported)")]
        string? versionA = null,
        [Description("Newer version (reserved; only the latest cached version is currently supported)")]
        string? versionB = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")]
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        var current = CollectConfig(roots);
        var diff = DiffConfig(new Dictionary<string, string?>(), current);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(versionA) || !string.IsNullOrEmpty(versionB))
        {
            sb.AppendLine(
                "Note: explicit version comparison is not yet supported; showing all keys of the latest version.");
        }

        sb.Append(FormatDiff(diff, current));
        return sb.ToString();
    }

    [McpServerTool(Name = "changelog"),
     Description("Search CHANGELOG / RELEASE_NOTES files in cached source for breaking changes.")]
    public async Task<string> ChangelogAsync(
        [Description("Optional keyword to filter changelog entries")]
        string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")]
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindChangelog(roots, filter);
    }

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

    [GeneratedRegex(@"public\s+static\s+\S+\s+(?<name>(With|Add)\w+)\s*[\(<]", RegexOptions.NonBacktracking)]
    private static partial Regex NamedMethodPattern();

    [GeneratedRegex(@"\bakka(?:\.[A-Za-z0-9_\-]+)+", RegexOptions.NonBacktracking)]
    private static partial Regex HoconKeyPattern();



    public Dictionary<string, SortedSet<string>> CollectHostingKeys(IEnumerable<string> roots)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (full, _) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            string? current = null;
            foreach (var line in _fileService.ReadLines(full))
            {
                var m = NamedMethodPattern().Match(line);
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
            return string.IsNullOrWhiteSpace(path)
                ? "No reference.conf config found."
                : $"No config found for path: {path}";
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

    public static ConfigDiffResult DiffConfig(IReadOnlyDictionary<string, string?> a,
        IReadOnlyDictionary<string, string?> b)
    {
        var added = b.Keys.Where(k => !a.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var removed = a.Keys.Where(k => !b.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var changed = a.Keys.Where(k => b.TryGetValue(k, out var v) && v != a[k])
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
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
