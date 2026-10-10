# Bugfix: RMQ.Sync producer re-subscribes confirm handlers and calls ConfirmSelect on every Send

**Linked Issue**: #4567
**Status**: Verified

## Symptom
**Observed:** each call to `RmqMessageProducer.Send` / `SendWithDelay` (RMQ.Sync, RabbitMQ.Client 6.x) does three things:
- adds `OnPublishSucceeded` to `Channel.BasicAcks` again;
- adds `OnPublishFailed` to `Channel.BasicNacks` again;
- calls `Channel.ConfirmSelect()`, a synchronous `confirm.select` round trip to the broker.

On a long-lived channel:
- After N sends, each event's invocation list holds N copies of the handler. Each `+=` copies the whole delegate list, so total copying is O(N²). The list is released only when the channel is replaced.
- Every broker ack/nack frame invokes the settle handler N times. The extra calls do no settling (since #4562 `TryRemove` lets only one call claim each tag), but on a `multiple=true` frame each extra call still scans and sorts the keys of `_pendingConfirmations` (`ConfirmedDeliveryTags`).
- Every Send pays one extra synchronous broker round trip.

Correctness is not affected: confirmations are neither lost nor repeated (the "exactly N confirmations" assertions from #4562 still hold).

**Expected:** handlers are subscribed, and `ConfirmSelect()` is called, once per channel instance. When the channel is replaced, the producer subscribes to and confirm-selects the new channel and detaches from the failed one, as RMQ.Async does.

**Reproduction (test-level):** with `CoalescingConfirmsRmqProducer`, send three messages and inspect the wrapped channel's `_acks` invocation list: 3 entries where 1 is expected. Every send also forwards a `ConfirmSelect` to the real channel. The double does not yet expose either count (see Testing notes).

## Suspected Location
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:169-172` — inside `lock (s_lock)` in the private `SendWithDelay(Message, TimeSpan?, bool)` (starts `:150`), unguarded on every send:
  ```csharp
  Channel!.BasicAcks += OnPublishSucceeded;
  Channel.BasicNacks += OnPublishFailed;
  Channel.ConfirmSelect();
  _confirmsSelected = true;
  ```
  Reached from `Send` (`:138`), public `SendWithDelay` (`:149`), and `SendAsync` / `SendWithDelayAsync` (`:219`, `:224`, via `Task.Run`).
- `RmqMessageProducer.cs:202-209` — the `IOException` catch clears `_pendingConfirmations` (`:206`) and calls `ResetConnectionToBroker()` (`:207`) but never unsubscribes from the failed channel.
- `RmqMessageProducer.cs:280` — comment documenting the current behaviour ("the handlers are subscribed once per Send"); must change with the behaviour.
- `RmqMessageProducer.cs:293-296` — `ConfirmedDeliveryTags`: with `multiple=true` scans, sorts and copies pending keys, once per duplicate handler invocation.
- `RmqMessageProducer.cs:245-270` — `Dispose(bool)`: calls `WaitForConfirms` when `_confirmsSelected` (`:251`); never unsubscribes handlers (Async does, `Async/RmqMessageProducer.cs:531-532`).
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageGateway.cs`:
  - `:62` — `protected IModel? Channel;`
  - `:134-168` — `ConnectToBroker`: early return if `Channel != null && !Channel.IsClosed` (`:136`); otherwise overwrites `Channel` with `_pooledConnection.CreateModel()` (`:162`). Old `IModel` is neither disposed nor unsubscribed.
  - `:180-186` — `ResetConnectionToBroker()` only calls `RmqMessageGatewayConnectionPool.ResetConnection`; does **not** null `Channel`.
  - `:193-212` — `Dispose(bool)` is the only place `Channel` is aborted/disposed/nulled (`:204-209`).
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageGatewayConnectionPool.cs:84-98` — `ResetConnection` → `CreateConnection` → `TryRemoveConnection` (`:157-165`) disposes the pooled `IConnection`, closing the old channel so the next `ConnectToBroker` replaces it.
- Contrast — `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs`:
  - `:184` records `channelInitialized = Channel is not null` before `EnsureBrokerAsync`;
  - `:188-192` subscribes `BasicAcksAsync` / `BasicNacksAsync` only when not initialized;
  - `:233-241` captures `failedChannel` before reset and unsubscribes from it;
  - `:294` sets `Channel = null` (also `Async/RmqMessageGateway.cs:225,253`);
  - no `ConfirmSelect` needed (v7 enables confirms at channel creation).
- Test seam (from #4562):
  - `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/CoalescingConfirmsRmqProducer.cs:36-43` — overrides `ConnectToBroker`, wraps each new `Channel` in the proxy; `Confirms` (`:36`) returns the *current* channel's proxy.
  - `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/CoalescingConfirmsRmqChannel.cs:39-108` — `DispatchProxy`; intercepts `add_BasicAcks` / `add_BasicNacks` into `_acks` / `_nacks` (`:87-92`); `FailNextPublish` injects an `IOException` on next `BasicPublish` (`:55`, `:93-95`); forwards everything else, including `ConfirmSelect` and `remove_*`.
  - Existing users in `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/`: `When_the_broker_coalesces_acks_should_confirm_every_message_the_ack_covers.cs`, `When_the_broker_coalesces_nacks_should_fail_every_message_the_nack_covers.cs`, `When_a_confirmation_subscriber_throws_should_still_confirm_every_message_once.cs`, `When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel.cs`. These need a real broker.

## Root-Cause Hypothesis
**Hypothesis:** the subscription and `ConfirmSelect()` at `RmqMessageProducer.cs:169-171` have no "already done for this channel" guard, so each Send adds another delegate to the same `IModel`'s `BasicAcks` / `BasicNacks` and issues another `confirm.select`. The duplicates are dropped only when `ConnectToBroker` (`RmqMessageGateway.cs:162`) overwrites `Channel` after the old one closes.

Falsifiable: after K sends on one channel, the ack and nack invocation lists each have length K and the underlying channel has received K `ConfirmSelect` calls; with a guard, both would be 1.

Corollaries to confirm or refute in `/bugfix:confirm`:
1. **Stale handlers on old channels.** After a reset the failed `IModel` still holds K delegates back to the producer (catch at `:202-209` never detaches). The producer drops its reference to the old channel, and `TryRemoveConnection` disposes the connection (`RmqMessageGatewayConnectionPool.cs:164`), so this is likely hygiene rather than a material leak. UNVERIFIED.
2. **The Async guard cannot be copied as-is.** Sync `Channel` only becomes `null` in `Dispose`, so a `Channel is null` guard would never subscribe or confirm-select a replacement channel: acks on the new channel would settle nothing, and `Dispose`'s `WaitForConfirms` (`:251-255`) would run on a non-confirm-mode channel because `_confirmsSelected` stays true across channels (expected to throw in client 6.x). UNVERIFIED.
3. **Per-Send `ConfirmSelect` currently masks silent channel replacement.** The channel can be replaced without an `IOException`/reset (broker closes it, `IsClosed` true, next `EnsureBroker` swaps it at `RmqMessageGateway.cs:136-162`). Today the new channel is still confirm-selected and subscribed only because every Send does both. Any fix must preserve this.

**Possible additional defect, outside #4567's stated scope (UNVERIFIED):** on silent replacement (corollary 3), `_pendingConfirmations` is *not* cleared (only in the `IOException` catch at `:206`). The new channel restarts delivery tags at 1, so `_pendingConfirmations.TryAdd(Channel.NextPublishSeqNo, ...)` at `:179` could silently return false for a tag still pending from the old channel — the new message's confirmation would be lost and the new channel's ack would confirm the *old* message. That would be a correctness bug; confirm or refute separately.

**Thread safety:** subscribing inside `lock (s_lock)` (`:161`) serializes Sends; note `s_lock` is *static* (shared across producer instances). Handlers run on the client's connection loop and share only the `ConcurrentDictionary`. No new race apparent from the duplicate subscriptions.

**Suggested direction from the issue (UNVERIFIED — to be proven or refuted in /bugfix:confirm):**
- Mirror RMQ.Async: subscribe handlers and call `ConfirmSelect()` only when the channel is new; unsubscribe from the failed channel on reset.
- Because Sync replaces rather than nulls `Channel`, the guard should compare the current `Channel` with the instance last subscribed to (e.g. a stored `IModel` reference), not test for null.

Testing notes:
- Use `CoalescingConfirmsRmqChannel`. Two cases: one subscription after several sends; after a reset, acks on the new channel still raise confirmations.
- The double cannot yet observe the bug; it would need a subscription count (`_acks?.GetInvocationList().Length`) and a `ConfirmSelect` counter, `remove_BasicAcks` / `remove_BasicNacks` handling so detaching is visible, and a way to retain the *old* proxy after reset (`Confirms` returns only the current one).

## Confirmed Root Cause
**Verdict: CONFIRMED.**

In the private `RmqMessageProducer.SendWithDelay(Message, TimeSpan?, bool)`, `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:169-172` runs unconditionally on every Send inside `lock (s_lock)`:
```csharp
Channel!.BasicAcks += OnPublishSucceeded;
Channel.BasicNacks += OnPublishFailed;
Channel.ConfirmSelect();
_confirmsSelected = true;
```
`Channel` is the same `IModel` across Sends until it closes, because `RmqMessageGateway.ConnectToBroker` returns early while `Channel != null && !Channel.IsClosed` (`RmqMessageGateway.cs:136`). So each Send appends another delegate to the same channel's ack/nack invocation lists and makes another synchronous `confirm.select` round trip. Nothing ever unsubscribes: not the IOException catch (`:202-209`), not `Dispose` (`:245-270`). Duplicates go away only when `ConnectToBroker` overwrites `Channel` (`:162`) after the old one closes.

Severity: cost is quadratic in Sends per channel. Each `+=` copies an N-entry list; each ack frame invokes N handlers on the client's connection-loop thread; on `multiple=true` each of those N calls scans/sorts/copies `_pendingConfirmations.Keys` (`:293-296`). That slows confirm processing itself, including `WaitForConfirms` in Dispose. Confirmations are still neither lost nor repeated (the `TryRemove` claim from #4562 holds).

## Evidence
- [x] Code-trace (RMQ.Sync tests need a live broker; a broker-free repro is feasible, see Scope Notes → Test seam)
  1. Entry points `Send` (`RmqMessageProducer.cs:138-141`), public `SendWithDelay` (`:149`), `SendAsync`/`SendWithDelayAsync` (`:219-227`, via `Task.Run`) all reach the private `SendWithDelay` (`:150`).
  2. `:163` `EnsureBroker` → `ConnectWithCircuitBreaker` → `ConnectWithRetry` → `ConnectToBroker` (`RmqMessageGateway.cs:117-131`); `:136` returns early while the channel is open, so `Channel` is the same instance.
  3. `:169-171` run every time against that instance; no guard. `_confirmsSelected` is read only in Dispose (`:251`).
  4. No `-=` for `BasicAcks`/`BasicNacks` anywhere in `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/`.
  5. `Channel` is nulled only in `RmqMessageGateway.Dispose` (`:204-209`). `ResetConnectionToBroker` (`:180-186`) → pool `ResetConnection` → `CreateConnection` → `TryRemoveConnection` disposes the pooled `IConnection` (`RmqMessageGatewayConnectionPool.cs:84-98, 157-165`), closing the model; the next `ConnectToBroker` replaces it (`:142`, `:162`).
  6. The code documents the behaviour itself at `:280`: "the handlers are subscribed once per Send, so only one invocation may win each tag".
  - So after K Sends on one channel: ack and nack lists each hold K delegates, and the inner channel has had K `ConfirmSelect` calls.

Corollaries from triage:
1. **Stale handlers on old channels: hygiene, not a material leak (CONFIRMED).** The old `IModel` references the producer, not vice versa; after `:162` overwrites `Channel` and the pool disposes the connection, nothing in Brighter roots the old model. The material growth is on the *live* channel. Detaching is still worth doing for Async parity and so a failed-but-not-yet-closed model cannot keep calling the producer. (Client-internal session cleanup is recollection of 6.x source, not verified locally.)
2. **A null guard would break replacement channels (CONFIRMED); sticky `_confirmsSelected` matters (CONFIRMED).** Sync `Channel` is never null between Sends, so `Channel is null` would set up only the first channel. The existing test `When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel.cs` would go red (new proxy's `_acks` null). RabbitMQ.Client 6.8.1 XML docs (`RabbitMQ.Client.xml:2339-2380`): `WaitForConfirms` "throws an exception when called on a non-Confirm channel"; Dispose (`:251-255`) has try/finally but no catch, so it would escape. This can already happen today if a Send replaces the channel then throws before `:171`. Re-issuing `ConfirmSelect` on a confirm-mode channel is idempotent in AMQP — today's behaviour is wasteful, not broken.
3. **Silent channel replacement exists (CONFIRMED).** Every Send reaches `ConnectToBroker` via `EnsureBroker` (`:163`); a closed channel is replaced with no IOException (`RmqMessageGateway.cs:136, 142, 162`). Paths: (a) broker closes the channel on a channel-level error (e.g. 404 missing exchange); (b) connection dies, pool `ShutdownHandler` removes it (`RmqMessageGatewayConnectionPool.cs:134-146`); (c) most realistic — the pool is process-wide, keyed by credentials/host/vhost (`:167-170`), so a consumer's `ResetConnectionToBroker` (`RmqMessageConsumer.cs:562`) or another producer's IOException closes this producer's channel. Per-Send subscribe/ConfirmSelect hides these today; an identity guard preserves it (a replacement is always a new instance), a null guard does not.

## Suggested-Fix Assessment
**PARTIAL.** "Subscribe and ConfirmSelect once per channel, unsubscribe on reset, guard on channel identity not null" is right. "Mirror RMQ.Async" literally is **WRONG**: RMQ.Async has exactly the corollary-2 bug. `Async/RmqMessageProducer.cs:184` sets `channelInitialized = Channel is not null` and subscribes only when false (`:188-192`); Async `Channel` is nulled only on dispose (`Async/RmqMessageProducer.cs:294` via `DisposeChannelAsync`, `Async/RmqMessageGateway.cs:225, 253`), while reset does not null it and `ConnectToBrokerAsync` overwrites a closed channel in place. So after the first channel, Async never subscribes a replacement; its IOException path even detaches the failed one (`:233-241`). No Async test covers reset confirmations.

Correct Sync shape (for `/bugfix:fix`, not applied here):
- Field `IModel? _confirmChannel`.
- After `EnsureBroker`, if `!ReferenceEquals(Channel, _confirmChannel)`: detach from old `_confirmChannel` if non-null; subscribe both handlers on `Channel`; `ConfirmSelect()`; `_confirmChannel = Channel`.
- IOException catch: detach from `_confirmChannel` (the instance actually subscribed, not `Channel`) and null it. This also covers an IOException inside `ConnectToBroker` after `:162` has assigned a new, unsubscribed model (retry policy handles `Or<Exception>()`, `ConnectionPolicyFactory.cs:65-66`); removing a never-added delegate is a no-op.
- Dispose: replace sticky `_confirmsSelected` with "`_confirmChannel` is the current `Channel`" before `WaitForConfirms` (`:251`), and detach (Async parity, `Async/RmqMessageProducer.cs:529-533`).
- Update the comment at `:280`. Keep the `TryRemove` claim — ack and nack handlers can still race.

## Scope Notes
**In scope for #4567:**
- `RmqMessageProducer.cs:169-172` — identity guard; subscribe + `ConfirmSelect` once per channel instance.
- `:202-209` — detach from the subscribed instance on IOException.
- `:245-270` — detach in Dispose; make the `WaitForConfirms` precondition per-channel instead of sticky `_confirmsSelected`.
- `:280` — fix the comment.

**Recommended as separate issues (not in this fix):**
1. **Silent-replacement delivery-tag collision (PROVEN, correctness).** `_pendingConfirmations` is cleared only in the IOException catch (`:206`; its own comment at `:205` notes tags restart at 1). On silent replacement stale entries survive (only ack/nack handlers remove entries; shutdown is not believed to raise BasicNacks — recollection of 6.x, unverified locally). New channel's `NextPublishSeqNo` restarts at 1, so `TryAdd` at `:180` silently returns false on collision: the new message's confirmation is lost (outbox resend → duplicate) and the new channel's ack confirms the *old* message, which may never have reached the broker (possible loss). `multiple=true` frames can sweep stale entries too. Natural fix point is the same identity guard (clear pending on new channel). RMQ.Async has the same gap (`Async/RmqMessageProducer.cs:230, 441-447`).
2. **RMQ.Async never subscribes replacement channels (correctness).** `Async/RmqMessageProducer.cs:184-192`. After any reset or silent replacement, all later publish confirmations are lost.
3. **Sync scheduler-path tag collision (new finding, correctness).** `:180` adds a pending entry *before* the branch at `:182-195`. When `delay > 0 && !DelaySupported && Scheduler != null`, nothing is published, so `NextPublishSeqNo` does not advance; the next real publish's `TryAdd` fails, its ack confirms the scheduled message's id, the real message is never confirmed, and the scheduled message is confirmed again when it is eventually sent. Async adds the entry only inside `PublishesOnChannel` (`Async/RmqMessageProducer.cs:202-207`). Path is exercised by consumer requeue without native delay (`When_rmq_sync_consumer_requeues_without_native_delay_should_use_producer.cs`).
4. **Sync orphan tags on non-IOException publish failures** (e.g. `AlreadyClosedException` from `BasicPublish`). Async removes them in `finally` (`Async/RmqMessageProducer.cs:244-251`); Sync has nothing equivalent.

**Other RMQ.Sync call sites checked — no action:**
- `RmqMessageConsumer.ForwardToInvalidChannel` (`RmqMessageConsumer.cs:491-519`) calls `ConfirmSelect()` per rejected message (`:508`) but detaches its `BasicReturn` handler in `finally` (`:517`); rejection path only, not a leak.
- Gateway `ConnectionBlocked`/`Unblocked` handlers are paired with removal (`RmqMessageGateway.cs:157-158, 223-224`).

**Test seam:**
- `CoalescingConfirmsRmqChannel` (`tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/CoalescingConfirmsRmqChannel.cs:82-107`) needs: `remove_BasicAcks`/`remove_BasicNacks` handling (today they fall through to the real channel, so detaching is invisible); a `ConfirmSelect` call counter (still forwarding); `AckSubscriberCount`/`NackSubscriberCount` from `GetInvocationList().Length`.
- `CoalescingConfirmsRmqProducer` (`CoalescingConfirmsRmqProducer.cs:36-43`) needs to retain every wrapped proxy so a test can check the failed channel was detached after reset.
- Red assertions available today: after 3 Sends `AckSubscriberCount == 1` (is 3) and `ConfirmSelectCount == 1` (is 3); after `FailNextPublish` + another Send, old proxy count 0 and new proxy count 1.
- **Broker-free option (feasible):** a `DispatchProxy` `IModel` with no inner channel, in a producer subclass overriding `ConnectToBroker` without calling base (set `Channel = fake` when null or `IsClosed`). The gateway constructor does no broker I/O (`RmqMessageGateway.cs:69-95`); `ReleaseConnection` returns early when `_pooledConnection` is null (`:217-218`). The fake must handle `IsClosed`/`IsOpen` (+ settable closed flag for silent replacement), `NextPublishSeqNo`, `CreateBasicProperties` (`new RabbitMQ.Client.Framing.BasicProperties()`), `BasicPublish` (no-op, advance seq), `ConfirmSelect` (count; seq 0→1), `add_`/`remove_` acks/nacks, `WaitForConfirms(TimeSpan, out bool)`, `Abort`/`Dispose`. `RmqMessagePublisher` uses only `CreateBasicProperties` and `BasicPublish` (`RmqMessagePublisher.cs:94, 134, 223`).

**Unverified:** (i) 6.x shutdown does not raise BasicNacks for outstanding confirms; (ii) a closed session is removed from the client's session map. Only the 6.8.1 DLL/XML docs are local; decompilation failed. The `WaitForConfirms`-throws point is verified from the XML docs.

## Regression Test
Both run against the local RabbitMQ broker (`[Collection("RMQ")]`), like the #4562 tests, and are red today:
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_sending_several_messages_on_one_channel_should_select_confirms_once.cs` — after three Sends on one channel, `ConfirmSelectCount`, `AckSubscriberCount` and `NackSubscriberCount` are each 1. Fails today: `Expected: 1, Actual: 3` (`ConfirmSelectCount`).
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_the_channel_resets_should_move_confirm_handlers_to_the_new_channel.cs` — after an IOException reset, the failed channel has 0 ack/nack subscribers and the new channel has exactly one `ConfirmSelect` and one subscriber of each. Fails today: `Expected: 0, Actual: 2` (old channel's ack subscribers). The new-channel asserts guard against an Async-style null guard.
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_the_shared_connection_is_dropped_should_select_confirms_on_the_new_channel.cs` — added at Verify. After another gateway drops the pooled connection (silent replacement, no IOException), the next Send succeeds and the new channel is set up once. **Green on arrival** (no named mutation): it refuted a suspicion that detaching from the old model would throw on Send; kept, with approval, as a guard against a null-style check.
- Existing guard: `When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel.cs` — acks on the new channel still raise confirmations.

Test double changes:
- `TestDoubles/CoalescingConfirmsRmqChannel.cs` — `AckSubscriberCount`, `NackSubscriberCount`, `ConfirmSelectCount` (still forwarded); intercepts `remove_BasicAcks`/`remove_BasicNacks` and still forwards them to the real channel (a no-op while live; rejected once disposed, as in production).
- `TestDoubles/CoalescingConfirmsRmqProducer.cs` — `Channels`: every wrapped channel, oldest first.

Not separately tested (approved): the Dispose change (per-channel `WaitForConfirms` precondition, detach handlers).

## Fix
`src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs` — confirm mode and the ack/nack handlers are set up once per channel *instance*:
- `_confirmsSelected` (sticky bool) replaced by `IModel? _confirmChannel`, the channel in confirm mode with the handlers on it.
- `SelectConfirmsOnChannel()` (called from `SendWithDelay` where the per-Send `+=`/`ConfirmSelect` was): returns if `Channel` is `_confirmChannel`; otherwise detaches from the old one, calls `ConfirmSelect()`, then subscribes both handlers. Compares by reference, not null, because the Sync gateway replaces a closed channel rather than nulling it.
- `DetachConfirms()`: unsubscribes from `_confirmChannel` and clears it. Called in the IOException catch (before the reset) and in `Dispose`'s `finally`, before `base.Dispose` disposes the channel, so it runs even if `WaitForConfirms` throws.
- `Dispose` calls `WaitForConfirms` only when the current `Channel` is `_confirmChannel`.
- Comment at the `TryRemove` claim updated (no longer "subscribed once per Send").

Separate issues from Scope Notes deliberately not addressed.

Verify found a regression in the first version, which detached after the confirmation callbacks: when `WaitForConfirms` threw, the detach was skipped, and a second `Dispose` then removed handlers from the disposed model (`ObjectDisposedException` from `AutorecoveringModel.remove_BasicAcks`; `When_gateway_cleanup_throws_should_release_its_connection`, `WaitForConfirms` cases). Fixed by moving the detach into the `finally`.

Initial targeted run: 6/6 pass (2 regression tests + 4 #4562 tests sharing the double), net10.0; build succeeds for all target frameworks.

## Verification
Full `Paramore.Brighter.RMQ.Sync.Tests` against the local RabbitMQ broker, net9.0 and net10.0: 200 passed, 1 skipped, 9 failed per framework. All 9 failures are the mTLS acceptance tests (`RmqMutualTls*`), failing with `Client certificate not found … Run ./tests/generate-test-certs.sh` — environmental, unrelated to this change.
