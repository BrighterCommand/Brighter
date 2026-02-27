# Implementation Tasks

This document outlines the tasks for implementing the Kafka Dead Letter Queue feature as specified in the requirements and ADRs (0045, 0046, 0047).

## TDD Workflow

Each task follows a strict Test-Driven Development workflow:

1. **RED**: Write a failing test that specifies the behavior
2. **APPROVAL**: Get approval for the test before proceeding
3. **GREEN**: Implement minimum code to make the test pass
4. **REFACTOR**: Improve design while keeping tests green

Tests are written in `tests/Paramore.Brighter.Kafka.Tests/`. Integration tests that require Kafka should use docker-compose-kafka.yaml for test infrastructure.

## Test Strategy

See `test-analysis.md` for detailed analysis of existing tests and reusable test doubles.

**Key decisions:**
- **Phase 1-2**: Unit tests (no Kafka required)
- **Phase 3+**: Direct consumer integration tests (test `consumer.Reject()` directly with Kafka)
  - Create consumer with DLQ routing keys configured
  - Send/receive messages via Kafka
  - Call `consumer.Reject()` with rejection reason
  - Verify messages appear on DLQ/invalid message topics via second consumer
  - This follows existing Kafka test patterns and enables faster TDD cycles
- Reuse test double patterns from Core tests (`MyRejectedEventHandler`) where applicable

## Task List

### Phase 1: Naming Convention Classes

- [x] **TEST: DeadLetterNamingConvention uses default template**
  - Write test: When_creating_dead_letter_name_with_default_template_should_append_dlq
  - Verify "orders" → "orders.dlq"
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [x] **IMPLEMENT: DeadLetterNamingConvention with default template**
  - Create DeadLetterNamingConvention class in Paramore.Brighter
  - Implement MakeChannelName method
  - Make the test pass

- [x] **TEST: DeadLetterNamingConvention uses custom template**
  - Write test: When_creating_dead_letter_name_with_custom_template_should_use_template
  - Verify custom template like "failed-{0}" works
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [x] **IMPLEMENT: DeadLetterNamingConvention with custom template**
  - Add template constructor parameter
  - Make the test pass

- [x] **TEST: InvalidMessageNamingConvention uses default template**
  - Write test: When_creating_invalid_message_name_with_default_template_should_append_invalid
  - Verify "orders" → "orders.invalid"
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [x] **IMPLEMENT: InvalidMessageNamingConvention with default template**
  - Create InvalidMessageNamingConvention class
  - Implement MakeChannelName method
  - Make the test pass

- [x] **TEST: InvalidMessageNamingConvention uses custom template**
  - Write test: When_creating_invalid_message_name_with_custom_template_should_use_template
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [x] **IMPLEMENT: InvalidMessageNamingConvention with custom template**
  - Add template constructor parameter
  - Make the test pass

### Phase 2: KafkaSubscription DLQ Support

- [x] **TEST: KafkaSubscription implements IUseBrighterDeadLetterSupport**
  - Write test: When_creating_kafka_subscription_with_dead_letter_routing_key_should_expose_property
  - Verify DeadLetterRoutingKey property is accessible
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [x] **IMPLEMENT: KafkaSubscription implements IUseBrighterDeadLetterSupport**
  - Add interface implementation
  - Add DeadLetterRoutingKey property
  - Add optional deadLetterRoutingKey constructor parameter
  - Make the test pass

- [x] **TEST: KafkaSubscription implements IUseBrighterInvalidMessageSupport**
  - Write test: When_creating_kafka_subscription_with_invalid_message_routing_key_should_expose_property
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [x] **IMPLEMENT: KafkaSubscription implements IUseBrighterInvalidMessageSupport**
  - Add interface implementation
  - Add InvalidMessageRoutingKey property
  - Add optional invalidMessageRoutingKey constructor parameter
  - Make the test pass

### Phase 3: Message Rejection to DLQ (Integration Test)

