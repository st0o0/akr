namespace Akr.Tools;

public sealed record SearchResult(
    string File,
    int LineNumber,
    string MatchingLine,
    string[] ContextBefore,
    string[] ContextAfter);
