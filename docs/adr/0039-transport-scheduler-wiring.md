# 39. Transport Channel Factory Scheduler Wiring

Date: 2026-02-19

## Status

Accepted

## Context

**Parent Requirement**: [specs/0004-transport-scheduler-wiring/requirements.md](../../specs/0004-transport-scheduler-wiring/requirements.md)

**Scope**: This ADR covers how `IAmAMessageScheduler` flows from DI registration through the channel factory chain to transport consumers, and the creation of missing MQTT factory infrastructure.

In spec 0002 (ADR 0037), consumers for RMQ, Kafka, MQTT, MsSql, and Redis were updated to accept an optional `IAmAMessageScheduler?` constructor parameter. The scheduler enables delayed requeue by delegating the delay to a scheduler implementation (e.g. Hangfire, Quartz) rather than blocking the consumer thread.

However, the **factory chain** that creates these consumers was not updated. Brighter uses a two-layer factory pattern:

```
IAmAChannelFactory
  └── holds IAmAMessageConsumerFactory
        └── creates IAmAMessageConsumer{Sync,Async}
              └── needs IAmAMessageScheduler (optional)
```

Currently, neither the `IAmAMessageConsumerFactory` implementations nor the `ChannelFactory` wrappers accept or forward a scheduler. The `IAmAMessageScheduler` is registered in DI via `UseMessageScheduler()`, but `BuildDispatcher` never resolves it and passes it to channel factories.

The `InMemoryChannelFactory` is the sole exception — it accepts `IAmAMessageScheduler?` in its constructor and passes it to every `InMemoryMessageConsumer` it creates. This is the reference pattern.

Additionally, the MQTT transport has no `IAmAMessageConsumerFactory` or `IAmAChannelFactory` implementation at all, making it impossible to use MQTT consumers through the standard DI/dispatcher path.

### Forces

- The `IAmAChannelFactory` and `IAmAMessageConsumerFactory` interfaces currently have no knowledge of schedulers — they take only `Subscription`
- Adding scheduler to these interfaces would break all existing implementations (including third-party)
- The scheduler is a cross-cutting concern used by consumers for delayed requeue
- Without a scheduler, consumers that need to requeue with delay have no mechanism to do so without blocking
- The `InMemoryScheduler` and `InMemorySchedulerFactory` already exist in the core library and provide a timer-based scheduler that requires no external infrastructure
- Backward compatibility is essential — existing code must work without changes
- MQTT lacks factory infrastructure, making it inconsistent with other transports

## Decision

We will:

1. **Register `InMemorySchedulerFactory` as the default** when the user does not explicitly configure a scheduler via `UseScheduler()` or `UseMessageScheduler()`. This ensures a scheduler is always available — consumers never receive a null scheduler. Users who want Hangfire, Quartz, or another scheduler can still override by calling `UseScheduler()` as before.

2. **Thread the scheduler through the factory chain** using constructor injection on the concrete factory classes, without modifying the `IAmAChannelFactory` or `IAmAMessageConsumerFactory` interfaces. The DI registration path will resolve the scheduler and pass it to channel factories.

### Default Scheduler Registration

The `InMemorySchedulerFactory` already exists in `Paramore.Brighter` and creates `InMemoryScheduler` instances that use a timer to schedule a `FireSchedulerRequest` (via `TimeProvider`) to schedule delayed command processor calls. It requires no external infrastructure (no Hangfire, no Quartz, no database).

During DI registration, if no `IAmAMessageSchedulerFactory` has been registered (i.e., the user did not call `UseScheduler()` or `UseMessageScheduler()`), we register `InMemorySchedulerFactory` as the default using `TryAddSingleton`. This follows the same pattern Brighter uses elsewhere — sensible defaults with opt-in overrides.

```csharp
// In the DI registration path (e.g., AddServiceActivator or BuildDispatcher)
builder.Services.TryAddSingleton<IAmAMessageSchedulerFactory>(new InMemorySchedulerFactory());
```

Because `TryAddSingleton` is a no-op if a registration already exists, calling `UseScheduler(new HangfireMessageSchedulerFactory(...))` before or after still takes precedence. The `InMemoryScheduler` is only used when no explicit scheduler is configured.

This means:
- Consumers **always** have a scheduler — null checks for scheduler become unnecessary
- The default `InMemoryScheduler` provides timer-based delayed requeue out of the box
- Users who need durable/persistent scheduling (surviving process restarts) still configure Hangfire/Quartz explicitly

### Architecture Overview

```
DI Container
  ├── IAmAMessageSchedulerFactory (always present: InMemorySchedulerFactory default, or user-configured)
  │     └── creates IAmAMessageScheduler
  │
  └── BuildDispatcher resolves scheduler, passes to channel factory
        │
        ChannelFactory(consumerFactory, scheduler)      ← updated constructor
          └── IAmAMessageConsumerFactory(config, scheduler)   ← updated constructor
                └── new XxxMessageConsumer(..., scheduler)    ← already accepts it
```

