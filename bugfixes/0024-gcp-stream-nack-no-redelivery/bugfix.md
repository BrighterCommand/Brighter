# Bugfix: GCP Pub/Sub Stream `Nack`/`NackAsync` are no-ops, so a nacked message is held (lease extended) and not redelivered, and at flow control 1 the subscription stalls

**Linked Issue**: #4449
**Status**: Verified

## Symptom
A handler on a GCP Pub/Sub **Stream** channel throws `DontAckAction`, or a caller calls `Channel.Nack`
directly. After that, the message is not redelivered while the consumer runs. At the default
`BufferSize: 1` / `NoOfPerformers: 1`, no further message on that subscription is likely to be delivered
either.

- **Expected:** Nack "releases it back to the transport for redelivery … available for reprocessing on
  the next receive call" (`IAmAMessageConsumerSync.cs:67-76`; async twin at `IAmAMessageConsumerAsync.cs:79`).
- **Actual (from the issue, not yet reproduced live for this half):** the `SubscriberClient` callback
  stays parked. The client keeps extending the lease for up to `MaxTotalAckExtension` (60 min by
  default). Nothing is redelivered, and nothing behind the message is delivered.
- **Already fixed by 0023 (`a010129ea`):** the *dispose-hang* half of #4449. `StopAsync` now uses
  `NackImmediately`, so Dispose returns and nacks the held message. This bugfix covers only the
  *no-redelivery / stall* half.

Reproduction (the generated FR-16 tests, currently Skip-marked):
1. Stream subscription with default flow control. Publish one message.
2. `Receive(5 s)`, then `Nack(received)`.
3. Poll `Receive(500 ms)` for 30 s.
- **Expected:** the same message Id is redelivered.
- **Predicted today:** `MT_NONE` for all 30 s.

Two-message variant: nack the first message, then expect both Ids within 30 s. The prediction is that
neither Id arrives, because the only flow-control slot is held.

## Suspected Location
**The no-ops:**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs:67-75`: `Nack(Message)`
  is empty. Its comment says "not acknowledging is sufficient for redelivery", which is true for Pull
  and false for Stream.
- `GcpPubSubStreamMessageConsumer.cs:77-86`: `NackAsync` returns `Task.CompletedTask`.

**The working settle path that Nack does not use:**
- `GcpPubSubStreamMessageConsumer.cs:267-277`: `Requeue` looks up `Bag["ReceiptHandle"] is GcpStreamMessage`
  (`:269`), then calls the private `Nack(GcpStreamMessage)` at `:320`, which calls `gcpStreamMessage.Reject()`.
- `GcpPubSubStreamMessageConsumer.cs:110-115` and `:146-151`: `Reject`/`RejectAsync` already call
  `gcpStreamMessage.Reject()` on routing failure (R-19).

**Handle lookup:**
- There is no dictionary. `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:79` stores the
  `GcpStreamMessage` itself in `Bag["ReceiptHandle"]`.
- Only `GcpRejectionRouter.cs:220` removes it, and only on the Reject path.

**Stream hand-off** (line numbers as of `a010129ea`; 0023's references to this file have moved):
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs:77-91`: `BrighterStreamHandler.HandleMessage`
  writes to the channel (`:84`), then `await streamMessage.WaitForCompleteAsync()` (`:85`).
- `GcpStreamConsumer.cs:99-157`: `GcpStreamMessage`. Its TCS is completed only by `Accepted()`
  (`:130-133`), `Reject()` (`:139-142`, `Reply.Nack`) or `Cancel()` (`:148-151`).
- `GcpStreamConsumer.cs:50-58`: `StopAsync`, now `NackImmediately` (0023).

**The pump's DontAckAction path:**
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs:263-273` (the AggregateException branch, `Channel.Nack`
  at `:269`) and `:322-332` (`catch (DontAckAction)`, `Channel.Nack` at `:328`). Each then sleeps
  `DontAckDelay`, which is 1 s by default (`MessagePump.cs:92`).
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs:307` and `:362`: `await Channel.NackAsync(message)`.
- `src/Paramore.Brighter/Channel.cs:102-105` and `ChannelAsync.cs:105-108`: straight pass-through to the consumer.

