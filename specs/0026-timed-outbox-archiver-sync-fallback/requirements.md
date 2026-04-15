# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #3670

## Problem Statement

As a developer using Brighter with a sync-only outbox implementation, I would like `TimedOutboxArchiver` to archive messages using the sync `Archive` method when no async outbox is available, so that outbox archiving works regardless of whether the outbox implementation is sync-only, async-only, or both.

Currently, `TimedOutboxArchiver` unconditionally calls `OutboxArchiver.ArchiveAsync` (line 117 of `TimedOutboxArchiver.cs`). When only a sync outbox is registered, `_asyncOutbox` is null and `ArchiveAsync` throws an `ArgumentException` ("An async Outbox must be defined."). This means timed archiving silently fails for sync-only outbox implementations.

## Proposed Solution

`TimedOutboxArchiver` should detect whether the underlying `OutboxArchiver` has async outbox support and, if not, fall back to calling the sync `Archive` method instead. This follows the existing pattern in the codebase where sync and async paths are both supported.

## Requirements

### Functional Requirements
- When an async outbox is available, `TimedOutboxArchiver` calls `ArchiveAsync` (current behavior, unchanged)
- When only a sync outbox is available, `TimedOutboxArchiver` falls back to calling `Archive`
- When neither sync nor async outbox is available, the error is logged (existing error handling in the catch block)
- `OutboxArchiver` exposes whether it has async and/or sync outbox support so `TimedOutboxArchiver` can choose the right path

### Non-functional Requirements
- No breaking changes to existing public API
- No change to behavior for users who already have async outbox implementations

### Constraints and Assumptions
- `OutboxArchiver` already has both `Archive` (sync) and `ArchiveAsync` methods
- `OutboxArchiver` already detects sync/async support via pattern matching in its constructor
- The `_outBox` and `_asyncOutbox` fields in `OutboxArchiver` are currently private — they need to be exposed (read-only) for `TimedOutboxArchiver` to make the right call
- The distributed lock used by `TimedOutboxArchiver` has both sync and async methods available

### Out of Scope
- Changes to `OutboxArchiver.Archive` or `ArchiveAsync` internal logic
- Changes to the outbox interface hierarchy (`IAmAnOutbox`, `IAmAnOutboxSync`, `IAmAnOutboxAsync`)
- Changes to `TimedOutboxSweeper` (separate class, separate concern)

## Acceptance Criteria

- A sync-only outbox registered with `TimedOutboxArchiver` successfully archives messages without throwing
- An async outbox registered with `TimedOutboxArchiver` continues to work as before
- An outbox implementing both sync and async prefers the async path
- Unit tests cover both sync-only and async-only outbox scenarios via `TimedOutboxArchiver`

## Additional Context

The `OutboxArchiver` constructor already pattern-matches the outbox to detect capabilities:
```csharp
if (outbox is IAmAnOutboxSync<TMessage, TTransaction> syncOutbox) _outBox = syncOutbox;
if (outbox is IAmAnOutboxAsync<TMessage, TTransaction> asyncOutbox) _asyncOutbox = asyncOutbox;
```

The fix needs to surface this information and use it in `TimedOutboxArchiver`.
