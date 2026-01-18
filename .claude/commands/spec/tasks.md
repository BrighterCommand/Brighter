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
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - [implementation point 1 with specific file/line numbers where applicable]
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
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In KafkaMessageConsumer.Reject() check if both producers are null
    - Log warning via NoChannelsConfiguredForRejection
    - Acknowledge message anyway to prevent reprocessing
    - Return true
```

### Why This Format?

1. **Visible command**: The `/test-first` command is prominently displayed
2. **Stop sign**: The ⛔ emoji and "STOP HERE" makes the approval gate unmissable
3. **Single task**: Combines TEST + IMPLEMENT so workflow is clear
4. **Complete context**: All details needed for test and implementation
5. **IDE review**: Explicitly states user will review in IDE, not CLI

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