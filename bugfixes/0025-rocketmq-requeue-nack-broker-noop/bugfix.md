# Bugfix: RocketMQ Requeue and Nack do not act on the broker, so redelivery waits for the lease and Requeue's delay is ignored

**Linked Issue**: #4353
**Status**: Verified

## Symptom
Spec 0037 fixed the budget part of #4353. FR-23 is now `Fixed (#4353)`, because `HandledCount` comes from the
broker's `DeliveryAttempt`. One part of the issue is still open: `Requeue` and `Nack` never call the broker.

**Observed:**
- `Requeue(message, delay)` returns `true` and does nothing on the broker. The message comes back only when the
  subscription's invisibility lease lapses (10 s in the conformance provider), whatever `delay` was asked for.
- `Nack` / `NackAsync` do nothing. A nacked message also comes back only when the lease lapses.
- With the default 30 s lease (`RocketMqSubscription.cs:115`), a nacked or requeued message sits idle for up to
  30 s.

**Expected:**
- `Requeue(m, TimeSpan.Zero)` redelivers almost at once. FR-15 asserts redelivery within 5 s of `Requeue`
  returning.
- `Requeue(m, d)` redelivers after about `d`, not before. FR-2 asserts that nothing arrives in a 2 s window, and
  then that the message arrives within 30 s.
- `Nack(m)` releases the message for prompt redelivery, well inside the lease.

**Reproduction:**
- Ledger: the `RocketMQ / RocketMQMessagingGateway` row
  (`specs/0036-universal-transport-conformance-tests/conformance-status.md:1222`; column header `:1200`) has FR-2
  and FR-15 as `Deferred -> #4240 (sign-off: @iancooper)`. FR-16 (nack) is `Fixed (#4240)`, but it passes only
  because the lease lapses inside the template's 30 s ceiling (`:216-218`).
- Why FR-2 and FR-15 were deferred:
  - `conformance-status.md:219-224`: FR-2's delay is never honoured, and a pass would be "by accident", so the
    do-not-chase-a-green rule applies.
  - `:225-228`: FR-15 needs redelivery within 5 s, which is impossible through a 10 s lease.
  - FR-2 is also listed as a known non-conformance in the Rules section at `:25-28`.
- Prior measurement, spec 0037 task 7.1: on broker apache/rocketmq:5.5.0 with RocketMQ.Client 5.2.1,
  `SimpleConsumer.ChangeInvisibleDuration(view, TimeSpan.Zero)` redelivered in 2.9 s and 2.3 s instead of
  12-13 s, and `DeliveryAttempt` still went from 1 to 2.
  - Sources: `docs/adr/0077-delivery-count-contract.md:299-303` and `conformance-status.md:739-744`.
  - The probe is Skip-marked:
    `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor/RocketMqDeliveryAttemptMeasurementTests.cs:141-187`.

## Suspected Location
**Consumer (primary):**
- `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMessageConsumer.cs:179-189`, `Requeue`. It gets the
  `MessageView` from `Header.Bag["ReceiptHandle"]` and returns `true`. The broker call is commented out at
  `:186-187`, and the `delay` argument is never used.
- `RocketMessageConsumer.cs:192-193`, `RequeueAsync`. It just wraps the sync `Requeue`
  (`Task.FromResult(Requeue(...))`).
- `RocketMessageConsumer.cs:101-104` (`Nack`) and `:107-111` (`NackAsync`). Both are explicit no-ops.
- `RocketMessageConsumer.cs:26`. The `consumer` field is a `SimpleConsumer` (`Org.Apache.Rocketmq`).
- `RocketMessageConsumer.cs:337`, `header.Bag["ReceiptHandle"] = message`. The bag entry is the raw
  `MessageView`, not a handle string.
- Existing broker calls on the view:
  - `consumer.Ack(view)` at `:56` (`AcknowledgeAsync`) and at `:204` (`AckSourceMessageSafeAsync`).
  - `:133`, in `RejectAsync`.
  - The sync wrapper pattern, `BrighterAsyncContext.Run`, is at `:45-46`.
- `RocketMessageConsumer.cs:344`, `DeliveryCount.Resolve(header.HandledCount, message.DeliveryAttempt, header.Bag)`.
  This is the FR-23 path. It reads the broker counter on receive and does not depend on `Requeue`.

