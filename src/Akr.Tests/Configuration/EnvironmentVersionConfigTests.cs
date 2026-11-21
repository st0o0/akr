using Akr.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akr.Tests.Configuration;

public sealed class EnvironmentVersionConfigTests
{
    private static readonly NullLogger<EnvironmentVersionConfig> _logger = new();

    [Fact]
    public void Parses_multiple_packages()
    {
        var config = new EnvironmentVersionConfig("Akka=1.5.30,Akka.Hosting=1.5.71", _logger);

        Assert.Equal("1.5.30", config.GetVersion("Akka"));
        Assert.Equal("1.5.71", config.GetVersion("Akka.Hosting"));
    }

    [Fact]
    public void Parses_single_package()
    {
        var config = new EnvironmentVersionConfig("Akka=1.5.30", _logger);

        Assert.Equal("1.5.30", config.GetVersion("Akka"));
        Assert.Single(config.GetAll());
    }

    [Fact]
    public void Returns_null_for_unconfigured_package()
    {
        var config = new EnvironmentVersionConfig("Akka=1.5.30", _logger);

        Assert.Null(config.GetVersion("Akka.Hosting"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_missing_env_var_returns_empty(string? raw)
    {
        var config = new EnvironmentVersionConfig(raw, _logger);

        Assert.Empty(config.GetAll());
    }

    [Fact]
    public void Skips_malformed_entries_without_equals()
    {
        var config = new EnvironmentVersionConfig("Akka1.5.30,Akka.Hosting=1.5.71", _logger);

        Assert.Null(config.GetVersion("Akka"));
        Assert.Equal("1.5.71", config.GetVersion("Akka.Hosting"));
    }

    [Fact]
    public void Skips_unknown_package_names()
    {
        var config = new EnvironmentVersionConfig("FakePackage=1.0.0,Akka=1.5.30", _logger);

        Assert.Null(config.GetVersion("FakePackage"));
        Assert.Equal("1.5.30", config.GetVersion("Akka"));
    }

    [Fact]
    public void Trims_whitespace_around_entries()
    {
        var config = new EnvironmentVersionConfig(" Akka = 1.5.30 , Akka.Hosting = 1.5.71 ", _logger);

        Assert.Equal("1.5.30", config.GetVersion("Akka"));
        Assert.Equal("1.5.71", config.GetVersion("Akka.Hosting"));
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        var config = new EnvironmentVersionConfig("Akka=1.5.30", _logger);

        Assert.Equal("1.5.30", config.GetVersion("akka"));
        Assert.Equal("1.5.30", config.GetVersion("AKKA"));
    }
}
