using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Akr.Analysis;
using Akr.Configuration;
using Akr.Registry;
using Akr.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly ProjectScanner _projectScanner;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxCachedVersions;
    private readonly Dictionary<string, string> _latestVersions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _cachedRootsByVersion = new(StringComparer.Ordinal);
    private IReadOnlyList<string>? _cachedRoots;

    public GitHubClient(
        HttpClient httpClient,
        ILogger<GitHubClient> logger,
        FileService fileService,
        ProjectScanner projectScanner,
        IOptions<AkrOptions> options,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fileService = fileService;
        _projectScanner = projectScanner;
        _cacheRoot = options.Value.CacheRoot;
        _cacheTtl = options.Value.CacheTtl;
        _maxCachedVersions = options.Value.MaxCachedVersions;
        _timeProvider = timeProvider;
    }

    public async Task<string> EnsureSourceAsync(string repo, CancellationToken cancellationToken = default)
    {
        var version = await ResolveLatestAsync(repo, cancellationToken);
        var repoName = RepoName(repo);
        var targetPath = _fileService.Combine(_cacheRoot, repoName, version);

        if (_fileService.DirectoryExists(targetPath))
        {
            TouchVersionAccess(repoName, version);
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

        RecordVersionAccess(repoName, version, usedVPrefix ? "v-prefixed" : "plain");
        CleanupOldVersions(repoName);

        return targetPath;
    }

    public async Task<string> EnsureDocsAsync(CancellationToken cancellationToken = default)
    {
        var repo = RepoRegistry.DocsRepo;
        var repoName = RepoName(repo);
        const string branch = "master";
        var targetPath = _fileService.Combine(_cacheRoot, repoName, branch);

        if (_fileService.DirectoryExists(targetPath))
        {
            return targetPath;
        }

        var url = $"https://github.com/{repo}/archive/refs/heads/{branch}.tar.gz";
        _logger.LogInformation("Fetching docs from {Repo} branch {Branch}", repo, branch);

        var tempPath = targetPath + ".tmp";
        try
        {
            if (_fileService.DirectoryExists(tempPath))
            {
                _fileService.DeleteDirectory(tempPath, true);
            }

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            ThrowIfFailed(response, repo, branch);

            await using var gzipStream = new GZipStream(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                CompressionMode.Decompress);

            _fileService.CreateDirectory(tempPath);
            await ExtractTarAsync(gzipStream, tempPath, cancellationToken);

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

        return targetPath;
    }

    public async Task<string> EnsureSourceForVersionAsync(string repo, string version, CancellationToken cancellationToken = default)
    {
        var repoName = RepoName(repo);
        var targetPath = _fileService.Combine(_cacheRoot, repoName, version);

        if (_fileService.DirectoryExists(targetPath))
        {
            TouchVersionAccess(repoName, version);
            return targetPath;
        }

        var meta = ReadMetadata(repoName);
        var tagFormat = meta?.CachedVersions
            .FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.Ordinal))?.TagFormat
            ?? meta?.TagFormat;
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

        RecordVersionAccess(repoName, version, usedVPrefix ? "v-prefixed" : "plain");
        CleanupOldVersions(repoName);

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

    public async Task<IReadOnlyList<string>> EnsureAllSourcesForVersionAsync(string? version, CancellationToken cancellationToken = default)
    {
        if (version is null)
        {
            return await EnsureAllSourcesAsync(cancellationToken);
        }

        if (_cachedRootsByVersion.TryGetValue(version, out var cached))
        {
            return cached;
        }

        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var repo in RepoRegistry.GetAllRepos())
        {
            if (seen.Add(repo))
            {
                roots.Add(await EnsureSourceForVersionAsync(repo, version, cancellationToken));
            }
        }

        _cachedRootsByVersion[version] = roots;
        return roots;
    }

    public async Task<string> ResolveLatestAsync(string repo, CancellationToken cancellationToken = default)
    {
        if (_latestVersions.TryGetValue(repo, out var cached))
        {
            return cached;
        }

        if (_projectScanner.Current is { HasPackages: true } scanResult)
        {
            var repoVersions = _projectScanner.MapToRepoVersions(scanResult.Packages);
            if (repoVersions.TryGetValue(repo, out var projectVersion))
            {
                _logger.LogInformation("Using project-detected version for {Repo}: {Version}", repo, projectVersion);
                _latestVersions[repo] = projectVersion;
                return projectVersion;
            }
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
            throw new SourceFetchException($"GitHub rate limit hit for repo '{repo}'. Try again later or increase AKR_CACHE_TTL_HOURS.");
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

    private void TouchVersionAccess(string repoName, string version)
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

    private void RecordVersionAccess(string repoName, string version, string tagFormat)
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

    private void CleanupOldVersions(string repoName)
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
}
