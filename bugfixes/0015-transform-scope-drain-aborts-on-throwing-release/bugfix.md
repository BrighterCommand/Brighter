# Bugfix: A throwing transform release aborts the drain and defers recovery to the finalizer

**Linked Issue**: PR #4254 review, Review B item #2 (comment 5102778094 deferral)
**Status**: Fixed

## Symptom
`TransformLifetimeScope.ReleaseTrackedObjects` (and the async scope's `ReleaseTrackedObjects` /
`ReleaseTrackedObjectsAsync`) drain the tracked transforms as a tail loop: remove each transform, then
release it. The release itself was **not** guarded per item, so the first `factory.Release` /
`factory.ReleaseAsync` that threw — a user transform `Dispose`/`DisposeAsync` that faults, a custom
`IAmAMessageTransformerFactory.Release` that faults, or (on `netstandard2.0`) MS DI's sync scope `Dispose`
of an `IAsyncDisposable`-only service — aborted the whole loop.

On an **explicit** `Dispose`/`DisposeAsync` this left every not-yet-drained transform tracked, with recovery
left to the GC-timed finalizer's synchronous re-drain. That contradicts this PR's bar: disposal is meant to
be **deterministic, not finalizer-driven**. A single explicit dispose should release everything it can,
now, and report the failures — not stop at the first fault and rely on a non-deterministic finalizer to
mop up the rest.

## Suspected / Confirmed Location
- `src/Paramore.Brighter/TransformLifetimeScope.cs` — `ReleaseTrackedObjects`.
- `src/Paramore.Brighter/TransformLifetimeScopeAsync.cs` — `ReleaseTrackedObjects` (sync, backs the
  finalizer) and `ReleaseTrackedObjectsAsync`.

## Fix
Each `factory.Release(trackedItem)` / `await factory.ReleaseAsync(trackedItem)` is wrapped in a per-item
`try/catch` that collects failures into a lazily-allocated local `List<Exception>`. The drain-as-you-go
invariant is unchanged (remove-before-release), so one full call drains **every** transform; after the loop,
if any release failed, the collected failures surface together as a single `throw new
AggregateException(errors)`.

Because the drain now completes in one pass, the finalizer that runs after an explicit dispose finds an
empty list and re-releases nothing — deterministic, no double-release. The finalizers already wrap the
release in `try { ... } catch { }`, so they swallow the new `AggregateException` exactly as before (a
finalizer must never throw); only an explicit `Dispose`/`DisposeAsync` surfaces it to the owner.

### Call-site audit (user's explicit ask)
`AggregateException` IS-A `Exception`, so every existing `catch (Exception)` already handles it. Verified no
narrower catch anywhere on the release path would miss it:
- `TransformPipeline.ReleaseUnmanagedResources` / `TransformPipelineAsync.ReleaseUnmanagedResources` /
  `DisposeAsync` — release the scope inside a `try`, the mapper in a `finally`; the scope's
  `AggregateException` surfaces to an explicit `Dispose`/`DisposeAsync` and is swallowed only by their
  finalizers (unchanged).
- Pump: `Reactor.TranslateMessage` / `Proactor.TranslateMessage` — release in a `finally` with
  `catch (Exception) → Log.FailedToReleasePipeline`.
- Mediator: `OutboxProducerMediator.ReleasePipeline` / `ReleasePipelineAsync` — `catch (Exception) →
  Log.FailedToReleasePipeline`.
- Builders: `TransformPipelineBuilder[Async].CleanUpAfterFailedBuild` — `catch (Exception) →
  Log.FailedToCleanUpAfterFailedBuild`.

All are `catch (Exception)`; none narrowed.

## Regression Tests (RED-proven)
`MessageSerialisation/When_a_transform_release_throws_the_scope_still_releases_the_rest.cs` (sync + async
facts, updated from the prior abort-and-retry characterization): three tracked transforms where the middle
one's release throws. A **single** explicit `Dispose`/`DisposeAsync` asserts (a) every transform is released
exactly once — the drain completed, not aborted — and (b) an `AggregateException` carrying the original
`InvalidOperationException` surfaces; a second dispose is a no-op. RED-proven by stashing the two prod files:
both facts fail (expected `AggregateException`, got raw `InvalidOperationException`).

The pre-existing `When_a_transform_lifetime_scope_finalizer_release_throws_it_should_not_escape` still passes
— the finalizer's bare `catch` swallows the `AggregateException` as it did the raw exception.

## Scope Notes
- No defaults changed. The change is confined to the three drain methods (per-item try/catch + terminal
  `AggregateException`) and the updated regression facts.
