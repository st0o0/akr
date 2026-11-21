using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using Akr.Core.Fetching;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akr.Tests.Fetching;

public sealed class GitHubArchiveFetcherTests
{
    [Fact]
    public async Task FetchAsync_extracts_tarball_stripping_prefix()
    {
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tarballBytes = CreateTestTarGz("akka.net-1.5.30/", [
            ("akka.net-1.5.30/src/core/Akka/Actor.cs", "public class Actor {}"),
            ("akka.net-1.5.30/README.md", "# Akka.NET"),
        ]);

        var handler = new FakeHandler(tarballBytes);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://github.com") };
        var fetcher = new GitHubArchiveFetcher(httpClient, NullLogger<GitHubArchiveFetcher>.Instance);

        try
        {
            await fetcher.FetchAsync("akkadotnet/akka.net", "1.5.30", targetPath, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(Path.Combine(targetPath, "src", "core", "Akka", "Actor.cs")));
            Assert.True(File.Exists(Path.Combine(targetPath, "README.md")));
        }
        finally
        {
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FetchAsync_throws_on_not_found()
    {
        var handler = new FakeHandler(HttpStatusCode.NotFound);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://github.com") };
        var fetcher = new GitHubArchiveFetcher(httpClient, NullLogger<GitHubArchiveFetcher>.Instance);

        await Assert.ThrowsAsync<SourceFetchException>(
            () => fetcher.FetchAsync("akkadotnet/akka.net", "0.0.0", "/tmp/nope", TestContext.Current.CancellationToken));
    }

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
                    DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
                };
                tarWriter.WriteEntry(entry);
            }
        }

        return memoryStream.ToArray();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly byte[]? _responseBytes;
        private readonly HttpStatusCode _statusCode;

        public FakeHandler(byte[] responseBytes)
        {
            _responseBytes = responseBytes;
            _statusCode = HttpStatusCode.OK;
        }

        public FakeHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_responseBytes is not null)
            {
                response.Content = new ByteArrayContent(_responseBytes);
            }

            return Task.FromResult(response);
        }
    }
}
