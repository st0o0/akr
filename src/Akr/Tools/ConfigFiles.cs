namespace Akr.Tools;

public static class ConfigFiles
{
    private static readonly string[] _excludeSegments =
    [
        "/test/", "/tests/", "/Test/", "/Tests/",
        ".Tests/", ".Tests.",
        ".TestKit/", ".TestKit.",
        "/example/", "/examples/", "/Example/", "/Examples/",
        "/obj/", "/bin/"
    ];

    public static IEnumerable<(string Full, string Relative)> EnumerateConfigFiles(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.conf", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                if (IsExcluded(relative))
                {
                    continue;
                }

                if (!IsKnownConfigFile(relative))
                {
                    continue;
                }

                yield return (file, relative);
            }
        }
    }

    private static bool IsKnownConfigFile(string relative)
    {
        var fileName = Path.GetFileName(relative);

        if (fileName.Equals("reference.conf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (relative.Contains("/Configuration/", StringComparison.OrdinalIgnoreCase)
            && ContainsCorePrefix(relative))
        {
            return true;
        }

        if (fileName.Equals("persistence.conf", StringComparison.OrdinalIgnoreCase)
            && ContainsCorePrefix(relative))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsCorePrefix(string relative) =>
        relative.Contains("/src/core/", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("src/core/", StringComparison.OrdinalIgnoreCase);

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
