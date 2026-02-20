# Specification: RocketMQ Dead Letter Queue

**Feature Name**: RocketMQ Dead Letter Queue
**Spec ID**: 0014
**Created**: 2026-02-10
**Status**: Requirements Draft

## Overview

Add Brighter-managed dead letter queue support to the RocketMQ messaging gateway. RocketMQ's `Reject()` currently requeues the message (infinite loop risk). Brighter will manage DLQ routing by producing rejected messages to a separate RocketMQ topic instead of requeuing.

## Workflow Status

- [x] Requirements defined
- [x] Requirements approved
- [x] ADRs created
- [x] Tasks created
- [ ] Tasks approved
- [ ] Implementation complete
- [ ] Tests passing
- [ ] PR submitted

## Files

- `requirements.md` - User requirements and problem statement
- `tasks.md` - Implementation task list

## ADRs

- [ADR 0034: Provide DLQ Where Missing](../../docs/adr/0034-provide-dlq-where-missing.md) - High-level DLQ strategy (existing)
- [ADR 0036: Message Rejection Routing Strategy](../../docs/adr/0036-message-rejection-routing-strategy.md) - Routing logic (existing)
- [ADR 0042: RocketMQ DLQ Brighter-Managed](../../docs/adr/0042-rocketmq-dlq-brighter-managed.md) - RocketMQ-specific design (async producer, Ack-based cleanup)

## Scope

### Package Affected
- `Paramore.Brighter.MessagingGateway.RocketMQ`

### Key Files
- `RocketMqSubscription.cs` - Add DLQ interface implementations
- `RocketMessageConsumerFactory.cs` - Pass routing keys to consumer
- `RocketMessageConsumer.cs` - Replace Requeue() with DLQ routing in Reject()/RejectAsync()
