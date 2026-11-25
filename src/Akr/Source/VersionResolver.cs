using System.Net;
using System.Text.Json;
using Akr.Configuration;
using Akr.Scanning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akr.Source;

public sealed class VersionResolver
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VersionResolver> _logger;
    private readonly ProjectScanner _projectScanner;
    private readonly SourceCache _cache;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, string> _latestVersions = new(StringComparer.Ordinal);

    public VersionResolver(
        HttpClient httpClient,
        ILogger<VersionResolver> logger,
        ProjectScanner projectScanner,
        SourceCache cache,
        IOptions<AkrOptions> options,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _projectScanner = projectScanner;
        _cache = cache;
        _cacheTtl = options.Value.CacheTtl;
        _timeProvider = timeProvider;
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

        var repoName = SourceCache.RepoName(repo);
        var meta = _cache.ReadMetadata(repoName);

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
            _cache.WriteMetadata(repoName, refreshed);
            _latestVersions[repo] = meta.Version;
            return meta.Version;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SourceFetchException($"No releases found for repo '{repo}'");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SourceFetchException($"GitHub returned {(int)response.StatusCode} for repo '{repo}'");
        }

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
        _cache.WriteMetadata(repoName, newMeta);

        _latestVersions[repo] = version;
        _logger.LogInformation("Resolved {Repo} -> {Version}", repo, version);
        return version;
    }

    public string? GetTagFormat(string repo, string? version = null)
    {
        var repoName = SourceCache.RepoName(repo);
        var meta = _cache.ReadMetadata(repoName);
        if (meta is null)
        {
            return null;
        }

        if (version is not null)
        {
            return meta.CachedVersions
                .FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.Ordinal))?.TagFormat
                ?? meta.TagFormat;
        }

        return meta.TagFormat;
    }

    public void UpdateTagFormat(string repo, string tagFormat)
    {
        var repoName = SourceCache.RepoName(repo);
        var meta = _cache.ReadMetadata(repoName);
        if (meta is not null)
        {
            _cache.WriteMetadata(repoName, meta with { TagFormat = tagFormat });
        }
    }
}