**Client API: RocketMQ.Client 5.2.1.** The package ships no XML doc, so these were checked with `ikdasm` against
`lib/net8.0/rocketmq-client-csharp.dll`.
- `public Task SimpleConsumer.ChangeInvisibleDuration(MessageView messageView, TimeSpan invisibleDuration)`. It
  returns a plain `Task`, so the caller never sees a new receipt handle.
- `MessageView.ReceiptHandle` is an internal readonly field, set only in the `MessageView` constructor.
- `SimpleConsumer` never reads `ChangeInvisibleDurationResponse.ReceiptHandle`. After the call, the view still
  holds the old handle.

**Pump: it applies no delay of its own.**
- Reactor:
  - `src/Paramore.Brighter.ServiceActivator/Reactor.cs:492-514` (`RequeueMessage`), which calls
    `Channel.Requeue(message, delay ?? RequeueDelay)` at `:513`.
  - `Requeue` callers: `:259` and `:320`. If `Requeue` returns `true`, the pump does `continue` and never touches
    the message again.
  - `Nack` callers: `:269` and `:328`.
- Proactor: `src/Paramore.Brighter.ServiceActivator/Proactor.cs:498-522`, with callers at `:297` and `:354`, and
  `NackAsync` at `:307` and `:362`.
- Channels pass the call straight through: `src/Paramore.Brighter/Channel.cs:102-104` and `:174-176`, and
  `src/Paramore.Brighter/ChannelAsync.cs:105-107` and `:181-183`.
- `Subscription.RequeueDelay` defaults to `TimeSpan.Zero` (`src/Paramore.Brighter/Subscription.cs:229-230`).

**Test harness. This affects the regression test.**
- Templates in `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/`:
  - FR-2, `When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay.cs.liquid`.
    - `Requeue(received, 5 s)` is at `:61`.
    - A 2000 ms receive before the delay is due expects `MT_NONE` (`:68-69`).
    - Then there is a 30 s poll (`:75`).
  - FR-15, `When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs.liquid`.
    - The stopwatch starts after `Requeue` returns (`:68`).
    - It asserts redelivery in under 5 s (`:84-85`).
  - FR-16, `When_nacking_a_message_it_should_be_redelivered.cs.liquid`. It has two facts with 30 s ceilings
    (`:71`, `:140`) and no bound that requires redelivery inside the lease.
- Provider, `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/RocketMqMessageGatewayProvider.cs`:
  - The lease is `s_invisibilityTimeout = 10 s` (`:137`). The poll is 2 s by default (`:142`), and 15 s for topic
    names containing `exhaust` or `rq_delay` (`:150-158`).
  - FR-15 topics are mapped: `gen_r_rq_zero` and `gen_p_rq_zero` (`:64`, `:89`). Both are NORMAL in
    `docker-compose-rocketmq.yaml` (`:137`, `:158`).
  - **FR-2 is not in `s_topicMap`.** The `..._with_delay_should_receive_message_again` entries (`:63`, `:88`) are
    the FR-9 sibling. FR-2 therefore falls through to `gen_nonexistent_<guid>` (`:114-115`).
  - The compose file pre-creates `gen_r_requeue_delay` and `gen_p_requeue_delay`, but as `message.type=DELAY`
    (`:107`, `:118`), and nothing maps to them. `GetTopicType` treats any name containing
    `requeue_delay`/`rq_delay`/`delayed_msg` as Delay (`RocketMqMessageGatewayProvider.cs:370`). FR-2 sends a plain
    message.
  - The comment at `:133-136` describes Requeue and Nack as no-ops.
- Generated tests take their Skip text from the ledger. The current Skips are in
  `MessagingGateway/Generated/{Reactor,Proactor}/When_requeuing_a_failed_message_with_{delay_should_redeliver_after_delay,zero_delay_should_redeliver_immediately}*.cs:40`.

