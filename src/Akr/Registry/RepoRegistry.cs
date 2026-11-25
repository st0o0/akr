using System.Collections.Frozen;

namespace Akr.Registry;

public static class RepoRegistry
{
    private const string Org = "akkadotnet";
    private const string Repo = "akka.net";

    private static readonly FrozenDictionary<string, PackageEntry> _packages = new Dictionary<string, PackageEntry>
    {
        // akkadotnet/akka.net (mono-repo)
        ["Akka"] = new($"{Org}/{Repo}", "src/core/Akka"),
        ["Akka.Remote"] = new($"{Org}/{Repo}", "src/core/Akka.Remote"),
        ["Akka.Cluster"] = new($"{Org}/{Repo}", "src/core/Akka.Cluster"),
        ["Akka.Streams"] = new($"{Org}/{Repo}", "src/core/Akka.Streams"),
        ["Akka.Persistence"] = new($"{Org}/{Repo}", "src/core/Akka.Persistence"),
        ["Akka.Persistence.Query"] = new($"{Org}/{Repo}", "src/core/Akka.Persistence.Query"),
        ["Akka.Coordination"] = new($"{Org}/{Repo}", "src/core/Akka.Coordination"),
        ["Akka.Discovery"] = new($"{Org}/{Repo}", "src/core/Akka.Discovery"),
        ["Akka.TestKit"] = new($"{Org}/{Repo}", "src/core/Akka.TestKit"),

        // akkadotnet/akka.net (hosting - merged from archived Akka.Hosting repo)
        ["Akka.Hosting"] = new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Hosting"),
        ["Akka.Hosting.TestKit"] = new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Hosting.TestKit"),
        ["Akka.Hosting.TestKit.Xunit2"] = new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Hosting.TestKit.Xunit2"),
        ["Akka.Cluster.Hosting"] = new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Cluster.Hosting"),
        ["Akka.Remote.Hosting"] = new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Remote.Hosting"),
        ["Akka.Persistence.Hosting"] = new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Persistence.Hosting"),
        ["Akka.Persistence.Embedded.Hosting"] =
            new($"{Org}/{Repo}", "src/contrib/hosting/Akka.Persistence.Embedded.Hosting"),

        // akkadotnet/akka.net (reminders)
        ["Akka.Reminders"] = new($"{Org}/{Repo}", "src/contrib/reminders/Akka.Reminders"),
        ["Akka.Reminders.Sql"] = new($"{Org}/{Repo}", "src/contrib/reminders/Akka.Reminders.Sql"),
        ["Akka.Reminders.SqlServer"] = new($"{Org}/{Repo}", "src/contrib/reminders/Akka.Reminders.SqlServer"),
        ["Akka.Reminders.PostgreSql"] = new($"{Org}/{Repo}", "src/contrib/reminders/Akka.Reminders.PostgreSql"),
        ["Akka.Reminders.Sqlite"] = new($"{Org}/{Repo}", "src/contrib/reminders/Akka.Reminders.Sqlite"),

        // akkadotnet/Akka.Management (nested layout)
        ["Akka.Management"] = new("akkadotnet/Akka.Management", "src/management/Akka.Management"),
        ["Akka.Discovery.KubernetesApi"] = new("akkadotnet/Akka.Management",
            "src/discovery/kubernetes/Akka.Discovery.KubernetesApi"),
        ["Akka.Discovery.AwsApi"] = new("akkadotnet/Akka.Management", "src/discovery/aws/Akka.Discovery.AwsApi"),

        // akkadotnet/Akka.Persistence.Sql
        ["Akka.Persistence.Sql"] = new("akkadotnet/Akka.Persistence.Sql", "src/Akka.Persistence.Sql"),
        ["Akka.Persistence.Sql.Hosting"] = new("akkadotnet/Akka.Persistence.Sql", "src/Akka.Persistence.Sql.Hosting"),

        // akkadotnet/Akka.Persistence.MongoDB
        ["Akka.Persistence.MongoDb"] = new("akkadotnet/Akka.Persistence.MongoDB", "src/Akka.Persistence.MongoDb"),
        ["Akka.Persistence.MongoDb.Hosting"] =
            new("akkadotnet/Akka.Persistence.MongoDB", "src/Akka.Persistence.MongoDb.Hosting"),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public const string MainRepo = $"{Org}/{Repo}";
    public const string DocsPath = "docs/articles";

    private static readonly IReadOnlyCollection<string> _allRepos =
        _packages.Values.Select(e => e.Repo).Distinct(StringComparer.Ordinal).ToArray();

    public static PackageEntry? GetEntry(string packageName) =>
        _packages.GetValueOrDefault(packageName);

    public static IReadOnlyCollection<string> GetAllRepos() => _allRepos;

    public static IReadOnlyCollection<string> GetAllPackageNames() => _packages.Keys;

    public static IEnumerable<string> ResolveTags(string version) => [version, $"v{version}"];
}
