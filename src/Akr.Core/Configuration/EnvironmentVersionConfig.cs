using System.Collections.Frozen;
using Akr.Core.Registry;
using Microsoft.Extensions.Logging;

namespace Akr.Core.Configuration;

public sealed class EnvironmentVersionConfig : IVersionConfig
{
    public const string EnvVarName = "AKKA_VERSIONS";

    private readonly FrozenDictionary<string, string> _versions;

    public EnvironmentVersionConfig(ILogger<EnvironmentVersionConfig> logger)
    {
        var raw = Environment.GetEnvironmentVariable(EnvVarName);
        _versions = Parse(raw, logger);
    }

    internal EnvironmentVersionConfig(string? raw, ILogger<EnvironmentVersionConfig> logger)
    {
        _versions = Parse(raw, logger);
    }

    public string? GetVersion(string packageName) =>
        _versions.GetValueOrDefault(packageName);

    public IReadOnlyDictionary<string, string> GetAll() => _versions;

    private static FrozenDictionary<string, string> Parse(string? raw, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FrozenDictionary<string, string>.Empty;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex <= 0)
            {
                logger.LogWarning("Malformed AKKA_VERSIONS entry (missing '='): {Entry}", entry);
                continue;
            }

            var package = entry[..eqIndex].Trim();
            var version = entry[(eqIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(version))
            {
                logger.LogWarning("Malformed AKKA_VERSIONS entry (empty package or version): {Entry}", entry);
                continue;
            }

            if (RepoRegistry.GetEntry(package) is null)
            {
                logger.LogWarning("Unknown Akka package in AKKA_VERSIONS: {Package}", package);
                continue;
            }

            result[package] = version;
        }

        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