**Docs and comments that state the no-op as fact.** These will need updating with the fix.
- `docs/adr/0042-rocketmq-dlq-brighter-managed.md:36`.
- `docs/adr/0077-delivery-count-contract.md:279-282`.
- Comments in spec 0037's tests:
  - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor/When_a_rocketmq_message_is_redelivered_should_present_increasing_delivery_count.cs:47-48`
    and `:120-122`.
  - `.../When_rocketmq_budget_is_minus_one_should_never_reject.cs:48-49` and `:119-124`.

## Root-Cause Hypothesis
`RocketMessageConsumer.Requeue` (`:179-189`) and `Nack` / `NackAsync` (`:101-111`) never call the broker.
`Requeue` was left that way on purpose, behind the comment "Waiting for next RocketMQ C# version". The pump passes
the delay through the channel to the consumer (`Reactor.cs:513`, `Proactor.cs:522`) and adds no delay of its own.
So a requeued or nacked message can come back only when the receive-time invisibility lease lapses. Task 7.1 showed
that the upstream blocker named in the comment is gone on client 5.2.1.

Suggested fixes, all **UNVERIFIED — to be proven or refuted in /bugfix:confirm**:
- `Requeue` / `RequeueAsync` call `consumer.ChangeInvisibleDuration(view, delay ?? TimeSpan.Zero)`, so the delay
  becomes the new invisibility window.
- `Nack` / `NackAsync` call `consumer.ChangeInvisibleDuration(view, TimeSpan.Zero)`. Open PR #4263 (Rafael Lillo,
  branch `update.rocketmq`, now conflicting) makes this change for `Nack`.

Falsifiable predictions for /bugfix:confirm:
1. **FR-15 today.** With the Skip removed, `Requeue(m, TimeSpan.Zero)` on `gen_r_rq_zero` redelivers only after
   about 10 s (the lease), so the `< 5 s` assertion fails. With `ChangeInvisibleDuration(view, 0)` it redelivers in
   about 2-3 s.
2. **Nack today.** After `Nack`, redelivery takes about 10 s or more (the lease). With
   `ChangeInvisibleDuration(view, 0)` it is well under 10 s.
3. **Non-zero delays are honoured.** With `ChangeInvisibleDuration(view, 5 s)`, nothing is redelivered within 2 s,
   and the message comes back at about 5 s. That is shorter than the 10 s receive lease, so it shortens the lease
   rather than adding to it.
   - This is untested: 7.1 measured only the zero case.
   - The provider comment (`:134`) claims a "10 s minimum invisibility". Check whether that minimum applies to
     `ChangeInvisibleDuration` (rejected or clamped, given the client's `ILLEGAL_INVISIBLE_TIME` status) or only to
     `Receive`.
4. **Maximum duration.** Check whether a large `delay` is rejected (`ILLEGAL_INVISIBLE_TIME` or similar), and at
   what limit. A rejection would surface as an exception from `Requeue`.
5. **Receipt handle.** After `ChangeInvisibleDuration`, the same `MessageView` still holds the old handle.
   - Prediction: a later `Ack`, `Reject` or `ChangeInvisibleDuration` on that view fails, or the broker ignores it.
   - The pump never touches a message after a successful `Requeue`, and a redelivered message brings a fresh view.
     So this should not matter in the pump path, but confirm that nothing (generated tests included) acks or
     rejects a message after requeueing that same instance.
6. **`DeliveryAttempt` still advances** after `ChangeInvisibleDuration(view, d)` for non-zero `d`, as 7.1 showed
   for `d = 0`. If so, FR-23 (`Fixed (#4353)`) and `DeliveryCount.Resolve` are unaffected, and FR-23 runs faster.
7. **Nothing relies on the no-op.** Spec 0037's RocketMQ budget tests state lease-lapse timing only in comments.
   Check their assertions for an upper bound on dispatch count, or a lower bound on elapsed time, that faster
   redelivery could break.
8. **FR-2 harness gap.**
   - Un-skipped as things stand, FR-2 fails on topic resolution (`gen_nonexistent_*`), not on behaviour.
   - It needs a NORMAL topic whose name avoids the `rq_delay`/`exhaust` 15 s-poll substrings (`:155`) and the
     `requeue_delay`/`rq_delay`/`delayed_msg` Delay-type rule (`:370`).
   - **Once mapped, FR-2 is predicted to PASS before the fix.** The ledger says it can pass "by accident", because
     the ~10 s lease redelivery falls inside its 2 s–30 s window.
   - So FR-2 cannot be the failing test. The Requeue RED must come from FR-15 (prediction 1), or from a
     delay-honouring assertion that the lease alone cannot satisfy. One example is a delay longer than the lease,
     with nothing arriving before it is due.
   - Confirm should also check whether the 5.5.0 broker's message-type check rejects a plain send to the
     DELAY-typed `gen_*_requeue_delay` topics.

## Confirmed Root Cause
**CONFIRMED (2026-10-02).** On RocketMQ, `Requeue`, `RequeueAsync`, `Nack` and `NackAsync` never call the broker.

- `RocketMessageConsumer.Requeue` (`:179-189`) checks that the bag holds a `MessageView`, then returns `true`.
  - The broker call is commented out at `:186-187`.
  - `delay` is never read.
  - `RequeueAsync` (`:192-193`) wraps that sync no-op.
- `Nack` (`:101-104`) and `NackAsync` (`:107-111`) are empty.
- The invisibility lease is set once, at receive, by `consumer.Receive(bufferSize, invisibilityTimeout)` (`:85`).
  The value comes from `RocketMqSubscription.InvisibilityTimeout` (default 30 s, `RocketMqSubscription.cs:115`,
  passed in by `RocketMessageConsumerFactory.cs:43-44`). Nothing changes the lease after that.
- The pump passes `delay ?? RequeueDelay` straight through and never applies a delay itself.

So a requeued or nacked message can come back only when the lease lapses, and any requested delay is ignored. The
upstream blocker named in the code comment is gone: `SimpleConsumer.ChangeInvisibleDuration` works on
RocketMQ.Client 5.2.1 against broker 5.5.0 (probes below).

## Evidence
- [x] **Code-trace** (Plan sub-agent, verified by the main agent where it shapes the fix):
  1. **Receive.** `ReceiveAsync` → `consumer.Receive(bufferSize, invisibilityTimeout)` (`RocketMessageConsumer.cs:85`).
     - The per-call `timeOut` is ignored. The long-poll is `SetAwaitDuration(ReceiveMessageTimeout)`
       (`RocketMessageConsumerFactory.cs:33`).
     - The bag holds the raw `MessageView` (`:337`), and `HandledCount` comes from `DeliveryAttempt` (`:344`).
  2. **Pump.**
     - A defer calls `RequeueMessage` (`Reactor.cs:259`, `:320`; `Proactor.cs:297`, `:354`).
     - A don't-ack calls `Channel.Nack`, then sleeps and continues (`Reactor.cs:269`, `:328`; `Proactor.cs:307`,
       `:362`).
     - `RequeueMessage` passes `delay ?? RequeueDelay` (`Reactor.cs:513`, `Proactor.cs:522`).
     - `RequeueDelay` is never null: `Subscription.cs:229-230` sets `??= TimeSpan.Zero`.
  3. **Channel.** Pure pass-through (`Channel.cs:102-104`, `:174-176`; `ChannelAsync.cs:105-107`, `:181-183`).
  4. **Consumer.** No RPC (`RocketMessageConsumer.cs:101-111`, `:186-188`).
  5. **After `true`.** The pump does `continue` and never touches that instance again. The receive lease is still
     held, so lease expiry is the only route back.
  6. **Client API** (IL of the 5.2.1 `rocketmq-client-csharp.dll`):
     - `Task ChangeInvisibleDuration(MessageView, TimeSpan)` does no client-side range check.
     - Any non-OK broker status throws through `StatusChecker.Check`.
     - The new receipt handle in the response is thrown away. `MessageView.ReceiptHandle` is internal and readonly.
  7. **Pump failure paths** (verified):
     - If `Requeue` returns `false`, the pump falls through to `AcknowledgeMessage(message)` (`Reactor.cs:367`,
       `Proactor.cs:402`), so the message is lost.
     - An exception thrown from `Requeue`/`Nack` inside the `catch (DeferMessageAction)` / `catch (DontAckAction)`
       handlers (`Reactor.cs:316-331`) escapes. The sibling `catch (Exception)` at `:356` does not catch it, and the
       outer `try` (`:98`) has only a `finally` (`:373`). So the pump dies.
