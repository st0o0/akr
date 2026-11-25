## Purpose

All logging uses `[LoggerMessage]` source generators for AOT compatibility and conditional argument evaluation.

## Requirements

### Requirement: All logging uses LoggerMessage source generators
All logging call sites SHALL use `[LoggerMessage]` partial methods instead of direct `ILogger.Log*()` calls. Each class that logs SHALL declare a `private static partial class Log` containing its log methods.

#### Scenario: Log method skips argument evaluation when level disabled
- **WHEN** a log method is called and the corresponding log level is disabled
- **THEN** no arguments are evaluated (no string formatting, no LINQ, no allocations)

#### Scenario: AOT-compatible logging
- **WHEN** the project is published with Native AOT
- **THEN** all logging methods compile without reflection or dynamic code generation

### Requirement: No eager argument construction in log calls
Logging calls SHALL NOT construct arguments eagerly. Expensive operations like `string.Join()`, LINQ projections, or string interpolation SHALL be deferred to the `[LoggerMessage]` source generator.

#### Scenario: ProjectScanner package detection logging
- **WHEN** `ProjectScanner` logs detected packages
- **THEN** the `string.Join` over package entries is only evaluated when Information level is enabled
