using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class HostingTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;
    private readonly HoconScanner _hoconScanner;

    public HostingTools(FileService fileService, SourcePipeline sourcePipeline, HoconScanner hoconScanner)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
        _hoconScanner = hoconScanner;
    }

    [McpServerTool(Name = "hosting_extensions"), Description("Search for With*/Add* extension methods in Akka.Hosting source.")]
    public async Task<string> SearchExtensionsAsync(
        [Description("Optional keyword to filter by file path (e.g. Persistence, Cluster)")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return Format(FindExtensions(roots, filter), filter);
    }

    public static string Format(IReadOnlyList<ExtensionFileResult> results, string? filter)
    {
        if (results.Count == 0)
        {
            return string.IsNullOrEmpty(filter)
                ? "No hosting extensions found."
                : $"No hosting extensions found matching filter: {filter}";
        }

        var sb = new StringBuilder();
        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.File} ---");
            foreach (var sig in result.Signatures)
            {
                sb.AppendLine($"  {sig}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"public\s+static\s+\S+\s+(With|Add)\w+\s*[\(<]", RegexOptions.NonBacktracking)]
    private static partial Regex MethodPattern();

    public IReadOnlyList<ExtensionFileResult> FindExtensions(IEnumerable<string> roots, string? filter = null)
    {
        var results = new List<ExtensionFileResult>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!string.IsNullOrEmpty(filter) && !relative.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var signatures = ExtractSignatures(_fileService.ReadAllLines(full));
            if (signatures.Count > 0)
            {
                results.Add(new ExtensionFileResult(relative, signatures));
            }
        }

        return results.OrderBy(r => r.File, StringComparer.Ordinal).ToList();
    }

    public static List<string> ExtractSignatures(string[] lines)
    {
        var signatures = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!MethodPattern().IsMatch(lines[i]))
            {
                continue;
            }

            var sig = lines[i].Trim();
            var j = i;
            while (!IsTerminated(sig) && j + 1 < lines.Length && j - i < 20)
            {
                j++;
                sig += " " + lines[j].Trim();
            }

            sig = Truncate(sig);
            sig = Regex.Replace(sig, @"\s+", " ", RegexOptions.NonBacktracking).Trim();
            if (sig.StartsWith("public ", StringComparison.Ordinal))
            {
                sig = sig["public ".Length..];
            }

            signatures.Add(sig);
        }

        return signatures;
    }

    private static bool IsTerminated(string sig) => sig.Contains('{') || sig.Contains("=>") || sig.Contains(';');

    private static string Truncate(string sig)
    {
        var end = sig.Length;
        foreach (var marker in new[] { "{", "=>", ";" })
        {
            var idx = sig.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0 && idx < end)
            {
                end = idx;
            }
        }

        return sig[..end];
    }

    [GeneratedRegex(
        @"(must|should|needs? to|has to)\s+be\s+(called|registered|configured|invoked|set)|(before|after)\s+(calling|registering|configuring)|call(ed)?\s+(this\s+)?(before|after)|requires?\s+(that\s+)?\w*(With|Add)\w+|order\s+(matters|of)",
        RegexOptions.NonBacktracking | RegexOptions.IgnoreCase)]
    private static partial Regex OrderingPattern();

    [GeneratedRegex(@"GetRequiredService<|GetService<|ActorRegistry|registry\.(Try)?Get<|DependencyResolver",
        RegexOptions.NonBacktracking)]
    private static partial Regex DiHintPattern();

    private static readonly Dictionary<string, string[]> _fallbackHostingMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WithClustering"] = ["akka.cluster"],
        ["WithRemoting"] = ["akka.remote"],
        ["WithDistributedData"] = ["akka.cluster.distributed-data"],
        ["WithSingleton"] = ["akka.cluster.singleton"],
        ["WithShardRegion"] = ["akka.cluster.sharding"],
        ["WithDistributedPubSub"] = ["akka.cluster.pub-sub"],
        ["WithJournal"] = ["akka.persistence.journal"],
        ["WithSnapshotStore"] = ["akka.persistence.snapshot-store"],
        ["WithCoordinatedShutdown"] = ["akka.coordinated-shutdown"],
    };

    private static readonly string[] _recommendedOrder =
    [
        "1. Remote / transport (WithRemoting)",
        "2. Cluster (WithClustering, WithClusterBootstrap, Akka.Management)",
        "3. Persistence (WithJournal, WithSnapshotStore, WithSqlPersistence)",
        "4. Sharding / Singleton / Distributed PubSub (WithShardRegion, WithSingleton, WithDistributedPubSub)",
        "5. Actors (WithActors) - last, so ActorRegistry dependencies from earlier steps are available",
    ];

    [McpServerTool(Name = "hosting_to_hocon"),
     Description("Bidirectional mapping between Akka.Hosting With*/Add* methods and HOCON config keys.")]
    public async Task<string> HostingToHoconAsync(
        [Description("Hosting method (e.g. WithClustering) or HOCON path (e.g. akka.cluster.roles)")]
        string query,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")]
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return MapQuery(roots, query);
    }

    [McpServerTool(Name = "hosting_startup_order"),
     Description("Registration ordering constraints and DI resolution hints from Akka.Hosting source.")]
    public async Task<string> StartupOrderAsync(
        [Description("Optional keyword to filter by file path")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")]
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindStartupOrder(roots, filter);
    }

    public string MapQuery(IEnumerable<string> roots, string query)
    {
        var rootList = roots.ToList();
        var scannedMap = _hoconScanner.CollectHostingKeys(rootList);
        var merged = MergeWithFallback(scannedMap);
        var q = query.Trim();
        var sb = new StringBuilder();

        if (q.StartsWith("akka", StringComparison.Ordinal))
        {
            var config = _hoconScanner.CollectConfig(rootList);
            sb.AppendLine("| HOCON path | Hosting method |");
            sb.AppendLine("|---|---|");
            var rows = 0;
            foreach (var (method, keys) in merged.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                foreach (var key in keys.Where(k => HoconParser.PathMatches(k, q)))
                {
                    var inRef = config.Keys.Any(c => c == key || c.StartsWith(key + ".", StringComparison.Ordinal))
                        ? string.Empty
                        : " (not in reference.conf)";
                    sb.AppendLine($"| {key}{inRef} | {method} |");
                    rows++;
                }
            }

            return rows == 0 ? $"No Hosting method found for HOCON path: {q}" : sb.ToString();
        }

        sb.AppendLine("| Hosting method | HOCON keys |");
        sb.AppendLine("|---|---|");
        var found = 0;
        foreach (var (method, keys) in merged.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!method.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var curatedOnly = !scannedMap.ContainsKey(method);
            var label = curatedOnly ? " (curated)" : "";
            sb.AppendLine($"| {method} | {string.Join(", ", keys)}{label} |");
            found++;
        }

        return found == 0 ? $"No HOCON keys found for Hosting method: {q}" : sb.ToString();
    }

    private static Dictionary<string, SortedSet<string>> MergeWithFallback(
        Dictionary<string, SortedSet<string>> scanned)
    {
        var merged = new Dictionary<string, SortedSet<string>>(scanned, StringComparer.Ordinal);
        foreach (var (method, keys) in _fallbackHostingMap)
        {
            if (!merged.TryGetValue(method, out var set))
            {
                set = new SortedSet<string>(StringComparer.Ordinal);
                merged[method] = set;
            }

            foreach (var key in keys)
            {
                set.Add(key);
            }
        }

        return merged;
    }

    // ---- startup order ----

    public string FindStartupOrder(IEnumerable<string> roots, string? filter)
    {
        var constraints = new List<string>();
        var hints = new List<string>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!string.IsNullOrEmpty(filter) && !relative.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = _fileService.ReadAllLines(full);
            var isHosting = relative.Contains("Hosting", StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                var isComment = t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith('*');
                if (isComment && OrderingPattern().IsMatch(t))
                {
                    constraints.Add($"{relative}:{i + 1}: {t.TrimStart('/', '*', ' ')}");
                }
                else if (isHosting && !isComment && hints.Count < 50 && DiHintPattern().IsMatch(t))
                {
                    hints.Add($"{relative}:{i + 1}: {t}");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("Recommended registration order:");
        foreach (var step in _recommendedOrder)
        {
            sb.AppendLine($"  {step}");
        }

        sb.AppendLine();
        sb.AppendLine(constraints.Count == 0 ? "No ordering comments found." : "Ordering constraints found in source:");
        foreach (var c in constraints.Take(50))
        {
            sb.AppendLine($"  {c}");
        }

        if (hints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DI resolution hints:");
            foreach (var h in hints)
            {
                sb.AppendLine($"  {h}");
            }
        }

        return sb.ToString();
    }

    private const int MaxMatchesPerCategory = 15;

    private static readonly (string Title, Regex Pattern)[] _diCategories =
    [
        ("Resolve actors via DI (IRequiredActor<T>)", new Regex(@"IRequiredActor<", RegexOptions.NonBacktracking)),
        ("ActorRegistry", new Regex(@"\bActorRegistry\b", RegexOptions.NonBacktracking)),
        ("Register services and actors", new Regex(@"\b(?:WithActors|AddAkka|WithActorRegistry)\b", RegexOptions.NonBacktracking)),
        ("Props with DI", new Regex(@"DependencyResolver|\bPropsFor\b|GetRequiredService|\bProps<|\.Props\(", RegexOptions.NonBacktracking))
    ];

    // ---------- hosting_di_patterns ----------

    [McpServerTool(Name = "hosting_di_patterns"), Description("Catalog of dependency injection patterns in Akka.Hosting source (IRequiredActor, ActorRegistry, Props with DI).")]
    public async Task<string> HostingDiPatternsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return FindDiPatterns(roots);
    }

    public string FindDiPatterns(IEnumerable<string> roots)
    {
        var buckets = _diCategories.Select(_ => new List<string>()).ToArray();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            if (!relative.Contains("Hosting", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("TestKit", StringComparison.OrdinalIgnoreCase)
                || relative.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = _fileService.ReadAllLines(full);
            for (var i = 0; i < lines.Length; i++)
            {
                for (var c = 0; c < _diCategories.Length; c++)
                {
                    if (buckets[c].Count < MaxMatchesPerCategory && _diCategories[c].Pattern.IsMatch(lines[i]))
                    {
                        var snippet = string.Join('\n', lines[i..Math.Min(lines.Length, i + 3)].Select(l => "    " + l.TrimEnd()));
                        buckets[c].Add($"  {relative}:{i + 1}\n{snippet}");
                    }
                }
            }
        }

        if (buckets.All(b => b.Count == 0))
        {
            return "No Akka.Hosting DI patterns found.";
        }

        var sb = new StringBuilder();
        for (var c = 0; c < _diCategories.Length; c++)
        {
            if (buckets[c].Count == 0)
            {
                continue;
            }

            sb.AppendLine($"## {_diCategories[c].Title}");
            foreach (var entry in buckets[c])
            {
                sb.AppendLine(entry);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ---------- hosting_testkit ----------

    [McpServerTool(Name = "hosting_testkit"), Description("Compare Akka.TestKit and Akka.Hosting.TestKit: base classes and assertion methods.")]
    public async Task<string> HostingTestKitAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return CompareTestKits(roots);
    }

    public string CompareTestKits(IEnumerable<string> roots)
    {
        var rows = new List<(string Package, string Class, string Base, string Assertions)>();

        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            var package = TypeScanner.ClassifyTestKit(relative);
            if (package is null)
            {
                continue;
            }

            var text = _fileService.ReadAllText(full);
            var assertions = TypeScanner.PublicMethodNamePattern().Matches(text)
                .Select(m => m.Groups[1].Value)
                .Where(n => n.StartsWith("Expect", StringComparison.Ordinal)
                    || n.StartsWith("Assert", StringComparison.Ordinal)
                    || n.StartsWith("Await", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            foreach (Match m in TypeScanner.PublicClassPattern().Matches(text))
            {
                var baseType = m.Groups[2].Success && m.Groups[2].Value.Trim().Length > 0
                    ? TypeScanner.WhitespacePattern().Replace(m.Groups[2].Value, " ").Trim()
                    : "-";
                rows.Add((package, m.Groups[1].Value, baseType, assertions.Count == 0 ? "-" : string.Join(", ", assertions)));
            }
        }

        if (rows.Count == 0)
        {
            return "No TestKit classes found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("| Package | Class | Base | Assertion methods |");
        sb.AppendLine("|---------|-------|------|-------------------|");
        foreach (var r in rows.OrderBy(r => r.Package, StringComparer.Ordinal).ThenBy(r => r.Class, StringComparer.Ordinal))
        {
            sb.AppendLine($"| {r.Package} | {r.Class} | {r.Base} | {r.Assertions} |");
        }

        return sb.ToString();
    }

}
