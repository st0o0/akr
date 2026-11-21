using Akr.Core;
using Akr.Core.Caching;
using Akr.Core.Configuration;
using Akr.Core.Fetching;
using Akr.Core.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Akr.Tests.Integration;

public sealed class SmokeTests : IDisposable
{
    private readonly string _cacheRoot;
    private readonly string _sourceRoot;

    public SmokeTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), $"akr-smoke-{Guid.NewGuid()}");
        _sourceRoot = Path.Combine(_cacheRoot, "akka.net", "1.5.30");
        Directory.CreateDirectory(_sourceRoot);

        WriteSource("src/core/Akka/Actor/ActorBase.cs", """
            using System;
            namespace Akka.Actor
            {
                public abstract class ActorBase : IInternalActor
                {
                    public virtual void AroundPreStart() { PreStart(); }
                    protected virtual void PreStart() { }
                    public virtual void AroundPostStop() { PostStop(); }
                    protected virtual void PostStop() { }
                    protected internal abstract bool Receive(object message);
                }
            }
            """);

        WriteSource("src/core/Akka/Actor/Props.cs", """
            namespace Akka.Actor
            {
                public class Props
                {
                    public Type Type { get; }
                    public static Props Create<TActor>() where TActor : ActorBase, new() => new Props(typeof(TActor));
                    public static Props Create(Type type) => new Props(type);
                    public Props(Type type) { Type = type; }
                }
            }
            """);

        WriteSource("src/core/Akka/Actor/IActorRef.cs", """
            namespace Akka.Actor
            {
                public interface IActorRef : ICanTell, IEquatable<IActorRef>, IComparable<IActorRef>
                {
                    ActorPath Path { get; }
                    void Tell(object message, IActorRef sender);
                }
            }
            """);

        WriteSource("src/core/Akka/Configuration/Config.cs", """
            namespace Akka.Configuration
            {
                public class Config
                {
                    public string GetString(string path) => string.Empty;
                    public int GetInt(string path) => 0;
                    public bool GetBoolean(string path) => false;
                    public TimeSpan GetTimeSpan(string path) => TimeSpan.Zero;
                }
            }
            """);

        WriteSource("src/core/Akka/reference.conf", """
            akka {
                actor {
                    default-dispatcher {
                        type = Dispatcher
                        throughput = 300
                    }
                }
                loglevel = INFO
            }
            """);
    }

    [Fact]
    public void Search_source_finds_matching_lines()
    {
        var results = SourceSearcher.Search([_sourceRoot], "PreStart");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Contains("PreStart", r.MatchingLine));
    }

    [Fact]
    public void Search_source_with_regex_finds_patterns()
    {
        var results = SourceSearcher.Search([_sourceRoot], @"public\s+(?:abstract\s+)?class\s+\w+");

        Assert.True(results.Count >= 3);
    }

    [Fact]
    public void Search_source_in_conf_files()
    {
        var results = SourceSearcher.Search([_sourceRoot], "throughput", filePattern: "*.conf");

        Assert.Single(results);
        Assert.Contains("300", results[0].MatchingLine);
    }

    [Fact]
    public void Lookup_type_finds_ActorBase()
    {
        var results = TypeFinder.FindType([_sourceRoot], "ActorBase");

        Assert.Single(results);
        Assert.Equal("Akka.Actor", results[0].Namespace);
        Assert.Contains("abstract class ActorBase", results[0].Declaration);
    }

    [Fact]
    public void Lookup_type_finds_interface()
    {
        var results = TypeFinder.FindType([_sourceRoot], "IActorRef");

        Assert.Single(results);
        Assert.Contains("interface IActorRef", results[0].Declaration);
    }

    [Fact]
    public void Lookup_type_full_source_returns_complete_file()
    {
        var results = TypeFinder.FindType([_sourceRoot], "Props", fullSource: true);

        Assert.Single(results);
        Assert.Contains("Create<TActor>", results[0].Source);
        Assert.Contains("Create(Type type)", results[0].Source);
        Assert.Contains("namespace Akka.Actor", results[0].Source);
    }

    [Fact]
    public void Lookup_type_summary_includes_member_signatures()
    {
        var results = TypeFinder.FindType([_sourceRoot], "Config");

        Assert.Single(results);
        Assert.Contains("GetString", results[0].Source);
        Assert.Contains("GetTimeSpan", results[0].Source);
    }

    [Fact]
    public async Task Source_provider_orchestrates_full_flow()
    {
        var versionConfig = Substitute.For<IVersionConfig>();
        versionConfig.GetVersion("Akka").Returns("1.5.30");
        versionConfig.GetAll().Returns(new Dictionary<string, string> { ["Akka"] = "1.5.30" });

        var cache = new FileSystemSourceCache(_cacheRoot);
        var fetcher = Substitute.For<ISourceFetcher>();

        var provider = new SourceProvider(versionConfig, cache, fetcher, NullLogger<SourceProvider>.Instance);

        var root = await provider.GetSourceRootAsync("Akka", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(root);
        Assert.True(Directory.Exists(root));
        await fetcher.DidNotReceiveWithAnyArgs()
            .FetchAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    private void WriteSource(string relativePath, string content)
    {
        var fullPath = Path.Combine(_sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_cacheRoot, recursive: true);
}
