namespace Akr.Core.Tools;

public sealed record TypeLookupResult(
    string File,
    string Namespace,
    string Declaration,
    string Source);
