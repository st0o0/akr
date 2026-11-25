using System.IO.Abstractions.TestingHelpers;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class StreamPersistenceToolsTests
{
    private const string Root = "C:/test";
    private readonly MockFileSystem _fs = new();
    private readonly StreamTools _tools;
    private readonly PersistenceTools _persistence;

    public StreamPersistenceToolsTests()
    {
        var fs = new FileService(_fs);
        _tools = new StreamTools(fs, null!);
        _persistence = new PersistenceTools(fs, null!);

        WriteFile("Akka.Streams/Implementation/Merge.cs", """
            namespace Akka.Streams.Implementation;
            public sealed class MergeStage<T> : GraphStage<UniformFanInShape<T, T>>
            {
            }
            """);
        WriteFile("Other/Merge.cs", """
            public sealed class MergeStage<T> { }
            """);
        WriteFile("Akka.Streams/Dsl/GraphDsl.cs", """
            namespace Akka.Streams.Dsl;
            public static class GraphDsl
            {
                /// <summary>Creates a graph.</summary>
                public static IGraph<S, M> Create<S, M>(Func<Builder, S> buildBlock)
                {
                }
                public sealed class Builder
                {
                    public Builder<T> Add<T>(IGraph<T> graph)
                    {
                    }
                }
            }
            """);
        WriteFile("Akka.Streams/Attributes.cs", """
            namespace Akka.Streams;
            public sealed class Attributes
            {
                public sealed class InputBuffer : Attributes.IAttribute
                {
                }
                public static readonly InputBuffer DefaultInputBuffer = new InputBuffer(16, 16);
            }
            """);
        WriteFile("Akka.Persistence/Journal.cs", """
            public sealed class MemoryJournal : AsyncWriteJournal
            {
            }
            public abstract class AbstractJournal : AsyncWriteJournal
            {
            }
            public sealed class MemorySnapshotStore : SnapshotStore
            {
            }
            public sealed class SqlReadJournal : IReadJournal, IEventsByTagQuery, IPersistenceIdsQuery
            {
            }
            public sealed class SqlReadJournalProvider : IReadJournalProvider
            {
            }
            public sealed class TagAdapter : IWriteEventAdapter
            {
            }
            public sealed class Upcaster : IEventAdapter, IReadEventAdapter
            {
            }
            """);
        WriteFile("Akka.Persistence/reference.conf", """
            akka.persistence {
              journal.inmem {
                class = "Akka.Persistence.Journal.MemoryJournal, Akka.Persistence"
              }
              snapshot-store.inmem {
                class = "Akka.Persistence.Snapshot.MemorySnapshotStore, Akka.Persistence"
              }
            }
            event-adapters {
              tagging = "Foo.TagAdapter, Foo"
            }
            """);
    }

    private void WriteFile(string relative, string content)
    {
        var path = _fs.Path.Combine(Root, relative);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(path)!);
        _fs.File.WriteAllText(path, content);
    }

    private string[] Roots => [Root];

    private string[] EmptyRoots => [_fs.Path.Combine(Root, "Other")];

    [Fact]
    public void StageDetail_ReturnsFullSource_ScopedToAkkaStreams()
    {
        var result = _tools.FormatStageDetail(Roots, "MergeStage");

        Assert.Contains("Akka.Streams/Implementation/Merge.cs", result);
        Assert.Contains("namespace Akka.Streams.Implementation;", result);
        Assert.DoesNotContain("Other/Merge.cs", result);
    }

    [Fact]
    public void StageDetail_Unknown_ReportsNotFound() =>
        Assert.Contains("not found", _tools.FormatStageDetail(Roots, "Nope"));

    [Fact]
    public void GraphDsl_ListsMethodsWithDocs()
    {
        var result = _tools.FormatGraphDsl(Roots);

        Assert.Contains("Create<S, M>(Func<Builder, S> buildBlock)", result);
        Assert.Contains("Creates a graph.", result);
        Assert.Contains("Add<T>(IGraph<T> graph)", result);
    }

    [Fact]
    public void GraphDsl_NoSource_ReportsNone() =>
        Assert.Contains("No GraphDSL", _tools.FormatGraphDsl(EmptyRoots));

    [Fact]
    public void Attributes_ListsClassesAndDefaults()
    {
        var result = _tools.FormatAttributes(Roots);

        Assert.Contains("InputBuffer", result);
        Assert.Contains("DefaultInputBuffer", result);
    }

    [Fact]
    public void Attributes_MatchesInstanceDefaults()
    {
        WriteFile("Akka.Streams/AsyncBoundary.cs", """
            namespace Akka.Streams;
            public sealed class AsyncBoundary : Attributes.IAttribute
            {
                public static readonly AsyncBoundary Instance = new();
            }
            """);

        var result = _tools.FormatAttributes(Roots);

        Assert.Contains("AsyncBoundary", result);
        Assert.Contains("Instance", result);
    }

    [Fact]
    public void Attributes_NoMatches_ReportsNone() =>
        Assert.Contains("No stream attributes", _tools.FormatAttributes(EmptyRoots));

    [Fact]
    public void Journals_FindsConcreteWithConfigSection()
    {
        var plugins = _persistence.FindPlugins(Roots, ["AsyncWriteJournal", "WriteJournalBase"], "journal");

        var p = Assert.Single(plugins);
        Assert.Equal("MemoryJournal", p.Name);
        Assert.Equal("akka.persistence.journal.inmem", p.Section);
    }

    [Fact]
    public void Journals_Format_IncludesFile()
    {
        var plugins = _persistence.FindPlugins(Roots, ["AsyncWriteJournal"], "journal");

        Assert.Contains("Akka.Persistence/Journal.cs", PersistenceTools.FormatPlugins("Journal plugins", plugins));
    }

    [Fact]
    public void Snapshots_FindsStoreWithConfigSection()
    {
        var plugins = _persistence.FindPlugins(Roots, ["SnapshotStore"], "snapshot-store");

        var p = Assert.Single(plugins);
        Assert.Equal("MemorySnapshotStore", p.Name);
        Assert.Equal("akka.persistence.snapshot-store.inmem", p.Section);
    }

    [Fact]
    public void Snapshots_Empty_ReportsNone() =>
        Assert.Contains("No snapshot", PersistenceTools.FormatPlugins("Snapshot store plugins", []));

    [Fact]
    public void Query_ListsProvidersAndQueryTypes()
    {
        var result = _persistence.FormatReadJournals(Roots);

        Assert.Contains("SqlReadJournalProvider", result);
        Assert.Contains("IEventsByTagQuery, IPersistenceIdsQuery", result);
    }

    [Fact]
    public void Query_NoMatches_ReportsNone() =>
        Assert.Contains("No ReadJournal", _persistence.FormatReadJournals(EmptyRoots));

    [Fact]
    public void Adapters_ListsImplementationsAndConfig()
    {
        var result = _persistence.FormatAdapters(Roots);

        Assert.Contains("TagAdapter", result);
        Assert.Contains("Upcaster", result);
        Assert.Contains("tagging = \"Foo.TagAdapter, Foo\"", result);
    }

    [Fact]
    public void Adapters_NoMatches_ReportsNone() =>
        Assert.Contains("No event adapters", _persistence.FormatAdapters(EmptyRoots));
}
