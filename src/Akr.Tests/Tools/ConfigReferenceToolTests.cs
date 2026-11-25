using System.IO.Abstractions;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class ConfigReferenceToolTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ConfigTools _tools;

    public ConfigReferenceToolTests()
    {
        _tools = new ConfigTools(new FileService(new FileSystem()), null!);
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
        var output = _tools.FindConfig([_testRoot], null);

        Assert.Contains("--- src/core/Akka/reference.conf ---", output);
        Assert.Contains("--- src/core/Akka.Persistence/reference.conf ---", output);
        Assert.Contains("throughput = 300", output);
    }

    [Fact]
    public void Path_filter_extracts_matching_block_only()
    {
        var output = _tools.FindConfig([_testRoot], "akka.persistence");

        Assert.Contains("--- src/core/Akka.Persistence/reference.conf ---", output);
        Assert.DoesNotContain("src/core/Akka/reference.conf", output);
        Assert.Contains("persistence {", output);
        Assert.DoesNotContain("loglevel", output);
        Assert.DoesNotContain("other", output);
    }

    [Fact]
    public void Nested_blocks_are_fully_extracted()
    {
        var output = _tools.FindConfig([_testRoot], "akka.persistence");

        Assert.Contains("journal {", output);
        Assert.Contains("snapshot-store {", output);
        Assert.Equal(3, output.Count(c => c == '}'));
    }

    [Fact]
    public void Extractor_returns_exact_block()
    {
        var lines = File.ReadAllLines(Path.Combine(_testRoot, "src/core/Akka/reference.conf"));
        var block = ConfigTools.ExtractHoconBlock(lines, "akka.actor.default-dispatcher");

        Assert.NotNull(block);
        Assert.StartsWith("        default-dispatcher {", block);
        Assert.EndsWith("}", block.TrimEnd());
        Assert.Contains("throughput = 300", block);
        Assert.DoesNotContain("actor {", block);
    }

    [Fact]
    public void Extractor_handles_dotted_key_on_one_line()
    {
        var block = ConfigTools.ExtractHoconBlock(["akka.remote {", "  port = 1", "}", "x = 2"], "akka.remote");

        Assert.Equal("akka.remote {\n  port = 1\n}", block);
    }

    [Fact]
    public void No_match_returns_message()
    {
        Assert.Equal("No config found for path: akka.nothing", _tools.FindConfig([_testRoot], "akka.nothing"));
    }

    [Fact]
    public void No_files_returns_message()
    {
        var empty = Path.Combine(_testRoot, "empty");
        Directory.CreateDirectory(empty);

        Assert.Equal("No reference.conf files found.", _tools.FindConfig([empty], null));
    }

    [Fact]
    public void Finds_core_akka_conf_by_path()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            WriteFile(root, "src/core/Akka/Configuration/akka.conf",
                """
                akka {
                  actor {
                    provider = "Akka.Actor.LocalActorRefProvider"
                  }
                }
                """);

            var output = _tools.FindConfig([root], "akka.actor.provider");
            Assert.Contains("provider", output);
            Assert.DoesNotContain("No config found", output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Finds_config_in_real_akr_cache_layout()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            // Mirror the actual layout: core configs are NOT reference.conf
            WriteFile(root, "src/core/Akka/Configuration/akka.conf",
                """
                akka {
                  actor {
                    provider = "Akka.Actor.LocalActorRefProvider"
                  }
                }
                """);
            WriteFile(root, "src/core/Akka.Cluster/Configuration/Cluster.conf",
                """
                akka {
                  cluster {
                    roles = []
                  }
                }
                """);
            WriteFile(root, "src/core/Akka.Persistence/persistence.conf",
                """
                akka {
                  persistence {
                    journal {
                      plugin = ""
                    }
                  }
                }
                """);
            // Satellite repos DO use reference.conf
            WriteFile(root, "src/Akka.Persistence.Sql/reference.conf",
                """
                akka.persistence.journal.sql {
                  class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
                }
                """);

            // Enumerate should find all 4
            var fs = new FileService(new System.IO.Abstractions.FileSystem());
            var files = fs.EnumerateConfigFiles([root]).Select(f => f.Relative).ToList();
            Assert.Equal(4, files.Count);

            // config_reference should find akka.actor.provider
            var actorConfig = _tools.FindConfig([root], "akka.actor.provider");
            Assert.Contains("provider", actorConfig);
            Assert.DoesNotContain("No config found", actorConfig);

            // config_reference should find akka.cluster
            var clusterConfig = _tools.FindConfig([root], "akka.cluster");
            Assert.Contains("cluster", clusterConfig);
            Assert.DoesNotContain("No config found", clusterConfig);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string content) =>
        WriteFile(_testRoot, relativePath, content);

    private static void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
