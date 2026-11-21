using System.Text;
using System.Text.RegularExpressions;

namespace Akr.Tools;

public sealed record StageInfo(string Name, string Category);

public static class StageFinder
{
    public static readonly IReadOnlyList<string> Categories = ["Source", "Flow", "Sink", "Fan-In", "Fan-Out", "Custom"];

    private static readonly Regex _classPattern = new(
        @"\bclass\s+(?<name>\w+)\s*(?<generics><[^>\r\n]*>)?\s*:\s*(?<bases>[^\r\n{]+)",
        RegexOptions.NonBacktracking);

    private static readonly string[] _stageBases =
    [
        "GraphStage<",
        "GraphStageWithMaterializedValue<",
        "SimpleLinearGraphStage<",
        "PushStage<",
        "PushPullStage<",
    ];

    public static IReadOnlyList<StageInfo> FindStages(IEnumerable<string> roots, string? filter = null)
    {
        var found = new HashSet<StageInfo>();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!relative.Contains("Akka.Streams", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var stage in ExtractStages(File.ReadAllText(file)))
                {
                    found.Add(stage);
                }
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
        foreach (Match m in _classPattern.Matches(source))
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
}
