# Bugfix: RMQ.Async producer mis-tracks publisher confirmations when several sends are in flight

**Linked Issue**: #4577
**Status**: Verified

## Symptom

**Scope.** This covers only the RMQ.Async half of #4577. The RMQ.Sync half has shipped (bugfixes 0056 and 0057). Bugfix 0055 (#4578) has also shipped: it makes the Async producer track the channel its confirm handlers are attached to. The maintainer's third comment on #4577 (issuecomment-6099959203) narrows what remains. Clearing pending confirmations on a channel switch makes no difference on a single-threaded send path. The remaining defect only appears when **several `SendAsync`/`SendWithDelayAsync` calls run at once on the same `RmqMessageProducer` instance**.

**Observed (expected from the code; not yet reproduced):** `_pendingConfirmations` (keyed only by delivery tag) gets out of step with what the broker actually confirms. This shows up in three ways:

1. **Duplicate delivery tags.** Two concurrent sends on one channel can record the same tag N. The second `AddPendingConfirmation` overwrites the first, which has these effects:
   - The first message's confirmation is lost. `OnMessagePublished`/`OnMessagePublishedAsync` never fire for it, the outbox never marks it dispatched, and the sweeper resends it (a duplicate for consumers).
   - The broker's ack for tag N (which belongs to the first message's frame) settles the **second** message's entry. The second message may then be reported confirmed before the broker has accepted its own frame (N+1). If that frame is later nacked or lost, the outbox has already marked it dispatched.
   - The ack for N+1 finds no entry.
2. **Cross-channel removal.** A send in flight on an old channel fails. Meanwhile a concurrent send has recorded the same tag on a new channel, whose tags restart at 1. The failed send's `finally` removes the new channel's entry, so that message's confirmation is lost.
3. **Attach race.** Two sends that both see a new `Channel` instance can each attach `OnPublishSucceeded`/`OnPublishFailed`. The handlers are then subscribed twice, so each ack is processed twice (the second pass is mostly a no-op, but the handler leaks). Alternatively, a send attaches to channel C(n), then publishes on, or reads its tag from, a different channel C(n+1) because `Channel` was replaced between reads.

**Expected:** Each send records exactly the delivery tag its own publish was given, on the channel it actually published on. An ack or nack settles only the entry for the message that frame belongs to. A failed send removes only its own entry. Confirm handlers are attached exactly once per channel instance.

**Reproduction (suspected, not yet run):**
1. Create one RMQ.Async `RmqMessageProducer` and subscribe to `OnMessagePublished`.
2. Start K sends at once (`Task.WhenAll` of K `SendAsync` calls) on that producer.
3. Expected result: K confirmations with K distinct message ids. Predicted result: fewer than K, or a confirmation reported for the wrong message, whenever two sends interleave between the tag read and the publish.

For variant (2): with sends in flight, close the channel under the producer (as the `ConfirmCountingRmqChannel` double from 0055 can), so one in-flight send fails while another opens the replacement channel.

## Suspected Location

Line references were checked by the triage sub-agent against the working tree. Of the prior refs, `RmqMessageGateway.cs:83-87` is now `:83-88`; the others still match.

`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs`:
- **:165-245, `SendWithDelayAsync` (the send path):**
  - **:188** `EnsureBrokerAsync`, which may replace `Channel` (see the gateway below).
  - **:190** `Channel is null` check: the first read of `Channel`.
  - **:191** `ListenForConfirmsOnChannel()`: another read of `Channel`.
  - **:202** `new RmqMessagePublisher(Channel, Connection)` captures `Channel` again. The publisher stores it in `_channel` (`RmqMessagePublisher.cs:57`, `:84`) and publishes on it (`RmqMessagePublisher.cs:108`).
  - **:203** `var deliveryTag = await Channel.GetNextPublishSequenceNumberAsync(cancellationToken);` reads `Channel` yet again, so it may be a different instance from the one captured at :202. It also reads the tag separately from the publish.
  - **:204** `AddPendingConfirmation(deliveryTag, ...)`.
  - **:205** `pendingDeliveryTag = deliveryTag;`
  - **:206** `await rmqMessagePublisher.PublishMessageAsync(...)`. The publish is a separate step from the tag read at :203.
  - **:208** `pendingDeliveryTag = null;` runs after a successful publish.