- [x] **Empirical probes** (temporary probe class, run and deleted; never committed). Broker apache/rocketmq:5.5.0,
  RocketMQ.Client 5.2.1, 10 s lease, 2 s poll, topic `rmq_measure_delivery_attempt`, 2026-10-02:

  | Probe | Result |
  |---|---|
  | Brighter `Requeue(m, TimeSpan.Zero)` (today) | returns `true` in 0 ms; redelivered at **12.8 s** (the lease) |
  | Brighter `Requeue(m, 3 s)` (today) | redelivered at **12.9 s**: the delay is ignored |
  | Brighter `Nack(m)` (today) | redelivered at **12.9 s** (the lease) |
  | raw `ChangeInvisibleDuration(view, 1 s / 5 s / 25 s)` | redelivered at **5.7 s / 7.1 s / 27.4 s**. Durations below *and* above the lease are honoured, with about 2-5 s of overhead (poll granularity). There is no 10 s minimum on this call |
  | raw `ChangeInvisibleDuration(view, 0)` (7.1, 2026-10-01) | redelivered at 2.3-2.9 s |
  | raw `ChangeInvisibleDuration(view, 12 h)` | ok |
  | raw `ChangeInvisibleDuration(view, 24 h / 7 d)` | **throws `Org.Apache.Rocketmq.Error.BadRequestException`, response-code 40011, "the invisibleTime is too large. max is 43200000"**. The broker maximum is 12 h |
  | `DeliveryAttempt` across a `ChangeInvisibleDuration` redelivery | 1 → 2 in every probe. FR-23's broker counter is unaffected |
  | `Ack(original view)` after `ChangeInvisibleDuration(view, 20 s)` | returns **ok**, but the message is **still redelivered at 22.2 s**. An Ack with the stale receipt handle is a silent no-op |

