using Akr.Scanning;

namespace Akr.Tests.Analysis;

public sealed class DocsIndexTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"akr-docs-test-{Guid.NewGuid()}");
    private readonly DocsIndex _index = new();

    public DocsIndexTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private void WriteDoc(string name, string content) =>
        File.WriteAllText(Path.Combine(_tempDir, name), content);

    private void BuildIndex(params string[] fileNames)
    {
        var entries = new List<DocsEntry>();
        foreach (var name in fileNames)
        {
            var fullPath = Path.Combine(_tempDir, name);
            entries.Add(new DocsEntry(
                FilePath: fullPath,
                RelativePath: name,
                FileName: Path.GetFileNameWithoutExtension(fullPath),
                Directory: "",
                Content: File.ReadAllText(fullPath)));
        }

        _index.AddEntries(entries);
    }

    [Fact]
    public void Build_indexes_markdown_files()
    {
        WriteDoc("Cluster Sharding.md", "# Cluster Sharding\n\nIntro text.\n\n## Entities\n\nDetails.\n");
        WriteDoc("Remoting.md", "# Remoting\n\nSome remoting text.\n");

        BuildIndex("Cluster Sharding.md", "Remoting.md");
        var results = _index.Search("Sharding");

        var result = Assert.Single(results);
        Assert.Equal("Cluster Sharding.md", result.FileName);
        Assert.Equal("Cluster Sharding", result.Heading);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public void Search_returns_matches_ranked_by_relevance()
    {
        WriteDoc("Persistence.md", "# Overview\n\nNothing special.\n");
        WriteDoc("Other.md", "# Other\n\nThis mentions persistence in the body only.\n");

        BuildIndex("Persistence.md", "Other.md");
        var results = _index.Search("persistence");

        Assert.Equal(2, results.Count);
        Assert.Equal("Persistence.md", results[0].FileName);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Search_returns_empty_for_no_match()
    {
        WriteDoc("Remoting.md", "# Remoting\n\nSome text.\n");

        BuildIndex("Remoting.md");

        Assert.Empty(_index.Search("zzzznotfound"));
    }

    [Fact]
    public void FindExamples_extracts_code_blocks()
    {
        WriteDoc("Streams.md", "# Streams\n\n```csharp\nvar x = 1;\n```\n");

        BuildIndex("Streams.md");
        var examples = _index.FindExamples("Streams");

        var example = Assert.Single(examples);
        Assert.Equal("csharp", example.Language);
        Assert.Contains("var x = 1;", example.Code);
        Assert.Equal("Streams", example.Context);
    }

    [Fact]
    public void FindExamples_filters_by_language()
    {
        WriteDoc("Config.md", "# Config\n\n```csharp\nvar x = 1;\n```\n\n```hocon\nakka.loglevel = DEBUG\n```\n");

        BuildIndex("Config.md");
        var examples = _index.FindExamples("Config", "hocon");

        var example = Assert.Single(examples);
        Assert.Equal("hocon", example.Language);
        Assert.Contains("akka.loglevel", example.Code);
    }
}