- **:225-234, `IOException` catch:** calls `ClearPendingConfirmations()` (:228). That wipes **every** entry, including ones from other in-flight sends. It then calls `ResetConnectionToBrokerAsync` (:231) and `DetachPublisherConfirmHandlers()` (:232).
- **:235-244, `finally`:** at :240-241, `RemovePendingConfirmations(pendingDeliveryTag.Value, multiple: false, beginCallbacks: false)` removes by tag alone. It does not check which channel or which message the tag belonged to. This is the removal point for variant (2), and for the same-channel variant noted under the hypothesis.
- **:419-430, `AddPendingConfirmation`:** at :428, `_pendingConfirmations[deliveryTag] = confirmation;` uses indexer assignment, so a duplicate tag silently overwrites. It is under `_stateLock` (:421), but the lock covers only the dictionary write, not the tag read and publish around it.
- **:464-488, `RemovePendingConfirmations`,** and **:507-508, `IsConfirmedBy`:** matching is by tag only. `multiple=true` sweeps every key <= tag.
- **:520-528, `ListenForConfirmsOnChannel`:**
  - **:522** `if (ReferenceEquals(Channel, _confirmChannel)) return;`
  - **:524** `DetachPublisherConfirmHandlers()`
  - **:525-526** attach to `Channel` (read again).
  - **:527** `_confirmChannel = Channel;` (read again).
  - None of this is under any lock. `_confirmChannel` is a plain field (:64), not guarded by `_stateLock` (:61).
- **:530-537, `DetachPublisherConfirmHandlers`:** also unlocked. It is called both from the send path (:524, :232) and from dispose (:255, :272).
- **:539-560, ack/nack handlers to `SettleConfirmationsAsync`:** these settle by tag through `RemovePendingConfirmations(..., beginCallbacks: true)` (:549).
- **:384, dispose-wait guard:** `Channel is { IsOpen: true } && _pendingConfirmations.Count > 0`. Stale or overwritten entries here make disposal wait out the timeout. This is a secondary symptom.

`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs`:
- **:61** `private readonly SemaphoreSlim _connectionLock = new(1, 1);`
- **:65** `protected IChannel? Channel;` is a plain field, shared mutable state read without synchronisation on the producer side.
- **:83-88** `ConnectionFactory`. It does not set `AutomaticRecoveryEnabled`, so the RabbitMQ.Client 7.2.2 default applies (`Directory.Packages.props:143`).
- **:141-178, `ConnectToBrokerAsync`:**
  - **:143** has an unlocked fast-path check, `if (Channel != null && !Channel.IsClosed) return;`.
  - **:145-177** hold `_connectionLock` only while the channel is replaced.
  - **:164-168** create the channel with `publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true`.
  - The lock is released before the producer reads `Channel` again and attaches handlers (producer :190-204), so it does not cover the producer-side check-and-attach.

`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessagePublisher.cs`:
- **:94-121, `PublishMessageAsync`:** builds the headers and properties (:102-119), then calls `_channel.BasicPublishAsync` (:108). In tracking mode, the RabbitMQ.Client assigns the real sequence number inside this call.

Callers that can produce concurrent sends on one producer instance:
- `src/Paramore.Brighter/OutboxProducerMediator.cs:1286` (`producerAsync.SendAsync`) and `:1202` (bulk).
- The background outstanding-message check, run through `Task.Run` (:853), alongside request-path clears.

Whether Brighter's own mediator actually overlaps sends on the same producer instance is listed as an open question below.

Test seams already in the repo (`tests/Paramore.Brighter.RMQ.Async.Tests/TestDoubles/`):
- `ConfirmCountingRmqChannel.cs`, a forwarding `DispatchProxy` that counts confirm subscribers and provides `CloseAsync()`.
- `ConfirmCountingRmqProducer.cs`, which records every channel the producer opens.
- `FaultingRmqChannel.cs`.
- `CleanupFailureRmqProducer.cs`, which overrides `ConnectToBrokerAsync`.

