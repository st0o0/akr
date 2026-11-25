namespace Akr.Scanning;

public static class HoconParser
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
}
