using System.Text.RegularExpressions;

namespace Akr.Tools;

public sealed record ExtensionFileResult(string File, IReadOnlyList<string> Signatures);

public static class ExtensionMethodFinder
{
    private static readonly Regex _methodPattern = new(
        @"public\s+static\s+\S+\s+(With|Add)\w+\s*[\(<]",
        RegexOptions.NonBacktracking);

    public static IReadOnlyList<ExtensionFileResult> Find(IEnumerable<string> roots, string? filter = null)
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
            if (!_methodPattern.IsMatch(lines[i]))
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
}
