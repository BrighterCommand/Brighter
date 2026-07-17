# Tasks — 0027 Outbox Limits

**Spec**: [requirements.md](requirements.md)
**ADR**: [docs/adr/0057-in-memory-box-abstract-expiry.md](../../docs/adr/0057-in-memory-box-abstract-expiry.md)
**Issue**: #4115

## Phase 1: Structural Changes (Tidy First)

These are structural refactoring tasks that change no behaviour. All existing tests must pass after each task.

- [x] **TIDY-1a: Extract locking into Run wrappers with protected virtual methods** (FR-3)
  - **USE COMMAND**: `/tidy-first extract locking from RemoveExpiredMessages and Compact into Run wrapper methods with protected virtual delegates`
  - Extract locking from `RemoveExpiredMessages` into a private `RunRemoveExpiredMessages` method that acquires the lock then calls `protected virtual void RemoveExpiredMessages(DateTimeOffset now)`
  - Extract locking from `Compact` into a private `RunCompact` method that acquires the lock then calls `protected virtual void Compact(int entriesToRemove)`
  - Methods remain `virtual` (not abstract yet) — the base class retains its existing algorithm as the default implementation
  - `InMemoryBox<T>` remains concrete at this step
  - **Verify**: All existing tests pass unchanged (trivially, since behaviour is identical)

- [x] **TIDY-1b: Make InMemoryBox abstract and move algorithms to subclass overrides** (FR-3)
  - **USE COMMAND**: `/tidy-first make InMemoryBox abstract and override RemoveExpiredMessages and Compact in InMemoryOutbox and InMemoryInbox`
  - Make `InMemoryBox<T>` an `abstract class`
  - Change `RemoveExpiredMessages` and `Compact` from `protected virtual` to `protected abstract`
  - Move the existing default implementations from the base class into overrides in both `InMemoryOutbox` and `InMemoryInbox` (copy the algorithms verbatim — no behaviour change)
  - **Verify**: All existing tests pass unchanged

- [x] **TIDY-1c: Add compaction cooldown and EntryLimit = -1 guard** (FR-2)
  - **USE COMMAND**: `/tidy-first add compaction cooldown timestamp and EntryLimit equals minus one guard to EnforceCapacityLimit`
  - Add `_lastCompactionAttemptAt` timestamp and cooldown guard to `EnforceCapacityLimit()` (reusing `ExpirationScanInterval`)
  - Add `EntryLimit == -1` guard to `EnforceCapacityLimit()` to skip compaction
  - **Verify**: All existing tests pass unchanged

## Phase 2: Behavioural Changes

Each task changes one behaviour, verified by a test written first.

- [x] **TEST + IMPLEMENT: Outbox expiry only removes dispatched messages** (FR-4)
  - **USE COMMAND**: `/test-first when outbox expiry runs it should only remove dispatched messages not undispatched ones`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Outbox`
  - Test file: `When_expiring_only_dispatched_messages_in_outbox.cs`
  - Test should verify:
    - Add messages to outbox, mark some as dispatched via `MarkDispatched`
    - Advance time past `EntryTimeToLive` and `ExpirationScanInterval`
    - Trigger `ClearExpiredMessages` (via any outbox operation)
    - Dispatched messages are removed
    - Undispatched messages are still present
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Change `InMemoryOutbox.RemoveExpiredMessages` to filter on `TimeFlushed != DateTimeOffset.MinValue` and `(now - TimeFlushed) >= EntryTimeToLive`

- [x] **TEST + IMPLEMENT: Outbox compaction only removes dispatched messages** (FR-4)
  - **USE COMMAND**: `/test-first when outbox compacts it should only remove dispatched messages leaving undispatched ones intact`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Outbox`
  - Test file: `When_compacting_only_dispatched_messages_in_outbox.cs`
  - Test should verify:
    - Add messages to outbox at `EntryLimit`, mark some as dispatched
    - Add one more to trigger compaction
    - Poll for compaction to complete (same pattern as existing `When_controlling_cache_size.cs`)
    - Dispatched messages are removed (oldest first by `TimeFlushed`)
    - Undispatched messages are still present
    - Outbox may exceed `EntryLimit` if all entries are undispatched
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Change `InMemoryOutbox.Compact` to filter to dispatched entries only, order by `TimeFlushed`, remove up to `entriesToRemove`
    - Update existing `When_controlling_cache_size.cs` to mark messages as dispatched before triggering compaction (since compaction now only removes dispatched messages)

