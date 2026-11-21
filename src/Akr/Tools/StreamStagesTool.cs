using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class StreamStagesTool
{
    [McpServerTool(Name = "stream_stages"), Description("Catalog built-in Akka.Streams stages with shape classification.")]
    public static async Task<string> ListStagesAsync(
        GitHubClient github,
        [Description("Optional keyword to filter stages (e.g. fan, merge, broadcast)")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return StageFinder.Format(StageFinder.FindStages(roots, filter), filter);
    }
}
