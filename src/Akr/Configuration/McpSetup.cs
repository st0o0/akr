using System.IO.Abstractions;
using Akka.Hosting;
using Akr.Analysis;
using Akr.Pipelines;
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
        services.AddSingleton<DocsIndex>();
        services.AddSingleton<DeepIntelligenceTools>();
        services.AddSingleton<ProjectScanner>();
        services.AddSingleton(TimeProvider.System);

        services.Configure<AkrOptions>(AkrOptions.BindFromEnvironment());

        services.AddAkka("akr", (builder, _) => { });

        services.AddSingleton<SourcePipeline>();
        services.AddSingleton<DocsPipeline>();

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
                    "or read the akr://context resource to see the current version resolution state. " +
                    "All source tools accept an optional version parameter to target a specific Akka.NET version. " +
                    "Use docs_search and docs_example for conceptual documentation and code examples. " +
                    "Use source_diff to compare types or config between versions, and migration_guide for upgrade guidance.";
            })
            .WithStdioServerTransport()
            .WithTools<TypeTools>()
            .WithTools<HostingTools>()
            .WithTools<StreamTools>()
            .WithTools<DeepIntelligenceTools>()
            .WithTools<StreamPersistenceTools>()
            .WithTools<ConfigMigrationTools>()
            .WithTools<EcosystemTools>()
            .WithTools<DocsTools>()
            .WithTools<VersionTools>()
            .WithTools<ProjectContextTools>()
            .WithResources<ProjectContextResources>();
    }
}
