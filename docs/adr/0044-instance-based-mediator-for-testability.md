# 44. Instance-Based Mediator for Testability

Date: 2025-12-23

## Status

Proposal

## Context

ADR 0033 established that `OutboxProducerMediator` should be a static singleton to ensure one outbox per application. However, this creates testability issues:

1. Tests cannot run in parallel - require `[Collection("CommandProcessor")]` serialization
2. Tests require explicit `ClearServiceBus()` calls in teardown
3. Static state bleeds between tests causing flaky failures
4. `InMemoryOutbox` instances are shared across tests unexpectedly

The singleton requirement is valid, but enforcing it via static fields is the wrong mechanism.

## Decision

1. **Convert static to instance-based**: Change `s_mediator` to `_mediator` instance field in `CommandProcessor`
2. **Preserve singleton via DI**: Register `IAmAnOutboxProducerMediator` with `ServiceLifetime.Singleton`
3. **Add service provider overloads**: Enable `Func<IServiceProvider, T>` configuration for `AddBrighter`, `AddProducers`, `AddConsumers`
4. **Integrate Options pattern**: Allow `PostConfigure<BrighterOptions>` for test-time overrides
5. **Deprecate ClearServiceBus**: Mark obsolete (still functional for transition)

## Consequences

**Positive:**
- Tests can run in parallel without `[Collection]` attributes
- Each test can have isolated `CommandProcessor` with own mediator
- Standard .NET Options pattern enables test overrides
- `InMemoryOutbox` properly isolated per test

**Negative:**
- Migration effort for existing tests (optional - old pattern still works)
- Slight API surface increase with new overloads

**Backward Compatibility:**
- All existing `Action<T>` overloads unchanged
- `ClearServiceBus()` still works (deprecated)
- DI registration patterns unchanged
