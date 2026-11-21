namespace Akr.Core;

public interface ISourceProvider
{
    Task<string?> GetSourceRootAsync(string packageName, string? versionOverride = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAllCachedSourceRootsAsync(CancellationToken cancellationToken = default);
    Task EnsureAllConfiguredAsync(CancellationToken cancellationToken = default);
}
