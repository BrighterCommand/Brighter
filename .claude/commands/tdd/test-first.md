---
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, AskUserQuestion
description: TDD workflow with an approval gate before implementation (armed by default)
argument-hint: <behavior description>
---

# Test-First Development (TDD with Approval Gate)

You are guiding the user through a Test-Driven Development workflow with an **approval gate** before
implementation. The gate is **armed by default** — assume it is armed unless a spec command has
explicitly told you otherwise (see *Review gear*, below).

## The Behavior to Test

$ARGUMENTS

## Review gear

Which review mode applies is called the **gear** (see
[ADR 0071](../../../docs/adr/0071-tdd-review-gear.md)):

| Gear | Gate | Meaning |
|------|------|---------|
| `review-before` | ✅ armed | Stop after the test and get explicit approval before implementing. **The default.** |
| `review-after` | ➖ not armed | Write the test, prove RED, then implement without pausing. The user reviews the batch afterwards. |

**This command runs `review-before` unless the invoking command states otherwise in its prompt.**

- Invoked directly by the user (`/test-first <behavior>`) → **`review-before`**, always. Do **not**
  read `specs/*/.current-gear` yourself. A standalone run must never inherit a mode it did not ask
  for from whichever spec happens to be current.
- Invoked by `/bugfix:test` → **`review-before`**, always. Bugfixes are outside this mechanism.
- Invoked by `/spec:implement` or `/spec:ralph-implement` → those commands resolve the gear for the
  spec and the specific task, and pass the result in explicitly. Honour what they pass.

If you are ever unsure which gear applies, it is `review-before`. Fail safe, never fail open.
The user can shift the gear for a spec with `/spec:gear`.

## What the gear does NOT change

`review-after` removes the **human approval pause** and nothing else. Every one of these holds in
both gears:

- **RED first.** Write the test and observe it fail *for the right reason* before any production
  code exists. Ungated is not test-after.
- **The full regression suite**, not just the new test's own `--filter`.
- **The two-commit shape**: a `feat:`/`test:` commit for the behaviour, then a separate `docs:`
  commit for any task-list checkbox.
- **Every test-authoring convention below**, plus the standing ones in
  [.agent_instructions/testing.md](../../../.agent_instructions/testing.md): TestDoubles one class
  per file; a distinct request type per new test double so assembly scans do not collide; new closed
  generics registered with each test project's logging `Initializer.cs`.

## Workflow Phases

### 🔴 RED Phase - Write Failing Test

**Your task:** Write a test that specifies the desired behavior, following the guidelines in [.agent_instructions/testing.md](../../../.agent_instructions/testing.md).

**Test Requirements:**
1. **Naming Convention**: Method names use `When_[condition]_should_[expected_behavior]`. Class names use `[Behavior]Tests` — never use `When_` for class names.
2. **File Structure**: One test per file: `When_[condition]_should_[expected_behavior].cs`. If multiple tests share a file, name the file after the happy path test.
3. **Test Structure**: Use Arrange/Act/Assert with explicit comments
4. **Evident Data**: Highlight only the data that impacts the test outcome
5. **Test Exports Only**: Test public methods on public classes only
6. **Developer Test Style**: Do NOT use mocks for isolation - we test behaviors, not classes
7. **I/O Substitution**: Use InMemory* implementations for I/O (e.g., InMemoryMessageProducer, InMemoryOutbox)

**Steps:**
1. Identify the exact behavior being tested
2. Determine which public class/method will provide this behavior
3. Write the test following the xUnit BDD-style naming
4. Run the test to verify it fails for the right reason
5. Show the test code and failure message to the user

**After writing the test, proceed to the Approval Gate.**

---

### ✅ APPROVAL GATE - User Must Approve Test

**This phase runs when the gear is `review-before` — which is the default, and which you should
assume unless the invoking command explicitly said `review-after`.**

**CRITICAL: You MUST get explicit user approval before proceeding to implementation.**

Use the AskUserQuestion tool to ask:

```
Question: "Should I proceed to implement the code to make this test pass?"
Options:
1. "Yes, implement the code" - Proceed to GREEN phase
2. "Modify the test first" - User will explain changes needed
3. "Cancel" - Stop the workflow
```

**If user requests modifications:**
- Make the requested changes to the test
- Run it again to verify it still fails appropriately
- Ask for approval again

**Do NOT proceed to implementation without explicit approval.**

#### When the gear is `review-after`

Skip the pause — and **only** the pause. Instead:

1. Confirm you have already proved RED (the test ran and failed for the right reason). If you have
   not, go back and do it; `review-after` never licenses writing implementation first.
2. Print one line naming the gear and where it came from, so the run is self-documenting, e.g.
   `➖ review-after (specs/0027-…/.current-gear, Phase 5) — proceeding to GREEN without pausing`.
3. Proceed to GREEN.

