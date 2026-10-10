# Bugfix: RMQ.Async producer never subscribes confirm handlers to a replacement channel

**Linked Issue**: #4578
**Status**: Fixed

## Symptom

**Observed:** `Paramore.Brighter.MessagingGateway.RMQ.Async.RmqMessageProducer` attaches its publisher-confirm handlers (`OnPublishSucceeded` and `OnPublishFailed`) only to the first `IChannel` it gets. Sometimes the gateway replaces that channel with a new one. After that, the broker's acks and nacks for messages published on the new channel reach no handler in the producer. The effects are:
- `OnMessagePublished` and `OnMessagePublishedAsync` never fire for those messages.
- Outbox entries are never marked dispatched, so the sweeper resends them and consumers get duplicates.
- `_pendingConfirmations` keeps growing.
- `Dispose`/`DisposeAsync` waits in `WaitForPendingPublisherConfirmationsAsync` until the `WaitForConfirmsTimeOutInMilliseconds` timeout runs out, then logs `FailedToAwaitPublisherConfirms`.

**Expected:** Every channel the producer publishes on has the confirm handlers attached exactly once. Every confirmed publish raises `OnMessagePublished`, whether or not the channel was replaced.

**Reproduction (suspected, not yet run):**
1. Create an RMQ.Async `RmqMessageProducer` and subscribe to `OnMessagePublished`.
2. Send message A. A confirmation is raised, because the first channel is subscribed.
3. Make the gateway replace the channel. Any of these should work:
   - make `SendAsync` throw an `IOException`, for example with the `FaultingRmqChannel` proxy, which takes the reset path;
   - have the broker close the channel, for example by publishing to a missing exchange or redeclaring with conflicting arguments;
   - have another gateway reset or drop the shared pooled connection.
4. Send message B. `EnsureBrokerAsync` opens a new channel. No confirmation is raised for B, and B stays in `_pendingConfirmations`.

## Suspected Location

Line numbers are checked against master at `2c5ae241d`, the same commit as the issue's permalinks.

- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:185-193` is the subscribe guard. `var channelInitialized = Channel is not null;` is read before `EnsureBrokerAsync`. The handlers are attached only `if (!channelInitialized)`. This is a null check, not a check of which channel instance has the handlers.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:227-243` is the `IOException` catch.
  - `ClearPendingConfirmations()` at :230.
  - The failed channel is captured at :235.
  - `ResetConnectionToBrokerAsync` at :236.
  - The handlers are detached from `failedChannel` at :237-241, after the reset.
  - `Channel` is never set to null, so on the next send `channelInitialized` is `true` and nothing is attached to the new channel.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:529-533` is `DetachPublisherConfirmHandlers()`. It detaches from whatever `Channel` is now, not from the channel that actually has the handlers. It is called from `Dispose` (:264) and `DisposeAsync` (:281).
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:290-305` is `DisposeChannelAsync`. Setting `Channel = null` at :294 is the only place the producer clears `Channel`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:393` is the guard in `WaitForPendingPublisherConfirmationsAsync`: `Channel is { IsOpen: true } && _pendingConfirmations.Count > 0`. Here disposal waits for the full timeout on an open replacement channel whose acks no handler receives.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs:141-178` is `ConnectToBrokerAsync`.
  - :143 and :150: if `Channel` is null or `IsClosed`, it gets a pooled connection again if needed (:152-159).
  - :164-168: it assigns a new channel to `Channel` with `publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true`. So confirms are on, but no producer handler is attached.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs:197-200` is `ResetConnectionToBrokerAsync`. It only calls the pool's `ResetConnectionAsync` and does not touch `Channel`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs:217-225` and `:248-253` are gateway `DisposeAsync`/`Dispose`. These are the other places that set `Channel = null`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGatewayConnectionPool.cs:100-121` is `ResetConnectionAsync`, which calls `CreateConnectionAsync` (:162). That calls `TryRemoveConnectionAsync` (:166, defined at :218-226), which disposes the shared pooled connection with `DisposeAsync()` at :224. This closes every channel on it, including channels that belong to other gateways. The connection `ShutdownHandler` (:180-205) also removes a connection the broker closed from the pool.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs:83-88`: the `ConnectionFactory` does not set `AutomaticRecoveryEnabled`, so the RabbitMQ.Client 7.2.2 default applies (`Directory.Packages.props:143`). This matters for the auto-recovery question below.

Reference model, the RMQ.Sync fix from #4567, in `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs`:
- the `_confirmChannel` field at :63;
- `SelectConfirmsOnChannel()` at :281-290 is called on each send at :172. It does a `ReferenceEquals(Channel, _confirmChannel)` check, then detaches from the old channel and attaches to the new one;
- `DetachConfirms()` at :292-299;
- the `IOException` path at :201-209 detaches before the reset (:204);
- the dispose guard `ReferenceEquals(Channel, _confirmChannel)` at :251;
- the detach in `finally` at :271.

Test coverage:
- No test in `tests/Paramore.Brighter.RMQ.Async.Tests` checks confirmations after a reset or channel replacement. Every test that uses `OnMessagePublished` sends on one channel:
  - `MessagingGateway/Proactor/When_confirming_multiple_messages_via_the_messaging_gateway_async.cs`
  - `When_disposing_*_after_sending_should_publish_confirmation.cs`
  - `When_a_confirmation_is_received_should_carry_id_topic_and_context.cs`
  - the `Reactor/When_confirming_multiple_messages_via_the_messaging_gateway.cs` test
  - the generated `When_confirming_posting_a_message_should_receive_publish_confirmation.cs` tests
- The reset-related tests check only connection and pool lifetime, not confirms:
  - `MessagingGateway/Proactor/When_resetting_a_connection_that_exists.cs`
  - `MessagingGateway/When_disposing_a_gateway_after_reset_should_preserve_the_replacement_connection.cs`
  - `MessagingGateway/When_disposing_the_only_gateway_after_reset_should_close_the_unused_replacement.cs`
- Test doubles: Async has `TestDoubles/FaultingRmqChannel.cs`, a `DispatchProxy` over `IChannel`. It has no counterpart to Sync's `CoalescingConfirmsRmqChannel.cs` or `CoalescingConfirmsRmqProducer.cs`.
- The Sync equivalents to mirror are in `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/`:
  - `When_the_channel_resets_should_move_confirm_handlers_to_the_new_channel.cs`
  - `When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel.cs`
  - `When_the_shared_connection_is_dropped_should_select_confirms_on_the_new_channel.cs`
  - `When_sending_several_messages_on_one_channel_should_select_confirms_once.cs`

## Root-Cause Hypothesis

**Hypothesis (falsifiable):** The producer decides whether to attach confirm handlers by checking whether `Channel` was null before `EnsureBrokerAsync` (`RmqMessageProducer.cs:185, :189`). Only disposal sets `Channel` back to null (`RmqMessageProducer.cs:294`, `RmqMessageGateway.cs:225, :253`). When `RmqMessageGateway.ConnectToBrokerAsync` replaces a closed channel in place (`RmqMessageGateway.cs:150, :164`), `channelInitialized` is `true`, so the new channel gets no `BasicAcksAsync`/`BasicNacksAsync` handlers. Broker confirms for publishes on the replacement channel are then dropped, and the matching `_pendingConfirmations` entries are never settled.

Prediction: send once, force a channel replacement, then send again. The second message raises no `OnMessagePublished`, and `_pendingConfirmations` still holds its delivery tag. If Confirm sees the second send raise a confirmation on a replacement channel (a different `IChannel` instance from the first), the hypothesis is refuted.

**Suggested fix from the issue: UNVERIFIED, to be proven or refuted in /bugfix:confirm.** Mirror #4567 (RMQ.Sync):
- Track the channel instance that has the handlers (an `_confirmChannel` equivalent).
- On each send after `EnsureBrokerAsync`, compare `Channel` to it by reference, not with a null check.
- If they differ, detach from the old instance and attach to the new one.
- Make disposal detach from the tracked instance, not from whatever `Channel` is.

v7 already turns on confirms at channel creation (`RmqMessageGateway.cs:165-167`), so no `ConfirmSelect` equivalent is needed.

**Open questions for Confirm to settle:**
1. **Which paths really replace the channel, and which recover in place?** The factory leaves `AutomaticRecoveryEnabled` at the v7 default (`RmqMessageGateway.cs:83-88`). After a network failure, an `AutorecoveringChannel` may recover in place: `IsClosed` goes back to `false`, `ConnectToBrokerAsync` returns early at :143, and the handlers survive on the same instance. Confirm should list the paths that set `IsClosed == true` for good and so reach :164. Candidates:
   - (a) the `IOException` catch, then `ResetConnectionToBrokerAsync`, then the pool disposes the connection (`RmqMessageGatewayConnectionPool.cs:224`). This is a deliberate close, which does not auto-recover;
   - (b) a broker-initiated channel close (channel-level exception), which v7 does not auto-recover;
   - (c) another gateway's reset, or the pool `ShutdownHandler`, removing and disposing the shared connection;
   - (d) the connection dying when recovery is disabled or exhausted.

   Also: is there any window where a send sees `IsClosed == true` during auto-recovery, before recovery completes, and so creates a second channel on the recovering connection?
2. **Is it safe to detach on a disposed v7 channel?** The `IOException` catch detaches from `failedChannel` after `ResetConnectionToBrokerAsync` (`RmqMessageProducer.cs:236-241`). By then the pool has disposed the owning connection (`RmqMessageGatewayConnectionPool.cs:224`), and possibly the channel with it. The Sync fix hit `ObjectDisposedException` (`AutorecoveringModel.remove_BasicAcks`) in the equivalent spot (`bugfixes/0054-rmq-sync-confirm-resubscribe/bugfix.md:162`) and moved the detach before the reset, or into `finally`. Confirm should check whether v7 `AutorecoveringChannel` event `remove` throws after dispose. If it does, that `ObjectDisposedException` would hide the `ChannelFailureException` at :242. Also check `DetachPublisherConfirmHandlers` (:529-533) on a `Channel` that has been closed or replaced.
3. **How much does this overlap with #4577 (stale pending confirmations after a silent replacement)?** The Async producer clears `_pendingConfirmations` only on `IOException` (`RmqMessageProducer.cs:230`). A new channel numbers delivery tags from 1 again. So once handlers are attached to a silently replaced channel, its acks could settle stale entries with the same tag from the old channel. A `multiple=true` ack could make this worse. Does fixing #4578 alone expose #4577? Should they be fixed together, the way Sync clears pending entries when it detaches (Sync `RmqMessageProducer.cs:204-206`)?
4. **Disposal wait:** `WaitForPendingPublisherConfirmationsAsync` (`RmqMessageProducer.cs:393`) waits whenever `Channel` is open and entries are pending. Confirm should check whether it should also require `ReferenceEquals(Channel, trackedConfirmChannel)`, like Sync's dispose guard at :251, to avoid waiting for the full timeout.
5. **Concurrency:** Async sends are not serialized the way Sync sends are under `lock (s_lock)` (Sync :163). Concurrent `SendWithDelayAsync` calls could race on the reference compare and the attach, causing a double attach or a missed attach. Does the tracked-channel check need `_stateLock` or the gateway's `_connectionLock`?
6. **Test seam:** Async has no confirm-capable channel double. Can `FaultingRmqChannel` (a `DispatchProxy` over `IChannel`) be extended to raise `BasicAcksAsync`? Or does the regression test need a real broker, as some Sync reset tests use?

## Confirmed Root Cause

**Verdict: CONFIRMED.**

**Evidence limits.** The RabbitMQ.Client v7.2.2 internals below come from two sources:
- the shipped XML docs (`~/.nuget/packages/rabbitmq.client/7.2.2/lib/net8.0/RabbitMQ.Client.xml`);
- the confirming agent's recollection of the v7 source.

