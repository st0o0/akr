using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Text.Json;
using Akr;
using Akr.Configuration;
using Akr.Scanning;
using Akr.Source;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Akr.Tests.Source;

public sealed class VersionResolverTests
{
    private const string CacheRoot = "C:/cache";
    private readonly MockFileSystem _fs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private SourceCache CreateCache() =>
        new(
            new FileService(_fs),
            NullLogger<SourceCache>.Instance,
            Options.Create(new AkrOptions { CacheRoot = CacheRoot }),
            _time);

    private ProjectScanner CreateScanner(string? projectPath = null) =>
        new(
            new FileService(_fs),
            NullLogger<ProjectScanner>.Instance,
            Options.Create(new AkrOptions { ProjectPath = projectPath }));

    private void WriteMetadata(string repoName, RepoMetadata metadata)
    {
        var path = $"{CacheRoot}/meta/{repoName}.json";
        var dir = _fs.Path.GetDirectoryName(path)!;
        _fs.Directory.CreateDirectory(dir);
        _fs.File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(metadata, AkrJsonContext.Default.RepoMetadata));
    }

    private static HttpClient CreateHttpClient(HttpStatusCode status, string? body = null)
    {
        var handler = new FakeHttpHandler(status, body);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task Returns_cached_version_when_TTL_is_fresh()
    {
        var cache = CreateCache();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = _time.GetUtcNow(),
            TagFormat = "v{0}"
        });

        _time.Advance(TimeSpan.FromMinutes(30));

        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.OK),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions { CacheTtlHours = 1 }),
            _time);

        var version = await resolver.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
    }

    [Fact]
    public async Task Fetches_from_GitHub_when_TTL_expired()
    {
        var cache = CreateCache();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = _time.GetUtcNow()
        });

        _time.Advance(TimeSpan.FromHours(2));

        var responseBody = JsonSerializer.Serialize(new { tag_name = "1.5.80" });
        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.OK, responseBody),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions { CacheTtlHours = 1 }),
            _time);

        var version = await resolver.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.80", version);
    }

    [Fact]
    public async Task Prefers_project_detected_version_over_cache()
    {
        var cache = CreateCache();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.70",
            LastChecked = _time.GetUtcNow()
        });

        _fs.Directory.CreateDirectory("C:/project");
        _fs.File.WriteAllText("C:/project/Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Akka" Version="1.5.75" />
              </ItemGroup>
            </Project>
            """);

        var scanner = CreateScanner("C:/project");

        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.InternalServerError),
            NullLogger<VersionResolver>.Instance,
            scanner,
            cache,
            Options.Create(new AkrOptions { CacheTtlHours = 1 }),
            _time);

        var version = await resolver.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
    }

    [Fact]
    public async Task Throws_on_rate_limit_403()
    {
        var cache = CreateCache();

        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.Forbidden),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions { CacheTtlHours = 0 }),
            _time);

        var ex = await Assert.ThrowsAsync<SourceFetchException>(
            () => resolver.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken));

        Assert.Contains("403", ex.Message);
    }

    [Fact]
    public async Task Stores_v_prefixed_tag_as_is()
    {
        var cache = CreateCache();

        var responseBody = JsonSerializer.Serialize(new { tag_name = "v1.5.75" });
        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.OK, responseBody),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions { CacheTtlHours = 0 }),
            _time);

        var version = await resolver.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("v1.5.75", version);
    }

    [Fact]
    public async Task Expires_at_exact_TTL_boundary()
    {
        var cache = CreateCache();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = _time.GetUtcNow()
        });

        _time.Advance(TimeSpan.FromHours(1));

        var responseBody = JsonSerializer.Serialize(new { tag_name = "1.5.80" });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, responseBody);
        var resolver = new VersionResolver(
            new HttpClient(handler),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions { CacheTtlHours = 1 }),
            _time);

        var version = await resolver.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.80", version);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void GetTagFormat_returns_null_before_any_resolve()
    {
        var cache = CreateCache();
        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.OK),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions()),
            _time);

        Assert.Null(resolver.GetTagFormat("akkadotnet/akka.net"));
    }

    [Fact]
    public void UpdateTagFormat_is_noop_without_metadata()
    {
        var cache = CreateCache();
        var resolver = new VersionResolver(
            CreateHttpClient(HttpStatusCode.OK),
            NullLogger<VersionResolver>.Instance,
            CreateScanner(),
            cache,
            Options.Create(new AkrOptions()),
            _time);

        resolver.UpdateTagFormat("akkadotnet/akka.net", "v{0}");

        Assert.Null(cache.ReadMetadata("akka.net"));
    }

    private sealed class FakeHttpHandler(HttpStatusCode statusCode, string? body = null) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
            {
                response.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
