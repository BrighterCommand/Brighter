---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(ls:*), Bash(echo:*), Bash(dotnet:*), Bash(git:*), Read, Write, Edit, Glob, Grep, AskUserQuestion
description: Start TDD implementation from approved tasks
argument-hint: [task-number]
---

## Context

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → **Tests → Code**

**TDD Cycle**: 🔴 Red → ✅ User Approval → 🟢 Green → 🔵 Refactor

## Critical Guidelines

**ALWAYS follow these instructions when writing code:**
- **Testing**: [docs/agent_instructions/testing.md](../../../docs/agent_instructions/testing.md)
- **Code Style**: [docs/agent_instructions/code_style.md](../../../docs/agent_instructions/code_style.md)

## Your Task

### Step 1: Gather Context

1. Read `specs/.current-spec` to determine the active specification directory
2. Verify `.tasks-approved` exists in that directory
3. Read `specs/{current-spec}/tasks.md` to see task list
4. Read `specs/{current-spec}/.adr-list` to see all ADRs
5. Read ADRs from `docs/adr/` to understand design decisions
6. If task number provided in $ARGUMENTS, focus on that task only

### Step 2: Verify Prerequisites

Check that all phases are approved:
- Requirements: `.requirements-approved` exists
- Design: `.design-approved` exists and all ADRs have Status "Accepted"
- Tasks: `.tasks-approved` exists

If not all approved, inform user and exit.

### Step 3: Select Task

Display current incomplete tasks from tasks.md.

If task number provided, work on that specific task.
Otherwise, suggest the next logical task to work on.

### Step 4: TDD Implementation Cycle

For each task, follow this strict workflow:

#### 🔴 RED: Write a Failing Test

1. **Read Testing Guidelines**: Review [docs/agent_instructions/testing.md](../../../docs/agent_instructions/testing.md)

2. **Understand the Behavior**: Identify the specific behavior this task requires
   - What is the expected behavior?
   - What is the simplest test that demonstrates this behavior?

3. **Write the Test** following these rules from testing.md:
   - **Test naming**: `When_[condition]_should_[expected_behavior]`
   - **File naming**: Prefer one test case per file named `When_[condition]_should_[expected_behavior].cs`
   - **Structure**: Use Arrange/Act/Assert with explicit comments
   - **Evident Data**: Highlight the state that impacts the test outcome
   - **Test behavior, not implementation**: Test public exports only
   - **No mocks for isolation**: Use developer tests that implicate the most recent edit
   - **Use in-memory implementations**: For I/O, use InMemory* classes (e.g., InMemoryMessageProducer)
   - **Only test public exports**: Don't test private or internal methods

4. **Create/Update Test File**: Use Write or Edit tool to create the test

