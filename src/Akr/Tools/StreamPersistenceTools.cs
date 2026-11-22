using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Akr.Tools;

public sealed record ClassInfo(string Name, string File, string Bases, bool IsAbstract);

public sealed record PluginInfo(string Name, string File, string Section);

[McpServerToolType]
public sealed partial class StreamPersistenceTools
{
    private const string NoSource = "No Akka.NET source available.";

    private static readonly string[] _journalBases = ["AsyncWriteJournal", "WriteJournalBase", "SyncWriteJournal"];
    private static readonly string[] _snapshotBases = ["SnapshotStore"];
    private static readonly string[] _readJournalBases = ["IReadJournalProvider", "ReadJournalBase", "IReadJournal"];
    private static readonly string[] _adapterBases = ["IEventAdapter", "IWriteEventAdapter", "IReadEventAdapter"];
    private static readonly string[] _attributeBases = ["IAttribute"];

    [GeneratedRegex(@"^[ \t]*(?<mods>(?:[a-z]+[ \t]+)*)class\s+(?<name>\w+)\s*(?:<[^>\r\n]*>)?\s*:\s*(?<bases>[^\r\n{]+)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"(?:journal|snapshot-store)\.(?<name>[\w-]+)\s*\{", RegexOptions.NonBacktracking)]
    private static partial Regex ConfigSectionPattern();

    [GeneratedRegex(@"class\s*=\s*""(?<class>[^""]+)""", RegexOptions.NonBacktracking)]
    private static partial Regex ConfigClassPattern();

    [GeneratedRegex(@"^\s*public\s+(?<sig>[^;{=]*\([^)]*\))", RegexOptions.NonBacktracking)]
    private static partial Regex PublicMethodPattern();

    [GeneratedRegex(@"^\s*(?:public|internal)\s+static\s+(?:readonly\s+)?[^=;\r\n]*\b(?:Default|Instance|None)\w*\s*(?:=\s*[^;\r\n]+;|\{[^}]*\}\s*=\s*[^;\r\n]+;)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex DefaultPattern();

    [GeneratedRegex(@"\bclass\s+GraphDsl\b|\bclass\s+Builder\b", RegexOptions.NonBacktracking)]
    private static partial Regex GraphDslClassPattern();

    [GeneratedRegex(@"event-adapters\s*\{[^}]*\}|event-adapter-bindings\s*\{[^}]*\}", RegexOptions.NonBacktracking)]
    private static partial Regex AdapterConfigPattern();

    [GeneratedRegex(@"\bI\w*Query\b", RegexOptions.NonBacktracking)]
    private static partial Regex QueryTypePattern();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.NonBacktracking)]
    private static partial Regex XmlTag();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex Whitespace();

    [McpServerTool(Name = "stage_detail"), Description("Get the full source of a specific Akka.Streams stage by class name.")]
    public static async Task<string> StageDetailAsync(
        GitHubClient github,
        [Description("Stage class name (e.g. Merge, Broadcast, Buffer)")] string stage,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatStageDetail(roots, stage);
    }

    [McpServerTool(Name = "graph_dsl_operators"), Description("List GraphDSL builder operators and their signatures from Akka.Streams source.")]
    public static async Task<string> GraphDslOperatorsAsync(
        GitHubClient github,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatGraphDsl(roots);
    }

    [McpServerTool(Name = "stream_attributes"), Description("Catalog configurable Akka.Streams attributes with their defaults.")]
    public static async Task<string> StreamAttributesAsync(
        GitHubClient github,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatAttributes(roots);
    }

    [McpServerTool(Name = "journal_plugins"), Description("Catalog Akka.Persistence journal plugins with file paths and config sections.")]
    public static async Task<string> JournalPluginsAsync(
        GitHubClient github,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatPlugins("Journal plugins", FindPlugins(roots, _journalBases, "journal"));
    }

    [McpServerTool(Name = "snapshot_plugins"), Description("Catalog Akka.Persistence snapshot store plugins with file paths and config sections.")]
    public static async Task<string> SnapshotPluginsAsync(
        GitHubClient github,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatPlugins("Snapshot store plugins", FindPlugins(roots, _snapshotBases, "snapshot-store"));
    }

    [McpServerTool(Name = "persistence_query"), Description("List Akka.Persistence.Query ReadJournal implementations and their supported query types.")]
    public static async Task<string> PersistenceQueryAsync(
        GitHubClient github,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatReadJournals(roots);
    }

    [McpServerTool(Name = "event_adapter_chain"), Description("List event adapter implementations and event adapter configuration.")]
    public static async Task<string> EventAdapterChainAsync(
        GitHubClient github,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        return roots.Count == 0 ? NoSource : FormatAdapters(roots);
    }

    public static string FormatStageDetail(IReadOnlyList<string> roots, string stage)
    {
        var results = TypeTools.FindType(roots, stage, fullSource: true)
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

    public static string FormatGraphDsl(IReadOnlyList<string> roots)
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var file in EnumerateFiles(roots, "*.cs"))
        {
            if (!file.Relative.Contains("Akka.Streams", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(file.Path);
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

    public static string FormatAttributes(IReadOnlyList<string> roots)
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var file in EnumerateFiles(roots, "*.cs"))
        {
            var content = File.ReadAllText(file.Path);
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

    public static IReadOnlyList<ClassInfo> FindClasses(IReadOnlyList<string> roots, string[] baseNames)
    {
        var list = new List<ClassInfo>();
        foreach (var file in EnumerateFiles(roots, "*.cs"))
        {
            foreach (Match m in ClassPattern().Matches(File.ReadAllText(file.Path)))
            {
                var bases = m.Groups["bases"].Value.Trim();
                if (HasBase(bases, baseNames))
                {
                    var isAbstract = m.Groups["mods"].Value.Contains("abstract", StringComparison.Ordinal);
                    list.Add(new ClassInfo(m.Groups["name"].Value, file.Relative, bases, isAbstract));
                }
            }
        }

        return list;
    }

    public static IReadOnlyList<PluginInfo> FindPlugins(IReadOnlyList<string> roots, string[] baseNames, string kind)
    {
        var sections = new List<(string Name, string Class)>();
        foreach (var file in EnumerateFiles(roots, "*.conf"))
        {
            var text = File.ReadAllText(file.Path);
            var matches = ConfigSectionPattern().Matches(text);
            for (var i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                if (!m.Value.StartsWith(kind, StringComparison.Ordinal))
                {
                    continue;
                }

                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var cls = ConfigClassPattern().Match(text[m.Index..end]);
                sections.Add((m.Groups["name"].Value, cls.Success ? cls.Groups["class"].Value : ""));
            }
        }

        var plugins = new List<PluginInfo>();
        foreach (var c in FindClasses(roots, baseNames).Where(c => !c.IsAbstract))
        {
            var match = sections.FirstOrDefault(s => SimpleClassName(s.Class) == c.Name);
            var section = match.Name is null ? "(none found)" : $"akka.persistence.{kind}.{match.Name}";
            plugins.Add(new PluginInfo(c.Name, c.File, section));
        }

        return plugins;
    }

    public static string FormatPlugins(string title, IReadOnlyList<PluginInfo> plugins)
    {
        if (plugins.Count == 0)
        {
            return $"No {title.ToLowerInvariant()} found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{title} ({plugins.Count}):");
        sb.AppendLine();
        foreach (var p in plugins)
        {
            sb.AppendLine(p.Name);
            sb.AppendLine($"  File: {p.File}");
            sb.AppendLine($"  Config: {p.Section}");
        }

        return sb.ToString();
    }

    public static string FormatReadJournals(IReadOnlyList<string> roots)
    {
        var classes = FindClasses(roots, _readJournalBases);
        if (classes.Count == 0)
        {
            return "No ReadJournal implementations found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"ReadJournal implementations ({classes.Count}):");
        sb.AppendLine();
        foreach (var c in classes)
        {
            sb.AppendLine(c.Name);
            sb.AppendLine($"  File: {c.File}");
            var queries = QueryTypePattern().Matches(c.Bases).Select(m => m.Value).Distinct().ToList();
            sb.AppendLine($"  Queries: {(queries.Count == 0 ? "(none declared)" : string.Join(", ", queries))}");
        }

        return sb.ToString();
    }

    public static string FormatAdapters(IReadOnlyList<string> roots)
    {
        var classes = FindClasses(roots, _adapterBases);
        var sb = new StringBuilder();
        if (classes.Count > 0)
        {
            sb.AppendLine($"Event adapters ({classes.Count}):");
            foreach (var c in classes)
            {
                sb.AppendLine($"{c.Name} : {c.Bases}");
                sb.AppendLine($"  File: {c.File}");
            }

            sb.AppendLine();
        }

        foreach (var file in EnumerateFiles(roots, "*.conf"))
        {
            foreach (Match m in AdapterConfigPattern().Matches(File.ReadAllText(file.Path)))
            {
                sb.AppendLine($"--- {file.Relative} ---");
                sb.AppendLine(m.Value);
                sb.AppendLine();
            }
        }

        return sb.Length == 0 ? "No event adapters found." : sb.ToString();
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

    private static string SimpleClassName(string typeSpec)
    {
        var comma = typeSpec.IndexOf(',');
        var name = (comma >= 0 ? typeSpec[..comma] : typeSpec).Trim();
        return name[(name.LastIndexOf('.') + 1)..];
    }

    private static IEnumerable<(string Path, string Relative)> EnumerateFiles(IReadOnlyList<string> roots, string pattern)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            {
                yield return (file, Path.GetRelativePath(root, file).Replace('\\', '/'));
            }
        }
    }
}