## Root-Cause Hypothesis

**Hypothesis (falsifiable):** `SendWithDelayAsync` treats three things as if they happened atomically and on one channel, but none of them are protected against concurrent sends on the same producer:
- choosing the channel (several separate reads of `Channel`: :190, :191/:522-527, :202, :203);
- choosing the delivery tag (`GetNextPublishSequenceNumberAsync` at :203);
- publishing (`BasicPublishAsync` through :206 to `RmqMessagePublisher.cs:108`).

Because pending confirmations are keyed by tag alone, with no channel or message identity (:428, :464-508), any interleaving between these steps corrupts the map.

- **H1 (duplicate tags).** RabbitMQ.Client 7.2.2 releases `_confirmSemaphore` after `GetNextPublishSequenceNumberAsync` returns (`Impl/Channel.PublisherConfirms.cs:91-107`). It then takes and increments the sequence number again inside `BasicPublishAsync` (`Impl/Channel.BasicPublish.cs:53-58`, `Channel.PublisherConfirms.cs:327-343`). These upstream line numbers come from the maintainer's comment and bugfix 0055's upstream check against the v7.2.2 tag; they were not re-checked during triage.
  - Two sends that both run :203 before either reaches the client's publish lock record the same N.
  - The second write at :428 overwrites the first.
  - The broker then acks N (the first frame) and N+1 (the second frame). The ack for N settles the second message's entry; the ack for N+1 finds nothing.
  - *Falsified if* K concurrent sends on one open channel always record K distinct tags, each equal to the sequence number its own publish was given.
- **H2 (cross-channel removal).** A send on C(n) records tag t and then fails: its publish throws because C(n) closed. Before its `finally` (:240-241) runs, a concurrent send has opened C(n+1) through :188, and its tags restart at 1. That send has recorded the same t. The `finally` removes by tag only, so it deletes C(n+1)'s entry. When C(n+1)'s ack for t arrives, it finds nothing, and that message's confirmation is lost.
  - Related path: the `IOException` catch's `ClearPendingConfirmations()` (:228) wipes other in-flight sends' entries on either channel.
  - *Falsified if* a failed send's cleanup can never remove an entry recorded by a different send.
- **H3 (attach race).** `ListenForConfirmsOnChannel` (:520-528) is a check-then-act on `_confirmChannel` and `Channel` with no lock. `_connectionLock` (`RmqMessageGateway.cs:145-177`) covers only channel creation, and the fast path at :143 takes no lock at all.
  - Two sends that both see `Channel != _confirmChannel` can both run :524-527. The second detach removes the first send's subscription from the new channel only if `_confirmChannel` was already updated; otherwise it detaches from the old channel again and attaches a second time.
  - Separately, `Channel` can change between :191 (attach), :202 (the publisher's channel) and :203 (the tag's channel). A send can then record a tag from one channel and publish on another, or publish on a channel with no handler attached.
  - *Falsified if* each channel instance always ends with exactly one `BasicAcksAsync` and one `BasicNacksAsync` subscriber under concurrent sends that race a channel replacement, and every send's tag read and publish use the same instance.

**Same-channel variant of H2, which the issue comment does not list.** Under H1, two sends share tag N. Suppose the first fails after :204 but before `BasicPublishAsync` takes a sequence number. For example, Sync's `CreateBasicProperties` throws `ConfigurationException` for a non-AMQP header value (bugfix 0056, Scope Notes item 1); the Async equivalent in `RmqMessagePublisher.cs` is not yet checked. That send's `finally` then removes the other send's entry for N. Confirm should decide whether this is the same defect.

**Why the 0055 single-threaded argument doesn't cover this:** with `publisherConfirmationTrackingEnabled: true` (`RmqMessageGateway.cs:167`), each publish waits for its own confirm, and the user `BasicAcksAsync` handlers run before the tracked publish completes (upstream `Impl/Channel.cs:677-684`, verified in 0055). So one send at a time settles its own entry before it returns. That ordering guarantee doesn't hold across interleaved sends.

