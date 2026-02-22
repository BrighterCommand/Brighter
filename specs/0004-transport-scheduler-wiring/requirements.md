# Requirements: Transport Channel Factory Scheduler Wiring

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: N/A (follow-up from spec 0002-universal_scheduler_delay review feedback)

## Problem Statement

As a Brighter user configuring scheduler-based requeue delay on non-InMemory transports (RMQ, Kafka, MQTT, MsSql, Redis), I would like the scheduler to be automatically wired through the standard DI/channel factory path, so that I don't have to manually construct consumers to use the feature.

In spec 0002, `IAmAMessageScheduler?` was added as a constructor parameter to consumers for RMQ (Async + Sync), Kafka, MQTT, MsSql, and Redis. However, none of the corresponding **consumer factories** or **channel factories** were updated to accept and forward this parameter. The InMemory transport is the only one with end-to-end wiring via `InMemoryChannelFactory`.

Additionally, the MQTT transport has no consumer factory or channel factory at all — users must manually construct `MQTTMessageConsumer` instances. This is inconsistent with every other transport and prevents MQTT from participating in the standard DI/channel factory path.

This means the universal scheduler delay feature is **unreachable via normal DI configuration** for all production transports. Users would have to manually construct consumers and bypass the standard `IAmAChannelFactory` / `ServiceCollection` path entirely.

## Proposed Solution

Update the factory chain for each affected transport so that `IAmAMessageScheduler?` flows from DI registration through to the consumer:

1. **Consumer factories** accept and forward the scheduler to consumers they create
2. **Channel factories** accept and forward the scheduler to consumer factories they use
3. **DI registration** resolves the scheduler and passes it when constructing channel factories

## Requirements

### Functional Requirements

- FR-1: `RmqMessageConsumerFactory` (Async and Sync) must accept `IAmAMessageScheduler?` and pass it when constructing `RmqMessageConsumer`
- FR-2: `KafkaMessageConsumerFactory` must accept `IAmAMessageScheduler?` and pass it when constructing `KafkaMessageConsumer`
- FR-3: `MsSqlMessageConsumerFactory` must accept `IAmAMessageScheduler?` and pass it when constructing `MsSqlMessageConsumer`
- FR-4: `RedisMessageConsumerFactory` must accept `IAmAMessageScheduler?` and pass it when constructing `RedisMessageConsumer`
- FR-5: An `MQTTMessageConsumerFactory` must be created, implementing the same consumer factory pattern as other transports, accepting `IAmAMessageScheduler?` and passing it when constructing `MQTTMessageConsumer`
- FR-6: An `MQTTChannelFactory` (or equivalently named channel factory) must be created for the MQTT transport, following the same pattern as other transports' channel factories
- FR-7: Each transport's `ChannelFactory` must accept `IAmAMessageScheduler?` and pass it to its consumer factory
- FR-8: The DI registration path (`ServiceActivatorServiceCollectionExtensions` or transport-specific extensions) must resolve `IAmAMessageScheduler` from the container and pass it when constructing channel factories
- FR-9: When no scheduler is explicitly configured by the user (i.e., `UseScheduler()` / `UseMessageScheduler()` not called), the DI registration must default to registering `InMemorySchedulerFactory` so that a scheduler is always available
- FR-10: When no scheduler is registered and consumers are constructed outside DI, the parameter defaults to `null` and existing behaviour is unchanged (backward compatible)

### Non-functional Requirements

- All changes must be backward compatible — existing code that does not use a scheduler must continue to work without modification
- No new package dependencies introduced
- Consistent pattern across all transports (follow the InMemory reference implementation)

### Constraints and Assumptions

- The Postgres transport does **not** need updating — it handles requeue delay natively via SQL `visible_timeout`
- MQTT currently has no consumer factory or channel factory — these will be created as part of this work, following the patterns established by other transports
- The `InMemoryChannelFactory` is the reference implementation for the correct wiring pattern
- The scheduler is optional (`null` by default) on all constructors to maintain backward compatibility

### Out of Scope

- Postgres scheduler support (uses native SQL delay)
- Changes to the scheduler implementations themselves
- New scheduler types or strategies
- Producer-side scheduler wiring (already complete in spec 0002)

## Acceptance Criteria

- AC-1: A user can register a scheduler via `UseMessageScheduler()` and have it automatically available in RMQ, Kafka, MQTT, MsSql, and Redis consumers created through the standard channel factory / DI path
- AC-2: Existing applications that do not register a scheduler continue to work without any code changes
- AC-3: Integration tests exist for at least one transport (e.g., RMQ or Redis) verifying that scheduler-based requeue delay works end-to-end when wired through the channel factory
- AC-4: The `InMemoryChannelFactory` pattern is consistently followed across all updated transports
- AC-5: MQTT has a consumer factory and channel factory consistent with other transports, and the scheduler is wired through them

## Additional Context

This was identified during review of spec 0002-universal_scheduler_delay. The consumer-side scheduler parameter was added but the factory chain was not updated, making the feature unreachable via standard configuration for production transports. See the investigation summary:

| Transport | Consumer has scheduler? | Factory passes it? | Status |
|---|---|---|---|
| InMemory | Yes | Yes | **Working** |
| RMQ Async | Yes | No | **Needs fix** |
| RMQ Sync | Yes | No | **Needs fix** |
| Kafka | Yes | No | **Needs fix** |
| MsSql | Yes | No | **Needs fix** |
| Redis | Yes | No | **Needs fix** |
| MQTT | Yes | No factory exists | **Needs factory + wiring** |
| Postgres | No (native) | N/A | Not applicable |
