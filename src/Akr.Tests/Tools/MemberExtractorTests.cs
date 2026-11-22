using Akr.Tools;

namespace Akr.Tests.Tools;

public sealed class MemberExtractorTests
{
    private const string ClassSource = """
        namespace Akka.Actor;
        public sealed class Props : ISurrogated
        {
            private readonly int _hidden = 1;
            public Type Type { get; }
            public int Count { get; set; }
            public Props(Type type)
            {
                Type = type;
            }

            protected Props(Type type, object[] args) : this(type)
            {
            }

            public static Props Create<T>() where T : ActorBase, new()
            {
                return new Props(typeof(T));
            }

            private void Hidden() { }
        }
        """;

    [Fact]
    public void Extract_ClassWithPropertiesMethodsConstructors()
    {
        var members = TypeTools.Extract(ClassSource, "Props");

        Assert.Contains(new MemberInfo("Property", "Type Type { get; }"), members);
        Assert.Contains(new MemberInfo("Property", "int Count { get; set; }"), members);
        Assert.Contains(new MemberInfo("Constructor", "Props(Type type)"), members);
        Assert.Contains(new MemberInfo("Constructor", "protected Props(Type type, object[] args)"), members);
        Assert.Contains(new MemberInfo("Method", "static Props Create<T>() where T : ActorBase, new()"), members);
        Assert.DoesNotContain(members, m => m.Signature.Contains("Hidden", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_Interface_IncludesUnmodifiedMembers()
    {
        const string source = """
            public interface IActorRef
            {
                ActorPath Path { get; }
                void Tell(object message, IActorRef sender);
                Task<T> Ask<T>(object message, TimeSpan? timeout = null);
            }
            """;

        var members = TypeTools.Extract(source, "IActorRef");

        Assert.Contains(new MemberInfo("Property", "ActorPath Path { get; }"), members);
        Assert.Contains(new MemberInfo("Method", "void Tell(object message, IActorRef sender)"), members);
        Assert.Contains(new MemberInfo("Method", "Task<T> Ask<T>(object message, TimeSpan? timeout = null)"), members);
    }

    [Fact]
    public void Extract_Events()
    {
        const string source = """
            public class Watcher
            {
                public event EventHandler Changed;
                protected event Action<string> Notified;
            }
            """;

        var members = TypeTools.Extract(source, "Watcher");

        Assert.Contains(new MemberInfo("Event", "event EventHandler Changed"), members);
        Assert.Contains(new MemberInfo("Event", "protected event Action<string> Notified"), members);
    }

    [Fact]
    public void Extract_ExpressionBodiedMembers_StripBodies()
    {
        const string source = """
            public class Sample
            {
                public int Value => 42;
                public string Describe(int x) => x.ToString();
            }
            """;

        var members = TypeTools.Extract(source, "Sample");

        Assert.Contains(new MemberInfo("Property", "int Value { get; }"), members);
        Assert.Contains(new MemberInfo("Method", "string Describe(int x)"), members);
    }

    [Fact]
    public void Extract_MultiLineSignature_CapturesFullSignature()
    {
        const string source = """
            public class Sample
            {
                public void Configure(
                    string name,
                    int count)
                {
                    DoWork();
                }

                public int Number
                {
                    get;
                    set;
                }
            }
            """;

        var members = TypeTools.Extract(source, "Sample");

        Assert.Contains(new MemberInfo("Method", "void Configure(string name, int count)"), members);
        Assert.Contains(new MemberInfo("Property", "int Number { get; set; }"), members);
    }

    [Fact]
    public void Format_GroupsMembersByKind()
    {
        var text = TypeTools.Format(TypeTools.Extract(ClassSource, "Props"));

        Assert.StartsWith("Properties:", text, StringComparison.Ordinal);
        Assert.Contains("Methods:", text, StringComparison.Ordinal);
        Assert.Contains("Constructors:", text, StringComparison.Ordinal);
        Assert.Contains("  static Props Create<T>() where T : ActorBase, new()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHeader_IncludesBaseTypes()
    {
        Assert.Equal("Props : ISurrogated", TypeTools.FormatHeader("public sealed class Props : ISurrogated", "Props"));
        Assert.Equal("Props", TypeTools.FormatHeader("public sealed class Props", "Props"));
    }

    [Fact]
    public void ExtractParentTypes_ParsesInterfaceList()
    {
        var parents = TypeTools.ExtractParentTypes("public interface IActorRef : ICanTell, IEquatable<IActorRef>, IComparable<IActorRef>, ISurrogated, IComparable");

        Assert.Contains("ICanTell", parents);
        Assert.DoesNotContain("IEquatable", parents);
        Assert.DoesNotContain("IComparable", parents);
        Assert.DoesNotContain("ISurrogated", parents);
    }

    [Fact]
    public void ExtractParentTypes_NoColon_ReturnsEmpty()
    {
        var parents = TypeTools.ExtractParentTypes("public class Simple");

        Assert.Empty(parents);
    }

    [Fact]
    public void ExtractParentTypes_HandlesGenericBases()
    {
        var parents = TypeTools.ExtractParentTypes("public class MyActor : ActorBase, IWithTimers");

        Assert.Contains("ActorBase", parents);
        Assert.Contains("IWithTimers", parents);
    }
}
