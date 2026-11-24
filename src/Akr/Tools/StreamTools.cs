using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class StreamTools
{
    private readonly FileService _fileService;
    private readonly SourceSearcher _sourceSearcher;

    public StreamTools(FileService fileService, SourceSearcher sourceSearcher)
    {
        _fileService = fileService;
        _sourceSearcher = sourceSearcher;
    }

    [McpServerTool(Name = "search_source"), Description("Search across cached Akka.NET source code using regex patterns.")]
    public async Task<string> SearchAsync(
        GitHubClient github,
        [Description("Regex pattern to search for")] string query,
        [Description("File glob pattern (default: *.cs)")] string? filePattern = null,
        [Description("Maximum results to return (default: 20)")] int? maxResults = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        IReadOnlyList<SearchResult> results;
        try
        {
            results = _sourceSearcher.Search(roots, query, filePattern ?? "*.cs", maxResults ?? 20);
        }
        catch (RegexParseException ex)
        {
            return $"Invalid regex pattern: {ex.Message}";
        }

        if (results.Count == 0)
        {
            return $"No matches found for pattern: {query}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} match(es):");
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.File}:{result.LineNumber} ---");
            foreach (var line in result.ContextBefore)
            {
                sb.AppendLine($"  {line}");
            }

            sb.AppendLine($"> {result.MatchingLine}");
            foreach (var line in result.ContextAfter)
            {
                sb.AppendLine($"  {line}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "stream_stages"), Description("Catalog built-in Akka.Streams stages with shape classification.")]
    public async Task<string> ListStagesAsync(
        GitHubClient github,
        [Description("Optional keyword to filter stages (e.g. fan, merge, broadcast)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return Format(FindStages(roots, filter), filter);
    }

    public static readonly IReadOnlyList<string> Categories = ["Source", "Flow", "Sink", "Fan-In", "Fan-Out", "Custom"];

    [GeneratedRegex(@"\bclass\s+(?<name>\w+)\s*(?<generics><[^>\r\n]*>)?\s*:\s*(?<bases>[^\r\n{]+)", RegexOptions.NonBacktracking)]
    private static partial Regex ClassPattern();

    private static readonly string[] _stageBases =
    [
        "GraphStage<",
        "GraphStageWithMaterializedValue<",
        "SimpleLinearGraphStage<",
        "PushStage<",
        "PushPullStage<",
    ];

    public IReadOnlyList<StageInfo> FindStages(IEnumerable<string> roots, string? filter = null)
    {
        var found = new HashSet<StageInfo>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!relative.Contains("Akka.Streams", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var stage in ExtractStages(_fileService.ReadAllText(full)))
            {
                found.Add(stage);
            }
        }

        var query = found.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(s =>
                s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(s => CategoryIndex(s.Category))
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static IEnumerable<StageInfo> ExtractStages(string source)
    {
        foreach (Match m in ClassPattern().Matches(source))
        {
            var bases = m.Groups["bases"].Value;
            if (!_stageBases.Any(b => bases.Contains(b, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return new StageInfo(m.Groups["name"].Value + m.Groups["generics"].Value, Classify(bases));
        }
    }

    public static string Classify(string bases)
    {
        if (bases.Contains("FanInShape", StringComparison.Ordinal))
        {
            return "Fan-In";
        }

        if (bases.Contains("FanOutShape", StringComparison.Ordinal))
        {
            return "Fan-Out";
        }

        if (bases.Contains("SourceShape<", StringComparison.Ordinal))
        {
            return "Source";
        }

        if (bases.Contains("SinkShape<", StringComparison.Ordinal))
        {
            return "Sink";
        }

        if (bases.Contains("FlowShape<", StringComparison.Ordinal) ||
            bases.Contains("SimpleLinearGraphStage<", StringComparison.Ordinal) ||
            bases.Contains("PushStage<", StringComparison.Ordinal) ||
            bases.Contains("PushPullStage<", StringComparison.Ordinal))
        {
            return "Flow";
        }

        return "Custom";
    }

    public static string Format(IReadOnlyList<StageInfo> stages, string? filter)
    {
        if (stages.Count == 0)
        {
            return string.IsNullOrEmpty(filter)
                ? "No stream stages found."
                : $"No stream stages found matching filter: {filter}";
        }

        var sb = new StringBuilder();
        foreach (var group in stages.GroupBy(s => s.Category))
        {
            sb.AppendLine($"{group.Key}:");
            foreach (var stage in group)
            {
                sb.AppendLine($"  {stage.Name}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int CategoryIndex(string category)
    {
        for (var i = 0; i < Categories.Count; i++)
        {
            if (Categories[i] == category)
            {
                return i;
            }
        }

        return Categories.Count;
    }
}
