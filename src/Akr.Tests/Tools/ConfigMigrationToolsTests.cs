using System.IO.Abstractions;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class ConfigMigrationToolsTests : IDisposable
{
    private readonly string _root;
    private readonly ConfigMigrationTools _tools;

    public ConfigMigrationToolsTests()
    {
        _tools = new ConfigMigrationTools(new FileService(new FileSystem()), null!);
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);

        WriteFile("src/reference.conf", """
            akka {
              actor.provider = local
              cluster {
                roles = []
                min-nr-of-members = 1
                # comment
                downing { enabled = on }
              }
            }
            """);

        WriteFile("hosting/ClusterExtensions.cs", """
            namespace Akka.Hosting;
            public static class ClusterExtensions
            {
                // WithClustering must be called before WithActors.
                public static AkkaConfigurationBuilder WithClustering(this AkkaConfigurationBuilder b)
                {
                    var cfg = "akka.cluster.roles = []";
                    var x = sp.GetRequiredService<IFoo>();
                    return b;
                }
            }
            """);

        WriteFile("RELEASE_NOTES.md", """
            # Release Notes
            #### 1.5.0
            Breaking change: removed Foo.
            #### 1.4.0
            Added Bar.
            """);
    }

    [Fact]
    public void ParseConfig_builds_full_paths()
    {
        var entries = ConfigMigrationTools.ParseConfig(File.ReadAllLines(Path.Combine(_root, "src", "reference.conf")));
        var paths = entries.Select(e => e.Path).ToList();

        Assert.Contains("akka.actor.provider", paths);
        Assert.Contains("akka.cluster.min-nr-of-members", paths);
        Assert.Contains("akka.cluster.downing", paths);
    }

    [Fact]
    public void ConfigTree_annotates_hosting_methods()
    {
        var tree = _tools.BuildConfigTree([_root], "akka.cluster");

        Assert.Contains("  cluster", tree);
        Assert.Contains("roles = []  [Hosting: WithClustering]", tree);
        Assert.DoesNotContain("provider", tree);
    }

    [Fact]
    public void ConfigTree_reports_missing_path()
    {
        Assert.Contains("No config found", _tools.BuildConfigTree([_root], "akka.nope"));
    }

    [Fact]
    public void MapQuery_method_to_keys()
    {
        var output = _tools.MapQuery([_root], "WithClustering");

        Assert.Contains("akka.cluster.roles", output);
    }

    [Fact]
    public void MapQuery_key_to_method()
    {
        var output = _tools.MapQuery([_root], "akka.cluster.roles");

        Assert.Contains("WithClustering", output);
        Assert.Contains("No Hosting method found", _tools.MapQuery([_root], "akka.zzz"));
    }

    [Fact]
    public void StartupOrder_finds_constraints_and_di_hints()
    {
        var output = _tools.FindStartupOrder([_root], null);

        Assert.Contains("Recommended registration order", output);
        Assert.Contains("must be called before WithActors", output);
        Assert.Contains("GetRequiredService<IFoo>", output);
    }

    [Fact]
    public void StartupOrder_filter_excludes_files()
    {
        var output = _tools.FindStartupOrder([_root], "nomatch");

        Assert.Contains("No ordering comments found", output);
    }

    [Fact]
    public void DiffConfig_detects_added_removed_changed()
    {
        var a = new Dictionary<string, string?> { ["x"] = "1", ["y"] = "2", ["z"] = "3" };
        var b = new Dictionary<string, string?> { ["x"] = "1", ["y"] = "9", ["w"] = "4" };

        var diff = ConfigMigrationTools.DiffConfig(a, b);

        Assert.Equal(["w"], diff.Added);
        Assert.Equal(["z"], diff.Removed);
        Assert.Equal(["y"], diff.Changed);
    }

    [Fact]
    public void DiffConfig_against_empty_lists_all_keys_as_added()
    {
        var current = _tools.CollectConfig([_root]);
        var diff = ConfigMigrationTools.DiffConfig(new Dictionary<string, string?>(), current);

        Assert.Equal(current.Count, diff.Added.Count);
        Assert.Contains("+ akka.cluster.roles", ConfigMigrationTools.FormatDiff(diff, current));
    }

    [Fact]
    public void Changelog_returns_sections()
    {
        var output = _tools.FindChangelog([_root], null);

        Assert.Contains("#### 1.5.0", output);
        Assert.Contains("#### 1.4.0", output);
    }

    [Fact]
    public void Changelog_filter_limits_entries()
    {
        var output = _tools.FindChangelog([_root], "breaking");

        Assert.Contains("1.5.0", output);
        Assert.DoesNotContain("1.4.0", output);
        Assert.Contains("No changelog entries found", _tools.FindChangelog([_root], "zzz"));
    }

    [Fact]
    public void MapQuery_fallback_returns_mapping_when_no_regex_match()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var output = _tools.MapQuery([emptyRoot], "WithClustering");

            Assert.Contains("WithClustering", output);
            Assert.Contains("akka.cluster", output);
            Assert.Contains("curated", output);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    [Fact]
    public void MapQuery_regex_takes_precedence_over_fallback()
    {
        var output = _tools.MapQuery([_root], "WithClustering");

        Assert.Contains("akka.cluster.roles", output);
        Assert.DoesNotContain("curated", output);
    }

    [Fact]
    public void MapQuery_fallback_returns_no_match_for_unknown()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var output = _tools.MapQuery([emptyRoot], "WithZzzNonexistent");

            Assert.Contains("No HOCON keys found", output);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    [Fact]
    public void MapQuery_merges_regex_and_fallback_keys()
    {
        var output = _tools.MapQuery([_root], "WithClustering");

        Assert.Contains("akka.cluster.roles", output);
        Assert.Contains("akka.cluster", output);
        Assert.DoesNotContain("curated", output);
    }

    [Fact]
    public void MapQuery_expanded_fallback_has_persistence_mappings()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var journal = _tools.MapQuery([emptyRoot], "WithJournal");
            Assert.Contains("akka.persistence.journal", journal);

            var snapshot = _tools.MapQuery([emptyRoot], "WithSnapshotStore");
            Assert.Contains("akka.persistence.snapshot-store", snapshot);

            var shutdown = _tools.MapQuery([emptyRoot], "WithCoordinatedShutdown");
            Assert.Contains("akka.coordinated-shutdown", shutdown);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
