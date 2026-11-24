using Microsoft.Extensions.Logging;

namespace Akr.Configuration;

public sealed class AkrOptions
{
    public string CacheRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akr", "sources");

    public int CacheTtlHours { get; set; } = 24;

    public string? ProjectPath { get; set; }

    public int MaxCachedVersions { get; set; } = 3;

    public TimeSpan CacheTtl => TimeSpan.FromHours(CacheTtlHours);

    public static Action<AkrOptions> BindFromEnvironment(ILogger? logger = null)
    {
        return options =>
        {
            var cacheRoot = Environment.GetEnvironmentVariable("AKR_CACHE_ROOT");
            if (!string.IsNullOrEmpty(cacheRoot))
            {
                options.CacheRoot = cacheRoot;
            }

            var ttl = Environment.GetEnvironmentVariable("AKR_CACHE_TTL_HOURS");
            if (!string.IsNullOrEmpty(ttl))
            {
                if (int.TryParse(ttl, out var hours) && hours >= 0)
                {
                    options.CacheTtlHours = hours;
                }
                else
                {
                    logger?.LogWarning("Invalid AKR_CACHE_TTL_HOURS value '{Value}', using default 24h", ttl);
                }
            }

            var maxVersions = Environment.GetEnvironmentVariable("AKR_MAX_CACHED_VERSIONS");
            if (!string.IsNullOrEmpty(maxVersions))
            {
                if (int.TryParse(maxVersions, out var max) && max >= 1)
                {
                    options.MaxCachedVersions = max;
                }
                else
                {
                    logger?.LogWarning("Invalid AKR_MAX_CACHED_VERSIONS value '{Value}', using default 3", maxVersions);
                }
            }

            var projectPath = Environment.GetEnvironmentVariable("AKR_PROJECT_PATH");
            if (!string.IsNullOrEmpty(projectPath))
            {
                options.ProjectPath = projectPath;
            }
        };
    }
}
