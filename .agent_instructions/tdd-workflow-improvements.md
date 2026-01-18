# Proposed Changes to Enforce TDD Approval Workflow

## Problem

Claude bypassed the mandatory approval workflow in Phase 6 by:
1. Writing tests directly instead of using `/test-first`
2. Running tests and implementing without stopping for approval
3. Treating "APPROVAL REQUIRED" as guidance rather than a hard gate

## Solution

Make `/test-first` skill usage mandatory and impossible to miss.

---

## 1. `.claude/commands/spec/tasks.md` - Enforce skill in task generation

### BEFORE
```markdown
---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Create implementation task list
---

## Context

Current spec directory: specs/

## Your Task

First, read specs/.current_spec to determine the active specification directory.

1. Verify design is approved (look for .design-approved file in the spec directory)
2. Create tasks.md with:
   - Detailed task list with checkboxes
   - Task dependencies
   - Risk mitigation tasks
3. Each task should be specific and actionable
4. A task MUST hould represent implementing a behavior and NOT an implementation detail
4. Use markdown checkboxes: `- [ ] Task description`

Organize tasks to enable incremental development and testing.
```

### AFTER
```markdown
---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Create implementation task list
---

## Context

Current spec directory: specs/

## Your Task

First, read specs/.current_spec to determine the active specification directory.

1. Verify design is approved (look for .design-approved file in the spec directory)
2. Create tasks.md with:
   - Detailed task list with checkboxes
   - Task dependencies
   - Risk mitigation tasks
3. Each task should be specific and actionable
4. A task MUST represent implementing a behavior and NOT an implementation detail
5. Use markdown checkboxes: `- [ ] Task description`

Organize tasks to enable incremental development and testing.

## CRITICAL: TDD Task Format

**MANDATORY**: When creating TEST tasks, you MUST format them to enforce `/test-first` skill usage:

### Task Template

```markdown
- [ ] **TEST + IMPLEMENT: [Behavior description]**
  - **USE COMMAND**: `/test-first [behavior description for command]`
  - Test location: "[test directory path]"
  - Test file: `[When_condition_should_behavior.cs]`
  - Test should verify:
    - [verification point 1]
    - [verification point 2]
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL before implementing**
  - Implementation should:
    - [implementation point 1]
    - [implementation point 2]
```

### Example Task

```markdown
- [ ] **TEST + IMPLEMENT: Rejection with no channels configured acknowledges message**
  - **USE COMMAND**: `/test-first when rejecting message with no channels configured should acknowledge and log`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`
  - Test should verify:
    - Consumer created without DLQ or invalid message routing keys
    - Message rejected with DeliveryError reason
    - Message acknowledged (can consume next message)
    - Warning logged
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL before implementing**
  - Implementation should:
    - Check if both producers are null
    - Log warning via NoChannelsConfiguredForRejection
    - Acknowledge message anyway
    - Return true
```

### Why This Format?

1. **Visible command**: The `/test-first` command is prominently displayed
2. **Stop sign**: The ⛔ emoji and "STOP HERE" makes the gate unmissable
3. **Single task**: Combines TEST + IMPLEMENT so workflow is clear
4. **Complete context**: All details needed for test and implementation

### DO NOT Format Tasks Like This

❌ **BAD - Separates test and implementation:**
```markdown
- [ ] **TEST: Rejection with no channels configured**
  - Write test...
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: No channels configured behavior**
  - Handle null producers...
```

This format allows Claude to skip the approval by treating them as independent tasks.
```

---

## 2. `CLAUDE.md` - Add explicit TDD requirement

### BEFORE
```markdown
## Claude Code Skills (Recommended)

Claude Code skills automate common workflows and enforce mandatory engineering practices. **Use these skills proactively** rather than manually following documented procedures:
```

### AFTER
```markdown
## Claude Code Skills (MANDATORY)

Claude Code skills automate common workflows and enforce mandatory engineering practices. **You MUST use these skills** - they are not optional:

### ⛔ TDD Workflow (MANDATORY - NOT OPTIONAL)

When working on implementation tasks in `specs/*/tasks.md`:

- **ALWAYS use `/test-first <behavior>`** for TEST tasks
- **NEVER write tests manually and proceed to implementation**
- **STOP and ASK FOR APPROVAL** after writing each test
- The user will review the test in their IDE before you implement
- Each TEST task in tasks.md specifies the exact `/test-first` command to use
- The skill enforces the approval gate automatically - you cannot bypass it

**Why this is mandatory:**
1. Tests correctly specify desired behavior before implementation
2. Scope control - only code required by tests is written
3. No speculative code
4. User reviews test in IDE, not in CLI output

**If a task says `/test-first when ...`** - YOU MUST USE THAT COMMAND. Do not write the test file manually.

## Claude Code Skills (Recommended)

