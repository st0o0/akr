using System.IO.Abstractions;
using Akr.Analysis;
using Akr.Tools;

namespace Akr.Tests.Tools;

// TODO: SourceDiffAsync/MigrationGuideAsync need a GitHubClient (HTTP); add end-to-end
// tests once the client can be substituted. FindType is covered in TypeFinderTests.
public sealed class VersionToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"akr-version-test-{Guid.NewGuid()}");
    private readonly FileService _fileService = new(new FileSystem());

    public VersionToolsTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Constructor_accepts_file_service_and_docs_index()
    {
        var tools = new VersionTools(_fileService, new DocsIndex(_fileService));

        Assert.NotNull(tools);
    }

    [Fact]
    public void DocsIndex_used_by_VersionTools_finds_migration_docs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Migration.md"), "# Migration\n\nUpgrade notes.\n");
        var docs = new DocsIndex(_fileService);
        _ = new VersionTools(_fileService, docs);

        docs.Build(_tempDir);

        Assert.NotEmpty(docs.Search("migration"));
    }
}