- [X] **TEST: Rejected message sent to dead letter queue**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_delivery_error_should_send_to_dlq
  - Use docker-compose-kafka.yaml for test infrastructure
  - Create consumer with DLQ routing key configured
  - Reject message with MessageRejectionReason.DeliveryError
  - Verify message appears on DLQ topic with correct metadata
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [X] **IMPLEMENT: KafkaMessageConsumer DLQ producer infrastructure**
  - Add deadLetterRoutingKey and invalidMessageRoutingKey constructor parameters
  - Add Lazy<IAmAMessageProducerSync> fields
  - Implement CreateDeadLetterProducer factory method
  - Implement CreateInvalidMessageProducer factory method
  - Producers inherit consumer configuration
  - Make the test pass

- [X] **IMPLEMENT: KafkaMessageConsumer.Reject() with DLQ routing**
  - Implement routing logic for MessageRejectionReason.DeliveryError → DLQ
  - Implement message enrichment with metadata
  - Handle DLQ production failures (log and acknowledge)
  - Make the test pass

- [X] **IMPLEMENT: Update KafkaMessageConsumer.Dispose()**
  - Check IsValueCreated before disposing producers
  - Dispose both DLQ and invalid message producers
  - Make the test pass

### Phase 4: Message Rejection to DLQ Async (Integration Test)

- [X] **TEST: Rejected message sent to dead letter queue**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Write test: When_rejecting_message_with_delivery_error_should_send_to_dlq_async. Note this test is similar to 
    "tests/Paramore.Brighter.Kafka.
    Tests/MessagingGateway/Reactor/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs" but uses async 
    methods not sync ones i.e. `RejectAsync` and not `Async` and should use an async test method. 
  - Use docker-compose-kafka.yaml for test infrastructure
  - Create consumer with DLQ routing key configured
  - Reject message with MessageRejectionReason.DeliveryError
  - Verify message appears on DLQ topic with correct metadata
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [X] **IMPLEMENT: KafkaMessageConsumer.RejectAsync() with DLQ routing**
  - Add Lazy<IAmAMessageProducerAsync> fields
  - Implement async producer factory methods
  - Implement routing logic for MessageRejectionReason.DeliveryError → DLQ
  - Note that this is the same algorithm as KafkaMessageConsumer.RejectAsync() 
  - Implement message enrichment with metadata
  - Handle DLQ production failures (log and acknowledge)
  - Make the test pass

- [X] **REFACTOR: Update KafkaMessageConsumer**
  - Use `.claude/commands/refactor/tidy-first.md`
  - Think: can we remove duplication between KafkaMessageConsumer.Reject() and KafkaMessageConsumer.RejectAsync()?
  - Implement: make any changes to KafkaMessageConsumer
  - Ensure the test still pass

### Phase 5: Invalid Message Routing

- [X] **TEST: Invalid message sent to invalid message channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel
  - Configure consumer with invalid message routing key
  - Reject message with MessageRejectionReason.Unacceptable
  - Verify message appears on invalid message topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [X] **IMPLEMENT: Reject() routes Unacceptable to invalid message channel**
  - This may already be implemented in Phase 3, so check
  - Implement routing logic for MessageRejectionReason.Unacceptable
  - Make the test pass

- [X] **TEST: Invalid message falls back to DLQ when no invalid channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq
  - Configure consumer with DLQ but no invalid message routing key
  - Reject with MessageRejectionReason.Unacceptable
  - Verify message appears on DLQ topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [X] **IMPLEMENT: Fallback logic for Unacceptable → DLQ**
  - Implement fallback when invalid channel not configured
  - Make the test pass

### Phase 5: Invalid Message Routing Async

- [X] **TEST: Invalid message sent to invalid message channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Write test: When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel_async
  - Configure consumer with invalid message routing key
  - Reject message with MessageRejectionReason.Unacceptable
  - Verify message appears on invalid message topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [X] **IMPLEMENT: Reject() routes Unacceptable to invalid message channel**
  - This may already be implemented in Phase 4, so check
  - Implement routing logic for MessageRejectionReason.Unacceptable
  - Make the test pass

- [X] **TEST: Invalid message falls back to DLQ when no invalid channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Write test: When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq_async
  - Configure consumer with DLQ but no invalid message routing key
  - Reject with MessageRejectionReason.Unacceptable
  - Verify message appears on DLQ topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [X] **IMPLEMENT: Fallback logic for Unacceptable → DLQ**
  - Implement fallback when invalid channel not configured
  - Make the test pass

[X] **REFACTOR: Update KafkaMessageConsumer**
- Use `.claude/commands/refactor/tidy-first.md`
- Think: can we remove duplication between KafkaMessageConsumer.Reject() and KafkaMessageConsumer.RejectAsync()?
- Implement: make any changes to KafkaMessageConsumer
- Ensure the test still pass

### Phase 6: Edge Cases and Error Handling

#### No Channels Configured Behavior

- [X] **TEST + IMPLEMENT: Rejection with no channels configured (Sync)**
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

- [X] **TEST + IMPLEMENT: Rejection with no channels configured (Async)**
  - **USE COMMAND**: `/test-first when rejecting message async with no channels configured should acknowledge and log`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log_async.cs`
  - Test should verify:
    - Same behavior as sync test but using async methods
    - Use `RejectAsync()`, `ReceiveAsync()`, and async consumer creation
    - Consumer created with MessagePumpType.Proactor
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumer.RejectAsync()` line ~584
    - Same logic as Reject() but using async producer checks
    - Check: `if (reason == null || (_deadLetterProducerAsync == null && _invalidMessageProducerAsync == null))`
    - Await `AcknowledgeAsync(message, cancellationToken)`
    - Return `true`

#### Unknown Rejection Reason Handling

- [X] **TEST + IMPLEMENT: Unknown rejection reason routes to DLQ (Sync)**
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

- [X] **TEST + IMPLEMENT: Unknown rejection reason routes to DLQ (Async)**
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

- [X] **TEST + IMPLEMENT: DLQ topic creation follows MakeChannels strategy**
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

### Phase 7: Channel Factory Integration

- [X] **TEST + IMPLEMENT: ChannelFactory passes DLQ routing keys to consumer**
  - **USE COMMAND**: `/test-first when creating channel with dlq subscription should pass routing keys`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway`
  - Test file: `When_creating_channel_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - Create KafkaSubscription with deadLetterRoutingKey and invalidMessageRoutingKey
    - Use KafkaMessageConsumerFactory to create consumer from subscription
    - Verify consumer has access to both routing keys (test by triggering rejection and verifying DLQ/invalid message channels work)
    - Alternative: Use reflection to verify routing keys were passed to constructor
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumerFactory.Create()` or similar factory method
    - Check if subscription implements IUseBrighterDeadLetterSupport
    - Check if subscription implements IUseBrighterInvalidMessageSupport
    - Extract DeadLetterRoutingKey and InvalidMessageRoutingKey from subscription
    - Pass routing keys to KafkaMessageConsumer constructor (deadLetterRoutingKey, invalidMessageRoutingKey parameters)
    - Make the test pass

### Phase 8: Message Enrichment Verification

- [X] **TEST + IMPLEMENT: Rejected message includes all required metadata**
  - **USE COMMAND**: `/test-first when rejecting message should include metadata`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor`
  - Test file: `When_rejecting_message_should_include_metadata.cs`
  - Test should verify:
    - Send message to data topic and reject it with DLQ configured
    - Consume rejected message from DLQ topic
    - Verify metadata in message headers:
      - OriginalTopic: Contains source topic name
      - RejectionTimestamp: Contains rejection time in ISO format
      - RejectionReason: Contains rejection reason string
      - MESSAGE_TYPE: Contains original message type
    - Note: OriginalPartition, OriginalOffset, ConsumerGroup, RedeliveryCount may not be available in current implementation
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumer.RefreshMetadata()` line ~895
    - Verify all metadata fields are being added to message.Header.Bag
    - Add any missing fields if needed
    - Implement GetCurrentPartition() and GetCurrentOffset() if needed for partition/offset metadata
    - Make the test pass

### Phase 9: Regression Testing

- [X] **Run existing Kafka test suite**
  - ✅ All 34 tests pass
  - ✅ No breaking changes to existing behavior
  - 0 errors, 12 pre-existing nullable warnings (unrelated to DLQ feature)

### Phase 10: Documentation

- [X] **Update XML documentation**
  - ✅ Constructor parameters already documented (deadLetterRoutingKey, invalidMessageRoutingKey)
  - ✅ IUseBrighterDeadLetterSupport interface already documented
  - ✅ IUseBrighterInvalidMessageSupport interface already documented
  - ✅ DeadLetterNamingConvention class already documented
  - ✅ InvalidMessageNamingConvention class already documented

- [X] **Create usage examples**
  - ✅ Created comprehensive usage guide: docs/Kafka-DeadLetterQueue-Usage.md
  - ✅ Basic DLQ configuration examples
  - ✅ Naming convention usage (default and custom templates)
  - ✅ Invalid message channel configuration
  - ✅ Rejecting messages from handlers (RejectMessageAction, DeferMessageAction)
  - ✅ Message metadata access and usage
  - ✅ Advanced scenarios (environment-specific, no DLQ, async, custom routing)

## Task Dependencies

Each phase builds on the previous:

```
Phase 1: Naming Convention Classes (unit tests)
    ↓
