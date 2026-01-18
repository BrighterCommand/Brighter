# Phase 6 Tasks - Before and After Comparison

## Current Format (BEFORE)

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

- [ ] **TEST: Async rejection with no channels configured acknowledges message**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Write test: When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log_async
  - Configure consumer without DLQ or invalid message routing keys
  - RejectAsync message
  - Verify message acknowledged and warning logged
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: No channels configured behavior**
  - Handle null producers case
  - Log warning
  - Acknowledge anyway
  - Make the test pass

- [ ] **TEST: Unknown rejection reason routes to DLQ**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_unknown_reason_should_send_to_dlq
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Unknown reason → DLQ routing**
  - Make the test pass

- [ ] **TEST: Async unknown rejection reason routes to DLQ**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Write test: When_rejecting_message_with_unknown_reason_should_send_to_dlq_async
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Unknown reason → DLQ routing**
  - Make the test pass

- [ ] **TEST: DLQ topic creation follows MakeChannels strategy**
  - Write test: When_creating_dlq_producer_with_make_channels_create_should_create_topic
  - Test MakeChannels.Create, Validate, and Assume modes
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: MakeChannels strategy inheritance**
  - Ensure DLQ producer inherits MakeChannels from consumer config
  - Make the test pass
```

### Problems with Current Format

1. ❌ TEST and IMPLEMENT are separate - allows skipping approval
2. ❌ No `/test-first` command shown - Claude writes tests manually
3. ❌ "APPROVAL REQUIRED" is buried in bullets - easy to miss
4. ❌ No stop sign or visual indicator
5. ❌ Async variants repeat everything - verbose

---

## Proposed Format (AFTER)

```markdown
### Phase 6: Edge Cases and Error Handling

#### No Channels Configured Behavior

- [ ] **TEST + IMPLEMENT: Rejection with no channels configured (Sync)**
  - **USE COMMAND**: `/test-first when rejecting message with no channels configured should acknowledge and log`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`
  - Test should verify:
    - Consumer created without deadLetterRoutingKey or invalidMessageRoutingKey parameters
    - Send two messages to data topic
    - Consume and reject first message with DeliveryError reason
    - Verify Reject() returns true even with no channels
    - Verify second message can be consumed (proving first was acknowledged)
    - Warning should be logged with message ID and rejection reason
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumer.Reject()` line ~515
    - Check: `if (reason == null || (_deadLetterProducer == null && _invalidMessageProducer == null))`
    - Log warning: `NoChannelsConfiguredForRejection(s_logger, message.Header.MessageId, reason.RejectionReason)`
    - Call `Acknowledge(message)` to prevent reprocessing
    - Return `true`

- [ ] **TEST + IMPLEMENT: Rejection with no channels configured (Async)**
  - **USE COMMAND**: `/test-first when rejecting message async with no channels configured should acknowledge and log`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log_async.cs`
  - Test should verify:
    - Same behavior as sync test but using async methods
    - Use `RejectAsync()`, `ConsumeMessageAsync()`, `AcknowledgeAsync()`
    - Consumer created with MessagePumpType.Proactor
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumer.RejectAsync()` line ~584
    - Same logic as Reject() but using async producer checks
    - Check: `if (reason == null || (_deadLetterProducerAsync == null && _invalidMessageProducerAsync == null))`
    - Await `AcknowledgeAsync(message, cancellationToken)`
    - Return `true`

#### Unknown Rejection Reason Handling

- [ ] **TEST + IMPLEMENT: Unknown rejection reason routes to DLQ (Sync)**
  - **USE COMMAND**: `/test-first when rejecting message with unknown reason should send to dlq`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor`
  - Test file: `When_rejecting_message_with_unknown_reason_should_send_to_dlq.cs`
  - Test should verify:
    - Consumer created WITH deadLetterRoutingKey configured
    - Send message to data topic
    - Reject with `RejectionReason.None` (unknown reason)
    - Verify message appears on DLQ topic (consume from DLQ)
    - Verify rejection metadata includes: OriginalTopic, RejectionReason="None"
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumer.DetermineRejectionRoute()` line ~932
    - The `default:` case should handle unknown reasons
    - Route to DLQ if available: `if (hasDeadLetterProducer) return (_deadLetterRoutingKey, true, false)`
    - This catches `RejectionReason.None` and any future unknown values

