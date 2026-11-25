using System.IO.Abstractions;
using Akr.Scanning;
using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class HostingExtensionsToolTests : IDisposable
{
    private readonly string _testRoot;
    private readonly HostingTools _tools;

    public HostingExtensionsToolTests()
    {
        _tools = new HostingTools(new FileService(new FileSystem()), null!, new HoconScanner(new FileService(new FileSystem())));
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        WriteFile("Persistence/PersistenceExtensions.cs", """
            namespace Akka.Hosting;
            public static class PersistenceExtensions
            {
                public static AkkaConfigurationBuilder WithSqlPersistence(
                    this AkkaConfigurationBuilder builder,
                    string connectionString)
                {
                    return builder;
                }

                public static AkkaConfigurationBuilder WithJournalAndSnapshot(this AkkaConfigurationBuilder builder) => builder;

                public static string Helper(string x) => x;
            }
            """);

        WriteFile("Cluster/ClusterExtensions.cs", """
            namespace Akka.Hosting;
            public static class ClusterExtensions
            {
                public static IServiceCollection AddAkka<T>(this IServiceCollection services) => services;
            }
            """);
    }

    [Fact]
    public void Finds_With_and_Add_methods()
    {
        var results = _tools.FindExtensions([_testRoot]);
        var all = results.SelectMany(r => r.Signatures).ToList();

        Assert.Contains(all, s => s.Contains("WithSqlPersistence"));
        Assert.Contains(all, s => s.Contains("WithJournalAndSnapshot"));
        Assert.Contains(all, s => s.Contains("AddAkka<T>"));
    }

    [Fact]
    public void Extracts_multiline_signature()
    {
        var results = _tools.FindExtensions([_testRoot], "Persistence");
        var sig = results.Single().Signatures.First(s => s.Contains("WithSqlPersistence"));

        Assert.Contains("string connectionString", sig);
        Assert.DoesNotContain("{", sig);
        Assert.StartsWith("static ", sig);
    }

    [Fact]
    public void Filter_limits_files_case_insensitively()
    {
        var results = _tools.FindExtensions([_testRoot], "cluster");

        var result = Assert.Single(results);
        Assert.Equal("Cluster/ClusterExtensions.cs", result.File);
    }

    [Fact]
    public void Excludes_non_extension_static_methods()
    {
        var all = _tools.FindExtensions([_testRoot]).SelectMany(r => r.Signatures);

        Assert.DoesNotContain(all, s => s.Contains("Helper"));
    }

    [Fact]
    public void Format_reports_no_matches()
    {
        Assert.Equal("No hosting extensions found.", HostingTools.Format([], null));
        Assert.Contains("matching filter: X", HostingTools.Format([], "X"));
    }

    [Fact]
    public void Format_groups_by_file()
    {
        var output = HostingTools.Format(_tools.FindExtensions([_testRoot], "Cluster"), "Cluster");

        Assert.Contains("--- Cluster/ClusterExtensions.cs ---", output);
        Assert.Contains("  static IServiceCollection AddAkka<T>", output);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