5. **Run the Test**: Use Bash to run: `dotnet test [test-project] --filter "FullyQualifiedName~When_[test_name]"`
   - Verify the test FAILS (Red)
   - The failure should be for the expected reason (behavior doesn't exist yet)

6. **Show Test to User**:
   - Display the test code
   - Explain what behavior it tests
   - Show the test failure output
   - Explain why this is the next logical step

#### ✅ USER APPROVAL: Get Approval for Test

**CRITICAL**: Before writing any implementation code, you MUST:

1. Use AskUserQuestion tool to ask: "I've written a failing test for [behavior]. The test verifies that [expected behavior]. Should I proceed to make this test pass?"

2. Wait for user approval

3. If user requests changes to the test:
   - Make the requested changes
   - Re-run the test to verify it still fails correctly
   - Ask for approval again

**DO NOT proceed to implementation without explicit user approval of the test.**

#### 🟢 GREEN: Make the Test Pass

1. **Read Code Style Guidelines**: Review [docs/agent_instructions/code_style.md](../../../docs/agent_instructions/code_style.md)

2. **Write Minimum Code** to make the test pass:
   - Only write code necessary for the test to pass
   - No speculative code
   - "Commit any sins necessary to move fast" - don't worry about perfect design yet
   - That comes in the Refactor step

3. **Follow Code Style** from code_style.md:
   - Use .NET C# naming conventions (PascalCase for public, camelCase for private)
   - Use ALL_CAPS for constants with underscores
   - Use expression-bodied members for simple properties/methods
   - Use readonly for fields that don't change after construction
   - Enable nullable reference types
   - Follow Responsibility Driven Design principles
   - Avoid primitive obsession

4. **Create/Update Implementation Files**: Use Write or Edit tool

5. **Run the Test Again**: `dotnet test [test-project] --filter "FullyQualifiedName~When_[test_name]"`
   - Verify the test PASSES (Green)

6. **Run All Tests**: `dotnet test` to ensure no regressions

7. **Show Results to User**:
   - Show what code was added/changed
   - Show the test now passes
   - Show all tests still pass

#### 🔵 REFACTOR: Improve the Design

1. **Review the Code** for design improvements:
   - Is it tidy and simple?
   - Can complexity be reduced?
   - Are there any code smells?
   - Does it follow Responsibility Driven Design?
   - Does it avoid primitive obsession?
   - Are methods small and focused?
   - Is there duplicated knowledge?
   - Is intention revealed clearly?

2. **Apply "Tidy First" Principles**:
   - Separate structural changes from behavioral changes
   - Make structural improvements (renaming, extracting methods, moving code)
   - Don't change behavior during refactoring

3. **Make Refactoring Changes**: Use Edit tool to improve the design
   - Keep methods small and focused on single responsibility
   - Extract methods if more than one level of indentation
   - Use expressive types instead of primitives
   - Distribute behavior appropriately

4. **Run All Tests After Each Refactoring**: Verify no behavioral changes
   - Tests should still pass
   - If a test breaks, the refactoring changed behavior (rollback)

5. **Show Refactoring to User**:
   - Explain what was refactored and why
   - Show the improved design
   - Confirm all tests still pass

### Step 5: Commit the Change

After completing Red-Green-Refactor for a behavior:

1. **Stage Changes**:
   ```bash
   git add [test-file] [implementation-files]
   ```

2. **Commit with Descriptive Message**:
   ```bash
   git commit -m "feat: [behavior description]

   - Test: When_[condition]_should_[expected_behavior]
   - Implementation: [brief description]

   Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
   ```

3. **Update Tasks**: Use Edit tool to check off completed task in `specs/{current-spec}/tasks.md`

### Step 6: Continue to Next Behavior

Ask user: "This behavior is complete. Should I continue to the next test, or would you like to review?"

- If continue: Return to Step 4 (Red-Green-Refactor cycle)
- If review: Show current progress and wait for next instruction

## Important Reminders

### Test-First Requirements

- **NEVER write implementation before writing a failing test**
- **ALWAYS get user approval of the test before implementing**
- Each test should represent the smallest possible behavioral step
- The next test should be the most obvious step toward implementing the requirement

### Code Quality Requirements

- Follow ALL guidelines in docs/agent_instructions/testing.md
- Follow ALL guidelines in docs/agent_instructions/code_style.md
- Keep changes small and incremental
- Each Red-Green-Refactor cycle should take minutes, not hours
- Commit frequently (after each successful cycle)

### Test Scope

- Only test public exports from assemblies
- Don't test private or internal implementation details
- Use in-memory implementations (InMemory*) for I/O, not mocks
- Tests should be coupled to behavior, not implementation

### Design Principles

- Responsibility Driven Design: Focus on "knowing", "doing", "deciding"
- Distribute behavior: Make objects smart
- Preserve flexibility: Interior details should be changeable
- Avoid primitive obsession: Use expressive types
- Keep methods small: Single responsibility, minimal indentation

## Example Session

```
🔴 RED Phase:
Creating test: When_message_is_invalid_should_send_to_dead_letter_queue.cs
[Shows test code]
Test fails with: "Method SendToDeadLetterQueue not found"

✅ USER APPROVAL:
Asking: Should I proceed to make this test pass?
User: Yes, proceed

🟢 GREEN Phase:
Adding SendToDeadLetterQueue method to KafkaMessageConsumer
[Shows implementation]
Test now passes ✓
All tests pass ✓

🔵 REFACTOR Phase:
Extracting message validation to IsValidMessage method
Renaming variable for clarity
[Shows refactored code]
All tests still pass ✓

✓ Committed: feat: add dead letter queue for invalid messages
✓ Updated tasks.md

Ready for next behavior!
```

Use Read, Write, Edit, Bash, and AskUserQuestion tools throughout the implementation process.