using Akr.Registry;

namespace Akr.Tests.Registry;

public sealed class RepoRegistryTests
{
    [Theory]
    [InlineData("Akka", "akkadotnet/akka.net", "src/core/Akka")]
    [InlineData("Akka.Remote", "akkadotnet/akka.net", "src/core/Akka.Remote")]
    [InlineData("Akka.Hosting", "akkadotnet/akka.net", "src/contrib/hosting/Akka.Hosting")]
    [InlineData("Akka.Persistence.Embedded.Hosting", "akkadotnet/akka.net", "src/contrib/hosting/Akka.Persistence.Embedded.Hosting")]
    [InlineData("Akka.Hosting.TestKit.Xunit2", "akkadotnet/akka.net", "src/contrib/hosting/Akka.Hosting.TestKit.Xunit2")]
    [InlineData("Akka.Reminders", "akkadotnet/akka.net", "src/contrib/reminders/Akka.Reminders")]
    [InlineData("Akka.Reminders.Sql", "akkadotnet/akka.net", "src/contrib/reminders/Akka.Reminders.Sql")]
    [InlineData("Akka.Management", "akkadotnet/Akka.Management", "src/management/Akka.Management")]
    [InlineData("Akka.Persistence.Sql", "akkadotnet/Akka.Persistence.Sql", "src/Akka.Persistence.Sql")]
    [InlineData("Akka.Persistence.MongoDb", "akkadotnet/Akka.Persistence.MongoDB", "src/Akka.Persistence.MongoDb")]
    public void GetEntry_returns_correct_mapping(string package, string expectedRepo, string expectedPath)
    {
        var entry = RepoRegistry.GetEntry(package);

        Assert.NotNull(entry);
        Assert.Equal(expectedRepo, entry.Repo);
        Assert.Equal(expectedPath, entry.SourcePath);
    }

    [Fact]
    public void GetEntry_is_case_insensitive()
    {
        var entry = RepoRegistry.GetEntry("akka.hosting");

        Assert.NotNull(entry);
        Assert.Equal("akkadotnet/akka.net", entry.Repo);
    }

    [Fact]
    public void GetEntry_returns_null_for_unknown_package()
    {
        Assert.Null(RepoRegistry.GetEntry("FakePackage"));
    }

    [Fact]
    public void GetAllRepos_returns_all_four_repos()
    {
        var repos = RepoRegistry.GetAllRepos();

        Assert.Equal(4, repos.Count);
        Assert.Contains("akkadotnet/akka.net", repos);
        Assert.Contains("akkadotnet/Akka.Management", repos);
        Assert.Contains("akkadotnet/Akka.Persistence.Sql", repos);
        Assert.Contains("akkadotnet/Akka.Persistence.MongoDB", repos);
    }

    [Fact]
    public void ResolveTags_returns_unprefixed_first_then_prefixed()
    {
        var tags = RepoRegistry.ResolveTags("1.5.30").ToList();

        Assert.Equal(["1.5.30", "v1.5.30"], tags);
    }
}
