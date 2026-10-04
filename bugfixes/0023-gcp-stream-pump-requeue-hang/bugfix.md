# Bugfix: GCP Pub/Sub Stream pump hangs on quit after requeuing (unsettled redelivery blocks WaitForProcessing shutdown)

**Linked Issue**: #4479
**Status**: Verified

## Symptom
A `Reactor` or `Proactor` reading a GCP Pub/Sub **Stream** channel never finishes once its handler
defers and the pump starts requeuing. It makes no visible progress and throws nothing. Only
`--blame-hang-timeout` ends the run. The same scenario on GCP **Pull** finishes in about 65 s.

Reproduction (drafted test `task-6.5-parked/Stream/When_gcp_budget_is_minus_one_should_never_reject.cs`,
plus `_async.cs`, copied to session scratchpad `950b60be-…/scratchpad/`):
1. On the Pub/Sub emulator, create a `GcpPubSubSubscription<ConformanceDeferredCommand>` with
   `subscriptionMode: Stream`, `messagePumpType: Reactor` (or Proactor), `ackDeadlineSeconds: 10`,
   `requeueCount: -1`, no native DeadLetterPolicy, a Brighter `deadLetterRoutingKey` and
   `makeChannels: Create`. Buffer size and performers stay at their defaults (1 and 1).
2. Publish one message. Run `ConformanceDeferredPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000))`
   on a LongRunning task. The handler always throws `DeferMessageAction`.
3. After 60 s, call `channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey))`, then `await pumping`.

- **Expected:** the message is requeued repeatedly (dispatch count > 3), then the pump reads the quit
  and returns.
- **Actual:** `await pumping` never completes, on both Reactor and Proactor. The hang comes before the
  test's own `finally` / `provider.CleanUp`.
- **Rows that pass:** requeueCount 1, 0 and -3 reject on the first deferral, so nothing is redelivered.
  Once a mutation forces those rows to requeue, they hang too.

So what separates passing from hanging is "a redelivery exists when the pump quits", not
"requeue runs".

## Suspected Location
**Shutdown path, where the hang is expected to be:**
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs:179-184`: on `MT_QUIT` the pump itself calls
  `Channel.Dispose()` before it breaks out of the loop.
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs:220-225`: the async twin, `await Channel.DisposeAsync()`.
- `src/Paramore.Brighter/Channel.cs:190-200` and `ChannelAsync.cs:197-202`: these call the consumer's
  `Dispose` / `DisposeAsync`.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs:296-300`:
  `Dispose()` calls `consumer.StopAsync().GetAwaiter().GetResult()`. `DisposeAsync` is at `:306-310`.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs:44-52`: `StopAsync` calls
  `client.StopAsync(new ShutdownOptions { Mode = WaitForProcessing }, CancellationToken.None)` with no
  timeout.

**Message hand-off, where the unsettled message waits:**
- `GcpStreamConsumer.cs:14-19`: an unbounded `Channel<GcpStreamMessage>` buffers the messages the
  SubscriberClient delivers.
- `GcpStreamConsumer.cs:71-85`: `BrighterStreamHandler.HandleMessage` writes the wrapped message
  (`:78`), then `await streamMessage.WaitForCompleteAsync()` (`:79`). The callback does not return
  until the pump settles that specific message.
- `GcpStreamConsumer.cs:93-151`: `GcpStreamMessage`, whose TCS is completed only by `Accepted()`,
  `Reject()` or `Cancel()`.

**Requeue path. It works, and it is what produces the redelivery:**
- `Reactor.cs:259` and `:320`, and `Proactor.cs:297` and `:354`: `DeferMessageAction` calls
  `RequeueMessage`. This is not the `DontAckAction` / `Nack` path, which is #4449's no-op.
- `Reactor.cs:513` and `Proactor.cs:522`, through `Channel.cs:174-176` and `ChannelAsync.cs:181-183`,
  reach `GcpPubSubStreamMessageConsumer.Requeue` (`:267-277`) / `RequeueAsync` (`:287-291`), then the
  private `Nack` (`:320`), then `GcpStreamMessage.Reject()`. `delay` is ignored.

