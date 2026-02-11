# Specification: AWS SQS Dead Letter Queue

**Feature Name**: AWS SQS Dead Letter Queue
**Spec ID**: 0010
**Created**: 2026-02-10
**Status**: Requirements Draft

## Overview

Fix the AWS SQS message consumer to send rejected messages directly to a DLQ queue using `SendMessage` instead of the current `ChangeMessageVisibility(0)` approach. Add support for `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport` interfaces. Both AWSSQS and AWSSQS.V4 packages need identical changes.

## Workflow Status

- [x] Requirements defined
- [x] Requirements approved
- [x] ADRs created
- [ ] Tasks created
- [ ] Tasks approved
- [ ] Implementation complete
- [ ] Tests passing
- [ ] PR submitted

## Files

- `requirements.md` - User requirements and problem statement

## ADRs

- [ADR 0034: Provide DLQ Where Missing](../../docs/adr/0034-provide-dlq-where-missing.md) - High-level DLQ strategy (existing)
- [ADR 0036: Message Rejection Routing Strategy](../../docs/adr/0036-message-rejection-routing-strategy.md) - Routing logic (existing)
- [ADR 0038: AWS SQS DLQ Direct Send](../../docs/adr/0038-aws-sqs-dlq-direct-send.md) - SQS-specific implementation decisions

## Scope

### Packages Affected
- `Paramore.Brighter.MessagingGateway.AWSSQS`
- `Paramore.Brighter.MessagingGateway.AWSSQS.V4`

### Key Files
- `SqsSubscription.cs` - Add DLQ interface implementations
- `SqsMessageConsumerFactory.cs` - Pass routing keys to consumer
- `SqsMessageConsumer.cs` - Replace ChangeMessageVisibility with SendMessage to DLQ
