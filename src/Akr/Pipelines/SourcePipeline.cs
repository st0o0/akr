using Akka.Actor;
using Akka.Streams.Dsl;
using Akr.Registry;

namespace Akr.Pipelines;

public sealed class SourcePipeline
{
    private readonly GitHubClient _github;
    private readonly ActorSystem _actorSystem;
    private readonly Dictionary<string, IReadOnlyList<string>> _cachedRootsByVersion = new(StringComparer.Ordinal);
    private IReadOnlyList<string>? _cachedRoots;

    public SourcePipeline(GitHubClient github, ActorSystem actorSystem)
    {
        _github = github;
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

        var roots = await Source.From(repos)
            .SelectAsyncUnordered(4, repo =>
                _github.EnsureSourceForVersionAsync(repo, version, cancellationToken))
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

        var roots = await Source.From(repos)
            .SelectAsyncUnordered(4, repo =>
                _github.EnsureSourceAsync(repo, cancellationToken))
            .RunWith(Sink.Seq<string>(), _actorSystem);

        var result = roots.ToList();
        _cachedRoots = result;
        return result;
    }
}
