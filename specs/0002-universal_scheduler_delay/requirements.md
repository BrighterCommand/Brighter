# Requirements: Universal Scheduler Delay Support

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a **Brighter library user**, I would like **all message consumers handling a `DeferMessageAction` exception through either the `Requeue` or `RequeueAsync` method, which have a delay but do not have native support for send with delay to call the matching producer method `SendWithDelay` or `SendWithDelayAsync` method support of the producer, so that **I can use delayed messaging consistently across any transport without needing to understand each transport's native delay capabilities or limitations**.

### Current State

In V10, Brighter introduced message scheduling support via:
- `IAmAMessageSchedulerAsync` / `IAmAMessageSchedulerSync` interfaces
- `InMemoryScheduler` implementation
- `MessageSchedulerFactory` configuration in `IAmProducersConfiguration`

However, the current implementation of `IAmAMessageConsumer` and `IAmAMessageConsumerAsync` has inconsistencies:

| Producer | Native Delay Support | Scheduler Integration                              | Add call to SendWithDelay via Producer              |
|----------|-----|----------------------------------------------------|-----------------------------------------------------|
| RabbitMQ | Yes (via DLX/TTL) | Yes, but uses a scheduler when native not supported. | It should use a producer when native not available  |
| Azure Service Bus | Yes | Uses native                                        | No                                                  |
| AWS SQS | Partial (max 15 min) | Yes uses native scheduling                         | No                                                  |
| Kafka | No  | None                                               | Should use a producer for delay                     |
| Redis | No  | None                                               | Should use a Producer for delay                     |
| GCP Pub/Sub | Yes, by policy in subscription | No                                                 |
| InMemoryConsumer | None | No - uses direct timer for requeue delay           | It should use the In-memory producer                |

In order to support `InMemoryConsumer` then `InMemoryProducer` needs to stop using a timer to schedule events and 
instead use the built-in `InMemoryScheduler`.

This inconsistency means:
1. Users cannot rely on a uniform delay behavior across transports
2. The `InMemoryProducer` and `InMemoryConsumer` don't use the scheduler system at all
3. Some producers throw exceptions when scheduler is null; others silently skip delay
4. Testing with in-memory transport doesn't accurately reflect scheduler behavior

## Proposed Solution

All message producers should support configuring a message scheduler for delay handling, with `InMemoryScheduler` as the default when no scheduler is explicitly configured. This provides:

1. **Consistent API**: All `SendWithDelay`/`SendWithDelayAsync` calls work the same way
2. **Testability**: In-memory transport uses the same scheduler mechanism as production transports
3. **Flexibility**: Users can swap schedulers (in-memory, Hangfire, Quartz, etc.) without changing producer code
4. **Graceful Fallback**: Transports with native delay support can still use it, with scheduler as fallback

## Requirements

### Functional Requirements

#### FR1: Universal Scheduler Configuration
- The scheduler is injectable via `MessageSchedulerFactory` in `IAmProducersConfiguration`
- It MUST default to `InMemorySchedulerFactory`
- All message producers currently have an optional `IAmAMessageScheduler` property. It MUST be set from the 
  `MessageSchedulerFactory` on the `IAmProducersConfiguration` 
- As a consequence, if no scheduler is explicitly configured, `InMemoryScheduler` SHOULD be used as the default

#### FR2: InMemoryMessageProducer Scheduler Support
- `InMemoryMessageProducer` MUST use the configured scheduler for `SendWithDelay`/`SendWithDelayAsync`
- MUST replace the current direct `TimeProvider.CreateTimer()` implementation
- MUST maintain backward compatibility with existing tests that don't configure a scheduler

#### FR3: InMemoryMessageConsumer Producer Support
- The `Requeue(Message message, TimeSpan? timeOut)` method SHOULD delegate to the `InMemoryMessageProducer` `SendWithDelay` when `timeOut` is specified
- The `RequeueAsync(Message message, TimeSpan? timeOut)` method SHOULD delegate to the `InMemoryMessageProducer` 
  `SendWithDelayAsync` when `timeOut` is specified
- MUST maintain backward compatibility with existing behavior
- The `InMemoryMessageConsumer` needs to create the `InMemoryMessageProducer` lazily, in case delay is never used.
  It should use the topic for the message to be sent as the topic for the producer that it creates.