Phase 2: KafkaSubscription DLQ Support (unit tests)
    ↓
Phase 3: Message Rejection to DLQ (integration test drives implementation)
    ↓
Phase 4: Invalid Message Routing (integration tests)
    ↓
Phase 5: Invalid Message Routing Async (integration tests)
    ↓
Phase 6: Edge Cases and Error Handling (integration tests)
    ↓
Phase 7: Channel Factory Integration (integration tests)
    ↓
Phase 8: Message Enrichment Verification (integration test)
    ↓
Phase 9: Regression Testing
    ↓
Phase 10: Documentation
    ↓
Phase 11: PR Review Feedback (fixes and enhancements)
```

Each TEST task must be approved before its corresponding IMPLEMENT task.

## Risk Mitigation

- **Risk**: DLQ producer creation fails due to configuration issues
  - **Mitigation**: Fail fast with clear error messages; test Create/Validate/Assume modes thoroughly

- **Risk**: DLQ production failures cause message loss
  - **Mitigation**: Log full message content on failure; acknowledge anyway to avoid blocking consumer

- **Risk**: Breaking changes to existing Kafka functionality
  - **Mitigation**: All new parameters are optional; verify existing tests pass

- **Risk**: Performance impact from lazy producer initialization
  - **Mitigation**: Use Lazy<T> for thread-safe initialization; measure performance in integration tests

### Phase 11: PR Review Feedback

#### Critical Issues (Must Fix)

- [X] **FIX: Add missing MIT license header**
  - File: `src/Paramore.Brighter/Extensions/MessageTypeExtensions.cs`
  - Issue: Missing the MIT license header required per codebase standards
  - Add standard MIT license header block at the top of the file
  - Match format of other extension files in the codebase

- [X] **TEST + IMPLEMENT: InvalidMessageAction for deserialization failures**
  - **USE COMMAND**: `/test-first when message deserialization fails should throw invalid message action`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor` (or appropriate location)
  - The ADR mentions this exception class for failed message deserialization
  - Test should verify:
    - When a message cannot be deserialized, InvalidMessageAction is thrown
    - Exception contains original message and error details
    - Message pump can catch this and route to invalid message channel
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create InvalidMessageAction exception class inheriting from MessageHandlingException
    - Should be parallel to RejectMessageAction but specifically for deserialization failures
    - Include properties for original message body and deserialization error
    - Update message mappers to throw this exception on deserialization failure

- [X] **FIX: Complete incomplete documentation sentence**
  - File: `src/Paramore.Brighter/IUseBrighterInvalidMessageSupport.cs:28-32`
  - ✅ VERIFIED COMPLETE: Documentation now reads "...we will send invalid messages to the Dead Letter Channel instead."
  - The sentence is complete and explains the fallback behavior

#### Important Issues (Should Fix)

- [X] **FIX: Inconsistent property mutability**
  - Files:
    - `src/Paramore.Brighter/IUseBrighterInvalidMessageSupport.cs`
    - `src/Paramore.Brighter/IUseBrighterDeadLetterSupport.cs`
  - ✅ VERIFIED CONSISTENT: Both interfaces now have `RoutingKey? { get; set; }`
  - Both properties are mutable, which is consistent