The scheduler flows **downward** through the factory chain via constructor parameters. Each layer stores it and forwards it to the next layer when creating objects.

### Key Components and Responsibilities

**1. Transport Consumer Factories** (knowing + doing)

Each transport's `IAmAMessageConsumerFactory` implementation gains an optional `IAmAMessageScheduler?` constructor parameter:

- `RmqMessageConsumerFactory` (Async and Sync) — knows the RMQ connection config and scheduler; creates `RmqMessageConsumer` with both
- `KafkaMessageConsumerFactory` — knows the Kafka config and scheduler; creates `KafkaMessageConsumer` with both
- `MsSqlMessageConsumerFactory` — knows the MsSql config and scheduler; creates `MsSqlMessageConsumer` with both
- `RedisMessageConsumerFactory` — knows the Redis config and scheduler; creates `RedisMessageConsumer` with both
- `MqttMessageConsumerFactory` (**new**) — knows the MQTT config and scheduler; creates `MqttMessageConsumer` with both

The responsibility is: **knowing** the transport configuration and scheduler; **doing** consumer construction with correct parameters.

**2. Transport Channel Factories** (coordinator)

Each transport's `IAmAChannelFactory` implementation gains an optional `IAmAMessageScheduler?` constructor parameter:

- `ChannelFactory` in RMQ Async, RMQ Sync, Kafka, MsSql, Redis — updated to accept scheduler and pass it to the consumer factory
- `MqttChannelFactory` (**new**) — created following the same pattern as other transports

The channel factory's responsibility is: **coordinating** the creation of channels by delegating consumer creation to the consumer factory and wrapping the result in `Channel`/`ChannelAsync`.

For the existing transports where the `ChannelFactory` constructs the consumer factory internally, the scheduler is passed through. For transports where the consumer factory is injected, the caller must construct the consumer factory with the scheduler.

**3. DI Registration** (deciding)

The DI registration path is responsible for two things:

**a) Ensuring a scheduler factory is always registered:**

During Brighter's DI setup, we register `InMemorySchedulerFactory` as the default `IAmAMessageSchedulerFactory` using `TryAddSingleton`. If the user has already called `UseScheduler()` or `UseMessageScheduler()`, this is a no-op. The `IAmAMessageScheduler` service registration (which resolves from the factory) follows the same `TryAddSingleton` pattern.

**b) Passing the scheduler to channel factories:**

`ServiceActivatorServiceCollectionExtensions.BuildDispatcher` is updated to:
1. Resolve `IAmAMessageScheduler` from the service provider (always present due to default registration)
2. Pass it to the channel factory

Since `options.DefaultChannelFactory` is user-supplied (already constructed), and per-subscription factories may also be pre-built, we need a mechanism to set the scheduler on existing factory instances. We have two options:

**Option A — Setter on channel factories**: Add a `Scheduler` property to each channel factory. `BuildDispatcher` resolves the scheduler from DI and sets it on the factory after obtaining it from options. This avoids requiring users to change how they construct factories.

**Option B — Users pass scheduler when constructing factories**: Users who register a custom channel factory via `options.DefaultChannelFactory` must construct it with the scheduler themselves. `BuildDispatcher` only handles the fallback `InMemoryChannelFactory`.

We choose **Option A** because it preserves backward compatibility and works with the existing DI pattern where users construct and assign `DefaultChannelFactory` in `AddServiceActivator`. The setter approach also aligns with how `Scheduler` is already set on producers via a property in the existing `UseMessageScheduler` path.

To implement Option A consistently, we define a property on the concrete channel factories:

```csharp
// On each transport's ChannelFactory (concrete class, not on the interface)
public IAmAMessageScheduler? Scheduler { get; set; }
```

This keeps the `IAmAChannelFactory` interface unchanged. `BuildDispatcher` can check if the factory implements a scheduler-aware pattern and set the property. To make this type-safe without downcasting, we introduce a small optional interface:

```csharp
public interface IAmAChannelFactoryWithScheduler
{
    IAmAMessageScheduler? Scheduler { get; set; }
}
```

Channel factories that support scheduler injection implement this interface in addition to `IAmAChannelFactory`. `BuildDispatcher` checks for this interface and sets the scheduler if available.

**4. MQTT Factory Infrastructure** (new classes)

Two new classes following the established patterns:

- `MqttMessageConsumerFactory : IAmAMessageConsumerFactory` — mirrors the pattern from `RmqMessageConsumerFactory` / `KafkaMessageConsumerFactory`
- `MqttChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler` — mirrors the pattern from other transports' `ChannelFactory`

### Implementation Approach

