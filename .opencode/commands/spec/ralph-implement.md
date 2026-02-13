---
description: Unattended TDD implementation from ralph-tasks (no approval gates)
agent: coder
---

**CRITICAL: You MUST call tools in your FIRST response. Do NOT describe what you will do - just DO IT.**

## IMMEDIATE ACTION REQUIRED

Count of tasks to complete: $ARGUMENTS (default: 1 if not provided or empty)

**Your first action**: Use the Read tool NOW to read these files IN PARALLEL:
1. `RALPH_STOP` (check if exists - if it does, output RALPH SUMMARY with Status: STOPPED and stop)
2. `specs/.current-spec` (to get current spec name)
3. `specs/0003-test-coverage-improvement/ralph-tasks.md` (to find first unchecked task)
4. `.agent_instructions/testing.md`
5. `.agent_instructions/code_style.md`

Do NOT output any text before calling the Read tool. Call it NOW.

---

## Workflow Details (reference after reading files)

**Workflow**: Unattended TDD implementation - no human approval gates.

**TDD Cycle**: 🔴 Red → 🟢 Green → 🔵 Refactor (no approval step)

**AskUserQuestion is deliberately excluded** - this command runs unattended.

### After Reading Files - Check STOP File

If `RALPH_STOP` exists at the repository root:
```
=== RALPH SUMMARY ===
Tasks completed this run: 0
Total tasks complete: X/Y
Tasks remaining: Z
Status: STOPPED
=== END RALPH ===
```
Then STOP immediately. Do not proceed.

### Select Next Task

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

### Read Task References

Before implementing, read ALL files listed in the task's **References** section. This provides context that would normally come from an ongoing conversation.

### TDD Implementation Cycle (No Approval Gate)

For the selected task:

#### 🔴 RED: Write a Failing Test

1. **Write the Test** following these rules (you already read the testing guidelines):
   - **Test naming**: `When_[condition]_should_[expected_behavior]`
   - **File naming**: One test case per file named `When_[condition]_should_[expected_behavior].cs`
   - **Structure**: Use Arrange/Act/Assert with explicit comments
   - **Evident Data**: Highlight the state that impacts the test outcome
   - **Test behavior, not implementation**: Test public exports only
   - **No mocks for isolation**: Use InMemory* implementations for I/O
   - **Only test public exports**: Don't test private or internal methods

2. **Create Test File**: Use Write tool to create the test at the path specified in the task

3. **Run the Test**: Use the task's `RALPH-VERIFY` command
   - Verify the test **FAILS** (Red)
   - The failure should be for the expected reason (behavior doesn't exist yet)
   - **If the test PASSES without any implementation changes**: The behavior already exists. Revise the test to verify something genuinely new, or mark the task as already complete.

#### 🟢 GREEN: Make the Test Pass

1. **Write Minimum Code** to make the test pass (you already read the code style guidelines):
   - Only write code necessary for the test to pass
   - No speculative code
   - Follow the implementation guidance from the task's **Implementation files** section

2. **Follow Code Style**:
   - .NET C# naming conventions (PascalCase for public, camelCase for private)
   - Use ALL_CAPS for constants with underscores
   - Expression-bodied members for simple properties/methods
   - readonly for fields that don't change after construction
   - Enable nullable reference types
   - Responsibility Driven Design principles
   - Avoid primitive obsession

3. **Run the Test Again**: Use the task's `RALPH-VERIFY` command
   - Verify the test **PASSES** (Green)

4. **Run Project Tests**: Run broader tests to catch regressions
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

### Commit and Update

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

### Check Continuation

Before starting the next task, check ALL of these:

1. **STOP file**: If `RALPH_STOP` exists at repo root → stop
2. **Count limit**: If completed tasks this run >= count from $ARGUMENTS → stop
3. **All done**: If no more `- [ ]` tasks remain → stop

If none of these conditions are met, return to **Select Next Task** for the next task.

### Print Summary

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