- [X] **FIX: Add thread safety documentation**
  - File: `src/Paramore.Brighter/InMemoryMessageConsumer.cs`
  - ✅ N/A - DESIGN CHANGED: No `DeadLetterRoutingKey` property exists on `InMemoryMessageConsumer`
  - The class uses constructor injection with private readonly fields (`_deadLetterTopic`, `_invalidMessageTopic`)
  - This design is inherently thread-safe as values are immutable after construction

- [X] **FIX: Remove or complete empty remarks tag**
  - File: `src/Paramore.Brighter/Actions/RejectMessageAction.cs:35-43`
  - ✅ VERIFIED COMPLETE: The `<remarks>` tag now contains meaningful content
  - Documents when to use RejectMessageAction vs DeferMessageAction and the intended workflow

#### Minor Issues (Nice to Fix)

- [X] **FIX: Typo in extension method documentation**
  - File: `src/Paramore.Brighter/Extensions/MessageConsumerExtensions.cs`
  - ✅ N/A - FILE DOES NOT EXIST: The `MessageConsumerExtensions.cs` file is not present in the codebase
  - Either removed during refactoring or never created

- [X] **FIX: Code style - missing space**
  - File: `src/Paramore.Brighter/InMemoryMessageConsumer.cs`
  - ✅ VERIFIED FIXED: The pattern `var removed =_` does not exist in the codebase
  - All spacing is now correct

#### Suggestions to Consider

- [X] **CONSIDER: Add REJECTION_REASON constant to MessageHeader**
  - File: `src/Paramore.Brighter/MessageHeader.cs`
  - ✅ N/A - ALREADY EXISTS IN KAFKA: Constants are defined in `HeaderNames.cs`:
    - `REJECTION_REASON`, `REJECTION_MESSAGE`, `REJECTION_TIMESTAMP`, `ORIGINAL_TOPIC`, `ORIGINAL_TYPE`
  - Transport-specific constants are appropriate since only Kafka implements Brighter-managed DLQ
  - Other transports (SQS, RabbitMQ) use native DLQ support and don't need these headers

- [X] **REVIEW: Observability in Reject methods**
  - Files: KafkaMessageConsumer.Reject() and RejectAsync()
  - ✅ SUFFICIENT FOR V1: Structured logging is implemented:
    - `NoChannelsConfiguredForRejection` (Warning) - when no DLQ/invalid channel configured
    - `MessageSentToRejectionChannel` (Information) - on successful DLQ send
    - `ErrorSendingToRejectionChannel` (Error) - on DLQ send failure
  - All logs include MessageId and RejectionReason for structured logging
  - OpenTelemetry traces/metrics can be added as follow-up enhancement

- [X] **TEST: Edge case coverage**
  - ✅ ALL EDGE CASES COVERED by existing tests:
    - `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log` (null routing key)
    - `When_rejecting_message_should_include_metadata` (reason added to headers)
    - `When_rejecting_message_with_unknown_reason_should_send_to_dlq` (unknown reason handling)
    - `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel`
    - `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq`
  - Concurrent access N/A - InMemoryMessageConsumer uses constructor injection with immutable fields

#### Documentation Needs

- [X] **CLARIFY: Documentation plan**
  - ✅ DOCUMENTATION COMPLETE: `docs/Kafka-DeadLetterQueue-Usage.md` covers:
    - Basic DLQ configuration
    - Naming conventions (DeadLetterNamingConvention, InvalidMessageNamingConvention)
    - Invalid message channel configuration
    - RejectMessageAction and DeferMessageAction usage with examples
    - Message metadata table (OriginalTopic, RejectionReason, RejectionTimestamp, etc.)
    - Advanced scenarios (env-specific, no DLQ, async, custom routing)
  - Migration guide not needed - this is opt-in additive functionality
  - Other transport examples not needed - only Kafka implements Brighter-managed DLQ

## Notes

- All new constructor parameters are optional to maintain backward compatibility
- DLQ/invalid message channels are opt-in features
- Message rejection always acknowledges/commits offset to prevent reprocessing
- Producer lifecycle is managed by consumer (created lazily, disposed with consumer)
- Next steps include Middleware to throw RejectMessageAction
