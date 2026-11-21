using Akr.Core;
using Akr.Core.Caching;
using Akr.Core.Configuration;
using Akr.Core.Fetching;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Akr.Tests;

public sealed class SourceProviderTests
{
    private readonly IVersionConfig _versionConfig = Substitute.For<IVersionConfig>();
    private readonly ISourceCache _cache = Substitute.For<ISourceCache>();
    private readonly ISourceFetcher _fetcher = Substitute.For<ISourceFetcher>();
    private readonly SourceProvider _provider;

    public SourceProviderTests()
    {
        _provider = new SourceProvider(_versionConfig, _cache, _fetcher, NullLogger<SourceProvider>.Instance);
    }

    [Fact]
    public async Task GetSourceRootAsync_returns_null_for_unknown_package()
    {
        var result = await _provider.GetSourceRootAsync("FakePackage", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSourceRootAsync_uses_configured_version()
    {
        _versionConfig.GetVersion("Akka").Returns("1.5.30");
        _cache.Exists("akkadotnet/akka.net", "1.5.30").Returns(true);
        _cache.GetPath("akkadotnet/akka.net", "1.5.30").Returns("/cache/akka.net/1.5.30");

        await _provider.GetSourceRootAsync("Akka", cancellationToken: TestContext.Current.CancellationToken);

        await _fetcher.DidNotReceiveWithAnyArgs().FetchAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetSourceRootAsync_fetches_when_not_cached()
    {
        _versionConfig.GetVersion("Akka").Returns("1.5.30");
        _cache.Exists("akkadotnet/akka.net", "1.5.30").Returns(false);
        _cache.GetPath("akkadotnet/akka.net", "1.5.30").Returns("/cache/akka.net/1.5.30");

        await _provider.GetSourceRootAsync("Akka", cancellationToken: TestContext.Current.CancellationToken);

        await _fetcher.Received(1).FetchAsync(
            "akkadotnet/akka.net",
            "1.5.30",
            "/cache/akka.net/1.5.30",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSourceRootAsync_resolves_latest_when_no_version_configured()
    {
        _versionConfig.GetVersion("Akka").Returns((string?)null);
        _fetcher.ResolveLatestVersionAsync("akkadotnet/akka.net", Arg.Any<CancellationToken>()).Returns("1.5.75");
        _cache.Exists("akkadotnet/akka.net", "1.5.75").Returns(true);
        _cache.GetPath("akkadotnet/akka.net", "1.5.75").Returns("/cache/akka.net/1.5.75");

        await _provider.GetSourceRootAsync("Akka", cancellationToken: TestContext.Current.CancellationToken);

        await _fetcher.Received(1).ResolveLatestVersionAsync("akkadotnet/akka.net", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSourceRootAsync_uses_version_override()
    {
        _versionConfig.GetVersion("Akka").Returns("1.5.30");
        _cache.Exists("akkadotnet/akka.net", "1.5.75").Returns(true);
        _cache.GetPath("akkadotnet/akka.net", "1.5.75").Returns("/cache/akka.net/1.5.75");

        await _provider.GetSourceRootAsync("Akka", versionOverride: "1.5.75", cancellationToken: TestContext.Current.CancellationToken);

        _cache.Received(1).Exists("akkadotnet/akka.net", "1.5.75");
    }

    [Fact]
    public async Task EnsureAllConfiguredAsync_fetches_each_repo_once()
    {
        _versionConfig.GetAll().Returns(new Dictionary<string, string>
        {
            ["Akka"] = "1.5.30",
            ["Akka.Remote"] = "1.5.30",
            ["Akka.Hosting"] = "1.5.71",
        });
        _cache.Exists(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _cache.GetPath(Arg.Any<string>(), Arg.Any<string>()).Returns(ci =>
            $"/cache/{((string)ci[0]).Split('/')[1]}/{ci[1]}");

        await _provider.EnsureAllConfiguredAsync(TestContext.Current.CancellationToken);

        // akka.net should be fetched once (not twice for Akka + Akka.Remote)
        await _fetcher.Received(1).FetchAsync(
            "akkadotnet/akka.net", "1.5.30", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fetcher.Received(1).FetchAsync(
            "akkadotnet/Akka.Hosting", "1.5.71", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
