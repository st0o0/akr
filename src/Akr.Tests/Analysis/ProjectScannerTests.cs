using System.IO.Abstractions.TestingHelpers;
using Akr.Configuration;
using Akr.Registry;
using Akr.Scanning;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Akr.Tests.Analysis;

public sealed class ProjectScannerTests
{
    private const string TestRoot = "C:/test";
    private readonly MockFileSystem _fs = new();
    private readonly ProjectScanner _scanner;

    public ProjectScannerTests()
    {
        _scanner = new ProjectScanner(
            new FileService(_fs),
            NullLogger<ProjectScanner>.Instance,
            Options.Create(new AkrOptions()));
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = _fs.Path.Combine(TestRoot, relativePath);
        _fs.Directory.CreateDirectory(_fs.Path.GetDirectoryName(fullPath)!);
        _fs.File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void Scans_Directory_Packages_Props_for_Akka_versions()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
                <PackageVersion Include="Akka.Hosting" Version="1.5.71" />
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.True(result.HasPackages);
        Assert.Equal("1.5.75", result.Packages["Akka"]);
        Assert.Equal("1.5.71", result.Packages["Akka.Hosting"]);
        Assert.False(result.Packages.ContainsKey("Newtonsoft.Json"));
    }

    [Fact]
    public void Scans_csproj_for_inline_PackageReference()
    {
        WriteFile("MyApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Akka" Version="1.5.75" />
                <PackageReference Include="Akka.Remote" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.True(result.HasPackages);
        Assert.Equal("1.5.75", result.Packages["Akka"]);
        Assert.Equal("1.5.75", result.Packages["Akka.Remote"]);
    }

    [Fact]
    public void Skips_PackageReference_without_version()
    {
        WriteFile("MyApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Akka" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.False(result.HasPackages);
    }

    [Fact]
    public void Scans_Directory_Build_Props()
    {
        WriteFile("Directory.Build.props", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Akka.Persistence.Sql" Version="1.5.30" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.True(result.HasPackages);
        Assert.Equal("1.5.30", result.Packages["Akka.Persistence.Sql"]);
    }

    [Fact]
    public void Walks_up_directory_tree()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var subDir = _fs.Path.Combine(TestRoot, "src", "MyApp");
        _fs.Directory.CreateDirectory(subDir);

        var result = _scanner.Scan(subDir);

        Assert.True(result.HasPackages);
        Assert.Equal("1.5.75", result.Packages["Akka"]);
        Assert.Equal(_fs.Path.GetFullPath(TestRoot), result.ProjectPath);
    }

    [Fact]
    public void Returns_empty_when_no_project_files()
    {
        var emptyDir = _fs.Path.Combine(TestRoot, "empty");
        _fs.Directory.CreateDirectory(emptyDir);

        var result = _scanner.Scan(emptyDir, stopDirectory: TestRoot);

        Assert.False(result.HasPackages);
        Assert.Null(result.ProjectPath);
    }

    [Fact]
    public void Caches_results_per_path()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var result1 = _scanner.Scan(TestRoot);
        var result2 = _scanner.Scan(TestRoot);

        Assert.Same(result1, result2);
    }

    [Fact]
    public void Packages_props_takes_priority_over_csproj()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);
        WriteFile("MyApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Akka" Version="1.5.30" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.Equal("1.5.75", result.Packages["Akka"]);
    }

    [Fact]
    public void Maps_packages_to_repo_versions()
    {
        var packages = new Dictionary<string, string>
        {
            ["Akka"] = "1.5.75",
            ["Akka.Hosting"] = "1.5.71",
            ["Akka.Management"] = "1.5.10",
            ["Akka.Persistence.Sql"] = "1.5.30"
        };

        var repoVersions = RepoRegistry.MapToRepoVersions(packages);

        Assert.Equal("1.5.75", repoVersions["akkadotnet/akka.net"]);
        Assert.Equal("1.5.10", repoVersions["akkadotnet/Akka.Management"]);
        Assert.Equal("1.5.30", repoVersions["akkadotnet/Akka.Persistence.Sql"]);
    }

    [Fact]
    public void Detects_prerelease_versions()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.6.0-preview.3" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.True(result.HasPackages);
        Assert.Equal("1.6.0-preview.3", result.Packages["Akka"]);
    }

    [Fact]
    public void Handles_malformed_xml_gracefully()
    {
        WriteFile("Directory.Packages.props", "<not valid xml");

        var result = _scanner.Scan(TestRoot);

        Assert.False(result.HasPackages);
    }

    [Fact]
    public void Empty_props_file_returns_no_packages()
    {
        WriteFile("Directory.Packages.props", "<Project></Project>");

        var result = _scanner.Scan(TestRoot);

        Assert.False(result.HasPackages);
    }

    [Fact]
    public void Version_range_stored_verbatim()
    {
        WriteFile("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="[1.5.0, 2.0.0)" />
              </ItemGroup>
            </Project>
            """);

        var result = _scanner.Scan(TestRoot);

        Assert.True(result.HasPackages);
        Assert.Equal("[1.5.0, 2.0.0)", result.Packages["Akka"]);
    }

    [Fact]
    public void GetRelativePosixPath_normalizes_backslashes()
    {
        var fileService = new FileService(_fs);

        var result = fileService.GetRelativePosixPath("C:\\root", "C:\\root\\src\\file.cs");

        Assert.Equal("src/file.cs", result);
    }
}