**Flow control:**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubConsumerFactory.cs:96-99`:
  `maxInFlightMessages = BufferSize × NoOfPerformers`.
- `GcpPubSubConsumerFactory.cs:129-131`: that value becomes `FlowControlSettings(maxOutstandingElementCount: …)`.
- `StreamingConfiguration` runs before this (`:126`), so it can set `MaxTotalAckExtension` but cannot
  override flow control.

**Pull, for parity:**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs:84-92` (`Nack`) and
  `:100-103` (`NackAsync`) are also no-ops. On Pull that is *correct in kind*: nothing extends the
  lease, so the message comes back when the ack deadline lapses.
- Pull's requeue uses `ModifyAckDeadline(…, 0)` (`:487`, `:497-501`) for an immediate release.
- Pull's FR-16 cell is also `Deferred -> #4240`. The likely reason is that the lapse (subscription
  default `ackDeadlineSeconds = 30`, `GcpPubSubSubscription.cs:155`) is at or past the test's 30 s
  window. That is out of scope here and only noted.

**Contract:**
- `IAmAMessageConsumerSync.cs:67-74`: "releasing it back to the transport for redelivery".
- `src/Paramore.Brighter/Actions/DontAckAction.cs:34-38`: "use the transport's not-acknowledged
  disposition … other transports may redeliver later".
- Kafka's stream-style consumer actively seeks back on Nack (`KafkaMessageConsumer.cs:357-378`). It does
  not rely on passivity.

**Generated tests and ledger:**
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/Generated/Reactor/When_nacking_a_message_it_should_be_redelivered.cs`:
  - Skip reason at `:42` and `:89`: `"Deferred: #4240 — Nack redelivers not yet conformant for GCP / Stream (maintainer sign-off)"`.
  - Asserts at `:63-86`: receive in 5 s, Nack, poll for 30 s, then `Assert.NotEqual(MT_NONE)` at `:80`,
    then HandledCount ≥ sent, then message equality.
  - The second fact, `When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second`
    (`:90-154`), expects both Ids within 30 s, acking each.
- The Proactor twin is `…/Stream/Generated/Proactor/When_nacking_a_message_it_should_be_redelivered.cs`.
  The StreamOrdering twins are `…/StreamOrdering/Generated/{Reactor,Proactor}/…`.
- The skip comes from the ledger matrix, `specs/0036-universal-transport-conformance-tests/conformance-status.md:947`
  (GCP / Stream) and `:948` (GCP / StreamOrdering), FR-16 column = `Deferred -> #4240 (sign-off: @iancooper)`.
  The generator reads it through `tools/Paramore.Brighter.Test.Generator/ConformanceLedger.cs:62` and `:113-117`.
- To un-skip: change those FR-16 cells to `Fixed (#<PR>)`, then regenerate. Do not hand-edit the
  generated files.

## Root-Cause Hypothesis
**H1 (most likely): `Nack`/`NackAsync` on the Stream consumer never complete the message's TCS, so the
`SubscriberClient` treats the message as still being processed.**

Sequence:
1. `Channel.Nack` reaches the no-op at `GcpPubSubStreamMessageConsumer.cs:72-75`.
2. `HandleMessage` stays parked at `GcpStreamConsumer.cs:85`.
3. The client keeps extending the lease, up to `MaxTotalAckExtension` (60 min by default), so the
   server does not redeliver.
4. At flow control 1 the single outstanding slot stays occupied, so the next message is not delivered
   either. The subscription stalls until the lease cap is reached or the consumer stops, and stop now
   nacks the message (0023).

