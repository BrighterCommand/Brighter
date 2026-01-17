# Implementation Tasks

This document outlines the tasks for implementing the Kafka Dead Letter Queue feature as specified in the requirements and ADRs (0034, 0035, 0036).

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

- [] **REFACTOR: Update KafkaMessageConsumer**
  - Use `.claude/commands/refactor/tidy-first.md` 
  - Think: can we remove duplication between KafkaMessageConsumer.Reject() and KafkaMessageConsumer.RejectAsync()? 
  - Implement: make any changes to KafkaMessageConsumer 
  - Ensure the test still pass

### Phase 5: Invalid Message Routing

- [ ] **TEST: Invalid message sent to invalid message channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel
  - Configure consumer with invalid message routing key
  - Reject message with MessageRejectionReason.Unacceptable
  - Verify message appears on invalid message topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Reject() routes Unacceptable to invalid message channel**
  - This may already be implemented in Phase 3, so check
  - Implement routing logic for MessageRejectionReason.Unacceptable
  - Make the test pass

- [ ] **TEST: Invalid message falls back to DLQ when no invalid channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor"
  - Write test: When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq
  - Configure consumer with DLQ but no invalid message routing key
  - Reject with MessageRejectionReason.Unacceptable
  - Verify message appears on DLQ topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Fallback logic for Unacceptable → DLQ**
  - Implement fallback when invalid channel not configured
  - Make the test pass

### Phase 5: Invalid Message Routing Async

- [ ] **TEST: Invalid message sent to invalid message channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor" 
  - Write test: When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel_async
  - Configure consumer with invalid message routing key
  - Reject message with MessageRejectionReason.Unacceptable
  - Verify message appears on invalid message topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Reject() routes Unacceptable to invalid message channel**
  - This may already be implemented in Phase 4, so check
  - Implement routing logic for MessageRejectionReason.Unacceptable
  - Make the test pass

- [ ] **TEST: Invalid message falls back to DLQ when no invalid channel**
  - In "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Write test: When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq_async
  - Configure consumer with DLQ but no invalid message routing key
  - Reject with MessageRejectionReason.Unacceptable
  - Verify message appears on DLQ topic
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Fallback logic for Unacceptable → DLQ**
  - Implement fallback when invalid channel not configured
  - Make the test pass

[X] **REFACTOR: Update KafkaMessageConsumer**
- Use `.claude/commands/refactor/tidy-first.md`
- Think: can we remove duplication between KafkaMessageConsumer.Reject() and KafkaMessageConsumer.RejectAsync()?
- Implement: make any changes to KafkaMessageConsumer
- Ensure the test still pass

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

### Phase 7: Channel Factory Integration

- [ ] **TEST: ChannelFactory passes DLQ routing keys to consumer**
  - Write test: When_creating_channel_with_dlq_subscription_should_pass_routing_keys
  - Create KafkaSubscription with DLQ routing keys
  - Verify ChannelFactory extracts and passes them to consumer
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: ChannelFactory extracts routing keys**
  - Check if subscription implements IUseBrighterDeadLetterSupport
  - Check if subscription implements IUseBrighterInvalidMessageSupport
  - Extract routing keys and pass to consumer constructor
  - Make the test pass

### Phase 8: Message Enrichment Verification

- [ ] **TEST: Rejected message includes all required metadata**
  - Write test: When_rejecting_message_should_include_metadata
  - Verify OriginalTopic, OriginalPartition, OriginalOffset
  - Verify RejectionTimestamp, RejectionReason
  - Verify ConsumerGroup, RedeliveryCount
  - **APPROVAL REQUIRED BEFORE IMPLEMENTATION**

- [ ] **IMPLEMENT: Complete EnrichMessageWithMetadata if needed**
  - Add any missing metadata fields
  - Implement GetCurrentPartition() and GetCurrentOffset() if needed
  - Make the test pass

### Phase 9: Regression Testing

- [ ] **Run existing Kafka test suite**
  - Verify all existing tests still pass
  - No breaking changes to existing behavior

### Phase 10: Documentation

- [ ] **Update XML documentation**
  - Document new constructor parameters
  - Document IUseBrighterDeadLetterSupport and IUseBrighterInvalidMessageSupport interfaces
  - Document naming convention classes

- [ ] **Create usage examples**
  - Show how to configure DLQ for Kafka subscription
  - Show how to use naming conventions
  - Show how to throw RejectMessageAction in handler

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
Phase 6: Async Consumer Support (integration tests)
    ↓
Phase 8: Message Enrichment Verification (integration test)
    ↓
Phase 9: Regression Testing
    ↓
Phase 10: Documentation
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

## Notes

- All new constructor parameters are optional to maintain backward compatibility
- DLQ/invalid message channels are opt-in features
- Message rejection always acknowledges/commits offset to prevent reprocessing
- Producer lifecycle is managed by consumer (created lazily, disposed with consumer)