The upstream source could not be fetched in this session. Every claim that rests on recollection is
marked **[upstream, unverified]**. Check those against the `v7.2.2` tag before the Fix depends on
them. The Brighter-side trace does not depend on them.

In `RmqMessageProducer.cs` (RMQ.Async):
- `channelInitialized` is a null check on `Channel`, read at :185, before `EnsureBrokerAsync` (:186).
- The handlers are attached only `if (!channelInitialized)` (:189-193).

`Channel` goes back to null only on disposal: producer :294, and gateway `RmqMessageGateway.cs:225`
and `:253`. `ConnectToBrokerAsync` replaces a closed channel in place (`RmqMessageGateway.cs:150`,
`:164-168`). So for every replacement channel, `channelInitialized == true` and no handler is
attached. The only `BasicAcksAsync +=` in the Async gateway is :191.

The channel is created with `publisherConfirmationTrackingEnabled: true` (:167). In that mode,
`BasicPublishAsync` awaits the broker confirm itself, and throws `PublishException` on a nack
(XML:2673, :2688). So on a replacement channel:
1. The publish returns normally on an ack.
2. `pendingDeliveryTag = null` (:210) hands the entry to a handler that does not exist.
3. The entry added at :206 is never removed, and `OnMessagePublished`/`OnMessagePublishedAsync`
   never fire.
4. The outbox is never marked dispatched by confirmation, so the sweeper resends and consumers see
   duplicates.
5. Disposal waits for the whole `WaitForConfirmsTimeOutInMilliseconds`, because of the guard
   `Channel is { IsOpen: true } && _pendingConfirmations.Count > 0` (:393).

## Evidence

- [x] Code-trace:
  1. **Send #1.** `Channel` is null, so `channelInitialized=false` (:185). `ConnectToBrokerAsync`
     creates C1 (`RmqMessageGateway.cs:164-168`), and the handlers attach to C1 (:191-192). The tag
     is recorded from `C1.GetNextPublishSequenceNumberAsync` (:205-206). The ack reaches
     `OnPublishSucceeded`, then `SettleConfirmationsAsync` (:537, :539-556), which removes the entry
     and raises the callbacks.
  2. **C1 is replaced, here through the `IOException` path.**
     - The catch is at :227, and `ClearPendingConfirmations` runs at :230.
     - `ResetConnectionToBrokerAsync` (:236) calls the pool's `ResetConnectionAsync`
       (`RmqMessageGatewayConnectionPool.cs:100-121`). That calls `CreateConnectionAsync` (:110),
       then `TryRemoveConnectionAsync` (:166), which disposes the shared connection (:224) and so
       closes C1.
     - The handlers are removed from `failedChannel` (:237-241). `Channel` still points at C1 and
       is not null.
  3. **Send #2.** `channelInitialized` is `true` (:185).
     - `Channel.IsClosed` (:143/:150) and `!_pooledConnection.IsOpen` (:152) lead to a re-acquire
       (:154-158) and a new channel, C2 (:164).
     - The guard at :189 skips the attach.
     - The tag is recorded against C2 (:205-206). `BasicPublishAsync` returns on the broker ack,
       because tracking is on, so `pendingDeliveryTag=null` (:210).
     - C2 has no subscriber, so the entry stays forever.
  4. **Dispose.** C2 is open and Count>0 (:393), so the producer waits the whole timeout and logs
     `FailedToAwaitPublisherConfirms` (:421-422). `DetachPublisherConfirmHandlers` (:529-533) then
     removes handlers from C2 that were never attached. That is harmless.
