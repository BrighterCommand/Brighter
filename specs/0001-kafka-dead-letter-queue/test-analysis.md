# Test Analysis for Kafka DLQ Implementation

## Summary

**Finding**: There are NO existing tests for rejection in the Kafka test suite. However, there are:
1. Good patterns in existing Kafka tests we can follow
2. Reusable test doubles from Core tests that handle RejectMessageAction
3. Existing InMemory tests that verify DLQ behavior works at the Dispatcher level

## Existing Test Infrastructure

### Test Doubles Available for Reuse

From `tests/Paramore.Brighter.Core.Tests/MessageDispatch/TestDoubles/`:

1. **MyRejectedEvent.cs** - Simple event class
2. **MyRejectedEventHandler.cs** - Throws `RejectMessageAction("Test of rejection flow")`
3. **MyRejectedEventHandlerAsync.cs** - Async version that throws `RejectMessageAction`
4. **MyRejectedEventHandlerMessageMapper.cs** - Maps event to message
5. **MyRejectedEventHandlerMessageMapperAsync.cs** - Async version

**Pattern from MyRejectedEventHandler:**
```csharp
public class MyRejectedEventHandler : RequestHandler<MyRejectedEvent>
{
    public override MyRejectedEvent Handle(MyRejectedEvent myRejectedEvent)
    {
        throw new RejectMessageAction("Test of rejection flow");
    }
}
```

### Existing Rejection Tests (InMemory Transport)

