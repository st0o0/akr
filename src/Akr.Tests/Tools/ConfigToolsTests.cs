using System.IO.Abstractions.TestingHelpers;
using Akr.Scanning;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class ConfigToolsTests
{
    private const string Root = "C:/test";
    private const string EmptyRoot = "C:/empty";
    private readonly MockFileSystem _fs = new();
    private readonly ConfigTools _tools;
    private readonly HostingTools _hosting;

    public ConfigToolsTests()
    {
        var fs = new FileService(_fs);
        _tools = new ConfigTools(fs, null!, new HoconScanner(fs));
        _hosting = new HostingTools(fs, null!, new HoconScanner(fs));

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

        _fs.Directory.CreateDirectory(EmptyRoot);
    }

    [Fact]
    public void ParseConfig_builds_full_paths()
    {
        var entries = HoconParser.ParseConfig(_fs.File.ReadAllLines(_fs.Path.Combine(Root, "src", "reference.conf")));
        var paths = entries.Select(e => e.Path).ToList();

        Assert.Contains("akka.actor.provider", paths);
        Assert.Contains("akka.cluster.min-nr-of-members", paths);
        Assert.Contains("akka.cluster.downing", paths);
    }

    [Fact]
    public void ConfigTree_annotates_hosting_methods()
    {
        var tree = _tools.BuildConfigTree([Root], "akka.cluster");

        Assert.Contains("  cluster", tree);
        Assert.Contains("roles = []  [Hosting: WithClustering]", tree);
        Assert.DoesNotContain("provider", tree);
    }

    [Fact]
    public void ConfigTree_reports_missing_path()
    {
        Assert.Contains("No config found", _tools.BuildConfigTree([Root], "akka.nope"));
    }

    [Fact]
    public void MapQuery_method_to_keys()
    {
        var output = _hosting.MapQuery([Root], "WithClustering");

        Assert.Contains("akka.cluster.roles", output);
    }

    [Fact]
    public void MapQuery_key_to_method()
    {
        var output = _hosting.MapQuery([Root], "akka.cluster.roles");

        Assert.Contains("WithClustering", output);
        Assert.Contains("No Hosting method found", _hosting.MapQuery([Root], "akka.zzz"));
    }

    [Fact]
    public void StartupOrder_finds_constraints_and_di_hints()
    {
        var output = _hosting.FindStartupOrder([Root], null);

        Assert.Contains("Recommended registration order", output);
        Assert.Contains("must be called before WithActors", output);
        Assert.Contains("GetRequiredService<IFoo>", output);
    }

    [Fact]
    public void StartupOrder_filter_excludes_files()
    {
        var output = _hosting.FindStartupOrder([Root], "nomatch");

        Assert.Contains("No ordering comments found", output);
    }

    [Fact]
    public void DiffConfig_detects_added_removed_changed()
    {
        var a = new Dictionary<string, string?> { ["x"] = "1", ["y"] = "2", ["z"] = "3" };
        var b = new Dictionary<string, string?> { ["x"] = "1", ["y"] = "9", ["w"] = "4" };

        var diff = ConfigTools.DiffConfig(a, b);

        Assert.Equal(["w"], diff.Added);
        Assert.Equal(["z"], diff.Removed);
        Assert.Equal(["y"], diff.Changed);
    }

    [Fact]
    public void DiffConfig_against_empty_lists_all_keys_as_added()
    {
        var current = new HoconScanner(new FileService(_fs)).CollectConfig([Root]);
        var diff = ConfigTools.DiffConfig(new Dictionary<string, string?>(), current);

        Assert.Equal(current.Count, diff.Added.Count);
        Assert.Contains("+ akka.cluster.roles", ConfigTools.FormatDiff(diff, current));
    }

    [Fact]
    public void DiffConfig_identical_returns_no_differences_message()
    {
        var a = new Dictionary<string, string?> { ["x"] = "1", ["y"] = "2" };
        var b = new Dictionary<string, string?> { ["x"] = "1", ["y"] = "2" };

        var diff = ConfigTools.DiffConfig(a, b);
        var output = ConfigTools.FormatDiff(diff, b);

        Assert.Contains("No configuration differences detected", output);
    }

    [Fact]
    public void Changelog_returns_sections()
    {
        var output = _tools.FindChangelog([Root], null);

        Assert.Contains("#### 1.5.0", output);
        Assert.Contains("#### 1.4.0", output);
    }

    [Fact]
    public void Changelog_filter_limits_entries()
    {
        var output = _tools.FindChangelog([Root], "breaking");

        Assert.Contains("1.5.0", output);
        Assert.DoesNotContain("1.4.0", output);
        Assert.Contains("No changelog entries found", _tools.FindChangelog([Root], "zzz"));
    }

    [Fact]
    public void MapQuery_fallback_returns_mapping_when_no_regex_match()
    {
        var output = _hosting.MapQuery([EmptyRoot], "WithClustering");

        Assert.Contains("WithClustering", output);
        Assert.Contains("akka.cluster", output);
        Assert.Contains("curated", output);
    }

    [Fact]
    public void MapQuery_regex_takes_precedence_over_fallback()
    {
        var output = _hosting.MapQuery([Root], "WithClustering");

        Assert.Contains("akka.cluster.roles", output);
        Assert.DoesNotContain("curated", output);
    }

    [Fact]
    public void MapQuery_fallback_returns_no_match_for_unknown()
    {
        var output = _hosting.MapQuery([EmptyRoot], "WithZzzNonexistent");

        Assert.Contains("No HOCON keys found", output);
    }

    [Fact]
    public void MapQuery_merges_regex_and_fallback_keys()
    {
        var output = _hosting.MapQuery([Root], "WithClustering");

        Assert.Contains("akka.cluster.roles", output);
        Assert.Contains("akka.cluster", output);
        Assert.DoesNotContain("curated", output);
    }

    [Fact]
    public void MapQuery_expanded_fallback_has_persistence_mappings()
    {
        var journal = _hosting.MapQuery([EmptyRoot], "WithJournal");
        Assert.Contains("akka.persistence.journal", journal);

        var snapshot = _hosting.MapQuery([EmptyRoot], "WithSnapshotStore");
        Assert.Contains("akka.persistence.snapshot-store", snapshot);

        var shutdown = _hosting.MapQuery([EmptyRoot], "WithCoordinatedShutdown");
        Assert.Contains("akka.coordinated-shutdown", shutdown);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = _fs.Path.Combine(Root, relativePath.Replace('/', _fs.Path.DirectorySeparatorChar));
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(fullPath)!);
        _fs.File.WriteAllText(fullPath, content);
    }
}
