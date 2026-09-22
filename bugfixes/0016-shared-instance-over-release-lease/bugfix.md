# Bugfix: Release keyed on instance identity over-releases a shared resolution's scope

**Linked Issue**: PR #4254 review, A#3 (opaque-lease redesign; supersedes bugfix 0009/0012 residual window)
**Status**: Fixed

## Symptom
`ServiceProviderLifetimeScope` keyed its per-resolution transient `IServiceScope`s by the **instance**
returned (`InstanceComparer` over a `ConcurrentDictionary<object, ConcurrentStack<IServiceScope>>`). When a
factory hands out a **shared** instance — e.g. a container-`Singleton` mapper/transform resolved under the
default `Transient` `MapperLifetime`/`TransformerLifetime` — every resolution pushes its own scope onto one
stack keyed by that single instance. `Release(instance)` then pops an **arbitrary** scope (LIFO), so
releasing one resolution can dispose a scope another **still-live** resolution depends on — a
use-after-dispose (the released instance's captured `IServiceProvider` is now disposed). Over-releasing the
same instance pops yet another live resolution's scope. The `CollectScopesToRelease` re-home pass (bugfix
0012) narrowed but never closed the matching concurrent window.

## Fix (reviewer's option 1 — opaque lease)
Key release on the **resolution**, not the instance. `Create`/`Get` now return a `Lease<T>` carrying the
instance plus an opaque release token (for the DI-backed factories, the resolution's own `IServiceScope`).
`Release(lease)` disposes exactly that scope. `ServiceProviderLifetimeScope` tracks outstanding transient
scopes in a **set keyed by the scope reference** (`ConcurrentDictionary<IServiceScope, byte>`) and releases
one via an atomic `TryRemove`, so:
- two resolutions of a shared instance have **two distinct entries**; releasing one leaves the other's scope
  intact — no use-after-dispose;
- an **over-release** of a lease is an idempotent no-op (the second `TryRemove` finds nothing), never a pop
  of another resolution's scope.

This deletes `InstanceComparer`, the per-instance `ConcurrentStack`, and `CollectScopesToRelease`'s re-home
pass — the whole shared-instance bug class, plus bugfix 0009 (key collision) and bugfix 0012's residual
window, are designed out. The change threads the lease across the 6 factory/registry interfaces, their
impls, the transform lifetime scopes, the pipelines and the builders. See PR #4254 / release_notes.md for the
public-surface (breaking) detail.

## Regression Test (RED-proven)
`tests/Paramore.Brighter.Extensions.Tests/When_releasing_one_lease_of_a_shared_mapper_the_other_resolution_stays_usable.cs`:
a Singleton-registered mapper resolved twice under `Transient` (two leases over one shared instance, two
scopes). Releasing the **first** lease disposes exactly the first scope and leaves the second live;
over-releasing the first lease is a no-op that does not touch the second's scope; releasing the second lease
disposes its own scope. RED-proven by making `Release` dispose an arbitrary outstanding scope (the
resolution-blind behaviour the old instance keying could not avoid): the "first scope disposed, second
still live" assertion flips.

Prior tests `TransientScopeKeyCollisionTests` (bugfix 0009) and the reflection-based release/dispose-drain
tests were updated to the lease model (`_outstandingScopes` keyed by scope, `Assert.Same` on `lease.Instance`).

## Scope Notes
- No defaults changed. Public surface changes are the lease-typed `Create`/`Get`/`Release` on the 6
  interfaces (already a breaking major-version PR).
