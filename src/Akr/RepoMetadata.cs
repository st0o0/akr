using System.Text.Json.Serialization;

namespace Akr;

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
}
