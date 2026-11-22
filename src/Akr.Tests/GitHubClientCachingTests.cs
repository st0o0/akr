using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akr.Tests;

public sealed class GitHubClientCachingTests : IDisposable
{
    private const string Repo = "akkadotnet/akka.net";
    private const string RepoName = "akka.net";

    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), $"akr-cache-test-{Guid.NewGuid()}");
    private readonly FakeTimeProvider _time = new();

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveLatestAsync_fresh_metadata_returns_cached_without_http()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddHours(-1),
            TagFormat = "plain"
        });

        var handler = new CachingFakeHandler(_ => throw new InvalidOperationException("Should not be called"));
        var client = CreateClient(handler);

        var version = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ResolveLatestAsync_stale_metadata_sends_conditional_request_304()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddHours(-25),
            TagFormat = "plain"
        });

        var handler = new CachingFakeHandler(req =>
        {
            Assert.Equal("\"etag-1\"", req.Headers.IfNoneMatch.ToString());
            return (HttpStatusCode.NotModified, null, null);
        });
        var client = CreateClient(handler);

        var version = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
        Assert.Equal(1, handler.CallCount);

        var meta = client.ReadMetadata(RepoName);
        Assert.NotNull(meta);
        Assert.Equal(_time.GetUtcNow(), meta.LastChecked);
    }

    [Fact]
    public async Task ResolveLatestAsync_stale_metadata_200_updates_version()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddHours(-25),
            TagFormat = "plain"
        });

        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.76" }), "\"etag-2\""));
        var client = CreateClient(handler);

        var version = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal("1.5.76", version);

        var meta = client.ReadMetadata(RepoName);
        Assert.NotNull(meta);
        Assert.Equal("1.5.76", meta.Version);
        Assert.Equal("\"etag-2\"", meta.Etag);
    }

    [Fact]
    public async Task ResolveLatestAsync_missing_metadata_does_full_request()
    {
        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }), "\"etag-1\""));
        var client = CreateClient(handler);

        var version = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
        Assert.Equal(1, handler.CallCount);

        var meta = client.ReadMetadata(RepoName);
        Assert.NotNull(meta);
        Assert.Equal("1.5.75", meta.Version);
        Assert.Equal("\"etag-1\"", meta.Etag);
    }

    [Fact]
    public async Task ResolveLatestAsync_corrupt_metadata_does_full_request()
    {
        var metaDir = Path.Combine(_cacheRoot, "meta");
        Directory.CreateDirectory(metaDir);
        File.WriteAllText(Path.Combine(metaDir, $"{RepoName}.json"), "not-valid-json!!!");

        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }), "\"etag-1\""));
        var client = CreateClient(handler);

        var version = await client.ResolveLatestAsync(Repo, TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", version);
    }

    [Fact]
    public async Task EnsureSourceAsync_cleans_up_old_version()
    {
        var oldDir = Path.Combine(_cacheRoot, RepoName, "1.5.74");
        Directory.CreateDirectory(oldDir);
        File.WriteAllText(Path.Combine(oldDir, "marker.txt"), "old");

        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain"
        });

        var tarball = CreateTestTarGz("akka.net-1.5.75/", [
            ("akka.net-1.5.75/README.md", "# New"),
        ]);

        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, tarball, null));
        var client = CreateClient(handler);

        await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(oldDir));
        Assert.True(Directory.Exists(Path.Combine(_cacheRoot, RepoName, "1.5.75")));
    }

    [Fact]
    public async Task EnsureSourceAsync_cleanup_failure_does_not_throw()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain"
        });

        Directory.CreateDirectory(Path.Combine(_cacheRoot, RepoName, "1.5.75"));

        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }), null));
        var client = CreateClient(handler);

        var path = await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void ParseCacheTtl_default_is_24_hours()
    {
        Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", null);
        var ttl = GitHubClient.ParseCacheTtl(NullLogger.Instance);
        Assert.Equal(TimeSpan.FromHours(24), ttl);
    }

    [Fact]
    public void ParseCacheTtl_valid_value()
    {
        Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", "12");
        try
        {
            var ttl = GitHubClient.ParseCacheTtl(NullLogger.Instance);
            Assert.Equal(TimeSpan.FromHours(12), ttl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", null);
        }
    }

    [Fact]
    public void ParseCacheTtl_zero_means_always_check()
    {
        Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", "0");
        try
        {
            var ttl = GitHubClient.ParseCacheTtl(NullLogger.Instance);
            Assert.Equal(TimeSpan.Zero, ttl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", null);
        }
    }

    [Fact]
    public void ParseCacheTtl_invalid_falls_back_to_default()
    {
        Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", "banana");
        try
        {
            var ttl = GitHubClient.ParseCacheTtl(NullLogger.Instance);
            Assert.Equal(TimeSpan.FromHours(24), ttl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", null);
        }
    }

    [Fact]
    public async Task EnsureSourceAsync_uses_cached_tag_format_plain()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain"
        });

        var tarball = CreateTestTarGz("akka.net-1.5.75/", [
            ("akka.net-1.5.75/README.md", "# Akka"),
        ]);

        var requests = new List<string>();
        var handler = new CachingFakeHandler(req =>
        {
            requests.Add(req.RequestUri!.AbsoluteUri);
            if (req.RequestUri.AbsoluteUri.Contains(".tar.gz"))
            {
                return (HttpStatusCode.OK, tarball, null);
            }

            return (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }), null);
        });
        var client = CreateClient(handler);

        await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        var archiveRequests = requests.Where(r => r.Contains(".tar.gz")).ToList();
        Assert.Single(archiveRequests);
        Assert.Contains("/1.5.75.tar.gz", archiveRequests[0]);
        Assert.DoesNotContain("/v1.5.75.tar.gz", archiveRequests[0]);
    }

    [Fact]
    public async Task EnsureSourceAsync_uses_cached_tag_format_v_prefixed()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "v-prefixed"
        });

        var tarball = CreateTestTarGz("akka.net-v1.5.75/", [
            ("akka.net-v1.5.75/README.md", "# Akka"),
        ]);

        var requests = new List<string>();
        var handler = new CachingFakeHandler(req =>
        {
            requests.Add(req.RequestUri!.AbsoluteUri);
            if (req.RequestUri.AbsoluteUri.Contains(".tar.gz"))
            {
                return (HttpStatusCode.OK, tarball, null);
            }

            return (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }), null);
        });
        var client = CreateClient(handler);

        await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        var archiveRequests = requests.Where(r => r.Contains(".tar.gz")).ToList();
        Assert.Single(archiveRequests);
        Assert.Contains("/v1.5.75.tar.gz", archiveRequests[0]);
    }

    private GitHubClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<GitHubClient>.Instance, _cacheRoot,
            cacheTtl: TimeSpan.FromHours(24), timeProvider: _time);

    private void WriteTestMetadata(RepoMetadata metadata)
    {
        var metaDir = Path.Combine(_cacheRoot, "meta");
        Directory.CreateDirectory(metaDir);
        var json = JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllBytes(Path.Combine(metaDir, $"{RepoName}.json"), json);
    }

    private static byte[] JsonBody(object obj) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

    private static byte[] CreateTestTarGz(string prefix, (string path, string content)[] files)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
                };
                tarWriter.WriteEntry(entry);
            }
        }

        return memoryStream.ToArray();
    }

    private sealed class CachingFakeHandler(
        Func<HttpRequestMessage, (HttpStatusCode Status, byte[]? Body, string? Etag)> handler)
        : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var (status, body, etag) = handler(request);
            var response = new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(body ?? [])
            };
            if (etag is not null)
            {
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