#### FR4: Consistent Behavior Across Transports
- All producers MUST behave consistently when:
  - Delay is `TimeSpan.Zero`: send immediately without using producer
  - Transports with native delay support MUST use native delay over producer (configurable)
  - No native delay support then use the producer for delays
  - If using a producer, create lazily, in case delay is never used
  - Use the topic from the message when creating the producer. Use sensible defaults over forcing configuration

#### FR5: Producer Scheduler Property
- All `IAmAMessageProducer` implementations MUST expose a settable `Scheduler` property
- This SHOULD BE present in most producers but needs verification and standardization

### Non-functional Requirements

#### NFR1: Performance
- Using scheduler for in-memory transport MUST NOT significantly degrade performance compared to direct timer
- Scheduler overhead for immediate sends (delay = 0) MUST be negligible

#### NFR2: Testability
- The scheduler integration MUST be testable with `TimeProvider` abstraction
- Tests MUST be able to advance time to verify scheduled message delivery

#### NFR3: Backward Compatibility
- Existing code that doesn't configure a scheduler MUST continue to work
- No breaking changes to public APIs

#### NFR4: Thread Safety
- All scheduler operations MUST be thread-safe
- Concurrent `SendWithDelay` calls MUST not cause race conditions

### Constraints and Assumptions

#### Constraints
- Must work with the existing `IAmAMessageScheduler` interface hierarchy
- Must not require changes to the core `IAmACommandProcessor` interface
- Must maintain compatibility with all supported .NET versions

#### Assumptions
- The `InMemoryScheduler` implementation is sufficient as a default
- Users who need persistent scheduling will configure external schedulers (Hangfire, Quartz)
- Native delay support is preferred when available (for transports like Azure Service Bus)

### Out of Scope

- Adding new scheduler implementations (e.g., database-backed schedulers)
- Modifying the `IAmAMessageScheduler` interface
- Adding delay support to message consumers other than `InMemoryMessageConsumer`
- Persistent scheduling across process restarts (this requires external schedulers)
- Distributed scheduler coordination

## Acceptance Criteria

### AC1: InMemoryMessageProducer Uses Scheduler
- [ ] `InMemoryMessageProducer.SendWithDelay()` uses configured scheduler
- [ ] `InMemoryMessageProducer.SendWithDelayAsync()` uses configured scheduler
- [ ] Default `InMemoryScheduler` is used when no scheduler configured
- [ ] Immediate sends (delay = 0) bypass scheduler

### AC2: InMemoryMessageConsumer Uses Scheduler
- [ ] If delay is not zero, then `InMemoryMessageConsumer.Requeue()` with timeout uses `InMemoryMessageProducer` 
  `SendWithDelay`
- [ ] If delay is not zero, then `InMemoryMessageConsumer.RequeueAsync()` with timeout uses `InMemoryMessageProducer` `SendWithDelayAsync`
- [ ] Immediate requeues (timeout = 0 or null) bypass producer

### AC3: Consistent Configuration
- [ ] All producers can be configured with scheduler via `MessageSchedulerFactory`
- [ ] Scheduler property is consistently named and typed across all producers

### AC4: Tests Pass
- [ ] All existing tests continue to pass
- [ ] New tests verify scheduler integration for in-memory transport
- [ ] Tests can use `TimeProvider` to control time for deterministic verification

### AC5: Documentation
- [ ] ADR documents the design decision
- [ ] XML documentation updated for modified public APIs

## Additional Context

### Related Code Locations

| Component | Path |
|-----------|------|
| Scheduler Interfaces | `src/Paramore.Brighter/IAmAMessageScheduler*.cs` |
| InMemoryScheduler | `src/Paramore.Brighter/InMemoryScheduler.cs` |
| InMemoryMessageProducer | `src/Paramore.Brighter/InMemoryMessageProducer.cs` |
| InMemoryMessageConsumer | `src/Paramore.Brighter/InMemoryMessageConsumer.cs` |
| Configuration | `src/Paramore.Brighter/ProducersConfiguration.cs` |
| RMQ Producer (reference) | `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` |

