using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class TypeMembersTool
{
    [McpServerTool(Name = "type_members"), Description("Get compact member signatures for a C# type in Akka.NET source.")]
    public static async Task<string> GetMembersAsync(
        GitHubClient github,
        [Description("Type name (e.g. ActorBase, Props, IActorRef)")] string type,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        var results = TypeFinder.FindType(roots, type, fullSource: true);
        if (results.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source.";
        }

        var members = results
            .SelectMany(r => MemberExtractor.Extract(r.Source, type))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(MemberExtractor.FormatHeader(results[0].Declaration, type));
        sb.AppendLine();
        sb.Append(members.Count == 0 ? "No members found." : MemberExtractor.Format(members));
        return sb.ToString();
    }
}
