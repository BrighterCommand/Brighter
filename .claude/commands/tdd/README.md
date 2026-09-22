# Test-Driven Development (TDD) Commands

This directory contains Claude Code commands that enforce Test-Driven Development workflows for the Brighter project.

## Commands

### `/test-first <behavior description>`

Guides you through the Red-Green-Refactor TDD cycle with an **approval gate before implementation
that is armed by default**.

**Purpose**: Ensures you write and approve tests before writing implementation code, preventing scope creep and promoting better design.

**Usage:**
```bash
/test-first when an invalid message is received it should be sent to the dead letter queue
```

**Workflow:**

1. **🔴 RED Phase**: Claude writes a failing test following Brighter's testing conventions
   - Uses BDD-style naming: `When_[condition]_should_[expected_behavior]`
   - One test per file
   - Arrange/Act/Assert structure
   - Tests public exports only
   - Uses InMemory* implementations instead of mocks

2. **✅ APPROVAL GATE**: Claude asks for your explicit approval
   - You must approve the test before implementation begins
   - You can request modifications to the test
   - Implementation only proceeds after approval
   - Armed by default — see *The review gear* below

3. **🟢 GREEN Phase**: Claude implements minimum code to pass the test
   - Only writes what's needed for this specific test
   - No speculative code
   - Follows Brighter's code style and documentation standards

4. **🔵 REFACTOR Phase**: Claude suggests design improvements (optional)
   - Structural changes only (no behavior changes)
   - Tests remain green throughout

**Why Use This?**

From [.agent_instructions/testing.md](../../../.agent_instructions/testing.md):
> You cannot bypass the gate on your own initiative. It is disarmed only by a deliberate,
> recorded, scoped gear shift the user makes with `/spec:gear`.

This command enforces that requirement automatically, ensuring:
- Tests correctly specify desired behavior before implementation
- Scope control - only code required by tests is written
- Better design - thinking about behavior first
- No speculative code
- Incremental progress through small, focused tests

## The review gear

The pause is a **gear**, not a policy ([ADR 0071](../../../docs/adr/0071-tdd-review-gear.md)). You
select it for the certainty you have and the blast radius you face: review every test while the
design is still moving; review a batch once the shape has been approved several times over; shift
back down the moment the work produces a test you would not have approved.

| Gear | Gate | Meaning |
|------|------|---------|
| `review-before` | ✅ armed | Approve each test before implementation. **The default.** |
| `review-after` | ➖ not armed | RED still proved first; reviewed as a batch afterwards. |

**`/test-first` itself is always gated.** It does not read the gear file — a standalone invocation
must never inherit a mode it did not ask for from whichever spec happens to be current. The gear is
resolved by `/spec:implement` and `/spec:ralph-implement`, which pass the result into this workflow
explicitly. `/bugfix:test` is always gated too.

Shift the gear with [`/spec:gear`](../spec/gear.md). It writes `specs/{spec}/.current-gear` —
untracked working state, scoped to one spec and optionally one section of `tasks.md`, carrying the
reason for the shift.

**`review-after` removes the human pause and nothing else.** These hold in both gears:

- **RED first** — the test is observed failing *for the right reason* before any production code.
  Ungated is not test-after.
- **The full regression suite**, not just the new test's own `--filter`.
- **The two-commit shape** — `feat:`/`test:` for the behaviour, then a separate `docs:` for any
  task-list tick.
- **Every test-authoring convention** — naming, one test per file, TestDoubles one class per file,
  a distinct request type per new test double, new closed generics registered with the test
  project's logging `Initializer.cs`, no mocks for isolation, `InMemory*` for I/O.

**Related Guidelines:**
- [Testing Guidelines](../../../.agent_instructions/testing.md)
- [Code Style](../../../.agent_instructions/code_style.md)
- [Documentation Standards](../../../.agent_instructions/documentation.md)

## Integration with Spec Workflow

The `/test-first` command can be used standalone or as part of the [specification workflow](../spec/README.md).

- **Standalone**: Use anytime you want to add behavior with TDD. **Always gated.**
- **With /spec:implement**: The spec implement command uses the same TDD workflow, in whichever gear
  the spec is currently in
- **With /spec:ralph-implement**: The same workflow again, run unattended in a loop over the same
  `tasks.md`, always `review-after`

## Example Session

```bash
$ /test-first when a message fails to send it should retry with exponential backoff

🔴 RED: Writing test...
[Claude shows test code]
Test fails: ✓ "RetryWithExponentialBackoff method not found"

✅ Should I proceed to implement the code to make this test pass?
> Yes

🟢 GREEN: Implementing RetryWithExponentialBackoff...
[Claude shows implementation]
✓ Test passes
✓ All tests pass

🔵 REFACTOR: Extracting backoff calculation...
[Claude shows refactoring]
✓ All tests still pass

Suggested commit: feat: add exponential backoff retry logic

Should I commit these changes?
```

## Best Practices

1. **Start small**: Write the simplest test that moves you toward your goal
2. **One behavior at a time**: Use `/test-first` multiple times to build up functionality
3. **Review the test carefully**: The test is your specification - make sure it's correct before approving
4. **Trust the process**: Don't skip ahead to implementation. If the gate has stopped earning its
   cost on a run of near-identical tasks, shift gear deliberately with `/spec:gear` — don't quietly
   stop honouring it
5. **Refactor regularly**: Take advantage of the refactor phase to improve design while tests are green

## Future Commands

Planned TDD commands:
- `/tdd:refactor` - Separate refactoring workflow following "Tidy First" principles (see `/tidy-first`)
- `/tdd:coverage` - Analyze test coverage and suggest missing tests
