using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Akr.Models;
using Akr.Scanning;
using Akr.Source;
using ModelContextProtocol.Server;

namespace Akr.Tools;

[McpServerToolType]
public sealed partial class TypeTools
{
    private readonly FileService _fileService;
    private readonly SourcePipeline _sourcePipeline;
    private readonly TypeScanner _scanner;

    public TypeTools(FileService fileService, SourcePipeline sourcePipeline, TypeScanner scanner)
    {
        _fileService = fileService;
        _sourcePipeline = sourcePipeline;
        _scanner = scanner;
    }

    [McpServerTool(Name = "lookup_type"), Description("Find a C# type by name across cached Akka.NET source code and return its source.")]
    public async Task<string> LookupTypeAsync(
        [Description("Type name to look up (e.g. ActorBase, IActorRef, Props)")] string type,
        [Description("Return full file source instead of summary (default: false)")] bool? fullSource = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        var results = FindType(roots, type, fullSource ?? false);
        if (results.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source. Try search_source to find it by a different name.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} match(es) for type '{type}':");
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"--- {result.File} ---");
            if (!string.IsNullOrEmpty(result.Namespace))
            {
                sb.AppendLine($"Namespace: {result.Namespace}");
            }

            sb.AppendLine($"Declaration: {result.Declaration}");
            sb.AppendLine();
            sb.AppendLine(result.Source);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "type_members"), Description("Get compact member signatures for a C# type in Akka.NET source.")]
    public async Task<string> GetMembersAsync(
        [Description("Type name (e.g. ActorBase, Props, IActorRef)")] string type,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        var results = FindType(roots, type, fullSource: true);
        if (results.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source. Try search_source to find it by a different name.";
        }

        var members = results
            .SelectMany(r => Extract(r.Source, type))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(FormatHeader(results[0].Declaration, type));
        sb.AppendLine();
        sb.Append(members.Count == 0 ? "No members found." : Format(members));

        var parentNames = ExtractParentTypes(results[0].Declaration);
        foreach (var parent in parentNames)
        {
            var parentResults = FindType(roots, parent, fullSource: true);
            if (parentResults.Count == 0)
            {
                continue;
            }

            var parentMembers = parentResults
                .SelectMany(r => Extract(r.Source, parent))
                .ToList();

            if (parentMembers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine($"Inherited from {parent}:");
                sb.Append(Format(parentMembers));
            }
        }

        return sb.ToString();
    }

    private static readonly HashSet<string> _systemInterfaces = new(StringComparer.Ordinal)
    {
        "IEquatable", "IComparable", "ISurrogated", "IDisposable", "IAsyncDisposable",
        "ICloneable", "IFormattable", "IConvertible", "ISerializable",
    };

    internal static IReadOnlyList<string> ExtractParentTypes(string declaration)
    {
        var colon = declaration.IndexOf(':');
        if (colon < 0)
        {
            return [];
        }

        var rest = declaration[(colon + 1)..];
        var whereIdx = rest.IndexOf(" where ", StringComparison.Ordinal);
        if (whereIdx >= 0)
        {
            rest = rest[..whereIdx];
        }

        rest = rest.TrimEnd('{').Trim();

        var parents = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < rest.Length; i++)
        {
            var c = rest[i];
            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                AddIfValid(parents, rest[start..i].Trim());
                start = i + 1;
            }
        }

        AddIfValid(parents, rest[start..].Trim());
        return parents;

        static void AddIfValid(List<string> list, string name)
        {
            var genericIdx = name.IndexOf('<');
            var baseName = genericIdx >= 0 ? name[..genericIdx] : name;
            baseName = baseName.Trim();
            if (baseName.Length > 0 && !_systemInterfaces.Contains(baseName))
            {
                list.Add(baseName);
            }
        }
    }

    [GeneratedRegex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex NamespacePattern();

    public IReadOnlyList<TypeLookupResult> FindType(
        IReadOnlyList<string> sourceRoots,
        string typeName,
        bool fullSource = false) =>
        FindType(_fileService, sourceRoots, typeName, fullSource);

    public static IReadOnlyList<TypeLookupResult> FindType(
        FileService fileService,
        IReadOnlyList<string> sourceRoots,
        string typeName,
        bool fullSource = false)
    {
        var typePattern = new Regex(
            @$"(?:class|interface|record|struct|enum)\s+{Regex.Escape(typeName)}(?:[\s<({{:,]|$)",
            RegexOptions.NonBacktracking);

        var results = new List<TypeLookupResult>();

        foreach (var (full, relative) in fileService.EnumerateFiles(sourceRoots, "*.cs"))
        {
            var content = fileService.ReadAllText(full);
            if (!typePattern.IsMatch(content))
            {
                continue;
            }

            var lines = content.Split('\n');
            var ns = ExtractNamespace(lines);
            var declaration = ExtractDeclaration(lines, typePattern);
            var source = fullSource ? content : ExtractSummary(lines, typePattern);

            results.Add(new TypeLookupResult(relative, ns, declaration, source));
        }

        return results;
    }

    private static string ExtractNamespace(string[] lines)
    {
        foreach (var line in lines)
        {
            var match = NamespacePattern().Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return string.Empty;
    }

    private static string ExtractDeclaration(string[] lines, Regex typePattern)
    {
        foreach (var line in lines)
        {
            if (typePattern.IsMatch(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }

    private static string ExtractSummary(string[] lines, Regex typePattern)
    {
        var sb = new StringBuilder();
        var inType = false;
        var braceDepth = 0;

        foreach (var line in lines)
        {
            if (!inType)
            {
                if (typePattern.IsMatch(line))
                {
                    inType = true;
                    sb.AppendLine(line.TrimEnd());
                    braceDepth += CountChar(line, '{') - CountChar(line, '}');
                }
                else if (line.TrimStart().StartsWith("namespace", StringComparison.Ordinal) ||
                         line.TrimStart().StartsWith("using", StringComparison.Ordinal))
                {
                    sb.AppendLine(line.TrimEnd());
                }

                continue;
            }

            braceDepth += CountChar(line, '{') - CountChar(line, '}');

            if (braceDepth <= 0)
            {
                sb.AppendLine(line.TrimEnd());
                break;
            }

            var trimmed = line.TrimStart();
            if (IsMemberSignature(trimmed))
            {
                sb.AppendLine(line.TrimEnd());
            }
        }

        return sb.ToString();
    }

    private static bool IsMemberSignature(string line) =>
        line.StartsWith("public", StringComparison.Ordinal) ||
        line.StartsWith("protected", StringComparison.Ordinal) ||
        line.StartsWith("internal", StringComparison.Ordinal) ||
        line.StartsWith("private", StringComparison.Ordinal) ||
        line.StartsWith("static", StringComparison.Ordinal) ||
        line.StartsWith("abstract", StringComparison.Ordinal) ||
        line.StartsWith("virtual", StringComparison.Ordinal) ||
        line.StartsWith("override", StringComparison.Ordinal) ||
        line.StartsWith("event", StringComparison.Ordinal) ||
        line.StartsWith("//", StringComparison.Ordinal) ||
        line.StartsWith("///", StringComparison.Ordinal) ||
        line.StartsWith("[", StringComparison.Ordinal) ||
        line.StartsWith("}", StringComparison.Ordinal);

    private static int CountChar(string s, char c)
    {
        var count = 0;
        foreach (var ch in s)
        {
            if (ch == c)
            {
                count++;
            }
        }

        return count;
    }

    public const string PropertyKind = "Property";
    public const string MethodKind = "Method";
    public const string ConstructorKind = "Constructor";
    public const string EventKind = "Event";
    private const int MaxSignatureLines = 25;

    [GeneratedRegex(@"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new|file)\s+)*(?:record\s+)?(class|struct|interface|record)\s+(\w+)", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex TypeDeclPattern();

    [GeneratedRegex(@"^(?:\[[^\]]*\]\s*)+", RegexOptions.NonBacktracking)]
    private static partial Regex AttributePrefix();

    [GeneratedRegex(@"^(?:public|protected|internal)\b", RegexOptions.NonBacktracking)]
    private static partial Regex ModifierStart();

    [GeneratedRegex(@"^[A-Za-z_]", RegexOptions.NonBacktracking)]
    private static partial Regex InterfaceStart();

    [GeneratedRegex(@"^(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new|file)\s+)*(?:class|struct|interface|enum|delegate|record)\s+\w+", RegexOptions.NonBacktracking)]
    private static partial Regex NestedTypePattern();

    [GeneratedRegex(@"^(?:\w+\s+)*event\s", RegexOptions.NonBacktracking)]
    private static partial Regex EventPattern();

    [GeneratedRegex(@"^\{\s*(?:get|set|init)\b[^}]*\}", RegexOptions.NonBacktracking)]
    private static partial Regex InlineAccessors();

    [GeneratedRegex(@"^(?:(?:public|protected|internal|private|static|abstract|virtual|override|sealed|async|extern|unsafe|new|partial|readonly)\s+)+", RegexOptions.NonBacktracking)]
    private static partial Regex ModifierWords();

    [GeneratedRegex(@"^public\s+", RegexOptions.NonBacktracking)]
    private static partial Regex PublicPrefix();

    [GeneratedRegex(@"\)\s*:\s*(?:base|this)\s*\(.*$", RegexOptions.NonBacktracking)]
    private static partial Regex ConstructorInitializer();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\b(get|set|init)\b", RegexOptions.NonBacktracking)]
    private static partial Regex AccessorWord();

    [GeneratedRegex(@"\b(?:class|struct|interface|record)\s+(\w+(?:<[^>]*>)?)(.*)$", RegexOptions.NonBacktracking)]
    private static partial Regex HeaderPattern();

    public static IReadOnlyList<MemberInfo> Extract(string source, string? typeName = null)
    {
        var lines = source.Split('\n');
        var members = new List<MemberInfo>();
        var depth = 0;
        var targetDepth = -1;
        var bodyOpened = false;
        var inInterface = false;
        var targetUsed = false;
        var activeName = typeName;

        for (var i = 0; i < lines.Length; i++)
        {
            if (targetDepth >= 0 && bodyOpened && depth < targetDepth)
            {
                targetDepth = -1;
                bodyOpened = false;
                if (typeName is null)
                {
                    break;
                }
            }

            var line = lines[i].Trim();

            if (targetDepth < 0)
            {
                var decl = TypeDeclPattern().Match(line);
                if (decl.Success && !(typeName is null && targetUsed) && (typeName is null || decl.Groups[2].Value == typeName))
                {
                    targetDepth = depth + 1;
                    inInterface = decl.Groups[1].Value == "interface";
                    activeName = decl.Groups[2].Value;
                    targetUsed = true;
                    bodyOpened = false;
                }

                depth += BraceDelta(line);
                if (targetDepth >= 0 && depth >= targetDepth)
                {
                    bodyOpened = true;
                }

                continue;
            }

            if (depth >= targetDepth)
            {
                bodyOpened = true;
            }

            var stripped = AttributePrefix().Replace(line, "");
            var isCandidate = bodyOpened && depth == targetDepth && IsCandidate(stripped, inInterface);
            if (!isCandidate)
            {
                depth += BraceDelta(line);
                continue;
            }

            // Accumulate a (possibly multi-line) signature until a terminator is found.
            var text = new StringBuilder(stripped);
            depth += BraceDelta(line);
            var j = i;
            while (FindTerminator(text.ToString()).Index < 0 && j + 1 < lines.Length && j - i < MaxSignatureLines)
            {
                j++;
                var next = lines[j].Trim();
                depth += BraceDelta(next);
                text.Append(' ').Append(next);
            }

            var member = Parse(text.ToString(), activeName, () => InferAccessors(lines, j + 1, targetDepth));
            if (member is not null)
            {
                members.Add(member);
            }

            i = j;
        }

        return members;
    }

    public static string Format(IEnumerable<MemberInfo> members)
    {
        var list = members.ToList();
        var sb = new StringBuilder();
        AppendGroup(sb, "Properties", list, PropertyKind);
        AppendGroup(sb, "Methods", list, MethodKind);
        AppendGroup(sb, "Constructors", list, ConstructorKind);
        AppendGroup(sb, "Events", list, EventKind);
        return sb.ToString().TrimEnd();
    }

    public static string FormatHeader(string declaration, string fallbackName)
    {
        var match = HeaderPattern().Match(declaration);
        if (!match.Success)
        {
            return fallbackName;
        }

        var name = match.Groups[1].Value;
        var rest = match.Groups[2].Value;
        var whereIdx = rest.IndexOf(" where ", StringComparison.Ordinal);
        if (whereIdx >= 0)
        {
            rest = rest[..whereIdx];
        }

        var colon = rest.IndexOf(':');
        if (colon < 0)
        {
            return name;
        }

        var bases = rest[(colon + 1)..].Trim().TrimEnd('{').Trim();
        return bases.Length == 0 ? name : $"{name} : {bases}";
    }

    private static void AppendGroup(StringBuilder sb, string title, List<MemberInfo> members, string kind)
    {
        var group = members.Where(m => m.Kind == kind).Select(m => m.Signature).Distinct().ToList();
        if (group.Count == 0)
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.AppendLine();
        }

        sb.AppendLine($"{title}:");
        foreach (var signature in group)
        {
            sb.AppendLine($"  {signature}");
        }
    }

    private static bool IsCandidate(string stripped, bool inInterface)
    {
        if (stripped.Length == 0 ||
            stripped.StartsWith("//", StringComparison.Ordinal) ||
            stripped.StartsWith("/*", StringComparison.Ordinal) ||
            stripped.StartsWith('*') ||
            stripped.StartsWith('}') ||
            stripped.StartsWith('{'))
        {
            return false;
        }

        return ModifierStart().IsMatch(stripped) || (inInterface && InterfaceStart().IsMatch(stripped));
    }

    private static MemberInfo? Parse(string text, string? typeName, Func<string> inferAccessors)
    {
        var (idx, term) = FindTerminator(text);
        var head = (idx < 0 ? text : text[..idx]).Trim();
        if (head.Length == 0 || NestedTypePattern().IsMatch(head))
        {
            return null;
        }

        head = Normalize(PublicPrefix().Replace(head, ""));
        var hasParen = head.Contains('(');

        if (EventPattern().IsMatch(head))
        {
            return new MemberInfo(EventKind, head);
        }

        if (!hasParen)
        {
            if (term == "{")
            {
                var inline = InlineAccessors().Match(text[idx..]);
                var accessors = inline.Success ? Normalize(inline.Value) : inferAccessors();
                return new MemberInfo(PropertyKind, $"{head} {accessors}");
            }

            return term == "=>" ? new MemberInfo(PropertyKind, $"{head} {{ get; }}") : null;
        }

        var open = head.IndexOf('(');
        var core = ModifierWords().Replace(head[..open].Trim(), "");
        if (core.StartsWith('~'))
        {
            return null;
        }

        if (core.Length > 0 && core.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            if (typeName is not null && core != typeName)
            {
                return null;
            }

            return new MemberInfo(ConstructorKind, ConstructorInitializer().Replace(head, ")"));
        }

        return new MemberInfo(MethodKind, head);
    }

    private static string InferAccessors(string[] lines, int start, int targetDepth)
    {
        var found = new List<string>();
        var depth = targetDepth + 1;
        for (var k = start; k < lines.Length && depth > targetDepth; k++)
        {
            var line = lines[k].Trim();
            if (depth == targetDepth + 1)
            {
                var m = AccessorWord().Match(line);
                if (m.Success && m.Index == 0 && !found.Contains(m.Value))
                {
                    found.Add(m.Value);
                }
            }

            depth += BraceDelta(line);
        }

        return found.Count == 0 ? "{ get; }" : "{ " + string.Join(' ', found.Select(a => a + ";")) + " }";
    }

    private static (int Index, string Kind) FindTerminator(string text)
    {
        var best = -1;
        var kind = "";
        Consider(text.IndexOf('{'), "{");
        Consider(text.IndexOf("=>", StringComparison.Ordinal), "=>");
        Consider(text.IndexOf(';'), ";");
        return (best, kind);

        void Consider(int index, string k)
        {
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
                kind = k;
            }
        }
    }

    private static string Normalize(string s) => Whitespace().Replace(s.Trim(), " ").Replace("( ", "(", StringComparison.Ordinal);

    private static int BraceDelta(string line)
    {
        var delta = 0;
        foreach (var ch in line)
        {
            if (ch == '{')
            {
                delta++;
            }
            else if (ch == '}')
            {
                delta--;
            }
        }

        return delta;
    }

    private const string NoSource = "No Akka.NET source available. Use set_project_context or specify a version parameter.";
    private const int MaxWindowLines = 25;
    private static readonly string[] _lifecycle = ["PreStart", "PreRestart", "PostRestart", "PostStop", "AroundReceive", "Receive"];
    private static readonly string[] _requestSuffixes = ["Command", "Query"];
    private static readonly string[] _responseSuffixes = ["Response", "Result"];
    private const string EventSuffix = "Event";

    [McpServerTool(Name = "type_hierarchy"), Description("Show the full inheritance tree of a C# type: ancestors (base types and interfaces) and descendants.")]
    public async Task<string> TypeHierarchyAsync(
        [Description("Type name (e.g. ActorBase, ReceiveActor, IActorRef)")] string type,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Hierarchy(roots, type);
    }

    [McpServerTool(Name = "find_implementations"), Description("Find all classes implementing an interface, including classes deriving from an implementing class.")]
    public async Task<string> FindImplementationsAsync(
        [Description("Interface name (e.g. IActorRef, IWithTimers)")] string @interface,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Implementations(roots, @interface);
    }

    [McpServerTool(Name = "find_messages"), Description("List message types handled by an actor via Receive<T>, ReceiveAsync<T>, Command<T> handlers.")]
    public async Task<string> FindMessagesAsync(
        [Description("Actor class name")] string actor,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Messages(roots, actor);
    }

    [McpServerTool(Name = "message_protocol"), Description("Summarize Command/Query/Event/Response/Result types in a namespace or actor, grouped into request-response pairs.")]
    public async Task<string> MessageProtocolAsync(
        [Description("Namespace fragment or actor/type name")] string scope,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Protocol(roots, scope);
    }

    [McpServerTool(Name = "virtual_methods"), Description("List virtual and abstract methods of a type, ordered by actor lifecycle (PreStart, PreRestart, PostRestart, PostStop, AroundReceive, Receive).")]
    public async Task<string> VirtualMethodsAsync(
        [Description("Type name (e.g. ActorBase, ReceiveActor)")] string type,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : VirtualMethods(roots, type);
    }

    [McpServerTool(Name = "supervision_defaults"), Description("Show supervision strategies: default strategy, or SupervisorStrategy overrides for a given type.")]
    public async Task<string> SupervisionDefaultsAsync(
        [Description("Type name, or 'default' (default) for the framework default strategy")] string? filter = null,
        [Description("Optional Akka.NET version to target (e.g. \"1.5.52\"). Omit for project-detected or latest.")] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await _sourcePipeline.EnsureAllForVersionAsync(version, cancellationToken);
        return roots.Count == 0 ? NoSource : Supervision(roots, filter);
    }

    [GeneratedRegex(@"\b(?:Receive|ReceiveAsync|Command|CommandAsync)\s*<\s*(.+?)\s*>\s*\(")]
    private static partial Regex HandlerPattern();

    [GeneratedRegex(@"\b(Receive|ReceiveAsync|Command|CommandAsync)\s*<", RegexOptions.NonBacktracking)]
    private static partial Regex HandlerKindPattern();

    [GeneratedRegex(@"^\s*((?:(?:public|protected|internal|private|virtual|abstract|override|sealed|async|unsafe|new|static)\s+)+)(.*)$", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex MethodLine();

    [GeneratedRegex(@"(\w+)\s*(?:<[^>(]*>)?\s*\(", RegexOptions.NonBacktracking)]
    private static partial Regex MethodName();

    [GeneratedRegex(@"\boverride\s+SupervisorStrategy\s+SupervisorStrategy\b", RegexOptions.NonBacktracking)]
    private static partial Regex StrategyOverride();

    [GeneratedRegex(@"\b(?:DefaultStrategy|DefaultDecider)\s*(?:=|\{|\()", RegexOptions.NonBacktracking)]
    private static partial Regex StrategyDefault();

    [GeneratedRegex(@"\b(OneForOneStrategy|AllForOneStrategy)\s*\(", RegexOptions.NonBacktracking)]
    private static partial Regex StrategyCtor();

    [GeneratedRegex(@"\bDirective\.(\w+)", RegexOptions.NonBacktracking)]
    private static partial Regex DirectivePattern();

    public string Hierarchy(IReadOnlyList<string> roots, string type)
    {
        var name = TypeScanner.BaseName(type);
        var all = _scanner.ScanTypes(roots);
        var decls = all.Where(t => t.Name == name).ToList();
        if (decls.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source. Try search_source to find it by a different name.";
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
            return $"No implementations of '{@interface}' found. Try search_source to locate it.";
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

    public string Messages(IReadOnlyList<string> roots, string actor)
    {
        var name = TypeScanner.BaseName(actor);
        var decls = _scanner.ScanTypes(roots).Where(t => t.Name == name).ToList();
        if (decls.Count == 0)
        {
            return $"Actor '{actor}' not found in cached Akka.NET source. Try search_source to find it by a different name.";
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
            return $"No Receive/Command handlers found in '{actor}'. Try search_source to locate handler patterns.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{name} ({decls[0].Source.Path}) handles {handlers.Count} message type(s):");
        foreach (var (kind, message) in handlers)
        {
            sb.AppendLine($"  {message}  [{kind}]");
        }

        return sb.ToString().TrimEnd();
    }

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
            return $"No Command/Query/Event/Response/Result types found for '{scope}'. Try search_source to locate message types.";
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

    public string VirtualMethods(IReadOnlyList<string> roots, string type)
    {
        var name = TypeScanner.BaseName(type);
        var decls = _scanner.ScanTypes(roots).Where(t => t.Name == name).ToList();
        if (decls.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source. Try search_source to find it by a different name.";
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
            return $"No virtual or abstract methods found in '{type}'. Try type_members for all members.";
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
                ? "No default supervision strategy found in cached Akka.NET source. Try search_source for 'SupervisorStrategy'."
                : $"No SupervisorStrategy override or strategy usage found for '{filter}'. Try search_source to locate it.";
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
