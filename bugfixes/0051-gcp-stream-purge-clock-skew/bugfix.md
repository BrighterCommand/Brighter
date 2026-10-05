# Bugfix: GCP stream Purge drain compares the server's PublishTime with the client's clock

**Linked Issue**: #4525
**Status**: Verified

## Symptom
- After `Purge`/`PurgeAsync` on a Stream-mode GCP channel, a message published *before* the purge can still be
  delivered.
- Repro: both Stream purge regression tests from #4508 fail locally on master (`f75a1bd02`) against the Pub/Sub
  emulator. The failure is `Assert.Equal() Failure: Values differ`: the received message is not `MT_NONE`.
  - Sync, `GcpStreamPurgeTests` (Reactor):
    `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_channel_is_purged_should_receive_no_message_published_before_the_purge.cs`
    - `bufferSize: 3` at `:56`.
    - Three `producer.Send` calls at `:71-73`.
    - `Receive` (20 s) and `Acknowledge` of the first message at `:75-76`, leaving 2 in the local buffer.
    - `channel.Purge()` at `:79`, then `Receive` (10 s) at `:80`.
    - `Assert.Equal(MessageType.MT_NONE, …)` at `:83`.
  - Async, `GcpStreamPurgeAsyncTests` (Proactor): `…/Stream/When_a_gcp_stream_channel_is_purged_should_receive_no_message_published_before_the_purge_async.cs`
    - `bufferSize: 3` at `:57`, `SendAsync` x3 at `:72-74`.
    - `ReceiveAsync`/`AcknowledgeAsync` at `:76-77`.
    - `PurgeAsync` at `:80`, `ReceiveAsync` at `:81`, assert at `:84`.
  - Helper: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpStreamMessageGatewayProvider.cs`
    (`CreateChannel` `:315`, `CreateChannelAsync` `:356`).
- Measured by the reporter:
  - The emulator's `PublishTime` is about 110 ms ahead of the host.
  - After `chronyc makestep` on the podman VM it is still about 45–53 ms ahead (the Mac is behind NTP).
  - Publish to purge takes about 100 ms or less.
  - The tests passed earlier, when the skew was smaller.
- CI runs no Stream tests, so CI does not see this.
- Expected: a message published before `Purge` is called is never delivered after it, for any reasonable
  client/server clock offset.

## Suspected Location
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs:97-116`,
  `PurgeBuffered(DateTimeOffset publishedBefore)`.
  - The cut-off test is at `:102`: `if (message.Message.PublishTime.ToDateTimeOffset() < publishedBefore)`.
    `PublishTime` is stamped by the server.
  - Messages that fail the test are put back into the channel (`:106-115`), so they will still be delivered.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs`:
  - `Purge` (`:182-205`) reads `var purgeStarted = timeProvider.GetUtcNow();` from the client clock (`:190`).
    - It Seeks to `purgeStarted.AddMinutes(1)` (`:191-195`).
    - It then calls `consumer.PurgeBuffered(purgeStarted)` (`:196`).
  - `PurgeAsync` (`:214-239`) does the same: clock read `:222`, `SeekAsync` `:223-229`, `PurgeBuffered` `:230`.
  - The Seek runs before the drain in both paths.
- Where `timeProvider` comes from:
  - It is set on the subscription: `GcpPubSubSubscription` ctor param `timeProvider` (`GcpPubSubSubscription.cs:166`),
    defaulting to `TimeProvider.System` (`:177`), exposed as `TimeProvider` (`:104`).
  - `GcpPubSubConsumerFactory` passes it on: to the stream consumer at `GcpPubSubConsumerFactory.cs:113`, and to the
    pull consumer at `:88`.
  - A test can therefore inject one through the subscription ctor.
  - The same provider also feeds `GcpRejectionRouter` (`GcpPubSubStreamMessageConsumer.cs:31-37`,
    `GcpRejectionRouter.cs:226`). That only stamps a rejection timestamp and does not affect this bug.
- Other uses of the local clock in the GcpPubSub project:
  - `GcpPullMessageConsumer.cs:120` and `:149` use `timeProvider.GetUtcNow().AddMinutes(1)` only as a Seek time.
  - There is no `PublishTime` comparison anywhere else. `GcpStreamConsumer.cs:102` is the only place a server
    timestamp is compared with a client time.
  - Pull has no local buffer drain, so Pull is not affected.

## Root-Cause Hypothesis
- **H1:** `PurgeBuffered` compares two different clocks. `PublishTime` comes from the Pub/Sub server clock;
  `purgeStarted` comes from the client `TimeProvider`.
  - If the server clock is ahead of the client by more than the publish-to-purge gap, a buffered message's
    `PublishTime` is at or after `purgeStarted`.
  - That message fails the `<` test at `GcpStreamConsumer.cs:102` and is written back to the channel (`:114`).
  - The next `Receive` then returns it.
  - In the tests, skew of about 45–110 ms is at least the gap of about 100 ms or less, so this happens.
- Why the buffered messages survive the Seek (prior art: `bugfixes/0047-gcp-purge-seek-and-stream-buffer/bugfix.md`):
  - Seek only changes the subscription's backlog on the service.
  - A buffered message has already been delivered to the streaming client. `BrighterStreamHandler.HandleMessage`
    (`GcpStreamConsumer.cs:135-151`) has written it into the local channel. The handler awaits
    `WaitForCompleteAsync()`, and the client keeps extending the message's lease.
  - Nothing in the Seek removes that local copy, so only the local drain can remove it. 0047 RED 2 confirmed this:
    with the Seek fix alone the result was `Expected: MT_NONE, Actual: MT_EVENT`.
  - The Seek's own +1 min cut-off is still a server-side cut-off, so it has no effect on the local buffer.
- Why the drain rule is "published before" rather than "drain everything" (0047 `bugfix.md:212-227`, `:244-246`,
  `:283-284`, `:302-309`):
  - The buffer is shared by every performer on the subscription, and there is no purge barrier.
  - A plain drain of everything would also Ack messages that arrived after the purge started.
  - The user chose rule (b): Ack only messages published before the purge started, and put later ones back.
  - The cut-off is deliberately the purge start, not the Seek's +1 min. Using the +1 min would Ack up to a minute of
    post-purge messages that the server itself would still deliver.
  - So the server-side purge (Seek, +1 min, judged by the server) and the local drain (purge start, judged against
    the client clock) already use different cut-offs and different clocks.
  - 0047 `bugfix.md:247` notes that the seek time reads `TimeProvider`, so a fake clock in a test also changes the
    real Seek time.
- **Falsifiable predictions:**
  - Server ahead of client by more than the gap: pre-purge messages leak (the test fails).
  - Client ahead of server by any amount: the test passes.
  - Making the client clock lag the server by a large, fixed amount should make the failure deterministic, whatever
    the real host/emulator skew is.
- **How `/bugfix:confirm` can prove it deterministically:**
  - Give the test subscription a `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) set to real UTC
    now minus a fixed lag, for example 5–10 s. Pass it through the `timeProvider` ctor parameter.
    - The package is in `Directory.Packages.props:103`, but the Gcp test project does not reference it yet.
  - Expect RED every time, on the emulator and on real Pub/Sub.
  - Control: the same test with a fake clock *ahead* of real time (for example +5 s) should go GREEN every time.
    That isolates the clock comparison.
  - **Seek side effect:** the fake time also moves the real Seek target to `fake + 1 min`.
    - With a lag under 1 min, the Seek target is still in the future, so the backlog purge still works.
    - With a lag of 1 min or more, the Seek target is in the past and stops purging the server backlog. That would
      mix in a second failure mode, so keep the lag well under 60 s.
  - Optional: log each buffered message's `PublishTime` against `purgeStarted` inside `PurgeBuffered` to show the
    measured skew directly.
