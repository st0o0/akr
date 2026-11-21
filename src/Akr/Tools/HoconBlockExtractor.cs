namespace Akr.Tools;

public static class HoconBlockExtractor
{
    public static string? Extract(IReadOnlyList<string> lines, string path)
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

    private static string StripComment(string line)
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
}
