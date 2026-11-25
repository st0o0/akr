using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class SerializationTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;

    public SerializationTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

    [GeneratedRegex(@"serialization-bindings\s*(=|\{|:)", RegexOptions.NonBacktracking)]
    private static partial Regex BindingsKeyRegex();

    [GeneratedRegex(@"^\s*""?([^""=\s]+)""?\s*=\s*""?([\w\-.]+)""?", RegexOptions.NonBacktracking)]
    private static partial Regex BindingLineRegex();

    [GeneratedRegex(@"ISerializationManifest|\bManifest\s*\(|\bManifest\s*=|SerializerWithStringManifest|IWithManifest|const\s+string\s+\w*Manifest\w*\s*=", RegexOptions.NonBacktracking)]
    private static partial Regex ManifestRegex();

    [McpServerTool(Name = "serializer_bindings"), Description("Show Akka.NET serialization-bindings (type to serializer mapping) from reference.conf files.")]
    public async Task<string> SerializerBindingsAsync(
        [Description("Optional filter on type or serializer name")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(FindSerializerBindings(roots, filter));
    }

    public string FindSerializerBindings(IEnumerable<string> roots, string? filter = null)
    {
        var rows = new List<(string File, string Type, string Serializer)>();
        foreach (var (full, relative) in _fileService.EnumerateConfigFiles(roots))
        {
            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!BindingsKeyRegex().IsMatch(lines[i]))
                {
                    continue;
                }

                foreach (var line in TypeScanner.CollectBlockLines(lines, i).Skip(1))
                {
                    var m = BindingLineRegex().Match(line);
                    if (m.Success)
                    {
                        rows.Add((relative, m.Groups[1].Value, m.Groups[2].Value));
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            rows = [.. rows.Where(r => r.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || r.Serializer.Contains(filter, StringComparison.OrdinalIgnoreCase))];
        }

        if (rows.Count == 0)
        {
            return "No serialization bindings found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("| Type | Serializer | Source |");
        sb.AppendLine("|------|------------|--------|");
        foreach (var r in rows.OrderBy(r => r.File, StringComparer.Ordinal).ThenBy(r => r.Type, StringComparer.Ordinal))
        {
            sb.AppendLine($"| {r.Type} | {r.Serializer} | {r.File} |");
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "serializer_detail"), Description("Show source and reference.conf config for Akka.NET serializer classes (Serializer / SerializerWithStringManifest).")]
    public async Task<string> SerializerDetailAsync(
        [Description("Serializer class name or fragment (e.g. Json, Protobuf)")] string name,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(FindSerializerDetail(roots, name));
    }

    public string FindSerializerDetail(IEnumerable<string> roots, string name)
    {
        var rootList = roots.ToList();
        var sb = new StringBuilder();
        var classNames = new List<string>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(rootList, "*.cs"))
        {
            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = TypeScanner.ClassWithBasesPattern().Match(lines[i]);
                if (!m.Success
                    || !m.Groups[1].Value.Contains(name, StringComparison.OrdinalIgnoreCase)
                    || !IsSerializerBase(m.Groups[2].Value))
                {
                    continue;
                }

                classNames.Add(m.Groups[1].Value);
                sb.AppendLine($"--- {relative}:{i + 1} ---");
                sb.AppendLine(string.Join('\n', TypeScanner.CollectBlockLines(lines, i, TypeScanner.MaxClassLines)));
                sb.AppendLine();
            }
        }

        if (classNames.Count == 0)
        {
            return $"No serializer class found matching: {name}";
        }

        foreach (var (full, relative) in _fileService.EnumerateFiles(rootList, "reference.conf"))
        {
            var hits = _fileService.ReadAllLines(full)
                .Where(l => classNames.Any(c => l.Contains(c, StringComparison.Ordinal)))
                .Select(l => l.Trim())
                .ToList();
            if (hits.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"--- config: {relative} ---");
            foreach (var hit in hits)
            {
                sb.AppendLine(hit);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool IsSerializerBase(string bases) =>
        bases.Split(',', StringSplitOptions.TrimEntries)
            .Any(b => b is "Serializer" or "SerializerWithStringManifest"
                || b.EndsWith(".Serializer", StringComparison.Ordinal)
                || b.EndsWith(".SerializerWithStringManifest", StringComparison.Ordinal));

    [McpServerTool(Name = "serialization_manifest"), Description("Find serialization manifest mappings (ISerializationManifest, Manifest methods, manifest constants) in persistence code.")]
    public async Task<string> SerializationManifestAsync(
        [Description("Optional file path filter (default: Persistence)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(FindManifests(roots, filter));
    }

    public string FindManifests(IEnumerable<string> roots, string? filter = null, int maxResults = 100)
    {
        var pathFilter = string.IsNullOrWhiteSpace(filter) ? "Persistence" : filter;
        var sb = new StringBuilder();
        var count = 0;

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!relative.Contains(pathFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!ManifestRegex().IsMatch(lines[i]))
                {
                    continue;
                }

                sb.AppendLine($"{relative}:{i + 1}: {lines[i].Trim()}");
                if (++count >= maxResults)
                {
                    return sb.ToString();
                }
            }
        }

        return count == 0 ? "No serialization manifest patterns found." : sb.ToString();
    }

}
