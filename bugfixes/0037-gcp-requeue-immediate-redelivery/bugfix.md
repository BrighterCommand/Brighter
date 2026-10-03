# Bugfix: GCP Pub/Sub Pull – Requeue(message, TimeSpan.Zero) does not redeliver promptly on real Pub/Sub

**Linked Issue**: #4321
**Status**: Verified

## Symptom
On every GCP Pub/Sub **Pull** configuration (Pull.Reactor, Pull.Proactor, PullOrdering.Reactor, PullOrdering.Proactor), calling `Requeue(message, TimeSpan.Zero)` / `RequeueAsync(...)` returns `true`, so `ModifyAckDeadline(ackId, 0)` completes without error. On **real** Pub/Sub, though, the message does not come back promptly. The time from Requeue returning to redelivery was 7.12–27.27 s over 11 measured runs, against a 5 s bound (FR-15 of the #4240 conformance suite). The subscription's ack deadline is 10 s. That makes the observed delays roughly 0.7 to 2.7 ack-deadline periods: the message behaves as if it were never nacked, and in the longer runs as if it were leased and then lost more than once. The failure reproduced on 8 of 8+ CI runs. The same test **passed against the local Pub/Sub emulator** when the Pull conformance was first recorded (commit `d94e9b827` message: "FR-7/15/16/22 Pass (native: ... zero-delay requeue ...)"). So the failure only shows up on real Pub/Sub.

## Suspected Location
- **Requeue (sync)**: `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs:335-359`, with the modack at `:349` (`client.ModifyAckDeadline(subscriptionName, [ackId], 0);`). The issue's `:309-332` is out of date because the file has moved.
- **Requeue (async)**: same file, `:369-399`, with the modack at `:384-389` (`AckDeadlineSeconds = 0`).
- **Receive path, the prime suspect**: same file.
  - `ReceiveAsync` at `:152-205` and `Receive` at `:229-268` both now bound the unary Pull with a client-side deadline taken from the caller's timeout. That comes from `BuildPullCallSettings` at `:213-216` (`CallSettings.FromExpiration(Expiration.FromTimeout(window))`).
  - They treat the resulting `DeadlineExceeded` as an empty receive (`:177-191` async, `:248-254` sync).
  - This bounding was added in `d94e9b827` (for FR-9) and refined in `cd7b8e257` and `1ae8e6ba6`. Before that, Pull ran with only the client's default expiration.
- **How the test drives it**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/Generated/Reactor/When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs:61-85` (Proactor twin `.../Proactor/...:67-91`).
  - After the requeue, it polls `_channel.Receive(TimeSpan.FromMilliseconds(500))` in a loop, so every poll is a Pull with a 500 ms client deadline that normally ends in `DeadlineExceeded`.
  - The Channel passes `Requeue` straight to the consumer (`src/Paramore.Brighter/Channel.cs:174-176`, `ChannelAsync.cs:181-183`). The channel's buffer only holds what `Receive` returned (`Channel.cs:146-149`).
- **ackId handling**: `src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:86,130` stores `ReceivedMessage.AckId` (a string) in `Bag["ReceiptHandle"]`. Nothing else touches it.
- **Harness ack deadline**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpPullMessageGatewayProvider.cs:169` (`ackDeadlineSeconds: 10`, not `:138` as the issue says) and `GcpPullOrderingMessageGatewayProvider.cs:180`.
- **RetryPolicy (lead 2)**:
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubMessageGateway.cs:369-377` (create) only sets `RetryPolicy` when `RequeueDelay != TimeSpan.Zero`.
  - `:449-462` (update) sets it to `null` otherwise.
  - `RequeueDelay` defaults to `TimeSpan.Zero` at `src/Paramore.Brighter/Subscription.cs:233-234` (not `:229`).
- **StreamingPull vs Pull (lead 4)**: the codebase has two separate paths.
  - Stream mode uses `SubscriberClient` (StreamingPull, `GcpStreamConsumer.cs:10`, built in `GcpPubSubConsumerFactory.cs:89-118`). Its `Requeue` calls `GcpStreamMessage.Reject()` → `SubscriberClient.Reply.Nack` (`GcpPubSubStreamMessageConsumer.cs:217-227`, `GcpStreamConsumer.cs:83,135`) and never calls `ModifyAckDeadline` directly.
  - Pull mode uses the raw `SubscriberServiceApiClient` unary `Pull`/`ModifyAckDeadline`.
  - The Stream/StreamOrdering FR-15 tests are also `Skip`ped (`.../Stream/Generated/*/When_requeuing_a_failed_message_with_zero_delay...cs:40/46`), so we have no real-GCP evidence either way for StreamingPull.

## Root-Cause Hypothesis
Ranked. Each one comes with the evidence that would prove or refute it.

**Leads the code rules out or downgrades**
- **Lead 1 (stale ackId from lease extension) has no mechanism in the code.** `GcpPullMessageConsumer` has no lease management, no auto-extension and no background modack. The only `ModifyAckDeadline` calls in the gateway are the two requeue calls (`:349`, `:384`). Each ackId comes from a single Pull, is stored once (`Parser.cs:130`) and is used once. Nothing in Brighter can supersede it between Receive and Requeue.
  - The one way an ackId could still be "accepted but ineffective" is on the server side: the ack deadline expiring before the modack arrives. That is implausible here, because Receive → Requeue takes well under 10 s.
- **Lead 2 (RetryPolicy null) is almost certainly not the cause.** A subscription with no retry policy is exactly the configuration where Pub/Sub redelivers immediately; a policy would only add backoff. `:369-377` / `:449-462` leave it unset or null because `RequeueDelay` is Zero.

**H1 (primary — UNVERIFIED): client-side-bounded Pulls lose messages in flight on real Pub/Sub, and the lost message sits leased until its ack deadline expires.**
- **The mechanism.** Since `d94e9b827`, every Receive with a timeout puts a short gRPC deadline on the unary Pull (`GcpPullMessageConsumer.cs:160,170,234,241,213-216`), and the FR-15 test polls with 500 ms windows. The modack-to-0 does make the message available straight away.
  - Real Pub/Sub can hand that message to an outstanding Pull just as the client's deadline fires. The client then sees `DeadlineExceeded` and throws the response away (`:177-191`/`:248-254`), but the server has already started a lease on the message.
  - The message is then invisible until the 10 s ack deadline expires. It can be lost the same way again on the next poll, which explains the spread of 10–27 s.
  - The emulator does not reproduce this server-side lease-on-cancelled-pull race, which fits FR-15 passing on the emulator and failing on real GCP.
  - In short: the modack works, and the redelivered copy is lost in a cancelled poll.
- **What would prove H1 in /bugfix:confirm:**
  - Read the Pub/Sub `DeliveryAttempt` or the redelivered message's ack/lease history.
  - Or, more practically, run FR-15 on real GCP with verbose logging and count how many 500 ms polls returned `DeadlineExceeded` between Requeue and redelivery.
  - Then change **only the test's poll window**: one Pull with a long timeout (e.g. `Receive(TimeSpan.FromSeconds(10))`), or non-cancelled polling. If redelivery then consistently lands well under 5 s, H1 is confirmed.
  - Alternatively, repeat the whole requeue→redeliver sequence against real GCP with `ModifyAckDeadline(0)` followed by one unbounded Pull.
- **What would refute H1:** with no client-side-cancelled Pulls in the window, redelivery still takes about one ack deadline.
- **A problem for H1:** the 7.12 s sample is shorter than a full 10 s deadline measured from the Requeue. A lease started by a lost poll *after* Requeue cannot expire in 7 s. That sample instead matches the *original* lease (started at the first Receive, 1–3 s before the stopwatch) expiring. That points at H2 for at least some runs.

**H2 (UNVERIFIED, platform-difference candidate): Pub/Sub does not act promptly on modack-to-0 for this Pull path, so redelivery falls back to the original ack deadline (issue leads 3 and 4).**
- If the zero-deadline modack is only best-effort on unary Pull (without exactly-once delivery, modack responses are not confirmations), the message comes back when the *original* lease ends. That is about 7–9 s after the stopwatch starts, plus extra 10 s periods whenever H1-style losses also happen.
- The 7.12 s sample is best explained this way.
- **What would prove or refute H2:** in the same controlled test (a single long-timeout Pull after Requeue, so H1's losses cannot happen), measure the time to redelivery.
  - ≈ 0–2 s means the modack works and H1 is the whole story.
  - ≈ (10 s − time between first Pull and Requeue) means the modack is ineffective and H2 holds.
  - A second control: set a long ack deadline (e.g. 60 s). Under H2, redelivery tracks the original deadline (~60 s). Under H1, it tracks deadline multiples only after a poll has been lost.

**H3 (lower priority, UNVERIFIED): the same experiment run on the Stream variants.** Stream uses `SubscriberClient` Nack instead of a raw modack. If Stream FR-15 passes on real GCP while Pull fails even with long polls, then the unary-Pull behaviour is the platform difference that lead 4 suggests, not a Brighter defect.

**What best separates the leads:** vary the post-requeue poll window (500 ms vs one long Pull) and the ack deadline (10 s vs 60 s), then see whether redelivery tracks modack+~0, the original lease, or "lease started by a cancelled poll".

## Confirmed Root Cause
**H1 CONFIRMED by live-GCP measurement. H2 REFUTED.** Three diagnostic runs against real Pub/Sub
(`diagnostic/Program.cs`, calling Brighter's actual `GcpPullMessageConsumer.Receive`/`Requeue` — not
a re-implementation):

| Run | ack deadline | poll window | redelivery time (since Requeue) |
|---|---|---|---|
| 1 | 10 s | 500 ms (matches the FR-15 test and the production pump's default poll shape) | 15.78 s |
| 2 | 10 s | 15000 ms | 1.60 s |
| 3 | 60 s | 15000 ms | 2.06 s |

**Runs 2 and 3 refute H2 outright.** If Pub/Sub ignored a unary-Pull `ModifyAckDeadline(0)` and fell
back to the original lease, run 3 (60 s ack deadline) should have taken ~60 s. It took 2.06 s —
essentially identical to run 2's 1.60 s at a 10 s deadline. Redelivery latency does not scale with
ack-deadline length once the poll isn't short-cancelled, which is exactly the signature of a modack
that works. There is no GCP platform limitation here.

**Run 1 vs runs 2/3 confirms H1.** With the short 500 ms poll window, redelivery took 15.78 s instead
of the ~1.6–2.1 s runs 2/3 establish as the true modack latency — a ~14 s gap that appears *only*
when the poll window is short enough to be client-cancelled. This is consistent with: the redelivered
message becomes available at the server around the ~1.5–2 s mark (matching runs 2/3), but is handed
to a short, client-bounded Pull RPC (`BuildPullCallSettings`, `GcpPullMessageConsumer.cs:213-216`)
that times out (`DeadlineExceeded`) at/around that moment. The client never sees that delivery's
ackId, so it cannot ack or further modack it — the message silently re-enters a full fresh
ack-deadline lease (10 s here) before a *later* poll happens to land inside a window when it's
deliverable again. Run 1's ~15.7 s ≈ ~1.5–2 s (base modack latency) + one lost ~10 s lease cycle +
remaining poll overhead — consistent arithmetic.

**Root cause, precisely stated**: `GcpPullMessageConsumer`'s client-side-bounded unary Pull (added in
`d94e9b827` for FR-9, via `BuildPullCallSettings`/`CallSettings.FromExpiration`) races against the
Pub/Sub server's handling of a modack'd-to-zero redelivery. When the client's Pull deadline is short
enough — 500 ms in the FR-15 test, and `Subscription.TimeOut`'s 300 ms default in production
(`Subscription.cs:230-231`) — to be cancelled at/around the moment the server dispatches the
redelivered message to that specific RPC, the delivery is lost to the caller with no indication: no
exception, no logged signal, just an ordinary-looking empty receive. The message then costs a full
fresh ack-deadline-length wait before a later poll happens to catch it. **This is a genuine
Brighter-side defect in the interaction between the bounded-Pull pattern and Pub/Sub's redelivery
semantics — not a GCP platform limitation.**

## Evidence
- [x] Live-GCP repro (not a code-trace alone — actual measurement against real Pub/Sub):
  - Run 1 (`dotnet run -- 10 500 60`): ackDeadline=10s, 500ms polls → redelivered on poll 31, 15.78s after Requeue returned.
  - Run 2 (`dotnet run -- 10 15000 4`): ackDeadline=10s, 15000ms polls → redelivered on poll 1, 1.60s after Requeue returned.
  - Run 3 (`dotnet run -- 60 15000 4`): ackDeadline=60s, 15000ms polls → redelivered on poll 1, 2.06s after Requeue returned.
  - Runs 2 vs 3 (near-identical latency despite 6× ack-deadline difference) → refutes H2.
  - Run 1 vs runs 2/3 (~14s gap appearing only with the short poll window) → confirms H1.
- [ ] N/A as a separate item — the live-GCP runs supersede a code-only "red repro" description; they
  *are* the repro. Capturing this durably as an executable regression test (un-skipping/adapting the
  generated FR-15 test, or a new test) is `/bugfix:test`'s job.

**Carried over from the first Confirm pass**: bugfix.md's originally-cited "7.12s minimum" (across 11
historical CI runs) may have been measured across a stopwatch-start change (`353b6e10f`) and should
be treated as a separate, less certain historical data point — this diagnostic's three runs are the
reliable evidence for the confirmed root cause above.

## Scope Notes
- **This is not test-only — it plausibly affects production Pull-mode consumption generally,** not
  just an explicit zero-delay requeue. `Subscription.TimeOut` defaults to 300 ms
  (`Subscription.cs:230-231`), so the live message pump issues the same kind of short,
  client-cancelled Pulls routinely. Any delivery (first delivery *or* redelivery) that becomes
  available at an unlucky moment relative to a short Pull's deadline could be silently lost to the
  same race, costing a full ack-deadline wait instead of the expected latency. This plausibly explains
  the long-tail timings noted in the original issue's follow-up comment on FR-16/FR-22/FR-9/FR-7
  (22–28s against a 30s bound) as instances of the same race rather than unrelated flake — worth a
  look when scoping the fix.
- **The fix should likely target `GcpPullMessageConsumer`'s bounded-Pull pattern itself**
  (`GcpPullMessageConsumer.cs:160,213-216,234`, from `d94e9b827`), not just the FR-15 test's poll
  window. The right shape (a longer minimum deadline floor? a different cancellation/retry strategy?)
  is a `/bugfix:fix` design decision, and trades off against FR-9 — the reason the bound was added in
  the first place — so may be worth flagging for extra care or its own ADR rather than a one-line fix.
- **H3 (Stream vs Pull) is now low priority.** H1 fully explains the symptom without invoking any GCP
  platform limitation, so there's no live need to measure StreamingPull to explain FR-15 — though it
  may still be worth a separate look later, since Stream's long-lived-stream architecture has no
  per-receive cancelled RPCs and might not share this race at all.
- **Misleading doc comments**: `Requeue` XML docs (`GcpPullMessageConsumer.cs:329-334`, `:361-368`)
  describe deadline-0 as "immediately redeliver" — now confirmed true of the modack itself, but
  misleading about the caller's actual observed experience when a short poll is in play. Worth a
  comment update alongside the fix.
- **CI-signal note**: the FR-15 test (`.../Pull/Generated/Reactor/...:40`) is `[Fact(Skip = "Deferred:
  #4240 …")]` and won't run in CI until deliberately un-skipped — `/bugfix:test` will need to do that
  as part of writing the regression test.

## Regression Test
`tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/When_requeuing_a_message_with_zero_delay_and_polling_with_a_short_window_should_still_redeliver_promptly.cs`
(`RequeueZeroDelayShortPollWindowTests`)

Hand-written, not generated — placed as a sibling of (not inside) the `Pull/Generated/` tree so it
is invisible to `LedgerSkipCrossCheckAudit`'s file scan, which only walks `Generated/` directories.
The existing generated FR-15 test (`.../Pull/Generated/Reactor/When_requeuing_a_failed_message_with_
zero_delay_should_redeliver_immediately.cs`) and the conformance ledger
(`specs/0036-universal-transport-conformance-tests/conformance-status.md`) were deliberately left
untouched — un-skipping that file would require a ledger-cell update this bugfix doesn't own (see
Scope Notes). bugfix.md's own Scope Notes named "a new test" as an accepted alternative.

**RED confirmed against real Pub/Sub** (not the emulator — this bug is unobservable there):
`GOOGLE_CLOUD_PROJECT=brighter-gcp-diag-51720 dotnet test
tests/Paramore.Brighter.Gcp.Tests/Paramore.Brighter.Gcp.Tests.csproj --framework net9.0 --filter
"FullyQualifiedName~RequeueZeroDelayShortPollWindowTests"` failed for the right reason: the message
was never redelivered within the full 30s poll ceiling (`Assert.NotEqual(MessageType.MT_NONE, ...)`
itself failed, not merely the 5s timing bound) — consistent with the confirmed root cause and the
upper end of the issue's observed 7–27s+ variance.

## Fix
**Files changed**: `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs`

Added a `MinimumPullDeadline` floor (3s) in `BuildPullCallSettings`: a caller-requested Pull timeout
shorter than 3s is raised to 3s before being used as the gRPC call's own deadline. This does not
change the `Receive`/`ReceiveAsync` contract from the caller's point of view beyond timing — it still
returns an empty receive on `DeadlineExceeded`, just not before 3s have elapsed for a short request.

**Why 3s, empirically** (no code-trace could have told us this — needed live-GCP measurement):
- 500ms / 1s poll windows: message lost reliably (0/3 trials redelivered within 30s at 1s).
- 2s: redelivered in all 3 trials, but every one landed right at ~2.0s — no margin over the
  ~1.6–2.1s base modack latency measured at Confirm. Too tight to trust.
- 3s: redelivered in all 3 trials at ~1.9–2.0s, i.e. *inside* the window with real margin, not at
  its edge. Chosen as the floor.

**Deliberately not done, per the approach chosen** (see the fix-approach discussion): a proper
by-construction fix would decouple the Pull RPC's own deadline from the caller's requested wait
entirely (always issue a generous-deadline Pull, bound only how long `Receive`/`ReceiveAsync` *waits*
for it, buffering a late response for the next call) — eliminating the race rather than out-running it
with a bigger number. That's real design work (an internal buffer/lifecycle for an abandoned-but-still-
in-flight Pull), explicitly deferred to a follow-up issue (#4503) rather than folded into this fix.

**Known trade-off accepted**: `BuildPullCallSettings` is shared by every bounded `Receive` call, not
just post-requeue polling — including the production pump's idle-poll loop (`Subscription.TimeOut`
defaults to 300ms). An empty subscription now takes up to 3s to report empty instead of 300ms,
affecting idle-poll/shutdown responsiveness for all GCP Pull consumers, not just this race. Accepted
because the Scope Notes already established the race isn't redelivery-specific — any short Pull can
lose a dispatched-but-uncollected delivery the same way.

**Verification**: regression test passed 3/3 against real GCP (17–21s each, well under the
pre-existing 30s poll ceiling). `Category=GcpPubSubPull` (15 passed, 26 still `Skip`-deferred to
#4240, unrelated) and `Category=GcpPubSubPullOrdering` (14 passed, 26 skipped) both ran clean against
real GCP afterward — no regressions in either Pull-mode suite.

### Critical Files for Implementation
- src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs
- src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubMessageGateway.cs
- src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs
- tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/Generated/Reactor/When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs
- tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpPullMessageGatewayProvider.cs
