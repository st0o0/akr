using System.IO.Abstractions;
using Akr.Scanning;
using Akr.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akr.Tests.Tools;

public sealed class ProjectContextToolsTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ProjectScanner _scanner;
    private readonly ProjectContextTools _tools;

    public ProjectContextToolsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
        var fileService = new FileService(new FileSystem());
        _scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance);
        _tools = new ProjectContextTools(_scanner, fileService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void SetProjectContext_with_valid_project_returns_detected_packages()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var result = _tools.SetProjectContext(_testRoot);

        Assert.Contains("Akka = 1.5.75", result);
        Assert.Contains("akkadotnet/akka.net", result);
        Assert.True(_scanner.Current.HasPackages);
    }

    [Fact]
    public void SetProjectContext_with_no_akka_packages_returns_message()
    {
        WriteFile("nopkgs/Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        var result = _tools.SetProjectContext(Path.Combine(_testRoot, "nopkgs"));

        Assert.Contains("No Akka.NET packages found", result);
    }

    [Fact]
    public void SetProjectContext_with_invalid_path_returns_error()
    {
        var result = _tools.SetProjectContext(Path.Combine(_testRoot, "nonexistent"));

        Assert.Contains("Error: directory does not exist", result);
    }

    [Fact]
    public void SetProjectContext_overrides_previous_context()
    {
        WriteFile("projA/Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);
        WriteFile("projB/Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.6.0-preview.1" />
              </ItemGroup>
            </Project>
            """);

        _tools.SetProjectContext(Path.Combine(_testRoot, "projA"));
        Assert.Equal("1.5.75", _scanner.Current.Packages["Akka"]);

        _tools.SetProjectContext(Path.Combine(_testRoot, "projB"));
        Assert.Equal("1.6.0-preview.1", _scanner.Current.Packages["Akka"]);
    }
}

public sealed class ProjectContextResourcesTests : IDisposable
{
    private readonly string _testRoot;

    public ProjectContextResourcesTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    [Fact]
    public void GetContext_with_project_detected_returns_project_source()
    {
        var fileService = new FileService(new FileSystem());
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance);
        scanner.Current = new ProjectScanResult(_testRoot, new Dictionary<string, string>
        {
            ["Akka"] = "1.5.75"
        });

        var resources = new ProjectContextResources(scanner);
        var json = resources.GetContext();

        Assert.Contains("\"source\": \"project\"", json);
        Assert.Contains("\"Akka\": \"1.5.75\"", json);
        Assert.Contains(_testRoot.Replace("\\", "\\\\"), json);
    }

    [Fact]
    public void GetContext_without_project_returns_latest_source()
    {
        var fileService = new FileService(new FileSystem());
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance);

        var resources = new ProjectContextResources(scanner);
        var json = resources.GetContext();

        Assert.Contains("\"source\": \"latest\"", json);
    }
}
