using System.IO.Abstractions;
using Akr.Scanning;

namespace Akr.Tests.Tools;

public sealed class SourceSearcherTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SourceSearcher _searcher;

    public SourceSearcherTests()
    {
        _searcher = new SourceSearcher(new FileService(new FileSystem()));
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        WriteFile("src/Actor.cs", """
            namespace Akka.Actor;
            public class ActorBase
            {
                public virtual void PreStart() { }
                public virtual void PostStop() { }
            }
            """);

        WriteFile("src/Props.cs", """
            namespace Akka.Actor;
            public sealed class Props
            {
                public static Props Create<T>() => new();
            }
            """);

        WriteFile("reference.conf", "akka.actor.default-dispatcher = fork-join");
    }

    [Fact]
    public void Finds_matching_lines()
    {
        var results = _searcher.Search([_testRoot], "PreStart");

        Assert.Single(results);
        Assert.Equal("src/Actor.cs", results[0].File);
        Assert.Contains("PreStart", results[0].MatchingLine);
    }

    [Fact]
    public void Returns_context_lines()
    {
        var results = _searcher.Search([_testRoot], "PreStart");

        Assert.NotEmpty(results[0].ContextBefore);
        Assert.NotEmpty(results[0].ContextAfter);
    }

    [Fact]
    public void Respects_file_pattern()
    {
        var results = _searcher.Search([_testRoot], "dispatcher", filePattern: "*.conf");

        Assert.Single(results);
        Assert.Equal("reference.conf", results[0].File);
    }

    [Fact]
    public void Respects_max_results()
    {
        var results = _searcher.Search([_testRoot], @"public", maxResults: 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Returns_empty_for_no_match()
    {
        var results = _searcher.Search([_testRoot], "NonExistentSymbol");

        Assert.Empty(results);
    }

    [Fact]
    public void Supports_regex_patterns()
    {
        var results = _searcher.Search([_testRoot], @"public\s+virtual\s+void");

        Assert.Equal(2, results.Count);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
