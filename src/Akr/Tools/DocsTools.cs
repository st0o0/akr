using System.ComponentModel;
using System.Text;
using Akr.Registry;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class DocsTools
{
    private readonly DocsIndex _docsIndex;
    private readonly SourcePipeline _sourcePipeline;
    private readonly FileService _fileService;

    public DocsTools(DocsIndex docsIndex, SourcePipeline sourcePipeline, FileService fileService)
    {
        _docsIndex = docsIndex;
        _sourcePipeline = sourcePipeline;
        _fileService = fileService;
    }

    [McpServerTool(Name = "docs_search"), Description("Search Akka.NET documentation for a topic or question. Returns relevant excerpts from official Akka.NET docs.")]
    public async Task<string> SearchDocsAsync(
        [Description("Search query (e.g. 'cluster sharding', 'ask pattern', 'persistence')")] string query,
        CancellationToken cancellationToken = default)
    {
        await EnsureDocsIndexAsync(cancellationToken);

        var results = _docsIndex.Search(query);
        if (results.Count == 0)
        {
            return $"No documentation found for '{query}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} documentation page(s) for '{query}':");
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.FileName} ---");
            sb.AppendLine($"## {result.Heading}");
            sb.AppendLine();
            sb.AppendLine(result.Excerpt);
            sb.AppendLine();
        }

        return OutputLimit.Truncate(sb.ToString());
    }

    [McpServerTool(Name = "docs_example"), Description("Find code examples from Akka.NET documentation for a given topic.")]
    public async Task<string> FindExamplesAsync(
        [Description("Topic to find examples for (e.g. 'ask pattern', 'persistence', 'cluster sharding')")] string topic,
        [Description("Optional language filter (e.g. 'csharp', 'hocon'). Omit for all languages.")] string? language = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureDocsIndexAsync(cancellationToken);

        var examples = _docsIndex.FindExamples(topic, language);
        if (examples.Count == 0)
        {
            var searchResults = _docsIndex.Search(topic, maxResults: 3);
            if (searchResults.Count > 0)
            {
                var sb2 = new StringBuilder();
                sb2.AppendLine($"No code examples found for '{topic}', but documentation exists:");
                sb2.AppendLine();
                foreach (var result in searchResults)
                {
                    sb2.AppendLine($"- {result.FileName}: {result.Heading}");
                }

                return sb2.ToString();
            }

            return $"No documentation or code examples found for '{topic}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {examples.Count} code example(s) for '{topic}':");
        sb.AppendLine();

        foreach (var example in examples)
        {
            sb.AppendLine($"--- {example.FileName} ({example.Context}) ---");
            sb.AppendLine($"```{example.Language}");
            sb.AppendLine(example.Code);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return OutputLimit.Truncate(sb.ToString());
    }

    private async Task EnsureDocsIndexAsync(CancellationToken cancellationToken)
    {
        if (_docsIndex.IsBuilt)
        {
            return;
        }

        var roots = await _sourcePipeline.EnsureAllForVersionAsync(null, cancellationToken);
        var mainRoot = roots.FirstOrDefault(r => r.Contains(SourceCache.RepoName(RepoRegistry.MainRepo), StringComparison.OrdinalIgnoreCase));
        if (mainRoot is null)
        {
            return;
        }

        var docsPath = _fileService.Combine(mainRoot, RepoRegistry.DocsPath);
        if (!_fileService.DirectoryExists(docsPath))
        {
            return;
        }

        var entries = new List<DocsEntry>();
        foreach (var (fullPath, relativePath) in _fileService.EnumerateFiles([docsPath], "*.md"))
        {
            var fileName = _fileService.GetFileNameWithoutExtension(fullPath);
            var directory = _fileService.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";
            var content = System.Text.Encoding.UTF8.GetString(_fileService.ReadAllBytes(fullPath));

            entries.Add(new DocsEntry(
                FilePath: fullPath,
                RelativePath: relativePath,
                FileName: fileName,
                Directory: directory,
                Content: content));
        }

        _docsIndex.AddEntries(entries);
    }
}