**Suggested direction from the maintainer comment. UNVERIFIED — to be proven or refuted in /bugfix:confirm:**
- read the channel once per send and use that one instance for the attach, the tag and the publish;
- either serialise the tag read with the publish, or take the tag from the publish itself;
- treat all three variants as one fix.

The comment also says that clearing pending entries on a switch, or locking only the attach, would *not* fix this. That claim is also **UNVERIFIED**. Sync avoids the problem because it serialises every send under the static `s_lock`. Whether Async should copy that, which costs throughput, is a design question for Confirm.

**Open questions for Confirm:**
1. **Is the concurrent-send precondition reachable through Brighter's own callers?** Producers come from the registry and are shared. Can `OutboxProducerMediator` request-path clears (`:1286`, bulk `:1202`), the background outstanding check (`Task.Run`, :853) and the sweeper overlap on one producer instance? Or does `_backgroundClearSemaphore` (:71) or another guard serialise them? If only direct user code can trigger it, the severity changes but the defect is still there.
2. **Does RabbitMQ.Client 7.2.2 auto-recover the channel in place?** The factory leaves `AutomaticRecoveryEnabled` at the v7 default (`RmqMessageGateway.cs:83-88`). Bugfix 0055 found that `AutorecoveringChannel` keeps the outer instance and rebuilds `_innerChannel`, with `IsClosed` true during the outage. Check whether a recovered inner channel restarts `_nextPublishSeqNo` at 1 while the outer reference stays the same. If so, the `ReferenceEquals(Channel, _confirmChannel)` check at :522 cannot see it. The nupkg is at `~/.nuget/packages/rabbitmq.client/7.2.2`; it was not decompiled during triage.
3. **Tracking-mode failure semantics.** When a channel shuts down, do outstanding tracked `BasicPublishAsync` calls fault, so that each send's `finally` runs, or do they hang until cancelled? This decides how wide the H2 window is.
4. **Nack path.** With tracking on, a nack makes `BasicPublishAsync` throw `PublishException`, which also runs the `finally`. Does this interact with H1/H2? This is listed as a separate defect in 0055's Scope Notes.
5. **Test seam.** `ConfirmCountingRmqChannel` forwards to a real channel. A deterministic red test for H1 probably needs a double that forces two sends to interleave between `GetNextPublishSequenceNumberAsync` and `BasicPublishAsync`, for example by gating the tag read. A broker-backed `Task.WhenAll` stress test alone may not fail reliably.

## Confirmed Root Cause

**H1 CONFIRMED. H2 CONFIRMED (narrow, with a wider same-channel variant). H3 CONFIRMED (narrow).** All three share one cause.

In `SendWithDelayAsync` the producer reads the delivery tag in one client call and publishes in another. Each call takes and releases the client's `_confirmSemaphore` on its own. The producer then matches broker acks and nacks to messages by the bare `ulong` tag (`RmqMessageProducer.cs:428`, `:464-508`). Nothing ties a tag to the channel or to the send that recorded it.

## Evidence

Decompiled `RabbitMQ.Client.Impl.Channel` 7.2.2 (net8.0 and netstandard2.0 agree). The upstream source line numbers in the issue could not be matched to decompiled output; the behaviour was confirmed.

- [x] **Code-trace**
  - `GetNextPublishSequenceNumberAsync` does `WaitAsync; return _nextPublishSeqNo; Release`. It reads the number; it does not reserve it.
  - `BasicPublishAsync` takes `_confirmSemaphore` again. `MaybeStartPublisherConfirmationTracking` reads `_nextPublishSeqNo` itself (the pre-read value is never passed in), does `TryAdd(seq, tcs)`, then `_nextPublishSeqNo++`. It sends the frame and releases the semaphore, then awaits the confirm outside the semaphore. Tracked publishes are pipelined.
  - On failure, `MaybeHandleExceptionWithEnabledPublisherConfirmations` does `_nextPublishSeqNo--` and removes the TCS. Tags never skip; the next publish reuses the number.
  - The user ack/nack handlers run before the tracked publish completes (`HandleBasicAckAsync` awaits the wrappers, then `HandleAck` sets the TCS). Matches 0055.
  - Brighter passes no rate limiter (`RmqMessageGateway.cs:165-167`), so nothing throttles concurrent publishes.
