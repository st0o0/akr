using System.Text;
using Akr.Tools;

namespace Akr.Analysis;

public sealed class DocsIndex
{
    private readonly FileService _fileService;
    private readonly List<DocsEntry> _entries = [];
    private bool _built;

    public DocsIndex(FileService fileService)
    {
        _fileService = fileService;
    }

    public void Build(string docsRoot)
    {
        if (_built)
        {
            return;
        }

        var docsPath = _fileService.Combine(docsRoot, "src", "docs");
        if (!_fileService.DirectoryExists(docsPath))
        {
            docsPath = docsRoot;
        }

        foreach (var (fullPath, relativePath) in _fileService.EnumerateFiles([docsPath], "*.md"))
        {
            var fileName = Path.GetFileNameWithoutExtension(fullPath);
            var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";

            var content = Encoding.UTF8.GetString(_fileService.ReadAllBytes(fullPath));
            var headings = ExtractHeadings(content);
            var codeBlocks = ExtractCodeBlocks(content);

            _entries.Add(new DocsEntry(
                FilePath: fullPath,
                RelativePath: relativePath,
                FileName: fileName,
                Directory: directory,
                Headings: headings,
                Content: content,
                CodeBlocks: codeBlocks));
        }

        _built = true;
    }

    public IReadOnlyList<DocsSearchResult> Search(string query, int maxResults = 5)
    {
        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scored = new List<(DocsEntry Entry, int Score)>();

        foreach (var entry in _entries)
        {
            var score = 0;

            foreach (var term in queryTerms)
            {
                if (entry.FileName.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }

                if (entry.Directory.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 5;
                }

                foreach (var heading in entry.Headings)
                {
                    if (heading.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 7;
                    }
                }

                if (entry.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 2;
                }
            }

            if (score > 0)
            {
                scored.Add((entry, score));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(maxResults)
            .Select(s => new DocsSearchResult(
                FileName: s.Entry.RelativePath,
                Heading: s.Entry.Headings.Count > 0 ? s.Entry.Headings[0] : s.Entry.FileName,
                Excerpt: TruncateContent(s.Entry.Content, 2000),
                Score: s.Score))
            .ToList();
    }

    public IReadOnlyList<DocsCodeExample> FindExamples(string topic, string? language = null)
    {
        var results = Search(topic, maxResults: 10);
        var examples = new List<DocsCodeExample>();

        foreach (var result in results)
        {
            var entry = _entries.First(e => e.RelativePath == result.FileName);
            foreach (var block in entry.CodeBlocks)
            {
                if (language is not null &&
                    !string.Equals(block.Language, language, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                examples.Add(new DocsCodeExample(
                    FileName: entry.RelativePath,
                    Language: block.Language ?? "csharp",
                    Code: block.Code,
                    Context: block.PrecedingHeading ?? entry.FileName));
            }
        }

        return examples;
    }

    private static List<string> ExtractHeadings(string content)
    {
        var headings = new List<string>();
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal) || trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                headings.Add(trimmed.TrimStart('#', ' ').Trim());
            }
        }

        return headings;
    }

    private static List<CodeBlock> ExtractCodeBlocks(string content)
    {
        var blocks = new List<CodeBlock>();
        var lines = content.Split('\n');
        string? currentLanguage = null;
        string? precedingHeading = null;
        var blockContent = new StringBuilder();
        var inBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("# ", StringComparison.Ordinal) || trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                precedingHeading = trimmed.TrimStart('#', ' ').Trim();
            }

            if (!inBlock && trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inBlock = true;
                currentLanguage = trimmed.Length > 3 ? trimmed[3..].Trim() : null;
                blockContent.Clear();
            }
            else if (inBlock && trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inBlock = false;
                if (blockContent.Length > 0)
                {
                    blocks.Add(new CodeBlock(
                        Language: string.IsNullOrEmpty(currentLanguage) ? null : currentLanguage,
                        Code: blockContent.ToString().TrimEnd(),
                        PrecedingHeading: precedingHeading));
                }
            }
            else if (inBlock)
            {
                blockContent.AppendLine(line);
            }
        }

        return blocks;
    }

    private static string TruncateContent(string content, int maxChars)
    {
        if (content.Length <= maxChars)
        {
            return content;
        }

        var truncated = content[..maxChars];
        var lastNewline = truncated.LastIndexOf('\n');
        return lastNewline > maxChars / 2
            ? truncated[..lastNewline] + "\n[truncated]"
            : truncated + "\n[truncated]";
    }

    private sealed record DocsEntry(
        string FilePath,
        string RelativePath,
        string FileName,
        string Directory,
        List<string> Headings,
        string Content,
        List<CodeBlock> CodeBlocks);

    private sealed record CodeBlock(
        string? Language,
        string Code,
        string? PrecedingHeading);
}

public sealed record DocsSearchResult(
    string FileName,
    string Heading,
    string Excerpt,
    int Score);

public sealed record DocsCodeExample(
    string FileName,
    string Language,
    string Code,
    string Context);
