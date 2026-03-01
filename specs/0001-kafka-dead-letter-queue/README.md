# Specification: Kafka Dead Letter Queue

**Feature Name**: Kafka Dead Letter Queue
**Spec ID**: 0001
**Created**: 2026-01-11
**Status**: Complete

## Overview

This specification covers the implementation of dead letter queue functionality for Kafka message handling in Brighter.

## Workflow Status

- [X] Requirements defined
- [X] Requirements approved
- [X] ADRs created
- [X] ADRs approved
- [X] Tasks created
- [X] Tasks approved
- [X] Implementation complete
- [X] Tests passing
- [ ] PR submitted

## Files

- `requirements.md` - User requirements and problem statement
- `tasks.md` - Implementation task breakdown
- `pr-review.md` - PR review feedback and resolutions
- `.adr-list` - List of associated Architecture Decision Records

## ADRs

The following ADRs were created for this feature:

- [ADR 0045: Provide DLQ Where Missing](../../docs/adr/0045-provide-dlq-where-missing.md) - High-level DLQ strategy
- [ADR 0046: Kafka DLQ Producer for Requeue](../../docs/adr/0046-kafka-dlq-producer-for-requeue.md) - Kafka-specific producer implementation
- [ADR 0047: Message Rejection Routing Strategy](../../docs/adr/0047-message-rejection-routing-strategy.md) - Routing logic for rejected messages

## Documentation

- [Kafka Dead Letter Queue Usage Guide](../../docs/Kafka-DeadLetterQueue-Usage.md) - Comprehensive usage documentation

## Implementation Summary

### Key Components Added

1. **Naming Conventions** (`Paramore.Brighter`)
   - `DeadLetterNamingConvention` - Default template: `{0}.dlq`
   - `InvalidMessageNamingConvention` - Default template: `{0}.invalid`

2. **Interfaces** (`Paramore.Brighter`)
   - `IUseBrighterDeadLetterSupport` - Marks subscriptions supporting DLQ
   - `IUseBrighterInvalidMessageSupport` - Marks subscriptions supporting invalid message channel

3. **Exception Classes** (`Paramore.Brighter.Actions`)
   - `RejectMessageAction` - Throw to reject message to DLQ
   - `InvalidMessageAction` - Throw on deserialization failure

4. **Kafka Consumer** (`Paramore.Brighter.MessagingGateway.Kafka`)
   - `KafkaMessageConsumer.Reject()` and `RejectAsync()` - Routes to DLQ/invalid channel
   - Lazy producer initialization for DLQ and invalid message channels
   - Message metadata enrichment (OriginalTopic, RejectionReason, RejectionTimestamp, etc.)

5. **Tests** - 34 tests covering all scenarios (sync/async, DLQ, invalid channel, edge cases)

## Next Steps

1. Submit PR for review: `/commit-push-pr`

## Notes

- All new parameters are optional for backward compatibility
- DLQ/invalid message channels are opt-in features
- Message rejection always acknowledges to prevent reprocessing
- Producer lifecycle managed by consumer (lazy creation, disposed with consumer)
