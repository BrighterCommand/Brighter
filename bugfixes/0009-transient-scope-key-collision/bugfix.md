# Bugfix: Transient scope key collision leaks the prior scope

**Linked Issue**: PR #4254 review — point 2
**Status**: Verified

## Symptom
`ServiceProviderLifetimeScope.GetTransient<T>` creates a fresh `IServiceScope` per call and tracks
it keyed by the resolved instance: `_transientScopes[instance] = scope;`. When the *same* instance
is resolved twice — which happens when `MapperLifetime`/handler/transformer lifetime is configured
`Transient` but the type is actually registered as a *singleton* in the container, so
`GetService` returns the same reference every call — the indexer assignment **overwrites** the
previous entry. The earlier `IServiceScope` is dropped from the dictionary and never disposed:
one leaked (empty) scope per repeated resolution, and a later `Release(instance)` drains only the
last scope stored.

Expected: repeated resolution of the same instance must not silently leak scopes; every scope
created is eventually disposed.

## Suspected Location
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:135` —
  `_transientScopes[instance] = scope;` indexer overwrite (the defect).
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:125-146` —
  `GetTransient<T>` full method (creation + tracking + the `_disposed` re-check dance).
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:169-175` —
  `Release`, which `TryRemove`s a single scope per instance.
- `.../ServiceProviderLifetimeScope.cs:192-206` — `DisposeScope`, the async-aware disposer to reuse.

## Root-Cause Hypothesis
The indexer set (`_transientScopes[instance] = scope`) assumes each transient resolution yields a
distinct instance, so the key is unique. That assumption breaks when the underlying registration
is a singleton (or otherwise returns a shared instance): the second call's `scope` overwrites the
first, orphaning the first scope with no remaining reference, so it is never disposed.

**Proposed fix (UNVERIFIED — to be proven or refuted in /bugfix:confirm):** replace the indexer
assignment with `TryAdd`; if it fails (an entry already exists for this instance), the newly
created `scope` is redundant and must be disposed immediately (dispose the loser) rather than
dropped, keeping the already-tracked scope so `Release` still drains it. This makes repeated
resolution defensive instead of silently leaky.

## Confirmed Root Cause
**CONFIRMED.** `GetTransient<T>` (`ServiceProviderLifetimeScope.cs:125-146`) creates a fresh
`IServiceScope` on every call (`:127`) and stores it in a `ConcurrentDictionary<object, IServiceScope>`
keyed by the resolved **instance** under reference equality (`InstanceComparer`, `:44-45`, `:229-236`).
The store is an **indexer assignment** (`:135`), not an add. When `GetService` returns the *same*
reference twice, the second assignment replaces the first scope; the first scope now has no remaining
reference. `Release(instance)` (`:169-175`) `TryRemove`s only the single currently-stored scope, and
`Dispose()` (`:219-221`) only drains scopes still in the dictionary — so the overwritten scope is
disposed by neither. One leaked `IServiceScope` per orphaning overwrite.

The "same instance twice under Transient lifetime" precondition is genuinely reachable: Brighter
registers mapper/transform types with `TryAdd(... ServiceLifetime.Transient)`
(`ServiceCollectionMessageMapperRegistryBuilder.cs:80,99,116-137`), so a user `AddSingleton<MyMapper>()`
wins the container registration while `MapperLifetime` stays configured `Transient`. The single
long-lived `_lifetimeScope` in `ServiceProviderMapperFactory`/`ServiceProviderTransformerFactory`
(`.cs:36,46`) then resolves the same singleton reference on every `Create`, colliding on the key.

## Evidence
- [x] Code-trace:
  1. Mapper/transform types registered `TryAdd(..., Transient)` — a user `AddSingleton` wins; lifetime
     stays `Transient` (`ServiceCollectionMessageMapperRegistryBuilder.cs:80,99,116-137`).
  2. Factories hold ONE process-wide `_lifetimeScope` (`ServiceProviderMapperFactory.cs:36,45-46`;
     `ServiceProviderTransformerFactory.cs:36,46`) — leaks accumulate to process shutdown, not a
     bounded handler lifetime.
  3. `Create` → `GetOrCreate<T>` → `GetTransient` (`:75-86,125`). Call 1: `scope1=CreateScope()`,
     `instance=S`, `_transientScopes[S]=scope1`. Call 2 (same request type): `scope2=CreateScope()`,
     `GetService` returns same `S`, `_transientScopes[S]=scope2` **overwrites** → `scope1` unreferenced,
     never disposed.
  4. `Release(S)` (`:173`) disposes only `scope2`; `scope1` permanently leaked. `Dispose()` (`:219-221`)
     iterates only remaining `.Values`, so shutdown does not reclaim `scope1` either.
- Reference-equality key confirmed: `InstanceComparer.Equals => ReferenceEquals`,
  `GetHashCode => RuntimeHelpers.GetHashCode` (`:233-235`).

## Scope Notes
- **Suggested-fix assessment: PARTIAL.** `TryAdd` + "dispose the loser scope immediately" is safe only
  when the shared instance is a **container-owned singleton** (a child scope does not track a root-owned
  singleton, so disposing the loser does not dispose `S`). It introduces a **use-after-dispose
  regression** in the shared-disposable-transient case: with a custom `AddTransient(sp => sharedDisposable)`
  the loser `scope2` *owns* `S`'s disposal, so disposing it eagerly at resolution disposes an instance the
  caller is about to use. The current overwrite code does not have this fault (it defers disposal to
  `Release`/`Dispose`). ⇒ **Widen the fix** to one that never disposes a scope while its instance is in
  use — track a **stack/bag of scopes per instance** (`ConcurrentDictionary<object, ConcurrentStack<IServiceScope>>`):
  `GetTransient` pushes; `Release` pops+disposes exactly one (removes the key when empty); `Dispose`
  drains all. Symmetric create/release, no leak, no premature dispose.
- `_disposed` re-check dance (`:137-143`): must be preserved for the *tracked* scope under any rewrite —
  "whoever sees the entry after the drain owns disposing it."
- Handler factory is NOT affected: `ServiceProviderHandlerFactory` uses a per-`IAmALifetime` scope
  reclaimed at handler-lifetime end (`ServiceProviderHandlerFactory.cs:125-135`). The unbounded leak is
  specific to the mapper/transformer factories' shared long-lived `_lifetimeScope`.

## Regression Test
**Status: RED — APPROVED by user (2026-07-24).** Deterministic, no threads/GC, no live infra.

`tests/Paramore.Brighter.Extensions.Tests/When_the_same_transient_mapper_is_resolved_twice_before_release_should_dispose_every_scope.cs`
(`TransientScopeKeyCollisionTests`).

- **Arrange:** mapper registered `AddSingleton` (so every resolve returns the same reference) with
  `MapperLifetime = Transient`; a `ScopeTracker` wraps the real `IServiceScopeFactory` counting
  scopes **created** vs **disposed** (both via a `TrackingScope`).
- **Act:** two `Create` calls for the same instance *before* either `Release` (the key collision),
  then `Release` both.
- **Assert:** `Assert.Same(first, second)` documents the collision precondition;
  `CreatedCount == DisposedCount` proves no scope is orphaned.

RED today: `Assert.Equal() Expected: 2, Actual: 1` — the second `Create` overwrote the first scope
in `_transientScopes`, orphaning it (`Release`/`Dispose` reclaim only tracked scopes). net9.0.

**Approved fix (stack-per-instance):** `ConcurrentDictionary<object, ConcurrentStack<IServiceScope>>`
— `GetTransient` pushes, `Release` pops+disposes one (remove key when empty), `Dispose` drains all.
Preserve the `_disposed` post-add re-check for the tracked scope.

## Fix
**File:** `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs`.

Changed `_transientScopes` from `ConcurrentDictionary<object, IServiceScope>` to
`ConcurrentDictionary<object, ConcurrentStack<IServiceScope>>` (still `InstanceComparer.Default`):
- `GetTransient`: `GetOrAdd` the instance's stack and `Push` the new scope (replaces the overwriting
  indexer set). The `_disposed` re-check now drains the *local* stack reference (reclaimed even after
  `Dispose` removed the entry) — `TryPop` atomicity gives exactly-once disposal.
- `Release`: `TryPop` one scope per call; when the stack empties, detach the key via the conditional
  `ICollection<KeyValuePair>.Remove` (matches the exact stack) so the instance isn't retained, with a
  re-drain to cover a concurrent push racing the emptiness check.
- `Dispose`: drain every stack (`TryPop` loop) before `Clear`.

Minimal, no signature/API changes. Symmetric create/release, no scope leak on key collision, and no
premature dispose (disposal stays deferred to `Release`/`Dispose`, unlike the rejected TryAdd+dispose-loser).

**Test:** `TransientScopeKeyCollisionTests` GREEN (net9.0). Sibling scope-leak tests
(`TransientMapperScopeAccumulationTests`, `TransformerFactory*`) still green — 9/9.
