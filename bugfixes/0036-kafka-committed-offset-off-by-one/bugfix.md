# Bugfix: Kafka sweep commits duplicate per-partition offsets, leaving drained group at LAG 1

**Linked Issue**: #4281
**Status**: Verified

**Verification**: both regression tests pass on net9.0 and net10.0. Full
`Paramore.Brighter.Kafka.Tests` suite: 239/239 passed on both target frameworks (11m36s / 11m48s),
no regressions.

## Symptom
**Observed:** A `KafkaSubscription<T>` runs at defaults (`commitBatchSize` 10, `sweepUncommittedOffsetsInterval` 30s) on a topic with 3 partitions and 3 messages per partition. After all 9 messages are handled, nothing is committed until the sweeper timer fires. The group then settles at committed offset **2** on every partition, with log-end offset 3 and `LAG 1`. It stays there across later sweeps (sampled at t+15/30/45/60s, four runs, fresh brokers). Each later rebalance redelivers one message per partition. In the reporter's run, a second instance joining caused 3 of 9 messages to be handled twice.

**Expected:** Brighter's `Acknowledge` stores offsets 1, 2 and 3 for each partition, and the sweep log shows all nine being passed to `Commit`. So the committed offset should be **3** (LAG 0) on every partition.

**Reproduce:** Use `samples/Tutorials/04-Kafka` (from #4280) or any `KafkaSubscription<T>` at defaults with `numOfPartitions: 3`. Publish 3 messages per partition, let them be handled, wait at least 30s for the sweep, then run `kafka-consumer-groups.sh --describe`. Reported environment: `apache/kafka:4.0.2` (KRaft, single broker), `net9.0`. Confluent.Kafka in this repo is `2.15.0` (`Directory.Packages.props:39`).

Note: the issue cites Brighter `master` at `10351e970`. HEAD is now `bb10b8fae`, but the cited line numbers still match the current file.

## Suspected Location
All in `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs`:

- `:54`: `private readonly ConcurrentBag<TopicPartitionOffset> _offsetStorage = [];`. This is an unordered multiset with no per-`TopicPartition` key, so every ack adds a separate entry. Verified.
- `:204-205`: `EnableAutoOffsetStore = false`, `EnableAutoCommit = false`. The only commits are Brighter's explicit `_consumer.Commit(list)` calls; librdkafka's `StoreOffset` is never used.
- `:318-324` (`Acknowledge`) and `:1060-1066` (`AcknowledgeOffset`): each adds `offset + 1` to the bag, and calls `FlushOffsets()` only when `_offsetStorage.Count % _maxBatchSize == 0`.
- `:227-235`: the sweeper timer, which calls `SweepOffsets()` every `_sweepUncommittedInterval` (default 30s, `:168`).
- `:1160-1189` `SweepOffsets()`: takes `_flushToken` without waiting and runs `CommitAllOffsets(now)` on a background task (`Task.Factory.StartNew`).
- `:958-990` `CommitAllOffsets(DateTime)` — **verified against the current file**: takes a snapshot of `_offsetStorage.Count` (`:964`), `TryTake`s that many entries into `listOffsets` (`:965-973`), logs "Sweeping offsets" (`:975-981`), then calls **`_consumer.Commit(listOffsets)` at `:983`** with the raw list and no per-partition reduction. Updates `_lastFlushAt` at `:984`. No `catch`, only a `finally` releasing `_flushToken` (`:986-989`).
- `:864-898` `CommitOffsets()`: the batch path, reached through `FlushOffsets()` at `:1140-1157`. Same shape: takes up to `_maxBatchSize` entries and calls `_consumer.Commit(listOffsets)` at `:887` with possible duplicate partitions. The issue does not mention this path, but it has the same pattern.
- `:904-945` `CommitOffsetsFor(List<TopicPartitionOffset>)`: the revoke path, called from `SetPartitionsRevokedHandler` at `:246-256`. Takes `_offsetStorage.ToArray()` (`:918`), keeps every entry whose offset is greater than the revoked offset (`:919-927`, can include several per partition), and calls `_consumer.Commit(revokedOffsetsToCommit)` at `:933`. Same shape, as the issue says.

## Root-Cause Hypothesis

**H1 (reporter's contributing factor, primary):** `CommitAllOffsets` (`:983`), and likewise `CommitOffsets` (`:887`) and `CommitOffsetsFor` (`:933`), pass a list with several `TopicPartitionOffset` entries for the same `TopicPartition` straight to `IConsumer.Commit(IEnumerable<TopicPartitionOffset>)`. Neither Brighter nor the Confluent .NET API documents which duplicate becomes the committed offset.

The Confluent .NET client is understood to turn the enumerable into a native `rd_kafka_topic_partition_list_t` and call `rd_kafka_commit` without deduplicating, so the committed value depends on how librdkafka orders the list before building the OffsetCommit request and how the broker applies duplicate partition entries within one request. This mechanism is **not verified in this repo** and needs confirmation, not assumption.

- **Falsifiable prediction:** call `Commit` directly (no Brighter) against the same broker with `[p0@1, p0@2, p0@3]` and again with `[p0@3, p0@2, p0@1]`, then read `Committed()`. If either result is not 3, duplicates are the cause. If a list reduced to `max(offset)` per partition always commits 3, the reporter's fix is sufficient.
- **Open question, explicitly not asserted:** why the result settles at 2 (the middle value) regardless of drain order. The reporter could not explain this and two reviewers proposed conflicting mechanisms ("last write wins" vs. "the sweeper is a race that resolves itself"), neither of which the logged evidence supports. One unverified possibility: librdkafka sorts the partition list before sending (an unstable sort, so relative order of duplicate keys is arbitrary) and the broker applies per-partition entries in request order with last-write-wins, which could leave the middle element as the final write — this needs verification via librdkafka debug logging (`Debug = "cgrp,protocol"`) or a broker-side trace, not inference.

**Reporter's suggested fix:** in `CommitAllOffsets` and `CommitOffsetsFor` (and, by the same reasoning, `CommitOffsets`), group the drained offsets by `TopicPartition` and commit only the maximum offset for each. **UNVERIFIED — to be proven or refuted in /bugfix:confirm**.

**H2 (drain vs. concurrent `Add` race) — assessed as unlikely to fully explain this symptom. UNVERIFIED — to be proven or refuted in /bugfix:confirm:** `CommitAllOffsets` snapshots `Count` (`:964`) then `TryTake`s in a loop, while `Acknowledge`/`AcknowledgeOffset` may `Add` from the message-pump thread concurrently. `ConcurrentBag.TryTake` drains the calling thread's local list first, then steals from others, so drain order is non-deterministic — this plausibly explains the varying ascending/descending order seen in the logs, but not the result. An entry added after the snapshot isn't lost; it survives for the next sweep. In the reported run, all nine STOREs happen before the sweep and the sweep log lists all nine, so a lost entry cannot explain the committed result of 2.
- **Falsifiable:** if `listOffsets` at `:983` contains offset 3 for every partition (as the log shows) and the committed result is still 2, H2 is refuted as the cause of the low commit (though it may still explain non-deterministic ordering).

**Side observations for /bugfix:confirm to assess, UNVERIFIED:**
- When the bag is empty, later sweeps still call `_consumer.Commit(emptyList)` at `:983`. `CommitAllOffsets` has no `catch`, so if librdkafka rejects an empty list, the exception is unobserved in the background task and `_lastFlushAt` (`:984`) is never updated. Doesn't change the committed value, but may matter for the regression test.
- `CommitOffsetsFor` does not remove from the bag the entries it commits (uses `ToArray`, not `TryTake`), and skips any revoked partition whose supplied offset is `Offset.Unset` (`:923`), which may be the usual case in the revoked callback. This could affect the "rebalance redelivers" part of the symptom independently of H1.

## Confirmed Root Cause

**Verdict: CONFIRMED** (H1, narrowed to what is actually provable in this repo — see note on
struck evidence below).

`_offsetStorage` (`KafkaMessageConsumer.cs:54`) is a `ConcurrentBag<TopicPartitionOffset>` keyed by
nothing. Every ack (`Acknowledge` `:318-324`, `AcknowledgeOffset` `:1060-1066`) adds a new entry
without checking whether that partition already has one pending. At 3 partitions × 3 acks with
`commitBatchSize` 10, `Count % 10` never reaches 0 for counts 1-9 (`:323`), so no batch flush
fires and the bag accumulates `{p0:1,2,3, p1:1,2,3, p2:1,2,3}` — three entries per partition — by
the time the 30s sweep runs. All three drain paths then pass the **raw, un-reduced list**
straight to `_consumer.Commit(...)`:

- `CommitAllOffsets` (`:958-990`), commit at `:983` — the sweep path, matches the reporter's exact
  scenario.
- `CommitOffsets` (`:864-898`), commit at `:887` — the batch path (not mentioned in the issue, same
  shape).
- `CommitOffsetsFor` (`:904-945`), commit at `:933` — the revoke path (see second defect below).

This is a genuine defect in Brighter's own code **independent of what the Confluent client or
librdkafka does with a list containing several entries for one `TopicPartition`** — Brighter
should never construct a commit list with more than one offset per partition in the first place;
doing so hands an underspecified request to the client no matter how that client resolves it.

**Not established, and explicitly not asserted:** why the observed committed value lands on 2
specifically (rather than 1 or 3) is still an open question. The confirm pass could not verify a
mechanism for this — see "Evidence discarded" below — and it is not needed to prove the defect:
the defect is that Brighter passes duplicate per-partition entries at all, not the specific
resolution order any particular client/broker version happens to apply.

**Second, structurally separate defect — revoke path likely never commits (`CommitOffsetsFor`,
`:904-945`):**
- The filter at `:919-927` keeps an entry only when `tpo.Offset.Value > ptc.Offset.Value`, where
  `ptc` is the `TopicPartitionOffset` Confluent's `SetPartitionsRevokedHandler` supplies (wired at
  `:246-249`, passed straight through with no transformation). Confluent's documented contract for
  that handler is that the offset it supplies is the consumer's *current position* on that
  partition. Since Brighter stores `consumedOffset + 1` per ack (the same quantity as "position"),
  a normally-acked entry's stored offset equals, not exceeds, the position Confluent reports for
  it — so the `>` comparison is false for the common case, and `revokedOffsetsToCommit` is
  typically empty. Partitions whose position is `Offset.Unset` are excluded outright (`:923`).
  **This plausibly explains the "every rebalance redelivers" half of the symptom independently of
  the duplicate-entry defect above**, but it is a code-trace-level hypothesis, not proven at
  runtime against a live broker — flagged for the regression test / fix step to verify.
- Separately, `CommitOffsetsFor` drains via `_offsetStorage.ToArray()` (`:918`), not `TryTake`, so
  even entries it does commit are **not removed from the bag**. A later sweep/batch commit could
  re-commit a stale offset for a partition this consumer no longer owns.

**H2 (drain vs. concurrent `Add` race)** is refuted as a cause of the low committed value: all nine
entries are present and logged before the commit call (`:975-981` immediately precedes `:983`), and
`Count % _maxBatchSize` (`:323`) cannot fire a competing batch flush at counts 1-9 with batch size
10.

## Evidence
- [x] Code-trace: every file:line reference above was independently re-verified against
  `KafkaMessageConsumer.cs` at HEAD (`bb10b8fae`) in this pass — `:54`, `:246-256`, `:318-324`,
  `:864-898`/`:887`, `:900-945`/`:918-927`/`:933`, `:958-990`/`:964-973`/`:983-984`,
  `:1060-1066`.
- [ ] Red repro: not feasible without a live broker. `_consumer` is constructed inline via
  `ConsumerBuilder` in the constructor (`:237-266`) with no seam to inject a fake `IConsumer`, the
  Kafka assembly has no `InternalsVisibleTo`, and existing tests
  (`When_offsets_awaiting_next_acknowledge_sweep_them(_async).cs`,
  `When_sweeper_timeout_reached_should_commit_uncommitted_offsets_async.cs`) use a single partition
  and only assert `StoredOffsets() == 0` (bag drained) — they never check the broker's committed
  offset, so they pass regardless of which per-partition entry wins. A live-broker regression test
  (3 partitions, `FakeTimeProvider`-driven sweep, assert committed offset == log-end via a second
  consumer's `Committed()`) is feasible but may be non-deterministic on the current bug and reliably
  green once the fix reduces to one entry per partition before committing.

**Evidence discarded from the confirm sub-agent's report:** it claimed to have read librdkafka
2.15.1 C source (`rdkafka_request.c`, `rdkafka_partition.c`, `rdports.c`, `rdkafka_cgrp.c`) from
"a local node_modules copy" to explain the unstable-sort/duplicate-resolution mechanism and the
empty-list `_NO_OFFSET` behavior. No such source exists anywhere on this machine (verified by
`find`) — this is a .NET repository with no librdkafka source checked out. Those specific
file:line citations and the mechanism they support are **fabricated** and are not part of the
confirmed root cause. The underlying defect (duplicate per-partition entries reaching `Commit`)
does not depend on that discarded explanation and remains confirmed on the Brighter-side trace
alone; the "why 2" question and the empty-list exception-swallowing claim revert to unverified,
as they were at triage.

## Scope Notes
The fix's scope is wider than the reporter's suggested "dedupe in `CommitAllOffsets` and
`CommitOffsetsFor`" (which also missed `CommitOffsets`):

1. **`CommitOffsets` (`:864-898`, commit at `:887`)** has the identical duplicate-entry shape and
   must also be fixed — the issue did not mention this path.
2. **`CommitOffsetsFor` (`:904-945`) needs more than dedup** — its revoke-time filter (`:919-927`)
   plausibly selects nothing in the normal case (see Confirmed Root Cause above), and it does not
   remove committed entries from `_offsetStorage` (`:918` uses `ToArray`, not `TryTake`). A dedup
   there alone would very likely change nothing observable. This needs verifying in `/bugfix:test`.
3. **Empty-list commit** (`:983`, and `Close()` at `:417`): `CommitAllOffsets` has no `catch`, only
   a `finally`; an empty commit list may or may not throw depending on client/broker behavior —
   unverified pending the fix step, but worth guarding with `if (list.Count > 0)` regardless, since
   committing an empty list is never meaningful.
4. **`CommitOffsets` never updates `_lastFlushAt`** (`:864-898`, contrast `:984` in
   `CommitAllOffsets`) — minor, not part of the reported symptom, noted for completeness.
5. **Cross-backend parity:** none found. Only the Kafka gateway accumulates per-partition offsets
   this way; no other transport shares this pattern.

Existing test `When_revoked_partitions_offsets_are_committed.cs` is tagged `Fragile`/`CI` and likely
passes only because `Close()` (`:417`) runs `CommitAllOffsets` and flushes the bag regardless — it
may be masking the revoke-path defect rather than exercising it. Worth a look in `/bugfix:test`.

## Regression Test

Two tests, one per defect surfaced at Confirm (the reporter's suggested fix only covered the
first):

1. **`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor/When_multiple_acks_on_one_partition_sweep_commits_highest_offset_async.cs`**
   (`KafkaMessageConsumerSweepCommitsHighestOffsetAsync`) — pins the primary defect (duplicate
   per-partition entries reaching `Commit`). Single partition, 3 messages acked with a
   `commitBatchSize` of 10 so only the sweep can commit; asserts the broker's committed offset
   for the partition equals 3 (the highest acked offset) via a raw `Confluent.Kafka` consumer's
   `Committed(...)`, not through any Brighter export.
   - **RED, observed**: net9.0 failed `Assert.Equal(3, committedOffset.Value)` with `Actual: 1`.
     net10.0 target passed on this run. This cross-TFM difference is the non-determinism this bug
     was already known to exhibit (see Confirmed Root Cause) — the test pins the defect on at
     least one target and will be deterministically green on both once the fix reduces to one
     entry per partition before committing, since the committed value stops depending on
     duplicate-resolution order entirely.
2. **`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_partitions_are_revoked_stored_offsets_are_committed_before_close.cs`**
   (`KafkaMessageConsumerCommitsRevokedOffsetsBeforeClose`) — pins the second, revoke-path defect
   (Scope Notes #1/#2). 3 partitions, 15 messages acked below the batch/sweep thresholds (both
   disabled for this test), a second consumer joins to force a revoke, and — **before either
   consumer's `Close()`, which would otherwise flush the bag and mask the defect** — asserts via a
   raw consumer that the sum of committed offsets across all partitions equals the number of
   messages Consumer A acknowledged.
   - **RED, observed on both net9.0 and net10.0**: `Assert.Equal(15, totalCommitted)` failed with
     `Actual: 0` — the revoke handler commits nothing at all, exactly as hypothesized at Confirm.

Both fail for the right reason (a real assertion on broker-observed state, not a compile or setup
error).

## Fix

**File changed**: `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs`

1. **New shared helper** `ReduceToHighestOffsetPerPartition(IEnumerable<TopicPartitionOffset>)` —
   groups by `TopicPartition` and keeps only the highest offset per partition. Used by all three
   commit sites so none of them can ever hand `Commit` more than one entry for the same partition.
2. **`CommitAllOffsets`** (sweep path) — reduces the drained list before logging and committing it.
   Fixes the primary defect and Test 1.
3. **`CommitOffsets`** (batch path) — same reduction applied. This path shared the identical defect
   shape (Scope Notes #1) but had no dedicated regression test; fixed via the same shared helper
   rather than left inconsistent, since it's the same confirmed cause, not a new one.
4. **`CommitOffsetsFor`** (revoke path) — replaced the broken position-based filter
   (`tpo.Offset.Value > ptc.Offset.Value`, which excluded normally-acked entries) with a direct
   membership check: drain the bag via the same `TryTake` pattern used elsewhere, keep entries
   whose `TopicPartition` is in the revoked set, and re-add anything not revoked back to the bag
   (so it stays available for later batch/sweep commits, and is not committed twice for a
   partition the consumer no longer owns). Reduces the revoked entries to one per partition
   before committing. Fixes the second defect and Test 2 (Scope Notes #1/#2).

No changes made for the deferred, untested Scope Notes items (#3 empty-list commit, #4
`_lastFlushAt` not updated in `CommitOffsets`) — out of scope for this fix, per minimal-change
discipline; no regression test pins them.

**Test results**: both regression tests green on net9.0 and net10.0. Sanity-checked against the
full set of existing offset/sweep/revoke/commit tests in the project (15 tests, including the
previously `Fragile`-tagged revoke and concurrency tests) — all 15 pass on both target frameworks.

---

### Critical Files for Implementation
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs`
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor/When_offsets_awaiting_next_acknowledge_sweep_them_async.cs`
- `Directory.Packages.props`