**Receive path:**
- `Channel.cs:142-152` and `ChannelAsync.cs:147-158`: the Brighter `_queue` (for example an enqueued
  quit) is dequeued **before** the consumer is asked for more.
- `GcpPubSubStreamMessageConsumer.cs:220-258`: `Receive` / `ReceiveAsync` (`CancelAfter` +
  `reader.ReadAsync`).

**Flow control:**
- `GcpPubSubConsumerFactory.cs:96-99` and `:128-131`: `maxOutstandingElementCount = BufferSize × NoOfPerformers`,
  which is 1 by default.

## Root-Cause Hypothesis
**H1 (most likely): at quit time an unread redelivered message is parked in `GcpStreamConsumer`'s
channel, and the pump's own quit-time Dispose waits forever for it.**

Sequence:
1. Requeue calls `Reject()`, which completes the TCS with `Reply.Nack` and frees the flow-control slot.
2. The emulator redelivers. `HandleMessage` writes a new `GcpStreamMessage` into the channel (`:78`)
   and blocks on its TCS (`:79`).
3. The quit is enqueued into the Brighter `Channel._queue`. The next `Channel.Receive` dequeues the
   quit first (`Channel.cs:146`) and never reads the buffered redelivery. Nothing will ever Ack, Nack
   or Cancel that message.
4. The pump handles `MT_QUIT` by calling `Channel.Dispose()` (`Reactor.cs:182`, `Proactor.cs:223`). That
   leads to `GcpStreamConsumer.StopAsync` and then `client.StopAsync(WaitForProcessing, CancellationToken.None)`.
   The client waits for the stuck handler, and the handler's cancellation token never fires, so
   `Cancel()` is never called. The pump never returns.

Why the other variants behave differently:
- **Pull passes.** It has no callback, no TCS and no WaitForProcessing.
- **Reject-first rows pass.** Nothing is outstanding at quit.
- **Mutated rows hang.** Forcing requeue creates the unread redelivery.

The skipped generated tests `Stream/Generated/*/When_requeuing_a_failed_message_should_be_redelivered.cs`
(and their siblings, Skip "Deferred: #4240") probably hang by the same mechanism. **UNVERIFIED.**

**Relation to #4449:** same underlying design defect (a callback held open until the pump settles, plus a
`WaitForProcessing` stop with no timeout), but a different trigger:
- #4449's trigger is a message the pump *received* and never settled.
- #4479's trigger is a message the SubscriberClient *delivered* that the pump never received.

Fixing only `Nack`/`NackAsync` would not fix #4479. Fixing the stop path would likely fix both.

Candidate fix directions (**UNVERIFIED — to be proven or refuted in /bugfix:confirm**):
- On stop, complete the writer, drain the local channel and `Reject()`/`Cancel()` every buffered
  `GcpStreamMessage`.
- Use `ShutdownMode.NackImmediately`, or give the shutdown a timeout or cancellation token.
- Settle stale buffered messages on receive.

**Falsifiable check for /bugfix:confirm.** Add `Console.WriteLine` checkpoints (with a timestamp and
MessageId) at:
- (a) `HandleMessage` entry;
- (b) after `WriteAsync`;
- (c) after `WaitForCompleteAsync`, with the reply;
- (d) each return of `ReceiveAsync`;
- (e) `Requeue`;
- (f) `StopAsync`, before and after `client.StopAsync` and after `DisposeAsync`;
- (g) around `Channel.Dispose()` in the quit branch.

H1 is **proven** if the log shows all of the following:
- (a)–(e) repeat through the window;
- then quit received, then "StopAsync enter";
- exactly one MessageId has (a) and (b) but no (c) and no (d);
- there is no "StopAsync exit".

Minimal raw experiment (no pump), with Stream consumer and 1 message:
1. `Receive`, then `Requeue`, then wait about 2 s, then `Dispose()`. H1 predicts a **hang**.
2. Control: `Receive`, `Acknowledge`, `Dispose()`. H1 predicts it completes.
3. Control: `Receive`, `Requeue`, `Receive` the redelivery, `Acknowledge`, `Dispose()`. H1 predicts it completes.