- **H1:** T1 `:203` gets N; T2 `:203` gets N. T1 `:204` stores `[N]=A`; T2 stores `[N]=B`, overwriting A (`:428`). The frames go out as N and N+1. The ack for N removes `[N]=B` and raises a confirmation for B; the ack for N+1 finds nothing. A's `SendAsync` returns success with no confirmation, so the outbox never marks A dispatched and the sweeper resends it. The surviving entry may belong to the message whose frame is N+1, reported confirmed before its own ack.
- **H2a (cross-channel):** S1 on C(n) records t and awaits its confirm. C(n) shuts down; `MaybeHandlePublisherConfirmationTcsOnChannelShutdownAsync` faults every TCS with `AlreadyClosedException` (continuations run asynchronously). Meanwhile S2 creates C(n+1) (`RmqMessageGateway.cs:150-168`; tags restart at 1) and records t at `:204`. S1's `finally` (`:240-241`) removes S2's entry. `AlreadyClosedException` is not an `IOException`, so the catch at `:225` is skipped. Rare in production; deterministic in a test with t=1 on both channels.
- **H2b (same channel, wider than triage stated):** if A's publish fails before its frame is sent (flow-control cancellation, `ModelSendAsync` throwing, semaphore wait cancelled) the client rolls the counter back to N. B, which pre-read N, or read N after the rollback, has its entry removed by A's `finally`. `RmqMessagePublisher.PublishMessageAsync` can also throw before `BasicPublishAsync`: `CreateBasicProperties` (`RmqMessagePublisher.cs:112-119`) throws `ConfigurationException` (`:271-274`) for a header value `IsAnAmqpType` (`:291-298`) rejects, e.g. a `Guid` or `DateTime` in `Header.Bag`. Same defect, same fix.
- **H2c (`IOException` catch, `:225-233`):** `ClearPendingConfirmations()` (`:228`) wipes every in-flight send's entries; `ResetConnectionToBrokerAsync` (`:231`) tears down the shared connection; `DetachPublisherConfirmHandlers()` (`:232`) can detach a concurrent send's freshly attached channel and null `_confirmChannel`.
- **H3a (double attach):** `:520-528` has no lock and `_confirmChannel` (`:64`) is a plain field. T1 detaches C1; T2's detach returns early (`:532`); both attach to C2. Each ack then runs the handler twice (the second is a no-op), and a later detach removes only one subscription.
- **H3b (channel swap mid-send):** `Channel` is read at `:190`, `:191`/`:522-527`, `:202` and `:203` with no `await` between. A parallel `ConnectToBrokerAsync` (`:164`) can replace it, so a send attaches on one channel and publishes or reads its tag on another. The window is a few instructions wide but reachable.
- [ ] **Red repro:** not run (Confirm is read-only). Planned assertion: two `SendAsync` calls parked so both read the tag before either publishes; assert the confirmed message ids equal {A, B}. Fails today because both record N, so only one id is confirmed (count 1, not 2).

## Suggested-Fix Assessment

**PARTIAL.**

