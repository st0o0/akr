using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class StreamTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;

    public StreamTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

    [McpServerTool(Name = "stream_stages"), Description("Catalog built-in Akka.Streams stages with shape classification.")]
    public async Task<string> ListStagesAsync(
        [Description("Optional keyword to filter stages (e.g. fan, merge, broadcast)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return Format(FindStages(roots, filter), filter);
    }

    public static readonly IReadOnlyList<string> Categories = ["Source", "Flow", "Sink", "Fan-In", "Fan-Out", "Custom"];

    [GeneratedRegex(@"\bclass\s+(?<name>\w+)\s*(?<generics><[^>\r\n]*>)?\s*:\s*(?<bases>[^\r\n{]+)", RegexOptions.NonBacktracking)]
    private static partial Regex ClassPattern();

    private static readonly string[] _stageBases =
    [
        "GraphStage<",
        "GraphStageWithMaterializedValue<",
        "SimpleLinearGraphStage<",
        "PushStage<",
        "PushPullStage<",
    ];

    public IReadOnlyList<StageInfo> FindStages(IEnumerable<string> roots, string? filter = null)
    {
        var found = new HashSet<StageInfo>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!relative.Contains("Akka.Streams", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var stage in ExtractStages(_fileService.ReadAllText(full)))
            {
                found.Add(stage);
            }
        }

        var query = found.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(s =>
                s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(s => CategoryIndex(s.Category))
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static IEnumerable<StageInfo> ExtractStages(string source)
    {
        foreach (Match m in ClassPattern().Matches(source))
        {
            var bases = m.Groups["bases"].Value;
            if (!_stageBases.Any(b => bases.Contains(b, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return new StageInfo(m.Groups["name"].Value + m.Groups["generics"].Value, Classify(bases));
        }
    }

    public static string Classify(string bases)
    {
        if (bases.Contains("FanInShape", StringComparison.Ordinal))
        {
            return "Fan-In";
        }

        if (bases.Contains("FanOutShape", StringComparison.Ordinal))
        {
            return "Fan-Out";
        }

        if (bases.Contains("SourceShape<", StringComparison.Ordinal))
        {
            return "Source";
        }

        if (bases.Contains("SinkShape<", StringComparison.Ordinal))
        {
            return "Sink";
        }

        if (bases.Contains("FlowShape<", StringComparison.Ordinal) ||
            bases.Contains("SimpleLinearGraphStage<", StringComparison.Ordinal) ||
            bases.Contains("PushStage<", StringComparison.Ordinal) ||
            bases.Contains("PushPullStage<", StringComparison.Ordinal))
        {
            return "Flow";
        }

        return "Custom";
    }

    public static string Format(IReadOnlyList<StageInfo> stages, string? filter)
    {
        if (stages.Count == 0)
        {
            return string.IsNullOrEmpty(filter)
                ? "No stream stages found."
                : $"No stream stages found matching filter: {filter}";
        }

        var sb = new StringBuilder();
        foreach (var group in stages.GroupBy(s => s.Category))
        {
            sb.AppendLine($"{group.Key}:");
            foreach (var stage in group)
            {
                sb.AppendLine($"  {stage.Name}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int CategoryIndex(string category)
    {
        for (var i = 0; i < Categories.Count; i++)
        {
            if (Categories[i] == category)
            {
                return i;
            }
        }

        return Categories.Count;
    }

    private const string NoSource = "No Akka.NET source available.";
    private static readonly string[] _attributeBases = ["IAttribute"];

    [GeneratedRegex(@"^\s*public\s+(?<sig>[^;{=]*\([^)]*\))", RegexOptions.NonBacktracking)]
    private static partial Regex PublicMethodPattern();

    [GeneratedRegex(@"^\s*(?:public|internal)\s+static\s+(?:readonly\s+)?[^=;\r\n]*\b(?:Default|Instance|None)\w*\s*(?:=\s*[^;\r\n]+;|\{[^}]*\}\s*=\s*[^;\r\n]+;)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex DefaultPattern();

    [GeneratedRegex(@"\bclass\s+GraphDsl\b|\bclass\s+Builder\b", RegexOptions.NonBacktracking)]
    private static partial Regex GraphDslClassPattern();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.NonBacktracking)]
    private static partial Regex XmlTag();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex Whitespace();

    [McpServerTool(Name = "stage_detail"), Description("Get the full source of a specific Akka.Streams stage by class name.")]
    public async Task<string> StageDetailAsync(
        [Description("Stage class name (e.g. Merge, Broadcast, Buffer)")] string stage,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatStageDetail(roots, stage);
    }

    [McpServerTool(Name = "graph_dsl_operators"), Description("List GraphDSL builder operators and their signatures from Akka.Streams source.")]
    public async Task<string> GraphDslOperatorsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatGraphDsl(roots);
    }

    [McpServerTool(Name = "stream_attributes"), Description("Catalog configurable Akka.Streams attributes with their defaults.")]
    public async Task<string> StreamAttributesAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatAttributes(roots);
    }

    public string FormatStageDetail(IReadOnlyList<string> roots, string stage)
    {
        var results = TypeTools.FindType(_fileService, roots, stage, fullSource: true)
            .Where(r => r.File.Contains("Akka.Streams", StringComparison.OrdinalIgnoreCase) ||
                        r.Source.Contains("namespace Akka.Streams", StringComparison.Ordinal))
            .ToList();

        if (results.Count == 0)
        {
            return $"Stage '{stage}' not found in Akka.Streams source.";
        }

        var sb = new StringBuilder();
        foreach (var r in results)
        {
            sb.AppendLine($"--- {r.File} ---");
            sb.AppendLine(r.Source);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string FormatGraphDsl(IReadOnlyList<string> roots)
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var file in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!file.Relative.Contains("Akka.Streams", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = _fileService.ReadAllText(file.Full);
            if (!GraphDslClassPattern().IsMatch(content))
            {
                continue;
            }

            var doc = new List<string>();
            var header = false;
            foreach (var raw in content.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("///", StringComparison.Ordinal))
                {
                    var text = XmlTag().Replace(line[3..], "").Trim();
                    if (text.Length > 0)
                    {
                        doc.Add(text);
                    }

                    continue;
                }

                var m = PublicMethodPattern().Match(line);
                if (m.Success && !line.Contains(" class ", StringComparison.Ordinal))
                {
                    if (!header)
                    {
                        sb.AppendLine($"--- {file.Relative} ---");
                        header = true;
                    }

                    var sig = Whitespace().Replace(m.Groups["sig"].Value.Trim(), " ");
                    sb.AppendLine(doc.Count > 0 ? $"  {sig}  // {doc[0]}" : $"  {sig}");
                    count++;
                }

                if (line.Length > 0 && !line.StartsWith('[') && !line.StartsWith("//", StringComparison.Ordinal))
                {
                    doc.Clear();
                }
            }

            if (header)
            {
                sb.AppendLine();
            }
        }

        return count == 0 ? "No GraphDSL operators found in Akka.Streams source." : $"Found {count} GraphDSL operator(s):\n\n{sb}";
    }

    public string FormatAttributes(IReadOnlyList<string> roots)
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var file in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            var content = _fileService.ReadAllText(file.Full);
            var attrs = ClassPattern().Matches(content)
                .Where(m => HasBase(m.Groups["bases"].Value, _attributeBases))
                .Select(m => m.Groups["name"].Value)
                .ToList();
            if (attrs.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"--- {file.Relative} ---");
            foreach (var a in attrs)
            {
                sb.AppendLine($"  {a}");
                count++;
            }

            foreach (Match d in DefaultPattern().Matches(content))
            {
                sb.AppendLine($"    default: {Whitespace().Replace(d.Value.Trim(), " ")}");
            }

            sb.AppendLine();
        }

        return count == 0 ? "No stream attributes found." : $"Found {count} attribute(s):\n\n{sb}";
    }

    private static bool HasBase(string bases, string[] names)
    {
        foreach (var part in bases.Split(','))
        {
            var name = part.Trim();
            var lt = name.IndexOf('<');
            if (lt >= 0)
            {
                name = name[..lt];
            }

            if (names.Contains(name[(name.LastIndexOf('.') + 1)..]))
            {
                return true;
            }
        }

        return false;
    }
}
