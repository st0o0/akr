using System.Collections.Frozen;

namespace Akr.Registry;

public static class RepoRegistry
{
    private static readonly FrozenDictionary<string, PackageEntry> _packages = new Dictionary<string, PackageEntry>
    {
        // akkadotnet/akka.net (mono-repo)
        ["Akka"] = new("akkadotnet/akka.net", "src/core/Akka"),
        ["Akka.Remote"] = new("akkadotnet/akka.net", "src/core/Akka.Remote"),
        ["Akka.Cluster"] = new("akkadotnet/akka.net", "src/core/Akka.Cluster"),
        ["Akka.Streams"] = new("akkadotnet/akka.net", "src/core/Akka.Streams"),
        ["Akka.Persistence"] = new("akkadotnet/akka.net", "src/core/Akka.Persistence"),
        ["Akka.Persistence.Query"] = new("akkadotnet/akka.net", "src/core/Akka.Persistence.Query"),
        ["Akka.Coordination"] = new("akkadotnet/akka.net", "src/core/Akka.Coordination"),
        ["Akka.Discovery"] = new("akkadotnet/akka.net", "src/core/Akka.Discovery"),
        ["Akka.TestKit"] = new("akkadotnet/akka.net", "src/core/Akka.TestKit"),

        // akkadotnet/Akka.Hosting (archived, read-only)
        ["Akka.Hosting"] = new("akkadotnet/Akka.Hosting", "src/Akka.Hosting"),
        ["Akka.Hosting.TestKit"] = new("akkadotnet/Akka.Hosting", "src/Akka.Hosting.TestKit"),
        ["Akka.Cluster.Hosting"] = new("akkadotnet/Akka.Hosting", "src/Akka.Cluster.Hosting"),
        ["Akka.Remote.Hosting"] = new("akkadotnet/Akka.Hosting", "src/Akka.Remote.Hosting"),
        ["Akka.Persistence.Hosting"] = new("akkadotnet/Akka.Hosting", "src/Akka.Persistence.Hosting"),

        // akkadotnet/Akka.Management (nested layout)
        ["Akka.Management"] = new("akkadotnet/Akka.Management", "src/management/Akka.Management"),
        ["Akka.Discovery.KubernetesApi"] = new("akkadotnet/Akka.Management", "src/discovery/kubernetes/Akka.Discovery.KubernetesApi"),
        ["Akka.Discovery.AwsApi"] = new("akkadotnet/Akka.Management", "src/discovery/aws/Akka.Discovery.AwsApi"),

        // akkadotnet/Akka.Persistence.Sql
        ["Akka.Persistence.Sql"] = new("akkadotnet/Akka.Persistence.Sql", "src/Akka.Persistence.Sql"),
        ["Akka.Persistence.Sql.Hosting"] = new("akkadotnet/Akka.Persistence.Sql", "src/Akka.Persistence.Sql.Hosting"),

        // akkadotnet/Akka.Persistence.MongoDB
        ["Akka.Persistence.MongoDb"] = new("akkadotnet/Akka.Persistence.MongoDB", "src/Akka.Persistence.MongoDb"),
        ["Akka.Persistence.MongoDb.Hosting"] = new("akkadotnet/Akka.Persistence.MongoDB", "src/Akka.Persistence.MongoDb.Hosting"),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static PackageEntry? GetEntry(string packageName) =>
        _packages.GetValueOrDefault(packageName);

    public static IReadOnlyCollection<string> GetAllRepos() =>
        _packages.Values.Select(e => e.Repo).Distinct(StringComparer.Ordinal).ToArray();

    public static IReadOnlyCollection<string> GetAllPackageNames() =>
        _packages.Keys;

    public static IEnumerable<string> ResolveTags(string version) =>
        [version, $"v{version}"];
}