- **Predictions:** 1, 2, 3, 4, 5, 6, 7 and 8 all held.
  - Prediction 5's stale handle is harmless today. Nothing in the pump, the Channel, the generated templates (FR-2,
    FR-15, FR-16, FR-22, FR-23) or the hand-written tests touches the same instance after a `Requeue` or `Nack`.
    FR-16's second fact acks a fresh, loop-local delivery.
  - Prediction 7: no test asserts a lower bound on time between deliveries, or relies on Requeue not redelivering.
    Spec 0037's budget and delivery-count tests use lower bounds or budget bounds only, and they hold while
    `DeliveryAttempt` advances, which it does.
  - Prediction 8: FR-2 is unmapped, and once mapped it passes on the lease. FR-15 un-skipped fails today
    (12.8 s > 5 s).

**Suggested-fix assessment:**
- **Requeue → `ChangeInvisibleDuration(view, delay ?? Zero)`: PARTIAL.** The call is right. It needs:
  - **Negative delays clamped to zero.** `Subscription.RequeueDelay` is not validated, and direct callers can pass
    anything.
  - **Delays above 12 h handled.** The broker rejects them with 40011.
  - **`RequeueAsync` made truly async.** The sync method should wrap it with `BrighterAsyncContext.Run`, as
    `Acknowledge` and `Reject` already do (`:45-46`, `:114-115`). Today the async method wraps the sync one.
  - **A failure policy.** A throw kills the pump, and a `false` loses the message (Evidence 7).
