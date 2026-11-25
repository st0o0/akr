using Akka.Actor;
using Akka.Streams.Dsl;
using Akr.Registry;
using AkkaSource = Akka.Streams.Dsl.Source;

namespace Akr.Source;

public sealed class SourcePipeline
{
    private readonly SourceFetcher _fetcher;
    private readonly VersionResolver _resolver;
    private readonly SourceCache _cache;
    private readonly ActorSystem _actorSystem;
    private readonly Dictionary<string, IReadOnlyList<string>> _cachedRootsByVersion = new(StringComparer.Ordinal);
    private IReadOnlyList<string>? _cachedRoots;

    public SourcePipeline(SourceFetcher fetcher, VersionResolver resolver, SourceCache cache, ActorSystem actorSystem)
    {
        _fetcher = fetcher;
        _resolver = resolver;
        _cache = cache;
        _actorSystem = actorSystem;
    }

    public async Task<IReadOnlyList<string>> EnsureAllForVersionAsync(string? version, CancellationToken cancellationToken = default)
    {
        if (version is null)
        {
            return await EnsureAllLatestAsync(cancellationToken);
        }

        if (_cachedRootsByVersion.TryGetValue(version, out var cached))
        {
            return cached;
        }

        var repos = RepoRegistry.GetAllRepos().Distinct(StringComparer.Ordinal).ToList();

        var roots = await AkkaSource.From(repos)
            .SelectAsyncUnordered(4, async repo =>
            {
                var tagFormat = _resolver.GetTagFormat(repo, version);
                return await _fetcher.EnsureSourceAsync(repo, version, tagFormat, cancellationToken);
            })
            .RunWith(Sink.Seq<string>(), _actorSystem);

        var result = roots.ToList();
        _cachedRootsByVersion[version] = result;
        return result;
    }

    private async Task<IReadOnlyList<string>> EnsureAllLatestAsync(CancellationToken cancellationToken)
    {
        if (_cachedRoots is not null)
        {
            return _cachedRoots;
        }

        var repos = RepoRegistry.GetAllRepos().Distinct(StringComparer.Ordinal).ToList();

        var roots = await AkkaSource.From(repos)
            .SelectAsyncUnordered(4, async repo =>
            {
                var version = await _resolver.ResolveLatestAsync(repo, cancellationToken);
                var tagFormat = _resolver.GetTagFormat(repo);
                return await _fetcher.EnsureSourceAsync(repo, version, tagFormat, cancellationToken);
            })
            .RunWith(Sink.Seq<string>(), _actorSystem);

        var result = roots.ToList();
        _cachedRoots = result;
        return result;
    }
}
