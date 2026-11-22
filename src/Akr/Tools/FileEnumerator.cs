namespace Akr.Tools;

public static class FileEnumerator
{
    private static readonly string[] _excludeSegments = ["/obj/", "/bin/"];

    public static IEnumerable<(string Full, string Relative)> EnumerateFiles(
        IEnumerable<string> roots,
        string pattern)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (IsExcluded(relative))
                {
                    continue;
                }

                yield return (file, relative);
            }
        }
    }

    private static bool IsExcluded(string relative)
    {
        foreach (var segment in _excludeSegments)
        {
            if (relative.Contains(segment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