- **Candidate directions (from the issue, UNVERIFIED — to be proven or refuted in /bugfix:confirm; no choice made
  here):**
  - (a) Add a tolerance margin to the drain rule.
  - (b) Use a server-derived reference time, for example the `PublishTime` of the most recently received message.
  - (c) Drain everything buffered at purge time. This reopens the concurrency and post-purge-Ack concern recorded
    in 0047.

## Confirmed Root Cause
**H1 CONFIRMED.** `GcpStreamConsumer.PurgeBuffered` compares two clocks.
- `Purge`/`PurgeAsync` take `purgeStarted = timeProvider.GetUtcNow()` from the **client** clock
  (`GcpPubSubStreamMessageConsumer.cs:190`, `:222`). They Seek to `purgeStarted + 1 min` (`:194`, `:227`), then call
  `consumer.PurgeBuffered(purgeStarted)` (`:196`, `:230`).
- `PurgeBuffered` Acks a buffered message only if its **server**-stamped `PublishTime < publishedBefore`
  (`GcpStreamConsumer.cs:102`). Every other message is written back to the channel (`:108`, `:112-115`).
- When the server clock is ahead of the client by more than the gap between publish and purge, a pre-purge message
  fails that test. It is written back, and the next receive returns it.
- At most `BufferSize * NoOfPerformers` pre-purge messages can leak per purge (`GcpPubSubConsumerFactory.cs:121-124`).

