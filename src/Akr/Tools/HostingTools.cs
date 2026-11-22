using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Akr.Tools;

public sealed record ExtensionFileResult(string File, IReadOnlyList<string> Signatures);

[McpServerToolType]
public sealed partial class HostingTools
{
    [McpServerTool(Name = "hosting_extensions"), Description("Search for With*/Add* extension methods in Akka.Hosting source.")]
    public static async Task<string> SearchExtensionsAsync(
        GitHubClient github,
        [Description("Optional keyword to filter by file path (e.g. Persistence, Cluster)")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return Format(FindExtensions(roots, filter), filter);
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

    [McpServerTool(Name = "config_reference"), Description("Look up HOCON config defaults from Akka.NET reference.conf files.")]
    public static async Task<string> LookupConfigAsync(
        GitHubClient github,
        [Description("HOCON path to filter (e.g. akka.persistence, akka.remote). Omit for all config.")] string? path = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return FindConfig(roots, path);
    }

    public static string FindConfig(IEnumerable<string> roots, string? path)
    {
        var sb = new StringBuilder();
        var hasPath = !string.IsNullOrWhiteSpace(path);

        var files = ConfigFiles.EnumerateConfigFiles(roots)
            .OrderBy(f => f.Relative, StringComparer.Ordinal);

        foreach (var (full, relative) in files)
        {
            string content;
            if (hasPath)
            {
                var block = ExtractHoconBlock(File.ReadAllLines(full), path!);
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

        if (sb.Length == 0)
        {
            return hasPath
                ? $"No config found for path: {path}"
                : "No reference.conf files found.";
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"public\s+static\s+\S+\s+(With|Add)\w+\s*[\(<]", RegexOptions.NonBacktracking)]
    private static partial Regex MethodPattern();

    public static IReadOnlyList<ExtensionFileResult> FindExtensions(IEnumerable<string> roots, string? filter = null)
    {
        var results = new List<ExtensionFileResult>();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!string.IsNullOrEmpty(filter) && !relative.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var signatures = ExtractSignatures(File.ReadAllLines(file));
                if (signatures.Count > 0)
                {
                    results.Add(new ExtensionFileResult(relative, signatures));
                }
            }
        }

        return results.OrderBy(r => r.File, StringComparer.Ordinal).ToList();
    }

    public static List<string> ExtractSignatures(string[] lines)
    {
        var signatures = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!MethodPattern().IsMatch(lines[i]))
            {
                continue;
            }

            var sig = lines[i].Trim();
            var j = i;
            while (!IsTerminated(sig) && j + 1 < lines.Length && j - i < 20)
            {
                j++;
                sig += " " + lines[j].Trim();
            }

            sig = Truncate(sig);
            sig = Regex.Replace(sig, @"\s+", " ", RegexOptions.NonBacktracking).Trim();
            if (sig.StartsWith("public ", StringComparison.Ordinal))
            {
                sig = sig["public ".Length..];
            }

            signatures.Add(sig);
        }

        return signatures;
    }

    private static bool IsTerminated(string sig) => sig.Contains('{') || sig.Contains("=>") || sig.Contains(';');

    private static string Truncate(string sig)
    {
        var end = sig.Length;
        foreach (var marker in new[] { "{", "=>", ";" })
        {
            var idx = sig.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0 && idx < end)
            {
                end = idx;
            }
        }

        return sig[..end];
    }

    public static string? ExtractHoconBlock(IReadOnlyList<string> lines, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var index = 0;
        var remaining = segments.Length;
        var keyLine = -1;

        while (remaining > 0)
        {
            var found = false;
            for (var take = remaining; take >= 1 && !found; take--)
            {
                var key = string.Join('.', segments.Skip(segments.Length - remaining).Take(take));
                var match = FindKeyLine(lines, index, key);
                if (match >= 0)
                {
                    keyLine = match;
                    index = match + 1;
                    remaining -= take;
                    found = true;
                }
            }

            if (!found)
            {
                return null;
            }
        }

        return CollectBlock(lines, keyLine);
    }

    private static int FindKeyLine(IReadOnlyList<string> lines, int start, string key)
    {
        for (var i = start; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Length == key.Length)
            {
                return i;
            }

            var next = trimmed[key.Length];
            if (char.IsWhiteSpace(next) || next is '{' or '=' or ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static string CollectBlock(IReadOnlyList<string> lines, int keyLine)
    {
        var collected = new List<string>();
        var depth = 0;
        var opened = false;

        for (var i = keyLine; i < lines.Count; i++)
        {
            collected.Add(lines[i]);
            foreach (var c in HoconHelper.StripComment(lines[i]))
            {
                if (c == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (c == '}')
                {
                    depth--;
                }
            }

            if (depth <= 0 && (opened || i == keyLine))
            {
                break;
            }
        }

        return string.Join('\n', collected);
    }

}
