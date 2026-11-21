using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class StreamPersistenceToolsTests : IDisposable
{
    private readonly string _root;

    public StreamPersistenceToolsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);

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
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string[] Roots => [_root];

    private string[] EmptyRoots => [Path.Combine(_root, "Other")];

    [Fact]
    public void StageDetail_ReturnsFullSource_ScopedToAkkaStreams()
    {
        var result = StreamPersistenceTools.FormatStageDetail(Roots, "MergeStage");

        Assert.Contains("Akka.Streams/Implementation/Merge.cs", result);
        Assert.Contains("namespace Akka.Streams.Implementation;", result);
        Assert.DoesNotContain("Other/Merge.cs", result);
    }

    [Fact]
    public void StageDetail_Unknown_ReportsNotFound() =>
        Assert.Contains("not found", StreamPersistenceTools.FormatStageDetail(Roots, "Nope"));

    [Fact]
    public void GraphDsl_ListsMethodsWithDocs()
    {
        var result = StreamPersistenceTools.FormatGraphDsl(Roots);

        Assert.Contains("Create<S, M>(Func<Builder, S> buildBlock)", result);
        Assert.Contains("Creates a graph.", result);
        Assert.Contains("Add<T>(IGraph<T> graph)", result);
    }

    [Fact]
    public void GraphDsl_NoSource_ReportsNone() =>
        Assert.Contains("No GraphDSL", StreamPersistenceTools.FormatGraphDsl(EmptyRoots));

    [Fact]
    public void Attributes_ListsClassesAndDefaults()
    {
        var result = StreamPersistenceTools.FormatAttributes(Roots);

        Assert.Contains("InputBuffer", result);
        Assert.Contains("DefaultInputBuffer", result);
    }

    [Fact]
    public void Attributes_NoMatches_ReportsNone() =>
        Assert.Contains("No stream attributes", StreamPersistenceTools.FormatAttributes(EmptyRoots));

    [Fact]
    public void Journals_FindsConcreteWithConfigSection()
    {
        var plugins = StreamPersistenceTools.FindPlugins(Roots, ["AsyncWriteJournal", "WriteJournalBase"], "journal");

        var p = Assert.Single(plugins);
        Assert.Equal("MemoryJournal", p.Name);
        Assert.Equal("akka.persistence.journal.inmem", p.Section);
    }

    [Fact]
    public void Journals_Format_IncludesFile()
    {
        var plugins = StreamPersistenceTools.FindPlugins(Roots, ["AsyncWriteJournal"], "journal");

        Assert.Contains("Akka.Persistence/Journal.cs", StreamPersistenceTools.FormatPlugins("Journal plugins", plugins));
    }

    [Fact]
    public void Snapshots_FindsStoreWithConfigSection()
    {
        var plugins = StreamPersistenceTools.FindPlugins(Roots, ["SnapshotStore"], "snapshot-store");

        var p = Assert.Single(plugins);
        Assert.Equal("MemorySnapshotStore", p.Name);
        Assert.Equal("akka.persistence.snapshot-store.inmem", p.Section);
    }

    [Fact]
    public void Snapshots_Empty_ReportsNone() =>
        Assert.Contains("No snapshot", StreamPersistenceTools.FormatPlugins("Snapshot store plugins", []));

    [Fact]
    public void Query_ListsProvidersAndQueryTypes()
    {
        var result = StreamPersistenceTools.FormatReadJournals(Roots);

        Assert.Contains("SqlReadJournalProvider", result);
        Assert.Contains("IEventsByTagQuery, IPersistenceIdsQuery", result);
    }

    [Fact]
    public void Query_NoMatches_ReportsNone() =>
        Assert.Contains("No ReadJournal", StreamPersistenceTools.FormatReadJournals(EmptyRoots));

    [Fact]
    public void Adapters_ListsImplementationsAndConfig()
    {
        var result = StreamPersistenceTools.FormatAdapters(Roots);

        Assert.Contains("TagAdapter", result);
        Assert.Contains("Upcaster", result);
        Assert.Contains("tagging = \"Foo.TagAdapter, Foo\"", result);
    }

    [Fact]
    public void Adapters_NoMatches_ReportsNone() =>
        Assert.Contains("No event adapters", StreamPersistenceTools.FormatAdapters(EmptyRoots));
}
