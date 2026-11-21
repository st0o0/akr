# akr — Idea

## Problem

AI agents working with Akka.NET codebases constantly decompile assemblies (via ILSpy) to understand types, config, and internals. This is:

- **Token-expensive**: Full decompiled source includes compiler-generated noise, attributes, and boilerplate
- **Slow**: Each lookup requires finding the assembly, decompiling, and filtering
- **Lossy**: Decompiled code lacks doc-comments, intent, and cross-references
- **Repetitive**: The same types get decompiled across conversations

## Solution

An MCP server (`akr`) that provides **structured, token-efficient access to Akka.NET source code** directly from the GitHub repositories, matched to the latest release version.

## Core Principles

1. **Hosting-first**: Always suggest `Akka.Hosting` APIs before HOCON config
2. **Latest-by-default**: Fetches latest release, caches locally, zero config needed
3. **Token-efficient**: Returns signatures and summaries by default, full source on demand
4. **Multi-repo**: Covers the full Akka.NET ecosystem (5 repos, 24 packages)
5. **Native AOT**: Small binary, fast startup, no runtime dependencies

## Token Savings

| Scenario | Without akr | With akr | Savings |
|----------|-------------|----------|---------|
| Look up actor lifecycle methods | ~2000 tokens (decompile ActorBase) | ~200 tokens (virtual_methods) | ~90% |
| Find stream stage | ~3000 tokens (decompile + search) | ~200 tokens (stream_stages) | ~93% |
| Check HOCON config defaults | ~1500 tokens (find reference.conf) | ~150 tokens (config_reference) | ~90% |
| Understand type hierarchy | ~4000 tokens (multiple decompiles) | ~300 tokens (type_hierarchy) | ~92% |
| Find hosting extension methods | ~2000 tokens (search docs) | ~200 tokens (hosting_extensions) | ~90% |
