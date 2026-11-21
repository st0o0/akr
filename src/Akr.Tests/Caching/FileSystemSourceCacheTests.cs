using Akr.Core.Caching;

namespace Akr.Tests.Caching;

public sealed class FileSystemSourceCacheTests
{
    [Fact]
    public void GetPath_strips_owner_from_repo()
    {
        var cache = new FileSystemSourceCache("/tmp/test-cache");

        var path = cache.GetPath("akkadotnet/akka.net", "1.5.30");

        Assert.Equal(Path.Combine("/tmp/test-cache", "akka.net", "1.5.30"), path);
    }

    [Fact]
    public void GetPath_handles_repo_without_owner()
    {
        var cache = new FileSystemSourceCache("/tmp/test-cache");

        var path = cache.GetPath("akka.net", "1.5.30");

        Assert.Equal(Path.Combine("/tmp/test-cache", "akka.net", "1.5.30"), path);
    }

    [Fact]
    public void Exists_returns_false_for_nonexistent_directory()
    {
        var cache = new FileSystemSourceCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.False(cache.Exists("akkadotnet/akka.net", "1.5.30"));
    }

    [Fact]
    public void Exists_returns_true_for_existing_directory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var cache = new FileSystemSourceCache(tempRoot);
        var path = cache.GetPath("akkadotnet/akka.net", "1.5.30");
        Directory.CreateDirectory(path);

        try
        {
            Assert.True(cache.Exists("akkadotnet/akka.net", "1.5.30"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void GetCacheRoot_returns_configured_root()
    {
        var cache = new FileSystemSourceCache("/my/cache");

        Assert.Equal("/my/cache", cache.GetCacheRoot());
    }
}
