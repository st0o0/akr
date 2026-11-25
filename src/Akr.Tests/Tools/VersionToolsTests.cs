using Akr.Analysis;

namespace Akr.Tests.Tools;

public sealed class VersionToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"akr-version-test-{Guid.NewGuid()}");

    public VersionToolsTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void DocsIndex_finds_migration_docs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Migration.md"), "# Migration\n\nUpgrade notes.\n");
        var docs = new DocsIndex();

        docs.AddEntries([new DocsEntry(
            FilePath: Path.Combine(_tempDir, "Migration.md"),
            RelativePath: "Migration.md",
            FileName: "Migration",
            Directory: "",
            Content: "# Migration\n\nUpgrade notes.\n")]);

        Assert.NotEmpty(docs.Search("migration"));
    }

    [Fact]
    public void DocsIndex_IsBuilt_true_after_AddEntries()
    {
        var docs = new DocsIndex();
        Assert.False(docs.IsBuilt);

        docs.AddEntries([]);
        Assert.True(docs.IsBuilt);
    }
}
