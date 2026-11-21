using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Akr.Core.Fetching;

public sealed class GitHubArchiveFetcher : ISourceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubArchiveFetcher> _logger;

    public GitHubArchiveFetcher(HttpClient httpClient, ILogger<GitHubArchiveFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task FetchAsync(string repo, string tag, string targetPath, CancellationToken cancellationToken = default)
    {
        var url = $"https://github.com/{repo}/archive/refs/tags/{tag}.tar.gz";
        _logger.LogInformation("Fetching {Repo} at tag {Tag}", repo, tag);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

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

        _logger.LogInformation("Extracted {Repo}@{Tag} to {Path}", repo, tag, targetPath);
    }

    public async Task<string> ResolveLatestVersionAsync(string repo, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{repo}/releases/latest";
        _logger.LogInformation("Resolving latest version for {Repo}", repo);

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var tagName = doc.RootElement.GetProperty("tag_name").GetString()
            ?? throw new SourceFetchException($"No tag_name in latest release for '{repo}'");

        return tagName;
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
