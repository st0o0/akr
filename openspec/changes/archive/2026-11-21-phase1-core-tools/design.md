## Context

Akr has `search_source` (grep) and `lookup_type` (find type, return source). Both return raw
source text. The next tools need structured extraction: member signatures without bodies, and
extension method discovery filtered by pattern.

## Goals / Non-Goals

**Goals:**
- `type_members` tool returning structured member list for any Akka.NET type
- `hosting_extensions` tool returning `With*`/`Add*` signatures from Akka.Hosting
- Regex-based extraction (no Roslyn), AOT-safe

**Non-Goals:**
- Roslyn semantic analysis (Phase 1.5)
- Inheritance chain resolution (needs Roslyn)
- HOCON config reference (separate tool, Phase 1 follow-up)

## Decisions

### 1. MemberExtractor: regex-based member parsing

Extract members from C# source using regex patterns for:
- Properties: `(public|protected|internal) ... { get; set; }`
- Methods: `(public|protected|internal) ... (...)`
- Constructors: `(public|protected|internal) TypeName(...)`
- Events: `(public|protected|internal) event ...`

Output format per member:
```
[access] [static] [return-type] Name([params])
```

Grouped by kind (Properties, Methods, Constructors, Events). No method bodies  -  just
the signature line, trimmed.

This is good enough without Roslyn. Edge cases (expression-bodied members, multi-line
signatures) are handled by reading until the opening brace or semicolon.

### 2. type_members tool

```
Name: type_members
Parameters:
  - type: string (required)  -  type name
Returns:
  TypeName : BaseType, IInterface1, IInterface2
  
  Properties:
    Type Type { get; }
  
  Methods:
    static Props Create<T>() where T : ActorBase, new()
    static Props Create(Type type)
  
  Constructors:
    Props(Type type)
```

Uses `TypeFinder.FindType()` to locate the file, then `MemberExtractor` to parse members.
If multiple matches (partial classes), merge members from all files.

### 3. hosting_extensions tool

```
Name: hosting_extensions
Parameters:
  - filter: string (optional)  -  keyword to filter (e.g. "Persistence", "Cluster")
Returns:
  Grouped by source file (= feature area):
  
  --- Akka.Persistence.Hosting ---
  static AkkaConfigurationBuilder WithSqlPersistence(...)
  static AkkaConfigurationBuilder WithJournalAndSnapshot(...)
  
  --- Akka.Cluster.Hosting ---
  static AkkaConfigurationBuilder WithClustering(...)
  static AkkaConfigurationBuilder WithShardRegion<T>(...)
```

Searches all files in Akka.Hosting repos for `public static` methods whose name starts
with `With` or `Add` and whose first parameter is `this AkkaConfigurationBuilder` or
`this IServiceCollection`. Groups results by file path (which maps to feature area).

### 4. Both tools registered explicitly in McpSetup

```csharp
.WithTools<TypeMembersTool>()
.WithTools<HostingExtensionsTool>()
```
