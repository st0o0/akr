using System.ComponentModel;
using System.Text;
using Akr.Models;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class VersionTools
{
    private readonly FileService _fileService;
    private readonly DocsIndex _docsIndex;
    private readonly SourcePipeline _sourcePipeline;

    public VersionTools(FileService fileService, DocsIndex docsIndex, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _docsIndex = docsIndex;
        _sourcePipeline = sourcePipeline;
    }

    [McpServerTool(Name = "source_diff"), Description("Compare a type's source code or config between two Akka.NET versions. Returns a unified diff.")]
    public async Task<string> SourceDiffAsync(
        [Description("Type name to diff (e.g. 'Throttle', 'ActorBase'). Mutually exclusive with configPath.")] string? typeName = null,
        [Description("Config path to diff (e.g. 'akka.cluster.sharding'). Mutually exclusive with typeName.")] string? configPath = null,
        [Description("First version to compare (e.g. '1.5.0')")] string? versionA = null,
        [Description("Second version to compare (e.g. '1.5.74')")] string? versionB = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(versionA) || string.IsNullOrEmpty(versionB))
        {
            return "Both versionA and versionB are required.";
        }

        if (string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(configPath))
        {
            return "Either typeName or configPath is required.";
        }

        var rootsA = await _sourcePipeline.EnsureAllForVersionAsync(versionA, cancellationToken);
        var rootsB = await _sourcePipeline.EnsureAllForVersionAsync(versionB, cancellationToken);

        if (typeName is not null)
        {
            return OutputLimit.Truncate(DiffType(rootsA, rootsB, typeName, versionA, versionB));
        }

        return OutputLimit.Truncate(DiffConfig(rootsA, rootsB, configPath!, versionA, versionB));
    }

    [McpServerTool(Name = "migration_guide"), Description("Generate a migration guide for upgrading between two Akka.NET versions. Combines changelog, config changes, and relevant documentation.")]
    public async Task<string> MigrationGuideAsync(
        [Description("Version to migrate from (e.g. '1.5.0')")] string fromVersion,
        [Description("Version to migrate to (e.g. '1.5.74')")] string toVersion,
        [Description("Optional area to focus on (e.g. 'persistence', 'cluster', 'streams')")] string? area = null,
        CancellationToken cancellationToken = default)
    {
        var rootsFrom = await _sourcePipeline.EnsureAllForVersionAsync(fromVersion, cancellationToken);
        var rootsTo = await _sourcePipeline.EnsureAllForVersionAsync(toVersion, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"# Migration Guide: {fromVersion} → {toVersion}");
        if (area is not null)
        {
            sb.AppendLine($"Focus: {area}");
        }

        sb.AppendLine();

        var changelogSection = ExtractChangelog(rootsTo, fromVersion, toVersion, area);
        sb.AppendLine("## Changelog");
        sb.AppendLine();
        sb.AppendLine(changelogSection);

        var configDiff = DiffAllConfigs(rootsFrom, rootsTo, fromVersion, toVersion, area);
        if (!string.IsNullOrEmpty(configDiff))
        {
            sb.AppendLine("## Configuration Changes");
            sb.AppendLine();
            sb.AppendLine(configDiff);
        }

        try
        {
            // DocsIndex may already be built by a prior docs_search call
            var searchQuery = area ?? "migration upgrade";
            var docs = _docsIndex.Search(searchQuery, maxResults: 3);
            if (docs.Count > 0)
            {
                sb.AppendLine("## Relevant Documentation");
                sb.AppendLine();
                foreach (var doc in docs)
                {
                    sb.AppendLine($"### {doc.Heading} ({doc.FileName})");
                    sb.AppendLine();
                    sb.AppendLine(TruncateContent(doc.Excerpt, 1000));
                    sb.AppendLine();
                }
            }
        }
        catch (SourceFetchException)
        {
            sb.AppendLine("## Documentation");
            sb.AppendLine();
            sb.AppendLine("Documentation source unavailable.");
        }

        return OutputLimit.Truncate(sb.ToString());
    }

    private string DiffType(IReadOnlyList<string> rootsA, IReadOnlyList<string> rootsB, string typeName, string versionA, string versionB)
    {
        var resultsA = TypeTools.FindType(_fileService, rootsA, typeName, fullSource: true);
        var resultsB = TypeTools.FindType(_fileService, rootsB, typeName, fullSource: true);

        if (resultsA.Count == 0 && resultsB.Count == 0)
        {
            return $"Type '{typeName}' not found in either version.";
        }

        if (resultsA.Count == 0)
        {
            return $"Type '{typeName}' was added in {versionB} (not present in {versionA}).\n\n" +
                   FormatTypeResults(resultsB);
        }

        if (resultsB.Count == 0)
        {
            return $"Type '{typeName}' was removed in {versionB} (present in {versionA}).\n\n" +
                   FormatTypeResults(resultsA);
        }

        var sourceA = string.Join("\n", resultsA.Select(r => r.Source));
        var sourceB = string.Join("\n", resultsB.Select(r => r.Source));

        if (string.Equals(sourceA, sourceB, StringComparison.Ordinal))
        {
            return $"No changes to type '{typeName}' between {versionA} and {versionB}.";
        }

        var diff = GenerateUnifiedDiff(sourceA, sourceB, $"{typeName} ({versionA})", $"{typeName} ({versionB})");
        if (diff.Length > 10_000)
        {
            diff = diff[..10_000] + "\n[diff truncated at 10000 chars]";
        }

        return diff;
    }

    private string DiffConfig(IReadOnlyList<string> rootsA, IReadOnlyList<string> rootsB, string configPath, string versionA, string versionB)
    {
        var configsA = CollectConfigs(rootsA);
        var configsB = CollectConfigs(rootsB);

        var relevantA = configsA.Where(c => c.Contains(configPath, StringComparison.OrdinalIgnoreCase)).ToList();
        var relevantB = configsB.Where(c => c.Contains(configPath, StringComparison.OrdinalIgnoreCase)).ToList();

        var contentA = string.Join("\n", relevantA);
        var contentB = string.Join("\n", relevantB);

        if (string.IsNullOrEmpty(contentA) && string.IsNullOrEmpty(contentB))
        {
            return $"Config path '{configPath}' not found in either version.";
        }

        if (string.Equals(contentA, contentB, StringComparison.Ordinal))
        {
            return $"No changes to config '{configPath}' between {versionA} and {versionB}.";
        }

        return GenerateUnifiedDiff(contentA, contentB, $"config ({versionA})", $"config ({versionB})");
    }

    private string DiffAllConfigs(IReadOnlyList<string> rootsFrom, IReadOnlyList<string> rootsTo, string versionFrom, string versionTo, string? area)
    {
        var configsFrom = CollectConfigs(rootsFrom);
        var configsTo = CollectConfigs(rootsTo);

        var contentFrom = string.Join("\n", area is not null
            ? configsFrom.Where(c => c.Contains(area, StringComparison.OrdinalIgnoreCase))
            : configsFrom);
        var contentTo = string.Join("\n", area is not null
            ? configsTo.Where(c => c.Contains(area, StringComparison.OrdinalIgnoreCase))
            : configsTo);

        if (string.Equals(contentFrom, contentTo, StringComparison.Ordinal))
        {
            return "No configuration changes detected.";
        }

        return GenerateUnifiedDiff(contentFrom, contentTo, $"config ({versionFrom})", $"config ({versionTo})");
    }

    private List<string> CollectConfigs(IReadOnlyList<string> roots)
    {
        var configs = new List<string>();
        foreach (var root in roots)
        {
            foreach (var (fullPath, _) in _fileService.EnumerateConfigFiles([root]))
            {
                configs.Add(Encoding.UTF8.GetString(_fileService.ReadAllBytes(fullPath)));
            }
        }

        return configs;
    }

    private string ExtractChangelog(IReadOnlyList<string> roots, string fromVersion, string toVersion, string? area)
    {
        var sb = new StringBuilder();
        var found = false;

        foreach (var root in roots)
        {
            foreach (var (fullPath, relativePath) in _fileService.EnumerateFiles([root], "RELEASE_NOTES.md"))
            {
                var content = Encoding.UTF8.GetString(_fileService.ReadAllBytes(fullPath));
                var section = ExtractVersionRange(content, fromVersion, toVersion);
                if (!string.IsNullOrEmpty(section))
                {
                    if (area is not null)
                    {
                        var filtered = FilterByArea(section, area);
                        if (!string.IsNullOrEmpty(filtered))
                        {
                            sb.AppendLine($"### {relativePath}");
                            sb.AppendLine(filtered);
                            found = true;
                        }
                    }
                    else
                    {
                        sb.AppendLine($"### {relativePath}");
                        sb.AppendLine(section);
                        found = true;
                    }
                }
            }

            foreach (var (fullPath, relativePath) in _fileService.EnumerateFiles([root], "CHANGELOG.md"))
            {
                var content = Encoding.UTF8.GetString(_fileService.ReadAllBytes(fullPath));
                var section = ExtractVersionRange(content, fromVersion, toVersion);
                if (!string.IsNullOrEmpty(section))
                {
                    sb.AppendLine($"### {relativePath}");
                    sb.AppendLine(area is not null ? FilterByArea(section, area) : section);
                    found = true;
                }
            }
        }

        return found ? sb.ToString() : "No changelog entries found between these versions.";
    }

    private static string ExtractVersionRange(string content, string fromVersion, string toVersion)
    {
        var lines = content.Split('\n');
        var sb = new StringBuilder();
        var capturing = false;

        foreach (var line in lines)
        {
            if (line.StartsWith('#') && line.Contains(toVersion, StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
            }

            if (capturing && line.StartsWith('#') && line.Contains(fromVersion, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (capturing)
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString().Trim();
    }

    private static string FilterByArea(string content, string area)
    {
        var lines = content.Split('\n');
        var filtered = lines.Where(l =>
            l.Contains(area, StringComparison.OrdinalIgnoreCase) ||
            l.StartsWith('#'));
        return string.Join('\n', filtered).Trim();
    }

    private static string FormatTypeResults(IReadOnlyList<TypeLookupResult> results)
    {
        var sb = new StringBuilder();
        foreach (var r in results)
        {
            sb.AppendLine($"--- {r.File} ---");
            sb.AppendLine(r.Source);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateUnifiedDiff(string textA, string textB, string labelA, string labelB)
    {
        var linesA = textA.Split('\n');
        var linesB = textB.Split('\n');

        var sb = new StringBuilder();
        sb.AppendLine($"--- {labelA}");
        sb.AppendLine($"+++ {labelB}");

        var setA = new HashSet<string>(linesA);
        var setB = new HashSet<string>(linesB);

        var removed = linesA.Where(l => !setB.Contains(l)).ToList();
        var added = linesB.Where(l => !setA.Contains(l)).ToList();

        if (removed.Count > 0)
        {
            foreach (var line in removed)
            {
                sb.AppendLine($"-{line}");
            }
        }

        if (added.Count > 0)
        {
            foreach (var line in added)
            {
                sb.AppendLine($"+{line}");
            }
        }

        return sb.ToString();
    }

    private static string TruncateContent(string content, int maxChars)
    {
        if (content.Length <= maxChars)
        {
            return content;
        }

        return content[..maxChars] + "\n[truncated]";
    }
}