If anything about the test looks wrong to you — it asserts something the task did not ask for, it
needs a design decision you cannot make, it duplicates an existing test — **stop and ask anyway**.
`review-after` is a default for the routine case, not a gag order.

---

### 🟢 GREEN Phase - Make Test Pass

**Under `review-before`, only execute this phase after receiving approval.** Under `review-after`,
proceed once RED is proved.

**Your task:** Write the **minimum code** necessary to make the test pass.

**Code Requirements** (from [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md)):
1. Follow .NET C# naming conventions
2. Use Responsibility Driven Design principles
3. Avoid primitive obsession - use expressive types
4. Keep methods small and focused on a single responsibility
5. Support both sync and async I/O where appropriate
6. Enable nullable reference types
7. Add MIT license header to new files

**Documentation Requirements** (from [.agent_instructions/documentation.md](../../../.agent_instructions/documentation.md)):
1. Add XML documentation comments (`///`) for all public members
2. Use `<summary>`, `<param>`, `<returns>`, `<exception>` tags
3. Add `<remarks>` for complex implementation details

**Steps:**
1. Write ONLY the code needed to make this specific test pass
2. Do NOT write speculative code for future requirements
3. Run the test to verify it passes
4. Run all related tests to ensure no regressions
5. Show the implementation and test results to the user

---

### 🔵 REFACTOR Phase - Improve Design (Optional)

**Your task:** Review the implementation for potential improvements while keeping tests green.

**Refactoring Principles:**
1. Keep methods small (avoid more than one level of indentation)
2. Extract methods to express intent
3. Replace primitives with expressive types where appropriate
4. Ensure clear responsibility assignment
5. Simplify conditional logic

**IMPORTANT - Tidy First Approach** (from [.agent_instructions/code_style.md](../../../.agent_instructions/code_style.md)):
- Only make STRUCTURAL changes (renaming, extracting methods, moving code)
- Do NOT change behavior during refactoring
- Run all tests after each refactoring to verify behavior unchanged

**Steps:**
1. Identify potential design improvements
2. Make one refactoring at a time
3. Run all tests after each change
4. Show the refactored code with explanation

---

## Completion

After completing all phases:

1. **Suggest a commit message** following conventional commits format:
   - `test: add test for [behavior]` (if only test was committed before approval)
   - `feat: [description]` (for new feature implementation)
   - `fix: [description]` (for bug fix implementation)
   - `refactor: [description]` (for refactoring changes)

2. **Ask the user:**
   - "Should I commit these changes?"
   - "Do you want to write another test for the next behavior?"

---

## Key Principles

**From testing.md:**
- The approval step is **armed by default** and is only disarmed by a deliberate, scoped, recorded
  gear shift (`/spec:gear`) — never by assumption, convenience, or a prose instruction in a scratch
  file
- Tests should specify behavior, not implementation details
- Only write code required by tests - no speculative code
- Tests should use the Arrange/Act/Assert structure
- Use developer tests (not unit tests) - tests implicate the most recent edit

**From code_style.md:**
- Follow Responsibility Driven Design
- Maximize abstraction - think in terms of roles and responsibilities
- Distribute behavior - make objects smart
- Preserve flexibility - interior details should be readily changeable
- Avoid primitive obsession

**Scope Control:**
- Write the most obvious test to move towards the requirement
- Only implement what's needed for the current test
- Each test should be a small, focused step forward

---

## Example Session

```
User: /test-first when an invalid message is received it should be sent to the dead letter queue

🔴 RED: Writing test When_message_is_invalid_should_send_to_dead_letter_queue

[Shows test code]
Test file: tests/Paramore.Brighter.Kafka.Tests/When_message_is_invalid_should_send_to_dead_letter_queue.cs

Running test...
Test fails: ✓ "Method SendToDeadLetterQueue not found on KafkaMessageConsumer"

✅ APPROVAL REQUIRED
Should I proceed to implement the code to make this test pass?

User: Yes

🟢 GREEN: Implementing SendToDeadLetterQueue method

[Shows implementation code with XML docs]

Running tests...
✓ When_message_is_invalid_should_send_to_dead_letter_queue PASSED
✓ All 47 tests in Paramore.Brighter.Kafka.Tests PASSED

🔵 REFACTOR: Extracting validation logic into IsValidMessage method

[Shows refactoring]

Running tests...
✓ All 47 tests PASSED

Suggested commit message:
feat: send invalid messages to dead letter queue

Should I commit these changes?
```

---

## Notes

- This workflow enforces the TDD approval requirement from .agent_instructions/testing.md
- The approval gate ensures the test correctly specifies desired behavior before implementation
- The gate is armed by default; `/spec:gear` is the only supported way to disarm it, and it does so
  for one spec (optionally one phase) at a time — see ADR 0071
- Following this workflow provides scope control and better design
- You may run `/test-first` multiple times to build up functionality incrementally
