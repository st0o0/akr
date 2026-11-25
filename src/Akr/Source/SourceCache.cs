using System.Text.Json;
using Akr.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akr.Source;

public sealed class SourceCache
{
    private readonly string _cacheRoot;
    private readonly FileService _fileService;
    private readonly ILogger<SourceCache> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxCachedVersions;

    public SourceCache(
        FileService fileService,
        ILogger<SourceCache> logger,
        IOptions<AkrOptions> options,
        TimeProvider timeProvider)
    {
        _fileService = fileService;
        _logger = logger;
        _cacheRoot = options.Value.CacheRoot;
        _timeProvider = timeProvider;
        _maxCachedVersions = options.Value.MaxCachedVersions;
    }

    public string GetPath(string repoName, string version) =>
        _fileService.Combine(_cacheRoot, repoName, version);

    public bool Exists(string repoName, string version) =>
        _fileService.DirectoryExists(GetPath(repoName, version));

    internal RepoMetadata? ReadMetadata(string repoName)
    {
        var path = MetaPath(repoName);
        try
        {
            if (!_fileService.FileExists(path))
            {
                return null;
            }

            var json = _fileService.ReadAllBytes(path);
            return JsonSerializer.Deserialize(json, AkrJsonContext.Default.RepoMetadata);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning("Corrupt metadata for {Repo}, ignoring: {Error}", repoName, ex.Message);
            return null;
        }
    }

    internal void WriteMetadata(string repoName, RepoMetadata metadata)
    {
        var path = MetaPath(repoName);
        _fileService.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(metadata, AkrJsonContext.Default.RepoMetadata));
    }

    public void TouchVersionAccess(string repoName, string version)
    {
        var meta = ReadMetadata(repoName);
        if (meta is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var updated = meta.CachedVersions
            .Select(v => string.Equals(v.Version, version, StringComparison.Ordinal)
                ? v with { LastAccessed = now }
                : v)
            .ToList();

        if (!updated.Exists(v => string.Equals(v.Version, version, StringComparison.Ordinal)))
        {
            return;
        }

        WriteMetadata(repoName, meta with { CachedVersions = updated });
    }

    public void RecordVersionAccess(string repoName, string version, string tagFormat)
    {
        var meta = ReadMetadata(repoName);
        var now = _timeProvider.GetUtcNow();

        var entry = new CachedVersionEntry
        {
            Version = version,
            LastAccessed = now,
            TagFormat = tagFormat
        };

        var versions = (meta?.CachedVersions ?? [])
            .Where(v => !string.Equals(v.Version, version, StringComparison.Ordinal))
            .Append(entry)
            .ToList();

        if (meta is not null)
        {
            WriteMetadata(repoName, meta with { CachedVersions = versions });
        }
    }

    public void EvictOldVersions(string repoName)
    {
        try
        {
            var meta = ReadMetadata(repoName);
            if (meta is null || meta.CachedVersions.Count <= _maxCachedVersions)
            {
                return;
            }

            var toEvict = meta.CachedVersions
                .OrderBy(v => v.LastAccessed)
                .Take(meta.CachedVersions.Count - _maxCachedVersions)
                .ToList();

            foreach (var entry in toEvict)
            {
                var dir = _fileService.Combine(_cacheRoot, repoName, entry.Version);
                if (_fileService.DirectoryExists(dir))
                {
                    _logger.LogInformation("Evicting LRU version {Repo}/{Version}", repoName, entry.Version);
                    _fileService.DeleteDirectory(dir, true);
                }
            }

            var remaining = meta.CachedVersions
                .Where(v => !toEvict.Exists(e => string.Equals(e.Version, v.Version, StringComparison.Ordinal)))
                .ToList();

            WriteMetadata(repoName, meta with { CachedVersions = remaining });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to clean up old versions for {Repo}: {Error}", repoName, ex.Message);
        }
    }

    public static string RepoName(string repo) =>
        repo.Contains('/') ? repo[(repo.IndexOf('/') + 1)..] : repo;

    private string MetaPath(string repoName) =>
        _fileService.Combine(_cacheRoot, "meta", $"{repoName}.json");
}
