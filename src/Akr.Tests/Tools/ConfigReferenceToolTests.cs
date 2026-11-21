using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class ConfigReferenceToolTests : IDisposable
{
    private readonly string _testRoot;

    public ConfigReferenceToolTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        WriteFile("src/core/Akka/reference.conf", """
            akka {
                actor {
                    default-dispatcher {
                        type = Dispatcher
                        throughput = 300
                    }
                }
            }
            """);

        WriteFile("src/core/Akka.Persistence/reference.conf", """
            akka {
                loglevel = INFO
                persistence {
                    journal {
                        plugin = ""
                    }
                    snapshot-store {
                        plugin = ""
                    }
                }
                other {
                    x = 1
                }
            }
            """);
    }

    [Fact]
    public void Finds_all_reference_conf_files()
    {
        var output = HostingTools.FindConfig([_testRoot], null);

        Assert.Contains("--- src/core/Akka/reference.conf ---", output);
        Assert.Contains("--- src/core/Akka.Persistence/reference.conf ---", output);
        Assert.Contains("throughput = 300", output);
    }

    [Fact]
    public void Path_filter_extracts_matching_block_only()
    {
        var output = HostingTools.FindConfig([_testRoot], "akka.persistence");

        Assert.Contains("--- src/core/Akka.Persistence/reference.conf ---", output);
        Assert.DoesNotContain("src/core/Akka/reference.conf", output);
        Assert.Contains("persistence {", output);
        Assert.DoesNotContain("loglevel", output);
        Assert.DoesNotContain("other", output);
    }

    [Fact]
    public void Nested_blocks_are_fully_extracted()
    {
        var output = HostingTools.FindConfig([_testRoot], "akka.persistence");

        Assert.Contains("journal {", output);
        Assert.Contains("snapshot-store {", output);
        Assert.Equal(3, output.Count(c => c == '}'));
    }

    [Fact]
    public void Extractor_returns_exact_block()
    {
        var lines = File.ReadAllLines(Path.Combine(_testRoot, "src/core/Akka/reference.conf"));
        var block = HostingTools.ExtractHoconBlock(lines, "akka.actor.default-dispatcher");

        Assert.NotNull(block);
        Assert.StartsWith("        default-dispatcher {", block);
        Assert.EndsWith("}", block.TrimEnd());
        Assert.Contains("throughput = 300", block);
        Assert.DoesNotContain("actor {", block);
    }

    [Fact]
    public void Extractor_handles_dotted_key_on_one_line()
    {
        var block = HostingTools.ExtractHoconBlock(["akka.remote {", "  port = 1", "}", "x = 2"], "akka.remote");

        Assert.Equal("akka.remote {\n  port = 1\n}", block);
    }

    [Fact]
    public void No_match_returns_message()
    {
        Assert.Equal("No config found for path: akka.nothing", HostingTools.FindConfig([_testRoot], "akka.nothing"));
    }

    [Fact]
    public void No_files_returns_message()
    {
        var empty = Path.Combine(_testRoot, "empty");
        Directory.CreateDirectory(empty);

        Assert.Equal("No reference.conf files found.", HostingTools.FindConfig([empty], null));
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
