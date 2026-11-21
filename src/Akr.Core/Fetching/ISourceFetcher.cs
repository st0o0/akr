namespace Akr.Core.Fetching;

public interface ISourceFetcher
{
    Task FetchAsync(string repo, string tag, string targetPath, CancellationToken cancellationToken = default);
    Task<string> ResolveLatestVersionAsync(string repo, CancellationToken cancellationToken = default);
}
