using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Akr.Registry;
using Akr.Tools;
using Microsoft.Extensions.Logging;

namespace Akr;

public sealed class GitHubClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _cacheRoot;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubClient> _logger;
    private readonly FileService _fileService;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, string> _latestVersions = new(StringComparer.Ordinal);
    private IReadOnlyList<string>? _cachedRoots;

    public GitHubClient(
        HttpClient httpClient,
        ILogger<GitHubClient> logger,
        FileService fileService,
        string? cacheRoot = null,
        TimeSpan? cacheTtl = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fileService = fileService;
        _cacheRoot = cacheRoot ?? _fileService.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akr", "sources");
        _cacheTtl = cacheTtl ?? ParseCacheTtl(logger);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> EnsureSourceAsync(string repo, CancellationToken cancellationToken = default)
    {
        var version = await ResolveLatestAsync(repo, cancellationToken);
        var repoName = RepoName(repo);
        var targetPath = _fileService.Combine(_cacheRoot, repoName, version);

        if (_fileService.DirectoryExists(targetPath))
        {
            return targetPath;
        }

        var meta = ReadMetadata(repoName);
        var tagFormat = meta?.TagFormat;
        var usedVPrefix = false;

        var tempPath = targetPath + ".tmp";
        try
        {
            if (_fileService.DirectoryExists(tempPath))
            {
                _fileService.DeleteDirectory(tempPath, true);
            }

            if (tagFormat == "v-prefixed")
            {
                await FetchArchiveAsync(repo, $"v{version}", tempPath, cancellationToken);
                usedVPrefix = true;
            }
            else if (tagFormat == "plain")
            {
                await FetchArchiveAsync(repo, version, tempPath, cancellationToken);
            }
            else
            {
                try
                {
                    await FetchArchiveAsync(repo, version, tempPath, cancellationToken);
                }
                catch (SourceFetchException)
                {
                    _logger.LogDebug("Tag {Tag} failed for {Repo}, trying v-prefixed", version, repo);
                    if (_fileService.DirectoryExists(tempPath))
                    {
                        _fileService.DeleteDirectory(tempPath, true);
                    }

                    await FetchArchiveAsync(repo, $"v{version}", tempPath, cancellationToken);
                    usedVPrefix = true;
                }
            }

            _fileService.MoveDirectory(tempPath, targetPath);
        }
        catch
        {
            if (_fileService.DirectoryExists(tempPath))
            {
                _fileService.DeleteDirectory(tempPath, true);
            }

            throw;
        }

        if (meta is not null)
        {
            var updatedMeta = meta with { TagFormat = usedVPrefix ? "v-prefixed" : "plain" };
            WriteMetadata(repoName, updatedMeta);
        }

        CleanupOldVersions(repoName, version);

        return targetPath;
    }

    public async Task<IReadOnlyList<string>> EnsureAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedRoots is not null)
        {
            return _cachedRoots;
        }

        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var repo in RepoRegistry.GetAllRepos())
        {
            if (seen.Add(repo))
            {
                roots.Add(await EnsureSourceAsync(repo, cancellationToken));
            }
        }

        _cachedRoots = roots;
        return roots;
    }

    public async Task<string> ResolveLatestAsync(string repo, CancellationToken cancellationToken = default)
    {
        if (_latestVersions.TryGetValue(repo, out var cached))
        {
            return cached;
        }

        var repoName = RepoName(repo);
        var meta = ReadMetadata(repoName);

        if (meta is not null)
        {
            var age = _timeProvider.GetUtcNow() - meta.LastChecked;
            if (age < _cacheTtl)
            {
                _logger.LogDebug("Cache fresh for {Repo} ({Version}, checked {Age} ago)", repo, meta.Version, age);
                _latestVersions[repo] = meta.Version;
                return meta.Version;
            }
        }

        var url = $"https://api.github.com/repos/{repo}/releases/latest";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (meta?.Etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", meta.Etag);
        }

        _logger.LogInformation("Resolving latest version for {Repo}{Conditional}",
            repo, meta?.Etag is not null ? " (conditional)" : "");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified && meta is not null)
        {
            _logger.LogDebug("Version unchanged for {Repo} ({Version})", repo, meta.Version);
            var refreshed = meta with { LastChecked = _timeProvider.GetUtcNow() };
            WriteMetadata(repoName, refreshed);
            _latestVersions[repo] = meta.Version;
            return meta.Version;
        }

        ThrowIfFailed(response, repo, "latest");

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            !doc.RootElement.TryGetProperty("tag_name", out var tagElement) ||
            tagElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(tagElement.GetString()))
        {
            throw new SourceFetchException($"No tag_name in latest release for '{repo}'");
        }

        var version = tagElement.GetString()!;
        var etag = response.Headers.ETag?.Tag;

        var newMeta = new RepoMetadata
        {
            Version = version,
            Etag = etag,
            LastChecked = _timeProvider.GetUtcNow(),
            TagFormat = meta?.TagFormat
        };
        WriteMetadata(repoName, newMeta);

        _latestVersions[repo] = version;
        _logger.LogInformation("Resolved {Repo} -> {Version}", repo, version);
        return version;
    }

    private async Task FetchArchiveAsync(string repo, string tag, string targetPath, CancellationToken cancellationToken)
    {
        var url = $"https://github.com/{repo}/archive/refs/tags/{tag}.tar.gz";
        _logger.LogInformation("Fetching {Repo} at tag {Tag}", repo, tag);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ThrowIfFailed(response, repo, tag);

        await using var gzipStream = new GZipStream(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            CompressionMode.Decompress);

        _fileService.CreateDirectory(targetPath);
        await ExtractTarAsync(gzipStream, targetPath, cancellationToken);

        _logger.LogInformation("Extracted {Repo}@{Tag} to {Path}", repo, tag, targetPath);
    }

    private static void ThrowIfFailed(HttpResponseMessage response, string repo, string tag)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SourceFetchException($"Tag '{tag}' not found in repo '{repo}'");
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            throw new SourceFetchException($"GitHub rate limit hit for repo '{repo}'. Try again later or set GITHUB_TOKEN.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SourceFetchException($"GitHub returned {(int)response.StatusCode} for repo '{repo}' tag '{tag}'");
        }
    }

    private async Task ExtractTarAsync(Stream stream, string targetPath, CancellationToken cancellationToken)
    {
        await using var reader = new TarReader(stream);

        while (await reader.GetNextEntryAsync(cancellationToken: cancellationToken) is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            var entryName = StripFirstSegment(entry.Name);
            if (string.IsNullOrEmpty(entryName))
            {
                continue;
            }

            var fullPath = _fileService.GetFullPath(_fileService.Combine(targetPath, entryName));
            if (!fullPath.StartsWith(_fileService.GetFullPath(targetPath), StringComparison.Ordinal))
            {
                continue;
            }

            await _fileService.ExtractTarEntryAsync(entry, fullPath, cancellationToken);
        }
    }

    private static string StripFirstSegment(string path)
    {
        var normalized = path.Replace('\\', '/');
        var slashIndex = normalized.IndexOf('/');
        return slashIndex >= 0 ? normalized[(slashIndex + 1)..] : string.Empty;
    }

    private static string RepoName(string repo) =>
        repo.Contains('/') ? repo[(repo.IndexOf('/') + 1)..] : repo;

    private string MetaPath(string repoName) =>
        _fileService.Combine(_cacheRoot, "meta", $"{repoName}.json");

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
            return JsonSerializer.Deserialize<RepoMetadata>(json, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning("Corrupt metadata for {Repo}, ignoring: {Error}", repoName, ex.Message);
            return null;
        }
    }

    private void WriteMetadata(string repoName, RepoMetadata metadata)
    {
        var path = MetaPath(repoName);
        _fileService.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions));
    }

    private void CleanupOldVersions(string repoName, string currentVersion)
    {
        try
        {
            var repoDir = _fileService.Combine(_cacheRoot, repoName);
            if (!_fileService.DirectoryExists(repoDir))
            {
                return;
            }

            foreach (var dir in _fileService.EnumerateDirectories(repoDir))
            {
                var dirName = _fileService.GetFileName(dir);
                if (!string.Equals(dirName, currentVersion, StringComparison.Ordinal) &&
                    !dirName.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    _logger.LogInformation("Cleaning up old version {Repo}/{Version}", repoName, dirName);
                    _fileService.DeleteDirectory(dir, true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Failed to clean up old versions for {Repo}: {Error}", repoName, ex.Message);
        }
    }

    internal static TimeSpan ParseCacheTtl(ILogger logger)
    {
        var value = Environment.GetEnvironmentVariable("AKR_CACHE_TTL_HOURS");
        if (string.IsNullOrEmpty(value))
        {
            return TimeSpan.FromHours(24);
        }

        if (int.TryParse(value, out var hours) && hours >= 0)
        {
            return TimeSpan.FromHours(hours);
        }

        logger.LogWarning("Invalid AKR_CACHE_TTL_HOURS value '{Value}', using default 24h", value);
        return TimeSpan.FromHours(24);
    }
}
