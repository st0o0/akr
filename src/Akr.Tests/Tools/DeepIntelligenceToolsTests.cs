using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class DeepIntelligenceToolsTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IReadOnlyList<string> _roots;

    public DeepIntelligenceToolsTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
        _roots = [_testRoot];

        WriteFile("Actor/ActorBase.cs", """
            namespace Akka.Actor;

            public interface IActorRef { }
            public interface IInternalActor { }

            public abstract class ActorBase : IInternalActor
            {
                protected abstract bool Receive(object message);
                protected virtual void PostStop() { }
                protected virtual void AroundReceive(Receive receive, object message) { }
                protected virtual void PreStart() { }
                protected virtual void PreRestart(Exception reason, object message) { }
                protected virtual void PostRestart(Exception reason) { }
                protected virtual SupervisorStrategy SupervisorStrategy() => SupervisorStrategy.DefaultStrategy;
                protected virtual bool Unhandled(object message) => false;
                public void NotOverridable() { }
            }

            public abstract class SupervisorStrategy
            {
                public static readonly SupervisorStrategy DefaultStrategy = new OneForOneStrategy(
                    Decider.From(
                        Directive.Restart,
                        Directive.Stop));
            }

            public sealed class LocalActorRef : ActorBase, IActorRef
            {
                protected override bool Receive(object message) => true;
            }

            public sealed class RemoteActorRef<T> : IActorRef, IDisposable where T : class
            {
            }

            public sealed class ChildRef : LocalActorRef
            {
            }
            """);

        WriteFile("Users/UserActor.cs", """
            namespace MyApp.Users;

            public sealed record GetUserQuery(string Id);
            public sealed record GetUserResponse(string Name);
            public sealed record CreateUserCommand(string Name);
            public sealed record CreateUserResult(bool Ok);
            public sealed record DeleteUserCommand(string Id);
            public sealed record UserCreatedEvent(string Id);

            public sealed class UserActor : ReceiveActor
            {
                public UserActor()
                {
                    Receive<GetUserQuery>(q => { });
                    ReceiveAsync<CreateUserCommand>(async c => await Task.CompletedTask);
                    Command<DeleteUserCommand>(c => { });
                    Receive<List<string>>(l => { });
                }

                protected override SupervisorStrategy SupervisorStrategy()
                {
                    return new AllForOneStrategy(3, TimeSpan.FromSeconds(10), ex =>
                    {
                        return Directive.Escalate;
                    });
                }
            }

            public sealed class EmptyActor : ReceiveActor
            {
            }
            """);
    }

    // type_hierarchy

    [Fact]
    public void Hierarchy_shows_ancestors_and_descendants()
    {
        var output = DeepIntelligenceTools.Hierarchy(_roots, "LocalActorRef");

        Assert.Contains("-> ActorBase (class)", output);
        Assert.Contains("-> IInternalActor (interface)", output);
        Assert.Contains("-> IActorRef (interface)", output);
        Assert.Contains("<- ChildRef (class)", output);
    }

    [Fact]
    public void Hierarchy_handles_generics_and_where_clauses()
    {
        var output = DeepIntelligenceTools.Hierarchy(_roots, "RemoteActorRef<T>");

        Assert.Contains("-> IActorRef", output);
        Assert.Contains("-> IDisposable (external)", output);
        Assert.DoesNotContain("where", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Hierarchy_unknown_type_reports_not_found()
    {
        Assert.Contains("not found", DeepIntelligenceTools.Hierarchy(_roots, "Nope"));
    }

    // find_implementations

    [Fact]
    public void Implementations_finds_direct_and_via_base_class()
    {
        var output = DeepIntelligenceTools.Implementations(_roots, "IActorRef");

        Assert.Contains("LocalActorRef (class)", output);
        Assert.Contains("RemoteActorRef (class)", output);
        Assert.Contains("ChildRef (class) via LocalActorRef", output);
        Assert.DoesNotContain("ActorBase (", output);
    }

    [Fact]
    public void Implementations_no_match()
    {
        Assert.Contains("No implementations", DeepIntelligenceTools.Implementations(_roots, "IUnknown"));
    }

    // find_messages

    [Fact]
    public void Messages_extracts_handler_types()
    {
        var output = DeepIntelligenceTools.Messages(_roots, "UserActor");

        Assert.Contains("GetUserQuery  [Receive]", output);
        Assert.Contains("CreateUserCommand  [ReceiveAsync]", output);
        Assert.Contains("DeleteUserCommand  [Command]", output);
        Assert.Contains("List<string>  [Receive]", output);
        Assert.Contains("4 message type(s)", output);
    }

    [Fact]
    public void Messages_no_handlers_or_unknown_actor()
    {
        Assert.Contains("No Receive/Command handlers", DeepIntelligenceTools.Messages(_roots, "EmptyActor"));
        Assert.Contains("not found", DeepIntelligenceTools.Messages(_roots, "Ghost"));
    }

    // message_protocol

    [Fact]
    public void Protocol_pairs_requests_with_responses()
    {
        var output = DeepIntelligenceTools.Protocol(_roots, "MyApp.Users");

        Assert.Contains("GetUserQuery -> GetUserResponse", output);
        Assert.Contains("CreateUserCommand -> CreateUserResult", output);
        Assert.Contains("DeleteUserCommand -> (no response)", output);
        Assert.Contains("UserCreatedEvent", output);
    }

    [Fact]
    public void Protocol_by_actor_name_and_no_match()
    {
        Assert.Contains("GetUserQuery", DeepIntelligenceTools.Protocol(_roots, "UserActor"));
        Assert.Contains("No Command/Query", DeepIntelligenceTools.Protocol(_roots, "Nothing.Here"));
    }

    // virtual_methods

    [Fact]
    public void VirtualMethods_are_ordered_by_lifecycle()
    {
        var output = DeepIntelligenceTools.VirtualMethods(_roots, "ActorBase");

        var order = new[] { "PreStart", "PreRestart", "PostRestart", "PostStop", "AroundReceive", "Receive(object", "SupervisorStrategy", "Unhandled" };
        var positions = order.Select(o => output.IndexOf(o, StringComparison.Ordinal)).ToArray();

        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
        Assert.DoesNotContain("NotOverridable", output);
    }

    [Fact]
    public void VirtualMethods_none_or_unknown()
    {
        Assert.Contains("No virtual or abstract", DeepIntelligenceTools.VirtualMethods(_roots, "UserActor"));
        Assert.Contains("not found", DeepIntelligenceTools.VirtualMethods(_roots, "Ghost"));
    }

    // supervision_defaults

    [Fact]
    public void Supervision_default_shows_strategy_and_directives()
    {
        var output = DeepIntelligenceTools.Supervision(_roots, "default");

        Assert.Contains("OneForOneStrategy", output);
        Assert.Contains("Directives: Restart, Stop", output);
    }

    [Fact]
    public void Supervision_for_type_shows_override()
    {
        var output = DeepIntelligenceTools.Supervision(_roots, "UserActor");

        Assert.Contains("UserActor [override] AllForOneStrategy", output);
        Assert.Contains("Directives: Escalate", output);
    }

    [Fact]
    public void Supervision_no_match()
    {
        Assert.Contains("No SupervisorStrategy", DeepIntelligenceTools.Supervision(_roots, "EmptyActor"));
        Assert.Contains("No default supervision", DeepIntelligenceTools.Supervision([Path.Combine(_testRoot, "Users")], null));
    }

    [Fact]
    public void ScanTypes_returns_same_cached_instance_on_second_call()
    {
        var first = DeepIntelligenceTools.Hierarchy(_roots, "ActorBase");
        var second = DeepIntelligenceTools.Hierarchy(_roots, "ActorBase");

        Assert.Equal(first, second);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose() => Directory.Delete(_testRoot, recursive: true);
}