**H2 (alternate): a pump receive never returns**, so the quit is never reached.
- Evidence against: the reject-first rows run the same receive loop for 60 s and complete.
- Discriminator: the last log line is a `ReceiveAsync` entry with no return, and no quit is logged.

**H3 (alternate): WaitForProcessing never finishes even with nothing outstanding**, for example a
nack/ModifyAckDeadline flush, a lease-extension loop, or `DisposeAsync` blocking.
- Discriminator: in raw experiment control 3 (every (a) has a (c)), `Dispose()` still hangs, or
  "StopAsync exit" appears but "DisposeAsync exit" does not.

Ruled out on reading:
- **Requeue delay routed through a scheduler or sleep.** The delay is dropped.
- **`Channel.Requeue` buffering internally.** It passes straight through.
- **Message-ID dedup or TCS collision.** Each delivery gets a fresh `GcpStreamMessage`.

Side note, out of scope: `GcpStreamConsumer.StopAsync` disposes the client but leaves it in the static
`s_consumers` cache (`GcpPubSubConsumerFactory.cs:62`, `:96`). Reusing an equal subscription would get
a disposed client. The tests use GUID names, so this is not in play here.

## Confirmed Root Cause
**H1 confirmed; H2 and H3 refuted. One correction: the wait is bounded, at about 59 min 30 s by
default, not infinite. That is far past any test hang timeout, so it looks like a hang.**

`GcpStreamConsumer` hands each message from the `SubscriberClient` callback to Brighter through an
unbounded `System.Threading.Channels` channel. The callback then stays parked on the message's TCS
(`GcpStreamConsumer.cs:71-85`) until Brighter settles that message. `GcpStreamConsumer.StopAsync`
(`:44-52`) stops the client with `ShutdownMode.WaitForProcessing`, `Timeout = null` and
`CancellationToken.None`. In Google.Cloud.PubSub.V1 3.36.0 that mode:
- cancels only the *pull* token;
- leaves the handler token (`_handlerCts`, linked to the NackImmediately and HardStop tokens only)
  uncancelled until `timeout − 30 s`;
- uses `MaxTotalAckExtension` (60 min default) as the timeout when none is given.

