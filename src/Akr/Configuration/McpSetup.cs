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
        services.AddSingleton<ProjectScanner>();
        services.AddSingleton(TimeProvider.System);

        services.Configure<AkrOptions>(AkrOptions.BindFromEnvironment());

        services.AddHostedService<ProjectContextInitializer>();

        services.AddHttpClient<GitHubClient>(client =>
        {
            var version = typeof(McpSetup).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"akr/{version}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });

        services
            .AddMcpServer(options =>
            {
                options.ServerInstructions =
                    "akr provides Akka.NET source intelligence. It auto-detects the Akka.NET version from your project's NuGet references. " +
                    "If working in a .NET project directory, akr will automatically use the referenced Akka version. " +
                    "Use the set_project_context tool to point akr at a different project directory, " +
                    "or read the akr://context resource to see the current version resolution state.";
            })
            .WithStdioServerTransport()
            .WithTools<TypeTools>()
            .WithTools<HostingTools>()
            .WithTools<StreamTools>()
            .WithTools<DeepIntelligenceTools>()
            .WithTools<StreamPersistenceTools>()
            .WithTools<ConfigMigrationTools>()
            .WithTools<EcosystemTools>()
            .WithTools<ProjectContextTools>()
            .WithResources<ProjectContextResources>();
    }
}
