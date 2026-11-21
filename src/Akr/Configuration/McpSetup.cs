using Akr.Core;
using Akr.Core.Caching;
using Akr.Core.Configuration;
using Akr.Core.Fetching;
using Akr.Tools;
using Microsoft.Extensions.DependencyInjection;
using Terp.Hosting;

namespace Akr.Configuration;

public sealed class McpSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddSingleton<IVersionConfig, EnvironmentVersionConfig>();
        services.AddSingleton<ISourceCache, FileSystemSourceCache>();
        services.AddSingleton<ISourceProvider, SourceProvider>();

        services.AddHttpClient<GitHubArchiveFetcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("akr/0.1");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });
        services.AddSingleton<ISourceFetcher>(sp => sp.GetRequiredService<GitHubArchiveFetcher>());

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<SearchSourceTool>()
            .WithTools<LookupTypeTool>();
    }
}