- [ ] **TEST + IMPLEMENT: Unknown rejection reason routes to DLQ (Async)**
  - **USE COMMAND**: `/test-first when rejecting message async with unknown reason should send to dlq`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor`
  - Test file: `When_rejecting_message_with_unknown_reason_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same behavior as sync test but using async methods
    - Use `RejectAsync()` and await DLQ consumer operations
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Uses same `DetermineRejectionRoute()` method (shared by sync/async)
    - Already implemented by sync version - test should pass with existing code

#### MakeChannels Strategy Inheritance

- [ ] **TEST + IMPLEMENT: DLQ topic creation follows MakeChannels strategy**
  - **USE COMMAND**: `/test-first when creating dlq producer with make channels create should create topic`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor`
  - Test file: `When_creating_dlq_producer_with_make_channels_create_should_create_topic.cs`
  - Test should verify:
    - Create consumer with `OnMissingChannel.Create` and deadLetterRoutingKey
    - DLQ topic does NOT exist initially
    - Reject a message (triggers lazy DLQ producer creation)
    - Verify DLQ topic was automatically created
    - Verify message appears on DLQ (consume from DLQ consumer)
    - This proves MakeChannels.Create was inherited and applied
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumer.CreateDeadLetterProducer()` line ~827
    - Publication must inherit: `MakeChannels = MakeChannels`
    - When `producer.Init()` is called, it creates topic if MakeChannels=Create
    - Already implemented - test should verify existing behavior
```

### Benefits of New Format

1. ✅ **USE COMMAND** line is prominent - impossible to miss
2. ✅ Single TEST + IMPLEMENT task - clear workflow
3. ✅ Stop sign ⛔ with "WAIT FOR USER APPROVAL **in IDE**"
4. ✅ Explicit file paths and line numbers for implementation
5. ✅ Test verification points are complete - Claude knows what to test
6. ✅ Implementation guidance is specific - Claude knows where to change code
7. ✅ Grouped by feature - easier to understand related tests
8. ✅ Notes when implementation already exists - reduces confusion

---

## What Claude Does With New Format

### Step 1: Claude reads task
```markdown
- [ ] **TEST + IMPLEMENT: Rejection with no channels configured (Sync)**
  - **USE COMMAND**: `/test-first when rejecting message with no channels configured should acknowledge and log`
```

Claude sees the command and knows to use the skill.

### Step 2: Claude invokes skill
```
I'll use the /test-first skill to implement this behavior following the TDD workflow.
```
(Calls Skill tool with: `skill: "tdd:test-first", args: "when rejecting message with no channels configured should acknowledge and log"`)

### Step 3: Skill writes test
The skill writes the test file and shows it to the user.

### Step 4: Skill asks for approval
```
🔴 RED: Test written and fails as expected.

Test location: tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs

✅ Should I proceed to implement the code to make this test pass?
```

### Step 5: User reviews in IDE
User opens the file in their IDE, reviews the test, and types "yes" or requests changes.

### Step 6: Claude implements
Only after approval, Claude implements the changes to make the test pass.

### Step 7: Commit
Claude commits with appropriate message.

---

## Comparison Summary

| Aspect | Before | After |
|--------|--------|-------|
| **Command visibility** | Hidden/absent | **USE COMMAND** line at top |
| **Approval gate** | "APPROVAL REQUIRED" buried | **⛔ STOP HERE** with stop sign |
| **User review location** | Unclear (CLI?) | **in IDE** explicitly stated |
| **Task structure** | Separate TEST/IMPLEMENT | Combined TEST + IMPLEMENT |
| **Implementation guidance** | Vague bullets | Specific line numbers and logic |
| **Test verification** | Minimal | Complete list of assertions |
| **Async variants** | Repeat everything | Note what's different |
| **Easy to skip?** | Yes ❌ | No ✅ |

The new format makes the TDD workflow automatic and impossible to bypass.
