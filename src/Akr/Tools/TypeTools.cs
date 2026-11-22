using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Akr.Tools;

public sealed record MemberInfo(string Kind, string Signature);

[McpServerToolType]
public sealed partial class TypeTools
{
    private readonly FileService _fileService;

    public TypeTools(FileService fileService)
    {
        _fileService = fileService;
    }

    [McpServerTool(Name = "lookup_type"), Description("Find a C# type by name across cached Akka.NET source code and return its source.")]
    public async Task<string> LookupTypeAsync(
        GitHubClient github,
        [Description("Type name to look up (e.g. ActorBase, IActorRef, Props)")] string type,
        [Description("Return full file source instead of summary (default: false)")] bool? fullSource = null,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        var results = FindType(roots, type, fullSource ?? false);
        if (results.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source.";
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
        GitHubClient github,
        [Description("Type name (e.g. ActorBase, Props, IActorRef)")] string type,
        CancellationToken cancellationToken = default)
    {
        var roots = await github.EnsureAllSourcesAsync(cancellationToken);
        if (roots.Count == 0)
        {
            return "No Akka.NET source available.";
        }

        var results = FindType(roots, type, fullSource: true);
        if (results.Count == 0)
        {
            return $"Type '{type}' not found in cached Akka.NET source.";
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

    [GeneratedRegex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline)]
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

    [GeneratedRegex(@"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new|file)\s+)*(?:record\s+)?(class|struct|interface|record)\s+(\w+)")]
    private static partial Regex TypeDeclPattern();

    [GeneratedRegex(@"^(?:\[[^\]]*\]\s*)+")]
    private static partial Regex AttributePrefix();

    [GeneratedRegex(@"^(?:public|protected|internal)\b")]
    private static partial Regex ModifierStart();

    [GeneratedRegex(@"^[A-Za-z_]")]
    private static partial Regex InterfaceStart();

    [GeneratedRegex(@"^(?:\w+\s+)*?(?:class|struct|interface|enum|delegate|record)\s+\w+")]
    private static partial Regex NestedTypePattern();

    [GeneratedRegex(@"^(?:\w+\s+)*event\s")]
    private static partial Regex EventPattern();

    [GeneratedRegex(@"^\{\s*(?:get|set|init)\b[^}]*\}")]
    private static partial Regex InlineAccessors();

    [GeneratedRegex(@"^(?:(?:public|protected|internal|private|static|abstract|virtual|override|sealed|async|extern|unsafe|new|partial|readonly)\s+)+")]
    private static partial Regex ModifierWords();

    [GeneratedRegex(@"^public\s+")]
    private static partial Regex PublicPrefix();

    [GeneratedRegex(@"\)\s*:\s*(?:base|this)\s*\(.*$")]
    private static partial Regex ConstructorInitializer();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\b(get|set|init)\b")]
    private static partial Regex AccessorWord();

    [GeneratedRegex(@"\b(?:class|struct|interface|record)\s+(\w+(?:<[^>]*>)?)(.*)$")]
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
}
