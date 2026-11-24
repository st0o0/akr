using System.Text.Json.Serialization;

namespace Akr;

public sealed record CachedVersionEntry
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("lastAccessed")]
    public DateTimeOffset LastAccessed { get; init; }

    [JsonPropertyName("tagFormat")]
    public string? TagFormat { get; init; }
}

public sealed record RepoMetadata
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("etag")]
    public string? Etag { get; init; }

    [JsonPropertyName("lastChecked")]
    public DateTimeOffset LastChecked { get; init; }

    [JsonPropertyName("tagFormat")]
    public string? TagFormat { get; init; }

    [JsonPropertyName("cachedVersions")]
    public IReadOnlyList<CachedVersionEntry> CachedVersions { get; init; } = [];
}
