## Why

akr has 33 MCP tools but only unit-level tests. There is no validation that the tools work
correctly when called through the MCP protocol by a real AI agent — the actual usage scenario.
A skill-driven E2E test lets Claude exercise every tool the way a real agent would, with strict
pass/fail criteria that represent the quality bar for the MCP server.

## What Changes

- Add a Claude Code skill (`.claude/skills/e2e-test/skill.md`) that orchestrates a full E2E
  test run against akr's MCP tools
- Add a detailed test plan (`e2e/E2E-TEST-PLAN.md`) with 33+ tool checks and 4 multi-tool
  scenarios, each with explicit validation criteria
- Opus orchestrates setup, sequential scenarios, result validation; 3 Haiku subagents test
  disjoint tool groups in parallel
- Results written to `e2e/E2E-TEST-RESULTS-<date>.md`

## Capabilities

### New Capabilities

- `e2e-skill`: The Claude Code skill definition — orchestration flow, subagent prompts,
  result validation, report generation
- `e2e-test-plan`: The detailed test plan with all tool checks, validation criteria,
  and multi-tool scenarios

### Modified Capabilities

(none)

## Impact

- **Files**: `.claude/skills/e2e-test/skill.md`, `e2e/E2E-TEST-PLAN.md`
- **Dependencies**: Requires akr to be configured as MCP server in the session (`.mcp.json`)
- **Runtime**: Manual invocation via `/e2e-test`, no CI integration
- **No code changes**: This is a testing skill, not application code
