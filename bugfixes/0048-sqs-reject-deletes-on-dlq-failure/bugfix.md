# Bugfix: SQS RejectAsync deletes the source message when the DLQ/invalid-message send fails (should release it, per spec 0037 R-19)

**Linked Issue**: #4415
**Status**: Verified

> **Scope decision (made before triage, by the user):** the issue itself frames this as an open
> question — keep SQS's current bounded-loss behaviour (documented informally in ADR 0038) or
> change it to match spec 0037 R-19's GCP behaviour (release for redelivery instead of deleting).
> The user chose **change to match R-19**. This triage's hypothesis is built around that direction;
> it does not re-litigate whether to change.

## Symptom

When `SqsMessageConsumer.RejectAsync` routes a rejected message to a configured dead-letter or
invalid-message queue and the `SendAsync` to that queue throws, the consumer logs an Error and then
**deletes the source message**. The message ends up in neither queue, so it is lost. This happens
on any send failure, transient or not: throttling, a network blip, a missing destination queue, or
an IAM denial. Both SQS packages (`AWSSQS` and `AWSSQS.V4`) behave the same way.

Expected (the user's decision, matching spec 0037 R-19 for GCP): on a failed routing send, the
source message is **not** deleted. It is released for prompt redelivery, an Error is logged, and
`Reject` returns `true` ("settled by this call").

## Suspected Location

**Primary: the delete-on-send-failure branch (lockstep pair)**
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs:311-318`: `catch (Exception
  ex)` logs `ErrorSendingToRejectionChannel` (:315), calls
  `DeleteSourceMessageAsync(receiptHandle!, ...)` (:316) and returns `true` (:317). The comment at
  :313-314 reads: "Sending to DLQ failed — delete the original to prevent infinite reprocessing.
  The message is lost rather than stuck in a retry loop." The issue cites `:307-316`, which is a
  few lines off. `RejectAsync` itself starts at :256.
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageConsumer.cs:208-215`: the same code.
  `RejectAsync` starts at :153, the delete is at :213 and the comment at :210-211.

**Supporting code the fix touches or must avoid**
- The handle is copied before it is stripped. V3: the copy is at :258-261 and `RefreshMetadata`
  removes `"ReceiptHandle"` from the bag at :510, called at :283. V4: the strip is at :503. By the
  time the send fails, the bag no longer holds the handle.
- The existing release primitives read the handle from the bag:
  - `NackAsync` (V3 :338-371; V4 :337) issues `ChangeMessageVisibilityRequest(_channelUrl,
    receiptHandle, 0)` (V3 :351-352). It reads the handle from the bag at :340 and silently returns
    if it is missing. It also rethrows non-handle exceptions at :369.
  - `RequeueAsync` (V3 :388-428; V4 :381) also reads the bag (:391) and returns `false` if the
    handle is missing.
  - Called after `RefreshMetadata`, either one would silently do nothing. A handle-taking private
    helper, like the existing `DeleteSourceMessageAsync(string receiptHandle, ...)` (V3 :525-549;
    V4 :518), is the in-file pattern to mirror. Note that `DeleteSourceMessageAsync` also rethrows
    (:547).
- **A second loss path (not named in the issue).**
  - `CreateDeadLetterProducer` and `CreateInvalidMessageProducer` catch construction exceptions and
    return `null`: V3 :474-478 and :493-497; V4 :470 and :489. The null is cached in the `Lazy`.
  - A null producer then goes to the `else` branch at V3 :306-309 ("NoChannelsConfiguredForRejection")
    and on to the unconditional delete at V3 :320, V4 :217.
  - ADR 0078 already records this as a divergence from GCP:
    `docs/adr/0078-gcp-rejection-routing-and-dlq-channel-creation.md:136` and `:139` ("SQS turns a
    creation failure into a `null` producer, which then leads to 'no channels configured' and a
    delete").

**Reference implementation to mirror (GCP, R-19)**
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs:361-407`
  (`RejectAsync`):
  - It copies the handle first (:363-365) and routes (:367).
  - On `RoutingOutcome.Failed` it calls `ReleaseByHandleAsync(ackId)` (:376-392). That helper is at
    :513, uses `ModifyAckDeadline(…, 0)`, takes the handle and does not read the bag.
  - A failed release is caught and logged (:386-389), and the method returns `true`.
  - The sync version is at :323-330.
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpRejectionRouter.cs:163-195`: `RouteAsync`
  treats both producer creation and publish failure as `Failed` (:186-194). `RoutingOutcome.cs`
  defines `NoDestination | Routed | Failed`.

**Governing documents**
- Spec 0037 R-19 is on master: `specs/0037-delivery-count-and-rejection-routing/requirements.md:531-575`.
  It reads: "**R-19. If the routing publish fails, the original message is not acknowledged and the
  failure is logged at Error.** The message therefore becomes eligible for redelivery rather than
  being destroyed." The outcomes it lists:
  - an Error is logged;
  - the message is "released for prompt redelivery, by the same call that consumer's `Requeue`
    already makes";
  - "`Reject` returns `true`";
  - "If the release itself fails, that is also logged at Error, `Reject` still returns `true`".
  - Loop bound (:556-566): "What bounds it is Pub/Sub's own `DeadLetterPolicy.MaxDeliveryAttempts`
    … **Where no policy can take effect, there is no bound** … accepted, explicitly, as the price
    of never discarding a message whose destination is configured."
  - :570-575 and §Out of Scope :1215-1221 explicitly leave SQS unchanged and defer the question to
    this issue.
  - The trap list at :597-607 warns that "Reusing `Requeue` as-is is a trap", including SQS's handle
    stripping (`SqsMessageConsumer.cs:504`, now :510).
- ADR 0038, `docs/adr/0038-aws-sqs-dlq-direct-send.md` (status Accepted, :4 and :22):
  - Decision §2 (:63-71) is "send to DLQ, then `DeleteMessage`".
  - It does **not** state delete-on-send-failure as an explicit decision. It is only implied by the
    Risks entry at :128-129: "`SendMessage` to DLQ fails, then `DeleteMessage` also fails — message
    stuck … log the error and continue".
  - The lockstep requirement is at :49 ("Both must be updated identically") and :121.
- The issue says the behaviour is "documented in ADR 0038". That is only partly true: it is implied
  in Risks, not decided. The change will still need ADR 0038 amended, or a short superseding note.

**Tests**
- No test (in `tests/Paramore.Brighter.AWS.Tests` or `tests/Paramore.Brighter.AWS.V4.Tests`, by
  grep for `ErrorSendingToRejectionChannel`, send-failure or throwing-producer patterns) exercises
  the failed-send branch today. That suggests no existing test asserts the delete, though grep
  alone does not prove it — `/bugfix:confirm` should check this directly.
- The rejection tests that exist cover only success paths and are generated conformance tests, e.g.
  `.../SqsStandard/Generated/{Reactor,Proactor}/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`,
  `..._with_no_channels_configured_should_acknowledge_and_log.cs`, plus the SqsFifo/SnsStandard/SnsFifo
  equivalents in both test projects.
- If the no-existing-test finding holds, the regression test is new, with no existing test to invert.

## Root-Cause Hypothesis

**Hypothesis (falsifiable).** Any exception from the routing `SendAsync`, or from resolving the
producer inside the `try` that starts at V3 :281, lands in `catch` at V3 :311 / V4 :208. That block
unconditionally calls `DeleteSourceMessageAsync` (V3 :316 / V4 :213). So a rejected message whose
destination is configured is permanently removed from the source queue without reaching any
destination.

- **Test that falsifies it:** with a `deadLetterRoutingKey` pointing at a queue the producer cannot
  send to, `RejectAsync(msg, DeliveryError)` returns `true`. If the source queue then still holds
  the message (redelivered after visibility 0), the hypothesis is false. Today it should be empty,
  and the DLQ empty too.
- **Cause:** this is a deliberate design choice (the comment at :313-314), not an accidental
  defect. Per the user's decision it is wrong under R-19's "never discard a message whose
  destination is configured" rule.

**Required shape of the change (for confirm to validate, not a fix):**
- **What happens on a failed routing send.** Replace the delete in the failure branch with a
  release: `ChangeMessageVisibility(receiptHandle, 0)`.
  - Use a **private helper that takes the copied `receiptHandle`**, mirroring
    `DeleteSourceMessageAsync` and GCP's `ReleaseByHandleAsync`.
  - Do **not** use public `NackAsync` or `RequeueAsync`. Both read `"ReceiptHandle"` from the bag,
    which `RefreshMetadata` has already removed (V3 :510 / V4 :503), so they would silently no-op.
    That is exactly the R-19 trap at requirements.md:597-607.
- **What `Reject` reports.** Log at Error (the existing `ErrorSendingToRejectionChannel` already
  does this) and return `true`.
- **When the release itself fails.** A failed release, including `ReceiptHandleIsInvalidException`,
  must be caught and logged, with `Reject` still returning `true`, so nothing escapes the pump.
  Reactor and Proactor call `Reject` from inside catch blocks:
  `src/Paramore.Brighter.ServiceActivator/Reactor.cs:448-454` and the call sites at :299-373.

**Risks /bugfix:confirm MUST verify:**

1. **No RedrivePolicy means an unbounded loop.** The consumer knows only Brighter's *configured*
   attributes:
   - `queueAttributes` comes in at the V3 ctor :86, is stored at :101, and is passed from
     `SqsMessageConsumerFactory.cs:90`.
   - `SqsSubscription.NativeRedriveLimit => QueueAttributes.RedrivePolicy?.MaxReceiveCount` (V3 and
     V4 `SqsSubscription.cs:53`).
   - `AWSMessagingGateway` applies a RedrivePolicy only when Brighter creates the queue (V3
     `AWSMessagingGateway.cs:372-380`; V4 :366-374).
   - Nothing reads the *actual* queue's `RedrivePolicy` at runtime. With `MakeChannels` set to
     Assume or Validate, the real queue may have a policy Brighter doesn't know about, or none at
     all.
   - With no effective redrive policy, a deterministic send failure (missing DLQ queue, IAM denial)
     will loop for ever, logging an Error each time.
   - Confirm must decide whether to accept this unconditionally, as R-19 does for GCP (":560-566 …
     accepted, explicitly"), or to guard it, e.g. fall back to delete when `RedrivePolicy == null`.
     A guard would split the behaviour and diverge from R-19.
   - Also check the delivery-budget interaction. `requeueCount` exhaustion calls `Reject` again
     (`Reactor.cs:534`), which fails again and releases again. As R-19 notes, the budget does not
     bound this loop. Only the native `maxReceiveCount` does, and after it the message reaches the
     native DLQ without Brighter's rejection metadata.
2. **Producer-creation failure is a second loss path.** A null-cached `Lazy` producer leads to "no
   channels configured" and then a delete (V3 :474-478 / :493-497, :306-309, :320). Confirm should
   decide whether it is in scope. GCP treats it as `Failed` and releases (ADR 0078 :136-139).
   Fixing only the `catch` would leave this path still deleting.
3. **AWSSQS.V4 lockstep (mandatory).** ADR 0038 :49 and :121 require both packages to change
   identically: V3 `SqsMessageConsumer.cs:311-318` and V4 `SqsMessageConsumer.cs:208-215`, plus the
   sync `Reject` wrappers (V3 :247, V4 :144), with regression tests in both test projects.
4. **ADR impact.** ADR 0038 (Accepted) implies the delete-on-failure via Risks :128-129, and its
   coexistence table (:77-81) assumes the native policy is "irrelevant for rejected messages".
   After the change, released messages *do* count toward native `maxReceiveCount`. ADR 0038
   therefore needs an amendment, or a superseding ADR, recording the R-19 alignment. Spec 0037's
   §Out of Scope (:1215-1221) and :570-575 should be updated to say the open question has been
   answered.

## Confirmed Root Cause

**CONFIRMED.** `SqsMessageConsumer.RejectAsync` wraps everything from the handle copy onward
(metadata stamping, route selection, lazy producer resolution, `producer.SendAsync`) in a single
`try` whose `catch (Exception ex)` is unconditional: it logs `ErrorSendingToRejectionChannel`, then
unconditionally calls `DeleteSourceMessageAsync(receiptHandle!, …)`, then returns `true`. There is
no exception-type filter, no `RedrivePolicy`/`_queueAttributes` check, no retry — the comment at
V3 :313-314 / V4 :210-211 confirms this is deliberate. Under the user's decision to align with spec
0037 R-19, this deliberate choice is the defect: a rejected message whose destination is configured
is destroyed without reaching any destination.

- V3: `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs` — `try` :281-318,
  delete at :316, return at :317.
- V4: `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageConsumer.cs` — `try` :178-215,
  delete at :213, return at :214.
- All bugfix.md line citations against SQS source were re-verified accurate at current HEAD
  (`d3fb29fa1`). Stale citations exist only in *other* documents that reference SQS lines: spec
  0037's own SQS citations (`:307-316`, `:504`, `:279`, `:257` — now :311-318, :510, :283, :258)
  and ADR 0078's (`SqsMessageConsumer.cs:104`, `:455-475`) — both predate later SQS edits and will
  need correcting as part of the doc updates this fix requires anyway.

## Evidence

- [x] Code-trace (infra-bound bug; LocalStack required for an executable repro, deferred to
  `/bugfix:test`)
- [ ] Red repro

1. **Entry and handle copy.** V3 `RejectAsync` :256, handle copied :258-261. V4: :153, :155-158.
2. **Early "no channels" branch is legitimate, not the bug.** V3 :268-277 / V4 :165-174 fires only
   when both `Lazy` producer fields are null (no routing keys configured at all) and calls
   `AcknowledgeAsync` — this is correct `NoDestination` behaviour and out of scope.
3. **Handle is stripped from the bag before the send that can fail.** `try` opens V3 :281 / V4 :178;
   its first statement is `RefreshMetadata` (V3 :283 / V4 :180), which removes `"ReceiptHandle"`
   from `message.Header.Bag` (V3 :510 / V4 :503) — before `producer.SendAsync` (V3 :303 / V4 :200)
   can throw. This proves `NackAsync`/`RequeueAsync` (which both read the bag) would silently no-op
   if reused here, exactly the trap spec 0037 names at :597-607.
4. **Producer resolution is inside the same unguarded `try`**, not just the send: `_invalidMessageProducer?.Value` /
   `_deadLetterProducer?.Value` at V3 :296/:298, V4 :193/:195.
5. **The catch is truly unconditional.** V3 :311-318 / V4 :208-215: log → `DeleteSourceMessageAsync`
   → `return true`, no condition anywhere. `grep` for `RedrivePolicy` / `_queueAttributes` inside
   `RejectAsync` returns nothing.
6. **Existing bug, found by the sub-agent and not named in triage: the delete helper can itself
   escape `Reject`.** `DeleteSourceMessageAsync` (V3 :525-549 / V4 :518-542) swallows only
   `ReceiptHandleIsInvalidException` and rethrows everything else (V3 :547 / V4 :540). Today a
   failed delete inside the failure-catch branch can throw out of `Reject` into the pump —
   `Reactor.RejectMessage` (`src/Paramore.Brighter.ServiceActivator/Reactor.cs:448-454`) has no try
   around `Channel.Reject`. The replacement release helper must not repeat this: it must swallow
   everything itself.
7. **Second loss path confirmed to exist, but low practical reachability.**
   `CreateDeadLetterProducer`/`CreateInvalidMessageProducer` catch construction exceptions and
   return `null` (V3 :474-478/:493-497, V4 :467-471/:486-490); the `Lazy` (`LazyThreadSafetyMode.None`,
   V3 :106-114) caches that `null` permanently; a null producer falls to "no channels configured"
   (V3 :306-309 / V4 :203-206) and then the unconditional delete (V3 :320 / V4 :217). However the
   sub-agent traced that `SqsMessageProducer`'s ctor (`SqsMessageProducer.cs:64-79`) does no I/O and
   only throws on null publication/channel name — queue-existence failures surface lazily inside
   `SendAsync` → `ConfirmQueueExistsAsync`, i.e. they land in the *main* catch (item 5), not this
   null-producer path. So this path is real but rarely hit in practice.
8. **No existing test exercises the failed-send branch — confirmed directly, not just by grep.**
   No `.cs` file under `tests/Paramore.Brighter.AWS.Tests` or `tests/Paramore.Brighter.AWS.V4.Tests`
   references `ErrorSendingToRejectionChannel` or a throwing producer (only stale `obj/` DLL hits).
   Every existing test that configures a `deadLetterRoutingKey` also provisions the DLQ queue (e.g.
   `Sqs/Standard/Reactor/When_sqs_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral.cs:96-109`,
   the conformance provider `SqsStandardMessageGatewayProvider.cs:89-107` which also sets a native
   `RedrivePolicy(deadLetterChannelName, 5)` at :104). The regression test is genuinely new — there
   is no existing test to invert.
9. **GCP reference confirmed accurate**: `GcpPullMessageConsumer.RejectAsync` :361, handle copy
   :363-365, route :367; `RoutingOutcome.Failed` (:376) → `ReleaseByHandleAsync(ackId)` (:384)
   inside try/catch that logs and returns `true` (:382-391); `ReleaseByHandleAsync` (:513) does
   `ModifyAckDeadline(…, 0)` on the passed handle, no bag read. `GcpRejectionRouter.RouteAsync`
   (:163-195) treats producer-creation and publish failure identically as `Failed` (:186-194).

## Scope Notes

**Suggested-Fix Assessment: PARTIAL.** The hypothesis's "required shape of the change" is correct
in its central claim (replace the delete with a handle-taking release helper; don't reuse
`NackAsync`/`RequeueAsync`), but the sub-agent's trace surfaced gaps that change the fix's scope:

1. **The release helper must not rethrow.** Unlike `DeleteSourceMessageAsync`/`NackAsync` (which
   both rethrow non-handle exceptions), the new helper must catch everything —
   `ReceiptHandleIsInvalidException` at Warning (message is already visible again), everything else
   at Error — and always let `Reject` return `true`. Otherwise it repeats item 6 above.
2. **Cancellation needs a deliberate choice.** `catch (Exception)` also catches
   `OperationCanceledException` from pump shutdown. After the fix, the release call would run on
   the same (already-cancelling) token and likely throw immediately, falling back to the visibility
   timeout rather than an immediate release. Decide and document this; don't assume immediate
   release under cancellation in the test.
3. **The catch covers more than "send failed"** — also `RefreshMetadata`, route selection, and
   producer construction inside the `Lazy` factory. That's consistent with R-19's "producer
   creation or publish threw ⇒ Failed", but the log message/comment should be updated to say so
   rather than implying only the send can fail.
4. **`Reject`'s doc comments need updating.** They currently say "True if the message has been
   removed from the channel" (V3 :246/:255; V4 :143/:152) — after the change `true` means "settled
   by this call" (matching GCP's doc comment), not "deleted".
5. **Falsifying/regression-test setup must avoid `makeChannels: Create` auto-creating the DLQ.**
   With `Create`, the DLQ producer would just create the missing queue and the send would succeed.
   The test needs a `deadLetterRoutingKey` naming a non-existent queue with `OnMissingChannel.Validate`
   or `.Assume`, a pre-created source queue, and Reactor + Proactor variants in both test projects.

**Risk 1 (RedrivePolicy / unbounded loop) — recommendation: accept unconditionally, matching GCP/R-19;
do not guard.** A guard keyed on Brighter's *configured* `SqsAttributes.RedrivePolicy` would be
checking the wrong fact under `Assume`/`Validate` (the real queue's policy is unknown), a runtime
`GetQueueAttributes` call adds an extra broker round-trip and IAM permission, and a fallback-to-delete
guard reintroduces the exact loss the user decided to remove. Mitigants to record in the ADR
amendment: SQS's `MessageRetentionPeriod` (default 4 days, max 14) is a hard time bound even with no
redrive policy, so "unbounded" means "bounded by retention," not infinite; each loop re-runs the
handler, so handler idempotency matters; and a new SQS-specific consequence not in the original
hypothesis — **releasing a FIFO message that loops blocks its whole message group (head-of-line)**
until native `maxReceiveCount` or retention ends it. Document, don't guard.

**Risk 2 (producer-creation failure, the second loss path) — recommendation: in scope, but as a
small, low-urgency change.** Treat a `Lazy` producer that resolves to `null` for a route that *was*
selected (`shouldRoute == true`) as `Failed` and release, mirroring GCP (ADR 0078 :136-139).
Reachability is low in practice (Evidence item 7), so this is about closing a documented
cross-backend parity gap, not urgent loss prevention.

**Risk 3 (AWSSQS.V4 lockstep) — confirmed mandatory, confirmed identical today.** V3 :311-318 and
V4 :208-215 are the same shape. Sync `Reject` wrappers (V3 :247, V4 :144) route through
`RejectAsync` and need no logic change, but do need Reactor-path regression tests alongside the
Proactor ones. SNS subscriptions reuse the same `SqsMessageConsumer`
(`SqsMessageConsumerFactory.cs:81-91`), so the fix covers SNS automatically; adding dedicated
Sns/Standard and Sns/Fifo regression variants is optional since the code path is identical.

**Risk 4 (ADR/spec impact) — confirmed needed.** ADR 0038's coexistence row (:79, "native policy is
irrelevant for rejected messages") becomes false once released messages count toward native
`maxReceiveCount`; its Risks entry (:128-129) also needs rewriting. Spec 0037 (:570-575,
:1215-1221) should record the open question as answered. ADR 0078 :139 and its stale SQS line
citations (`:104`, `:455-475`) should be corrected alongside.

**Additional findings, out of scope for #4415 but flagged for the record:**
- A **pre-existing, separate bug**: `DeleteSourceMessageAsync`'s success-path rethrow (V3 :320 /
  V4 :217) can already escape `Reject` on the *acknowledge* path today — the SQS analogue of
  R-16/R-17, which GCP already handles by catching. Not required for this fix; worth its own issue.
- **RocketMQ has the same loss pattern** (`RocketMessageConsumer.cs:176-184`: logs, then acks the
  source in `finally` on DLQ-send failure) — a separate cross-backend parity gap, not this issue.
- **Redis cannot release** (`RedisMessageConsumer.cs:376-383` has already popped the message before
  this point) — structurally out of scope, not a gap to fix.
- No other callers of `DeleteSourceMessageAsync` exist beyond `AcknowledgeAsync` (V3 :135) and
  `RejectAsync`.

## Regression Test

Four tests, one per ADR 0038 lockstep/path combination, approved by the user 2026-10-05. Each
builds `SqsMessageConsumer` directly (bypassing `SqsMessageConsumerFactory`/subscription-level
`MakeChannels`) with `deadLetterRoutingKey` pointing at a DLQ queue that is deliberately never
created and `makeChannels: OnMissingChannel.Validate`, so the DLQ send genuinely fails with
`QueueDoesNotExistException` rather than silently auto-creating the queue (which `Create` mode
would do, masking the failure). Each rejects a received message with `DeliveryError`, asserts
`Reject`/`RejectAsync` returns `true`, then asserts the source queue redelivers the message
(`MessageType` is not `MT_NONE`) rather than returning empty. All four were run RED against the
live Floci AWS emulator (localhost:4566) and failed for the right reason — today the message is
deleted, so the redelivery assertion fails with "Expected: Not MT_NONE, Actual: MT_NONE" — on both
net9.0 and net10.0 TFMs:

- `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor/When_rejecting_message_with_failed_dlq_send_should_release_for_redelivery.cs`
  (`SqsMessageConsumerFailedDlqSendReleasesSourceTests`)
- `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Proactor/When_rejecting_message_with_failed_dlq_send_should_release_for_redelivery_async.cs`
  (`SqsMessageConsumerFailedDlqSendReleasesSourceTestsAsync`)
- `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Reactor/When_rejecting_message_with_failed_dlq_send_should_release_for_redelivery.cs`
  (`SqsMessageConsumerFailedDlqSendReleasesSourceTests`)
- `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Proactor/When_rejecting_message_with_failed_dlq_send_should_release_for_redelivery_async.cs`
  (`SqsMessageConsumerFailedDlqSendReleasesSourceTestsAsync`)

**Deliberately not covered by a dedicated regression test**: the producer-creation-failure second
loss path (Scope Notes, Risk 2). It is not reachable from the public test surface with real
infrastructure — `SqsMessageProducer`'s constructor does no I/O and only throws on a null
publication/channel name, neither of which a valid `RoutingKey`-based DLQ configuration can
produce, so there is no realistic external failure that drives this path without reaching into
internals. The fix should still close it per the Scope Notes recommendation; it remains
unverified by an automated test, consistent with its low practical reachability.

## Fix

**Minimal change, scoped to the Confirmed Root Cause.** In both `SqsMessageConsumer.RejectAsync`
(V3 and V4, kept in lockstep per ADR 0038), the `catch (Exception ex)` block that previously called
the unconditional `DeleteSourceMessageAsync` now calls a new private
`ReleaseSourceMessageAsync(receiptHandle, messageId, cancellationToken)` instead, and still returns
`true`. No other control flow changed.

- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs`
  - New private `ReleaseSourceMessageAsync` (mirrors `DeleteSourceMessageAsync`'s shape, but issues
    `ChangeMessageVisibilityRequest(_channelUrl, receiptHandle, 0)` instead of `DeleteMessageAsync`).
    Unlike `DeleteSourceMessageAsync`, it **never rethrows**: `ReceiptHandleIsInvalidException` is
    logged at Warning, everything else at Error — satisfying Scope Notes item 1 (a release failure
    must not escape `Reject` into the pump).
  - The failure-catch in `RejectAsync` calls the new helper with **`CancellationToken.None`**
    (Scope Notes item 2: a deliberate choice, documented inline — the release must still be
    attempted even when the pump is shutting down and the caller's token is already cancelled,
    otherwise the message would sit out its full visibility timeout instead of becoming available
    immediately).
  - Updated the inline comment at the catch site (was: "delete the original to prevent infinite
    reprocessing... lost rather than stuck in a retry loop"; now describes the release and cites
    R-19).
  - Updated the XML doc `<returns>` on both `Reject` and `RejectAsync` (was: "True if the message
    has been removed from the channel"; now: "True if the message was settled by this call
    (deleted, routed to a rejection channel, or released for redelivery); false if the message had
    no receipt handle" — Scope Notes item 4).
  - Three new `LoggerMessage` entries: `ReleasedSourceMessageAfterRejectionFailure` (Information),
    `ReleaseFailedReceiptHandleExpiredAfterRejectionFailure` (Warning),
    `ErrorReleasingSourceMessageAfterRejectionFailure` (Error).
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageConsumer.cs` — identical change
  (lockstep, ADR 0038 :49/:121).

**Deliberately NOT fixed (left for a future issue, not this one):**
- **Scope Notes Risk 2 (producer-creation-failure second loss path)**: not changed. It has no red
  test — the Regression Test section already recorded why (not reachable from the public test
  surface with real infrastructure) — and `.agent_instructions/testing.md` is explicit that code is
  written to satisfy a test, not speculatively. Confirm's recommendation ("in scope, low urgency")
  stands as a recommendation for a future, separately-tracked change; implementing it here would be
  untested production code. `else if (shouldRoute)` still falls through to
  `NoChannelsConfiguredForRejection` + delete when the `Lazy` producer resolves to `null`, unchanged.
- The ADR 0038 amendment and spec 0037 Out-of-Scope update (Scope Notes Risk 4) — documentation
  work, out of scope for `/bugfix:fix`; left for a follow-up before/with the PR.
- The pre-existing success-path rethrow in `DeleteSourceMessageAsync` (Evidence item 6) and the
  RocketMQ parity gap — both explicitly flagged in Scope Notes as separate, future issues.

**Verification run (targeted, then full suite — `/bugfix:verify`):**
- All 4 regression tests green, both net9.0 and net10.0 TFMs, against the live Floci AWS emulator
  (`AWS_SERVICE_URL=http://localhost:4566`).
- Broader regression check: every existing test matching `FullyQualifiedName~Reject` in both test
  projects still passes — **97/97** in `Paramore.Brighter.AWS.Tests` and **97/97** in
  `Paramore.Brighter.AWS.V4.Tests` (each count ×2 TFMs), confirming the success path and the
  legitimate "no channels configured" path are unaffected.
- Both changed source projects (`Paramore.Brighter.MessagingGateway.AWSSQS`,
  `...AWSSQS.V4`) build with 0 warnings, 0 errors.
- **Full test project suite (`/bugfix:verify`)**: `Paramore.Brighter.AWS.Tests` **416/416 passed**
  and `Paramore.Brighter.AWS.V4.Tests` **416/416 passed**, both on net9.0 and net10.0 (0 failed, 0
  skipped), against the live Floci AWS emulator. No regressions anywhere in either project.

### Critical Files for Implementation
- src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs
- src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsMessageConsumer.cs
- src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs (reference pattern, :361-407, :513)
- docs/adr/0038-aws-sqs-dlq-direct-send.md
- specs/0037-delivery-count-and-rejection-routing/requirements.md (R-19 at :531-643; Out of Scope at :1215-1221)
