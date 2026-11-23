using System.IO.Abstractions;
using Akr.Analysis;
using Akr.Tools;
using Microsoft.Extensions.DependencyInjection;
using Terp.Hosting;

namespace Akr.Configuration;

public sealed class McpSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddSingleton<IFileSystem>(new FileSystem());
        services.AddSingleton<FileService>();
        services.AddSingleton<SourceSearcher>();
        services.AddSingleton<TypeScanner>();
        services.AddSingleton<DeepIntelligenceTools>();

        services.AddHttpClient<GitHubClient>(client =>
        {
            var version = typeof(McpSetup).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"akr/{version}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<TypeTools>()
            .WithTools<HostingTools>()
            .WithTools<StreamTools>()
            .WithTools<DeepIntelligenceTools>()
            .WithTools<StreamPersistenceTools>()
            .WithTools<ConfigMigrationTools>()
            .WithTools<EcosystemTools>();
    }
}
