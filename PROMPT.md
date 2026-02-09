# PROMPT.md - Test Coverage Improvement Project

This file captures the current state of the test coverage improvement initiative for resuming work.

## Current State

**Specification**: `specs/0003-test-coverage-improvement/`
**Status**: Requirements approved, Tasks created, ready for implementation

## What Was Done

1. **Analyzed test coverage** for Group 1 unit test projects:
   - `Paramore.Brighter.Core.Tests` (~363 test classes)
   - `Paramore.Brighter.Extensions.Tests` (13 test classes)
   - `Paramore.Brighter.InMemory.Tests` (~27 test classes)

2. **Identified coverage gaps** - ~102 new test classes needed across 7 phases

3. **Created specification** following existing pattern in `specs/`:
   - `specs/0003-test-coverage-improvement/README.md` - Overview
   - `specs/0003-test-coverage-improvement/requirements.md` - Detailed analysis and requirements (APPROVED)
   - `specs/0003-test-coverage-improvement/tasks.md` - 102 test tasks organized in 7 phases
   - `specs/0003-test-coverage-improvement/.requirements-approved` - Approval marker

4. **Updated `AGENTS.md`** with content from `.agent_instructions/` including:
   - Build commands
   - Code style guidelines
   - Design principles
   - Testing guidelines with TDD approval workflow

5. **Configured TDD workflow** for manual approval (since OpenCode doesn't have `/test-first` command):
   ```
   Agent writes test → STOP → User reviews → User approves → Agent runs test → Agent marks complete
   ```

## Files to Read on Resume

1. `specs/0003-test-coverage-improvement/tasks.md` - The task list to work through
2. `AGENTS.md` - Coding guidelines and conventions
3. `.agent_instructions/testing.md` - Detailed testing guidelines

## Next Steps

1. **Approve tasks** - Mark tasks as approved to begin implementation
2. **Start Phase 1** - Core Value Types (15 tests, highest priority)
   - Message, MessageHeader, MessageBody
   - Id, RoutingKey, PartitionKey
   - Subscription, Publication

## Phase Summary

| Phase | Focus | Tests | Priority | Status |
|-------|-------|-------|----------|--------|
| 1 | Core Value Types | 15 | P1 - Critical | Not started |
| 2 | Builders & Config | 8 | P2 - High | Not started |
| 3 | Extension Methods | 10 | P3 - Medium | Not started |
| 4 | JSON Converters | 12 | P3 - Medium | Not started |
| 5 | In-Memory Components | 18 | P4 - Medium | Not started |
| 6 | DI Extensions | 17 | P5 - Lower | Not started |
| 7 | Observability & Misc | 22 | P3 - Medium | Not started |

## TDD Workflow Reminder

For each test task:
1. I write the test file and **STOP**
2. You review in your IDE
3. You approve (or request changes)
4. I run the test to verify it passes
5. Mark task complete, move to next

## Key Conventions

- Test naming: `When_[condition]_should_[expected_behavior].cs`
- One test case per file (preferred)
- Use xUnit framework
- Use in-memory implementations over mocks (e.g., `InMemoryOutbox`)
- Include MIT license header in all new files
- Follow AAA pattern: Arrange, Act, Assert

## To Resume

Say: "Let's continue with the test coverage improvement. Start with Phase 1."

Or to check status: "What's the current state of the test coverage spec?"