- [ ] Red repro: a cheap one exists but has not been written. `/bugfix:test` writes it.
  - **Seam.** `ConnectToBrokerAsync` is `protected virtual` (`RmqMessageGateway.cs:141`), and
    `TestDoubles/CleanupFailureRmqProducer.cs` already overrides it to swap `Channel`.
  - **Producer double.** Override `ConnectToBrokerAsync` without calling base. Each time the
    current channel reports `IsClosed`, set `Channel` to a fresh, non-forwarding `DispatchProxy`
    over `IChannel`.
  - **Channel double.** The proxy:
    - records `add_`/`remove_` calls for `BasicAcksAsync`/`BasicNacksAsync`;
    - returns tags starting at 1 from `GetNextPublishSequenceNumberAsync`;
    - completes `BasicPublishAsync<T>` by invoking the stored ack handlers, modelling tracking mode;
    - exposes `IsClosed`/`IsOpen` flags that the test can flip.
  - **Test.** Subscribe to `OnMessagePublished`. Send A, then set C1 `IsClosed`, then send B.
    - It expects 2 confirmations, the second for B. Today it gets 1, because of the guard at :189.
    - Optional: assert that C2 has one ack subscriber and C1 has none.
    - Optional: dispose with a short timeout and check it returns promptly. Today it waits out the
      timeout, because of the guard at :393.
  - **Doubles must also:**
    - skip `DeclareExchangeForConnection`;
    - return completed tasks for `AbortAsync`/`DisposeAsync`;
    - leave `ReleaseConnectionAsync` safe to call with a null `_pooledConnection`.

### Answers to the triage's open questions

1. **Which paths really replace the channel?** `AutomaticRecoveryEnabled` is not set
   (`RmqMessageGateway.cs:83-88`), and the v7 default is `true` (XML:640-644).
   - **Not affected:** network-driven auto-recovery. It keeps the outer `AutorecoveringChannel` and
     rebuilds its inner channel, and the subscriptions survive **[upstream, unverified]**.
   - **Real replacements, which reach :164:**
     - (a) **The IOException reset.** The pool disposes the connection (`Pool.cs:224`). An
       application-initiated close is not recovered.
     - (b) **A broker-initiated channel close**, such as a 404 or PRECONDITION_FAILED. A channel is
       not recovered outside connection recovery **[upstream, unverified]**.
     - (c) **Another gateway's reset or release disposes the shared connection**
       (`Pool.cs:151-153`, `:166`), or the pool's `ShutdownHandler` does (:180-205).
     - (d) **Recovery is disabled or exhausted.**
   - **Caveat on (a).** A v7 publish on a closed channel usually throws `AlreadyClosedException` or
     `OperationInterruptedException`, not `IOException` **[upstream, unverified]**. So the catch
     at :227 may rarely run. The replacement then happens lazily, on the next send at :150.
   - **Recovery window.** During recovery, the outer channel may report `IsClosed` **[upstream,
     unverified]**. A send in that window could create C2 next to the C1 that is recovering, and
     leak C1. That is a pre-existing leak. With reference tracking, the fix still moves the
     handlers to C2.
2. **Is it safe to detach from a disposed channel?** Not guaranteed.
   - The v7 `AutorecoveringChannel` event accessors forward to `InnerChannel`, which calls
     `ThrowIfDisposed()` **[upstream, unverified]**. This is the same design as v6, which threw in
     #4567.
   - The detach at :237-241 runs after the dispose at :236. If it throws, the
     `ObjectDisposedException` replaces the `ChannelFailureException` at :242.
   - Fix: detach before the reset, as Sync does at :204-206, and tolerate `ObjectDisposedException`.
3. **Overlap with #4577 is smaller than triage assumed.** In tracking mode, each publish awaits its
   own confirm.
   - On a channel with handlers, the entry is settled before `BasicPublishAsync` returns
     **[upstream, unverified: ordering]**.
   - On failure, the `finally` at :249-250 removes the tag.
   - So entries from an old channel outlive a switch only if they leaked, which is exactly #4578.
   - The remaining risk: a `multiple=true` ack on C(n+1) (`IsConfirmedBy`, :516-517) is processed
     before a concurrent C(n) send's `finally` runs.
   - Recommendation: clear `_pendingConfirmations` on the switch, as Sync does at :204-206.
4. **The dispose-wait guard (:393).** Adding `ReferenceEquals(Channel, _confirmChannel)` mirrors
   Sync :251. It is defence in depth, inside the existing `_stateLock` snapshot.
   `_inFlightConfirmationCallbacks` must still be awaited.
5. **Race between concurrent sends: real, and a parity gap.**
   - Sync serialises sends under `lock (s_lock)` (Sync :162). In Async, `_connectionLock` covers
     only the replacement (`RmqMessageGateway.cs:145-177`), and `Channel` is re-read at :188, :191,
     :204 and :205.
   - Two sends can both attach, which double-subscribes and leaks a handler.
   - Or a send can attach to C2 and then publish on C3.
   - Fix: capture `var channel = Channel` once, and do compare/detach/attach/clear under a lock
     (`_stateLock` or a dedicated one).
6. **Test seam.** See the red repro above. `FaultingRmqChannel` forwards to a real channel
   (`method.Invoke(_channel, args)`), so it cannot raise acks without a broker. Add a new
   non-forwarding double. A broker-backed IOException-reset test, mirroring the Sync Reactor tests,
   is optional and only for parity.

### Upstream verification (RabbitMQ.Client v7.2.2 source, checked after Confirm)

The **[upstream, unverified]** claims above were checked against the `v7.2.2` tag of
`rabbitmq/rabbitmq-dotnet-client`, `projects/RabbitMQ.Client/Impl/`:

- **The ack handlers run before a tracked publish returns. Verified.** `Channel.cs:677-684` invokes
  `_basicAcksAsyncWrapper` first, then `HandleAck`, which completes the publish's confirmation task.
- **Removing a handler from a disposed channel throws. Verified, but only if the *channel* is
  disposed.** The `AutorecoveringChannel` event accessors go through `InnerChannel`
  (`AutorecoveringChannel.cs:85-94`), whose getter calls `ThrowIfDisposed()` (:61-67).
- **Disposing the connection does *not* dispose its channels. This refutes Q2's concern.**
  `AutorecoveringConnection.DisposeAsync` only clears `_channels` (:303-351). The channel's own
  `_disposed` flag stays false.
  - So the IOException catch's detach after `ResetConnectionToBrokerAsync` is safe in v7: the pool
    disposes the connection, not the channel. The producer disposes its own channel only in
    `DisposeChannelAsync`, after it has detached.
  - So Q2 is **not** a defect in v7, unlike #4567's v6 `AutorecoveringModel`. Reordering the detach
    is optional tidying, not a required fix.
- **`IsClosed` during recovery. Verified.** `IsClosed => !IsOpen`, and
  `IsOpen => !_disposed && _innerChannel.IsOpen` (:146-148). So while the inner channel is down,
  `IsClosed` is true.
- **Duplicate tags under concurrent sends. Verified, a separate defect.**
  - `GetNextPublishSequenceNumberAsync` reads `_nextPublishSeqNo` under `_confirmSemaphore` and
    releases the lock (`Channel.PublisherConfirms.cs:91-107`).
  - `BasicPublishAsync` later takes the number and increments it under the publisher-confirmation
    lock (`Channel.BasicPublish.cs:53-58`, `Channel.PublisherConfirms.cs:327-343`).
  - Two concurrent sends on one channel can therefore record the same tag in the producer.

### Suggested-Fix Assessment

**PARTIAL.** The core of the suggested fix is right:
- track `_confirmChannel` by reference;
- attach when the channel changes;
- detach from the tracked instance;
- use the tracked instance in the dispose guard.

Copied from Sync as it stands, though, it misses:
1. **Concurrency.** Capture the channel once, and compare and attach under a lock. Sync relies on
   `s_lock` instead.
2. **IOException path.** Detach before the reset, and tolerate `ObjectDisposedException`.
3. **Pending confirmations.** Clear them on a channel switch.
4. **Dispose.** Detach from `_confirmChannel` in a `finally`, before `DisposeChannelAsync`, as
   Sync does at :271.

No `ConfirmSelect` equivalent is needed, because v7 enables confirms when it creates the channel
(`RmqMessageGateway.cs:165-167`).

