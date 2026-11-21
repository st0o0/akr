using System.Text;
using System.Text.RegularExpressions;

namespace Akr.Tools;

public static partial class TypeFinder
{
    [GeneratedRegex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    public static IReadOnlyList<TypeLookupResult> FindType(
        IReadOnlyList<string> sourceRoots,
        string typeName,
        bool fullSource = false)
    {
        var typePattern = new Regex(
            @$"(?:class|interface|record|struct|enum)\s+{Regex.Escape(typeName)}(?:[\s<({{:,]|$)",
            RegexOptions.NonBacktracking);

        var results = new List<TypeLookupResult>();

        foreach (var root in sourceRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (!typePattern.IsMatch(content))
                {
                    continue;
                }

                var lines = content.Split('\n');
                var ns = ExtractNamespace(lines);
                var declaration = ExtractDeclaration(lines, typePattern);
                var source = fullSource ? content : ExtractSummary(lines, typePattern);
                var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');

                results.Add(new TypeLookupResult(relativePath, ns, declaration, source));
            }
        }

        return results;
    }

    private static string ExtractNamespace(string[] lines)
    {
        foreach (var line in lines)
        {
            var match = NamespacePattern().Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return string.Empty;
    }

    private static string ExtractDeclaration(string[] lines, Regex typePattern)
    {
        foreach (var line in lines)
        {
            if (typePattern.IsMatch(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }

    private static string ExtractSummary(string[] lines, Regex typePattern)
    {
        var sb = new StringBuilder();
        var inType = false;
        var braceDepth = 0;

        foreach (var line in lines)
        {
            if (!inType)
            {
                if (typePattern.IsMatch(line))
                {
                    inType = true;
                    sb.AppendLine(line.TrimEnd());
                    braceDepth += CountChar(line, '{') - CountChar(line, '}');
                }
                else if (line.TrimStart().StartsWith("namespace", StringComparison.Ordinal) ||
                         line.TrimStart().StartsWith("using", StringComparison.Ordinal))
                {
                    sb.AppendLine(line.TrimEnd());
                }

                continue;
            }

            braceDepth += CountChar(line, '{') - CountChar(line, '}');

            if (braceDepth <= 0)
            {
                sb.AppendLine(line.TrimEnd());
                break;
            }

            var trimmed = line.TrimStart();
            if (IsMemberSignature(trimmed))
            {
                sb.AppendLine(line.TrimEnd());
            }
        }

        return sb.ToString();
    }

    private static bool IsMemberSignature(string line) =>
        line.StartsWith("public", StringComparison.Ordinal) ||
        line.StartsWith("protected", StringComparison.Ordinal) ||
        line.StartsWith("internal", StringComparison.Ordinal) ||
        line.StartsWith("private", StringComparison.Ordinal) ||
        line.StartsWith("static", StringComparison.Ordinal) ||
        line.StartsWith("abstract", StringComparison.Ordinal) ||
        line.StartsWith("virtual", StringComparison.Ordinal) ||
        line.StartsWith("override", StringComparison.Ordinal) ||
        line.StartsWith("event", StringComparison.Ordinal) ||
        line.StartsWith("//", StringComparison.Ordinal) ||
        line.StartsWith("///", StringComparison.Ordinal) ||
        line.StartsWith("[", StringComparison.Ordinal) ||
        line.StartsWith("}", StringComparison.Ordinal);

    private static int CountChar(string s, char c)
    {
        var count = 0;
        foreach (var ch in s)
        {
            if (ch == c)
            {
                count++;
            }
        }

        return count;
    }
}
