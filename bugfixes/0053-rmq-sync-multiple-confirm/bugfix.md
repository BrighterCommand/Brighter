# Bugfix: RMQ.Sync producer ignores coalesced (multiple=true) publisher confirms

**Linked Issue**: none yet (found by CI on PR #4562; related to #4560)
**Status**: Verified

## Symptom

**Observed**

- With publisher confirms on, the RMQ.Sync `RmqMessageProducer` sometimes raises no publish
  confirmation for some of the messages it sends. Neither `OnMessagePublished` nor
  `OnMessagePublishedAsync` fires, even though the broker accepted the messages.
- CI on PR #4562 caught it (rabbitmq-sync-ci, run 37918253244). The failing test was
  `RmqConfirmationBatchingTests.When_confirming_many_messages_should_raise_confirmations_in_overlapping_batches_off_the_thread_pool`.
  - It failed with "Not every message was confirmed" after the full 30 s wait on one target
    framework.
  - It passed in about 1 s on the other framework in the same run, so the failure is intermittent.
- In production, an outbox that relies on publish confirmation would never mark those messages
  dispatched. The sweeper would send them again, so consumers would see duplicates.

**Expected**

Every message the broker acks or nacks raises exactly one confirmation. That includes each message
covered by a coalesced ack or nack (`multiple=true`, meaning "every delivery tag up to and including
`DeliveryTag`").

**Reproduction** (as in the test)

1. Create a Sync `RmqMessageProducer` with a bound queue.
2. Subscribe to `OnMessagePublishedAsync`.
3. Call `Send` 50 times back-to-back, without waiting for confirms between sends.
4. Wait for 50 confirmations.

The test passes or fails depending on whether the broker happens to coalesce acks. See
`tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_confirming_many_messages_should_raise_confirmations_in_overlapping_batches_off_the_thread_pool.cs:53-66`.

## Suspected Location

The handlers and the dictionary in the Sync producer:

- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:268-276`: `OnPublishFailed`
  looks up and removes only `_pendingConfirmations[e.DeliveryTag]`. It never reads `e.Multiple`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:278-286`:
  `OnPublishSucceeded` has the same problem for acks.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:58`: `_pendingConfirmations`
  is a `ConcurrentDictionary<ulong, PendingConfirmation>` keyed by delivery tag.
  - The two handlers above are the only places that remove entries.
  - Nothing clears it on dispose or on connection reset.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:178`: each `Send` adds an
  entry keyed by `Channel.NextPublishSeqNo`, so each message gets its own tag. A coalesced ack
  therefore has to settle a range of entries.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:168-170`: every `Send`
  subscribes `BasicAcks`/`BasicNacks` again and calls `ConfirmSelect()` again.
  - This is a handler leak.
  - It does not lose confirmations: the second and later invocations find no entry to settle.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:247-254`: `WaitForConfirms`
  is called only from `Dispose`.
  - `Send` never waits per message, so many publishes can be unconfirmed at once. That is what lets
    the broker coalesce acks.

Reference implementation:

- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:535-537` pass `e.Multiple`
  to `SettleConfirmationsAsync`.
- `RemovePendingConfirmations` (`:473-512`) removes every pending tag that `IsConfirmedBy`
  (`:516-517`) matches: `multiple ? pending <= tag : pending == tag`.
- The Async producer also clears its dictionary on channel reset (`:445`).

Elsewhere in RMQ.Sync:

- No other code in `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/` handles publisher confirms.
  The `DeliveryTag` uses in `RmqMessageConsumer.cs` and `PullConsumer.cs` are consumer-side acks.
- RMQ.Sync uses RabbitMQ.Client 6.x, which passes the broker's `multiple` flag straight to
  `BasicAcks`/`BasicNacks` subscribers.
  - The client's own `WaitForConfirms` bookkeeping does honour `multiple`.
  - That is why `Dispose` does not hang, even though Brighter's dictionary still holds the entries
    that were never settled.

## Root-Cause Hypothesis

**Hypothesis**

- RabbitMQ sometimes acks or nacks several publishes with one frame: `multiple=true` and
  `DeliveryTag = N`.
- When that happens, the Sync producer settles only the entry for tag `N` (`RmqMessageProducer.cs:270`
  and `:280`).
- Every pending entry with a tag `< N` that the frame covered stays in `_pendingConfirmations`
  forever, and no `PublishConfirmationResult` is ever raised for it.
- RabbitMQ decides whether to coalesce confirms based on timing and load, so the failure is
  intermittent.

**How to falsify it**

1. Capture `e.Multiple` and `e.DeliveryTag` in the Sync handlers during a 50-message burst.
   - A failing run should show at least one `Multiple == true` event.
   - The number of entries left in `_pendingConfirmations` should equal the number of missing
     confirmations.
2. Raise `BasicAcks` with `Multiple=true` deterministically, with no broker timing involved. The
   current code will raise one confirmation where it should raise several.
3. If failing runs show no `Multiple=true` frames, the hypothesis is refuted.

**Alternatives considered**

- *`BatchedCallbackQueue` drops callbacks.* `Enqueue` drops a callback only after `Complete()`, and
  `Complete()` runs only in `Dispose`, after the test's wait. `Drain` runs every item it takes. This
  is unlikely, but `/bugfix:confirm` should rule it out.
- *The 30 s timeout is too short.* The passing framework finished in about 1 s. A complete run would
  not need anywhere near 30 s, so lost confirmations fit the evidence better than slowness.
- *A new channel resets delivery tags.* If a new channel were created mid-burst, `NextPublishSeqNo`
  would restart at 1.
  - Old entries would then be orphaned, and `TryAdd` silently ignores duplicate keys.
  - That needs an `IOException` in `Send`, which would fail the test differently.
  - This is a real but secondary weakness: unlike the Async producer, the Sync producer never clears
    `_pendingConfirmations` on reset.
- *The handlers are subscribed once per `Send`.* This only adds no-op invocations; it doesn't lose
  confirmations.
- *This branch introduced the bug.* No: origin/master has the same `e.DeliveryTag`-only lookups. The
  branch changed only the callback queue, which may change how often the broker coalesces acks.

**Suggested direction** (**UNVERIFIED — to be proven or refuted in /bugfix:confirm**)

- When `Multiple` is true, settle every pending tag `<= DeliveryTag`.
- Mirror the Async producer's `RemovePendingConfirmations` and `IsConfirmedBy`.
- Raise the confirmations in ascending tag order.

**Scope-note candidates** (not part of the root cause)

- The comments at `RmqMessageProducer.cs:62-63` and `:298` are out of date. They say callbacks run
  "one at a time, in ack order", but they now run in batches of up to 32.
- Every `Send` subscribes the handlers and calls `ConfirmSelect()` again (`:168-170`).
- `_pendingConfirmations` is never cleared on channel reset or dispose.

## Confirmed Root Cause

**Verdict: CONFIRMED.**

- **The client passes coalesced acks through unchanged.** RMQ.Sync uses RabbitMQ.Client 6.8.1.
  - One broker `basic.ack`/`basic.nack` frame produces exactly one `BasicAcks`/`BasicNacks` event.
  - The broker's `multiple` flag arrives unchanged in `e.Multiple`; the client does not expand it
    into one event per tag.
- **Brighter settles only the exact tag.** `RmqMessageProducer.OnPublishSucceeded` and
  `OnPublishFailed` (`RmqMessageProducer.cs:268-286`) never read `e.Multiple`. They look up and
  remove only the key `e.DeliveryTag`.
- **The covered messages are lost.** For a coalesced frame with `DeliveryTag = N`, every pending
  entry with a lower tag stays in `_pendingConfirmations` forever. No `OnMessagePublished` or
  `OnMessagePublishedAsync` is raised for those messages.
- **Nothing visible goes wrong.** The client's own `WaitForConfirms` bookkeeping does honour
  `multiple`, so `Dispose` returns normally.
- **It is intermittent.** Whether the broker coalesces acks depends on timing.

## Evidence

- [x] **Code-trace.** The client was checked by disassembling the shipped 6.8.1 DLL; no source
  package is on disk.
  1. **Client version.** The RMQ.Sync csproj uses `VersionOverride="$(RabbitMQClientV6)"`, and
     `Directory.Packages.props:13` sets it to `6.8.1`.
  2. **The event args expose `Multiple`.** See
     `~/.nuget/packages/rabbitmq.client/6.8.1/lib/netstandard2.0/RabbitMQ.Client.xml:3100`
     (`BasicAckEventArgs.Multiple`) and `:3149` (`BasicNackEventArgs.Multiple`). Both types have
     public constructors and public setters.
  3. **There is no per-tag expansion.** `ModelBase.HandleBasicAck(deliveryTag, multiple)`:
     - builds one `BasicAckEventArgs` with `DeliveryTag` and `Multiple` set;
     - invokes each subscriber in `GetInvocationList()` once, with each call wrapped in a try/catch
       that routes to `OnCallbackException`;
     - then calls `HandleAckNack(deliveryTag, multiple, false)`.

     `HandleBasicNack` does the same.
  4. **The client's own bookkeeping honours `multiple`.** `ModelBase.HandleAckNack` removes every
     pending tag `< deliveryTag`, then tag N itself, and signals the countdown. This is why
     `WaitForConfirms` in `Dispose` (`RmqMessageProducer.cs:251`) never times out.
  5. **Each message gets its own tag.** `RmqMessageProducer.cs:178` calls `TryAdd` with
     `Channel.NextPublishSeqNo` before each publish. Calling `ConfirmSelect` again does not reset the
     sequence number on a live channel.
  6. **Settlement is by exact tag only.** The nack handler keys only on `e.DeliveryTag` at `:270`
     and `:273`; the ack handler does the same at `:280` and `:283`. Nothing else removes entries
     from `_pendingConfirmations`.
  7. **The test's burst is what triggers coalescing.** `Send` never waits for confirms per message.
     The test sends 50 messages back-to-back (`...overlapping_batches_off_the_thread_pool.cs:53-63`),
     so any coalesced frame leaves the `CountdownEvent` short.
  8. **The reference implementation is correct.** In RMQ.Async (`RmqMessageProducer.cs:535-537`),
     the handlers pass `e.Multiple` on, and `IsConfirmedBy` (`:516-517`) treats a multiple ack as
     covering every tag up to and including `DeliveryTag`.
- **Alternatives ruled out**
  - *`BatchedCallbackQueue` drops callbacks.* No. The queue drops only after `Complete()`, which runs
    only in `Dispose`. `Drain` runs every item it takes. In any case, the missing confirmations are
    never enqueued: they are lost upstream of the queue.
  - *The 30 s timeout is too short.* No. The other framework finished in about 1 s, and orphaned
    entries never settle however long you wait.
  - *A new channel resets delivery tags.* Not this failure. That path rethrows a
    `ChannelFailureException` out of `Send`, which would fail the test differently. It is a real
    secondary weakness (see Scope Notes).
  - *Duplicate handler subscriptions.* No. The first invocation's `TryRemove` wins, and the later
    invocations are no-ops.
  - *This branch introduced the bug.* No. The same code is on origin/master.
- [ ] **Red repro:** not run in Confirm. A deterministic one is feasible; see Regression Test.

**Suggested-fix assessment: PARTIAL.** Settling every tag up to and including `DeliveryTag` when
`Multiple` is set is right, but the fix also needs to:

1. **Claim each entry with `TryRemove` before raising it.** Today the code raises, then removes.
   - If a sync `OnMessagePublished` subscriber throws, the client swallows the exception and the
     remove is skipped.
   - The handler is subscribed once per `Send`, so the next duplicate invocation finds the entry
     still there and raises it again.
   - In a range loop, the same throw would also abort settlement of the rest of the range.
   - So raise each confirmation only after its `TryRemove` succeeds, and isolate each raise in a
     try/catch that logs. This also makes the fix safe when the handler is subscribed many times.
2. **Settle in ascending tag order, sorted explicitly.** `ConcurrentDictionary` enumeration order is
   unspecified. The Async producer doesn't sort either.
3. **Concurrency needs no lock.** `Send` adds each entry before it publishes, so any entry added
   during enumeration has a tag above anything the broker could have acked.
4. **Log each settled message,** as today.

## Scope Notes

- **`_pendingConfirmations` is never cleared on channel reset or dispose.** The user directed that
  this be raised as a separate issue at the end. But range settlement makes this weakness worse:
  - After `ResetConnectionToBroker` (`:203`), the new channel restarts `NextPublishSeqNo` at 1.
  - `TryAdd` (`:178`) then silently fails on a colliding stale key. The new message is never tracked.
  - Today, one wrong message is confirmed per collision.
  - With range settlement, a coalesced ack on the new channel would confirm **every** stale entry up
    to and including N. Messages that were never confirmed would be marked dispatched.
  - The RMQ.Async producer clears on reset (`Async/RmqMessageProducer.cs:230`, `:441`).
  - The minimal parity fix is `_pendingConfirmations.Clear()` in the Sync `catch (IOException)`
    before `ResetConnectionToBroker()`.
  - Not verified: RabbitMQ.Client auto-recovery (on by default) could also restart tags without the
    `IOException` path running.
  - **Decided at the Confirm gate (2026-10-09): clear in the `IOException` path as part of this fix**,
    with its own regression test. The separate issue still covers dispose and auto-recovery.
- **Every `Send` subscribes the handlers again and calls `ConfirmSelect()` again (`:168-170`).**
  Not related to this root cause. **Raise it as a separate GitHub issue at the end**, per the user's
  direction.
  - It causes unbounded invocation-list growth.
  - Every `Send` makes a synchronous `confirm.select` round-trip.
  - It amplifies the raise-before-remove duplicate described above.
  - The fix here only has to be safe under N subscriptions, which item 1 above covers.
- **Out-of-date comments** at `RmqMessageProducer.cs:62-63` and `:297-298`. They come from the #4560
  batching change on this branch, not from this bug. Correct them separately, as a tidy-first or docs
  commit.
- **Other transports:** none settle publisher confirms by exact tag; RMQ.Async already handles
  `Multiple`.

## Regression Test

Four tests, each written red-first and approved before its fix, in
`tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/`:

| Test | Red today | Commit |
| --- | --- | --- |
| `When_the_broker_coalesces_acks_should_confirm_every_message_the_ack_covers.cs` | 1 of 3 confirmations, for the third message only | `6b546641b` |
| `When_the_broker_coalesces_nacks_should_fail_every_message_the_nack_covers.cs` | 1 of 3 failed confirmations | `0af74dcf1` |
| `When_a_confirmation_subscriber_throws_should_still_confirm_every_message_once.cs` | the first message confirmed twice | `dda706a47` |
| `When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel.cs` | the new channel's ack confirms the two old-channel messages | `229908c4e` |

Test doubles in `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/`:

- **`CoalescingConfirmsRmqChannel`**: a `DispatchProxy` over `IModel`.
  - It forwards every call to the real channel except the `BasicAcks`/`BasicNacks` subscriptions.
    It keeps those, so the broker's real per-tag acks never reach the producer.
  - `RaiseAck`/`RaiseNack` simulate one broker frame. Each subscriber is called separately and its
    exception swallowed, as RabbitMQ.Client 6.8.1's `HandleBasicAck` does.
  - `FailNextPublish()` makes the next `BasicPublish` throw an `IOException`.
- **`CoalescingConfirmsRmqProducer`**: overrides `ConnectToBroker` to wrap each new channel in the
  proxy, and exposes it as `Confirms`.

The original CI failure,
`RmqConfirmationBatchingTests.When_confirming_many_messages_should_raise_confirmations_in_overlapping_batches_off_the_thread_pool`,
depends on broker timing. These four tests reproduce its cause every time.

**Recommended approach** (from Confirm). A deterministic, broker-backed test in
`tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/`, driven through the public `Send`
and `OnMessagePublished` API.

It needs two test doubles, one class per file, following the precedent of `FaultingRmqChannel` and
`CleanupFailureRmqProducer`:

- **A channel proxy** (a `DispatchProxy` over `IModel`).
  - It forwards every call to the real channel except `add_BasicAcks` and `add_BasicNacks`. It
    captures those handlers and does **not** forward them, so real per-tag acks cannot race in.
  - It exposes `RaiseAck(tag, multiple)` and `RaiseNack(tag, multiple)`. Each invokes the captured
    handlers once, simulating one broker frame.
- **A producer subclass** whose `ConnectToBroker` wraps `Channel` in that proxy.

The test:

- **Arrange:** subscribe the sync `OnMessagePublished`, collecting results into a list.
- **Act:** `Send` 3 messages, then call `RaiseAck(3, multiple: true)`.
- **Assert:** exactly 3 results, for the 3 sent ids in send order, all successful.
  - Today it fails with 1 result.
  - The "exactly 3" assertion also guards against duplicates from the 3 handler subscriptions.
- **Nack variant:** the same with `RaiseNack(3, multiple: true)`, expecting 3 failed results.

## Fix

Each fix was made in the GREEN step of its own regression test, so `/bugfix:fix` had nothing left to
do. All the changes are in `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs`.

- **Range settlement** (`6b546641b`, `0af74dcf1`)
  - `OnPublishSucceeded` and `OnPublishFailed` delegate to `SettleConfirmations(deliveryTag, multiple,
    success)`.
  - `ConfirmedDeliveryTags` returns every pending tag up to and including `DeliveryTag` when `multiple`
    is set, otherwise just that tag. The tags are sorted ascending and copied before the loop.
- **Claim, then raise** (`dda706a47`)
  - Each entry is removed with `TryRemove` before its confirmation is raised, so each tag is raised
    at most once.
  - An exception from a sync `OnMessagePublished` subscriber is caught and logged with
    `ConfirmationCallbackFault`, so the rest of the range still settles.
- **Clear on reset** (`229908c4e`): `_pendingConfirmations.Clear()` before `ResetConnectionToBroker()`
  in the `IOException` path, as RMQ.Async does.

## Verification (2026-10-09)

- **Full suite.** `Paramore.Brighter.RMQ.Sync.Tests` with the CI filter
  (`Fragile!=CI&Requires!=Docker-mTLS&Category!=RMQNativeDelay`): **143 passed, 1 skipped, 0 failed**
  on both net9.0 and net10.0.
- **Repetition.** The 4 regression tests and the original CI failure
  (`RmqConfirmationBatchingTests...overlapping_batches_off_the_thread_pool`) were run 10 times on both
  frameworks: **20 of 20 runs green**.
- **Not counted.** Without the CI filter, 9 mutual-TLS acceptance tests fail locally. They need a TLS
  broker on port 5671, which this machine doesn't have, and CI excludes them.

## Follow-ups (raised 2026-10-09, assigned to the maintainer, labelled `Bug` and `1 - Up Next`)

- **#4567**: RMQ.Sync `Send` subscribes `BasicAcks`/`BasicNacks` again and calls `ConfirmSelect()` again on every
  call (`RmqMessageProducer.cs:168-170`). This is not the cause of this bug, but it means:
  - the invocation list grows without bound;
  - every `Send` makes a synchronous `confirm.select` round trip.
- **#4568**: `_pendingConfirmations` is not cleared when the client's automatic recovery replaces the
  channel. Auto-recovery is on by default in 6.8.1; that tags restart on recovery has not been
  verified. Not clearing on `Dispose` turned out not to be a defect: the entries are collected with
  the producer, and those messages were correctly never confirmed.
