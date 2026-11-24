using System.IO.Abstractions;
using Akr.Analysis;
using Akr.Tools;

namespace Akr.Tests.Analysis;

public sealed class DocsIndexTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"akr-docs-test-{Guid.NewGuid()}");
    private readonly DocsIndex _index;

    public DocsIndexTests()
    {
        Directory.CreateDirectory(_tempDir);
        _index = new DocsIndex(new FileService(new FileSystem()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private void WriteDoc(string name, string content) =>
        File.WriteAllText(Path.Combine(_tempDir, name), content);

    [Fact]
    public void Build_indexes_markdown_files()
    {
        WriteDoc("Cluster Sharding.md", "# Cluster Sharding\n\nIntro text.\n\n## Entities\n\nDetails.\n");
        WriteDoc("Remoting.md", "# Remoting\n\nSome remoting text.\n");

        _index.Build(_tempDir);
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

        _index.Build(_tempDir);
        var results = _index.Search("persistence");

        Assert.Equal(2, results.Count);
        Assert.Equal("Persistence.md", results[0].FileName);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Search_returns_empty_for_no_match()
    {
        WriteDoc("Remoting.md", "# Remoting\n\nSome text.\n");

        _index.Build(_tempDir);

        Assert.Empty(_index.Search("zzzznotfound"));
    }

    [Fact]
    public void FindExamples_extracts_code_blocks()
    {
        WriteDoc("Streams.md", "# Streams\n\n```csharp\nvar x = 1;\n```\n");

        _index.Build(_tempDir);
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

        _index.Build(_tempDir);
        var examples = _index.FindExamples("Config", "hocon");

        var example = Assert.Single(examples);
        Assert.Equal("hocon", example.Language);
        Assert.Contains("akka.loglevel", example.Code);
    }
}
