using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Arch = ArchUnitNET.Domain.Architecture;

namespace Akr.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly Arch _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Program).Assembly,
            typeof(Core.Placeholder).Assembly)
        .Build();

    [Fact]
    public void Core_Should_Not_Reference_Server()
    {
        var coreTypes = Types().That().ResideInAssembly(typeof(Core.Placeholder).Assembly);
        var serverTypes = Types().That().ResideInAssembly(typeof(Program).Assembly);

        var rule = coreTypes.Should().NotDependOnAny(serverTypes);
        rule.Check(_architecture);
    }

    [Fact]
    public void Core_Should_Not_Reference_Mcp_Sdk()
    {
        var coreTypes = Types().That().ResideInAssembly(typeof(Core.Placeholder).Assembly);
        var mcpTypes = Types().That().ResideInNamespace("ModelContextProtocol");

        var rule = coreTypes.Should().NotDependOnAny(mcpTypes);
        rule.Check(_architecture);
    }
}
