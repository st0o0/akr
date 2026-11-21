using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class HostingExtensionsTool
{
    [McpServerTool(Name = "hosting_extensions"), Description("Search for With*/Add* extension methods in Akka.Hosting source.")]
    public static async Task<string> SearchExtensionsAsync(
        GitHubClient github,
        [Description("Optional keyword to filter by file path (e.g. Persistence, Cluster)")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return Format(ExtensionMethodFinder.Find(roots, filter), filter);
    }

    public static string Format(IReadOnlyList<ExtensionFileResult> results, string? filter)
    {
        if (results.Count == 0)
        {
            return string.IsNullOrEmpty(filter)
                ? "No hosting extensions found."
                : $"No hosting extensions found matching filter: {filter}";
        }

        var sb = new StringBuilder();
        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.File} ---");
            foreach (var sig in result.Signatures)
            {
                sb.AppendLine($"  {sig}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
