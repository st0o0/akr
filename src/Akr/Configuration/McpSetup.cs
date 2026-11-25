using System.IO.Abstractions;
using Akka.Hosting;
using Akr.Scanning;
using Akr.Source;
using Akr.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.AddSingleton<HoconScanner>();
        services.AddSingleton<DocsIndex>();
        services.AddSingleton(TimeProvider.System);

        services.Configure<AkrOptions>(AkrOptions.BindFromEnvironment());

        services.AddSingleton<ProjectScanner>(sp =>
        {
            var scanner = ActivatorUtilities.CreateInstance<ProjectScanner>(sp);
            var options = sp.GetRequiredService<IOptions<AkrOptions>>().Value;
            scanner.Initialize(options.ProjectPath);
            return scanner;
        });

        services.AddAkka("akr", (builder, _) => { });

        services.AddSingleton<SourceCache>();
        services.AddSingleton<VersionResolver>();
        services.AddSingleton<SourcePipeline>();

        services.AddHttpClient<SourceFetcher>(client =>
        {
            var version = typeof(McpSetup).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"akr/{version}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });

        services.AddHttpClient<VersionResolver>(client =>
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
            .WithTools<SearchTools>()
            .WithTools<ConfigTools>()
            .WithTools<HostingTools>()
            .WithTools<StreamTools>()
            .WithTools<PersistenceTools>()
            .WithTools<SerializationTools>()
            .WithTools<ClusterTools>()
            .WithTools<TestKitTools>()
            .WithTools<DocsTools>()
            .WithTools<VersionTools>()
            .WithTools<ProjectContextTools>()
            .WithResources<ProjectContextResources>();
    }
}