- The diagnosis is right. "Clearing on switch won't fix it" holds because H1 happens on a single channel. "Locking only the attach won't fix it" holds because that fixes only H3a. "Read the channel once per send" is needed for H3b but not enough alone.
- **"Serialise the tag read with the publish":** feasible but expensive. With tracking on, `BasicPublishAsync` does not return until the broker confirms, so a per-producer lock is held for a full broker round-trip: one in-flight message per producer, worse than Sync. Sync's lock is static, but its publish does not wait for the confirm per call. A Sync-style pipelined lock needs `publisherConfirmationTrackingEnabled: false` (`RmqMessageGateway.cs:167`), which loses the client's nack to `PublishException` behaviour and TCS faulting on shutdown.
- **"Take the tag from the publish":** not available through any supported 7.2.2 API (`BasicPublishAsync` returns a plain `ValueTask`). The only hook is an unsupported write of `x-dotnet-pub-seq-no` into the caller's `Headers` dictionary; not advised.
- **Alternative proposed by the Confirm sub-agent (UNVERIFIED design, for the user to weigh):** stop keying by tag. With tracking on, `await BasicPublishAsync` succeeds exactly when this message's frame was acked and throws `PublishException` on a nack and `AlreadyClosedException` on shutdown. The producer could raise the confirmation result after the await in `SendWithDelayAsync`, and delete the tag map, `_confirmChannel`, attach/detach and the tag-based `finally`. That removes H1, H2 and H3 together, handles in-place auto-recovery, and keeps the callback order callers see. Design points to settle: on a nack, raise the failure callback, rethrow, or both (today both); disposal wait semantics (`WaitForPendingPublisherConfirmations`); behaviour on cancellation after the frame is sent (lost today, stays lost).
- **If the tag map is kept**, the minimum is a per-producer lock covering channel capture, attach, tag read and publish; removal by identity (only remove the stored entry if it is this send's own `PendingConfirmation`); no clearing of other sends' entries. Keying by outer `IChannel` reference is not enough (see auto-recovery below).

## Scope Notes

- **Reachable from Brighter's own callers: yes.** `ClearOutboxAsync` (`OutboxProducerMediator.cs:447-500`, via `CommandProcessor.cs:786`, `:1439`) takes no lock; `_backgroundClearSemaphore` (`:71`, `:751`, `:794`) only serialises sweeper runs against each other; `DispatchAsync` (`:1257-1291`) sends one at a time within a call but uses the shared registry producer (`:1277`). Concurrent `PostAsync`/`ClearOutboxAsync` calls and the timed sweeper can overlap on one producer. Correction to the triage: the `Task.Run` at `:853` only counts messages, and the RMQ producer is not a bulk producer, so `:1202` does not apply.
- **Auto-recovery restarts tags in place (answers triage question 2).** `AutomaticRecoveryEnabled` defaults to true and is unset (`RmqMessageGateway.cs:83-88`). `AutorecoveringChannel.AutomaticallyRecoverAsync` swaps `_innerChannel` and `OpenAsync` to `MaybeConfirmSelectAsync` resets `_nextPublishSeqNo` to 1 on the same outer reference. If no send happens during the outage, `ReferenceEquals` at `:522` stays true and tags restart with no signal, so an old in-flight send's late `finally` can remove a new send's entry. Any fix keyed by channel reference misses this.
- **Shutdown faults tracked publishes; they do not hang** (unless the channel is already `_disposed`, when waiters hang until their token fires).
- **Nack double-path:** on a nack Brighter raises the failure callback (`:539`, `:549`), then `BasicPublishAsync` throws `PublishException`, the `finally` calls `RemovePendingConfirmations(N)` a second time, and `SendAsync` fails, so `OutboxProducerMediator` (`:1285-1291`) may retry and duplicate. The second removal is a no-op single-threaded and one more wrong-entry removal under H1/H2.
- **Cancellation after the frame is sent:** the client removes its TCS and rethrows; Brighter's `finally` removes the entry; the later ack finds nothing and the sweeper resends. Happens single-threaded too. Pre-existing, not part of this fix unless the design changes it.
- **Shared mutable `Span`:** `OutboxProducerMediator.cs:1281` sets `producer.Span` on the shared producer and `RmqMessageProducer.cs:195` reads it. Concurrent sends can attach events to the wrong span. Generic producer issue, not RMQ-specific; leave alone.
- **Stale channels left open:** `RmqMessageGateway.cs:150-168` replaces a closed `Channel` without disposing the old instance. Separate issue.
- **Parity:** Sync serialises with a static `s_lock` (`RMQ.Sync/RmqMessageProducer.cs:59`, `:166`), a process-wide bottleneck. If Async adopts a lock it should be per instance.
- **Test seams** (for `/bugfix:test`):
  - H1 on the live broker: a `GatedRmqChannel : DispatchProxy` in the style of `ConfirmCountingRmqChannel`, installed through a producer subclass overriding `ConnectToBrokerAsync`. It lets the real `GetNextPublishSequenceNumberAsync` run, then parks on a two-party barrier so both sends hold the same N before either publishes. The barrier must also release on the first `BasicPublishAsync` or a timeout, so the test stays valid after a fix that no longer reads the tag.
  - H1 without a broker: a subclass sets `Channel` to a fake `DispatchProxy` `IChannel` that models the client (`next`, `seq = next++`, invoke ack handlers). Fast and deterministic but re-implements client semantics; a complement, not a replacement.
  - H2a: close C(n) through the double, hold S1 after its publish faults, let S2 open C(n+1) and record tag 1, release S1, assert S2 is still confirmed.
  - H2b: send A with a `Guid` header (throws at `RmqMessagePublisher.cs:273`) gated after the tag read, alongside a normal B that read the same N; assert B is confirmed.
  - H3a: make the proxy's `add_BasicAcksAsync` block on a barrier so two sends both enter the attach; assert one ack subscriber on the new channel.

## Regression Test

Status: all three tests (H1, H2a, H3a) approved and observed RED. The same-channel H2b case needs no test of its own: drop-on-collision (Fix Design 1) makes H1's test cover it.

- `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_sends_collide_on_a_delivery_tag_should_not_confirm_either_message.cs`
- Test doubles: `TestDoubles/GatedTagReadRmqChannel.cs`, `TestDoubles/GatedTagReadRmqProducer.cs` (hold each tag read until a second send has read one, or 2 s pass).
- `.../Proactor/When_a_send_fails_on_a_replaced_channel_should_not_remove_the_new_channels_confirmation.cs` (H2a), with `TestDoubles/ParkedPublishRmqChannel.cs` and `TestDoubles/ParkedPublishRmqProducer.cs`. RED reason: the message sent on the new channel is never confirmed after the old channel's send fails (no confirmation recorded).
- `.../Proactor/When_sends_attach_to_a_new_channel_concurrently_should_subscribe_once.cs` (H3a), with `TestDoubles/AttachGatedRmqChannel.cs` and `TestDoubles/AttachGatedRmqProducer.cs`. RED reason: two ack subscribers on the new channel (expected 1), seen in repeated runs.
- H1 RED reason: one of the two colliding messages is reported confirmed (`Assert.Empty` fails with one `PublishConfirmationResult`).

## Fix Design (decided by the maintainer after Confirm)

Not the Confirm sub-agent's alternatives: no lock around tag read and publish (it would hold the lock across the broker round-trip), and no change to how `BasicPublishAsync` is awaited. Duplicate sends from the Outbox are an accepted cost (at-least-once), so every ambiguity resolves to "drop the entry and let the sweeper resend".

1. **Drop on collision (H1):** if `AddPendingConfirmation` finds an entry already recorded for the tag, drop it and do not record the new one.
2. **Remove by identity (H2b, H2c):** the `finally` removes an entry only if it is this send's own `PendingConfirmation`; the `IOException` catch no longer clears other sends' entries.
3. **Clear on channel switch (H2a, H3):** drop pending entries when a send sees a new channel instance, with a guard so only one send attaches handlers per channel (H3a).

Known gap, left alone: in-place auto-recovery restarts tags on the same channel reference without a signal, so a stale entry can linger until a `multiple=true` ack sweeps it.

## Fix

`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` only; the three regression tests now pass.

- `PendingConfirmation` is now a `sealed record` (a class), so an entry has an identity a send can compare.
- `RecordPendingConfirmation` replaces `AddPendingConfirmation`: a tag already pending is removed and the new entry is not recorded, so neither message is confirmed and the sweeper resends both (H1).
- `RemoveOwnPendingConfirmation` replaces the by-tag removal in the send's `finally`: it removes the entry only if it is this send's own (H2).
- `ListenForConfirmsOnChannel(IChannel)` takes the channel the send already read, holds `_stateLock` across check and attach, and clears pending confirmations when it moves to a new channel (H2a, H3). `DetachPublisherConfirmHandlers` takes the same lock.
- `SendWithDelayAsync` reads `Channel` once and uses that instance for the attach, the publisher and the tag read (H3b).

Left alone: the `IOException` catch still calls `ClearPendingConfirmations()` (no red test asks for a change; it clears other sends' entries, which costs duplicates only). In-place auto-recovery tags restarting on the same channel reference (known gap, see Fix Design).
