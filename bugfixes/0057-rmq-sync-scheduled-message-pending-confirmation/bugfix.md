# Bugfix: RMQ.Sync producer leaves a pending confirmation for scheduler-delayed messages it never publishes

**Linked Issue**: #4579
**Status**: Verified

## Symptom

`RmqMessageProducer.SendWithDelay` in RMQ.Sync can be called with `delay > 0` when `DelaySupported` is false and a `Scheduler` is set. In that case it hands the message to the scheduler and publishes nothing. It still records a pending publisher confirmation under the channel's current `NextPublishSeqNo` (call it N). Nothing went onto the channel, so the client does not advance N. The issue reports these consequences:

- The next real publish on the same producer instance gets tag N too.
- Its `TryAdd(N, …)` returns false, and the result is ignored.
- The broker's ack for N removes the *scheduled* message's entry, so `OnMessagePublished` reports success for the scheduled message's id.
- The real message's id is never confirmed, so a subscribing outbox never marks it dispatched and a sweeper would resend it.

Expected behaviour: a message that is only handed to a scheduler should record no confirmation, so its `OnMessagePublished` should not fire from this send. When the scheduler fires, the message is posted again and gets its own confirmation then. That matches RMQ.Async.

Verified on master at e65e47b27 (after PR #4593 / bugfix 0056).

## Suspected Location

**The bug site:** `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs`
- `:182-184`: `pendingDeliveryTag = Channel!.NextPublishSeqNo;`, then `ClearPendingConfirmationsIfTagsRestarted(...)`, then `_pendingConfirmations.TryAdd(pendingDeliveryTag.Value, new PendingConfirmation(message.Id, …))`. This all runs *before* the branch, and the `TryAdd` result is ignored.
- `:186-190`: the publish branch (`delay == TimeSpan.Zero || DelaySupported || Scheduler == null`). It is the only branch that publishes and the only one that sets `_lastPublishedDeliveryTag` (`:189`).
- `:191-200`: the scheduler branches (async `:191-195`, sync `:196-200`). They publish nothing, and the entry added at `:184` stays.
- `:202-203`: `pendingDeliveryTag = null` runs after *every* branch. So the `finally` at `:219-223` removes the entry only if an exception is thrown. A scheduler send that succeeds leaves it in place.
- `:332-352`: `SettleConfirmations` / `ConfirmedDeliveryTags`. Whichever entry is under a tag gets settled by that tag's ack. With `multiple`, every key `<= deliveryTag` is settled, so an orphan at N is also settled by a coalesced ack.
- `:312-317`: `ClearPendingConfirmationsIfTagsRestarted`.

**How RMQ.Async does it:** `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs`
- `:200-208`: inside `if (PublishesOnChannel(delay.Value))`, it gets the sequence number (`:203`), calls `AddPendingConfirmation` (`:204`), sets `pendingDeliveryTag` and publishes.
- `:209-218`: the scheduler branches record nothing.
- `:462`: `PublishesOnChannel` is the same predicate as Sync `:186`.

**Callers and the scheduler round trip**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs:372-381`: requeue with a delay and no native delay calls `_requeueProducer!.SendWithDelay(message, timeout)`.
- `RmqMessageConsumer.cs:402-409`: `_requeueProducer` is a *separate* `new RmqMessageProducer(Connection) { Scheduler = _scheduler }`. Nothing subscribes to its `OnMessagePublished`; the only references to `_requeueProducer` are `:61`, `:380`, `:405-406` and `:640`.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:813-815`: every producer in the registry gets `Scheduler ??= messageSchedulerFactory.Create(command)`. So registry producers have a scheduler too and can reach `:191-200` if someone calls `SendWithDelay` on them directly.
- `src/Paramore.Brighter/OutboxProducerMediator.cs:1114-1120`: the mediator only calls `producerSync.Send(message)`. That is a zero delay, so it always takes the publish branch. A search of `src` for `.SendWithDelay(` finds only consumer requeue paths (RMQ.Sync `:380`, Redis, Kafka, MsSql, MQTT, InMemory).
- `OutboxProducerMediator.cs:1011-1036`: the confirmation subscriber. On success it calls `MarkDispatched(result.MessageId, …)`; on failure it calls `TripTopic(result.Topic)`. Both act on whatever id/topic the pending entry carries.
- When the scheduler fires: `src/Paramore.Brighter/InMemoryScheduler.cs:74-84` wraps the message in `FireSchedulerMessage { Async = false }`; `:300-315` `Execute` calls `processor.SendAsync(FireSchedulerMessage)`; `src/Paramore.Brighter/Scheduler/Handlers/FireSchedulerMessageHandler.cs:15-22` calls `processor.Post(command)`; `OutboxProducerMediator.cs:543-546` unwraps the raw message; it is then dispatched through the **producer registry's** producer for that topic via `Send` (`:1114-1120`). That producer is not necessarily the instance that scheduled it.

**The `useSchedulerAsync` flag**
- In Sync, both callers pass false: `RmqMessageProducer.cs:153` passes `false` explicitly, and `SendWithDelayAsync` (`:238-241`) calls the public `SendWithDelay(message, delay)`, which also passes `false`. So `:191-195` is unreachable in Sync, and the async API uses the sync scheduler cast at `:198`.
- In Async, the public `SendWithDelayAsync` passes `true` (`Async/RmqMessageProducer.cs:161-162`).

## Root-Cause Hypothesis

In RMQ.Sync, a send that only schedules leaves an entry in `_pendingConfirmations` for a delivery tag it never used. This is because the entry is recorded at `:184`, before the publish/schedule branch (`:186-200`), and `:203` treats a scheduled send as "sent" just like a published one.

What should happen, to be checked in confirm:

1. **Scheduled send, then a real publish on the same instance.**
   - The scheduled send at tag N adds `{N → scheduledId}`. `_lastPublishedDeliveryTag` stays at N-1, so `ClearPendingConfirmationsIfTagsRestarted(N)` does nothing (`N > N-1`, `:314`).
   - The next `Send` also sees `NextPublishSeqNo == N`. The restart check still does nothing, `TryAdd(N, realId)` fails silently, the message is published and `_last = N`.
   - Ack N then raises `OnMessagePublished(Success, scheduledId)`, and `realId` is never confirmed.
   - Falsifiable by a test with a channel that does not advance `NextPublishSeqNo` on a scheduled send.
2. **Interaction with `ClearPendingConfirmationsIfTagsRestarted`.** A scheduled send never moves `_lastPublishedDeliveryTag` (`:189` is only on the publish branch). It therefore cannot make a later `Send` hit the clear path at `:316`, and cannot wipe valid entries. Expected: no extra harm from this interaction, and it does not hide the bug.
3. **What happens when the scheduler fires.** The fired message comes back through `CommandProcessor.Post` and the registry producer's `Send`, which records a new entry under a real tag.
   - If the scheduling instance *is* that registry producer (a direct `SendWithDelay` on a registry producer) and something was published in between, the scheduled id was already "confirmed" by the other message's ack, and is confirmed a second time, correctly, when the fired copy is acked. The outbox sees two confirmations for it and none for the message published in between.
   - If nothing was published in between, the orphan `{N → scheduledId}` is still there, the fired `Send` gets tag N, and `TryAdd` fails, but the entry has the same id. The confirmation is then correct by coincidence.
4. **Reach in production.**
   - The only production caller of the Sync scheduler branch is the consumer's private `_requeueProducer` (`RmqMessageConsumer.cs:380, 406`). Nothing subscribes to its confirmations, and it only ever takes the scheduler branch, so its `NextPublishSeqNo` never moves.
   - On that path the wrong id is never seen by an outbox. What remains is a single stale entry, because later scheduled sends collide on the same key and `TryAdd` fails.
   - The outbox-visible failure needs a direct `SendWithDelay(delay > 0)` on a registry producer that has a scheduler (`ServiceCollectionExtensions.cs:815`) and a confirmation subscriber (`OutboxProducerMediator.cs:1011`), followed by a `Send` on the same instance.
   - **Confirm should establish this severity** rather than assume the issue's "outbox resends" outcome happens on the requeue path.
5. **Existing tests.** No test in `tests/Paramore.Brighter.RMQ.Sync.Tests` checks pending-confirmation behaviour for scheduled sends:
   - `When_rmq_sync_consumer_requeues_without_native_delay_should_use_producer.cs:63` builds its producer with no `Scheduler`, so `:186` publishes and the scheduler branch is never run (its consumer's scheduler is null, `:72-73`).
   - The confirmation tests only send with zero delay.

**Suggested fix (from the issue) — UNVERIFIED — to be proven or refuted in /bugfix:confirm:** move the `_pendingConfirmations.TryAdd(...)` (and `pendingDeliveryTag` / `ClearPendingConfirmationsIfTagsRestarted` with it) inside the publish branch at `:186-190`, as RMQ.Async does at `Async/RmqMessageProducer.cs:200-208`.

Confirm should also check:
- Whether the `pendingDeliveryTag = null` at `:203` must move inside the publish branch, so the failure cleanup in the `finally` (`:219-223`) still only covers a tag that was actually recorded.
- Whether the ignored `TryAdd` result (`:184`) should be left as it is now that there is no other way for tags to collide.
- (Out of scope, note only) Sync's async scheduler branch `:191-195` cannot be reached.

## Confirmed Root Cause
**Verdict: CONFIRMED.** Reproduced against the local broker and the real RabbitMQ.Client 6.8.1. Severity is narrower than the issue implies: see Scope Notes item 1.

All in `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs` (triage line numbers still match):
- **Recorded before the branch.** `:182-184` reads `NextPublishSeqNo`, runs the restart check and does `TryAdd(tag, …)`, all before the branch at `:186` decides whether to publish or schedule.
- **Only one branch publishes.** `:186-190` is the only path that calls `BasicPublish`, and the only one that sets `_lastPublishedDeliveryTag`.
- **The scheduler branches publish nothing** (`:191-200`).
- **The entry is never cleaned up.** `:203` sets `pendingDeliveryTag = null` after every branch, so the `finally` (`:219-223`) never removes a scheduler-path entry.

In 6.8.1 only `ModelBase.BasicPublish` increments the tag (`:1015`, `_pendingDeliveryTags.AddLast(NextPublishSeqNo++)`, immediately before `_Private_BasicPublish`; decompiled `scratchpad/rmq681/RabbitMQ.Client.Impl/ModelBase.cs:993-1018`). A scheduler send never calls it, so the next real publish reuses tag N, its `TryAdd(N, …)` fails (result ignored), and `SettleConfirmations` (`:332-346`) settles whatever entry is under N, the scheduled id.

## Evidence
- [x] **Code trace** (scheduled send S, then real sends R1 and R2, on a channel that has already published k messages, N = k+1):
  1. `SendWithDelay(S, 5s)`: tag = N; `ClearPendingConfirmationsIfTagsRestarted(N)` does nothing (N > k); `TryAdd(N, S)` succeeds; the scheduler branch runs; `pendingDeliveryTag = null`, so `{N→S}` stays; `_last` stays k; the client tag stays N.
  2. `Send(R1)`: tag = N; restart check does nothing; `TryAdd(N, R1)` returns **false** and is ignored; `BasicPublish` uses client tag N; `_last = N`.
  3. `Send(R2)`: tag N+1, recorded correctly.
  4. Ack N raises `OnMessagePublished(Success, S)`; ack N+1 raises R2. **R1 is never confirmed.** With `multiple=true`, ack(N+1) settles `[S, R2]`. A nack on N would trip the circuit breaker for S's topic rather than R1's (`OutboxProducerMediator.cs:1011-1036`).
  - The bugfix 0056 restart check neither hides nor worsens this: a scheduler send never moves `_lastPublishedDeliveryTag`, so it cannot cause a false clear. `SelectConfirmsOnChannel`'s clear (`:302`) wipes the orphan only when the channel is replaced.
- [x] **Red repro against the real broker and client** (throwaway console app in the session scratchpad, not in the repo): `new RmqMessageProducer(conn){ Scheduler = spySync }`, `SendWithDelay(S, 5s)`, `Send(R1)`, `Send(R2)`, wait for the real acks. Output: `scheduled calls: 1`, `confirmed: scheduled-S:True, real-R2:True`. R1 is missing and S is falsely confirmed.
- [x] **Scheduler-only producer (the requeue shape):** five `SendWithDelay(…, 5s)` calls on one producer gave `scheduled=5 pending=1 keys=1` (read by reflection). The client tag never moves and every later `TryAdd(1, …)` fails, so the orphan stays at one entry.

## Suggested-Fix Assessment
**CONFIRMED** (with a placement rule): move the tag read, `ClearPendingConfirmationsIfTagsRestarted` and the recording into the publish branch, as RMQ.Async does (`Async/RmqMessageProducer.cs:200-208`, predicate `PublishesOnChannel` at `:462`).
- **Record before you publish.** The entry must exist before `PublishMessage`: the frame is sent synchronously and the ack is handled on the connection thread, so it can arrive before `PublishMessage` returns. Order inside the branch: (1) `tag = Channel.NextPublishSeqNo`; (2) `ClearPendingConfirmationsIfTagsRestarted(tag)`; (3) `TryAdd`; (4) `pendingDeliveryTag = tag`; (5) `PublishMessage`; (6) `_lastPublishedDeliveryTag = tag`; (7) `pendingDeliveryTag = null`.
- **The `finally` still covers failed publishes:** a throw before `BasicPublish` (e.g. `ConfigurationException` from `CreateBasicProperties`) leaves the client tag unconsumed and the `finally` removes the entry (bugfix 0056 test 2); a throw from `_Private_BasicPublish` after the increment also has its entry removed, and the next send reads N+1. Nothing in `PublishMessage` runs after `BasicPublish`, so a message that reached the broker is never un-recorded.
- **The scheduler path records nothing**, so a scheduler that throws has nothing to clean up. Whether the trailing `pendingDeliveryTag = null` (`:203`) moves into the branch makes no behavioural difference; moving it matches Async.
- **No `OnMessagePublished` from a scheduler send.** This matches Async. The scheduled message gets its own confirmation when the scheduler fires (`InMemoryScheduler` → `FireSchedulerMessageHandler` → `Post` → the mediator's `Send`, `OutboxProducerMediator.cs:1114-1120`). No caller in `src` depends on a confirmation from `SendWithDelay`.
- **The ignored `TryAdd` result:** after the fix nothing can collide (sends are serialised by `s_lock`, and both restart paths clear first). Leave as is; switching to the indexer (Async parity, `Async/RmqMessageProducer.cs:428`) is optional hardening and not needed here.

## Scope Notes
1. **Severity and reach.** The only production path to the Sync scheduler branch is `RmqMessageConsumer.Requeue` with `timeout > 0` and `!DelaySupported` (`RmqMessageConsumer.cs:372-381`) through the private `_requeueProducer` (`:402-409`, scheduler set when it comes from `RmqMessageConsumerFactory`, `:47-50`, `:76`). Its only references are `:61`, `:380`, `:405-406` and `:640`; **nothing subscribes to its `OnMessagePublished`**, and with a scheduler it always takes the scheduler branch. Registry producers also get a scheduler (`ServiceCollectionExtensions.cs:813-815`), but the mediator only calls `Send` (zero delay) and no scheduler package calls a producer's `SendWithDelay`. **So the outbox-visible failure (real message left undispatched and resent as a duplicate, wrong id confirmed, wrong topic tripped) happens only through user code calling `SendWithDelay(delay > 0)` on a registry producer with `SupportDelay=false` and then `Send` on the same instance. It is not reachable through Brighter's own pipeline today.** The issue's "outbox resends" outcome does not occur on the requeue path.
2. **Orphan over time on the requeue-only producer:** a bounded leak of exactly one entry per channel instance (the first scheduled id). It does not grow (`TryAdd(1, …)` keeps failing), cannot be settled by a real ack (the channel never publishes), is cleared when the channel is replaced, never triggers the restart clear (`_last` stays 0), and does not affect Dispose (`WaitForConfirms` waits on the client's own countdown, which Brighter's entries are not part of).
3. **#4580 is fully covered by bugfix 0056 (PR #4593):** the `finally` removes the recorded tag for every exception type after `TryAdd`; failures before `:182` record nothing; the `IOException` catch clears everything; `When_a_send_fails_before_publishing_should_not_confirm_the_unpublished_message` covers it. #4580 can be closed with a reference to #4593.
4. **No other Sync call site records entries:** `_pendingConfirmations` is only written at `:184`. The consumer's `RequeueMessage` publishes on the consumer's own channel, which is not in confirm mode.
5. **Parity:** after the fix, Sync matches Async's structure (record only when publishing, set the pending tag, clear after the publish, remove in `finally`).
6. **Note only, out of scope — unreachable branch:** Sync's async scheduler branch `:191-195` cannot be reached (`SendWithDelayAsync` calls the public `SendWithDelay`, which passes `false`). A scheduler implementing only `IAmAMessageSchedulerAsync` would throw `InvalidCastException` at `:198`.
7. **Note only, out of scope — misleading log:** `Log.PublishedMessage` (`:205-207`) logs "Published message to exchange" even for scheduled sends.

**Recommended regression test (one is enough):** `When_a_message_is_handed_to_the_scheduler_should_not_confirm_it_for_the_next_publish`, in `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/`, `[Collection("RMQ")]`, modelled on the bugfix 0056 test 2.
- **Arrange:** `new CoalescingConfirmsRmqProducer(rmqConnection) { Scheduler = spy }`. `SupportDelay` defaults to false (`RmqMessagingGatewayConnection.cs:203-209`) and `DelaySupported` is purely config (`RmqMessageGateway.cs:94`), so the broker's plugin does not matter. `spy` is a private nested `IAmAMessageSchedulerSync` recording `Schedule(Message, TimeSpan)` and returning an id (it must be the sync interface because of the cast at `:198`; shape in `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/When_mssql_consumer_requeues_with_delay_should_use_producer.cs:83`). The existing `StubMessageScheduler` classes in RMQ.Sync tests are empty `IAmAMessageScheduler` stubs and would fail the cast. Declare a queue as the sibling test does.
- **Act:** `SendWithDelay(scheduled, TimeSpan.FromSeconds(5))`, `Send(published)`, `Confirms.RaiseAck(deliveryTag: 1, multiple: false)`.
- **Assert:** precondition `Assert.Equal([scheduled.Id], spy.Scheduled.Select(m => m.Id))` (pins that the scheduler branch ran), then `Assert.Equal([published.Id], confirmations.Select(c => c.MessageId))`. Today this fails with actual `[scheduled.Id]`.
- Optional variant: schedule S1 and S2, `Send` R1 and R2, `RaiseAck(2, multiple: true)`; expected `[R1, R2]`, today `[S1, R2]`. A consumer-level test is not worth it: the requeue producer's confirmations are invisible to everything.

## Regression Test
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_a_message_is_handed_to_the_scheduler_should_not_confirm_it_for_the_next_publish.cs`
- New test double `tests/Paramore.Brighter.RMQ.Sync.Tests/TestDoubles/SpyMessageSchedulerSync.cs` (an `IAmAMessageSchedulerSync` that records what it is given; the existing `StubMessageScheduler` classes implement only `IAmAMessageScheduler` and would fail the producer's cast).
- Flow: `SendWithDelay(scheduled, 5s)` (precondition: the scheduler received it), `Send(published)`, `RaiseAck(1, false)`. RED: the confirmation carried the scheduled message's id instead of the published one. Approved, now GREEN.

## Fix
`src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs`: the tag read, the tag-restart check and the recording moved into a new `RecordPendingConfirmation`, called only inside the publish branch, before `PublishMessage` (the broker's ack can arrive on the connection thread before it returns). `pendingDeliveryTag` is cleared inside that branch after the publish, so the `finally` from bugfix 0056 still removes a recorded-but-failed publish. The scheduler branches record nothing. No comments added in method bodies (per the updated `documentation.md`).

**Suite:** `Paramore.Brighter.RMQ.Sync.Tests` net10.0: 203 passed, 10 failed, 1 skipped. The 10 are the same environmental failures as before (9 mTLS tests need `./tests/generate-test-certs.sh`; `DispatchBuilderTests` is the #4581 queue hazard).
