using System.Text.RegularExpressions;
using Akr.Models;

namespace Akr.Scanning;

public sealed class SourceSearcher
{
    private const int ContextLines = 2;
    private readonly FileService _fileService;

    public SourceSearcher(FileService fileService)
    {
        _fileService = fileService;
    }

    public IReadOnlyList<SearchResult> Search(
        IReadOnlyList<string> sourceRoots,
        string pattern,
        string filePattern = "*.cs",
        int maxResults = 20)
    {
        var regex = new Regex(pattern, RegexOptions.NonBacktracking, TimeSpan.FromSeconds(5));
        var results = new List<SearchResult>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(sourceRoots, filePattern))
        {
            var lines = _fileService.ReadAllLines(full);

            for (var i = 0; i < lines.Length; i++)
            {
                if (!regex.IsMatch(lines[i]))
                {
                    continue;
                }

                var contextBefore = lines[Math.Max(0, i - ContextLines)..i];
                var contextAfter = lines[(i + 1)..Math.Min(lines.Length, i + 1 + ContextLines)];

                results.Add(new SearchResult(relative, i + 1, lines[i], contextBefore, contextAfter));

                if (results.Count >= maxResults)
                {
                    return results;
                }
            }
        }

        return results;
    }
}
