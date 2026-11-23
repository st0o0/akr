namespace Akr.Models;

public sealed record TypeDecl(
    string Name,
    string Kind,
    string Namespace,
    IReadOnlyList<string> Bases,
    SourceFile Source,
    int Index);