Prediction for the un-skipped generated tests today (with 0023's fix in place):
- They would **fail on an assertion, not hang.**
- Single message: `Assert.NotEqual(MT_NONE …)` fails at `:80` after about 30 s.
- Two messages: `Assert.Contains` fails for the other Id, and possibly for both.
- `CleanUp` → `channel.Dispose()` → `StopAsync(NackImmediately)` then returns promptly.

Suggested fix (from the issue and 0023 Scope Notes item 2): make `Nack`/`NackAsync` look up the
`GcpStreamMessage` in `Bag["ReceiptHandle"]` and call `gcpStreamMessage.Reject()`, as `Requeue` already
does through the private `Nack` at `:320`. **UNVERIFIED — to be proven or refuted in /bugfix:confirm.**

Supporting evidence: 0023's live Case 3 showed Requeue → `Reject()` → redelivery in about 49 ms on the emulator.

**Discriminating checks for /bugfix:confirm.** A raw probe with no pump: a Stream channel at default
flow control, `Receive` / `Nack` / `Receive` on watchdogs.
- **(a) One message:** `Receive`, `Nack`, then poll 30 s. H1 predicts `MT_NONE` throughout, and a
  checkpoint at `GcpStreamConsumer.cs:85` shows no completion.
- **(b) Two messages published:** `Receive` m1, `Nack`, poll 30 s. H1 predicts m2 is **also** never
  received. That proves the whole-subscription stall rather than a one-message delay.
- **(c) Control:** same as (a), but call `Requeue` instead of `Nack`. H1 predicts redelivery within
  about 1 s (0023 measured 49 ms).
- **(d) Lease-extension check:** same as (a) with `StreamingConfiguration` setting
  `MaxTotalAckExtension = 10 s`. H1 predicts redelivery only after the cap lapses, as in ADR 0077's
  AC-42 procedure (`docs/adr/0077-delivery-count-contract.md:221-226`). That shows the held lease, not
  a lost message, is what blocks.

**Alternate hypotheses:**
- **H2: the handle lookup fails, so even `Reject()` would not reach the right message.**
  - Unlikely. `Parser.cs:79` stores the live `GcpStreamMessage`, the pump passes the same `Message`
    instance, and only the Reject router strips the handle (`GcpRejectionRouter.cs:220`).
  - Discriminator: in probe (a), log `Bag["ReceiptHandle"] is GcpStreamMessage` inside `Nack`, and
    check that it is the same reference `HandleMessage` created. Probe (c) already exercises the same
    lookup through `Requeue`.
- **H3: `Reject()` nacks correctly, but redelivery is delayed past the 30 s window**, for example by
  the subscription's retry-policy backoff, emulator behaviour, or the client not re-pulling at flow
  control 1.
  - Discriminator: after a candidate fix, measure Nack → redelivery latency on the emulator. It should
    be about the same as probe (c). The provider's plain subscription
    (`GcpStreamMessageGatewayProvider.cs:168-176`) has no DeadLetterPolicy or RetryPolicy.
- **H4: the pump's DontAckAction path does something else that delays or suppresses redelivery**,
  such as `Thread.Sleep(DontAckDelay)` or `IncrementUnacceptableMessageCount` reaching the
  unacceptable-message limit.
  - These are pump-level effects. The 1 s sleep does not explain a 60 min stall, and the raw
    `Channel.Nack` tests never go through the pump.
  - Discriminator: if probes (a) and (b) reproduce without a pump, the transport is the cause.
- **H5: the emulator does not honour client lease extension**, so redelivery happens at the
  subscription ack deadline instead. The provider default is 30 s (`GcpPubSubSubscription.cs:155`),
  which is borderline for the test window.
  - Discriminator: probe (a) with a 90 s poll. H1 predicts nothing arrives. H5 predicts redelivery at
    about the ack deadline.

**StreamOrdering note:**
- The `FifoMessageBuilder` sets one random partition key per builder
  (`tests/Paramore.Brighter.Gcp.Tests/FifoMessageBuilder.cs:45-48`), so both messages in the
  two-message fact share an ordering key.
- With ordering, Pub/Sub serialises same-key delivery. A held (un-nacked) message blocks its key even
  without flow control 1, so the stall is expected on this variant as well.
- After a real Nack, Pub/Sub is documented to redeliver the nacked message and then the later same-key
  messages, in order. The test tolerates this because it absorbs repeats in a set.
- The ordering-key "pause" is a *publisher*-side behaviour, triggered by a publish failure, and should
  not apply here. **UNVERIFIED.**
- Discriminator: run probes (a) and (b) on an ordering-enabled subscription, with a shared key, and
  with no key.

## Confirmed Root Cause
**H1 confirmed.** H2, H3 and H4 are refuted, and H5 is refuted live.

`GcpPubSubStreamMessageConsumer.Nack` (`:72-75`) and `NackAsync` (`:83-86`) never settle the
`GcpStreamMessage`. Its TCS is completed only by `Accepted()`, `Reject()` or `Cancel()`
(`GcpStreamConsumer.cs:130-151`). Since 0023, `Cancel` fires only when the consumer stops. So after a
Nack, `BrighterStreamHandler.HandleMessage` stays parked at `GcpStreamConsumer.cs:85`, and the
`SubscriberClient` (Google.Cloud.PubSub.V1 3.36.0) keeps treating the message as being processed:
- **It holds a flow-control slot.** The user handler runs inside `Flow.Process(...)`. By default the
  limit, `MaxOutstandingElementCount = BufferSize × NoOfPerformers`, is 1
  (`GcpPubSubConsumerFactory.cs:96-99`, `:129-131`). No other message reaches a handler, including a
  redelivery of the nacked message itself. **The whole subscription stalls.**
- **It keeps extending the lease.** It sends `ModifyAckDeadline` with the client's own lease (60 s
  default, a 15 s extension window), up to `MaxTotalAckExtension` (60 min default). The server
  therefore has nothing to redeliver.