## Evidence
- [x] **Red repro: a temporary probe on the emulator** (`Probe0051Tests.cs`, never committed). It ran the stream purge
  test's scenario with the subscription's `timeProvider` shifted from real time by a fixed offset.
  - The scenario: `bufferSize` 3, publish 3 messages with distinct ids, receive and Ack 1, `Purge`, then `Receive`
    with a 10 s window.
  - **Real time − 10 s** (client lags the server): **failed 5/5**. Within about 500 ms it received a pre-purge message
    that was *not* the acknowledged first one.
  - **Real time + 10 s** (client ahead): **passed 5/5** (`MT_NONE`).
  - The unmodified tests fail on master in 2 runs of 3 with the real skew. Each failure is
    `Expected: MT_NONE, Actual: MT_EVENT`, and returns in about 1 s.
- [x] **Code trace: the offset clock changes nothing else that could explain the result.**
  - With −10 s, the Seek target is fake now + 1 min, which is real time + 50 s. That is still in the future, so the
    backlog is still cleared. The only difference between the two arms is the cut-off at `GcpStreamConsumer.cs:102`.
  - The subscription's `TimeProvider` reaches only three places, none of which affects this test:
    - the stream consumer (`GcpPubSubConsumerFactory.cs:113`), which reads it only at `:190`/`:222`;
    - `GcpRejectionRouter`, which uses it only for the reject stamp (`GcpRejectionRouter.cs:226`);
    - the Pull consumer (`GcpPubSubConsumerFactory.cs:88`).
  - No timer or `Task.Delay` uses it. The receive timeout uses `CancelAfter` on the system timer.
  - Core `Channel.Purge` only delegates to the consumer and resets its own queue (`Channel.cs:137-141`).
  - The leaked id is not the acked one, which rules out a redelivery of message 1.

