using System.IO.Pipelines;
using System.Net;
using System.Text;
using Akr.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Akr.Tests.Integration;

public sealed class McpServerTests : IAsyncDisposable
{
    private readonly string _cacheRoot;
    private readonly Pipe _clientToServer = new();
    private readonly Pipe _serverToClient = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ServiceProvider _serviceProvider;
    private McpClient? _client;

    public McpServerTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), $"akr-mcp-test-{Guid.NewGuid()}");
        GitHubClient.CacheRoot = _cacheRoot;

        foreach (var repo in Akr.Registry.RepoRegistry.GetAllRepos())
        {
            var repoName = repo[(repo.IndexOf('/') + 1)..];
            Directory.CreateDirectory(Path.Combine(_cacheRoot, repoName, "1.5.75"));
        }

        var sourceRoot = Path.Combine(_cacheRoot, "akka.net", "1.5.75");
        WriteSource(sourceRoot, "src/core/Akka/Actor/ActorBase.cs", """
            namespace Akka.Actor
            {
                public abstract class ActorBase
                {
                    protected virtual void PreStart() { }
                    protected virtual void PostStop() { }
                    protected abstract bool Receive(object message);
                }
            }
            """);
        WriteSource(sourceRoot, "src/core/Akka/Actor/Props.cs", """
            namespace Akka.Actor
            {
                public sealed class Props
                {
                    public static Props Create<T>() where T : ActorBase, new() => new();
                }
            }
            """);

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        services.AddHttpClient<GitHubClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeGitHubHandler());

        services
            .AddMcpServer()
            .WithStreamServerTransport(
                _clientToServer.Reader.AsStream(),
                _serverToClient.Writer.AsStream())
            .WithTools<SearchSourceTool>()
            .WithTools<LookupTypeTool>();

        _serviceProvider = services.BuildServiceProvider(validateScopes: true);
        var server = _serviceProvider.GetRequiredService<McpServer>();
        _ = server.RunAsync(_cts.Token);
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        return _client ??= await McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: _clientToServer.Writer.AsStream(),
                serverOutput: _serverToClient.Reader.AsStream()),
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        await _cts.CancelAsync();
        _clientToServer.Writer.Complete();
        _serverToClient.Writer.Complete();
        await _serviceProvider.DisposeAsync();
        _cts.Dispose();

        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ListTools_returns_both_tools()
    {
        var client = await GetClientAsync(TestContext.Current.CancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var names = tools.Select(t => t.Name).ToList();
        Assert.Contains("search_source", names);
        Assert.Contains("lookup_type", names);
    }

    [Fact]
    public async Task SearchSource_finds_matching_code()
    {
        var client = await GetClientAsync(TestContext.Current.CancellationToken);
        var result = await client.CallToolAsync(
            "search_source",
            new Dictionary<string, object?> { ["query"] = "PreStart" },
            cancellationToken: TestContext.Current.CancellationToken);

        var text = string.Join("", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.Contains("PreStart", text);
        Assert.Contains("ActorBase.cs", text);
    }

    [Fact]
    public async Task SearchSource_returns_no_matches_message()
    {
        var client = await GetClientAsync(TestContext.Current.CancellationToken);
        var result = await client.CallToolAsync(
            "search_source",
            new Dictionary<string, object?> { ["query"] = "ThisDoesNotExistAnywhere12345" },
            cancellationToken: TestContext.Current.CancellationToken);

        var text = string.Join("", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.Contains("No matches found", text);
    }

    [Fact]
    public async Task LookupType_finds_ActorBase()
    {
        var client = await GetClientAsync(TestContext.Current.CancellationToken);
        var result = await client.CallToolAsync(
            "lookup_type",
            new Dictionary<string, object?> { ["type"] = "ActorBase" },
            cancellationToken: TestContext.Current.CancellationToken);

        var text = string.Join("", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.Contains("ActorBase", text);
        Assert.Contains("Akka.Actor", text);
        Assert.Contains("PreStart", text);
    }

    [Fact]
    public async Task LookupType_returns_not_found_message()
    {
        var client = await GetClientAsync(TestContext.Current.CancellationToken);
        var result = await client.CallToolAsync(
            "lookup_type",
            new Dictionary<string, object?> { ["type"] = "NonExistentType" },
            cancellationToken: TestContext.Current.CancellationToken);

        var text = string.Join("", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.Contains("not found", text);
    }

    [Fact]
    public async Task LookupType_full_source()
    {
        var client = await GetClientAsync(TestContext.Current.CancellationToken);
        var result = await client.CallToolAsync(
            "lookup_type",
            new Dictionary<string, object?> { ["type"] = "Props", ["fullSource"] = true },
            cancellationToken: TestContext.Current.CancellationToken);

        var text = string.Join("", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.Contains("Create<T>", text);
        Assert.Contains("namespace Akka.Actor", text);
    }

    private static void WriteSource(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private sealed class FakeGitHubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (uri.Host == "api.github.com" && uri.AbsolutePath.Contains("/releases/latest"))
            {
                var json = """{"tag_name": "1.5.75"}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