1. **Register `InMemorySchedulerFactory` as default** — in the DI registration path, use `TryAddSingleton<IAmAMessageSchedulerFactory>(new InMemorySchedulerFactory())` so a scheduler is always available. Also ensure the `IAmAMessageScheduler`, `IAmAMessageSchedulerSync`, and `IAmAMessageSchedulerAsync` service registrations are present via `TryAddSingleton` (mirroring the pattern in `UseMessageScheduler`)
2. **Add `IAmAChannelFactoryWithScheduler` interface** to `Paramore.Brighter` core — a single optional interface with one property
3. **Update each transport's consumer factory** to accept `IAmAMessageScheduler?` in its constructor (optional parameter, defaults to null — still nullable for direct construction outside DI)
4. **Update each transport's channel factory** to:
   - Accept `IAmAMessageScheduler?` in its constructor (optional, defaults to null)
   - Implement `IAmAChannelFactoryWithScheduler`
   - Pass the scheduler to the consumer factory (or directly to consumers for transports that construct consumers directly)
5. **Create `MqttMessageConsumerFactory`** and **`MqttChannelFactory`** following the same pattern
6. **Update `InMemoryChannelFactory`** to also implement `IAmAChannelFactoryWithScheduler` for consistency
7. **Update `BuildDispatcher`** to resolve `IAmAMessageScheduler` from DI and set it on the channel factory if it implements `IAmAChannelFactoryWithScheduler`

## Consequences

### Positive

- The scheduler feature becomes reachable via standard DI configuration for all production transports
- Delayed requeue works out of the box with no configuration — the `InMemoryScheduler` default means consumers always have a scheduler available
- Users who need durable scheduling (surviving restarts) can still opt into Hangfire/Quartz via `UseScheduler()`
- No breaking changes — all new parameters are optional with null defaults; the default registration uses `TryAddSingleton` which is a no-op if already registered
- The `IAmAChannelFactory` and `IAmAMessageConsumerFactory` interfaces remain unchanged, preserving third-party compatibility
- MQTT gains factory infrastructure consistent with other transports, closing a long-standing gap
- The `IAmAChannelFactoryWithScheduler` pattern is opt-in — existing factories that don't need scheduling support are unaffected

### Negative

- Adds a new interface (`IAmAChannelFactoryWithScheduler`) to the core library
- `BuildDispatcher` gains a runtime type check (`is IAmAChannelFactoryWithScheduler`)
- Each transport's factory constructors grow by one parameter (though optional)
- The default `InMemoryScheduler` uses in-process timers — delayed requeue won't survive process restarts (but this is clearly documented and easily overridden)

### Risks and Mitigations

- **Risk**: Users may not realise the default `InMemoryScheduler` is non-durable and lose scheduled requeues on process restart
  - **Mitigation**: Document clearly that `InMemoryScheduler` is best-effort/in-process; recommend Hangfire/Quartz for production scenarios requiring durability
- **Risk**: Third-party channel factories won't get scheduler injection automatically
  - **Mitigation**: They can implement `IAmAChannelFactoryWithScheduler` to opt in; the feature degrades gracefully (scheduler is just null)
- **Risk**: MQTT factory may not cover all edge cases that other transports handle
  - **Mitigation**: Follow the established patterns exactly; test with the existing MQTT integration test infrastructure

## Alternatives Considered

### 1. Add scheduler to `IAmAChannelFactory` interface

Rejected — this is a breaking change to a public interface. All existing implementations (including third-party) would need updating.

### 2. Pass scheduler via `Subscription`

Rejected — the scheduler is a runtime/infrastructure concern, not a subscription configuration concern. It would pollute the subscription model with DI responsibilities.

### 3. Resolve scheduler from DI inside each consumer

Rejected — consumers don't have access to the DI container and shouldn't. This would violate the principle of constructor injection and make testing harder.

### 4. No default scheduler — require explicit configuration

Rejected — this was the status quo. Without a default, consumers receive a null scheduler and delayed requeue silently does nothing (or blocks the thread). Providing `InMemorySchedulerFactory` as a default gives working delayed requeue out of the box with zero configuration, following the principle of sensible defaults.

### 5. Only update `BuildDispatcher` fallback `InMemoryChannelFactory`

Rejected — this would only fix the fallback case. Users who register transport-specific channel factories (which is all production usage) would still have no scheduler wiring.

## References

- Requirements: [specs/0004-transport-scheduler-wiring/requirements.md](../../specs/0004-transport-scheduler-wiring/requirements.md)
- Related ADR: [0037-universal-scheduler-delay.md](0037-universal-scheduler-delay.md) — the original scheduler feature
- `InMemoryChannelFactory` — reference implementation for correct scheduler threading
- `UseMessageScheduler` in `ServiceCollectionExtensions` — existing producer-side scheduler registration
