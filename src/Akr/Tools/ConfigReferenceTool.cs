using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class ConfigReferenceTool
{
    [McpServerTool(Name = "config_reference"), Description("Look up HOCON config defaults from Akka.NET reference.conf files.")]
    public static async Task<string> LookupConfigAsync(
        GitHubClient github,
        [Description("HOCON path to filter (e.g. akka.persistence, akka.remote). Omit for all config.")] string? path = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return Find(roots, path);
    }

    public static string Find(IEnumerable<string> roots, string? path)
    {
        var sb = new StringBuilder();
        var hasPath = !string.IsNullOrWhiteSpace(path);

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var files = Directory.EnumerateFiles(root, "reference.conf", SearchOption.AllDirectories)
                .Select(f => (Full: f, Relative: Path.GetRelativePath(root, f).Replace('\\', '/')))
                .OrderBy(f => f.Relative, StringComparer.Ordinal);

            foreach (var (full, relative) in files)
            {
                string content;
                if (hasPath)
                {
                    var block = HoconBlockExtractor.Extract(File.ReadAllLines(full), path!);
                    if (block is null)
                    {
                        continue;
                    }

                    content = block;
                }
                else
                {
                    content = File.ReadAllText(full).TrimEnd();
                }

                sb.AppendLine($"--- {relative} ---");
                sb.AppendLine(content);
                sb.AppendLine();
            }
        }

        if (sb.Length == 0)
        {
            return hasPath
                ? $"No config found for path: {path}"
                : "No reference.conf files found.";
        }

        return sb.ToString();
    }
}
