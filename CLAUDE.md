# CLAUDE.md

@AGENTS.md

## Workflow

Changes go through OpenSpec: `/opsx:explore` to think - `/opsx:propose` to create
a change (proposal/design/specs/tasks) - `/opsx:apply` to implement - `/opsx:archive`.

## Skill routing

### Plugin skills

- C# coding: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`
- Roslyn: `dotnet-skills:roslyn-incremental-generator-specialist` (agent)
- Testing: `dotnet-skills:snapshot-testing`, `dotnet-skills:testcontainers`
- Project structure: `dotnet-skills:project-structure`, `dotnet-skills:package-management`
- DI / Config: `dotnet-skills:microsoft-extensions-dependency-injection`, `dotnet-skills:microsoft-extensions-configuration`
- Serialization: `dotnet-skills:serialization`
- API design: `dotnet-skills:csharp-api-design`
- Concurrency: `dotnet-skills:csharp-concurrency-patterns`, `dotnet-skills:dotnet-concurrency-specialist` (agent)
- Performance: `dotnet-skills:dotnet-benchmark-designer` (agent), `dotnet-skills:dotnet-performance-analyst` (agent)
- Quality: `dotnet-skills:slopwatch`, `dotnet-skills:crap-analysis`

### Quality gate

Slopwatch runs automatically on every edit (PostToolUse hook). If it flags an issue,
fix the underlying problem - never suppress the warning or disable the check.

After any code change, run `slopwatch analyze` to verify. Use `slopwatch analyze --fail-on warning`
for strict mode.
