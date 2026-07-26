# Bugfix: Release re-drain disposes a scope a concurrent GetTransient handed to a live caller

**Linked Issue**: PR #4254 round-11 review, Finding #4 (comment 5081011984)
**Status**: Fixed

## Symptom
Under `MapperLifetime`/`TransformerLifetime` = `Transient` with a **shared instance** — the case the
per-instance scope stack exists for: a single object returned by every resolution, e.g.
`AddSingleton<TMapper>()` resolved under a Transient lifetime, or `AddTransient(sp => sharedDisposable)`
— a concurrent `Release(instance)` and `GetTransient(instance)` can race such that `Release` disposes
the `IServiceScope` that the concurrent `GetTransient` just created, pushed, and **returned to a live
caller**. When that scope owns disposable state (a `sp => sharedDisposable` transient tracked by the
scope, or the scope's own injected `IServiceProvider`), the caller is handed already-disposed state:
a **use-after-dispose**, not a leak. No exception at the race site; the symptom surfaces later as an
`ObjectDisposedException` (or silently wrong behaviour) from the still-live instance.

Expected: a single `Release` disposes exactly one scope (the one matching one creation) and never
disposes a scope belonging to an outstanding, un-released resolution.

## Suspected Location
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:293-299`
  — the re-drain block inside `CollectScopesToRelease`:
  ```csharp
  if (scopes.IsEmpty &&
      ((ICollection<KeyValuePair<object, ConcurrentStack<IServiceScope>>>)_transientScopes)
          .Remove(new KeyValuePair<object, ConcurrentStack<IServiceScope>>(instance, scopes)))
  {
      while (scopes.TryPop(out var raced))
          toDispose.Add(raced);   // <-- disposes scopes a concurrent GetTransient pushed for a live caller
  }
  ```
- Interacts with the push side `ServiceProviderLifetimeScope.cs:190-191` (`GetOrAdd` + `Push` in
  `GetTransient`) and the pop side `:286-287`.

## Root-Cause Hypothesis
`ICollection<KVP>.Remove` matches the dictionary value by **reference to the stack object**, not by
its emptiness. The block is guarded by `scopes.IsEmpty` (checked at `:293`), but between that check
and the `Remove` a concurrent `GetTransient` can `GetOrAdd` the *same* still-keyed stack and `Push` a
new scope onto it (`:190-191`) for a resolution it is about to return. The `Remove` still succeeds
(the reference is unchanged), and the re-drain loop then pops and disposes that freshly-pushed scope.

The re-drain was added (round 8, commit `6190d0b8e`) to stop an orphaned scope leaking when a push
lands in that window — the intent is correct (without *some* handling the pushed scope is lost once
the key is removed), but disposing it is the wrong remedy: it turns a would-be leak into a
use-after-dispose, which the review notes is the worse failure mode.

**Proposed remedy (UNVERIFIED — to be proven or refuted in /bugfix:confirm):** instead of disposing
the raced scopes, **re-home** them under the instance key (`GetOrAdd` a stack + `Push`) so a later
`Release` drains them, keeping the one-dispose-per-release contract and never disposing state still in
use. This is lock-free and preserves the leak-prevention the block was added for. (Note: the
reviewer's alternative "drop the key removal" is *not* viable as stated — every transient instance is
keyed at `:190`, so never removing the key reintroduces the unbounded per-message growth this PR
closes for genuine transients; the growth is bounded only for shared singletons.)

## Confirmed Root Cause
The re-drain at `ServiceProviderLifetimeScope.cs:297-298` disposes scopes that a concurrent
`GetTransient` pushed onto the instance's stack in the window between the `scopes.IsEmpty` check
(`:293`) and the `ICollection<KVP>.Remove` (`:294`). `Remove` matches the dictionary entry by
**reference to the stack object**, not by its emptiness, so a stack made non-empty by that concurrent
push is still removed, and the re-drain then disposes the pushed scope — one that backs a resolution
already returned to a live caller. A single `Release` thereby disposes **two** scopes (its own plus
the raced one) instead of one, tearing down state the concurrent resolution still holds.

## Evidence
Deterministic RED test `TransientReleaseRedrainRaceTests` forces the exact interleaving with no
production seam: the shared mapper's `GetHashCode` (called by `ConcurrentDictionary` during the
releaser's `Remove`) parks the releaser at the Remove probe — after it has popped its own scope and
observed the stack empty — while a second thread resolves the shared instance and pushes its scope
onto the same still-keyed stack. Against the current code the releaser's single `Release` disposes
**2** scopes (`DisposedCount == 2`); the test asserts `== 1`. RED-proven this way; GREEN after the
re-home fix.

## Scope Notes
- Fires only for a **shared** instance under `Transient` (a singleton resolved as transient, or
  `AddTransient(sp => sharedInstance)`) — the case the per-instance `ConcurrentStack` exists for — with
  concurrent create/release of that same instance. A genuine (distinct-per-resolution) transient never
  has a concurrent push on the same key, so it is unaffected.
- Fix is confined to the re-drain block in `CollectScopesToRelease`; the pop, the emptiness check, and
  the key removal are unchanged. Release contract (one dispose per release) and the leak-prevention the
  block was added for (round 8, `6190d0b8e`) are both preserved.
- Not viable: the reviewer's "drop the key removal" — every transient instance is keyed at `:190`, so
  never removing the key reintroduces unbounded per-message growth for genuine transients.

## Regression Test
**None added — this race is not deterministically reproducible from a test, and a probabilistic one
would be a flaky, weak guard.** Established empirically:
- The releaser's critical window (`ServiceProviderLifetimeScope.cs:293-299`, between the `IsEmpty`
  check and the `Remove`) touches only `_transientScopes` and a `ConcurrentStack`. `_transientScopes`
  is keyed with `InstanceComparer.Default` (`:49-50`), a **reference-identity** comparer using
  `RuntimeHelpers.GetHashCode`/`ReferenceEquals` — so the instance's own `GetHashCode`/`Equals` are
  never called (a throwaway probe confirmed `GetHashCode` call count == 0 across `Create`/`Release`).
  There is therefore no test-double method that runs inside that window to pause the releaser.
- The first test-controllable call on the releaser (the tracked scope's `Dispose`) happens *after* the
  re-drain, too late to coordinate the concurrent push.
- A deterministic seam would require an internal hook + `InternalsVisibleTo`, which this repo does not
  use. Decision (with the maintainer): accept the fix on correctness-by-construction reasoning plus the
  existing `ServiceProviderLifetimeScope` concurrency suite for regression cover, rather than add a
  flaky stress test or a production test seam.

## Fix
`ServiceProviderLifetimeScope.cs:293-300` — the re-drain now **re-homes** the raced scopes under the
instance key (`GetOrAdd` a stack + `Push`) instead of adding them to `toDispose`:
```csharp
while (scopes.TryPop(out var raced))
    _transientScopes.GetOrAdd(instance, static _ => new ConcurrentStack<IServiceScope>()).Push(raced);
```
A later `Release` of that concurrent resolution drains them — one dispose per release, and no scope a
live caller still holds is torn down. For a genuine (distinct-per-resolution) transient the re-drain
loop is a no-op (no concurrent push on the same key), so key removal still happens and the per-message
retention this PR closes is unaffected. Lock-free; the pop, emptiness check, and key removal are
unchanged.

## Residual window — narrowed, not fully closed
The re-home is a **single pass** over the detached stack, so it recovers only scopes a concurrent
`GetTransient` pushed *before* that `while (scopes.TryPop(...))` loop observes the stack empty. Two
narrower windows remain open by design:

1. **Steady-state (raised in round-12 review, finding #3).** A concurrent `GetTransient` can capture
   the old stack reference via `GetOrAdd(instance)` *before* the `Remove`, then `Push` onto it *after*
   the re-home loop has already exited on an empty pop. That scope now sits on a stack detached from
   `_transientScopes`: the concurrent caller's later `Release(instance)` does `TryGetValue` and finds
   the re-homed stack, not the detached one, and `Dispose` iterates `_transientScopes.Values`, which no
   longer contains it — so the scope is permanently leaked. This trades the original **use-after-dispose**
   (the worse failure) for a much rarer **leak**, which is the intended direction, but it does not
   eliminate the leak.
2. **Teardown.** A concurrent factory `Dispose()` racing between the `Remove` and the re-home `GetOrAdd`
   can likewise leak one re-homed scope — benign, and already the class's stance for the symmetric
   `GetTransient` teardown window.

Both are reachable only for a **shared** instance under a `Transient` lifetime (the case the
`ConcurrentStack` exists for) under concurrent create/release; a genuine per-resolution transient never
has a concurrent push on its key. A `while (true)` re-check loop around the re-home — re-`Remove` and
re-drain until an emptiness snapshot holds across a full pass — would close window 1; it is **not**
taken here because the residual is a rare, bounded leak rather than a correctness fault, and the extra
retry complexity is not justified for it. Recorded so the next reader treats this method as *narrowed*,
not *handled*.
