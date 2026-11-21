using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Arch = ArchUnitNET.Domain.Architecture;

namespace Akr.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly Arch _architecture = new ArchLoader()
        .LoadAssemblies(typeof(Program).Assembly)
        .Build();

    [Fact]
    public void Tools_Should_Not_Reference_Terp()
    {
        var toolTypes = Types().That().ResideInNamespace("Akr.Tools");
        var terpTypes = Types().That().ResideInNamespace("Terp");

        var rule = toolTypes.Should().NotDependOnAny(terpTypes);
        rule.Check(_architecture);
    }
}
