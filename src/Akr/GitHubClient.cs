using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Akr.Registry;
using Microsoft.Extensions.Logging;

namespace Akr;

public sealed class GitHubClient(HttpClient httpClient, ILogger<GitHubClient> logger)
{
    internal static string CacheRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akr", "sources");

    private readonly Dictionary<string, string> _latestVersions = new(StringComparer.Ordinal);

    public async Task<string> EnsureSourceAsync(string repo, CancellationToken cancellationToken = default)
    {
        var version = await ResolveLatestAsync(repo, cancellationToken);
        var repoName = repo.Contains('/') ? repo[(repo.IndexOf('/') + 1)..] : repo;
        var targetPath = Path.Combine(CacheRoot, repoName, version);

        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }

        foreach (var tag in new[] { version, $"v{version}" })
        {
            try
            {
                await FetchArchiveAsync(repo, tag, targetPath, cancellationToken);
                return targetPath;
            }
            catch (SourceFetchException) when (tag == version)
            {
                logger.LogDebug("Tag {Tag} not found for {Repo}, trying v-prefixed", tag, repo);
            }
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
        logger.LogInformation("Resolving latest version for {Repo}", repo);

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var version = doc.RootElement.GetProperty("tag_name").GetString()
            ?? throw new SourceFetchException($"No tag_name in latest release for '{repo}'");

        _latestVersions[repo] = version;
        logger.LogInformation("Resolved {Repo} → {Version}", repo, version);
        return version;
    }

    private async Task FetchArchiveAsync(string repo, string tag, string targetPath, CancellationToken cancellationToken)
    {
        var url = $"https://github.com/{repo}/archive/refs/tags/{tag}.tar.gz";
        logger.LogInformation("Fetching {Repo} at tag {Tag}", repo, tag);

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SourceFetchException($"Tag '{tag}' not found in repo '{repo}'");
        }

        response.EnsureSuccessStatusCode();

        await using var gzipStream = new GZipStream(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            CompressionMode.Decompress);

        Directory.CreateDirectory(targetPath);
        await ExtractTarAsync(gzipStream, targetPath, cancellationToken);

        logger.LogInformation("Extracted {Repo}@{Tag} to {Path}", repo, tag, targetPath);
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
