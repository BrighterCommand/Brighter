---
id: 0067-per-resolution-di-scope-for-transient-factory-instances
title: "Per-resolution DI scope for transient factory instances"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-28
summary: "A transient mapper/transform/handler is resolved in its own IServiceScope per resolution, tracked by scope identity in a set and released idempotently; the factory's own disposal drains any un-released scope as a safety net, closing the per-message scope leak of #4252."
tags:
  - "di"
  - "lifetime"
  - "memory"
---

# 67. Per-resolution DI scope for transient factory instances

Date: 2026-07-28

## Status

Proposed

## Context

**Scope**: how `ServiceProviderLifetimeScope` — the DI-backed lifetime helper shared by the mapper, transformer and handler factories — creates, tracks and releases the `IServiceScope` for a *transient* resolution.

[ADR 0039](0039-scoping-dependencies-inline-with-lifetime-scope.md) established a lifetime scope per subscriber so that handlers in a `Publish` fan-out do not share scoped dependencies. Within that model the mapper and transformer factories created a **single** `IServiceScope` per factory and reused it for every transient resolution. `MapperLifetime` and `TransformerLifetime` both **default to `Transient`**, and those factories live for the application's lifetime, so that one scope was never released between messages: every transient mapper or transform accumulated a scope for the life of the process. That is the leak reported in #4252, and because the default lifetime is `Transient` it was the default code path, not an opt-in.

A scope owns more than the instance it produced: it also owns whatever that instance captured from it, including the scope's own `IServiceProvider`, which the container injects when a constructor asks for one. So a scope's lifetime must follow the *resolution*, not the instance's disposability — disposing a scope while its instance is still in use hands that instance a disposed provider.

## Decision

`ServiceProviderLifetimeScope` creates a **fresh `IServiceScope` per transient resolution** and disposes it when that resolution is released.

### Track by scope identity, release idempotently

- Each transient resolution's scope is recorded in a set keyed by the **scope's own reference** (`ConcurrentDictionary<IServiceScope, byte>`). The scope *is* the resolution's identity; the returned release token (see [ADR 0066](0066-release-factory-instances-on-an-opaque-lease)) is that scope.
- `Release(token)` is an atomic `TryRemove` followed by disposal, so releasing exactly the one resolution's scope, and a second release of the same token is a no-op — never a pop of another resolution's scope.
- The tracking set is not keyed by the resolved instance. A shared instance (a container-`Singleton` resolved under `Transient`) therefore has one distinct entry per resolution rather than a stack under one key — the mechanism that realises ADR 0066's per-resolution keying.

### The factory's disposal is the safety net

An un-released resolution's scope is drained when the lifetime scope itself is disposed: `Dispose()` iterates the set, removing and disposing each scope best-effort (a throwing disposal is logged, not propagated, so one failure cannot skip the rest). This bounds an un-released resolution to the host's lifetime rather than leaking to process exit.

### Two resolution entry points

- The **mapper and transformer factories** take the token-returning entry point and release each resolution's scope per message — this is what closes the leak.
- The **handler factory** takes a token-discarding entry point: a handler is resolved through a lifetime scope created per request pipeline (one `IAmALifetime`) and disposed when that pipeline completes, so the whole scope is drained at pipeline end and there is no per-instance release. The transient-handler scope granularity within a pipeline is governed separately by `IBrighterOptions.IsolateTransientHandlerScope`.

### Scope disposal respects the async pump

A scope is disposed through `IAsyncDisposable` when it offers it. On a thread owned by the Proactor's single-threaded synchronization context the synchronous disposal path suppresses that context for the duration of the wait, so a user `DisposeAsync` continuation posted back to it resumes on the thread pool rather than deadlocking the pump.

## Consequences

### Positive

- Closes #4252: each transient mapper/transform gets and releases its own scope, so a long-running host no longer accumulates one scope per message.
- Releasing one resolution can never dispose a scope another live resolution depends on (realises ADR 0066), and disposal is deterministic rather than GC-timed (see ADR 0068).

### Negative

- A transient resolution allocates and retains a scope until it is released, **even for a non-disposable instance** (the scope owns the instance's injected provider). Callers that resolve a mapper/transform directly must release it; see ADR 0066 for the lease contract they use.

## Alternatives Considered

### Keep one shared factory scope, dispose only `IDisposable` instances

The pre-fix model. **Rejected because** it leaks the scope (and the instance's injected `IServiceProvider`) for the process lifetime under the default `Transient` lifetime, and it cannot free a resolution's dependencies until the factory itself is disposed at shutdown — the #4252 defect.

### Track scopes keyed by the resolved instance

**Rejected because** a shared instance stacks many scopes under one key, so release cannot tell resolutions apart — the exact use-after-dispose / over-release hazard ADR 0066 exists to remove.

## References

- Realises: [ADR 0066: Key Release Factory Created Instance via an Opaque `Lease<T>`](0066-release-factory-instances-on-an-opaque-lease)
- Refines: [ADR 0039: Scoping dependencies inline with lifetime scope](0039-scoping-dependencies-inline-with-lifetime-scope.md)
- Related: [ADR 0068: Deterministic disposal — the finalizer is a safety net](0068-deterministic-disposal-finalizer-safety-net.md), [ADR 0069: Ownership and disposal cascade for mapper/transform factories](0069-factory-registry-ownership-and-disposal-cascade.md)
