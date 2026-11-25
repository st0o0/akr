using System.Text.Json.Serialization;
using Akr.Tools;

namespace Akr;

[JsonSerializable(typeof(RepoMetadata))]
[JsonSerializable(typeof(ProjectContextInfo))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class AkrJsonContext : JsonSerializerContext;
