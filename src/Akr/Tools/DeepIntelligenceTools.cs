using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Analysis;
using Akr.Models;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class DeepIntelligenceTools
{
    private const string NoSource = "No Akka.NET source available.";
    private const int MaxWindowLines = 25;

    private static readonly string[] _lifecycle = ["PreStart", "PreRestart", "PostRestart", "PostStop", "AroundReceive", "Receive"];
    private static readonly string[] _requestSuffixes = ["Command", "Query"];
    private static readonly string[] _responseSuffixes = ["Response", "Result"];
    private const string EventSuffix = "Event";

    private readonly FileService _fileService;
    private readonly TypeScanner _scanner;

    public DeepIntelligenceTools(FileService fileService, TypeScanner scanner)
    {
        _fileService = fileService;
        _scanner = scanner;
    }

    [McpServerTool(Name = "type_hierarchy"), Description("Show the full inheritance tree of a C# type: ancestors (base types and interfaces) and descendants.")]
    public async Task<string> TypeHierarchyAsync(
        GitHubClient github,
        [Description("Type name (e.g. ActorBase, ReceiveActor, IActorRef)")] string type,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Hierarchy(roots, type);
    }

    [McpServerTool(Name = "find_implementations"), Description("Find all classes implementing an interface, including classes deriving from an implementing class.")]
    public async Task<string> FindImplementationsAsync(
        GitHubClient github,
        [Description("Interface name (e.g. IActorRef, IWithTimers)")] string @interface,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Implementations(roots, @interface);
    }

    [McpServerTool(Name = "find_messages"), Description("List message types handled by an actor via Receive<T>, ReceiveAsync<T>, Command<T> handlers.")]
    public async Task<string> FindMessagesAsync(
        GitHubClient github,
        [Description("Actor class name")] string actor,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Messages(roots, actor);
    }

    [McpServerTool(Name = "message_protocol"), Description("Summarize Command/Query/Event/Response/Result types in a namespace or actor, grouped into request-response pairs.")]
    public async Task<string> MessageProtocolAsync(
        GitHubClient github,
        [Description("Namespace fragment or actor/type name")] string scope,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Protocol(roots, scope);
    }

    [McpServerTool(Name = "virtual_methods"), Description("List virtual and abstract methods of a type, ordered by actor lifecycle (PreStart, PreRestart, PostRestart, PostStop, AroundReceive, Receive).")]
    public async Task<string> VirtualMethodsAsync(
        GitHubClient github,
        [Description("Type name (e.g. ActorBase, ReceiveActor)")] string type,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : VirtualMethods(roots, type);
    }

    [McpServerTool(Name = "supervision_defaults"), Description("Show supervision strategies: default strategy, or SupervisorStrategy overrides for a given type.")]
    public async Task<string> SupervisionDefaultsAsync(
        GitHubClient github,
        [Description("Type name, or 'default' (default) for the framework default strategy")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Supervision(roots, filter);
    }

    // ---- Regexes ----

    [GeneratedRegex(@"\b(?:Receive|ReceiveAsync|Command|CommandAsync)\s*<\s*(.+?)\s*>\s*\(")]
    private static partial Regex HandlerPattern();

    [GeneratedRegex(@"\b(Receive|ReceiveAsync|Command|CommandAsync)\s*<")]
    private static partial Regex HandlerKindPattern();

    [GeneratedRegex(@"^\s*((?:(?:public|protected|internal|private|virtual|abstract|override|sealed|async|unsafe|new|static)\s+)+)(.*)$")]
    private static partial Regex MethodLine();

    [GeneratedRegex(@"(\w+)\s*(?:<[^>(]*>)?\s*\(")]
    private static partial Regex MethodName();

    [GeneratedRegex(@"\boverride\s+SupervisorStrategy\s+SupervisorStrategy\b")]
    private static partial Regex StrategyOverride();

    [GeneratedRegex(@"\b(?:DefaultStrategy|DefaultDecider)\s*(?:=|\{|\()")]
    private static partial Regex StrategyDefault();

    [GeneratedRegex(@"\b(OneForOneStrategy|AllForOneStrategy)\s*\(")]
    private static partial Regex StrategyCtor();

    [GeneratedRegex(@"\bDirective\.(\w+)")]
    private static partial Regex DirectivePattern();

    // ---- type_hierarchy ----

    public string Hierarchy(IReadOnlyList<string> roots, string type)
    {
        var name = TypeScanner.BaseName(type);
        var all = _scanner.ScanTypes(roots);
        var decls = all.Where(t => t.Name == name).ToList();
        if (decls.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{name} ({decls[0].Kind}) - {decls[0].Source.Path}");
        sb.AppendLine();
        sb.AppendLine("Ancestors:");
        var before = sb.Length;
        AppendAncestors(sb, all, decls, 1, [name]);
        if (sb.Length == before)
        {
            sb.AppendLine("  (none)");
        }

        sb.AppendLine();
        sb.AppendLine("Descendants:");
        before = sb.Length;
        AppendDescendants(sb, all, name, 1, [name]);
        if (sb.Length == before)
        {
            sb.AppendLine("  (none)");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendAncestors(StringBuilder sb, List<TypeDecl> all, IEnumerable<TypeDecl> decls, int depth, HashSet<string> visited)
    {
        foreach (var b in decls.SelectMany(d => d.Bases).Distinct())
        {
            var baseName = TypeScanner.BaseName(b);
            var found = all.Where(t => t.Name == baseName).ToList();
            var info = found.Count > 0 ? $" ({found[0].Kind}) - {found[0].Source.Path}" : " (external)";
            sb.AppendLine($"{new string(' ', depth * 2)}-> {b}{info}");
            if (found.Count > 0 && visited.Add(baseName))
            {
                AppendAncestors(sb, all, found, depth + 1, visited);
            }
        }
    }

    private static void AppendDescendants(StringBuilder sb, List<TypeDecl> all, string name, int depth, HashSet<string> visited)
    {
        var children = all
            .Where(t => t.Bases.Any(b => TypeScanner.BaseName(b) == name))
            .GroupBy(t => t.Name)
            .Select(g => g.First());

        foreach (var child in children)
        {
            sb.AppendLine($"{new string(' ', depth * 2)}<- {child.Name} ({child.Kind}) - {child.Source.Path}");
            if (visited.Add(child.Name))
            {
                AppendDescendants(sb, all, child.Name, depth + 1, visited);
            }
        }
    }

    // ---- find_implementations ----

    public string Implementations(IReadOnlyList<string> roots, string @interface)
    {
        var name = TypeScanner.BaseName(@interface);
        var all = _scanner.ScanTypes(roots);

        var direct = all
            .Where(t => t.Kind != "interface" && t.Name != name && t.Bases.Any(b => TypeScanner.BaseName(b) == name))
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();
        var directNames = direct.Select(t => t.Name).ToHashSet();

        var indirect = all
            .Where(t => t.Kind != "interface" && !directNames.Contains(t.Name) && t.Bases.Any(b => directNames.Contains(TypeScanner.BaseName(b))))
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();

        if (direct.Count == 0 && indirect.Count == 0)
        {
            return $"No implementations of '{@interface}' found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Implementations of {name}: {direct.Count + indirect.Count}");
        sb.AppendLine();
        if (direct.Count > 0)
        {
            sb.AppendLine("Direct:");
            foreach (var t in direct)
            {
                sb.AppendLine($"  {t.Name} ({t.Kind}) - {t.Source.Path}");
            }
        }

        if (indirect.Count > 0)
        {
            if (direct.Count > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("Via base class:");
            foreach (var t in indirect)
            {
                var via = t.Bases.Select(TypeScanner.BaseName).First(directNames.Contains);
                sb.AppendLine($"  {t.Name} ({t.Kind}) via {via} - {t.Source.Path}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ---- find_messages ----

    public string Messages(IReadOnlyList<string> roots, string actor)
    {
        var name = TypeScanner.BaseName(actor);
        var decls = _scanner.ScanTypes(roots).Where(t => t.Name == name).ToList();
        if (decls.Count == 0)
        {
            return $"Actor '{actor}' not found in cached Akka.NET source.";
        }

        var handlers = new List<(string Kind, string Message)>();
        foreach (var decl in decls)
        {
            var body = TypeScanner.ExtractBody(decl.Source.Content, decl.Index);
            foreach (Match m in HandlerPattern().Matches(body))
            {
                var kind = HandlerKindPattern().Match(m.Value).Groups[1].Value;
                var entry = (kind, m.Groups[1].Value);
                if (!handlers.Contains(entry))
                {
                    handlers.Add(entry);
                }
            }
        }

        if (handlers.Count == 0)
        {
            return $"No Receive/Command handlers found in '{actor}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{name} ({decls[0].Source.Path}) handles {handlers.Count} message type(s):");
        foreach (var (kind, message) in handlers)
        {
            sb.AppendLine($"  {message}  [{kind}]");
        }

        return sb.ToString().TrimEnd();
    }

    // ---- message_protocol ----

    public string Protocol(IReadOnlyList<string> roots, string scope)
    {
        var all = _scanner.ScanTypes(roots);
        var scopedSources = all
            .Where(t => t.Namespace.Contains(scope, StringComparison.OrdinalIgnoreCase) || t.Name == TypeScanner.BaseName(scope))
            .Select(t => t.Source)
            .ToHashSet();

        var candidates = all
            .Where(t => scopedSources.Contains(t.Source))
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();

        var requests = candidates.Where(t => EndsWithAny(t.Name, _requestSuffixes)).ToList();
        var responses = candidates.Where(t => EndsWithAny(t.Name, _responseSuffixes)).ToList();
        var events = candidates.Where(t => t.Name.Length > EventSuffix.Length && t.Name.EndsWith(EventSuffix, StringComparison.Ordinal)).ToList();

        if (requests.Count == 0 && responses.Count == 0 && events.Count == 0)
        {
            return $"No Command/Query/Event/Response/Result types found for '{scope}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Protocol for '{scope}':");

        var paired = new HashSet<string>();
        if (requests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Requests:");
            foreach (var req in requests)
            {
                var prefix = StripSuffix(req.Name, _requestSuffixes);
                var matched = responses.Where(r => StripSuffix(r.Name, _responseSuffixes) == prefix).ToList();
                foreach (var r in matched)
                {
                    paired.Add(r.Name);
                }

                var target = matched.Count == 0 ? "(no response)" : string.Join(", ", matched.Select(r => r.Name));
                sb.AppendLine($"  {req.Name} -> {target}  - {req.Source.Path}");
            }
        }

        var unpaired = responses.Where(r => !paired.Contains(r.Name)).ToList();
        if (unpaired.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Responses without request:");
            foreach (var r in unpaired)
            {
                sb.AppendLine($"  {r.Name}  - {r.Source.Path}");
            }
        }

        if (events.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Events:");
            foreach (var e in events)
            {
                sb.AppendLine($"  {e.Name}  - {e.Source.Path}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static bool EndsWithAny(string name, string[] suffixes) =>
        suffixes.Any(s => name.Length > s.Length && name.EndsWith(s, StringComparison.Ordinal));

    private static string StripSuffix(string name, string[] suffixes)
    {
        var suffix = suffixes.First(s => name.EndsWith(s, StringComparison.Ordinal));
        return name[..^suffix.Length];
    }

    // ---- virtual_methods ----

    public string VirtualMethods(IReadOnlyList<string> roots, string type)
    {
        var name = TypeScanner.BaseName(type);
        var decls = _scanner.ScanTypes(roots).Where(t => t.Name == name).ToList();
        if (decls.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source.";
        }

        var methods = new List<(string Name, string Signature)>();
        foreach (var decl in decls)
        {
            foreach (var raw in TypeScanner.ExtractBody(decl.Source.Content, decl.Index).Split('\n'))
            {
                var m = MethodLine().Match(raw.TrimEnd('\r'));
                if (!m.Success)
                {
                    continue;
                }

                var modifiers = m.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var rest = m.Groups[2].Value.Trim();
                if (!modifiers.Contains("virtual") && !modifiers.Contains("abstract"))
                {
                    continue;
                }

                var nameMatch = MethodName().Match(rest);
                if (!nameMatch.Success || rest.StartsWith("class ", StringComparison.Ordinal) || rest.StartsWith("record ", StringComparison.Ordinal))
                {
                    continue;
                }

                var signature = $"{string.Join(' ', modifiers)} {rest}".TrimEnd('{', ';', ' ');
                methods.Add((nameMatch.Groups[1].Value, signature));
            }
        }

        if (methods.Count == 0)
        {
            return $"No virtual or abstract methods found in '{type}'.";
        }

        var ordered = methods
            .Select((m, i) => (m, i))
            .OrderBy(x => LifecycleRank(x.m.Name))
            .ThenBy(x => x.i)
            .Select(x => x.m)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Overridable methods of {name} (lifecycle order first):");
        var n = 1;
        foreach (var (methodName, signature) in ordered)
        {
            var tag = LifecycleRank(methodName) < _lifecycle.Length ? "  [lifecycle]" : "";
            sb.AppendLine($"  {n++}. {signature}{tag}");
        }

        return sb.ToString().TrimEnd();
    }

    private static int LifecycleRank(string method)
    {
        var idx = Array.IndexOf(_lifecycle, method);
        return idx < 0 ? _lifecycle.Length : idx;
    }

    // ---- supervision_defaults ----

    public IReadOnlyList<SupervisionInfo> FindSupervision(IReadOnlyList<string> roots)
    {
        var results = new List<SupervisionInfo>();
        foreach (var (full, relative) in _fileService.EnumerateFiles(roots, "*.cs"))
        {
            var content = _fileService.ReadAllText(full);
            var lines = content.Split('\n');
            var current = "";
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var decl = TypeScanner.DeclPattern().Match(line);
                if (decl.Success)
                {
                    current = decl.Groups[2].Value;
                }

                var kind = StrategyOverride().IsMatch(line) ? "override"
                    : StrategyDefault().IsMatch(line) ? "default"
                    : StrategyCtor().IsMatch(line) ? "usage"
                    : null;
                if (kind is null)
                {
                    continue;
                }

                var end = WindowEnd(lines, i);
                var text = string.Join('\n', lines[i..(end + 1)]);
                var strategy = StrategyCtor().Match(text) is { Success: true } sm ? sm.Groups[1].Value : "(unspecified)";
                var directives = DirectivePattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();
                results.Add(new SupervisionInfo(current, kind, strategy, directives, relative, i + 1));
                i = end;
            }
        }

        return results;
    }

    public string Supervision(IReadOnlyList<string> roots, string? filter)
    {
        var all = FindSupervision(roots);
        var isDefault = string.IsNullOrWhiteSpace(filter) || filter.Equals("default", StringComparison.OrdinalIgnoreCase);
        List<SupervisionInfo> selected = isDefault
            ? [.. all.Where(s => s.Kind == "default")]
            : [.. all.Where(s => s.Type == TypeScanner.BaseName(filter!))];

        if (selected.Count == 0)
        {
            return isDefault
                ? "No default supervision strategy found in cached Akka.NET source."
                : $"No SupervisorStrategy override or strategy usage found for '{filter}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine(isDefault ? "Default supervision strategy:" : $"Supervision in {filter}:");
        foreach (var s in selected)
        {
            var directives = s.Directives.Count == 0 ? "(none)" : string.Join(", ", s.Directives);
            sb.AppendLine($"  {s.Type} [{s.Kind}] {s.Strategy}");
            sb.AppendLine($"    Directives: {directives}");
            sb.AppendLine($"    {s.File}:{s.Line}");
        }

        return sb.ToString().TrimEnd();
    }

    private static int WindowEnd(string[] lines, int start)
    {
        var paren = 0;
        var brace = 0;
        var last = Math.Min(start + MaxWindowLines, lines.Length) - 1;
        for (var j = start; j <= last; j++)
        {
            foreach (var ch in lines[j])
            {
                switch (ch)
                {
                    case '(':
                        paren++;
                        break;
                    case ')':
                        paren--;
                        break;
                    case '{':
                        brace++;
                        break;
                    case '}':
                        brace--;
                        break;
                }
            }

            var t = lines[j].TrimEnd();
            if (paren <= 0 && brace <= 0 && (t.EndsWith(';') || t.EndsWith('}')))
            {
                return j;
            }
        }

        return last;
    }

}
