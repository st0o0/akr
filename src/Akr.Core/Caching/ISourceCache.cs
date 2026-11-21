namespace Akr.Core.Caching;

public interface ISourceCache
{
    string GetPath(string repo, string version);
    bool Exists(string repo, string version);
    string GetCacheRoot();
}
