# 56. TimedOutboxArchiver Sync Fallback

Date: 2026-04-15

## Status

Accepted

## Context

**Parent Requirement**: [specs/0026-timed-outbox-archiver-sync-fallback/requirements.md](../../specs/0026-timed-outbox-archiver-sync-fallback/requirements.md)

**Scope**: This ADR addresses how `TimedOutboxArchiver` selects the sync or async archiving path based on outbox capabilities.

`TimedOutboxArchiver` is an `IHostedService` that periodically archives old messages from the outbox. It currently unconditionally calls `OutboxArchiver.ArchiveAsync`, which throws an `ArgumentException` when only a sync outbox is registered (issue #3670).

The codebase already has an established pattern for this problem. `OutboxProducerMediator` exposes `HasAsyncOutbox()` and `HasOutbox()` methods that check whether its internal `_asyncOutbox` / `_outBox` fields are non-null. Callers like `CommandProcessor` use these to choose the right code path. `OutboxArchiver` performs the same pattern-matching detection in its constructor but does not expose the result.

The forces at play:

- Outbox implementations may be sync-only, async-only, or both
- `TimedOutboxArchiver` runs on a `Timer` callback, which is inherently sync but currently wraps an async call with `.GetAwaiter().GetResult()`
- The fix should follow existing codebase conventions rather than inventing new patterns

## Decision

Add `HasAsyncOutbox()` and `HasOutbox()` methods to `OutboxArchiver`, following the same naming convention used by `OutboxProducerMediator`. Then update `TimedOutboxArchiver.Archive` to check async availability and fall back to the sync path, with an explicit guard for the case where neither is available (FR3).

### Responsibilities

**OutboxArchiver** — *knowing* which outbox capabilities are available:

- Add `public bool HasAsyncOutbox()` — returns `_asyncOutbox != null`
- Add `public bool HasOutbox()` — returns `_outBox != null`

These methods expose existing knowledge without adding new state. The names match `OutboxProducerMediator.HasAsyncOutbox()` and `OutboxProducerMediator.HasOutbox()` exactly.

**TimedOutboxArchiver** — *deciding* which archive path to invoke:

- In the `Archive` method, check `_archiver.HasAsyncOutbox()` first (FR1)
- If true: call `await _archiver.ArchiveAsync(...)` (current behavior)
- Else if `_archiver.HasOutbox()`: call `_archiver.Archive(...)` via `Task.Run` (FR2, sync fallback)
- Else: log a warning that no outbox is configured and return (FR3)

### Implementation Approach

**Step 1 (structural):** Add the two query methods to `OutboxArchiver`. No behavioral change — existing callers are unaffected.

**Step 2 (behavioral):** Update `TimedOutboxArchiver.Archive` to branch:

```csharp
private async Task Archive(CancellationToken cancellationToken)
{
    try
    {
        var lockId = await _distributedLock.ObtainLockAsync(LockingResourceName, cancellationToken);
        if (lockId == null)
        {
            Log.OutboxArchiverIsStillRunningAbandoningAttempt(s_logger);
            return;
        }

        Log.OutboxArchiverLookingForMessagesToArchive(s_logger);
        try
        {
            if (_archiver.HasAsyncOutbox())
                await _archiver.ArchiveAsync(_options.MinimumAge, new RequestContext(), cancellationToken);
            else if (_archiver.HasOutbox())
                await Task.Run(() => _archiver.Archive(_options.MinimumAge, new RequestContext()), cancellationToken);
            else
                Log.NoOutboxConfigured(s_logger);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, cancellationToken);
        }
    }
    catch (Exception e)
    {
        Log.ErrorWhileSweepingTheOutbox(s_logger, e);
    }
    finally
    {
        Log.OutboxSweeperSleeping(s_logger);
    }
}
```

When async is available, it is preferred (FR1). When only sync is available, the sync path is used via `Task.Run` (FR2). When neither is available, a warning is logged and the method returns without throwing (FR3). This preserves current behavior for all existing users.

## Consequences

### Positive

- Sync-only outbox implementations work with `TimedOutboxArchiver` without error
- Follows the established `HasAsyncOutbox()` / `HasOutbox()` naming convention from `OutboxProducerMediator`
- No breaking changes — async-capable outboxes behave identically to before
- Explicit handling of all three cases (async, sync-only, neither) — no silent failures
- Minimal code change (two one-line methods + one three-way branch)
- Fixes pre-existing duplicate log call in `TimedOutboxArchiver.Archive`

### Negative

- `OutboxArchiver` gains two new public methods, slightly increasing its API surface
- The distributed lock still uses `ObtainLockAsync` / `ReleaseLockAsync` even in the sync path (acceptable since the `Timer` callback already wraps everything in `.GetAwaiter().GetResult()`)
- The sync fallback is wrapped in `Task.Run` to avoid blocking inline in the async method; this adds one thread pool schedule but keeps the async pipeline yielding correctly

### Risks and Mitigations

- **Risk**: Sync `Archive` called on a thread pool timer thread could block if the outbox operation is slow. **Mitigation**: This is the same behavior as the current `.GetAwaiter().GetResult()` wrapping of the async path — no regression. Users with sync-only outboxes accept blocking semantics.

## Alternatives Considered

1. **Add an `ArchiveAsync` overload that internally falls back to sync** — Rejected because it hides the sync/async decision inside `OutboxArchiver`, mixing concerns. The caller (`TimedOutboxArchiver`) is the coordinator and should decide.

2. **Require all outboxes to implement async** — Rejected because it would be a breaking change and the framework explicitly supports sync-only outboxes through `IAmAnOutboxSync`.

3. **Add a property instead of methods** — `OutboxProducerMediator` uses methods, so we follow the same convention for consistency.

## References

- Requirements: [specs/0026-timed-outbox-archiver-sync-fallback/requirements.md](../../specs/0026-timed-outbox-archiver-sync-fallback/requirements.md)
- GitHub Issue: [#3670](https://github.com/BrighterCommand/Brighter/issues/3670)
- Existing pattern: `OutboxProducerMediator.HasAsyncOutbox()` / `HasOutbox()` in `src/Paramore.Brighter/OutboxProducerMediator.cs`
