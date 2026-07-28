---
id: 0066-release-factory-instances-on-an-opaque-lease
title: Key Release Factory Created Instance via an Opaque `Lease<T>` 
status: Accepted
author:
  - "Ian Cooper"
created: 2026-07-28
summary: "Changes what a DI-friendly factory returns from being the instance to being a lease of the instance, which includes the instance and an opaque token, which allows us to release the scope associated with a transient instance from amongst scopes that we have loaded"
tags:
  - "lifetime"
  - "dependency injection"
  - "scope"
---

# 66. Key Release Factory Created Instance via an Opaque `Lease<T>`

Date: 2026-07-28

## Status
Accepted

## Context

The DI-friendly factories (mapper, transformer, handler) leaked memory. On master, a `Transient` resolution goes through `ServiceProviderLifetimeScope`, which creates **one shared child scope** the first time it is needed and reuses it for *every* subsequent resolution, disposing it only when the factory itself is disposed. `Release(instance)` disposes the resolved instance if it is `IDisposable` but never closes that scope. So each resolution's dependency graph is captured by the long-lived scope and retained for the lifetime of the factory — a per-message allocation that is never reclaimed on the hot path.

To fix the leak we must **control lifetime per resolution**: give each resolution its own child scope and close it on `Release`. That turns `Release` into the problem — it must close *the matching enclosing scope*, the one that leased this instance, and the bare instance cannot identify it. A *shared* instance (a container-`Singleton` mapper/transform resolved under the default `Transient` `MapperLifetime`/`TransformerLifetime`) is handed out from many scopes, so the instance no longer maps to a single scope. `Release` needs a **token for what leased the instance**, not the instance.

- **Why not just return the instance (the naïve option):** the caller (pipeline) must release *the specific resolution* it holds. Only a token that carries resolution identity — the resolution's own `IServiceScope` — lets `Release` reclaim exactly one scope and makes over-release an idempotent no-op. Returning the bare instance forces instance-keying, which *is* the bug.
- **Assembly-boundary force:** the lease type lives in **core** (`Paramore.Brighter`), but the real `IServiceScope` lives in the **DI** assembly. So the token must be an opaque `object?` the caller carries but cannot interpret; all DI-specific disposal (including the pump-deadlock context suppression) stays in the DI layer.
- **Forward influence:** this establishes "resolution-scoped lease" as the unit of lifetime, which is directly relevant to the planned **scope-handling improvements** (consumer scope = one message; producer side flowing the ambient ASP.NET request scope). The lease is the natural handle those designs can build on.

## Decision

`Create`/`Get` return a `sealed class Lease<T>` (opaque data: `Instance` + opaque `object? ReleaseToken`); `factory.Release(lease)` / `registry.Release<T>(lease)` key release on the resolution. Set-based tracking (`ConcurrentDictionary<IServiceScope, byte>` + idempotent `TryRemove`) replaces the per-instance stack, `InstanceComparer`, and `CollectScopesToRelease` re-home.

`Lease<T>` (new, in `Paramore.Brighter`) is a small `sealed class` pairing the resolved `Instance` with an opaque `ReleaseToken`. The token — for the DI-backed factories, the resolution's own `IServiceScope` — lets the factory reclaim exactly the one resolution being released, so a shared instance handed out under a transient lifetime is torn down one resolution at a time and an over-release is a no-op. A factory that opens a per-resolution scope returns a lease carrying its release token (the constructor's second argument); the token-less case — a shared instance, or a no-op factory that reclaims nothing on release — is built through a named `Lease<T>.ForSharedInstance(instance)` factory, not an implicit conversion from `T`. So "release reclaims nothing here" is a visible, deliberate choice at the call site rather than something a bare instance becomes silently: a factory that owns a scope cannot accidentally return a token-less lease, and a caller cannot pass a bare instance where a lease carrying a token is expected.

The lease's generic argument also carries the *interface*: `Get<T>` returns a `Lease<IAmAMessageMapper<T>>` and `GetAsync<T>` a `Lease<IAmAMessageMapperAsync<T>>`. A dual-interface mapper (e.g. `JsonMessageMapper<T>`, which implements both) resolved from `GetAsync` is therefore an async-typed lease that can only bind to the async `Release<T>` overload — routing it to the sync factory (a different `ServiceProviderLifetimeScope` than it was resolved from, so a silent leak) is a **compile-time type error**. The two `Release<T>` overloads are distinguished by parameter type, so they are plain public methods on the concrete registry and no interface cast is needed at the call site.

### Considered and rejected: lease is `IDisposable`/`IAsyncDisposable` (self-releasing)

A self-disposing lease (`using`/`await using`, `Release` gone from the interfaces) is the more modern shape, but was rejected on **impact**:

- It pushes the DI-specific pump-deadlock **context-suppression disposal** out of the DI layer into a token wrapper — worse separation of concerns.
- It's a **larger public-surface change** on an already-breaking major-version PR (removing `Release` from all six interfaces vs. changing its parameter).
- The chosen "opaque-data class + `factory.Release(lease)`" keeps Create/Release symmetry, so the conceptual change is smaller and all disposal logic stays where it belongs.

The user explicitly chose the opaque-data-class option over the self-disposing one; capture that this is a deliberate, reversible-if-warranted choice.

## Consequences

- Broad breaking surface: 6 interfaces, DI impls, registry, pipelines/builders, all test doubles.
- Because `Lease<T>` is invariant, registry `Release<T>` is generic; the factory `Release` stays non-generic and the registry re-wraps, carrying the same token.
- Over-releasing a lease is an **idempotent no-op** rather than a use-after-dispose, and the dual-interface silent-misroute becomes a compile error (above): both hazards are removed by the type system rather than by discipline. The type system also keeps the token-less "reclaims nothing" lease to the explicit `ForSharedInstance` factory, so a scope-owning factory cannot manufacture one by accident and silently leak the scope it was meant to reclaim.
- Keying on the resolution rather than the instance closes the shared-instance over-release/use-after-dispose bug class outright, so the instance-keyed stack and its concurrent re-home pass are no longer needed. See ADR 0067 for the internal `ServiceProviderLifetimeScope` mechanism this decision is realised by.
- The per-resolution lease is the natural handle for the planned scope-handling work (see *Forward influence* above): a consumer scope of one message, and a producer side flowing an ambient request scope, both key off it.
