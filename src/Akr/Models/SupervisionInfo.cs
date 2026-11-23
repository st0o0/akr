namespace Akr.Models;

public sealed record SupervisionInfo(
    string Type,
    string Kind,
    string Strategy,
    IReadOnlyList<string> Directives,
    string File,
    int Line);
