using System.IO.Abstractions;
using System.Net;
using System.Text.Json;
using Akr.Analysis;
using Akr.Configuration;
using Akr.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Akr.Tests;

public sealed class VersionResolutionTests : IDisposable
{
    private readonly string _cacheRoot;
    private readonly string _projectRoot;

    public VersionResolutionTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _projectRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_cacheRoot);
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, true);
        }

        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, true);
        }
    }

    [Fact]
    public async Task Project_detected_version_skips_github_api()
    {
        File.WriteAllText(Path.Combine(_projectRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var fileService = new FileService(new FileSystem());
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance);
        scanner.Current = scanner.Scan(_projectRoot);

        var handler = new FakeHandler();
        var client = new GitHubClient(
            new HttpClient(handler), NullLogger<GitHubClient>.Instance, fileService,
            scanner,
            Options.Create(new AkrOptions { CacheRoot = _cacheRoot }),
            TimeProvider.System);

        var version = await client.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Falls_back_to_github_when_no_project_detected()
    {
        var fileService = new FileService(new FileSystem());
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance);

        var handler = new FakeHandler(JsonSerializer.SerializeToUtf8Bytes(new { tag_name = "1.5.80" }));
        var client = new GitHubClient(
            new HttpClient(handler), NullLogger<GitHubClient>.Instance, fileService,
            scanner,
            Options.Create(new AkrOptions { CacheRoot = _cacheRoot }),
            TimeProvider.System);

        var version = await client.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.80", version);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Unmatched_repo_falls_back_to_github()
    {
        File.WriteAllText(Path.Combine(_projectRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var fileService = new FileService(new FileSystem());
        var scanner = new ProjectScanner(fileService, NullLogger<ProjectScanner>.Instance);
        scanner.Current = scanner.Scan(_projectRoot);

        var handler = new FakeHandler(JsonSerializer.SerializeToUtf8Bytes(new { tag_name = "1.5.10" }));
        var client = new GitHubClient(
            new HttpClient(handler), NullLogger<GitHubClient>.Instance, fileService,
            scanner,
            Options.Create(new AkrOptions { CacheRoot = _cacheRoot }),
            TimeProvider.System);

        var version = await client.ResolveLatestAsync("akkadotnet/Akka.Management", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.10", version);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly byte[]? _responseBody;
        public int CallCount { get; private set; }

        public FakeHandler(byte[]? responseBody = null)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_responseBody ?? [])
            };
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag-1\"");
            return Task.FromResult(response);
        }
    }
}
