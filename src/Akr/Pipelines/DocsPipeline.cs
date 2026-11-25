using System.Text;
using Akka.Actor;
using Akka.Streams.Dsl;
using Akr.Analysis;
using Akr.Tools;

namespace Akr.Pipelines;

public sealed class DocsPipeline
{
    private readonly GitHubClient _github;
    private readonly DocsIndex _docsIndex;
    private readonly FileService _fileService;
    private readonly ActorSystem _actorSystem;

    public DocsPipeline(GitHubClient github, DocsIndex docsIndex, FileService fileService, ActorSystem actorSystem)
    {
        _github = github;
        _docsIndex = docsIndex;
        _fileService = fileService;
        _actorSystem = actorSystem;
    }

    public async Task BuildIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_docsIndex.IsBuilt)
        {
            return;
        }

        var docsRoot = await _github.EnsureDocsAsync(cancellationToken);

        var docsPath = _fileService.Combine(docsRoot, "src", "docs");
        if (!_fileService.DirectoryExists(docsPath))
        {
            docsPath = docsRoot;
        }

        var mdFiles = _fileService.EnumerateFiles([docsPath], "*.md").ToList();

        var entries = await Source.From(mdFiles)
            .SelectAsync(4, file => Task.Run(() => ParseMarkdownFile(file.Full, file.Relative), cancellationToken))
            .RunWith(Sink.Seq<DocsEntry>(), _actorSystem);

        _docsIndex.AddEntries(entries);
    }

    private DocsEntry ParseMarkdownFile(string fullPath, string relativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(fullPath);
        var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";
        var content = Encoding.UTF8.GetString(_fileService.ReadAllBytes(fullPath));

        return new DocsEntry(
            FilePath: fullPath,
            RelativePath: relativePath,
            FileName: fileName,
            Directory: directory,
            Content: content);
    }
}
