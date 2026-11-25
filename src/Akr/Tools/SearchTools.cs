using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class SearchTools
{
    private readonly SourcePipeline _sourcePipeline;
    private readonly SourceSearcher _sourceSearcher;

    public SearchTools(SourcePipeline sourcePipeline, SourceSearcher sourceSearcher)
    {
        _sourcePipeline = sourcePipeline;
        _sourceSearcher = sourceSearcher;
    }

    [McpServerTool(Name = "search_source"), Description("Search across cached Akka.NET source code using regex patterns.")]
    public async Task<string> SearchAsync(
        [Description("Regex pattern to search for")] string query,
        [Description("File glob pattern (default: *.cs)")] string? filePattern = null,
        [Description("Maximum results to return (default: 20)")] int? maxResults = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
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
}
