using System.IO.Abstractions;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class EcosystemToolsTests : IDisposable
{
    private readonly string _root;
    private readonly EcosystemTools _tools;

    public EcosystemToolsTests()
    {
        _tools = new EcosystemTools(new FileService(new FileSystem()), null!);
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);

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
        var output = _tools.FindSerializerBindings([_root]);

        Assert.Contains("| System.Object | json |", output);
        Assert.Contains("| Akka.Actor.Foo | hyperion |", output);
    }

    [Fact]
    public void Bindings_filter_and_empty()
    {
        Assert.DoesNotContain("System.Object", _tools.FindSerializerBindings([_root], "hyperion"));
        Assert.Equal("No serialization bindings found.", _tools.FindSerializerBindings([_root], "zzz"));
    }

    [Fact]
    public void Detail_returns_source_and_config()
    {
        var output = _tools.FindSerializerDetail([_root], "Json");

        Assert.Contains("class NewtonSoftJsonSerializer : Serializer", output);
        Assert.Contains("ToBinary", output);
        Assert.Contains("config: src/Akka/reference.conf", output);
        Assert.Contains("NewtonSoftJsonSerializer, Akka", output);
        Assert.DoesNotContain("Unrelated", output);
    }

    [Fact]
    public void Detail_reports_missing()
    {
        Assert.Contains("No serializer class found", _tools.FindSerializerDetail([_root], "Nope"));
    }

    [Fact]
    public void Manifest_finds_patterns_in_persistence_only()
    {
        var output = _tools.FindManifests([_root]);

        Assert.Contains("PersistentManifest", output);
        Assert.Contains("SerializerWithStringManifest", output);
        Assert.DoesNotContain("NewtonSoft", output);
    }

    [Fact]
    public void Manifest_reports_none()
    {
        Assert.Equal("No serialization manifest patterns found.", _tools.FindManifests([_root], "Cluster"));
    }

    [Fact]
    public void DiPatterns_catalogs_categories()
    {
        var output = _tools.FindDiPatterns([_root]);

        Assert.Contains("## Resolve actors via DI", output);
        Assert.Contains("## ActorRegistry", output);
        Assert.Contains("## Props with DI", output);
        Assert.Contains("src/Akka.Hosting/Hosting.cs:1", output);
    }

    [Fact]
    public void DiPatterns_reports_none()
    {
        var empty = Path.Combine(_root, "src", "Akka.Cluster");
        Assert.Equal("No Akka.Hosting DI patterns found.", _tools.FindDiPatterns([empty]));
    }

    [Fact]
    public void TestKit_comparison_lists_both_packages()
    {
        var output = _tools.CompareTestKits([_root]);

        Assert.Contains("| Akka.TestKit | TestKitBase | ITestKit |", output);
        Assert.Contains("ExpectMsg", output);
        Assert.Contains("Akka.Hosting.TestKit |", output);
    }

    [Fact]
    public void TestKit_comparison_reports_none()
    {
        Assert.Equal("No TestKit classes found.", _tools.CompareTestKits([Path.Combine(_root, "src", "Akka.Cluster")]));
    }

    [Fact]
    public void TestKitHelpers_groups_methods()
    {
        var output = _tools.FindTestKitHelpers([_root]);

        Assert.Contains("## Expect (message assertions)", output);
        Assert.Contains("TestKitBase.ExpectMsg", output);
        Assert.Contains("## Await (polling and waiting)", output);
        Assert.Contains("## Timing and interception", output);
    }

    [Fact]
    public void TestKitHelpers_filter()
    {
        var output = _tools.FindTestKitHelpers([_root], "Within");

        Assert.Contains("Within", output);
        Assert.DoesNotContain("ExpectMsg", output);
    }

    [Fact]
    public void ClusterEvents_ordered_by_lifecycle_with_docs()
    {
        var output = _tools.FindClusterEvents([_root]);

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
        Assert.Equal("No cluster events found.", _tools.FindClusterEvents([Path.Combine(_root, "src", "Akka")]));
    }

    [Fact]
    public void ExtensionPoints_finds_interfaces_and_implementors()
    {
        var output = _tools.FindExtensionPoints([_root]);

        Assert.Contains("IThingProvider (src/Akka/IThingProvider.cs)", output);
        Assert.Contains("Provides things.", output);
        Assert.Contains("Implementors: DefaultThingProvider", output);
        Assert.Contains("IOtherPlugin", output);
        Assert.Contains("Implementors: (none found)", output);
    }

    [Fact]
    public void ExtensionPoints_reports_none()
    {
        Assert.Equal("No extension point interfaces found.", _tools.FindExtensionPoints([Path.Combine(_root, "src", "Akka.Cluster")]));
    }

    [Fact]
    public void DependencyGraph_builds_tree()
    {
        var output = _tools.BuildDependencyGraph([_root]);

        Assert.Contains("Akka.Cluster", output);
        Assert.Contains("+-- Akka.Remote (project)", output);
        Assert.Contains("+-- Akka (package)", output);
        Assert.DoesNotContain("Newtonsoft", output);
    }

    [Fact]
    public void DependencyGraph_filter()
    {
        var output = _tools.BuildDependencyGraph([_root], "Remote");

        Assert.StartsWith("Akka.Remote", output);
        Assert.DoesNotContain("Akka.Cluster", output);
        Assert.Contains("No Akka projects found matching: zzz", _tools.BuildDependencyGraph([_root], "zzz"));
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
