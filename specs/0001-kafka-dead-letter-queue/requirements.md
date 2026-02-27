# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #3660 - [Feature] "Simulate" Dead Letter for Kafka

## Problem Statement
When we fail to process a message in a handler, and that message is a transient failure, we can throw a 
`DeferMessageAction` to retry that message. After a given number of retries, we reject the message. If the broker 
supports it, we will forward that message to a dead letter channel.

This has two problems:

 1. It is not possible to force a message onto the dead letter channel, if it is not transient.
 2. We only move a message to a dead letter channel where the underlying transport supports it.

In addition, when we cannot deserialize a message that we have read from the channel, we do not forward that message 
to an invalid message channel, or if not available, to a dead letter channel, which makes it difficult to redirect 
that message.

**User Story**: As a Brighter user, I would like Brighter to handle failed messages consistently, with the ability 
to explicity send a message to a dead letter channel or invalid message channel, even if it is not natively supported, so that I can switch between message broker providers without changing my handler logic.

## Proposed Solution

We should provide the ability to explicity raise an exception that will publish a message to a dead-letter channel. We 
should provide the ability to publish a message to an invalid message channel when it can not be deserialized. We 
should provide a dead letter channel, where it is not available on the broker, by using a custom topic.

## Requirements

### Functional Requirements

- When a message handler throws a custom exception, the message should be sent to a configured dead letter topic
- The dead letter message should include:
  - Original message content
  - Error details (exception message, stack trace)
  - Metadata about the failure (timestamp, handler name, retry attempts, etc.)
- Failed message should be published to DLQ topic 
- Behavior should be configurable (enable/disable DLQ per topic or globally)
- DLQ topic naming should be configurable (e.g., `{original-topic}.dlq` or custom pattern)

- When a message mapper cannot deserialize a message, the message should be sent to either a configured invalid 
  message topic, or if that is not present any dead letter topic
- The invalid message should include:
  - Original message content
  - Error details (exception message, stack trace)
  - Metadata about the failure (timestamp, message type, etc.)
- Failed message should be moved to invalid message topic 
- Behavior should be configurable (enable/disable DLQ per topic or globally)
- DLQ topic naming should be configurable (e.g., `{original-topic}.dlq` or custom pattern)

### Non-functional Requirements

- **Usability**: Automatic DLQ topic creation should match automatic creation for the data topic i.e. create, 
  validate or assume. This means we MUST create if required.
- **Performance**: DLQ handling should not significantly impact message processing throughput
- **Reliability**: DLQ message production should be reliable (handle production failures gracefully)
- **Observability**: DLQ operations should be logged and traceable

### Out of Scope

- Automatic replay/retry from DLQ (this would be a separate feature)
- DLQ message format transformation (will use same serialization as original message)

## Acceptance Criteria

### Success Metrics

1. ✅ When a handler throws a `RejectMessageAction` exception, the message is sent to the configured DLQ topic
2. ✅ The DLQ message contains the original message payload and error metadata
3. ✅ Users can configure DLQ topic names per subscription
4. ✅ Failed messages will not be processed again on the original queue or stream (i.e. acknowledged, rejected, 
   offsets committed as appropriate to the transport) 
5. ✅ DLQ operations are properly logged for observability
6. ✅ Existing tests pass and new tests cover DLQ scenarios
7. ✅ Documentation explains how to configure and use DLQ

### Testing Approach

- Unit tests for DLQ message creation and metadata using in-memory substituves
- Integration tests with actual brokers for end-to-end DLQ flow
- Tests for error scenarios (DLQ topic unavailable, production failures)

### Definition of Done

- Implementation complete and follows Brighter coding standards
- All tests passing (unit and integration)
- Documentation updated (README, configuration guides)
- ADR(s) documenting architectural decisions
- Code reviewed and approved
- No regressions in existing Kafka functionality

## Additional Context

This feature addresses a key pain point where users cannot easily switch between message brokers without modifying handler logic. The inconsistent error handling behavior makes Brighter's transport abstraction less effective.

By simulating DLQ behavior for Brighter, we provide:
1. **Consistency** across transports
2. **Easier migration** between transports
3. **Better error handling**
4. **Production readiness** for systems that need guaranteed error capture

## Related Work

- ADR 0045: Provide DLQ where missing (docs/adr/0045-provide-dlq-where-missing.md)
