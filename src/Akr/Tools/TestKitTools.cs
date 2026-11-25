using System.ComponentModel;
using System.Text;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed class TestKitTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;

    public TestKitTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

    [McpServerTool(Name = "testkit_helpers"), Description("Catalog public utility and assertion methods in Akka TestKit source, grouped by purpose.")]
    public async Task<string> TestKitHelpersAsync(
        [Description("Optional method name filter")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return OutputLimit.Truncate(FindTestKitHelpers(roots, filter));
    }

    public string FindTestKitHelpers(IEnumerable<string> roots, string? filter = null)
    {
        var groups = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (TypeScanner.ClassifyTestKit(relative) is null)
            {
                continue;
            }

            var currentType = _fileService.GetFileNameWithoutExtension(relative);
            foreach (var line in _fileService.ReadAllLines(full))
            {
                var t = TypeScanner.PublicTypePattern().Match(line);
                if (t.Success)
                {
                    currentType = t.Groups[1].Value;
                    continue;
                }

                var m = TypeScanner.PublicMethodNamePattern().Match(line);
                if (!m.Success)
                {
                    continue;
                }

                var method = m.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(filter) && !method.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var category = CategorizeHelper(method);
                if (!groups.TryGetValue(category, out var set))
                {
                    groups[category] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                set.Add($"{currentType}.{method}");
            }
        }

        if (groups.Count == 0)
        {
            return "No TestKit helpers found.";
        }

        var sb = new StringBuilder();
        foreach (var (category, methods) in groups)
        {
            sb.AppendLine($"## {category}");
            foreach (var method in methods)
            {
                sb.AppendLine($"  {method}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string CategorizeHelper(string method) => method switch
    {
        _ when method.StartsWith("Expect", StringComparison.Ordinal) => "Expect (message assertions)",
        _ when method.StartsWith("Assert", StringComparison.Ordinal) => "Assert",
        _ when method.StartsWith("Await", StringComparison.Ordinal) => "Await (polling and waiting)",
        _ when method.StartsWith("Within", StringComparison.Ordinal)
            || method.StartsWith("Intercept", StringComparison.Ordinal) => "Timing and interception",
        _ when method.StartsWith("Create", StringComparison.Ordinal)
            || method.StartsWith("Child", StringComparison.Ordinal) => "Creation",
        _ when method.StartsWith("Watch", StringComparison.Ordinal)
            || method.StartsWith("Unwatch", StringComparison.Ordinal) => "Death watch",
        _ => "Other"
    };
}
