---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(dotnet:*), Bash(git:*), Read, Write, Edit, Glob, Grep
description: Unattended TDD implementation from ralph-tasks (no approval gates)
argument-hint: [count=1]
---

## Context

Current spec directory: specs/

**Workflow**: Unattended TDD implementation - no human approval gates.

**TDD Cycle**: 🔴 Red → 🟢 Green → 🔵 Refactor (no approval step)

**AskUserQuestion is deliberately excluded** - this command runs unattended.

## Critical Guidelines

**ALWAYS follow these instructions when writing code:**
- **Testing**: [.agent_instructions/testing.md](../../../.agent_instructions/testing.md)
- **Code Style**: [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md)

## Your Task

Parse count from $ARGUMENTS (default: 1 if not provided or empty).

### Step 1: Check STOP File

If a file named `RALPH_STOP` exists at the repository root:
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: Z
Status: STOPPED
=== END RALPH ===
```
Then STOP immediately. Do not proceed.

### Step 2: Gather Context

1. Read `specs/.current-spec` to determine the active specification directory
2. Verify `.tasks-approved` exists in that directory
3. Read `specs/{current-spec}/ralph-tasks.md` to see ralph task list
4. Read `specs/{current-spec}/.adr-list` to see all ADRs
5. Read each ADR from `docs/adr/` referenced in the task list

### Step 3: Select Next Task

Find the first unchecked `- [ ]` task in `ralph-tasks.md`.

If all tasks are checked (`[x]` or `[!]`):
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: 0
Status: ALL_DONE
=== END RALPH ===
```
Then STOP.

### Step 4: Read Task References

Before implementing, read ALL files listed in the task's **References** section. This provides context that would normally come from an ongoing conversation.

### Step 5: TDD Implementation Cycle (No Approval Gate)

For the selected task:

#### 🔴 RED: Write a Failing Test

1. **Read Testing Guidelines**: Review [.agent_instructions/testing.md](../../../.agent_instructions/testing.md)

2. **Write the Test** following these rules:
   - **Test naming**: `When_[condition]_should_[expected_behavior]`
   - **File naming**: One test case per file named `When_[condition]_should_[expected_behavior].cs`
   - **Structure**: Use Arrange/Act/Assert with explicit comments
   - **Evident Data**: Highlight the state that impacts the test outcome
   - **Test behavior, not implementation**: Test public exports only
   - **No mocks for isolation**: Use InMemory* implementations for I/O
   - **Only test public exports**: Don't test private or internal methods

3. **Create Test File**: Use Write tool to create the test at the path specified in the task

4. **Run the Test**: Use the task's `RALPH-VERIFY` command
   - Verify the test **FAILS** (Red)
   - The failure should be for the expected reason (behavior doesn't exist yet)
   - **If the test PASSES without any implementation changes**: The behavior already exists. Revise the test to verify something genuinely new, or mark the task as already complete.

#### 🟢 GREEN: Make the Test Pass

1. **Read Code Style Guidelines**: Review [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md)

2. **Write Minimum Code** to make the test pass:
   - Only write code necessary for the test to pass
   - No speculative code
   - Follow the implementation guidance from the task's **Implementation files** section

3. **Follow Code Style**:
   - .NET C# naming conventions (PascalCase for public, camelCase for private)
   - Use ALL_CAPS for constants with underscores
   - Expression-bodied members for simple properties/methods
   - readonly for fields that don't change after construction
   - Enable nullable reference types
   - Responsibility Driven Design principles
   - Avoid primitive obsession

4. **Run the Test Again**: Use the task's `RALPH-VERIFY` command
   - Verify the test **PASSES** (Green)

5. **Run Project Tests**: Run broader tests to catch regressions
   - `dotnet test` for the relevant test project(s)
   - If regressions found, fix them before proceeding

#### 🔵 REFACTOR: Improve the Design

1. **Review the Code** for Tidy First improvements:
   - Is it tidy and simple?
   - Can complexity be reduced?
   - Are methods small and focused?
   - Does it follow Responsibility Driven Design?
   - Does it avoid primitive obsession?

2. **Apply refactoring** if needed (structural changes only, no behavioral changes)

3. **Run All Tests After Each Refactoring**: Verify no behavioral changes

### Step 6: Commit and Update

1. **Mark task complete**: Use Edit tool to change `- [ ]` to `- [x]` in `ralph-tasks.md`

2. **Stage and commit**:
   ```bash
   git add [test-file] [implementation-files] specs/{current-spec}/ralph-tasks.md
   git commit -m "feat: [behavior description]

   - Test: When_[condition]_should_[expected_behavior]
   - Implementation: [brief description]
   - Ralph task: [task number]/[total]

   Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
   ```

### Step 7: Check Continuation

Before starting the next task, check ALL of these:

1. **STOP file**: If `RALPH_STOP` exists at repo root → stop
2. **Count limit**: If completed tasks this run >= count from $ARGUMENTS → stop
3. **All done**: If no more `- [ ]` tasks remain → stop

If none of these conditions are met, return to **Step 3** for the next task.

### Step 8: Print Summary

**ALWAYS** print this structured summary at the end, regardless of how you stopped:

```
=== RALPH SUMMARY ===
Tasks completed this run: N
Total tasks complete: X/Y
Tasks remaining: Z
Status: COMPLETED | COUNT_REACHED | STOPPED | ALL_DONE
=== END RALPH ===
```

Status meanings:
- `COMPLETED`: Finished all tasks requested by count
- `COUNT_REACHED`: Completed the requested number of tasks, more remain
- `STOPPED`: Halted due to RALPH_STOP file
- `ALL_DONE`: No more tasks in ralph-tasks.md

## Error Handling

If a task cannot be completed (build fails after multiple attempts, test won't pass, etc.):

1. **Mark the task as failed**: Change `- [ ]` to `- [!]` in ralph-tasks.md
2. **Add a comment**: Append ` <!-- RALPH-FAILED: [brief explanation of what went wrong] -->` to the task line
3. **Commit the failure marker**: So the next iteration knows to skip it
4. **Proceed to the next task**: Do not get stuck on a single task
5. **Count it toward the run count**: Failed tasks count toward the count limit

## Important Reminders

- **NEVER use AskUserQuestion** - this runs unattended
- **NEVER push to remote** - the human decides when to push
- **NEVER modify tasks.md** - only modify ralph-tasks.md
- **Always commit after each task** - each task gets its own commit
- **Read References first** - every task has context that must be read before starting
- Follow ALL guidelines in .agent_instructions/testing.md
- Follow ALL guidelines in .agent_instructions/code_style.md