It clears only when the consumer stops. Since 0023, stop nacks the held message.

The comment at `:68-69` / `:74`, "not acknowledging is sufficient for redelivery", is true for Pull and
false for Stream.

## Evidence
- [x] **Live probe** (emulator, 2026-09-30; a temporary `ZzProbe4449.cs` that was not committed and was moved
  to session scratchpad `f5edc526-…/scratchpad/`). It ran with no pump, on a raw Stream channel at the
  default BufferSize 1 / NoOfPerformers 1, and acked everything the poll received:

  | Case | Sequence | Result |
  |---|---|---|
  | A | 1 message: Receive → **Nack** → poll 60 s | **Not redelivered** in 60 s. CleanUp returned in 47 ms (0023's fix holds). |
  | B | 2 messages: Receive m1 → **Nack** → poll 45 s for both Ids | **Neither received**, not m1 again and not m2. The whole subscription stalled. |
  | C | Control: 1 message: Receive → **Requeue** (calls `Reject()`) → poll 30 s | Redelivered at **+0 ms** |
  | D | Case A with `MaxTotalAckExtension = 10 s` and `ackDeadlineSeconds = 10` | **Not redelivered** in 75 s. CleanUp **HUNG > 20 s** (see Scope Notes 6). |

  - A against C isolates the no-op. The same handle lookup and a `Reject()` redeliver at once.
  - B proves the stall affects the whole subscription, not just one delayed message.
  - D shows that capping the lease does not free the flow-control slot, because the handler is still
    parked. So even if the server redelivered, the client could not hand the message to a handler.
    The slot, not only the lease, is the blocker.
  - A with a 60 s poll refutes H5: the message was not redelivered at either the 30 s subscription
    deadline or the 60 s client lease.
- [x] **Code trace** (Opus sub-agent, checked against the source):
  1. `Reactor.cs:269` / `:328` → `Channel.cs:104` → `Nack` `:72-75`, which does nothing. The async twin is
     `Proactor.cs:307` / `:362` → `ChannelAsync.cs:107` → `NackAsync` `:83-86`. These are the only Nack
     callers in `src/`.
  2. The handle is present and live, which refutes H2. `Parser.cs:79` stores the same `GcpStreamMessage`
     in `Bag["ReceiptHandle"]`, and only `GcpRejectionRouter.cs:220` removes it, on the Reject path.
     `Requeue` `:269` does the same lookup, and case C shows it works.
  3. Library evidence (3.36.0 XML docs and IL):
     - `SingleChannel.ProcessPullMessagesAsync` wraps the handler in `Flow.Process` (the
       `FlowControlSettings` doc: "limited … at the level of the whole SubscriberClient").
     - Lease extension is `ModifyAckDeadlineAsync(…, AckDeadlineSeconds)`; `DefaultMaxTotalAckExtension` is 60 min.
     - `Reply.Nack` puts the ack id on `_nackQueue` and sends `ModifyAckDeadlineAsync(sub, nacks, 0, …)`,
       an immediate modack 0, and it releases the Flow slot.
  4. H3 is refuted for the test setup. Brighter sets a subscription `RetryPolicy` only when
     `RequeueDelay != 0` (`GcpPubSubMessageGateway.cs:375-383`), and the providers set none.
  5. H4 is refuted. `DontAckDelay` (1 s) and the unacceptable-message count are pump-only. The probe and
     the FR-16 tests call `Channel.Nack` directly.

## Scope Notes
**Suggested-fix assessment: CONFIRMED.**
- The fix: `Nack` looks up `Bag["ReceiptHandle"] is GcpStreamMessage` and calls `Reject()`, through the
  private `Nack(GcpStreamMessage)` at `:320`. `NackAsync` delegates to it.
- It uses the same mechanism as `Requeue`, which case C proves live.
- Details for the fix:
  - **Missing handle:** return silently, as `Acknowledge` (`:45-48`) and `Requeue` (`:269-272`) do. The
    handle is missing after a Reject stripped it, or for a message from another consumer. Do not throw.
  - **Leave the handle in the bag**, for parity with Ack and Requeue. Idempotence comes from the TCS:
    every completer uses `TrySet*`, so a Nack after an Ack, Reject or Requeue is a no-op.
  - Add a `Log.*` Information line, as `Requeue` does.
  - Rewrite the XML docs (`:67-70`, `:77-81`) and the inline comment at `:74`, which describe a no-op.

**Wider scope the proven cause implies (decisions for the user):**
1. **Production impact:** today a GCP Stream `DontAckAction` at default flow control stalls the whole
   subscription, for up to 60 min per nacked message. With the fix, a handler that always throws
   `DontAckAction` becomes a redelivery loop, throttled by `DontAckDelay` (1 s) and the
   unacceptable-message limit. That matches the other transports.
2. **Ledger and generated tests:** the FR-16 cells for GCP / Stream (`conformance-status.md:947`) and
   GCP / StreamOrdering (`:948`) are `Deferred -> #4240`. Moving them to `Fixed` and regenerating
   un-skips 8 facts: `{Stream,StreamOrdering}/Generated/{Reactor,Proactor}/When_nacking_a_message_it_should_be_redelivered.cs`,
   two facts each. These are the natural regression tests. Predicted RED today: the `:80`
   `Assert.NotEqual(MT_NONE)` after 30 s, not a hang.
   **User's call (2026-09-30): the regression test is this ledger move plus regeneration**, with no
   hand-written duplicate.
3. **Pull `Nack` parity: a separate latent defect, not the same bug.** `GcpPullMessageConsumer.Nack` /
   `NackAsync` (`:84-92`, `:100-103`) are no-ops that wait out the subscription ack deadline (30 s by
   default). That does not stall, but it does not meet "available … on the next receive call"
   (`IAmAMessageConsumerSync.cs:67-74`). The fix would be cheap: `ReleaseByHandle` (modack 0) already
   exists at `:484-503`. The Pull FR-16 cells (`:945-946`) are also Deferred.
   **User's call (2026-09-30): Stream only.** Pull parity is recorded as a follow-up and not folded in.
4. **StreamOrdering:** the cause and fix are the same. The library passes the ordering key into
   `Flow.Process`, so a held message blocks its key at any flow-control size.
5. **Cosmetic:** the `RequeueComplete` (`:351`) and `RejectError` (`:334`) log templates are mislabelled
   "PullPubSubConsumer".
6. **Unexplained, found by probe case D and not investigated:** with a custom `streamingConfiguration`
   that sets `MaxTotalAckExtension = 10 s`, CleanUp (Dispose → `StopAsync(NackImmediately)`, then
   delete topic and subscription) did not return within 20 s, although 0023's fix held in case A with
   the default settings. It may be library shutdown timing with a short `maxExtensionDuration`, or a
   redelivery held behind the flow-control slot. It needs a non-default configuration. Record it only,
   or raise it separately.
7. **Still out of scope, carried from 0023:** the `s_consumers` cache, the sync `Channel` double dispose,
   the `Register` leak and `Purge`.

## Regression Test
This is a ledger move plus regeneration, per the user's call at Confirm. There is no hand-written test.
- `specs/0036-universal-transport-conformance-tests/conformance-status.md:947` (GCP / Stream) and `:948`
  (GCP / StreamOrdering): the FR-16 column changes from `Deferred -> #4240 (sign-off: @iancooper)` to
  `Fixed (#4449)`. Only those two cells change.
- The generator is rebuilt and only `Paramore.Brighter.Gcp.Tests` is regenerated. The only effect is
  that the `Skip` is removed from 8 facts:
  `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/{Stream,StreamOrdering}/Generated/{Reactor,Proactor}/When_nacking_a_message_it_should_be_redelivered.cs`,
  two facts per file:
  - `When_nacking_a_message_it_should_be_redelivered[_async]`: Receive, Nack, then poll 30 s for the
    same message.
  - `When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second[_async]`: expects
    both Ids within 30 s.

**RED (2026-09-30, emulator, net10.0):** 8/8 failed on assertions and none hung. The run took 2 min 3 s,
and no stray test host was left.
- Single-message facts: `Assert.NotEqual() Failure … Expected: Not MT_NONE, Actual: MT_NONE` after
  about 30 s.
- Two-message facts: `Assert.Contains() Failure: Item not found in set` after about 30 s.

Status after this step: Tested, awaiting approval at the test-first gate.

## Fix
`src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs`: `Nack` now looks
up `Bag["ReceiptHandle"] is GcpStreamMessage` and calls the private `Nack(GcpStreamMessage)`, which calls
`Reject()`, the same path `Requeue` uses. `NackAsync` delegates to it.
- A missing handle returns silently, as in `Acknowledge` and `Requeue`.
- It logs a new Information line, `Log.NackComplete`.
- The XML docs no longer describe a no-op. A `<remarks>` explains why not acknowledging is not enough on
  a stream.
- It is a behavioural change with no structural tidy.
- Out of scope, as recorded in Scope Notes: Pull `Nack` parity (user's call), the mislabelled
  "PullPubSubConsumer" log templates, and probe case D.

**GREEN (2026-09-30, emulator, net10.0):** 8/8 regression tests passed, each in 99 ms – 1 s (RED took
about 30 s each). No stray test host.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub` builds on all target frameworks with 0 warnings
  and 0 errors.

**Verify, first attempt (2026-09-30): NOT verified. There is a StreamOrdering flake.**
- **Stream suite** `(Category=GcpPubSubStream|Category=GcpPubSubStreamOrdering)&Fragile!=CI`: **95 passed / 1 failed
  / 26 skipped** (122 in total). That is the baseline 88/34/0 plus the 8 un-skipped facts, less one
  failure. There was no other regression and no hang.
- The failure was `StreamOrdering/…/Proactor/…first_of_two…_async` at `:158`. The *other* message
  arrived, but the nacked one was not redelivered within 30 s.
- **Reruns:**
  - StreamOrdering nack facts, 4 runs: 1 failure, on the Reactor twin, with the same shape.
  - Stream nack facts without ordering, 5 runs: 20/20 passed.
- **Ordering probe** (temporary `ZzProbe4449Ordering.cs`, moved to session scratchpad `f5edc526-…/`).
  It ran 12 iterations: two messages with the same ordering key, Receive s1 → **Nack** → poll 60 s,
  acking everything:
  - **7 of 12 behaved as expected.** s2 was received at +0 to 30 ms, then s1 was redelivered at +6 to
    101 ms.
  - **5 of 12 failed the same way.** s2 was received at **+0 ms** and acked. s1 was **never
    redelivered** in 60 s. **s2 came back at about +60 s**, although it had been acked. 60 s is the
    `SubscriberClient`'s default lease.
  - Every iteration followed the same Brighter call path, `Nack` → `Reject()` → `Reply.Nack`. Only the
    broker's redelivery differed.
- **Reading, UNVERIFIED:** this is ordering-key redelivery behaviour of the emulator or the library,
  not a Brighter defect. On a Nack, real Pub/Sub with ordering is documented to redeliver the nacked
  message *and* the later same-key messages. In the failing runs the broker dropped s1's redelivery
  and treated s2's ack as lost. This cannot be settled without real GCP. The FR-2 / FR-15 requeue
  cells for StreamOrdering, which use the same `Reject()` path, are also still Deferred.
- **User's call (2026-09-30):**
  - Keep `GCP / StreamOrdering` FR-16 at `Fixed (#4449)`.
  - Record the flake in `bugfix.md` and in a dated ledger note in `conformance-status.md` (Rules, just
    before `## FR-23`), pending the non-emulator run from #4321.
  - No Fragile mechanism is needed. None exists in the generator, and CI already skips every GCP Stream
    and StreamOrdering test (`ci.yml:711` filters out both categories).

**Verify (2026-09-30, emulator, net10.0): VERIFIED, with the StreamOrdering flake recorded above.**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub` builds on all target frameworks with 0 warnings
  and 0 errors.
- Regression tests: the 4 Stream facts passed 20/20 over 5 runs. The 4 StreamOrdering facts pass,
  except that the two-message fact is intermittent, as recorded above.
- Stream suite: 95 / 1 (the recorded StreamOrdering flake) / 26, with no other regression and no hang.
- CI filter: **129 passed / 34 skipped / 50 failed**, which matches the baseline. The 50 failures are
  identical by name to 0023's list, with none in `MessagingGateway`.
- The generator's audit tests (`Paramore.Brighter.Test.Generator.Tests`) pass 307/307 against the
  edited ledger.
- No stray test host.
