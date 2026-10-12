# Bugfix: RMQ.Sync producer keeps old-channel pending confirmations when the channel is replaced without an IOException

**Linked Issue**: #4568 (also #4577)
**Status**: Verified

## Symptom
**Observed.** `RmqMessageProducer` in RMQ.Sync stores pending publisher confirmations in one dictionary keyed by delivery tag (`_pendingConfirmations`). That dictionary lives for the whole producer, not for one channel. Delivery tags restart at 1 on every new channel (or on a channel that has run `ConfirmSelect` again). The only place the dictionary is cleared is the `IOException` catch in `Send`. If the channel is replaced any other way, the old channel's entries stay:

1. **Silent replacement (#4577).** On the next `Send`, `EnsureBroker` → `ConnectToBroker` sees `Channel.IsClosed` and opens a new `IModel`. This happens when the broker closes the channel, when the connection drops and the pool evicts it, or when another gateway sharing the pooled connection resets it. No `IOException` reaches the producer's catch.
2. **Client auto-recovery (#4568, UNVERIFIED).** RabbitMQ.Client 6.8.1 may rebuild the channel underneath the same `IModel` reference. If so, it runs `ConfirmSelect` again, which resets the tags, and keeps the `BasicAcks`/`BasicNacks` handlers.
3. **Non-IOException publish failure.** For example, an `AlreadyClosedException` from `BasicPublish` during an outage. The catch does not run, so nothing is cleared or detached. The entry added just before the publish is also left behind.

What follows on the new or recovered channel:
- `TryAdd(NextPublishSeqNo, …)` returns `false` for a tag that is already in the dictionary, and the result is ignored. The new message is never tracked, so its confirmation is lost and the outbox sends it again (duplicate).
- An ack for tag N on the new channel settles the **old** message at tag N. That message may never have reached the broker.
- Since #4562, an ack with `multiple=true` settles every pending tag ≤ N. One ack can therefore mark a whole run of never-confirmed old messages as dispatched. The outbox records them as sent, so they are lost.

**Expected.** An ack or nack on a channel only settles messages published on that same channel. Entries from a replaced or recovered channel are dropped or failed, never confirmed. Which of the two is still an open design question (see Root-Cause Hypothesis, H4).

**Reproduction (silent replacement, conceptual).**
1. With publisher confirms on, `Send` messages A and B on channel C1 (tags 1 and 2). Make sure no ack arrives.
2. Close C1 without an `IOException` reaching `Send`, e.g. a broker-side channel close or `Channel.Close()` through a test double.
3. `Send` C and D. `ConnectToBroker` opens C2 and `SelectConfirmsOnChannel` re-subscribes. Tags 1 and 2 collide with A and B, `TryAdd` returns false, and C and D are not tracked.
4. Raise an ack on C2 with `deliveryTag: 2, multiple: true`.
5. Observed: A and B are reported confirmed. Expected: only C and D.

The existing test `When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel` covers only the `IOException` path.

## Suspected Location
**Producer (RMQ.Sync)**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs:61`: `_pendingConfirmations` is one dictionary for the whole producer, keyed only by delivery tag.
- `RmqMessageProducer.cs:62-63`: `_confirmChannel` tracks the channel by reference (added in #4567).
- `RmqMessageProducer.cs:165`: `EnsureBroker` may swap `Channel` here.
- `RmqMessageProducer.cs:172`: `SelectConfirmsOnChannel()` is called after the swap.
- `RmqMessageProducer.cs:179`: `_pendingConfirmations.TryAdd(Channel!.NextPublishSeqNo, …)`. The result is ignored. The entry is also added before the publish/scheduler branch at `:181-194`.
- `RmqMessageProducer.cs:201-209`: the only `catch`, and it handles `IOException` only. It is the only place pending entries are cleared (`:206`).
- `RmqMessageProducer.cs:281-290`: `SelectConfirmsOnChannel`. When it sees a new channel instance it detaches from the old one, calls `ConfirmSelect`, subscribes and records the new instance, but does **not** touch `_pendingConfirmations`. This is the fix point suggested in #4577.
- `RmqMessageProducer.cs:292-299`: `DetachConfirms`.
- `RmqMessageProducer.cs:305-319`: `SettleConfirmations`. It does `TryRemove` and raises the event, with no check of which channel the entry came from.
- `RmqMessageProducer.cs:322-325`: `ConfirmedDeliveryTags`. With `multiple`, it settles every pending key ≤ tag (the range sweep that makes the loss worse).
- `RmqMessageProducer.cs:251`: the Dispose `WaitForConfirms` guard. Dispose not clearing entries is not a defect (see bugfix 0053 follow-ups).

**Gateway and connection pool (RMQ.Sync)**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageGateway.cs:84-89`: `ConnectionFactory` is built without setting `AutomaticRecoveryEnabled` or `TopologyRecoveryEnabled`, so library defaults apply. `RmqTlsConfigurator.ConfigureIfEnabled` (`:92`) runs afterwards.
- `RmqMessageGateway.cs:134-168`: `ConnectToBroker`. It returns early if `Channel != null && !Channel.IsClosed` (`:136`). Otherwise it gets a new or reopened pooled connection when `!_pooledConnection.IsOpen` (`:149-159`) and replaces `Channel` with `CreateModel()` (`:162`). This is the silent-replacement path.
- `RmqMessageGateway.cs:180-186`: `ResetConnectionToBroker` resets the shared pooled connection. That is how one gateway closes another gateway's channel.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageGatewayConnectionPool.cs:65-82`: `GetConnection` / `AcquireConnection`. If the pooled connection's `IsOpen == false` (`:73`), it calls `CreateConnection`, which removes and disposes the old connection (`:120`, `:157-165`).
- `RmqMessageGatewayConnectionPool.cs:84-99`: `ResetConnection` always creates a new connection, disposing the shared one.
- `RmqMessageGatewayConnectionPool.cs:116-155`: `CreateConnection`. It sets heartbeat and timeouts (`:124-127`) but no recovery flags. Its `ConnectionShutdown` handler (`:134-146`) removes and **disposes** the pooled connection when `ReferenceEquals(pooled.Connection, sender)`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/ConnectionPoolFactory.cs:65-66`: the retry policy handles `BrokerUnreachableException` and any other `Exception`, so failures inside `ConnectToBroker` are retried and do not surface as `IOException`.

**Exception handling**
- Nothing in RMQ.Sync or RMQ.Async subscribes to `RecoverySucceeded` or uses `IAutorecoveringConnection`, and nothing sets the recovery flags. A grep for `AutomaticRecovery|TopologyRecovery|RecoverySucceeded|IAutorecovering` finds nothing.
- `AlreadyClosedException` is handled only by the consumer (`src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs:276-281`), not the producer.

**Package versions**
- `Directory.Packages.props:13`: `<RabbitMQClientV6>6.8.1</RabbitMQClientV6>`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/Paramore.Brighter.MessagingGateway.RMQ.Sync.csproj:19-20`: Sync uses `VersionOverride="$(RabbitMQClientV6)"`.
- `Directory.Packages.props:143`: RabbitMQ.Client 7.2.2, used by RMQ.Async.

**Test seams**
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel.cs:32-55`: the existing `IOException`-path test.
- `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/CoalescingConfirmsRmqProducer.cs:44-52`: wraps each new channel and keeps every wrapper in `Channels`, so a silent-replacement test can close the current wrapper and check old against new.
- `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/CoalescingConfirmsRmqChannel.cs`: the channel test double.

**RMQ.Async (status only, not in scope)**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs:520-528`: `ListenForConfirmsOnChannel` has the same identity guard and also does not clear pending entries.
- `Async/RmqMessageProducer.cs:225-233`: the `IOException` path clears.
- `Async/RmqMessageProducer.cs:235+`: a `finally` removes the orphaned tag on non-`IOException` failures. Sync has no equivalent.
- `Async/RmqMessageProducer.cs:428`: Async adds with the indexer `_pendingConfirmations[deliveryTag] = …`, which overwrites instead of `TryAdd`. On a collision the stale entry is replaced, not the new message dropped.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageGateway.cs:83-87`: the factory sets no recovery flags either.
- The Async half of #4577 was re-scoped in a comment as a separate concurrent-send defect (duplicate tags, removal across channels, attach race). It is not covered here.

## Root-Cause Hypothesis
**H1: silent channel replacement (#4577). Primary, falsifiable.**
`_pendingConfirmations` lives for the whole producer but means something only per channel. `SelectConfirmsOnChannel` (`RmqMessageProducer.cs:281-290`) is the single place that sees a new channel instance and moves the subscriptions to it, but it does not reset the pending entries. When `ConnectToBroker` (`RmqMessageGateway.cs:136-166`) replaces a closed channel without an `IOException`, the old tags 1..k stay. The new channel's `NextPublishSeqNo` restarts at 1, so:
- `TryAdd` at `:179` silently fails on collision.
- `SettleConfirmations` / `ConfirmedDeliveryTags` (`:305-325`) settle the old entries on the new channel's ack, including a range sweep when `multiple=true`.

*Falsified if:* after closing the current channel without an `IOException`, sending two messages and raising `ack(2, multiple: true)` on the new channel reports only the new messages as confirmed. A test can do this by closing the wrapper in `CoalescingConfirmsRmqProducer.Channels`.

**H2: non-IOException failure leaves entries behind. Contributes to H1.**
The `catch` at `:201` handles only `IOException`. In 6.x, `AlreadyClosedException` derives from `OperationInterruptedException`, which is believed **not** to be an `IOException`; check this against the 6.8.1 assembly. If so, a publish on a closed channel skips both the clear and the detach, and leaves the entry added at `:179`. The next `Send` then goes through H1.

*Falsified if:* `AlreadyClosedException` (or whatever 6.8.1 throws when `BasicPublish` runs on a closed model) is caught by `catch (IOException)`.

**H3: client auto-recovery (#4568). UNVERIFIED, and may be pre-empted by the pool.**
The issue claims that, because `AutomaticRecoveryEnabled` is not set (`RmqMessageGateway.cs:84-89`, `RmqMessageGatewayConnectionPool.cs:124-127`) and defaults to `true` in 6.8.1, recovery rebuilds the inner channel under the producer's `IModel` (an `AutorecoveringModel`). It also claims recovery re-applies `ConfirmSelect` (tags restart at 1) and keeps the `BasicAcks`/`BasicNacks` handlers. In that case `ReferenceEquals(Channel, _confirmChannel)` stays true, `SelectConfirmsOnChannel` returns early, and the old entries are never cleared.

Code in this repo argues **against** recovery ever completing under the producer:
- The pool's `ConnectionShutdown` handler (`RmqMessageGatewayConnectionPool.cs:134-146`) removes and **disposes** the pooled connection on shutdown. Disposing an auto-recovering connection is believed to cancel recovery.
- `GetConnection` (`:73-75`) disposes and replaces any pooled connection whose `IsOpen` is false.
- During the outage `Channel.IsClosed` is true, so the next `Send` replaces the channel through `ConnectToBroker` (`RmqMessageGateway.cs:142-162`). That is H1, not recovery.

Recovery could still win in two ways:
- The shutdown handler's `ReferenceEquals(pooled.Connection, sender)` guard (`:141`) fails because 6.8.1 raises the event with the inner `Connection` as `sender`, not the `AutorecoveringConnection`. The pooled connection would then not be disposed.
- No `Send` happens during the outage, so recovery finishes first.

Several points must be verified against the 6.8.1 assembly or a real broker:
- (a) the `AutomaticRecoveryEnabled` default;
- (b) which object is the `sender` of `ConnectionShutdown`;
- (c) whether `Dispose` stops recovery;
- (d) whether a recovered model re-runs `ConfirmSelect`, resets `NextPublishSeqNo`, and keeps its ack/nack subscriptions.

*Falsified if:* with a real broker, a forced connection drop and no intervening `Send`, the producer's `Channel` after reconnect is a different instance, or its `NextPublishSeqNo` does not restart.

**H4: open design question, nack vs drop.**
Clearing silently loses the "never confirmed" signal but is safe: the outbox resends later. Raising `PublishConfirmationResult(success: false)` for each stale entry tells subscribers explicitly. From memory of 6.x, and UNVERIFIED: channel shutdown does not raise `BasicNacks` for outstanding confirms, so nobody else will report them.

**Suggested fixes. All UNVERIFIED — to be proven or refuted in /bugfix:confirm:**
- From #4568: clear pending confirmations on `IAutorecoveringConnection.RecoverySucceeded`, or on a model-level equivalent such as the recovery event of `AutorecoveringModel`. **UNVERIFIED — to be proven or refuted in /bugfix:confirm.** Its value depends on H3, which may never happen given the pool's dispose-on-shutdown behaviour.
- From #4577: clear or fail pending entries in `SelectConfirmsOnChannel` when a new channel instance is seen (`RmqMessageProducer.cs:283-289`). **UNVERIFIED — to be proven or refuted in /bugfix:confirm.** It would cover H1. It covers H2 only once the next `Send` replaces the channel. It does not cover H3, because the reference does not change.
- Also implied: catch `AlreadyClosedException` / `OperationInterruptedException` in `Send`, and remove the orphaned tag (as Async's `finally` does). **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**
- Also implied: stop ignoring the result of `TryAdd`. **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**

**Related but separate (from bugfix 0054, Scope Notes item 3; now #4579/#4580).** On the scheduler path, `:179` adds a pending entry before the branch at `:181-194`. When nothing is published, `NextPublishSeqNo` does not advance and the next tag collides. It is a different trigger with the same `TryAdd` collision. Keep it out of this fix unless confirm shows it shares the fix point.

## Confirmed Root Cause
**Verdict:** H1 CONFIRMED (code trace). H2 CONFIRMED. H3 CONFIRMED by client code, not run against a real broker. H4 CONFIRMED (shutdown raises no nacks).

`RmqMessageProducer._pendingConfirmations` (`RmqMessageProducer.cs:61`) is keyed only by delivery tag and lives for the whole producer. A delivery tag only means something within one channel incarnation. The only reset is the `IOException` catch (`:201-209`), and in 6.8.1 that is reachable in practice only through `BrokerUnreachableException` from `EnsureBroker`, not from `BasicPublish`. Three other paths start a new tag sequence, or orphan an entry, without clearing:

1. **H1.** `ConnectToBroker` swaps in a new `IModel` (`RmqMessageGateway.cs:136-162`).
2. **H3.** Client auto-recovery swaps the inner model under the **same** `AutorecoveringModel` reference, so the `ReferenceEquals(Channel, _confirmChannel)` guard (`:283`) returns early.
3. **H2.** A `Send` fails after `TryAdd` (`:179`) with a non-IOException, leaving the entry orphaned.

After any of these, `TryAdd` at `:179` silently drops new messages on a tag collision. `SettleConfirmations`/`ConfirmedDeliveryTags` (`:305-325`) then settle stale entries as success, including whole ranges when `multiple=true`. The outbox marks those messages dispatched and never resends them.

## Evidence
- [x] **Code trace.** RabbitMQ.Client 6.8.1 (`~/.nuget/packages/rabbitmq.client/6.8.1/lib/netstandard2.0/RabbitMQ.Client.dll`) was decompiled with ilspycmd 11.1 (installed into the session scratchpad; output in `scratchpad/rmq681/`). Line numbers below refer to that decompiled output.
  - **(a) `AutomaticRecoveryEnabled` defaults to `true`.** `ConnectionFactory.cs:152` (`TopologyRecoveryEnabled` also `true`, `:265`). `CreateConnection` (`:600-615`) returns `AutorecoveringConnection`. Brighter never sets the flag (`RmqMessageGateway.cs:84-89`, `RmqMessageGatewayConnectionPool.cs:124-127`), so every pooled connection is auto-recovering. `NetworkRecoveryInterval` defaults to 5 s (`:176`).
  - **(b) The `sender` of `ConnectionShutdown` is the inner `Connection`, not the wrapper.** `AutorecoveringConnection.cs:384-396` forwards the handler to `_delegate`; `Connection.OnShutdown` (`Framing.Impl/Connection.cs:581-611`) calls `eventHandler(this, …)`. So `ReferenceEquals(pooled.Connection, sender)` at `RmqMessageGatewayConnectionPool.cs:141` is **never true**: the pool's `ShutdownHandler` is dead code (the guard came in with #4047, commit 88c32ea12). The triage's "pool pre-empts recovery" argument is **refuted**, and #4577's bullet saying the pool's shutdown handler removes the connection is wrong. Replacement happens lazily on the next `Send` through the `IsOpen` checks.
  - **(c) Dispose/Close/Abort stop recovery.** `Dispose` → `Abort()` → `StopRecoveryLoop()` (`AutorecoveringConnection.cs:722-733`, `:867-889`, `:1275-1283`). Recovery is cancelled only if something disposes the connection, i.e. a `Send` or other gateway activity during the outage reaching `GetConnection` (`:73`), `ConnectToBroker` (`:149-153`) or `ResetConnection`. With no `Send` during the outage (it lasts at least 5 s), recovery wins.
  - **(d) Recovery re-runs `ConfirmSelect`, restarts tags at 1 and keeps our handlers.** `AutorecoveringModel.AutomaticallyRecover` (`Impl/AutorecoveringModel.cs:354-394`) creates a fresh `RecoveryAwareModel`, re-attaches the recorded ack/nack handlers (`:368-369`), calls `ConfirmSelect()` when publisher confirms were used (`:380-383`), swaps `_delegate` (`:388`) and raises the model-level `Recovery` event. A fresh `ModelBase` starts at `NextPublishSeqNo = 0` (`ModelBase.cs:186-189`); `ConfirmSelect` sets it to 1 (`:1061-1066`). `InheritOffsetFrom` (`RecoveryAwareModel.cs:19-23`) only offsets consumer delivery tags.
  - **(e) The model's identity is stable across recovery.** Only the private `_delegate` changes, so the producer's reference check does not notice. The `sender` passed to `BasicAcks` handlers is the inner `RecoveryAwareModel` (`ModelBase.cs:548`), so a sender-based check against `Channel` would never match either.
  - **(f) `IsClosed` is true during an outage and false after recovery.** `AutorecoveringModel.cs:128-139`, `ModelBase.cs:113`, `:471-478`. A `Send` during the outage goes down the H1 path, and the pool's dispose cancels recovery.
  - **(g) `BasicPublish`/`ConfirmSelect` on a closed model throw `AlreadyClosedException`**, which is not an `IOException`: `AlreadyClosedException : OperationInterruptedException : RabbitMQClientException : Exception` (`Exceptions/AlreadyClosedException.cs:9`, `OperationInterruptedException.cs:13`, `RabbitMQClientException.cs:6`). `ModelBase.BasicPublish` (`:993-1018`) increments `NextPublishSeqNo` (`:1015`) **before** the send throws (`SessionBase.cs:127-140`). In 6.8.1 the socket write is `SocketFrameHandler.Write` → `_channelWriter.TryWrite` (`SocketFrameHandler.cs:197-200`), so `BasicPublish` never throws `IOException` for a dead socket. The only `IOException` the client raises is `BrokerUnreachableException : IOException` (`BrokerUnreachableException.cs:9`), from `CreateConnection` inside `EnsureBroker` after the retry policy gives up (`ConnectionPoolFactory.cs:64-86`). The injected `IOException` in the existing test (`CoalescingConfirmsRmqChannel.cs:107-109`) is therefore not a realistic production trigger.
  - **H4.** `ModelBase.OnModelShutdown` (`:443-469`) drains continuations and raises `ModelShutdown` only. `HandleBasicNack` is called only from broker frames, and recovery does not replay. Stale entries are never settled by the client.
  - **H1 trace through the repo.** `RmqMessageGateway.cs:136/142` (`Channel.IsClosed`) → `:149-159` (new or reacquired pooled connection) → `:162` (`Channel = CreateModel()`) → `RmqMessageProducer.cs:172` → `:283-289` (new reference: detach, `ConfirmSelect`, subscribe, **no clear**) → `:179` `TryAdd(1, …)` returns false and is ignored → ack(2, multiple) on the new channel → `:322-325` sweeps old tags 1..2 → `:310-312` raises success for the old messages.
  - **Triggers for a closed channel with no `IOException`:** a broker channel-level close (e.g. 404; auto-recovery is connection-level only and does not reopen it); a connection drop followed by a `Send` during the outage; another gateway's `ResetConnectionToBroker` → `ResetConnection` → `CreateConnection` → `TryRemoveConnection` → `Dispose` (`RmqMessageGatewayConnectionPool.cs:84-99`, `:157-165`).
- [ ] **Red repro:** not run in Confirm (it would build into the repo's `bin/obj`; Confirm writes no source). RabbitMQ is up (`brighter-rabbitmq-1`, 5672 and 15672 open). The management API credentials and connection-name lookup are unverified, because a `curl` to 15672 was denied by the permission system. See "Recommended regression tests" under Scope Notes.

## Scope Notes
**Suggested-fix assessment**
- **Clear on `IAutorecoveringConnection.RecoverySucceeded` (#4568): PARTIAL.** The event exists (`AutorecoveringConnection.cs:326`, raised at `:474`) but is connection-scoped, and the connection is shared by every gateway in the static pool; the producer only sees `IModel`. The model-level `IRecoverable.Recovery` event (`IRecoverable.cs:8-11`, implemented by `AutorecoveringModel`) is the better hook, but it is racy: it fires after `_delegate` is swapped (`:388`), so a concurrent `Send` can publish at tag 1, collide with a stale tag 1, and have its ack settle the stale entry before the clear runs. The handler runs on the recovery thread inside `lock(_models)` and must **not** take the static `s_lock` (`StopRecoveryLoop` waits on the recovery task, causing a ~5 s stall). A deterministic complement is to detect a sequence regression in `Send` under `s_lock` (`Channel.NextPublishSeqNo` not above the last tag published on this channel), which covers H1 and H3 at the point of use.
- **Clear or fail in `SelectConfirmsOnChannel` on a new instance (#4577): PARTIAL.** Covers H1, and H2 once the next `Send` replaces the channel. Does not cover H3 (same reference) or a non-IOException failure on a channel that stays open (item 1). A bare `Clear()` also has a small race: `HandleBasicAck` snapshots its invocation list (`ModelBase.cs:542`), so an old-connection ack already being dispatched can run after `DetachConfirms`, and its key snapshot (`:324`) can then `TryRemove` a *new* entry with the same tag. The robust shape is **one pending-confirmation map per channel incarnation, bound by closure into the handlers; swap the map rather than clear it.**
- **Catch `AlreadyClosedException`/`OperationInterruptedException` and remove the orphan: PARTIAL as stated, CONFIRMED in generalised form.** Catching only those types misses `ConfigurationException`/`InvalidOperationException` thrown before `BasicPublish`. Mirror Async (`Async/RmqMessageProducer.cs:200-208`, `:235-245`): record the tag, set `pendingDeliveryTag`, clear it after publish returns, remove it in `finally` on **any** exception. Do not clear the whole map there.
- **Stop ignoring the `TryAdd` result: PARTIAL.** A symptom amplifier, not the cause. Use the indexer `[tag] = …` (Async parity, `:428`), optionally logging a warning if a key was present. On its own it fixes only the "new message untracked" half.

**Additional defects and scope changes**
1. **New, more severe H2 variant on an open channel.** `RmqMessagePublisher.CreateBasicProperties` throws `ConfigurationException` for a non-AMQP header value (`RmqMessagePublisher.cs:233-241`, `IsAnAmqpType` `:253-262`; e.g. a `Guid` in `Header.Bag`, copied by `AddUserDefinedHeaders` `:196-205`). That happens **after** `TryAdd` (`RmqMessageProducer.cs:179`) and **before** `BasicPublish`, so `NextPublishSeqNo` does not advance and the channel stays open. The next `Send` collides on the same tag N and is untracked; the broker's ack for N then reports the failed, never-published message as success and the outbox marks it dispatched. Neither clear-on-channel-change nor recovery events catch this. Only orphan removal on failure does.
2. **The scheduler path (#4579/#4580) shares the fix point.** `:179` adds the entry before the branch at `:181-194`, so the scheduler branch orphans tag N without publishing. Same mechanism as item 1, same fix: add the entry only on the publish branch (as Async does, `:200-205`) and remove it on failure. Recommended in the same change, or at minimum the fix must not leave `:179` outside the publish branch. **Needs a user decision on whether #4579/#4580 fold into this fix.**
3. **The pool's shutdown handler is dead code under auto-recovery** (`RmqMessageGatewayConnectionPool.cs:134-146`, see (b)). A separate defect; do not fix here. Note the interaction: "fixing" it to evict and dispose on shutdown would cancel recovery for every consumer sharing the connection.
4. **Thread safety.** `s_lock` is static, so it serialises `Send` across all producers, but does not cover `SettleConfirmations`, which runs on the client's connection thread. `ConcurrentDictionary` makes single operations safe; the snapshot-then-`TryRemove` at `:322-325` racing with Clear+TryAdd is the remaining hazard, which the per-channel map closes. Never take `s_lock` from a `Recovery` handler.
5. **Nack vs drop (H4 design question): drop, with a log line.** On `Success=false`, `OutboxProducerMediator` logs `ConfirmationFailed` and calls `TripTopic(result.Topic)` (sync `:1020-1028`, async `:925-934`), and tripped topics are excluded from the sweeper (`:750`) until `CoolDown`. Nacking stale entries would trip the breaker for every topic with in-flight messages at reconnect, delaying their redelivery. Dropping leaves them undispatched and the sweeper resends (at-least-once). It matches the existing Sync `IOException` clear (`:206`) and Async `ClearPendingConfirmations`.
6. **Sync/Async parity.** Async already adds the entry only on publish, overwrites on collision and removes the orphan in `finally`. It lacks a clear on new channel (`ListenForConfirmsOnChannel` `:520-528`) and any recovery handling, and its factory sets no recovery flags (`Async/RmqMessageGateway.cs:83-87`). 7.2.2 behaviour was NOT verified. Async stays out of scope here (its remaining half is the concurrent-send defect in #4577's comment).
7. **Old channels are never disposed** on replacement in `ConnectToBroker` (`:162`), so they leak. Out of scope.
8. **Existing test caveat.** The existing `IOException`-path test injects an exception the real 6.8.1 client cannot raise from `BasicPublish`; note this in the test rather than treat it as the realistic trigger.

**Recommended regression tests** (all `[Collection("RMQ")]`, using `CoalescingConfirmsRmqProducer`/`CoalescingConfirmsRmqChannel`, which withhold real acks so the test raises them)
1. **H1, silent replacement (cheapest, deterministic).** Send A and B, `producer.Channels[0].Close()`, send C and D (opens `Channels[1]`), `Channels[1].RaiseAck(2, multiple: true)`. Assert only C and D are confirmed. Today A and B are reported.
2. **H2 on an open channel, real exception, no injection (most important new test).** Send A with `Header.Bag["x"] = Guid.NewGuid()`; assert `ConfigurationException`. Send B on the same channel (same tag N). `RaiseAck(N, false)`. Assert only B is confirmed and A is never reported. Today A is reported as success and B is lost.
3. **H2 on a closed channel (optional).** Extend the double with `FailNextPublishWith(Exception)` throwing `AlreadyClosedException`. Caveat: the double throws before forwarding, so it does not advance `NextPublishSeqNo` as the real client does; it models test 2 rather than the true closed-model sequence.
4. **H3, real recovery (slow, at least 6 s, needs a live broker).** Send A and B (acks withheld). Force-close the producer's connection with `DELETE /api/connections/{name}` on 15672 (name from `GET /api/connections`, credentials guest/guest unverified). Do **not** `Send` during the outage; poll until `IsOpen` goes false then true. Assert `Channels.Count == 1` (same reference) and `NextPublishSeqNo == 1` (this doubles as the recorded falsification check for H3). Send C and D, `RaiseAck(2, true)`, assert only C and D confirmed. Kills a pooled connection shared with other gateways, so keep it in the serial RMQ collection. Prefer this to a `DispatchProxy` simulation, which tests our model of the client rather than the client.

## Regression Test
**Test 1 of 3 (H1, silent channel replacement) — written, RED observed, approved, now GREEN.**
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_the_channel_is_replaced_should_not_confirm_messages_pending_on_the_old_channel.cs`
- Test double: added `Close()` to `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/CoalescingConfirmsRmqChannel.cs`.
- RED failure: confirmed IDs were the two *old* messages, expected the two sent on the new channel.

**Test 2 of 3 (H2, open-channel orphan) — written, RED observed, approved, now GREEN.**
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_a_send_fails_before_publishing_should_not_confirm_the_unpublished_message.cs`
- A `Guid` header makes `Send` throw `ConfigurationException` after `TryAdd`; the next message takes the same tag. RED: the failed, never-published message was reported as confirmed.

**Test 3 of 3 (H3, real auto-recovery) — written, RED observed, approved, now GREEN.**
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_the_connection_auto_recovers_should_not_confirm_messages_pending_before_the_outage.cs`
- Runs against the live broker: names the producer connection, finds it through `GET /api/connections` (polling, as the listing lags), force-closes it with `DELETE`, sends nothing during the outage, and waits for the client to recover the channel (about 10 s).
- Two preconditions pin the mechanism and **held against the real client**: the producer's `Channels` still has one entry (same `IModel` reference, so `SelectConfirmsOnChannel` cannot see the recovery) and `NextPublishSeqNo == 1` (tags restarted). This empirically confirms H3.
- RED failure: the two pre-outage messages were reported confirmed instead of the two sent after recovery.

## Fix
`src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs`, three changes, one per confirmed path:
1. **H1, channel replaced (test 1).** `SelectConfirmsOnChannel` clears `_pendingConfirmations` (and resets `_lastPublishedDeliveryTag`) when it sees a new channel instance.
2. **H2, send fails before publishing (test 2).** `SendWithDelay` remembers the tag it recorded (`pendingDeliveryTag`) and a `finally` removes it unless the send completed. It is set to null after the publish/schedule branch, so the scheduler path (#4579) is unchanged.
3. **H3, client auto-recovery (test 3).** `ClearPendingConfirmationsIfTagsRestarted` runs under `s_lock` in `Send`: if the channel's next tag is at or below `_lastPublishedDeliveryTag` (set only after a real publish), the sequence restarted on the same channel and the pending entries are cleared. Chosen over a `Recovery`/`RecoverySucceeded` handler because that handler would run on the recovery thread concurrently with `Send` and cannot take `s_lock` (it would stall `StopRecoveryLoop`).

Dropped, not nacked (Scope Notes item 5). The per-channel-map shape from Confirm was not needed to make the three tests pass, so it is not built.

**Behaviour to know about:** after a recovery, stale entries stay in the dictionary until the next `Send` clears them. That is safe: the recovered channel only ever acks tags it has published, and a new message is recorded only after the clear, so no stale entry can be settled. Not covered: RMQ.Async (separate concurrent-send defect, see #4577 comment) and the pool's dead `ShutdownHandler` (Scope Notes item 3).
