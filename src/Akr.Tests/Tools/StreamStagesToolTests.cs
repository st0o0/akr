using System.IO.Abstractions;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class StreamStagesToolTests : IDisposable
{
    private readonly string _testRoot;
    private readonly StreamTools _tools;

    public StreamStagesToolTests()
    {
        var fs = new FileService(new FileSystem());
        _tools = new StreamTools(fs, new SourceSearcher(fs));
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        WriteFile("Akka.Streams/Implementation/Stages.cs", """
            namespace Akka.Streams.Implementation;
            internal sealed class Selecter<TIn, TOut> : GraphStage<FlowShape<TIn, TOut>>
            {
            }
            internal sealed class Buffer<T> : SimpleLinearGraphStage<T>
            {
            }
            internal sealed class Tick<T> : GraphStage<SourceShape<T>>
            {
            }
            internal sealed class ForEach<T> : GraphStageWithMaterializedValue<SinkShape<T>, Task>
            {
            }
            internal sealed class Merge<T> : GraphStage<UniformFanInShape<T, T>>
            {
            }
            internal sealed class Broadcast<T> : GraphStage<UniformFanOutShape<T, T>>
            {
            }
            internal sealed class Weird : GraphStage<BidiShape<int, int, int, int>>
            {
            }
            internal sealed class NotAStage : ActorBase
            {
            }
            """);

        WriteFile("Other/Unrelated.cs", """
            internal sealed class Hidden<T> : GraphStage<FlowShape<T, T>>
            {
            }
            """);
    }

    private IReadOnlyList<StageInfo> Find(string? filter = null) => _tools.FindStages([_testRoot], filter);

    [Fact]
    public void GraphStage_with_FlowShape_is_Flow()
    {
        Assert.Contains(Find(), s => s == new StageInfo("Selecter<TIn, TOut>", "Flow"));
    }

    [Fact]
    public void SimpleLinearGraphStage_is_Flow()
    {
        Assert.Contains(Find(), s => s == new StageInfo("Buffer<T>", "Flow"));
    }

    [Fact]
    public void Source_and_Sink_shapes_are_classified()
    {
        var all = Find();

        Assert.Contains(all, s => s == new StageInfo("Tick<T>", "Source"));
        Assert.Contains(all, s => s == new StageInfo("ForEach<T>", "Sink"));
    }

    [Fact]
    public void Fan_shapes_are_classified()
    {
        var all = Find();

        Assert.Contains(all, s => s == new StageInfo("Merge<T>", "Fan-In"));
        Assert.Contains(all, s => s == new StageInfo("Broadcast<T>", "Fan-Out"));
    }

    [Fact]
    public void Unknown_shape_is_Custom()
    {
        Assert.Contains(Find(), s => s == new StageInfo("Weird", "Custom"));
    }

    [Fact]
    public void Filter_matches_name_or_category_case_insensitively()
    {
        var byName = Find("broadcast");
        Assert.Equal("Broadcast<T>", Assert.Single(byName).Name);

        var byCategory = Find("fan-in");
        Assert.Equal("Merge<T>", Assert.Single(byCategory).Name);
    }

    [Fact]
    public void Non_stage_classes_and_non_streams_paths_are_excluded()
    {
        var all = Find();

        Assert.DoesNotContain(all, s => s.Name.StartsWith("NotAStage", StringComparison.Ordinal));
        Assert.DoesNotContain(all, s => s.Name.StartsWith("Hidden", StringComparison.Ordinal));
    }

    [Fact]
    public void Format_groups_by_category()
    {
        var output = StreamTools.Format(Find("Tick"), "Tick");

        Assert.Contains("Source:", output);
        Assert.Contains("  Tick<T>", output);
        Assert.Contains("matching filter: X", StreamTools.Format([], "X"));
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
