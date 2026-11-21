namespace Akr.Core.Tools;

public sealed record SearchResult(
    string File,
    int LineNumber,
    string MatchingLine,
    string[] ContextBefore,
    string[] ContextAfter);
