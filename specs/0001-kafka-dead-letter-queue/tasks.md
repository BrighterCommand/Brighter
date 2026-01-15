# Implementation Tasks

This document outlines the tasks for implementing the Kafka Dead Letter Queue feature as specified in the requirements and ADRs (0034, 0035, 0036).

## Task List

### Core Infrastructure

- [ ] **Add dead letter and invalid message routing key properties to KafkaSubscription**
  - KafkaSubscription implements IUseBrighterDeadLetterSupport and IUseBrighterInvalidMessageSupport
  - Add optional deadLetterRoutingKey parameter to constructors
  - Add optional invalidMessageRoutingKey parameter to constructors
  - Properties can be null (DLQ/invalid message channels are optional)

- [ ] **Create naming convention helper classes**
  - DeadLetterNamingConvention class with default template "{0}.dlq"
  - InvalidMessageNamingConvention class with default template "{0}.invalid"
  - Both support custom templates via constructor parameter
  - MakeChannelName method creates routing key from data topic routing key

### Consumer Implementation

- [ ] **Add DLQ producer infrastructure to KafkaMessageConsumer**
  - Add deadLetterRoutingKey and invalidMessageRoutingKey parameters to constructor
  - Add lazy producer fields using Lazy<IAmAMessageProducerSync>
  - Implement CreateDeadLetterProducer factory method (returns null if no routing key)
  - Implement CreateInvalidMessageProducer factory method (returns null if no routing key)
  - Producers inherit configuration from consumer (bootstrap servers, security, MakeChannels strategy)
  - Producers use acks=all for reliability

- [ ] **Add DLQ producer infrastructure to KafkaMessageConsumerAsync**
  - Add deadLetterRoutingKey and invalidMessageRoutingKey parameters to constructor
  - Add lazy producer fields using Lazy<IAmAMessageProducerAsync>
  - Implement CreateDeadLetterProducer factory method (returns null if no routing key)
  - Implement CreateInvalidMessageProducer factory method (returns null if no routing key)
  - Async producers match sync implementation pattern

- [ ] **Implement message rejection routing in KafkaMessageConsumer.Reject()**
  - Route MessageRejectionReason.Unacceptable to invalid message channel (fallback to DLQ if not configured)
  - Route MessageRejectionReason.DeliveryError to dead letter channel
  - Route MessageRejectionReason.Unknown to dead letter channel
  - Enrich message with rejection metadata before producing
  - Handle case where no channels configured (log warning, acknowledge anyway)
  - Catch and log DLQ production failures but acknowledge anyway to avoid blocking

- [ ] **Implement message rejection routing in KafkaMessageConsumerAsync.RejectAsync()**
  - Match sync implementation with async/await patterns
  - Use async producer methods

- [ ] **Add message enrichment for rejected messages**
  - Implement EnrichMessageWithMetadata method in KafkaMessageConsumer
  - Add OriginalTopic, OriginalPartition, OriginalOffset to message bag
  - Add RejectionTimestamp, RejectionReason to message bag
  - Add ConsumerGroup, RedeliveryCount (from HandledCount) to message bag
  - Implement GetCurrentPartition() and GetCurrentOffset() helper methods

- [ ] **Update Dispose methods to clean up lazy producers**
  - KafkaMessageConsumer.Dispose checks IsValueCreated before disposing
  - Dispose both dead letter and invalid message producers if created
  - KafkaMessageConsumerAsync.Dispose matches sync pattern

### Channel Factory Integration

- [ ] **Update ChannelFactory to pass routing keys to consumer**
  - Extract DeadLetterRoutingKey from subscription if implements IUseBrighterDeadLetterSupport
  - Extract InvalidMessageRoutingKey from subscription if implements IUseBrighterInvalidMessageSupport
  - Pass routing keys to KafkaMessageConsumer/Async constructors

### Testing

- [ ] **Write unit tests for naming convention classes**
  - Test default template behavior
  - Test custom template behavior
  - Test with various routing key formats

- [ ] **Write unit tests for message rejection routing logic**
  - Test Unacceptable routes to invalid message channel
  - Test Unacceptable falls back to DLQ when no invalid channel
  - Test DeliveryError routes to DLQ
  - Test Unknown routes to DLQ
  - Test no channels configured logs warning and acknowledges

- [ ] **Write unit tests for message enrichment**
  - Verify all metadata fields added correctly
  - Verify original message content preserved

- [ ] **Write integration tests for DLQ with test broker**
  - Test message sent to DLQ on RejectMessageAction
  - Test message sent to invalid channel on deserialization failure
  - Test DLQ message contains correct metadata
  - Test fallback behavior (invalid → DLQ)
  - Test MakeChannels.Create creates DLQ topic
  - Test MakeChannels.Validate fails if DLQ topic missing
  - Test DLQ production failure handling

- [ ] **Write integration tests for async consumer**
  - Match sync consumer test coverage

- [ ] **Verify existing tests still pass**
  - Run full Kafka test suite
  - Ensure no regressions in existing functionality

### Documentation

- [ ] **Update XML documentation**
  - Document new constructor parameters
  - Document IUseBrighterDeadLetterSupport and IUseBrighterInvalidMessageSupport interfaces
  - Document naming convention classes

- [ ] **Create usage examples**
  - Show how to configure DLQ for Kafka subscription
  - Show how to use naming conventions
  - Show how to throw RejectMessageAction in handler

## Task Dependencies

```
Core Infrastructure (Subscription + Naming)
    ↓
Consumer Implementation (Add DLQ producers)
    ↓
Consumer Implementation (Implement Reject)
    ↓
Consumer Implementation (Message enrichment + Dispose)
    ↓
Channel Factory Integration
    ↓
Testing (Unit → Integration)
    ↓
Documentation
```

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
