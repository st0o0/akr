using System.Text.RegularExpressions;
using Akr.Models;

namespace Akr.Scanning;

public static partial class HoconParser
{
    public static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && (c == '#' || (c == '/' && i + 1 < line.Length && line[i + 1] == '/')))
            {
                return line[..i];
            }
        }

        return line;
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z0-9_\.\-""]+)\s*(?<op>[={:])\s*(?<rest>.*)$", RegexOptions.NonBacktracking)]
    private static partial Regex EntryPattern();

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
            foreach (var c in StripComment(lines[i]))
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

    public static IReadOnlyList<ConfigEntry> ParseConfig(IReadOnlyList<string> lines)
    {
        var entries = new List<ConfigEntry>();
        var stack = new Stack<string>();
        var bracketDepth = 0;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (bracketDepth > 0)
            {
                bracketDepth += line.Count(c => c == '[') - line.Count(c => c == ']');
                continue;
            }

            while (line.StartsWith('}'))
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                line = line[1..].TrimStart();
            }

            if (line.Length == 0)
            {
                continue;
            }

            var m = EntryPattern().Match(line);
            if (!m.Success)
            {
                continue;
            }

            var key = m.Groups["key"].Value.Replace("\"", string.Empty, StringComparison.Ordinal);
            var rest = m.Groups["rest"].Value.Trim();
            var prefix = stack.Count > 0 ? stack.Peek() : string.Empty;
            var full = prefix.Length > 0 ? $"{prefix}.{key}" : key;
            var op = m.Groups["op"].Value;

            if (op == "{" || rest.StartsWith('{'))
            {
                var body = op == "{" ? rest : rest[1..];
                var opens = 1 + body.Count(c => c == '{');
                var closes = body.Count(c => c == '}');
                if (opens - closes <= 0)
                {
                    entries.Add(new ConfigEntry(full, "{ " + body.TrimEnd('}').Trim() + " }"));
                }
                else
                {
                    stack.Push(full);
                }

                continue;
            }

            bracketDepth = Math.Max(0, rest.Count(c => c == '[') - rest.Count(c => c == ']'));
            entries.Add(new ConfigEntry(full, rest));
        }

        return entries;
    }

    public static bool PathMatches(string key, string query) =>
        key == query
        || key.StartsWith(query + ".", StringComparison.Ordinal)
        || query.StartsWith(key + ".", StringComparison.Ordinal);
}
