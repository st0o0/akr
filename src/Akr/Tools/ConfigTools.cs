using System.ComponentModel;
using System.Text;
using Akr.Models;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class ConfigTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;
    private readonly HoconScanner _hoconScanner;

    public ConfigTools(FileService fileService, SourcePipeline sourcePipeline, HoconScanner hoconScanner)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
        _hoconScanner = hoconScanner;
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
                var block = HoconParser.ExtractHoconBlock(_fileService.ReadAllLines(full), path!);
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

        return OutputLimit.Truncate(sb.ToString());
    }

    private const int MaxSectionLines = 40;

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
        return OutputLimit.Truncate(BuildConfigTree(roots, path));
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
        var current = _hoconScanner.CollectConfig(roots);
        var diff = DiffConfig(new Dictionary<string, string?>(), current);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(versionA) || !string.IsNullOrEmpty(versionB))
        {
            sb.AppendLine(
                "Note: explicit version comparison is not yet supported; showing all keys of the latest version.");
        }

        sb.Append(FormatDiff(diff, current));
        return OutputLimit.Truncate(sb.ToString());
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
        return OutputLimit.Truncate(FindChangelog(roots, filter));
    }

    public string BuildConfigTree(IEnumerable<string> roots, string? path)
    {
        var rootList = roots.ToList();
        var config = _hoconScanner.CollectConfig(rootList);
        var hosting = _hoconScanner.CollectHostingKeys(rootList);
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
            .Where(p => string.IsNullOrWhiteSpace(path) || HoconParser.PathMatches(p, path.Trim()))
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
        if (diff.Added.Count == 0 && diff.Removed.Count == 0 && diff.Changed.Count == 0)
        {
            sb.AppendLine("No configuration differences detected.");
            return sb.ToString();
        }

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
