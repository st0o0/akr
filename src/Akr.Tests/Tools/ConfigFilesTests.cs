using System.IO.Abstractions;

namespace Akr.Tests.Tools;

public sealed class ConfigFilesTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileService _fileService;

    public ConfigFilesTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
        _fileService = new FileService(new FileSystem());
    }

    [Fact]
    public void Finds_reference_conf()
    {
        WriteFile("src/contrib/Akka.Streams/reference.conf", "akka.stream { }");

        var files = _fileService.EnumerateConfigFiles([_testRoot]).ToList();

        Assert.Single(files);
        Assert.Contains("reference.conf", files[0].Relative);
    }

    [Fact]
    public void Finds_core_configuration_directory_conf()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { actor { } }");
        WriteFile("src/core/Akka.Cluster/Configuration/Cluster.conf", "akka { cluster { } }");
        WriteFile("src/core/Akka.Remote/Configuration/Remote.conf", "akka { remote { } }");

        var files = _fileService.EnumerateConfigFiles([_testRoot]).ToList();
        var relatives = files.Select(f => f.Relative).ToList();

        Assert.Equal(3, files.Count);
        Assert.Contains(relatives, r => r.Contains("akka.conf"));
        Assert.Contains(relatives, r => r.Contains("Cluster.conf"));
        Assert.Contains(relatives, r => r.Contains("Remote.conf"));
    }

    [Fact]
    public void Finds_core_persistence_conf()
    {
        WriteFile("src/core/Akka.Persistence/persistence.conf", "akka { persistence { } }");

        var files = _fileService.EnumerateConfigFiles([_testRoot]).ToList();

        Assert.Single(files);
        Assert.Contains("persistence.conf", files[0].Relative);
    }

    [Fact]
    public void Excludes_test_directories()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile("src/core/Akka.Tests/reference.conf", "test config");
        WriteFile("src/core/Akka.Persistence.Tests/reference.conf", "test config");

        var files = _fileService.EnumerateConfigFiles([_testRoot]).ToList();

        Assert.Single(files);
        Assert.DoesNotContain(files, f => f.Relative.Contains("Tests"));
    }

    [Fact]
    public void Excludes_example_directories()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile("src/examples/ClusterTools/app.conf", "example config");

        var files = _fileService.EnumerateConfigFiles([_testRoot]).ToList();

        Assert.Single(files);
        Assert.DoesNotContain(files, f => f.Relative.Contains("examples"));
    }

    [Fact]
    public void Ignores_non_config_conf_outside_known_locations()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile("src/other/app.conf", "not a config");
        WriteFile("resolv.conf", "nameserver 8.8.8.8");

        var files = _fileService.EnumerateConfigFiles([_testRoot]).ToList();

        Assert.Single(files);
        Assert.Contains("akka.conf", files[0].Relative);
    }

    [Fact]
    public void Multiple_roots_are_enumerated()
    {
        var root2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root2);
        try
        {
            WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
            WriteFile(root2, "some/reference.conf", "ext config");

            var files = _fileService.EnumerateConfigFiles([_testRoot, root2]).ToList();

            Assert.Equal(2, files.Count);
        }
        finally
        {
            Directory.Delete(root2, recursive: true);
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
