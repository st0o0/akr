using Microsoft.Extensions.Logging;

namespace Akr.Configuration;

public sealed class AkrOptions
{
    public string CacheRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".akr", "sources");

    public int CacheTtlHours { get; set; } = 24;

    public string? ProjectPath { get; set; }

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

            var projectPath = Environment.GetEnvironmentVariable("AKR_PROJECT_PATH");
            if (!string.IsNullOrEmpty(projectPath))
            {
                options.ProjectPath = projectPath;
            }
        };
    }
}
