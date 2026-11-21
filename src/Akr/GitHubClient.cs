using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Akr.Registry;
using Microsoft.Extensions.Logging;

namespace Akr;

public sealed class GitHubClient
{
    private readonly string _cacheRoot;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubClient> _logger;
    private readonly Dictionary<string, string> _latestVersions = new(StringComparer.Ordinal);

    public GitHubClient(HttpClient httpClient, ILogger<GitHubClient> logger, string? cacheRoot = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akr", "sources");
    }

    public async Task<string> EnsureSourceAsync(string repo, CancellationToken cancellationToken = default)
    {
        var version = await ResolveLatestAsync(repo, cancellationToken);
        var repoName = repo.Contains('/') ? repo[(repo.IndexOf('/') + 1)..] : repo;
        var targetPath = Path.Combine(_cacheRoot, repoName, version);

        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }

        var tempPath = targetPath + ".tmp";
        try
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }

            try
            {
                await FetchArchiveAsync(repo, version, tempPath, cancellationToken);
            }
            catch (SourceFetchException)
            {
                _logger.LogDebug("Tag {Tag} failed for {Repo}, trying v-prefixed", version, repo);
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }

                await FetchArchiveAsync(repo, $"v{version}", tempPath, cancellationToken);
            }

            Directory.Move(tempPath, targetPath);
        }
        catch
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }

            throw;
        }

        return targetPath;
    }

    public async Task<IReadOnlyList<string>> EnsureAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var repo in RepoRegistry.GetAllRepos())
        {
            if (seen.Add(repo))
            {
                roots.Add(await EnsureSourceAsync(repo, cancellationToken));
            }
        }

        return roots;
    }

    public async Task<string> ResolveLatestAsync(string repo, CancellationToken cancellationToken = default)
    {
        if (_latestVersions.TryGetValue(repo, out var cached))
        {
            return cached;
        }

        var url = $"https://api.github.com/repos/{repo}/releases/latest";
        _logger.LogInformation("Resolving latest version for {Repo}", repo);

        using var response = await _httpClient.GetAsync(url, cancellationToken);
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

        Directory.CreateDirectory(targetPath);
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

    private static async Task ExtractTarAsync(Stream stream, string targetPath, CancellationToken cancellationToken)
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

            var fullPath = Path.GetFullPath(Path.Combine(targetPath, entryName));
            if (!fullPath.StartsWith(Path.GetFullPath(targetPath), StringComparison.Ordinal))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await entry.ExtractToFileAsync(fullPath, overwrite: false, cancellationToken);
        }
    }

    private static string StripFirstSegment(string path)
    {
        var normalized = path.Replace('\\', '/');
        var slashIndex = normalized.IndexOf('/');
        return slashIndex >= 0 ? normalized[(slashIndex + 1)..] : string.Empty;
    }
}