## Scope Notes

**In scope for this fix:**
- **Wrong detach target.** `DetachPublisherConfirmHandlers` (:529-533) uses `Channel`, not the
  tracked instance.
- **Detach after dispose.** The IOException catch detaches after the reset (:235-241). Reorder it,
  and tolerate `ObjectDisposedException`.
- **Don't rely on the IOException catch to detach.** v7 failures may be other exception types.
  Reference tracking on the next send covers this.
- **Concurrent-send attach race.** See Q5.
- **Async half of #4577.** Clear `_pendingConfirmations` on a channel switch, in the same locked
  select step. It costs one line, matches Sync, and closes the `multiple=true` cross-channel race.

**Separate defects. Do not fix here; raise or append to issues:**
- **Nack surfaces as an exception.** With tracking on, a nack makes `BasicPublishAsync` throw
  `PublishException` (XML:2673). Nothing catches it, so the caller gets a raw RabbitMQ exception,
  while `OnPublishFailed` may also raise a failed confirmation. `mandatory` is false
  (`RmqMessagePublisher.cs:94`).
- **Duplicate tags under concurrent sends.** :205 reads `GetNextPublishSequenceNumberAsync`
  separately from `BasicPublishAsync`, which assigns the sequence number under its own semaphore in
  tracking mode **[upstream, unverified]**. So two concurrent sends can read the same N, and the
  second `AddPendingConfirmation` (:437) overwrites the first. Related to #4577.
- **Recovery-window channel leak.** Low priority; see Q1.

**Parity with Sync.** Async has none of these Sync pieces today:
- the `_confirmChannel` field (Sync :63);
- select-on-each-send (:172, :281-290);
- detach and clear before the reset (:204-206);
- the dispose guard (:251);
- detach in `finally` (:271).

## Regression Test

- `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor/When_the_channel_is_replaced_should_confirm_messages_sent_on_the_new_channel.cs`
  - It runs against the real broker. It sends message A, closes channel 0 under the producer (as a
    broker-side close would), then sends message B.
  - It asserts that B's confirmation arrives and succeeds, that channel 0 has no ack or nack
    subscribers, and that channel 1 has exactly one of each.
  - **RED:** it timed out waiting for B's confirmation. **Approved by the user.**
- Doubles:
  - `TestDoubles/ConfirmCountingRmqChannel.cs`, a forwarding `DispatchProxy` over `IChannel` that
    counts confirm subscribers and has a `CloseAsync()`;
  - `TestDoubles/ConfirmCountingRmqProducer.cs`, which keeps every channel the producer opens in
    `Channels`.

## Fix

**Commit:** `fix: RMQ.Async producer moves confirm handlers to a replacement channel`, in
`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs`.
- The `_confirmChannel` field records the channel the handlers are attached to.
- `ListenForConfirmsOnChannel()` runs on each send after `EnsureBrokerAsync`. It compares `Channel`
  with `_confirmChannel` by reference. If they differ, it detaches from the old channel and attaches
  to the new one.
- `DetachPublisherConfirmHandlers()` detaches from `_confirmChannel`, not from whatever `Channel`
  is, and then clears it.

**Scope narrowed after the test, with the user's agreement.** The Confirm gate approved a wider
scope. Writing tests for it showed none of the extra items has a failure a test can reach on a
single-threaded send path once the core fix is in. In tracking mode, each send settles its own
entry before `BasicPublishAsync` returns, and new-channel tags overwrite old entries with the same
tag. So these three items moved out of scope:

- clearing `_pendingConfirmations` on a channel switch;
- the dispose guard and detaching in `finally`;
- the lock around the attach.

The real leftover risk is a concurrent-send defect. Its three symptoms are duplicate tags, a failed
old-channel send's `finally` removing a new-channel entry, and a double attach. It was recorded as
one design problem on #4577:
https://github.com/BrighterCommand/Brighter/issues/4577#issuecomment-6099959203

Detaching after the IOException reset was left as it is: the v7 source shows it is safe (see
Upstream verification).
