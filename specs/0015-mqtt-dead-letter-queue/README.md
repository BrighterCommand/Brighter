# Specification: MQTT Dead Letter Queue

**Feature Name**: MQTT Dead Letter Queue
**Spec ID**: 0015
**Created**: 2026-02-10
**Status**: Design

## Overview

Add Brighter-managed dead letter queue support to the MQTT messaging gateway. MQTT has no native DLQ and `Reject()` is currently not implemented (returns false). Brighter will manage DLQ routing by publishing rejected messages to a separate MQTT topic.

## Workflow Status

- [x] Requirements defined
- [x] Requirements approved
- [ ] ADRs created
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
- [ADR 0043: MQTT DLQ Brighter-Managed](../../docs/adr/0043-mqtt-dlq-brighter-managed.md) - MQTT-specific design

## Scope

### Package Affected
- `Paramore.Brighter.MessagingGateway.MQTT`

### Key Files
- `MQTTMessageConsumer.cs` - Implement Reject()/RejectAsync() with DLQ routing
- `MqttSubscription.cs` - New subscription class with DLQ interfaces (ADR 0043)
- `MqttMessageConsumerFactory.cs` - New consumer factory (ADR 0043)