So the `Register(() => streamMessage.Cancel(...))` at `:76` does not fire for about 59.5 min.
**Any message the client has delivered to a callback that Brighter has not settled blocks stop,
and every Dispose of a GCP Stream channel, for about 59.5 min.** There are two ways to get there:
1. **Buffered but never read (#4479).** After a requeue (or an ack, with a backlog), the client pushes
   the next delivery into the channel. The pump dequeues the quit from `Channel._queue` first
   (`Channel.cs:146`, `ChannelAsync.cs:151`), so it never reads that message. The quit branch
   then disposes the channel (`Reactor.cs:183`, `Proactor.cs:224`), and that dispose waits.
2. **Received but never settled (#4449's dispose half).** A message the caller received and never
   acked, rejected or requeued is outside the channel, held only by its TCS. The effect is the same.

## Evidence
- [x] **Live experiment** (emulator, 2026-09-29; temporary probe, not committed, kept in session
  scratchpad `950b60be-…/scratchpad/ZzProbe4479.cs`). `Dispose()` ran on a 20 s watchdog, on a Stream
  channel with the default BufferSize 1 / NoOfPerformers 1:

  | Case | Sequence | Dispose |
  |---|---|---|
  | 1 | Receive → **Requeue** → wait 3 s (redelivery buffered, unread) → Dispose | **HUNG (>20 s)** |
  | 2 | Receive → Acknowledge → wait 3 s → Dispose | returned in 11 ms |
  | 3 | Receive → Requeue → Receive the redelivery (arrived in 49 ms) → Acknowledge → Dispose | returned in 3 ms |

  - Case 1 against case 3 isolates the unread buffered redelivery. Case 3 refutes H3: stop completes
    once nothing is outstanding.
  - Case 3 also shows redelivery after requeue is prompt. The "never redelivers at flow control 1"
    candidate is refuted.
- [x] **Wider live check:** the generated, **not** skipped
  `Stream/Generated/Reactor/When_posting_a_message_via_the_messaging_gateway_should_be_received`
  receives without settling. It was aborted by `--blame-hang-timeout 2min` in `CleanUp`, with the
  `createdump … Permission denied` abort. **The "pre-existing sandbox createdump hangs" in the Stream
  suite recorded during spec 0037 tasks 5.6/5.8 are this bug**, not an infra problem.
- [x] **Code trace** (Opus sub-agent, verified against source):
  - Requeue → `GcpPubSubStreamMessageConsumer.Requeue` `:267-277` → `Nack` `:320` → `GcpStreamMessage.Reject()`.
    This settles the *current* message.
  - `Channel.Receive` dequeues `_queue` before the consumer (`Channel.cs:146-148`).
  - Quit → `Channel.Dispose()` (`Reactor.cs:183`) → `GcpPubSubStreamMessageConsumer.Dispose` `:298`
    (sync-over-async) → `GcpStreamConsumer.StopAsync` `:49`.
  - Library behaviour, from IL of `SubscriberClientImpl.StopAsync` and `SingleChannel` in 3.36.0, and
    the package XML docs:
    - `timeout = Timeout ?? maxExtensionDuration`;
    - WaitForProcessing cancels only `_globalWaitForProcessingCts`;
    - `_handlerCts` links NackImmediately and HardStop only;
    - NackImmediately is scheduled at `timeout − 30 s`;
    - the default `MaxTotalAckExtension` is 60 min.
  - Nothing else drains the channel: no `Writer.Complete` or `TryRead` anywhere. `Purge` only Seeks
    on the server. The `~Channel` finalizer skips the consumer.
- **H2 refuted:** `Channel.Receive` defaults its timeout to 1 s, so `CancelAfter` always arms and
  `ReadAsync` returns `MT_NONE` on timeout. The reject-first rows run this same loop for 60 s and finish.

## Scope Notes
**Suggested-fix assessment:**
- **(b) `ShutdownMode.NackImmediately`: CONFIRMED.** It cancels `_handlerCts` at once, which fires the
  `:76` registration for **every** live callback, buffered or held. `Cancel` makes the TCS throw, the
  `catch (OperationCanceledException)` at `:81-84` returns Nack, and the message is redelivered
  (at-least-once). This covers both routes above in one change.
  - Cost: a message still being processed when the *last* consumer stops is nacked, and its later
    `Accepted()` is a silent no-op. The pump only handles quit between messages, so it holds nothing
    at that point.
  - A `Timeout` on its own only bounds the stall. With ≤ 30 s the library switches to NackImmediately
    at once, anyway.
- **(a) Drain the channel on stop: PARTIAL.** It misses route 2 (held messages). It must complete the
  writer first, because messages keep arriving after stop starts. It must also run only when
  `_handlers` reaches 0.
- **(c) Settle on receive: WRONG.** The consumer is never asked again after the quit is dequeued.

**Wider scope the proven cause implies:**
1. **Any Stream shutdown with a backlog stalls; requeue is not needed.** At flow control 1, acking
   message N lets N+1 into the channel. A Dispatcher stop (`Performer.Stop` enqueues the quit) then
   stalls for about 59.5 min. **This is a production defect for every busy GCP Stream subscription.**
   Every pump exit that disposes the channel shares it: `Reactor.cs:105, 154, 183, 296, 312`,
   `Proactor.cs:146, 195, 224, 333, 344`. Fix (b) covers all of them, because they all end in `StopAsync`.
2. **#4449, the `Nack`/`NackAsync` no-op** (`GcpPubSubStreamMessageConsumer.cs:72-86`, the pump's
   `DontAckAction` path). Fix (b) cures its *dispose* hang, but not its *no-redelivery* stall:
   - the TCS stays open;
   - the lease is extended for up to 60 min;
   - at flow control 1 the whole subscription blocks.
   The fix is `Nack` → `gcpStreamMessage.Reject()`. **Decision for the user:** fold it in here, or
   leave it to #4449's own bugfix. **User's call (2026-09-29): separate bugfix.** This fix covers only
   the stop path.
3. **Generated Stream tests.**
   - `When_posting_a_message_via_the_messaging_gateway_should_be_received` (live-confirmed) and any
     other Stream test that receives without settling stop hanging under (b). Their `CleanUp` then
     completes, nacking the held message.
   - The skipped `When_requeuing_a_failed_message_should_be_redelivered` / `…_with_zero_delay_…` hang
     by route 2. Whether they then *pass* is a matter for the #4240 ledger, not this fix.
   - `When_nacking_a_message_it_should_be_redelivered` also needs #4449's `Nack` fix to pass.
4. **Out of scope, recorded only:**
   - The `s_consumers` cache (`GcpPubSubConsumerFactory.cs:62, 96`) keeps a stopped client. A reuse of
     the same subscription instance calls `StartAsync` on a stopped client, and the resulting `Task`
     is discarded (`GcpStreamConsumer.cs:37`).
   - Sync `Channel` has no `_disposed` guard. The pump disposes, then `CleanUp` disposes again, so
     `_handlers` goes to −1 and `_router.Dispose()` runs twice.
   - The `cancellationToken.Register` at `:76` is never disposed, so one registration accumulates per
     delivered message on a client-lifetime token.
   - `Purge` leaves locally buffered messages in place.
5. **Pull parity:** `GcpPullMessageConsumer` has no callback or TCS, so it has no equivalent defect.
6. **Line-number correction:** the Reactor quit branch is `Reactor.cs:180-186`, with `Dispose` at `:183`.

## Regression Test
Four tests, in `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/` (`Category=GcpPubSubStream`, `[Collection("Stream")]`):
- `When_a_gcp_stream_channel_is_disposed_with_a_redelivery_buffered_after_requeue_should_return_promptly.cs`
  (+ `_async.cs`): route 1, the #4479 shape. Receive, `Requeue`, then wait 2 s so the redelivery is
  buffered unread, then dispose.
- `When_a_gcp_stream_channel_is_disposed_holding_an_unsettled_message_should_return_promptly.cs`
  (+ `_async.cs`): route 2, the dispose half of #4449. Receive, never settle, then dispose.

Each asserts that dispose returns within 10 s, on a watchdog, so a stall fails rather than hangs. It
then asserts that a fresh channel (a new subscription instance, which avoids the `s_consumers` cache)
receives the same message Id, which it acks.

**RED (2026-09-29, emulator):** 4/4 failed with "Disposing the stream channel did not return within
00:00:10". The run finished in about 50 s, with no hang and no stray test host. The user approved the
tests at the test-first gate.

## Fix
`src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs` `StopAsync`: the shutdown mode
changed from `ShutdownMode.WaitForProcessing` to `ShutdownMode.NackImmediately`, and a `<remarks>`
explains why. It is a one-line behavioural change with no structural tidy.
- Any delivered-but-unsettled message, whether buffered or held, is now nacked for redelivery on stop,
  instead of blocking stop for about 59.5 min.
- Out of scope, as recorded in Scope Notes: #4449's `Nack` no-op (a separate bugfix, next), the
  `s_consumers` cache, the `Register` leak and `Purge`.

**GREEN (2026-09-29, emulator):** 4/4 regression tests passed (229 ms – 4 s). No stray test host.

**Verify (2026-09-29, emulator, net10.0):**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub` builds on all target frameworks with 0 warnings
  and 0 errors.
- The regression tests pass 4/4.
- The Stream suite `(Category=GcpPubSubStream|Category=GcpPubSubStreamOrdering)&Fragile!=CI` gives
  **88 passed / 34 skipped / 0 failed, with no hang.** Before the fix, runs of this suite hung (the
  "createdump" aborts during spec 0037 tasks 5.6/5.8).
- The CI filter gives 129 passed / 34 skipped / 50 failed. That is the baseline 129/32/50 plus the two
  Skip-marked AC-39 measurement facts. The 50 failures match the known 45 Firestore + 5 GCS list by
  name, with none in `MessagingGateway`.
- No stray test host.