## Scope Notes
1. **What Purge means today (this corrects part of 0047's rationale).**
   - **Server side:** a message published straight after the Seek still arrives (0047 `bugfix.md:241-243`). So the
     Seek to now + 1 min removes what is retained when the Seek is applied. The +1 min is a skew margin, not a minute
     of discarded future messages.
   - **Local side:** the drain sees only what is in the channel when it runs. A message published after the drain
     can never be in the buffer then.
     - So the "published before the purge started" rule protects only buffered messages published in the few
       milliseconds between `purgeStarted` and the drain.
     - 0047's concern that a +1 min cut-off would "Ack messages published up to a minute after the purge"
       (`bugfix.md:244-246`) is overstated.
     - The doc comment at `GcpStreamConsumer.cs:90-94` ("kept, as the service would still deliver them") is only
       partly true.
2. **Fix directions (from the issue). The choice is the user's; none is made here.**
   - **(a) Tolerance margin on the cut-off: PARTIAL.**
     - It fixes the bug only while skew stays under the margin.
     - A margin equal to the Seek's 1 min behaves almost exactly like (c), and is consistent with the Seek.
     - It keeps 0047's rule (b) in form.
   - **(b) Server-derived reference time: WRONG as worded.**
     - The "PublishTime of the most recently received message" is message 1's. Messages 2 and 3 are later, so they
       still leak and the repro still fails.
     - Using the maximum `PublishTime` over the buffer with `<=` is (c) in disguise.
     - `SeekResponse` carries no server timestamp, so there is no true server "now" to read.
   - **(c) Drain everything buffered at purge time: removes the cause.**
     - No clocks are compared.
     - It is closest to the Seek's semantics ("everything retained at purge time").
     - Its cost is Acking messages published after the Seek that were buffered before the drain, a window of
       milliseconds.
     - It reverses the drain rule (b) the user approved in 0047, so it needs the user's sign-off.
     - MQTT already drains its whole local channel this way (`MQTTMessageConsumer.cs:249-254`).
   - 0047's concern about concurrent receives by other performers (no purge barrier) is unchanged by (a), (b) and (c).
3. **A residual gap that does not depend on clocks (not fixed by (a), (b) or (c); out of scope unless the user wants
   it).**
   - A message leased before the Seek but not yet passed to `HandleMessage` (`GcpStreamConsumer.cs:135-151`) when the
     drain runs is not filtered when it lands. It could still be in gRPC transit, or held back by client-side flow
     control (`GcpPubSubConsumerFactory.cs:121-124`).
   - The drain's own Acks free flow-control slots, which can release held-back messages straight afterwards.
   - The test does not expose this: 3 messages fill exactly 3 slots, and the +10 s control passed 5/5.
   - Closing it would need a persistent purge watermark checked on arrival or on read.
4. **A latent two-clock issue in the Seek itself (not this bug).** Both the Pull Seek
   (`GcpPullMessageConsumer.cs:120`, `:149`) and the stream Seek (`GcpPubSubStreamMessageConsumer.cs:194`, `:227`)
   target client now + 1 min. If the client lags the server by 1 min or more, the target is in the past and the
   backlog is not purged. Skew up to 60 s is tolerated.
5. **No other site has this pattern.** `GcpStreamConsumer.cs:102` is the only `PublishTime` comparison in the
   project. StreamOrdering uses the same `GcpStreamConsumer`. No other transport's Purge compares a broker timestamp
   with a client clock.

## Regression Test
**Purging a Stream channel clears every buffered pre-purge message even when the client clock lags the server.**
Approved 2026-10-05. The user chose fix direction **(c)**: drain everything buffered at purge time.
- `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_channel_is_purged_while_the_client_clock_lags_should_receive_no_message_published_before_the_purge.cs`
  (sync, `GcpStreamPurgeClockLagTests`, Reactor)
- `…_async.cs` (async, `GcpStreamPurgeClockLagAsyncTests`, Proactor)
- The test double `tests/Paramore.Brighter.Gcp.Tests/TestDoubles/LaggingTimeProvider.cs` is a running clock that is a
  fixed 10 s behind the system clock. It is passed as the subscription's `timeProvider`.
  - 10 s is well inside the Seek's 1 min margin, so the server-side purge still works.
- The scenario is the same as 0047's Stream purge tests: `bufferSize` 3, publish 3 (a fresh id each), receive and
  Ack 1, `Purge`, `Receive` with a 10 s window, assert `MT_NONE`.
- **RED on the emulator:** 0/2 in each of 3 runs, `Expected: MT_NONE, Actual: MT_EVENT`.

## Fix
**Direction (c): drain everything buffered at purge time.** No clocks are compared, so skew cannot leak a pre-purge
message.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpStreamConsumer.cs`: `PurgeBuffered(DateTimeOffset)` becomes
  `PurgeBuffered()`. It Acks every message in the local channel and no longer puts later ones back. The XML doc says
  why: `PublishTime` comes from the service's clock.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs`: `Purge`/`PurgeAsync` call
  `PurgeBuffered()`. `purgeStarted` remains only for the Seek target (`+1 min`), which is unchanged.
- **Public API:** the signature of `GcpStreamConsumer.PurgeBuffered` changes. 0047 added it in #4515, and it has no
  other callers in the repo.
- **GREEN on the emulator:** `GcpStreamPurge*` 4/4 in each of 3 runs. That is the two new clock-lag tests, plus 0047's
  two Stream purge tests, which failed on master from the real skew.
- **Not addressed (Scope Notes 3–4, out of scope):**
  - the clock-independent in-flight/flow-control gap;
  - the Seek's own two-clock target when the client lags by 1 min or more.
- **Verified** at the fix (uncommitted, on `f75a1bd02`), net10.0. Compared by test name with 0050's baselines.
  - **Emulator, Stream:** 129 passed / 0 failed / 25 skipped. The only changes are 0047's two Stream purge tests,
    which went Failed → Passed, and the two new tests.
  - **Emulator, CI filter:** 201 passed / 49 failed / 30 skipped. No test changed outcome (the 49 are the
    Firestore/GCS tests, which need real GCP).
  - **Real Pub/Sub, gcp-ci scope:** 171 passed / 1 failed / 30 skipped, the same totals as 0050.
    - The 1 was a different test from 0050's flake: a Pull DLQ `AlreadyExists` failure during provisioning. It passed
      on re-run.
  - **Real Pub/Sub, Stream filter** (the first run of this filter on real Pub/Sub): 118 passed / 11 failed /
    25 skipped.
    - All 4 purge tests passed.
    - The 11 failures also fail on master, run against real Pub/Sub from a throwaway worktree; master failed a 12th
      as well. They are test-harness gaps, unrelated to this fix:
      - `GcpStreamDeliveryAttemptAttribute(Async)Tests` publish a reserved `googclient_…` attribute key, which only
        the emulator accepts. They lack `Requires=PubSubEmulator`.
      - 9 StreamOrdering `When_rejecting_message_*` tests time out on a 5 s first receive. Real Pub/Sub's first
        ordering delivery takes about 8 s.
