namespace Akr.Tools;

internal static class OutputLimit
{
    private const int MaxLength = 50_000;
    private const string TruncationNotice = "\n\n[Output truncated. Use a more specific query or filter to narrow results.]";

    public static string Truncate(string output)
    {
        if (output.Length <= MaxLength)
        {
            return output;
        }

        var cutoff = output.LastIndexOf('\n', MaxLength);
        if (cutoff < MaxLength / 2)
        {
            cutoff = MaxLength;
        }

        return string.Concat(output.AsSpan(0, cutoff), TruncationNotice);
    }
}
