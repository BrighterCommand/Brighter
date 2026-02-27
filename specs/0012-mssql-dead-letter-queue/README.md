# Specification: MsSql Dead Letter Queue

**Feature Name**: MsSql Dead Letter Queue
**Spec ID**: 0012
**Created**: 2026-02-10
**Status**: Requirements Draft

## Overview

Add Brighter-managed dead letter queue support to the MsSql messaging gateway. MsSql has no native DLQ, and `Reject()` is currently not implemented (returns false). Brighter will manage DLQ routing by producing rejected messages to a separate MsSql queue topic.

## Workflow Status

- [x] Requirements defined
- [ ] Requirements approved
- [ ] ADRs created (may not need new ADR - covered by ADR 0034)
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

## Scope

### Package Affected
- `Paramore.Brighter.MessagingGateway.MsSql`

### Key Files
- `MsSqlSubscription.cs` - Add DLQ interface implementations
- `MsSqlMessageConsumerFactory.cs` - Pass routing keys to consumer
- `MsSqlMessageConsumer.cs` - Implement Reject()/RejectAsync() with DLQ routing
