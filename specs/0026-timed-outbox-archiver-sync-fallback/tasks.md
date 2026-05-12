# Tasks — TimedOutboxArchiver Sync Fallback

**Spec**: 0026-timed-outbox-archiver-sync-fallback
**ADR**: 0056-timed-outbox-archiver-sync-fallback
**Issue**: #3670

## Task List

### Step 1: Structural — Expose outbox capability queries on OutboxArchiver

- [x] **TIDY: Add `HasAsyncOutbox()` and `HasOutbox()` methods to `OutboxArchiver`**
  - File: `src/Paramore.Brighter/OutboxArchiver.cs`
  - Add `public bool HasAsyncOutbox()` returning `_asyncOutbox != null`
  - Add `public bool HasOutbox()` returning `_outBox != null`
  - Names match `OutboxProducerMediator.HasAsyncOutbox()` / `HasOutbox()` convention
  - This is a structural change only — no behavioral change, no new tests needed
  - Verify: existing tests still pass after adding methods

- [x] **TIDY: Remove duplicate `OutboxSweeperSleeping` log call in `TimedOutboxArchiver`**
  - File: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs`
  - Remove `Log.OutboxSweeperSleeping(s_logger)` at line 124 (inside `try` block)
  - Keep only the call in the `finally` block (line 132)
  - This is a structural change — fixes pre-existing double-logging on success path
  - Verify: existing tests still pass after removal

### Step 2: Behavioral — TimedOutboxArchiver sync fallback

- [x] **TEST + IMPLEMENT: TimedOutboxArchiver archives messages when only a sync outbox is registered**
  - **USE COMMAND**: `/test-first when archiving with sync-only outbox should call sync archive`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Archiving`
  - Test file: `When_archiving_with_sync_only_outbox_should_call_sync_archive.cs`
  - Test should verify:
    - `OutboxArchiver<Message, CommittableTransaction>` is constructed with a sync-only outbox (implements `IAmAnOutboxSync` but not `IAmAnOutboxAsync`)
    - `HasAsyncOutbox()` returns false, `HasOutbox()` returns true
    - `TimedOutboxArchiver` completes an archive cycle without throwing
    - The sync `Archive` path is exercised (messages are archived via sync outbox)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - File: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs`
    - In `TimedOutboxArchiver.Archive()`, replace the unconditional `await _archiver.ArchiveAsync(...)` call with a three-way branch:
      - `if (_archiver.HasAsyncOutbox())` → `await _archiver.ArchiveAsync(...)`
      - `else if (_archiver.HasOutbox())` → `await Task.Run(() => _archiver.Archive(...))`
      - `else` → `Log.NoOutboxConfigured(s_logger)`
    - Add `NoOutboxConfigured` log message to the `Log` partial class in `TimedOutboxArchiver.cs`

- [x] **TEST + IMPLEMENT: TimedOutboxArchiver archives messages when only an async outbox is registered (FR1)**
  - **USE COMMAND**: `/test-first when archiving with async-only outbox should call async archive`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Archiving`
  - Test file: `When_archiving_with_async_only_outbox_should_call_async_archive.cs`
  - Test should verify:
    - `OutboxArchiver<Message, CommittableTransaction>` is constructed with an async-only outbox (implements `IAmAnOutboxAsync` but not `IAmAnOutboxSync`)
    - `HasAsyncOutbox()` returns true, `HasOutbox()` returns false
    - `TimedOutboxArchiver` completes an archive cycle without throwing
    - The async `ArchiveAsync` path is exercised (messages are archived via async outbox)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - File: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs`
    - No additional implementation needed — the three-way branch from the previous task already handles this
    - This test validates FR1 / AC2: async-only outboxes continue to work

- [x] **TEST + IMPLEMENT: TimedOutboxArchiver prefers async path when both sync and async outbox are available (AC3)**
  - **USE COMMAND**: `/test-first when archiving with both sync and async outbox should prefer async`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Archiving`
  - Test file: `When_archiving_with_both_sync_and_async_outbox_should_prefer_async.cs`
  - Test should verify:
    - `OutboxArchiver<Message, CommittableTransaction>` is constructed with an outbox implementing both `IAmAnOutboxSync` and `IAmAnOutboxAsync`
    - `HasAsyncOutbox()` returns true, `HasOutbox()` returns true
    - `TimedOutboxArchiver` uses the async path (AC3)
    - Messages are archived via the async outbox methods, not the sync methods
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - File: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs`
    - No additional implementation needed — the three-way branch from the previous task already handles this
    - This test validates AC3: async is preferred when both are available

- [x] **TEST + IMPLEMENT: TimedOutboxArchiver logs warning when neither sync nor async outbox is available (FR3)**
  - **USE COMMAND**: `/test-first when archiving with no outbox configured should log warning`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Archiving`
  - Test file: `When_archiving_with_no_outbox_configured_should_log_warning.cs`
  - Test should verify:
    - `OutboxArchiver<Message, CommittableTransaction>` is constructed with an outbox that implements only `IAmAnOutbox` (neither `IAmAnOutboxSync` nor `IAmAnOutboxAsync`)
    - `HasAsyncOutbox()` returns false, `HasOutbox()` returns false
    - `TimedOutboxArchiver` completes without throwing (FR3)
    - A warning is logged indicating no outbox is configured
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - File: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs`
    - Already covered by the `else` branch from the first behavioral task
    - This test validates the FR3 guard path

## Dependencies

```
Task 1a (TIDY: HasAsyncOutbox/HasOutbox) ──┐
Task 1b (TIDY: duplicate log removal)  ────┤
                                           ↓
Task 2 (sync fallback - core fix) → Task 3 (async-only) → Task 4 (both prefer async) → Task 5 (no outbox warning)
```

Both tidy tasks must complete before behavioral work begins. Task 2 contains the core implementation. Tasks 3-5 validate additional paths already implemented in Task 2.
