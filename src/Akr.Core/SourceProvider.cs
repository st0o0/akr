using Akr.Core.Caching;
using Akr.Core.Configuration;
using Akr.Core.Fetching;
using Akr.Core.Registry;
using Microsoft.Extensions.Logging;

namespace Akr.Core;

public sealed class SourceProvider : ISourceProvider
{
    private readonly IVersionConfig _versionConfig;
    private readonly ISourceCache _cache;
    private readonly ISourceFetcher _fetcher;
    private readonly ILogger<SourceProvider> _logger;
    private readonly Dictionary<string, string> _resolvedLatest = new(StringComparer.Ordinal);
    private volatile bool _initialFetchDone;

    public SourceProvider(
        IVersionConfig versionConfig,
        ISourceCache cache,
        ISourceFetcher fetcher,
        ILogger<SourceProvider> logger)
    {
        _versionConfig = versionConfig;
        _cache = cache;
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<string?> GetSourceRootAsync(string packageName, string? versionOverride = null, CancellationToken cancellationToken = default)
    {
        var entry = RepoRegistry.GetEntry(packageName);
        if (entry is null)
        {
            _logger.LogWarning("Unknown package: {Package}", packageName);
            return null;
        }

        var version = versionOverride
            ?? _versionConfig.GetVersion(packageName)
            ?? await ResolveAndCacheLatestAsync(entry.Repo, cancellationToken);

        await EnsureRepoFetchedAsync(entry.Repo, version, cancellationToken);

        var repoRoot = _cache.GetPath(entry.Repo, version);
        var packagePath = Path.Combine(repoRoot, entry.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        return Directory.Exists(packagePath) ? packagePath : repoRoot;
    }

    public async Task<IReadOnlyList<string>> GetAllCachedSourceRootsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAllConfiguredAsync(cancellationToken);

        var roots = new List<string>();
        var cacheRoot = _cache.GetCacheRoot();
        if (!Directory.Exists(cacheRoot))
        {
            return roots;
        }

        foreach (var repoDir in Directory.GetDirectories(cacheRoot))
        {
            foreach (var versionDir in Directory.GetDirectories(repoDir))
            {
                roots.Add(versionDir);
            }
        }

        return roots;
    }

    public async Task EnsureAllConfiguredAsync(CancellationToken cancellationToken = default)
    {
        if (_initialFetchDone)
        {
            return;
        }

        var versions = _versionConfig.GetAll();
        var fetched = new HashSet<(string Repo, string Version)>();

        if (versions.Count > 0)
        {
            foreach (var (packageName, version) in versions)
            {
                var entry = RepoRegistry.GetEntry(packageName);
                if (entry is null || !fetched.Add((entry.Repo, version)))
                {
                    continue;
                }

                await EnsureRepoFetchedAsync(entry.Repo, version, cancellationToken);
            }
        }
        else
        {
            foreach (var repo in RepoRegistry.GetAllRepos())
            {
                var version = await ResolveAndCacheLatestAsync(repo, cancellationToken);
                await EnsureRepoFetchedAsync(repo, version, cancellationToken);
            }
        }

        _initialFetchDone = true;
    }

    private async Task<string> ResolveAndCacheLatestAsync(string repo, CancellationToken cancellationToken)
    {
        if (_resolvedLatest.TryGetValue(repo, out var cached))
        {
            return cached;
        }

        var version = await _fetcher.ResolveLatestVersionAsync(repo, cancellationToken);
        _resolvedLatest[repo] = version;
        _logger.LogInformation("Resolved latest for {Repo}: {Version}", repo, version);
        return version;
    }

    private async Task EnsureRepoFetchedAsync(string repo, string version, CancellationToken cancellationToken)
    {
        if (_cache.Exists(repo, version))
        {
            return;
        }

        var targetPath = _cache.GetPath(repo, version);

        foreach (var tag in RepoRegistry.ResolveTags(version))
        {
            try
            {
                await _fetcher.FetchAsync(repo, tag, targetPath, cancellationToken);
                return;
            }
            catch (SourceFetchException) when (tag != version)
            {
                _logger.LogDebug("Tag {Tag} not found for {Repo}, trying next", tag, repo);
            }
        }
    }
}
