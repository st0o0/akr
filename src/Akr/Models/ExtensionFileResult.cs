namespace Akr.Models;

public sealed record ExtensionFileResult(string File, IReadOnlyList<string> Signatures);
