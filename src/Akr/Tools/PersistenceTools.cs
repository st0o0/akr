using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class PersistenceTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;

    public PersistenceTools(FileService fileService, SourcePipeline sourcePipeline)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
    }

    private const string NoSource = "No Akka.NET source available.";
    private static readonly string[] _journalBases = ["AsyncWriteJournal", "WriteJournalBase", "SyncWriteJournal"];
    private static readonly string[] _snapshotBases = ["SnapshotStore"];
    private static readonly string[] _readJournalBases = ["IReadJournalProvider", "ReadJournalBase", "IReadJournal"];
    private static readonly string[] _adapterBases = ["IEventAdapter", "IWriteEventAdapter", "IReadEventAdapter"];

    [GeneratedRegex(@"^[ \t]*(?<mods>(?:[a-z]+[ \t]+)*)class\s+(?<name>\w+)\s*(?:<[^>\r\n]*>)?\s*:\s*(?<bases>[^\r\n{]+)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"(?:journal|snapshot-store)\.(?<name>[\w-]+)\s*\{", RegexOptions.NonBacktracking)]
    private static partial Regex ConfigSectionPattern();

    [GeneratedRegex(@"class\s*=\s*""(?<class>[^""]+)""", RegexOptions.NonBacktracking)]
    private static partial Regex ConfigClassPattern();

    [GeneratedRegex(@"^\s*public\s+(?<sig>[^;{=]*\([^)]*\))", RegexOptions.NonBacktracking)]
    private static partial Regex PublicMethodPattern();

    [GeneratedRegex(@"event-adapters\s*\{[^}]*\}|event-adapter-bindings\s*\{[^}]*\}", RegexOptions.NonBacktracking)]
    private static partial Regex AdapterConfigPattern();

    [GeneratedRegex(@"\bI\w*Query\b", RegexOptions.NonBacktracking)]
    private static partial Regex QueryTypePattern();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.NonBacktracking)]
    private static partial Regex XmlTag();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex Whitespace();

    [McpServerTool(Name = "journal_plugins"), Description("Catalog Akka.Persistence journal plugins with file paths and config sections.")]
    public async Task<string> JournalPluginsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatPlugins("Journal plugins", FindPlugins(roots, _journalBases, "journal"));
    }

    [McpServerTool(Name = "snapshot_plugins"), Description("Catalog Akka.Persistence snapshot store plugins with file paths and config sections.")]
    public async Task<string> SnapshotPluginsAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatPlugins("Snapshot store plugins", FindPlugins(roots, _snapshotBases, "snapshot-store"));
    }

    [McpServerTool(Name = "persistence_query"), Description("List Akka.Persistence.Query ReadJournal implementations and their supported query types.")]
    public async Task<string> PersistenceQueryAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatReadJournals(roots);
    }

    [McpServerTool(Name = "event_adapter_chain"), Description("List event adapter implementations and event adapter configuration.")]
    public async Task<string> EventAdapterChainAsync(
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : FormatAdapters(roots);
    }

    public IReadOnlyList<ClassInfo> FindClasses(IReadOnlyList<string> roots, string[] baseNames)
    {
        var list = new List<ClassInfo>();
        foreach (var file in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            foreach (Match m in ClassPattern().Matches(_fileService.ReadAllText(file.Full)))
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

    public IReadOnlyList<PluginInfo> FindPlugins(IReadOnlyList<string> roots, string[] baseNames, string kind)
    {
        var sections = new List<(string Name, string Class)>();
        foreach (var file in _fileService.EnumerateFiles(roots, "*.conf"))
        {
            var text = _fileService.ReadAllText(file.Full);
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

    public string FormatReadJournals(IReadOnlyList<string> roots)
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

    public string FormatAdapters(IReadOnlyList<string> roots)
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

        foreach (var file in _fileService.EnumerateFiles(roots, "*.conf"))
        {
            foreach (Match m in AdapterConfigPattern().Matches(_fileService.ReadAllText(file.Full)))
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
}
