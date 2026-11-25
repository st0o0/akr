using System.IO.Abstractions.TestingHelpers;
using Akr.Scanning;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class EcosystemToolsTests
{
    private const string Root = "C:/test";
    private readonly MockFileSystem _fs = new();
    private readonly SerializationTools _serialization;
    private readonly ClusterTools _cluster;
    private readonly TestKitTools _testKit;
    private readonly HostingTools _hosting;

    public EcosystemToolsTests()
    {
        var fs = new FileService(_fs);
        _serialization = new SerializationTools(fs, null!);
        _cluster = new ClusterTools(fs, null!);
        _testKit = new TestKitTools(fs, null!);
        _hosting = new HostingTools(fs, null!, new HoconScanner(fs));

        WriteFile("src/Akka/reference.conf", """
            akka {
              actor {
                serializers {
                  json = "Akka.Serialization.NewtonSoftJsonSerializer, Akka"
                }
                serialization-bindings {
                  "System.Object" = json
                  "Akka.Actor.Foo" = hyperion
                }
              }
            }
            """);

        WriteFile("src/Akka/Serialization/NewtonSoftJsonSerializer.cs", """
            namespace Akka.Serialization;
            public class NewtonSoftJsonSerializer : Serializer
            {
                public override byte[] ToBinary(object obj) => [];
            }
            public class Unrelated : Foo
            {
            }
            """);

        WriteFile("src/Akka.Persistence/Serialization/PersistenceSerializer.cs", """
            public sealed class PersistenceMessageSerializer : SerializerWithStringManifest
            {
                private const string PersistentManifest = "A";
                public override string Manifest(object o) => "B";
            }
            """);

        WriteFile("src/Akka.Hosting/Hosting.cs", """
            public interface IRequiredActor<T> { }
            public class ActorRegistry { }
            public static class Ext
            {
                public static void Use(IServiceProvider sp) => sp.GetRequiredService<ActorRegistry>();
            }
            """);

        WriteFile("src/Akka.TestKit/TestKitBase.cs", """
            public abstract class TestKitBase : ITestKit
            {
                public T ExpectMsg<T>(TimeSpan? t = null) => default;
                public void AwaitCondition(Func<bool> f) { }
                public void Within(TimeSpan t, Action a) { }
            }
            """);

        WriteFile("src/Akka.Hosting.TestKit/TestKit.cs", """
            public abstract class Akka.Hosting.TestKit.TestKit : TestKitBase
            {
                public void AssertRegistered() { }
            }
            """);

        WriteFile("src/Akka.Cluster/ClusterEvent.cs", """
            public static class ClusterEvent
            {
                /// <summary>Member was removed.</summary>
                public sealed class MemberRemoved { }

                /// <summary>
                /// Member is <b>up</b>.
                /// </summary>
                public sealed class MemberUp { }

                /// <summary>Member is joining.</summary>
                public sealed class MemberJoined { }

                public sealed class LeaderChanged { }
            }
            """);

        WriteFile("src/Akka/IThingProvider.cs", """
            /// <summary>Provides things.</summary>
            public interface IThingProvider { }
            public class DefaultThingProvider : IThingProvider { }
            public interface IOtherPlugin { }
            """);

        WriteFile("src/Akka.Cluster/Akka.Cluster.csproj", """
            <Project>
              <ItemGroup>
                <ProjectReference Include="..\Akka.Remote\Akka.Remote.csproj" />
                <PackageReference Include="Akka" Version="1.0" />
                <PackageReference Include="Newtonsoft.Json" Version="1.0" />
              </ItemGroup>
            </Project>
            """);

        WriteFile("src/Akka.Remote/Akka.Remote.csproj", """
            <Project><ItemGroup><ProjectReference Include="..\Akka\Akka.csproj" /></ItemGroup></Project>
            """);
    }

    [Fact]
    public void Bindings_parses_table()
    {
        var output = _serialization.FindSerializerBindings([Root]);

        Assert.Contains("| System.Object | json |", output);
        Assert.Contains("| Akka.Actor.Foo | hyperion |", output);
    }

    [Fact]
    public void Bindings_filter_and_empty()
    {
        Assert.DoesNotContain("System.Object", _serialization.FindSerializerBindings([Root], "hyperion"));
        Assert.Equal("No serialization bindings found.", _serialization.FindSerializerBindings([Root], "zzz"));
    }

    [Fact]
    public void Detail_returns_source_and_config()
    {
        var output = _serialization.FindSerializerDetail([Root], "Json");

        Assert.Contains("class NewtonSoftJsonSerializer : Serializer", output);
        Assert.Contains("ToBinary", output);
        Assert.Contains("config: src/Akka/reference.conf", output);
        Assert.Contains("NewtonSoftJsonSerializer, Akka", output);
        Assert.DoesNotContain("Unrelated", output);
    }

    [Fact]
    public void Detail_reports_missing()
    {
        Assert.Contains("No serializer class found", _serialization.FindSerializerDetail([Root], "Nope"));
    }

    [Fact]
    public void Manifest_finds_patterns_in_persistence_only()
    {
        var output = _serialization.FindManifests([Root]);

        Assert.Contains("PersistentManifest", output);
        Assert.Contains("SerializerWithStringManifest", output);
        Assert.DoesNotContain("NewtonSoft", output);
    }

    [Fact]
    public void Manifest_reports_none()
    {
        Assert.Equal("No serialization manifest patterns found.", _serialization.FindManifests([Root], "Cluster"));
    }

    [Fact]
    public void DiPatterns_catalogs_categories()
    {
        var output = _hosting.FindDiPatterns([Root]);

        Assert.Contains("## Resolve actors via DI", output);
        Assert.Contains("## ActorRegistry", output);
        Assert.Contains("## Props with DI", output);
        Assert.Contains("src/Akka.Hosting/Hosting.cs:1", output);
    }

    [Fact]
    public void DiPatterns_reports_none()
    {
        var empty = _fs.Path.Combine(Root, "src", "Akka.Cluster");
        Assert.Equal("No Akka.Hosting DI patterns found.", _hosting.FindDiPatterns([empty]));
    }

    [Fact]
    public void TestKit_comparison_lists_both_packages()
    {
        var output = _hosting.CompareTestKits([Root]);

        Assert.Contains("| Akka.TestKit | TestKitBase | ITestKit |", output);
        Assert.Contains("ExpectMsg", output);
        Assert.Contains("Akka.Hosting.TestKit |", output);
    }

    [Fact]
    public void TestKit_comparison_reports_none()
    {
        Assert.Equal("No TestKit classes found.", _hosting.CompareTestKits([_fs.Path.Combine(Root, "src", "Akka.Cluster")]));
    }

    [Fact]
    public void TestKitHelpers_groups_methods()
    {
        var output = _testKit.FindTestKitHelpers([Root]);

        Assert.Contains("## Expect (message assertions)", output);
        Assert.Contains("TestKitBase.ExpectMsg", output);
        Assert.Contains("## Await (polling and waiting)", output);
        Assert.Contains("## Timing and interception", output);
    }

    [Fact]
    public void TestKitHelpers_filter()
    {
        var output = _testKit.FindTestKitHelpers([Root], "Within");

        Assert.Contains("Within", output);
        Assert.DoesNotContain("ExpectMsg", output);
    }

    [Fact]
    public void ClusterEvents_ordered_by_lifecycle_with_docs()
    {
        var output = _cluster.FindClusterEvents([Root]);

        var joined = output.IndexOf("MemberJoined [Joining]", StringComparison.Ordinal);
        var up = output.IndexOf("MemberUp [Up]", StringComparison.Ordinal);
        var removed = output.IndexOf("MemberRemoved [Removed]", StringComparison.Ordinal);
        var leader = output.IndexOf("LeaderChanged", StringComparison.Ordinal);

        Assert.True(joined >= 0 && joined < up && up < removed && removed < leader);
        Assert.Contains("Member is up.", output);
        Assert.Contains("LeaderChanged: (no documentation)", output);
    }

    [Fact]
    public void ClusterEvents_reports_none()
    {
        Assert.Equal("No cluster events found.", _cluster.FindClusterEvents([_fs.Path.Combine(Root, "src", "Akka")]));
    }

    [Fact]
    public void ExtensionPoints_finds_interfaces_and_implementors()
    {
        var output = _cluster.FindExtensionPoints([Root]);

        Assert.Contains("IThingProvider (src/Akka/IThingProvider.cs)", output);
        Assert.Contains("Provides things.", output);
        Assert.Contains("Implementors: DefaultThingProvider", output);
        Assert.Contains("IOtherPlugin", output);
        Assert.Contains("Implementors: (none found)", output);
    }

    [Fact]
    public void ExtensionPoints_reports_none()
    {
        Assert.Equal("No extension point interfaces found.", _cluster.FindExtensionPoints([_fs.Path.Combine(Root, "src", "Akka.Cluster")]));
    }

    [Fact]
    public void DependencyGraph_builds_tree()
    {
        var output = _cluster.BuildDependencyGraph([Root]);

        Assert.Contains("Akka.Cluster", output);
        Assert.Contains("+-- Akka.Remote (project)", output);
        Assert.Contains("+-- Akka (package)", output);
        Assert.DoesNotContain("Newtonsoft", output);
    }

    [Fact]
    public void DependencyGraph_filter()
    {
        var output = _cluster.BuildDependencyGraph([Root], "Remote");

        Assert.StartsWith("Akka.Remote", output);
        Assert.DoesNotContain("Akka.Cluster", output);
        Assert.Contains("No Akka projects found matching: zzz", _cluster.BuildDependencyGraph([Root], "zzz"));
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = _fs.Path.Combine(Root, relativePath);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(fullPath)!);
        _fs.File.WriteAllText(fullPath, content);
    }
}