Claude Code skills automate common workflows and enforce mandatory engineering practices. **Use these skills proactively** rather than manually following documented procedures:
```

---

## 3. `.agent_instructions/testing.md` - Strengthen the mandate

### BEFORE (line 13)
```markdown
**Recommended Tool**: Use the `/test-first <behavior>` command (see [.claude/commands/tdd/test-first.md](../../.claude/commands/tdd/test-first.md)) to enforce the TDD approval workflow automatically. This ensures the mandatory approval step is never skipped.
```

### AFTER (line 13)
```markdown
**MANDATORY Tool**: ALWAYS use the `/test-first <behavior>` command (see [.claude/commands/tdd/test-first.md](../../.claude/commands/tdd/test-first.md)) when writing new tests.

- **DO NOT write test files manually** (using Write tool) and proceed to implementation
- **DO NOT run tests without approval**
- **STOP after writing the test and ASK FOR APPROVAL**
- The user will review the test in their IDE, not in CLI output
- This is NOT optional - the approval gate is MANDATORY when working with Claude Code

This ensures the mandatory approval step is never skipped and tests are reviewed before implementation.
```

### BEFORE (line 21-28)
```markdown
- **Approval Workflow**:
  - When working on a feature, write the test first and get approval before implementing
  - This ensures the test correctly specifies the desired behavior
  - The approval step is MANDATORY when working with an AI coding assistant
  - After approval, implement the minimum code to make the test pass
```

### AFTER (line 21-28)
```markdown
- **Approval Workflow** (⛔ MANDATORY - NOT OPTIONAL):
  - When working on a feature, ALWAYS use `/test-first <behavior>` - do not write tests manually
  - The skill will write the test and ASK FOR APPROVAL before proceeding
  - The user will review the test in their IDE
  - DO NOT run tests or start implementation without explicit user approval
  - After approval, implement the minimum code to make the test pass
  - The approval step is MANDATORY when working with Claude Code - you cannot bypass it
```

---

## 4. `specs/0001-kafka-dead-letter-queue/tasks.md` - Example reformatting

### BEFORE
```markdown
### Phase 6: Edge Cases and Error Handling

- [ ] **TEST: Rejection with no channels configured acknowledges message**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log
  - Configure consumer without DLQ or invalid message routing keys
  - Reject message
  - Verify message acknowledged and warning logged
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: No channels configured behavior**
  - Handle null producers case
  - Log warning
  - Acknowledge anyway
  - Make the test pass
```

### AFTER
```markdown
### Phase 6: Edge Cases and Error Handling

- [ ] **TEST + IMPLEMENT: Rejection with no channels configured acknowledges message (Sync)**
  - **USE COMMAND**: `/test-first when rejecting message with no channels configured should acknowledge and log`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`
  - Test should verify:
    - Consumer created without deadLetterRoutingKey or invalidMessageRoutingKey
    - Send two messages to data topic
    - Reject first message with DeliveryError reason
    - Verify rejection returns true
    - Verify second message can be consumed (proving first was acknowledged)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL before implementing**
  - Implementation should:
    - In KafkaMessageConsumer.Reject() check if both producers are null
    - If null, log warning: NoChannelsConfiguredForRejection
    - Acknowledge message anyway to prevent reprocessing
    - Return true
```

---

## 5. Additional System Prompt Enhancement

Consider adding to the system prompt or prominent location:

```markdown
## ⛔ CRITICAL: TDD Approval Gate

When you see a task that says:
- **USE COMMAND**: `/test-first [behavior]`

You MUST:
1. ✅ Use the exact command shown
2. ✅ Write the test using the skill
3. ✅ STOP and wait for user approval
4. ❌ DO NOT write test files manually with Write tool
5. ❌ DO NOT run tests without approval
6. ❌ DO NOT proceed to implementation without approval

The user will review tests in their IDE, not CLI output.
```

---

## Summary of Changes

| File | Change | Impact |
|------|--------|--------|
| `.claude/commands/spec/tasks.md` | Add TDD task format template | Claude generates tasks with `/test-first` commands |
| `CLAUDE.md` | Add mandatory TDD section at top | Makes requirement unmissable |
| `.agent_instructions/testing.md` | Strengthen "MANDATORY" language | Removes ambiguity |
| `specs/*/tasks.md` | Reformat tasks with commands | Shows exact command to use |

## Expected Behavior After Changes

When Claude sees:
```markdown
- [ ] **TEST + IMPLEMENT: Feature X**
  - **USE COMMAND**: `/test-first when feature x should do y`
```

Claude will:
1. Invoke the `/test-first` skill with that exact command
2. The skill writes the test
3. The skill asks: "Should I proceed to implement?"
4. Claude waits for "yes" before implementing
5. User reviews test in IDE during the wait

This makes the approval gate automatic and impossible to bypass.