- [x] **TEST + IMPLEMENT: EntryLimit of -1 disables compaction** (FR-2)
  - **USE COMMAND**: `/test-first when entry limit is minus one compaction should not run but expiry should still remove dispatched messages`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Outbox`
  - Test file: `When_entry_limit_is_minus_one_no_compaction.cs`
  - Test should verify:
    - Create outbox with `EntryLimit = -1`
    - Add many messages (more than default 2048)
    - No messages are removed by compaction
    - Mark some as dispatched, advance time past TTL and scan interval
    - Trigger expiry — dispatched messages are removed, undispatched remain
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify `EnforceCapacityLimit()` guard for `EntryLimit == -1` (done in Phase 1 tidy)
    - Ensure the outbox expiry override still runs when `EntryLimit = -1`

- [x] **TEST + IMPLEMENT: Inbox expiry removes items by WriteTime** (FR-5)
  - **USE COMMAND**: `/test-first when inbox expiry runs it should remove items older than EntryTimeToLive based on WriteTime`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Inbox`
  - Test file: `When_inbox_expiry_removes_by_write_time.cs`
  - Test should verify:
    - Add items to inbox, advance time past `EntryTimeToLive` and `ExpirationScanInterval`
    - Expired items are removed based on `WriteTime` (existing behaviour)
    - Recent items remain
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Confirm `InMemoryInbox.RemoveExpiredMessages` override uses the existing algorithm (from Phase 1 tidy)
    - This test codifies the existing behaviour to guard against regression

- [x] **TEST + IMPLEMENT: Inbox compaction removes oldest items by WriteTime** (FR-5)
  - **USE COMMAND**: `/test-first when inbox compacts it should remove oldest items by WriteTime`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Inbox`
  - Test file: `When_inbox_compaction_removes_oldest_by_write_time.cs`
  - Test should verify:
    - Add items to inbox at `EntryLimit`, add one more to trigger compaction
    - Oldest items by `WriteTime` are compacted (existing behaviour)
    - Newest items remain
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Confirm `InMemoryInbox.Compact` override uses the existing algorithm (from Phase 1 tidy)
    - This test codifies the existing behaviour to guard against regression

- [x] **TEST + IMPLEMENT: Inbox with EntryLimit = -1 disables compaction** (FR-5)
  - **USE COMMAND**: `/test-first when inbox entry limit is minus one compaction should not run`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Inbox`
  - Test file: `When_inbox_entry_limit_is_minus_one_no_compaction.cs`
  - Test should verify:
    - Create inbox with `EntryLimit = -1`
    - Add many items (more than default 2048)
    - No items are removed by compaction
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify the base class `EntryLimit == -1` guard (from TIDY-1c) applies to inbox too

