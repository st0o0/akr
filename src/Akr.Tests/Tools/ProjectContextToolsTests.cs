using System.IO.Abstractions.TestingHelpers;
using Akr.Configuration;
using Akr.Scanning;
using Akr.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Akr.Tests.Tools;

public sealed class ProjectContextToolsTests
{
    private const string TestRoot = "C:/test";
    private readonly MockFileSystem _fs = new();
    private readonly ProjectScanner _scanner;
    private readonly ProjectContextTools _tools;

    public ProjectContextToolsTests()
    {
        var fileService = new FileService(_fs);
        _scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance, Options.Create(new AkrOptions()));
        _tools = new ProjectContextTools(_scanner, fileService);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = _fs.Path.Combine(TestRoot, relativePath);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(fullPath)!);
        _fs.File.WriteAllText(fullPath, content);
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

        var result = _tools.SetProjectContext(TestRoot);

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

        var result = _tools.SetProjectContext(_fs.Path.Combine(TestRoot, "nopkgs"));

        Assert.Contains("No Akka.NET packages found", result);
    }

    [Fact]
    public void SetProjectContext_with_invalid_path_returns_error()
    {
        var result = _tools.SetProjectContext(_fs.Path.Combine(TestRoot, "nonexistent"));

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

        _tools.SetProjectContext(_fs.Path.Combine(TestRoot, "projA"));
        Assert.Equal("1.5.75", _scanner.Current.Packages["Akka"]);

        _tools.SetProjectContext(_fs.Path.Combine(TestRoot, "projB"));
        Assert.Equal("1.6.0-preview.1", _scanner.Current.Packages["Akka"]);
    }
}

public sealed class ProjectContextResourcesTests
{
    private const string TestRoot = "C:/test";

    [Fact]
    public void GetContext_with_project_detected_returns_project_source()
    {
        var fs = new MockFileSystem();
        var fileService = new FileService(fs);
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance, Options.Create(new AkrOptions()));
        scanner.Current = new ProjectScanResult(TestRoot, new Dictionary<string, string>
        {
            ["Akka"] = "1.5.75"
        });

        var resources = new ProjectContextResources(scanner);
        var json = resources.GetContext();

        Assert.Contains("\"source\": \"project\"", json);
        Assert.Contains("\"Akka\": \"1.5.75\"", json);
        Assert.Contains(TestRoot, json);
    }

    [Fact]
    public void GetContext_without_project_returns_latest_source()
    {
        var fs = new MockFileSystem();
        var fileService = new FileService(fs);
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance, Options.Create(new AkrOptions()));

        var resources = new ProjectContextResources(scanner);
        var json = resources.GetContext();

        Assert.Contains("\"source\": \"latest\"", json);
    }
}