- **Nack → `ChangeInvisibleDuration(view, Zero)` (PR #4263): CONFIRMED in substance, PARTIAL on details.**
  - Zero is right. The contract says "available for reprocessing on the next receive call"
    (`IAmAMessageConsumerSync.cs:68-76`), and SQS (visibility 0), RMQ (`BasicNack` requeue) and the GCP Stream
    consumer (immediate) all match it.
  - It has the same must-not-throw and async-direction needs as Requeue.

## Scope Notes
**Fix design decided by the user at the Confirm gate (2026-10-02):**
- **Failure policy (Requeue and Nack).** Catch, log a warning, and return `true` (Requeue). The receive lease
  still stands, so the message comes back when it lapses, which is today's behaviour, and the safety net already
  described on `AckSourceMessageSafeAsync` (`:195-212`). Keep `return false` only for the missing-`MessageView` case
  (`:181-184`).
- **Delays above the 12 h broker maximum.** Clamp to 12 h and log a warning that names the requested delay. If the
  call failed instead, the message would come back on the receive lease, which is much earlier than asked.
- **Negative delays.** Clamp to `TimeSpan.Zero`.

**Regression tests (for /bugfix:test):**
- **Requeue, zero delay.** Move the RocketMQ FR-15 ledger cell from `Deferred` to `Fixed (#4353)` and regenerate.
  The 2 facts un-skip and fail today (12.8 s > 5 s). This is the RED.
- **Requeue, non-zero delay.** No template proves the delay is honoured. FR-2's 5 s case passes on the lease. A
  hand-written test needs a delay longer than the lease, e.g. `Requeue(m, 20 s)` with a 10 s lease:
  - assert nothing arrives by about 15 s;
  - then assert it arrives.
  - Today it redelivers at about 13 s, so it fails.
- **Nack.** A hand-written test with a 10 s lease that asserts redelivery in under about 6 s. Today: 12.9 s.
- **FR-2.** Map FR-2 to a NORMAL, pre-created topic whose name avoids `rq_delay`, `requeue_delay`, `exhaust` and
  `delayed_msg` (`RocketMqMessageGatewayProvider.cs:155`, `:370`). Move the FR-2 cell to `Fixed (#4353)`. It is
  green on arrival, so record it honestly as not a RED.

**Sites that state the no-op as fact** (update with the fix):
- Code and test comments:
  - `RocketMqMessageGatewayProvider.cs:133-136` and `:144-150`.
  - Spec 0037's RocketMQ test doc comments, Reactor and Proactor twins:
    - `When_a_rocketmq_message_is_redelivered…`, `:47-48`, `:119-121`.
    - `When_rocketmq_budget_is_minus_one…`, `:48-49`, `:119-120`.
    - `When_rocketmq_budget_is_one_zero_or_below…`, `:50-55`, `:160-161`.
    - `When_rocketmq_budget_is_exhausted…`, `:54-55`.
    - `RocketMqDeliveryAttemptMeasurementTests.cs:67`.
- Docs:
  - `docs/adr/0042-rocketmq-dlq-brighter-managed.md:36`.
  - `docs/adr/0077-delivery-count-contract.md:279`, `:282` ("Requeue stays a broker no-op") and `:299-303`: a dated
    note, not a rewrite.
- Ledger:
  - `conformance-status.md:25-28` (Rules).
  - `:216-228` (the RocketMQ FR-16/FR-22 lease notes and the FR-2/FR-15 deferrals).
  - Row `:1222`.
- Historical specs and reviews (spec 0036/0037 requirements, decision logs, reviews) stay as written.

**Out of scope, recorded:**
- Stale entries in the provider's topic map: `…with_delay_should_receive_message_again[_async]` →
  `gen_*_rq_delay_again` (`:63`, `:88`) match no test. The DELAY-typed `gen_*_requeue_delay` topics (compose `:107`,
  `:118`) are unused.
- Pre-existing pump behaviour: a `false` from `Requeue` acks, and so drops, the message on every transport
  (`Reactor.cs:367`, `Proactor.cs:402`).
- Parity, mention only:
  - GCP's four consumers ignore the requeue `delay` (ledger Rules `:25-27`).
  - `GcpPullMessageConsumer.Nack` is a no-op (`:89-103`), as is `MsSqlMessageConsumer.Nack` (`:80-82`).
- A stale-handle Ack is silently ignored by the broker. Harmless today (prediction 5), but anything added later that
  acks after a `ChangeInvisibleDuration` would silently fail.
- Credit: PR #4263 (Rafael Lillo) proposed the `Nack` change. Credit him as co-author. Any comment on #4263 waits
  for the user's OK.

## Regression Test
Three behaviours, six facts. All were RED for the right reason on 2026-10-03 (broker 5.5.0, client 5.2.1), and the
user approved each set at the `/test-first` gate.

1. **Requeue with zero delay (FR-15). Ledger move plus regeneration; no hand-written test.**
   - `specs/0036-universal-transport-conformance-tests/conformance-status.md`: the `RocketMQ / RocketMQMessagingGateway`
     FR-15 cell changes from `Deferred -> #4240 (sign-off: @iancooper)` to `Fixed (#4353)`. Only that cell changes.
   - The generator was rebuilt, and only `Paramore.Brighter.RocketMQ.Tests` was regenerated. The only effect is that
     `Skip` is removed from
     `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/{Reactor,Proactor}/When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs`.
   - RED: `Expected redelivery within 5 s of Requeue(M, TimeSpan.Zero) returning; elapsed: 00:00:12.08` (Reactor) and
     `… RequeueAsync … 00:00:13.10` (Proactor).
2. **Requeue with a delay longer than the lease. Hand-written.**
   - Files:
     - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor/When_a_rocketmq_message_is_requeued_with_a_delay_longer_than_the_lease_should_not_redeliver_before_the_delay.cs`
       (`RocketMqRequeueDelayBeyondLeaseTests`, topic `rmq_requeue_beyond_lease_r`).
     - The `Proactor/…_async.cs` twin (`RocketMqRequeueDelayBeyondLeaseTestsAsync`, topic `rmq_requeue_beyond_lease_p`).
   - The test uses a 10 s lease and a 20 s delay. It asserts that `Requeue` returns `true`, that nothing arrives in a
     15 s quiet window, and that the message arrives within the next 30 s.
   - RED: `Requeued with a 20 s delay but redelivered after 12.1 s; the 10 s lease, not the delay, decided it` (both).
3. **Nack inside the lease. Hand-written.**
   - Files:
     - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor/When_a_rocketmq_message_is_nacked_should_redeliver_inside_the_lease.cs`
       (`RocketMqNackRedeliveryTests`, topic `rmq_nack_r`).
     - The `Proactor/…_async.cs` twin (`RocketMqNackRedeliveryTestsAsync`, topic `rmq_nack_p`).
   - The test uses a 10 s lease and asserts that the message is redelivered in under 6 s of `Nack`.
   - RED: `Nacked message redelivered after 12.0 s` (Reactor) and `12.6 s` (Proactor), against a limit of 6 s.

**Harness:** `docker-compose-rocketmq.yaml` creates the four new topics as NORMAL, in the "hand-written test topics"
block. They were also created on the running stack with `mqadmin updateTopic`.

**Not a RED: FR-2.** With the lease alone it passes, so it is left to `/bugfix:fix`. That step maps FR-2 to a NORMAL
topic, moves its cell, and records it honestly as green on arrival (see Scope Notes).

## Fix
**One-line summary:** RocketMQ `Requeue` sets the message's invisible duration to the requeue delay, and `Nack` sets
it to zero. The delay is clamped to the broker's range (0 to 12 h). A failed broker call is logged and falls back
to the receive lease.

**Added at the fix step: three more regression pairs (2026-10-03, user's call).** The first six facts did not
exercise the clamps or the failure path, so these were added with `/test-first`. Each pair was approved at its
gate:
- `…/Reactor/When_a_rocketmq_message_is_requeued_with_a_delay_above_the_broker_maximum_should_hold_it_for_the_maximum.cs`
  and its `_async` Proactor twin (topics `rmq_requeue_over_max_{r,p}`).
  - It uses a 13 h delay over a 10 s lease, and asserts that `Requeue` returns `true` and that nothing arrives in
    15 s.
  - RED before the fix: redelivered at 12.0 s.
- `…/Reactor/When_a_rocketmq_message_is_requeued_with_a_negative_delay_should_redeliver_as_if_zero.cs` and its twin
  (topics `rmq_requeue_negative_{r,p}`).
  - It uses a −5 s delay, and asserts that `Requeue` returns `true` and redelivery in under 6 s.
  - RED before the fix: 12.0 s.
- `…/Reactor/When_the_rocketmq_broker_call_behind_requeue_fails_should_return_true_and_leave_the_lease.cs` and its
  twin (topics `rmq_broker_call_fails_{r,p}`). Two facts each:
  - after the consumer is disposed, `Requeue` returns `true` and does not throw;
  - after the consumer is disposed, `Nack` does not throw.
  - These **pass on arrival** against the old no-op, which cannot throw. They went RED mid-fix (step 1 below)
    and GREEN once the catch was added.

**Built in three steps, each against the full set of 14 bug-0025 facts:**
1. **`ChangeInvisibleDuration` only.** Async-first; no clamp and no catch.
   - 6 pass: FR-15, delay longer than the lease, and Nack.
   - 8 fail:
     - The 4 failure-path facts fail: the call throws.
     - Over-max ×2: `BadRequestException 40011 "the invisibleTime is too large. max is 43200000"`.
     - Negative ×2: `BadRequestException 40011 "the invisibleTime is too small. min is 0"`. So the broker rejects
       negatives, and the negative test does pin the clamp.
2. **Plus the log-and-continue catch.** The failure-path facts pass. Over-max and negative now fall back to the
   lease (13.4–13.5 s), so they still fail.
   - FR-15 failed here on `RocketMqMessageAssertion` id mismatch: the redelivered id predated the run. A stale
     message was left on `gen_*_rq_zero` by the earlier RED runs, which never ack. This is the known
     clean-store-only limitation of the generated templates, not a regression.
3. **Plus the clamps.** All 12 hand-written facts pass (12/0, 26 s).

**FR-2:** mapped to `gen_{r,p}_rq_after` (NORMAL) in the provider and in compose, and created on the running
broker. The cell moved to `Fixed (#4353)` and the project was regenerated, which un-skipped 2 facts. They passed
2/0 on the fresh topics. FR-2 passed on arrival, as recorded at Confirm.

**Files changed:**
- `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMessageConsumer.cs`:
  - `Nack` → `NackAsync` and `Requeue` → `RequeueAsync` (the sync methods now wrap the async ones through
    `BrighterAsyncContext.Run`).
  - New private helpers `InvisibleDurationFor` (the clamp) and `ChangeInvisibleDurationSafeAsync` (the catch).
  - `s_maxInvisibleDuration` = 12 h.
  - Two Warning log messages.
- Ledger, `specs/0036-…/conformance-status.md`:
  - RocketMQ FR-2 and FR-15 → `Fixed (#4353)`.
  - A dated bugfix 0025 note in Rules, before `## FR-23`.
  - The seeded-FR-2 rule and the RocketMQ FR-2/FR-15/FR-7/16/22 notes updated.
- `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/{Reactor,Proactor}/`: FR-2 and FR-15
  regenerated, with the Skip removed.
- `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/RocketMqMessageGatewayProvider.cs`: FR-2 map entries,
  and two comments corrected (code unchanged).
- 10 new hand-written test files (5 pairs), listed above and under Regression Test.
- `docker-compose-rocketmq.yaml`: 12 new NORMAL topics (`rmq_requeue_beyond_lease_*`, `rmq_nack_*`,
  `rmq_requeue_over_max_*`, `rmq_requeue_negative_*`, `rmq_broker_call_fails_*`, `gen_*_rq_after`).
- Doc comments in spec 0037's 8 RocketMQ budget/delivery-count test files: "Requeue is a broker no-op" reframed as
  historical. The assertions are unchanged.
- `docs/adr/0042-rocketmq-dlq-brighter-managed.md`: a dated note.
- `docs/adr/0077-delivery-count-contract.md`: "Follow-up (2026-10-03): bugfix 0025, #4353".

**Checks:**
- `dotnet build src/Paramore.Brighter.MessagingGateway.RocketMQ` on all TFMs: 0 warnings, 0 errors.
- Ledger audits: 42/42.
- The full RocketMQ suite on a clean store, which also validates FR-15, is left for `/bugfix:verify`.

**Verify (2026-10-03, net10.0):**
- Setup: a clean RocketMQ store (`down -v; up -d`, approved by the user). All 112/112 compose topics were created,
  with no `SubCommandException`.
- Full `Paramore.Brighter.RocketMQ.Tests`: **83 passed / 0 failed / 2 skipped**, in 6 m 10 s. That is exactly as
  predicted:
  - 67/0/6 baseline from 8.7;
  - +4 FR-2 and FR-15 facts un-skipped;
  - +12 hand-written facts;
  - the 2 skips are the 7.1 measurement facts.
- Compared by name with 8.7's run (`rocket-87.trx`):
  - All 69 earlier results are present.
  - The only changes are the 4 FR-2/FR-15 facts, from skipped to **passed**.
  - FR-23 (2/2) and spec 0037's budget and delivery-count tests all pass.
  - The suite ran about 2.5 min faster, because requeued messages now come back in seconds instead of on the
    10 s lease.
- The generated suite on its own: 38/0/0.

**Credit:** PR #4263 (Rafael Lillo) proposed the `Nack` change. Add him as co-author on the commit.
