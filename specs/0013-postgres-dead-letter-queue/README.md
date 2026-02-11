# Specification: PostgreSQL Dead Letter Queue

**Feature Name**: PostgreSQL Dead Letter Queue
**Spec ID**: 0013
**Created**: 2026-02-10
**Status**: Requirements Draft

## Overview

Add Brighter-managed dead letter queue support to the PostgreSQL messaging gateway. PostgreSQL has no native DLQ - `Reject()` currently just deletes the message. Brighter will manage DLQ routing by producing rejected messages to a separate PostgreSQL queue topic before deleting the original.

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
- `Paramore.Brighter.MessagingGateway.Postgres`

### Key Files
- `PostgresSubscription.cs` - Add DLQ interface implementations
- `PostgresConsumerFactory.cs` - Pass routing keys to consumer
- `PostgresMessageConsumer.cs` - Add DLQ routing to Reject()/RejectAsync() before delete
