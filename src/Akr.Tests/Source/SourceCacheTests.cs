using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Akr.Configuration;
using Akr.Source;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Akr.Tests.Source;

public sealed class SourceCacheTests
{
    private const string CacheRoot = "C:/cache";
    private readonly MockFileSystem _fs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private SourceCache CreateCache(int maxVersions = 3) =>
        new(
            new FileService(_fs),
            NullLogger<SourceCache>.Instance,
            Options.Create(new AkrOptions { CacheRoot = CacheRoot, MaxCachedVersions = maxVersions }),
            _time);

    private void WriteMetadata(string repoName, RepoMetadata metadata)
    {
        var path = $"{CacheRoot}/meta/{repoName}.json";
        var dir = _fs.Path.GetDirectoryName(path)!;
        _fs.Directory.CreateDirectory(dir);
        _fs.File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(metadata, AkrJsonContext.Default.RepoMetadata));
    }

    private RepoMetadata? ReadMetadata(string repoName)
    {
        var path = $"{CacheRoot}/meta/{repoName}.json";
        if (!_fs.File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize(_fs.File.ReadAllBytes(path), AkrJsonContext.Default.RepoMetadata);
    }

    [Fact]
    public void WriteMetadata_then_ReadMetadata_round_trips()
    {
        var cache = CreateCache();
        var meta = new RepoMetadata
        {
            Version = "1.5.75",
            Etag = "\"abc\"",
            LastChecked = _time.GetUtcNow(),
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = _time.GetUtcNow(), TagFormat = "v{0}" }
            ]
        };

        cache.WriteMetadata("akka.net", meta);
        var read = cache.ReadMetadata("akka.net");

        Assert.NotNull(read);
        Assert.Equal("1.5.75", read.Version);
        Assert.Equal("\"abc\"", read.Etag);
        Assert.Single(read.CachedVersions);
    }

    [Fact]
    public void ReadMetadata_returns_null_for_nonexistent_file()
    {
        var cache = CreateCache();

        var result = cache.ReadMetadata("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void TouchVersionAccess_updates_LastAccessed()
    {
        var cache = CreateCache();
        var t0 = _time.GetUtcNow();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0, TagFormat = "v{0}" }
            ]
        });

        _time.Advance(TimeSpan.FromHours(1));
        cache.TouchVersionAccess("akka.net", "1.5.75");

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        var entry = Assert.Single(meta.CachedVersions);
        Assert.Equal(t0 + TimeSpan.FromHours(1), entry.LastAccessed);
    }

    [Fact]
    public void TouchVersionAccess_is_noop_for_nonexistent_version()
    {
        var cache = CreateCache();
        var t0 = _time.GetUtcNow();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0 }
            ]
        });

        cache.TouchVersionAccess("akka.net", "999.0.0");

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        var entry = Assert.Single(meta.CachedVersions);
        Assert.Equal(t0, entry.LastAccessed);
    }

    [Fact]
    public void RecordVersionAccess_creates_new_entry()
    {
        var cache = CreateCache();
        var t0 = _time.GetUtcNow();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions = []
        });

        cache.RecordVersionAccess("akka.net", "1.5.75", "v{0}");

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        var entry = Assert.Single(meta.CachedVersions);
        Assert.Equal("1.5.75", entry.Version);
        Assert.Equal(t0, entry.LastAccessed);
        Assert.Equal("v{0}", entry.TagFormat);
    }

    [Fact]
    public void RecordVersionAccess_updates_existing_entry()
    {
        var cache = CreateCache();
        var t0 = _time.GetUtcNow();
        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0, TagFormat = "v{0}" }
            ]
        });

        _time.Advance(TimeSpan.FromHours(2));
        cache.RecordVersionAccess("akka.net", "1.5.75", "v{0}");

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        var entry = Assert.Single(meta.CachedVersions);
        Assert.Equal(t0 + TimeSpan.FromHours(2), entry.LastAccessed);
    }

    [Fact]
    public void EvictOldVersions_removes_oldest_when_over_limit()
    {
        var cache = CreateCache(maxVersions: 2);
        var t0 = _time.GetUtcNow();

        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.70");
        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.71");
        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.75");

        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.70", LastAccessed = t0 },
                new CachedVersionEntry { Version = "1.5.71", LastAccessed = t0 + TimeSpan.FromHours(1) },
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0 + TimeSpan.FromHours(2) }
            ]
        });

        cache.EvictOldVersions("akka.net");

        Assert.False(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.70"));
        Assert.True(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.71"));
        Assert.True(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.75"));

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        Assert.Equal(2, meta.CachedVersions.Count);
    }

    [Fact]
    public void EvictOldVersions_is_noop_when_under_limit()
    {
        var cache = CreateCache(maxVersions: 3);
        var t0 = _time.GetUtcNow();

        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.70", LastAccessed = t0 },
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0 + TimeSpan.FromHours(1) }
            ]
        });

        cache.EvictOldVersions("akka.net");

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        Assert.Equal(2, meta.CachedVersions.Count);
    }

    [Fact]
    public void ReadMetadata_returns_null_for_corrupt_json()
    {
        var cache = CreateCache();
        var path = $"{CacheRoot}/meta/akka.net.json";
        _fs.Directory.CreateDirectory($"{CacheRoot}/meta");
        _fs.File.WriteAllText(path, "not valid json {{{");

        var result = cache.ReadMetadata("akka.net");

        Assert.Null(result);
    }

    [Fact]
    public void EvictOldVersions_cleans_metadata_when_directory_missing()
    {
        var cache = CreateCache(maxVersions: 1);
        var t0 = _time.GetUtcNow();

        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.75");

        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.70", LastAccessed = t0 },
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0 + TimeSpan.FromHours(1) }
            ]
        });

        cache.EvictOldVersions("akka.net");

        Assert.True(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.75"));
        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        Assert.Single(meta.CachedVersions);
        Assert.Equal("1.5.75", meta.CachedVersions[0].Version);
    }

    [Fact]
    public void EvictOldVersions_with_max_one_keeps_most_recent()
    {
        var cache = CreateCache(maxVersions: 1);
        var t0 = _time.GetUtcNow();

        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.70");
        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.71");
        _fs.Directory.CreateDirectory($"{CacheRoot}/akka.net/1.5.75");

        WriteMetadata("akka.net", new RepoMetadata
        {
            Version = "1.5.75",
            LastChecked = t0,
            CachedVersions =
            [
                new CachedVersionEntry { Version = "1.5.70", LastAccessed = t0 },
                new CachedVersionEntry { Version = "1.5.71", LastAccessed = t0 + TimeSpan.FromHours(1) },
                new CachedVersionEntry { Version = "1.5.75", LastAccessed = t0 + TimeSpan.FromHours(2) }
            ]
        });

        cache.EvictOldVersions("akka.net");

        Assert.False(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.70"));
        Assert.False(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.71"));
        Assert.True(_fs.Directory.Exists($"{CacheRoot}/akka.net/1.5.75"));
        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        Assert.Single(meta.CachedVersions);
    }

    [Fact]
    public void RecordVersionAccess_creates_metadata_when_none_exists()
    {
        var cache = CreateCache();

        cache.RecordVersionAccess("akka.net", "1.5.75", "v{0}");

        var meta = ReadMetadata("akka.net");
        Assert.NotNull(meta);
        Assert.Equal("1.5.75", meta.Version);
        var entry = Assert.Single(meta.CachedVersions);
        Assert.Equal("1.5.75", entry.Version);
    }
}