- [x] **TEST + IMPLEMENT: OutboxProducerMediator logs error instead of throwing when messages missing** (FR-6)
  - **USE COMMAND**: `/test-first when clearing outbox and messages are missing it should log error and dispatch found messages instead of throwing`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Sweeper`
  - Test file: `When_clearing_outbox_with_missing_messages.cs`
  - Setup approach: Use `When_sweeping_the_outbox.cs` as a reference for constructing the `OutboxProducerMediator` and `InMemoryOutbox` directly (no DI container). This test calls `OutboxProducerMediator.ClearOutbox` directly with specific message IDs — it does NOT go through `OutboxSweeper.SweepAsync`.
  - Test should verify:
    - Add messages to the outbox directly (using the outbox reference from the mediator setup)
    - Remove one message via `outbox.Delete(new Id[] { messageId }, requestContext)` to simulate compaction loss
    - Call `ClearOutbox` with all message IDs including the missing one
    - No exception is thrown
    - The found messages are still dispatched successfully
    - An error is logged with the missing message ID
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `OutboxProducerMediator.ClearOutbox` (line ~312-315): replace `throw new NullReferenceException` with logging the missing IDs and continuing with found messages
    - In `OutboxProducerMediator.ClearOutboxAsync` (line ~376-379): same change for the async path

## Phase 3: Configuration

- [x] **TEST + IMPLEMENT: Custom DefaultBoxConfiguration is applied to the auto-created InMemoryOutbox** (FR-1)
  - **USE COMMAND**: `/test-first when configuring producers with a custom DefaultBoxConfiguration the default InMemoryOutbox should use those values`
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/`
  - Test file: `When_configuring_default_outbox_via_producers_configuration.cs`
  - Note: This test exercises the DI registration pipeline (`ServiceCollectionExtensions.AddProducers`), so it belongs in the extensions test project which already references `Paramore.Brighter.Extensions.DependencyInjection`.
  - Test should verify:
    - Build a `ServiceCollection`, call `AddProducers` with a `ProducersConfiguration` that has `DefaultBoxConfiguration = new InMemoryBoxConfiguration(EntryLimit: 8192, EntryTimeToLive: TimeSpan.FromMinutes(10))`
    - Resolve the outbox from the service provider — it should be an `InMemoryOutbox` with those values
    - When no `DefaultBoxConfiguration` is set, the resolved `InMemoryOutbox` uses current defaults (2048, 5 min, 10 min, 0)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `InMemoryBoxConfiguration` record with `EntryLimit`, `EntryTimeToLive`, `ExpirationScanInterval`, `CompactionPercentage` and sensible defaults
    - Add `DefaultBoxConfiguration` property to `ProducersConfiguration` (not `IAmProducersConfiguration`)
    - Update both `AddProducers` overloads in `ServiceCollectionExtensions` to use `CreateDefaultOutbox` helper

- [x] **IMPLEMENT: CreateDefaultOutbox helper with defensive cast** (FR-1)
  - No separate test — the defensive fallback is unreachable in normal use (both `AddProducers` overloads always register a `ProducersConfiguration` as `IAmProducersConfiguration`). The cast-with-fallback is purely defensive.
  - Implementation should:
    - In the `CreateDefaultOutbox` helper (shared by both overloads): use `(config as ProducersConfiguration)?.DefaultBoxConfiguration` to read configuration
    - If the cast returns null (defensive), fall back to built-in defaults
    - This path is covered implicitly by the "no `DefaultBoxConfiguration` set" assertion in the previous test (defaults are used)

## Task Dependencies

```
TIDY-1a (extract locking) ──→ TIDY-1b (make abstract + overrides) ──→ TIDY-1c (cooldown + -1 guard)
                                                                              │
                                                                              ├──→ Phase 2 (all behavioural tasks can run in parallel)
                                                                              │         │
                                                                              │         ├─→ Outbox expiry (dispatched-only)
                                                                              │         ├─→ Outbox compaction (dispatched-only)
                                                                              │         ├─→ EntryLimit = -1 (outbox)
                                                                              │         ├─→ Inbox expiry (write-time)
                                                                              │         ├─→ Inbox compaction (write-time)
                                                                              │         ├─→ Inbox EntryLimit = -1
                                                                              │         └─→ OutboxProducerMediator error handling
                                                                              │
                                                                              └──→ Phase 3 (Configuration — independent of Phase 2)
                                                                                        ├─→ Custom config applied (TEST)
                                                                                        └─→ CreateDefaultOutbox helper (IMPLEMENT)
```

- TIDY-1a extracts locking into Run wrappers (methods stay virtual, class stays concrete)
- TIDY-1b makes the class abstract and moves algorithms to subclass overrides
- TIDY-1c adds the cooldown and `-1` guard on top of TIDY-1b
- Phase 2 tasks are independent of each other
- Phase 3 depends only on TIDY-1c (needs the abstract structure to accept configuration). It does NOT depend on Phase 2 — configuration plumbing is independent of behavioural changes.
- `OutboxProducerMediator` task is independent of the other Phase 2 tasks
