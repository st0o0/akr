using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class OutputLimitTests
{
    [Fact]
    public void Short_output_passes_through_unchanged()
    {
        var input = "Hello, world!";
        Assert.Equal(input, OutputLimit.Truncate(input));
    }

    [Fact]
    public void Long_output_is_truncated_with_notice()
    {
        var input = new string('x', 60_000);
        var result = OutputLimit.Truncate(input);

        Assert.True(result.Length < input.Length);
        Assert.Contains("[Output truncated", result);
    }

    [Fact]
    public void Truncation_prefers_line_boundary()
    {
        var lines = string.Join('\n', Enumerable.Range(0, 10_000).Select(i => $"Line {i}: content"));
        var result = OutputLimit.Truncate(lines);

        var noticeIndex = result.IndexOf("\n\n[Output");
        Assert.True(noticeIndex > 0);
        Assert.StartsWith("Line ", result.AsSpan()[(result.LastIndexOf('\n', noticeIndex - 1) + 1)..noticeIndex]);
    }
}
