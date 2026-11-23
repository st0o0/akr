using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Akr.Tests;

internal static class TestHelpers
{
    public static byte[] JsonBody(object obj) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));

    public static byte[] CreateTestTarGz(string prefix, (string path, string content)[] files)
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
}
