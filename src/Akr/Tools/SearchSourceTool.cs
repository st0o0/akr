using System.ComponentModel;
using System.Text;
using Akr.Core;
using Akr.Core.Tools;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class SearchSourceTool
{
    [McpServerTool(Name = "search_source"), Description("Search across cached Akka.NET source code using regex patterns.")]
    public static async Task<string> SearchAsync(
        ISourceProvider sourceProvider,
        [Description("Regex pattern to search for")] string query,
        [Description("File glob pattern (default: *.cs)")] string? filePattern = null,
        [Description("Maximum results to return (default: 20)")] int? maxResults = null,
        [Description("Version override for source fetching")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        await sourceProvider.EnsureAllConfiguredAsync(cancellationToken);

        var roots = await sourceProvider.GetAllCachedSourceRootsAsync(cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source cached. Configure AKKA_VERSIONS environment variable.";
        }

        var results = SourceSearcher.Search(roots, query, filePattern ?? "*.cs", maxResults ?? 20);
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
