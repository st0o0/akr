using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Akr.Source;

public sealed partial class SourceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SourceFetcher> _logger;
    private readonly FileService _fileService;
    private readonly SourceCache _cache;

    public SourceFetcher(
        HttpClient httpClient,
        ILogger<SourceFetcher> logger,
        FileService fileService,
        SourceCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fileService = fileService;
        _cache = cache;
    }

    public async Task<string> EnsureSourceAsync(string repo, string version, string? knownTagFormat, CancellationToken cancellationToken)
    {
        var repoName = SourceCache.RepoName(repo);
        var targetPath = _cache.GetPath(repoName, version);

        if (_fileService.DirectoryExists(targetPath))
        {
            _cache.TouchVersionAccess(repoName, version);
            return targetPath;
        }

        var usedVPrefix = await FetchWithTagDetection(repo, version, knownTagFormat, targetPath, cancellationToken);

        _cache.RecordVersionAccess(repoName, version, usedVPrefix ? "v-prefixed" : "plain");
        _cache.EvictOldVersions(repoName);

        return targetPath;
    }

    private async Task<bool> FetchWithTagDetection(string repo, string version, string? tagFormat, string targetPath, CancellationToken cancellationToken)
    {
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
                    Log.TagFailed(_logger, version, repo);
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

        return usedVPrefix;
    }

    private async Task FetchArchiveAsync(string repo, string tag, string targetPath, CancellationToken cancellationToken)
    {
        var url = $"https://github.com/{repo}/archive/refs/tags/{tag}.tar.gz";
        Log.FetchingRepo(_logger, repo, tag);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ThrowIfFailed(response, repo, tag);

        await using var gzipStream = new GZipStream(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            CompressionMode.Decompress);

        _fileService.CreateDirectory(targetPath);
        await ExtractTarAsync(gzipStream, targetPath, cancellationToken);

        Log.ExtractedRepo(_logger, repo, tag, targetPath);
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

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Tag {Tag} failed for {Repo}, trying v-prefixed")]
        public static partial void TagFailed(ILogger logger, string tag, string repo);

        [LoggerMessage(Level = LogLevel.Information, Message = "Fetching {Repo} at tag {Tag}")]
        public static partial void FetchingRepo(ILogger logger, string repo, string tag);

        [LoggerMessage(Level = LogLevel.Information, Message = "Extracted {Repo}@{Tag} to {Path}")]
        public static partial void ExtractedRepo(ILogger logger, string repo, string tag, string path);
    }
}
