# Test-Driven Development (TDD) Commands

This directory contains Claude Code commands that enforce Test-Driven Development workflows for the Brighter project.

## Commands

### `/test-first <behavior description>`

Guides you through the Red-Green-Refactor TDD cycle with a **mandatory approval gate** before implementation.

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

3. **🟢 GREEN Phase**: Claude implements minimum code to pass the test
   - Only writes what's needed for this specific test
   - No speculative code
   - Follows Brighter's code style and documentation standards

4. **🔵 REFACTOR Phase**: Claude suggests design improvements (optional)
   - Structural changes only (no behavior changes)
   - Tests remain green throughout

**Why Use This?**

From [.agent_instructions/testing.md](../../../.agent_instructions/testing.md):
> The approval step is MANDATORY when working with an AI coding assistant

This command enforces that requirement automatically, ensuring:
- Tests correctly specify desired behavior before implementation
- Scope control - only code required by tests is written
- Better design - thinking about behavior first
- No speculative code
- Incremental progress through small, focused tests

**Related Guidelines:**
- [Testing Guidelines](../../../.agent_instructions/testing.md)
- [Code Style](../../../.agent_instructions/code_style.md)
- [Documentation Standards](../../../.agent_instructions/documentation.md)

## Integration with Spec Workflow

The `/test-first` command can be used standalone or as part of the [specification workflow](../spec/README.md).

- **Standalone**: Use anytime you want to add behavior with TDD
- **With /spec:implement**: The spec implement command uses the same TDD workflow with approval gates

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
4. **Trust the process**: Don't skip ahead to implementation - the approval gate is there for a reason
5. **Refactor regularly**: Take advantage of the refactor phase to improve design while tests are green

## Future Commands

Planned TDD commands:
- `/tdd:refactor` - Separate refactoring workflow following "Tidy First" principles (see `/tidy-first`)
- `/tdd:coverage` - Analyze test coverage and suggest missing tests
