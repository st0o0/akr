namespace Akr.Core.Caching;

public sealed class FileSystemSourceCache : ISourceCache
{
    private readonly string _cacheRoot;

    public FileSystemSourceCache()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".akr", "sources"))
    {
    }

    internal FileSystemSourceCache(string cacheRoot)
    {
        _cacheRoot = cacheRoot;
    }

    public string GetPath(string repo, string version)
    {
        var repoName = repo.Contains('/') ? repo[(repo.IndexOf('/') + 1)..] : repo;
        return Path.Combine(_cacheRoot, repoName, version);
    }

    public bool Exists(string repo, string version) =>
        Directory.Exists(GetPath(repo, version));

    public string GetCacheRoot() => _cacheRoot;
}
