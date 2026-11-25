## Purpose

All `[GeneratedRegex]` patterns compile fully via source generator with `NonBacktracking` where compatible.

## Requirements

### Requirement: All GeneratedRegex patterns compile fully via source generator
Every `[GeneratedRegex]` attribute SHALL produce a complete source-generated implementation without SYSLIB1044 fallback to interpreted mode.

#### Scenario: Build produces no SYSLIB1044 warnings
- **WHEN** the solution is built
- **THEN** zero SYSLIB1044 warnings are emitted

### Requirement: NonBacktracking for all regex patterns
All `[GeneratedRegex]` patterns SHALL use `RegexOptions.NonBacktracking` unless the pattern uses features incompatible with the NonBacktracking engine (backreferences, lookaheads).

#### Scenario: Pattern with lazy quantifier is rewritten
- **WHEN** a pattern uses a lazy quantifier (`*?`, `+?`) with `NonBacktracking`
- **THEN** the pattern is rewritten to use a greedy equivalent or decomposed into multi-step operations

### Requirement: Complex declaration patterns are decomposed
Patterns that exceed the source generator's DFA capability (large nested alternation groups) SHALL be decomposed into multi-step string operations.

#### Scenario: TypeDeclPattern decomposition
- **WHEN** type declarations are parsed
- **THEN** the match uses a two-pass approach: first match the type keyword, then parse modifiers in code
