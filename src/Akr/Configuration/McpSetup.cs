using Akr.Tools;
using Microsoft.Extensions.DependencyInjection;
using Terp.Hosting;

namespace Akr.Configuration;

public sealed class McpSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddHttpClient<GitHubClient>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("akr/0.1");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<TypeTools>()
            .WithTools<HostingTools>()
            .WithTools<StreamTools>();
    }
}
