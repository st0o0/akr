using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akr.Tests;

public sealed class GitHubClientTests
{
    [Fact]
    public async Task EnsureSourceAsync_extracts_tarball_stripping_prefix()
    {
        var tarball = CreateTestTarGz("akka.net-1.5.30/", [
            ("akka.net-1.5.30/src/core/Akka/Actor.cs", "public class Actor {}"),
            ("akka.net-1.5.30/README.md", "# Akka.NET"),
        ]);

        var handler = new FakeHandler(new Dictionary<string, (HttpStatusCode Status, byte[] Body)>
        {
            ["api.github.com"] = (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.30" })),
            ["github.com"] = (HttpStatusCode.OK, tarball),
        });

        var client = CreateClient(handler);

        var path = await client.EnsureSourceAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        try
        {
            Assert.True(File.Exists(Path.Combine(path, "src", "core", "Akka", "Actor.cs")));
            Assert.True(File.Exists(Path.Combine(path, "README.md")));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ResolveLatestAsync_caches_result()
    {
        var callCount = 0;
        var handler = new FakeHandler(_ =>
        {
            callCount++;
            return (HttpStatusCode.OK, JsonBody(new { tag_name = "1.5.75" }));
        });

        var client = CreateClient(handler);

        var v1 = await client.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);
        var v2 = await client.ResolveLatestAsync("akkadotnet/akka.net", TestContext.Current.CancellationToken);

        Assert.Equal("1.5.75", v1);
        Assert.Equal("1.5.75", v2);
        Assert.Equal(1, callCount);
    }

    private static GitHubClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<GitHubClient>.Instance);

    private static byte[] JsonBody(object obj) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

    private static byte[] CreateTestTarGz(string prefix, (string path, string content)[] files)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
                };
                tarWriter.WriteEntry(entry);
            }
        }

        return memoryStream.ToArray();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode Status, byte[] Body)> _handler;

        public FakeHandler(Dictionary<string, (HttpStatusCode Status, byte[] Body)> responses)
        {
            _handler = req => responses.TryGetValue(req.RequestUri!.Host, out var r)
                ? r
                : (HttpStatusCode.NotFound, []);
        }

        public FakeHandler(Func<HttpRequestMessage, (HttpStatusCode Status, byte[] Body)> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = _handler(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }
}
