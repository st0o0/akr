using Akr.Analysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akr.Configuration;

public sealed class ProjectContextInitializer : IHostedService
{
    private readonly ProjectScanner _scanner;
    private readonly AkrOptions _options;
    private readonly ILogger<ProjectContextInitializer> _logger;

    public ProjectContextInitializer(
        ProjectScanner scanner,
        IOptions<AkrOptions> options,
        ILogger<ProjectContextInitializer> logger)
    {
        _scanner = scanner;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.ProjectPath))
        {
            _logger.LogInformation("AKR_PROJECT_PATH set to {Path}", _options.ProjectPath);
            var result = _scanner.Scan(_options.ProjectPath);
            if (result.HasPackages)
            {
                _scanner.Current = result;
                return Task.CompletedTask;
            }

            _logger.LogWarning("No Akka.NET packages found at AKR_PROJECT_PATH={Path}", _options.ProjectPath);
        }

        var cwd = Environment.CurrentDirectory;
        _logger.LogDebug("Scanning working directory for Akka.NET project: {Path}", cwd);
        var cwdResult = _scanner.Scan(cwd);
        if (cwdResult.HasPackages)
        {
            _scanner.Current = cwdResult;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
