# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #3660 - [Feature] "Simulate" Dead Letter for Kafka

## Problem Statement

As far as I understand the documentation, currently if I throw an exception in a request handler it will create a DLQ entry for RabbitMQ, but for Kafka it will just ack the message. This basically means that there are completely different behaviors between providers and it's hard to change between providers while keeping handler logic the same.

**User Story**: As a Brighter user, I would like Kafka to handle failed messages consistently with RabbitMQ's dead letter queue behavior, so that I can switch between message broker providers without changing my handler logic.

## Proposed Solution

Kafka does not have DLQ as a native concept, but Brighter could simulate it. When anything in a handler fails, the message and error details can be passed to another topic with a specified name, mimicking the dead letter queue pattern available in RabbitMQ.

## Requirements

### Functional Requirements

- When a message handler throws an exception for a Kafka message, the message should be sent to a configured dead letter topic
- The dead letter message should include:
  - Original message content
  - Error details (exception message, stack trace)
  - Metadata about the failure (timestamp, handler name, retry attempts, etc.)
- Failed message should be moved to DLQ topic instead of being acknowledged
- Behavior should be configurable (enable/disable DLQ per topic or globally)
- DLQ topic naming should be configurable (e.g., `{original-topic}.dlq` or custom pattern)

### Non-functional Requirements

- **Consistency**: DLQ behavior should be consistent with RabbitMQ implementation where possible
- **Performance**: DLQ handling should not significantly impact message processing throughput
- **Reliability**: DLQ message production should be reliable (handle production failures gracefully)
- **Observability**: DLQ operations should be logged and traceable
- **Compatibility**: Should work with existing Kafka consumer/producer configurations

### Constraints and Assumptions

- Kafka does not have native DLQ support (unlike RabbitMQ)
- DLQ will be implemented as a separate Kafka topic
- Assumes Kafka broker is available for DLQ topic creation
- Assumes users have permissions to produce to DLQ topics
- Implementation should align with existing Brighter Kafka transport patterns

### Out of Scope

- Automatic DLQ topic creation (users must create DLQ topics in advance or configure auto-creation in Kafka)
- Automatic replay/retry from DLQ (this would be a separate feature)
- DLQ message format transformation (will use same serialization as original message)
- Support for other transports (focus is Kafka only for this feature)

## Acceptance Criteria

### Success Metrics

1. ✅ When a handler throws an exception, the Kafka message is sent to the configured DLQ topic
2. ✅ The DLQ message contains the original message payload and error metadata
3. ✅ The behavior is consistent with RabbitMQ's DLQ handling from a user perspective
4. ✅ Users can configure DLQ topic names per subscription
5. ✅ Failed messages are not acknowledged to Kafka until DLQ production succeeds (or fails with configured behavior)
6. ✅ DLQ operations are properly logged for observability
7. ✅ Existing tests pass and new tests cover DLQ scenarios
8. ✅ Documentation explains how to configure and use Kafka DLQ

### Testing Approach

- Unit tests for DLQ message creation and metadata
- Integration tests with Kafka broker for end-to-end DLQ flow
- Tests verifying consistency with RabbitMQ DLQ behavior
- Tests for error scenarios (DLQ topic unavailable, production failures)
- Performance tests to ensure minimal overhead

### Definition of Done

- Implementation complete and follows Brighter coding standards
- All tests passing (unit and integration)
- Documentation updated (README, configuration guides)
- ADR(s) documenting architectural decisions
- Code reviewed and approved
- No regressions in existing Kafka functionality

## Additional Context

This feature addresses a key pain point where users cannot easily switch between RabbitMQ and Kafka as message brokers without modifying handler logic. The inconsistent error handling behavior makes Brighter's transport abstraction less effective.

By simulating DLQ behavior for Kafka, we provide:
1. **Consistency** across transports
2. **Easier migration** between RabbitMQ and Kafka
3. **Better error handling** for Kafka-based systems
4. **Production readiness** for systems that need guaranteed error capture

## Related Work

- Existing RabbitMQ DLQ implementation in Brighter
- ADR 0034: Provide DLQ where missing (docs/adr/0034-provide-dlq-where-missing.md)
