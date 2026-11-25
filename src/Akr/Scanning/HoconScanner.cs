using System.Text.RegularExpressions;

namespace Akr.Scanning;

public sealed partial class HoconScanner
{
    private readonly FileService _fileService;

    public HoconScanner(FileService fileService)
    {
        _fileService = fileService;
    }

    [GeneratedRegex(@"public\s+static\s+\S+\s+(?<name>(With|Add)\w+)\s*[\(<]", RegexOptions.NonBacktracking)]
    private static partial Regex NamedMethodPattern();

    [GeneratedRegex(@"\bakka(?:\.[A-Za-z0-9_\-]+)+", RegexOptions.NonBacktracking)]
    private static partial Regex HoconKeyPattern();

    public Dictionary<string, string?> CollectConfig(IEnumerable<string> roots)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (full, _) in _fileService.EnumerateConfigFiles(roots))
        {
            foreach (var entry in HoconParser.ParseConfig(_fileService.ReadAllLines(full)))
            {
                result[entry.Path] = entry.Value;
            }
        }

        return result;
    }

    public Dictionary<string, SortedSet<string>> CollectHostingKeys(IEnumerable<string> roots)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (full, _) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            string? current = null;
            foreach (var line in _fileService.ReadLines(full))
            {
                var m = NamedMethodPattern().Match(line);
                if (m.Success)
                {
                    current = m.Groups["name"].Value;
                }

                if (current is null)
                {
                    continue;
                }

                foreach (Match k in HoconKeyPattern().Matches(line))
                {
                    if (!map.TryGetValue(current, out var set))
                    {
                        set = new SortedSet<string>(StringComparer.Ordinal);
                        map[current] = set;
                    }

                    set.Add(k.Value);
                }
            }
        }

        return map;
    }
}
