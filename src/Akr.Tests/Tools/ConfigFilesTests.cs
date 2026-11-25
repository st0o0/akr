using System.IO.Abstractions.TestingHelpers;

namespace Akr.Tests.Tools;

public sealed class ConfigFilesTests
{
    private const string TestRoot = "C:/test";
    private const string TestRoot2 = "C:/test2";
    private readonly MockFileSystem _fs = new();
    private readonly FileService _fileService;

    public ConfigFilesTests()
    {
        _fileService = new FileService(_fs);
    }

    [Fact]
    public void Finds_reference_conf()
    {
        WriteFile("src/contrib/Akka.Streams/reference.conf", "akka.stream { }");

        var files = _fileService.EnumerateConfigFiles([TestRoot]).ToList();

        Assert.Single(files);
        Assert.Contains("reference.conf", files[0].Relative);
    }

    [Fact]
    public void Finds_core_configuration_directory_conf()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { actor { } }");
        WriteFile("src/core/Akka.Cluster/Configuration/Cluster.conf", "akka { cluster { } }");
        WriteFile("src/core/Akka.Remote/Configuration/Remote.conf", "akka { remote { } }");

        var files = _fileService.EnumerateConfigFiles([TestRoot]).ToList();
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

        var files = _fileService.EnumerateConfigFiles([TestRoot]).ToList();

        Assert.Single(files);
        Assert.Contains("persistence.conf", files[0].Relative);
    }

    [Fact]
    public void Excludes_test_directories()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile("src/core/Akka.Tests/reference.conf", "test config");
        WriteFile("src/core/Akka.Persistence.Tests/reference.conf", "test config");

        var files = _fileService.EnumerateConfigFiles([TestRoot]).ToList();

        Assert.Single(files);
        Assert.DoesNotContain(files, f => f.Relative.Contains("Tests"));
    }

    [Fact]
    public void Excludes_example_directories()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile("src/examples/ClusterTools/app.conf", "example config");

        var files = _fileService.EnumerateConfigFiles([TestRoot]).ToList();

        Assert.Single(files);
        Assert.DoesNotContain(files, f => f.Relative.Contains("examples"));
    }

    [Fact]
    public void Ignores_non_config_conf_outside_known_locations()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile("src/other/app.conf", "not a config");
        WriteFile("resolv.conf", "nameserver 8.8.8.8");

        var files = _fileService.EnumerateConfigFiles([TestRoot]).ToList();

        Assert.Single(files);
        Assert.Contains("akka.conf", files[0].Relative);
    }

    [Fact]
    public void Multiple_roots_are_enumerated()
    {
        WriteFile("src/core/Akka/Configuration/akka.conf", "akka { }");
        WriteFile(TestRoot2, "some/reference.conf", "ext config");

        var files = _fileService.EnumerateConfigFiles([TestRoot, TestRoot2]).ToList();

        Assert.Equal(2, files.Count);
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
