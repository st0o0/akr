using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class LookupTypeTool
{
    [McpServerTool(Name = "lookup_type"), Description("Find a C# type by name across cached Akka.NET source code and return its source.")]
    public static async Task<string> LookupAsync(
        GitHubClient github,
        [Description("Type name to look up (e.g. ActorBase, IActorRef, Props)")] string type,
        [Description("Return full file source instead of summary (default: false)")] bool? fullSource = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        var results = TypeFinder.FindType(roots, type, fullSource ?? false);
        if (results.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} match(es) for type '{type}':");
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.File} ---");
            if (!string.IsNullOrEmpty(result.Namespace))
            {
                sb.AppendLine($"Namespace: {result.Namespace}");
            }

            sb.AppendLine($"Declaration: {result.Declaration}");
            sb.AppendLine();
            sb.AppendLine(result.Source);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
