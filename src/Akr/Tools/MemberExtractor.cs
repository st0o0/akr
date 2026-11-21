using System.Text;
using System.Text.RegularExpressions;

namespace Akr.Tools;

public sealed record MemberInfo(string Kind, string Signature);

public static partial class MemberExtractor
{
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
