using System.Text.RegularExpressions;

namespace Akr.Tools;

public static class SourceSearcher
{
    private const int ContextLines = 2;

    public static IReadOnlyList<SearchResult> Search(
        IReadOnlyList<string> sourceRoots,
        string pattern,
        string filePattern = "*.cs",
        int maxResults = 20)
    {
        var regex = new Regex(pattern, RegexOptions.NonBacktracking, TimeSpan.FromSeconds(5));
        var results = new List<SearchResult>();

        foreach (var root in sourceRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, filePattern, SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (!regex.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    var contextBefore = lines[Math.Max(0, i - ContextLines)..i];
                    var contextAfter = lines[(i + 1)..Math.Min(lines.Length, i + 1 + ContextLines)];
                    var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');

                    results.Add(new SearchResult(relativePath, i + 1, lines[i], contextBefore, contextAfter));

                    if (results.Count >= maxResults)
                    {
                        return results;
                    }
                }
            }
        }

        return results;
    }
}
