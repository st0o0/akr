using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;

namespace Akr.Scanning;

public sealed partial class TypeScanner
{
    private readonly Dictionary<string, List<TypeDecl>> _scanCache = new(StringComparer.Ordinal);
    private readonly FileService _fileService;

    public TypeScanner(FileService fileService)
    {
        _fileService = fileService;
    }

    public List<TypeDecl> ScanTypes(IReadOnlyList<string> roots)
    {
        var key = string.Join('\n', roots.OrderBy(r => r, StringComparer.Ordinal));
        if (_scanCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var list = new List<TypeDecl>();
        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            var content = _fileService.ReadAllText(full);
            var source = new SourceFile(relative, content);
            var ns = NamespacePattern().Match(content) is { Success: true } m ? m.Groups[1].Value : "";
            foreach (Match d in DeclPattern().Matches(content))
            {
                list.Add(new TypeDecl(d.Groups[2].Value, d.Groups[1].Value, ns, ParseBases(d.Groups[3].Value), source, d.Index));
            }
        }

        _scanCache.TryAdd(key, list);
        return list;
    }

    public const int MaxClassLines = 200;

    public static bool HasBase(string bases, string[] names)
    {
        foreach (var part in bases.Split(','))
        {
            var name = part.Trim();
            var lt = name.IndexOf('<');
            if (lt >= 0)
            {
                name = name[..lt];
            }

            if (names.Contains(name[(name.LastIndexOf('.') + 1)..]))
            {
                return true;
            }
        }

        return false;
    }

    public static List<string> CollectBlockLines(string[] lines, int start, int maxLines = int.MaxValue)
    {
        var collected = new List<string>();
        var depth = 0;
        var opened = false;

        for (var i = start; i < lines.Length && collected.Count < maxLines; i++)
        {
            collected.Add(lines[i]);
            foreach (var c in lines[i])
            {
                if (c == '#')
                {
                    break;
                }

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

            if (opened && depth <= 0)
            {
                break;
            }

            if (!opened && i > start + 3)
            {
                break;
            }
        }

        return collected;
    }

    public static string? ClassifyTestKit(string relative)
    {
        if (relative.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (relative.Contains("Akka.Hosting.TestKit", StringComparison.OrdinalIgnoreCase))
        {
            return "Akka.Hosting.TestKit";
        }

        return relative.Contains("Akka.TestKit", StringComparison.OrdinalIgnoreCase) ? "Akka.TestKit" : null;
    }

    public static string ExtractBody(string content, int declIndex)
    {
        var open = -1;
        for (var i = declIndex; i < content.Length; i++)
        {
            if (content[i] == ';')
            {
                return "";
            }

            if (content[i] == '{')
            {
                open = i;
                break;
            }
        }

        if (open < 0)
        {
            return "";
        }

        var depth = 0;
        for (var i = open; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                depth++;
            }
            else if (content[i] == '}' && --depth == 0)
            {
                return content[open..(i + 1)];
            }
        }

        return content[open..];
    }

    public static string BaseName(string type)
    {
        var t = type.Trim();
        var generic = t.IndexOf('<');
        if (generic >= 0)
        {
            t = t[..generic];
        }

        var dot = t.LastIndexOf('.');
        return dot >= 0 ? t[(dot + 1)..] : t;
    }

    public static IReadOnlyList<string> ParseBases(string rest)
    {
        var s = LineComment().Replace(rest, "");
        var i = SkipWhitespace(s, 0);
        if (i < s.Length && s[i] == '<')
        {
            i = SkipBalanced(s, i, '<', '>');
        }

        i = SkipWhitespace(s, i);
        if (i < s.Length && s[i] == '(')
        {
            i = SkipBalanced(s, i, '(', ')');
        }

        i = SkipWhitespace(s, i);
        if (i >= s.Length || s[i] != ':')
        {
            return [];
        }

        var list = s[(i + 1)..];
        var where = WhereKeyword().Match(list);
        if (where.Success)
        {
            list = list[..where.Index];
        }

        var result = new List<string>();
        var depth = 0;
        var current = new StringBuilder();
        foreach (var ch in list)
        {
            if (ch is '<' or '(')
            {
                depth++;
            }
            else if (ch is '>' or ')')
            {
                depth--;
            }

            if (ch == ',' && depth == 0)
            {
                AddBase(result, current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        AddBase(result, current.ToString());
        return result;
    }

    private static void AddBase(List<string> result, string raw)
    {
        var text = raw.Trim();
        var paren = text.IndexOf('(');
        if (paren >= 0)
        {
            text = text[..paren].Trim();
        }

        if (text.Length > 0)
        {
            result.Add(text);
        }
    }

    private static int SkipWhitespace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        return i;
    }

    private static int SkipBalanced(string s, int start, char open, char close)
    {
        var depth = 0;
        for (var i = start; i < s.Length; i++)
        {
            if (s[i] == open)
            {
                depth++;
            }
            else if (s[i] == close && --depth == 0)
            {
                return i + 1;
            }
        }

        return s.Length;
    }

    [GeneratedRegex(@"^[ \t]*(?:\[[^\]\r\n]*\][ \t]*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new|file)[ \t]+)*(?:record[ \t]+)?(class|struct|interface|record)[ \t]+(\w+)([^{;]*)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    public static partial Regex DeclPattern();

    [GeneratedRegex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    public static partial Regex NamespacePattern();

    [GeneratedRegex(@"//[^\r\n]*", RegexOptions.NonBacktracking)]
    private static partial Regex LineComment();

    [GeneratedRegex(@"\bwhere\b", RegexOptions.NonBacktracking)]
    private static partial Regex WhereKeyword();


    [GeneratedRegex(@"^\s*public\s+(?<sig>[^;{=]*\([^)]*\))", RegexOptions.NonBacktracking)]
    public static partial Regex MethodSignaturePattern();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    public static partial Regex WhitespacePattern();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.NonBacktracking)]
    public static partial Regex XmlTagPattern();

    [GeneratedRegex(@"\bpublic\s+(?:(?:virtual|static|async|override|sealed|new)\s+)*[\w<>\[\],.?]+\s+(\w+)\s*(?:<[^>(]*>)?\(", RegexOptions.NonBacktracking)]
    public static partial Regex PublicMethodNamePattern();

    [GeneratedRegex(@"\bpublic\s+(?:(?:abstract|sealed|static|partial)\s+)*class\s+(\w+)(?:<[^>]*>)?(?:\s*:\s*([^{\r\n]*))?", RegexOptions.NonBacktracking)]
    public static partial Regex PublicClassPattern();

    [GeneratedRegex(@"\bpublic\s+(?:(?:abstract|sealed|static|partial)\s+)*(?:class|interface|record|struct)\s+(\w+)", RegexOptions.NonBacktracking)]
    public static partial Regex PublicTypePattern();

    [GeneratedRegex(@"\b(?:class|record)\s+(\w+)[^{;:]*:\s*([^{;]*)", RegexOptions.NonBacktracking)]
    public static partial Regex ClassWithBasesPattern();
}
