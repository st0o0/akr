using Akr.Core.Tools;

namespace Akr.Tests.Tools;

public sealed class TypeFinderTests : IDisposable
{
    private readonly string _testRoot;

    public TypeFinderTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        WriteFile("src/ActorBase.cs", """
            using System;
            namespace Akka.Actor;
            public abstract class ActorBase : IInternalActor
            {
                public virtual void PreStart() { }
                protected abstract bool Receive(object message);
                public virtual void PostStop() { }
            }
            """);

        WriteFile("src/Props.cs", """
            namespace Akka.Actor;
            public sealed class Props
            {
                public Type Type { get; }
                public static Props Create<T>() where T : ActorBase, new() => new();
                public static Props Create(Type type) => new();
            }
            """);

        WriteFile("src/ActorSystem.cs", """
            namespace Akka.Actor;
            public abstract class ActorSystem : IActorRefFactory, IDisposable
            {
                public abstract IActorRef ActorOf(Props props, string name);
            }
            """);

        WriteFile("src/Other/Actor.cs", """
            namespace Other;
            public class Actor { }
            """);
    }

    [Fact]
    public void Finds_type_by_exact_name()
    {
        var results = TypeFinder.FindType([_testRoot], "Props");

        Assert.Single(results);
        Assert.Equal("Akka.Actor", results[0].Namespace);
        Assert.Contains("class Props", results[0].Declaration);
    }

    [Fact]
    public void Does_not_match_partial_names()
    {
        var results = TypeFinder.FindType([_testRoot], "Actor");

        // Should match "class Actor" in Other/ but NOT "class ActorBase" or "class ActorSystem"
        Assert.Single(results);
        Assert.Equal("Other", results[0].Namespace);
    }

    [Fact]
    public void Returns_full_source_when_requested()
    {
        var results = TypeFinder.FindType([_testRoot], "Props", fullSource: true);

        Assert.Single(results);
        Assert.Contains("static Props Create<T>", results[0].Source);
        Assert.Contains("static Props Create(Type", results[0].Source);
    }

    [Fact]
    public void Returns_summary_by_default()
    {
        var results = TypeFinder.FindType([_testRoot], "ActorBase");

        Assert.Single(results);
        Assert.Contains("PreStart", results[0].Source);
        Assert.Contains("Receive", results[0].Source);
    }

    [Fact]
    public void Returns_empty_for_unknown_type()
    {
        var results = TypeFinder.FindType([_testRoot], "NonExistent");

        Assert.Empty(results);
    }

    [Fact]
    public void Finds_abstract_class()
    {
        var results = TypeFinder.FindType([_testRoot], "ActorSystem");

        Assert.Single(results);
        Assert.Contains("abstract class ActorSystem", results[0].Declaration);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
