using System.IO.Abstractions.TestingHelpers;
using Akr.Scanning;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class ConfigReferenceToolTests
{
    private const string TestRoot = "C:/test";
    private const string AltRoot = "C:/alt";
    private readonly MockFileSystem _fs = new();
    private readonly ConfigTools _tools;

    public ConfigReferenceToolTests()
    {
        var fileService = new FileService(_fs);
        _tools = new ConfigTools(fileService, null!, new HoconScanner(fileService));

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
        var output = _tools.FindConfig([TestRoot], null);

        Assert.Contains("--- src/core/Akka/reference.conf ---", output);
        Assert.Contains("--- src/core/Akka.Persistence/reference.conf ---", output);
        Assert.Contains("throughput = 300", output);
    }

    [Fact]
    public void Path_filter_extracts_matching_block_only()
    {
        var output = _tools.FindConfig([TestRoot], "akka.persistence");

        Assert.Contains("--- src/core/Akka.Persistence/reference.conf ---", output);
        Assert.DoesNotContain("src/core/Akka/reference.conf", output);
        Assert.Contains("persistence {", output);
        Assert.DoesNotContain("loglevel", output);
        Assert.DoesNotContain("other", output);
    }

    [Fact]
    public void Nested_blocks_are_fully_extracted()
    {
        var output = _tools.FindConfig([TestRoot], "akka.persistence");

        Assert.Contains("journal {", output);
        Assert.Contains("snapshot-store {", output);
        Assert.Equal(3, output.Count(c => c == '}'));
    }

    [Fact]
    public void Extractor_returns_exact_block()
    {
        var lines = _fs.File.ReadAllLines(_fs.Path.Combine(TestRoot, "src/core/Akka/reference.conf"));
        var block = HoconParser.ExtractHoconBlock(lines, "akka.actor.default-dispatcher");

        Assert.NotNull(block);
        Assert.StartsWith("        default-dispatcher {", block);
        Assert.EndsWith("}", block.TrimEnd());
        Assert.Contains("throughput = 300", block);
        Assert.DoesNotContain("actor {", block);
    }

    [Fact]
    public void Extractor_handles_dotted_key_on_one_line()
    {
        var block = HoconParser.ExtractHoconBlock(["akka.remote {", "  port = 1", "}", "x = 2"], "akka.remote");

        Assert.Equal("akka.remote {\n  port = 1\n}", block);
    }

    [Fact]
    public void No_match_returns_message()
    {
        Assert.Equal("No config found for path: akka.nothing", _tools.FindConfig([TestRoot], "akka.nothing"));
    }

    [Fact]
    public void No_files_returns_message()
    {
        _fs.Directory.CreateDirectory($"{TestRoot}/empty");

        Assert.Equal("No reference.conf files found.", _tools.FindConfig([$"{TestRoot}/empty"], null));
    }

    [Fact]
    public void Finds_core_akka_conf_by_path()
    {
        WriteFile(AltRoot, "src/core/Akka/Configuration/akka.conf",
            """
            akka {
              actor {
                provider = "Akka.Actor.LocalActorRefProvider"
              }
            }
            """);

        var output = _tools.FindConfig([AltRoot], "akka.actor.provider");
        Assert.Contains("provider", output);
        Assert.DoesNotContain("No config found", output);
    }

    [Fact]
    public void Finds_config_in_real_akr_cache_layout()
    {
        const string root = "C:/layout";
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
        WriteFile(root, "src/Akka.Persistence.Sql/reference.conf",
            """
            akka.persistence.journal.sql {
              class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
            }
            """);

        var fileService = new FileService(_fs);
        var files = fileService.EnumerateConfigFiles([root]).Select(f => f.Relative).ToList();
        Assert.Equal(4, files.Count);

        var actorConfig = _tools.FindConfig([root], "akka.actor.provider");
        Assert.Contains("provider", actorConfig);
        Assert.DoesNotContain("No config found", actorConfig);

        var clusterConfig = _tools.FindConfig([root], "akka.cluster");
        Assert.Contains("cluster", clusterConfig);
        Assert.DoesNotContain("No config found", clusterConfig);
    }

    private void WriteFile(string relativePath, string content) =>
        WriteFile(TestRoot, relativePath, content);

    private void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = _fs.Path.Combine(root, relativePath.Replace('/', _fs.Path.DirectorySeparatorChar));
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(fullPath)!);
        _fs.File.WriteAllText(fullPath, content);
    }
}
