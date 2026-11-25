using System.Net;
using Akr.Analysis;
using Akr.Configuration;
using Akr.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Akr.Tests.TestHelpers;

namespace Akr.Tests;

public sealed class GitHubClientTests : IDisposable
{
    private const string Repo = "akkadotnet/akka.net";

    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), $"akr-gh-test-{Guid.NewGuid()}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureSourceAsync_falls_back_to_v_prefixed_tag()
    {
        var tarball = CreateTestTarGz("akka.net-1.5.30/", [
            ("akka.net-1.5.30/src/core/Akka/Actor.cs", "public class Actor {}"),
            ("akka.net-1.5.30/README.md", "# Akka.NET"),
        ]);

        var handler = new FakeHandler(req =>
        {
            var url = req.RequestUri!.AbsoluteUri;
            if (req.RequestUri.Host == "api.github.com")
            {
                return (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.30" }));
            }

            return url.EndsWith("/v1.5.30.tar.gz", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, tarball)
                : (HttpStatusCode.NotFound, []);
        });

        var client = CreateClient(handler);

        var path = await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(path, "src", "core", "Akka", "Actor.cs")));
        Assert.True(File.Exists(Path.Combine(path, "README.md")));
        Assert.False(Directory.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task EnsureSourceAsync_cache_hit_skips_archive_download()
    {
        Directory.CreateDirectory(Path.Combine(_cacheRoot, "akka.net", "1.5.30"));
        var handler = new FakeHandler(req => req.RequestUri!.Host == "api.github.com"
            ? (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.30" }))
            : (HttpStatusCode.InternalServerError, []));

        var client = CreateClient(handler);

        var path = await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_cacheRoot, "akka.net", "1.5.30"), path);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, handler.CountFor("github.com"));
    }

    [Fact]
    public async Task EnsureSourceAsync_failed_download_leaves_no_cache()
    {
        var handler = new FakeHandler(req => req.RequestUri!.Host == "api.github.com"
            ? (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.30" }))
            : (HttpStatusCode.InternalServerError, []));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SourceFetchException>(
            () => client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken));

        var target = Path.Combine(_cacheRoot, "akka.net", "1.5.30");
        Assert.False(Directory.Exists(target));
        Assert.False(Directory.Exists(target + ".tmp"));
    }

    [Fact]
    public async Task EnsureSourceAsync_rate_limit_throws()
    {
        var handler = new FakeHandler(req => req.RequestUri!.Host == "api.github.com"
            ? (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.30" }))
            : (HttpStatusCode.TooManyRequests, []));

        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SourceFetchException>(
            () => client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken));

        Assert.Contains("rate limit", ex.Message);
    }

    [Fact]
    public async Task ResolveLatestAsync_server_error_throws()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.InternalServerError, []));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SourceFetchException>(
            () => client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveLatestAsync_missing_tag_name_throws()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, JsonBody(new { name = "v1.0" })));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SourceFetchException>(
            () => client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken));

        Assert.Contains("tag_name", ex.Message);
    }

    [Fact]
    public async Task ResolveLatestAsync_caches_result()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" })));
        var client = CreateClient(handler);

        var v1 = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);
        var v2 = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", v1);
        Assert.Equal("1.5.75", v2);
        Assert.Equal(1, handler.CallCount);
    }

    private GitHubClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<GitHubClient>.Instance,
            new FileService(new System.IO.Abstractions.FileSystem()),
            new ProjectScanner(new FileService(new System.IO.Abstractions.FileSystem()), NullLogger<ProjectScanner>.Instance),
            Options.Create(new AkrOptions { CacheRoot = _cacheRoot }),
            TimeProvider.System);

    private sealed class FakeHandler(Func<HttpRequestMessage, (HttpStatusCode Status, byte[] Body)> handler)
        : HttpMessageHandler
    {
        private readonly List<string> _hosts = [];

        public int CallCount => _hosts.Count;

        public int CountFor(string host) => _hosts.Count(h => h == host);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _hosts.Add(request.RequestUri!.Host);
            var (status, body) = handler(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }
}