**File**: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/When_an_event_handler_throw_a_reject_message_exception.cs`

**What it tests:**
- Creates InMemorySubscription with `DeadLetterRoutingKey` set
- Handler throws `RejectMessageAction`
- Verifies message removed from main routing key
- Verifies message appears on dead letter routing key
- Verifies rejection reason in message header bag

**Key insight:** InMemorySubscription already supports `DeadLetterRoutingKey` property (line 58), so the infrastructure exists at the Subscription level.

### Kafka Test Patterns

From `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/`:

**Common Pattern:**
1. Create `KafkaProducerRegistryFactory` with configuration pointing to `localhost:9092`
2. Create `KafkaMessageConsumerFactory` with subscription
3. Send messages via producer
4. Receive messages via consumer
5. Call `consumer.Acknowledge(message)` or `consumer.Reject(message)`
6. Use `Task.Delay()` to allow Kafka propagation
7. Verify behavior

**Example test structure (from When_a_message_is_acknowledged_update_offset.cs):**
- Uses `[Trait("Category", "Kafka")]` to mark Kafka tests
- Uses `[Collection("Kafka")]` to prevent parallel execution
- Uses `ITestOutputHelper` for debugging
- Creates unique topic names with `Guid.NewGuid().ToString()`
- Disposes resources properly with `IDisposable`
- Handles `ChannelFailureException` during topic propagation

## What We Need to Create for Kafka DLQ Tests

### New Test Doubles Needed

**In `tests/Paramore.Brighter.Kafka.Tests/TestDoubles/`:**

1. **MyKafkaRejectedCommand.cs** - Command that will be rejected
   - Similar to existing MyCommand.cs but specific to rejection tests
   - Can reuse MyCommand if we just need the type

2. **MyKafkaRejectedCommandHandler.cs** - Handler that throws RejectMessageAction
   - Sync version
   - Pattern: `throw new RejectMessageAction("DLQ test rejection");`

3. **MyKafkaRejectedCommandHandlerAsync.cs** - Async handler
   - Async version
   - Pattern: `throw new RejectMessageAction("DLQ test rejection async");`

**Alternative:** We could copy/adapt the existing Core test doubles into the Kafka test project.

### New Integration Tests Needed

Based on tasks.md phases, we need these Kafka integration tests:

#### Phase 3: Message Rejection to DLQ
- **When_rejecting_message_with_delivery_error_should_send_to_dlq.cs**
  - Create consumer with `deadLetterRoutingKey` configured
  - Send message to data topic
  - Handler throws `RejectMessageAction` with `MessageRejectionReason.DeliveryError`
  - Verify message appears on DLQ topic
  - Verify message NOT on original topic (offset committed)
  - Verify metadata enrichment (OriginalTopic, RejectionReason, etc.)

#### Phase 4: Invalid Message Routing
- **When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs**
  - Configure consumer with `invalidMessageRoutingKey`
  - Reject with `MessageRejectionReason.Unacceptable`
  - Verify message on invalid message topic

- **When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs**
  - Configure only DLQ, not invalid message channel
  - Reject with `MessageRejectionReason.Unacceptable`
  - Verify message falls back to DLQ

#### Phase 5: Edge Cases
- **When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs**
  - No DLQ or invalid message routing keys
  - Reject message
  - Verify message acknowledged (offset committed)
  - Verify warning logged

- **When_rejecting_message_with_unknown_reason_should_send_to_dlq.cs**
  - Reject with `MessageRejectionReason.Unknown`
  - Verify routes to DLQ

- **When_creating_dlq_producer_with_make_channels_create_should_create_topic.cs**
  - Test `MakeChannels.Create`, `Validate`, and `Assume` modes
  - Verify DLQ topic created/validated according to strategy

#### Phase 6: Async Support
- **When_rejecting_message_async_with_delivery_error_should_send_to_dlq.cs**
  - Same as Phase 3 but using `KafkaMessageConsumerAsync`

## Test Strategy Recommendations

### Option 1: Direct Consumer Tests (Lower Level)
Test the consumer's `Reject()` method directly without going through the full message pump:
- Create `KafkaMessageConsumer` with DLQ routing keys
- Send message to topic
- Receive message
- Call `consumer.Reject(message, MessageRejectionReason.DeliveryError)`
- Create second consumer on DLQ topic
- Verify message appears there

**Pros:**
- Direct test of the behavior we're implementing
- Faster execution (no message pump overhead)
- Easier to debug

**Cons:**
- Doesn't test full integration with Dispatcher and handlers

### Option 2: Full Integration Tests (Higher Level)
Test through the Dispatcher with real handlers that throw RejectMessageAction:
- Similar to Core tests but with Kafka transport
- Use `Dispatcher` with `KafkaSubscription`
- Handler throws `RejectMessageAction`
- Verify message on DLQ

**Pros:**
- Tests full integration
- More realistic

**Cons:**
- More complex setup
- Harder to isolate failures

### Recommendation: Start with Option 1

Follow the TDD workflow:
1. Start with **Option 1** (direct consumer tests) for initial implementation
2. These tests will drive the implementation of:
   - Consumer constructor parameters
   - Lazy producer creation
   - Reject() method routing logic
   - Message enrichment
3. Later, add **Option 2** tests as higher-level verification

This follows the existing Kafka test pattern which tests consumer behavior directly.

## Test Doubles to Create (First Phase)

For Phase 1 (naming conventions), we only need unit tests with no test doubles.

For Phase 2 (KafkaSubscription), we need unit tests verifying properties.

For Phase 3+ (consumer implementation), we need:

1. **Copy from Core tests (recommended):**
   - Copy MyRejectedEvent to Kafka.Tests/TestDoubles/MyKafkaRejectedCommand.cs
   - Adapt to Command instead of Event
   - No handler needed for direct consumer tests

2. **Or create minimal test double:**
   ```csharp
   // tests/Paramore.Brighter.Kafka.Tests/TestDoubles/MyKafkaRejectedCommand.cs
   internal sealed class MyKafkaRejectedCommand : Command
   {
       public MyKafkaRejectedCommand() : base(Guid.NewGuid()) {}
       public string Value { get; set; }
   }
   ```

## Next Steps

1. ✅ Review this analysis with user
2. Start Phase 1: Write naming convention tests (no Kafka needed)
3. Start Phase 2: Write KafkaSubscription property tests (no Kafka needed)
4. Start Phase 3: Write first integration test for DLQ rejection
   - Create test double command
   - Write failing test
   - Get approval
   - Implement consumer changes

## Notes

- All integration tests must use docker-compose-kafka.yaml
- Tests use `localhost:9092` for Kafka broker
- Use unique topic names with `Guid.NewGuid().ToString()` to avoid conflicts
- Mark tests with `[Trait("Category", "Kafka")]`
- Use `[Collection("Kafka")]` to prevent parallel execution
- Handle `ChannelFailureException` during topic propagation with retries
- Add delays (`Task.Delay`) to allow Kafka topic/message propagation
