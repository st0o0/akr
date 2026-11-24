using System.Net;
using System.Text.Json;
using Akr.Analysis;
using Akr.Configuration;
using Akr.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Akr.Tests.TestHelpers;

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
    public async Task EnsureSourceAsync_retains_old_version_under_limit()
    {
        var oldDir = Path.Combine(_cacheRoot, RepoName, "1.5.74");
        Directory.CreateDirectory(oldDir);
        File.WriteAllText(Path.Combine(oldDir, "marker.txt"), "old");

        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain",
            CachedVersions = [new CachedVersionEntry { Version = "1.5.74", LastAccessed = _time.GetUtcNow().AddHours(-2), TagFormat = "plain" }]
        });

        var tarball = CreateTestTarGz("akka.net-1.5.75/", [
            ("akka.net-1.5.75/README.md", "# New"),
        ]);

        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, tarball, null));
        var client = CreateClient(handler);

        await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(oldDir), "Old version should be retained under LRU limit");
        Assert.True(Directory.Exists(Path.Combine(_cacheRoot, RepoName, "1.5.75")));
    }

    [Fact]
    public async Task EnsureSourceAsync_evicts_lru_when_over_limit()
    {
        var v1Dir = Path.Combine(_cacheRoot, RepoName, "1.5.72");
        var v2Dir = Path.Combine(_cacheRoot, RepoName, "1.5.73");
        var v3Dir = Path.Combine(_cacheRoot, RepoName, "1.5.74");
        Directory.CreateDirectory(v1Dir);
        Directory.CreateDirectory(v2Dir);
        Directory.CreateDirectory(v3Dir);

        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain",
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.72", LastAccessed = _time.GetUtcNow().AddHours(-3), TagFormat = "plain" },
                new CachedVersionEntry { Version = "1.5.73", LastAccessed = _time.GetUtcNow().AddHours(-2), TagFormat = "plain" },
                new CachedVersionEntry { Version = "1.5.74", LastAccessed = _time.GetUtcNow().AddHours(-1), TagFormat = "plain" },
            ]
        });

        var tarball = CreateTestTarGz("akka.net-1.5.75/", [
            ("akka.net-1.5.75/README.md", "# New"),
        ]);

        var handler = new CachingFakeHandler(_ =>
            (HttpStatusCode.OK, tarball, null));
        var client = CreateClient(handler, maxCachedVersions: 3);

        await client.EnsureSourceAsync(Repo, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(v1Dir), "Oldest version should be evicted");
        Assert.True(Directory.Exists(v2Dir), "Recent version should be retained");
        Assert.True(Directory.Exists(v3Dir), "Recent version should be retained");
        Assert.True(Directory.Exists(Path.Combine(_cacheRoot, RepoName, "1.5.75")));
    }

    [Fact]
    public async Task EnsureSourceForVersionAsync_fetches_specific_version()
    {
        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain"
        });

        var tarball = CreateTestTarGz("akka.net-1.5.52/", [
            ("akka.net-1.5.52/README.md", "# Old version"),
        ]);

        var requests = new List<string>();
        var handler = new CachingFakeHandler(req =>
        {
            requests.Add(req.RequestUri!.AbsoluteUri);
            return (HttpStatusCode.OK, tarball, null);
        });
        var client = CreateClient(handler);

        var path = await client.EnsureSourceForVersionAsync(Repo, "1.5.52", TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(path));
        Assert.Contains("1.5.52.tar.gz", requests.Single(r => r.Contains(".tar.gz")));

        var meta = client.ReadMetadata(RepoName);
        Assert.NotNull(meta);
        Assert.Contains(meta.CachedVersions, v => v.Version == "1.5.52");
    }

    [Fact]
    public async Task EnsureSourceForVersionAsync_returns_cached_version_without_http()
    {
        Directory.CreateDirectory(Path.Combine(_cacheRoot, RepoName, "1.5.52"));

        WriteTestMetadata(new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"etag-1\"",
            LastChecked = _time.GetUtcNow().AddMinutes(-5),
            TagFormat = "plain",
            CachedVersions = [new CachedVersionEntry { Version = "1.5.52", LastAccessed = _time.GetUtcNow().AddHours(-1), TagFormat = "plain" }]
        });

        var handler = new CachingFakeHandler(_ => throw new InvalidOperationException("Should not be called"));
        var client = CreateClient(handler);

        var path = await client.EnsureSourceForVersionAsync(Repo, "1.5.52", TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(path));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void RepoMetadata_cachedVersions_defaults_to_empty()
    {
        var meta = new RepoMetadata { Version = "1.5.75" };
        Assert.Empty(meta.CachedVersions);
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
    public void AkrOptions_default_ttl_is_24_hours()
    {
        var options = new AkrOptions();
        Assert.Equal(TimeSpan.FromHours(24), options.CacheTtl);
    }

    [Fact]
    public void AkrOptions_custom_ttl()
    {
        var options = new AkrOptions { CacheTtlHours = 12 };
        Assert.Equal(TimeSpan.FromHours(12), options.CacheTtl);
    }

    [Fact]
    public void AkrOptions_zero_ttl_means_always_check()
    {
        var options = new AkrOptions { CacheTtlHours = 0 };
        Assert.Equal(TimeSpan.Zero, options.CacheTtl);
    }

    [Fact]
    public void AkrOptions_bind_from_environment_reads_ttl()
    {
        Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", "12");
        try
        {
            var options = new AkrOptions();
            AkrOptions.BindFromEnvironment()(options);
            Assert.Equal(12, options.CacheTtlHours);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", null);
        }
    }

    [Fact]
    public void AkrOptions_bind_from_environment_invalid_ttl_keeps_default()
    {
        Environment.SetEnvironmentVariable("AKR_CACHE_TTL_HOURS", "banana");
        try
        {
            var options = new AkrOptions();
            AkrOptions.BindFromEnvironment()(options);
            Assert.Equal(24, options.CacheTtlHours);
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

    [Fact]
    public async Task EnsureAllSourcesAsync_returns_cached_result_on_second_call()
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

        var handler = new CachingFakeHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains(".tar.gz"))
            {
                return (HttpStatusCode.OK, tarball, null);
            }

            return (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }), null);
        });
        var client = CreateClient(handler);

        var first = await client.EnsureAllSourcesAsync(TestContext.Current.CancellationToken);
        var callsAfterFirst = handler.CallCount;

        var second = await client.EnsureAllSourcesAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(callsAfterFirst, handler.CallCount);
    }

    private GitHubClient CreateClient(HttpMessageHandler handler, int maxCachedVersions = 3) =>
        new(new HttpClient(handler), NullLogger<GitHubClient>.Instance,
            new FileService(new System.IO.Abstractions.FileSystem()),
            new ProjectScanner(new FileService(new System.IO.Abstractions.FileSystem()), NullLogger<ProjectScanner>.Instance),
            Options.Create(new AkrOptions { CacheRoot = _cacheRoot, MaxCachedVersions = maxCachedVersions }),
            _time);

    private void WriteTestMetadata(RepoMetadata metadata)
    {
        var metaDir = Path.Combine(_cacheRoot, "meta");
        Directory.CreateDirectory(metaDir);
        var json = JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllBytes(Path.Combine(metaDir, $"{RepoName}.json"), json);
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
