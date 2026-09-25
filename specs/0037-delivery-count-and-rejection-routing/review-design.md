# Review: design — 0037-delivery-count-and-rejection-routing (round 1)

**Date**: 2026-09-24
**Threshold**: 60
**ADRs**: `0077-delivery-count-contract`, `0078-gcp-rejection-routing-and-dlq-channel-creation`
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

Review stance for this spec (the user's instruction): the requirements are approved; each finding carries
a **Fix type**, and the smallest effective fix is preferred — rephrase before adding text, because
repeated rewrites of definitions create errors as well as removing them.

## Findings

### 1. New core `Paramore.Brighter.RejectionMetadataKeys` shadows the generated harness record and breaks the test build (Score: 82)

**ADR**: both

0077 adds a core static class `Paramore.Brighter.RejectionMetadataKeys` (Key Components; Implementation step 1), and 0078 relies on it. Every conformance test project already has a generated `public sealed record RejectionMetadataKeys(...)` in a per-configuration sub-namespace (e.g. `Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull`). The providers that construct it live in the parent namespace and reach it only through a compilation-unit `using`; C# name lookup searches the enclosing namespaces (up to `Paramore.Brighter`) before compilation-unit usings, so once the core type exists, `new RejectionMetadataKeys(...)` binds to the core static class and fails to compile — across AWS, AWS.V4, GCP, Kafka, RMQ.Async and more providers, including transports outside this spec. 0078 step 5 ("they fill `RejectionMetadataKeys`") also uses the one name for both types.

**Evidence**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/Generated/RejectionMetadataKeys.cs:5,19`; `GcpPullMessageGatewayProvider.cs:35,41,364`; no `RejectionMetadataKeys` under `src/` today.

**Recommendation**: Rename the core type (e.g. `RejectionMetadataKeyNames`) in 0077 and 0078's references, or record that the harness record is renamed/fully qualified in the same structural commit.

**Fix type**: rephrase

---

### 2. The ADRs conflict on how the GCP harness keeps a `DeadLetterPolicy`; FR-23/AC-19 loses its counter (Score: 76)

**ADR**: both

0078 step 5: GCP providers "pass the routing keys **instead of** mapping `deadLetterRoutingKey` onto `DeadLetterPolicy`". The FR-23 template gets its DLQ only through `CreateSubscription(..., deadLetterRoutingKey: …)`, which today is the only thing giving a GCP subscription a `DeadLetterPolicy` — without one `DeliveryAttempt` is 0 (A-1; 0077's error table). 0077 step 7 / R-27(a) require `M = 5` on all four GCP providers and AC-19 requires a policy. Neither ADR says which topic the policy uses once the routing key goes to the Brighter route (AC-18 requires them distinct), nor whether every provider subscription gets a policy.

**Evidence**: 0078 Implementation step 5; 0077 Implementation step 7; `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid:63-66`; `GcpPullMessageGatewayProvider.cs:136-164`; R-27(a) GCP row `R = 3, M = 5`.

**Recommendation**: State the resulting provider shape in 0078 step 5 (pointer from 0077 step 7): routing keys to the Brighter route, plus a `DeadLetterPolicy { MaxDeliveryAttempts = 5 }` on a separately named topic wherever FR-23 needs the counter.

**Fix type**: clarify-add

---

### 3. R-26's "top of every `Create*Channel*`" logs twice on GCP (Score: 70)

**ADR**: 0077

0077 §R-26 calls `WarnIfUnenforceable` "once at the top of every `Create*Channel*`", listing GCP sync, async and `CreateAsyncChannelAsync`. `GcpPubSubChannelFactory.CreateAsyncChannel` is `=> BrighterAsyncContext.Run(() => CreateAsyncChannelAsync(subscription))`, the method the dispatcher calls for the Proactor (`ConsumerFactory.cs:124`) — two Warnings per channel, breaking R-26's "once per channel" and AC-29's "exactly one". GCP is the transport that trips R-11. AWS delegates the same way (`ChannelFactory.cs:82-83`), harmless only because the SQS call is a no-op.

**Evidence**: `GcpPubSubChannelFactory.cs:54-55`; `AWSSQS/ChannelFactory.cs:70-71,82-83`; AC-29.

**Recommendation**: Reword to "once on each non-delegating path — the method every public entry reaches"; name GCP's sites as `CreateSyncChannel` and `CreateAsyncChannelAsync`.

**Fix type**: rephrase

---

### 4. On Stream configurations the provisioning subscription starts a competing streaming pull (Score: 68)

**ADR**: 0078

The AC-18/AC-43 provisioning subscription is "identical … with the same names and `makeChannels: Create`". `EnsureSubscriptionExistsAsync` is `internal` and the harness has no `InternalsVisibleTo`, so provisioning goes through the channel/consumer factory. For `SubscriptionMode.Stream`, `GcpPubSubConsumerFactory.CreateAsync` registers a `GcpStreamConsumer` keyed on that (distinct) subscription object and calls `Start()`; that stream competes for the test message and extends its lease, so AC-18's "delivered again within W" and AC-43's pump observe the wrong consumer. Existing stream providers avoid this for DLQ readers with `SubscriptionMode.Pull`.

**Evidence**: `GcpPubSubMessageGateway.cs:186`; `GcpPubSubConsumerFactory.cs:62,89-95`; `GcpStreamMessageGatewayProvider`/`GcpStreamOrderingMessageGatewayProvider.cs:303,337`.

**Recommendation**: One line: the provisioning object uses `SubscriptionMode.Pull` (creation is mode-independent), or its channel is disposed before publishing. Say whether AC-18/AC-43 are bespoke or generated tests.

**Fix type**: clarify-add

---

### 5. Disposing the cached producer after `Failed` is outside the guarded region; sync dispose blocks (Score: 58)

**ADR**: 0078

The router catches "around producer creation and publish only" yet "disposes and discards its cached producer" after `Failed`. `GcpMessageProducer.Dispose` is `client.DisposeAsync().GetAwaiter().GetResult()` — sync-over-async on the Reactor pump thread, on a `PublisherClient` that just failed. A throw escapes `Reject` (R-19 forbids); a stall blocks the performer.

**Evidence**: 0078 router contract / lazy producer; `GcpMessageProducer.cs:141-143`.

**Recommendation**: Put disposal inside the guarded region (catch, log, drop reference); add "dispose after a failed publish completes promptly" to the unverified-at-implementation row.

**Fix type**: clarify-add

---

### 6. 0077 labels AC-39 the "Selector"; requirements say the ADR's conclusion selects (Score: 55)

**ADR**: 0077

§R-13: "Selector: AC-39, run on the emulator…". Requirements: AC-39 "does **not** by itself select between AC-19 and AC-40; the ADR's recorded conclusion does".

**Evidence**: 0077:165; requirements.md:1493-1495, :1523-1525.

**Recommendation**: "Input: AC-39's measurement; the selector is this ADR's amended conclusion under the rule below."

**Fix type**: rephrase

---

### 7. AC-20's members-unset case needs network to real GCP; the offline mitigation cannot apply (Score: 48)

**ADR**: 0078

The harness credential is never null (`GatewayFactory.GetCredential` falls back to `FromAccessToken("mock")`), so `GetProjectAsync` always leaves for real GCP; offline gives `Unavailable` (not tolerated). The Risks mitigation ("set members") contradicts AC-20's first Given (members unset).

**Evidence**: `tests/Paramore.Brighter.Gcp.Tests/Helper/GatewayFactory.cs:13-22`; 0078 R-21 paragraph and Risks.

**Recommendation**: Record that AC-20's first Given needs outbound network, or build its connection with `Credential = null` so the construction path is taken wherever ADC is absent.

**Fix type**: clarify-add

---

### 8. Missing-handle rationale is inaccurate for the stream consumer (Score: 45)

**ADR**: 0078

"Whatever broker copy exists returns at its own deadline" — on the stream consumer the uncompleted `GcpStreamMessage` keeps its lease extended and holds a flow-control slot. Unreachable (parsers always set the handle), but as written it is a third outstanding case.

**Evidence**: 0078 "Missing receipt handle"; `GcpStreamConsumer.cs` (`HandleMessage` awaits `WaitForCompleteAsync`); requirements.md:634-638.

**Recommendation**: Say the case is unreachable by construction (`Parser.cs:78`, `:130`); drop the deadline claim for the stream consumer.

**Fix type**: rephrase

---

### 9. `InvalidOperationException` widening vs NFR-5's "never widens" (Score: 40)

**ADR**: 0078

**Recommendation**: One sentence: NFR-5's "never widens" is met in its stated sense (subscription still created, no other call affected); the catch type is accepted as the narrowest nameable one.

**Fix type**: rephrase

---

### 10. `GcpIamCallTolerance` public justification reads against Alternative 6 (Score: 35)

**ADR**: 0078

**Recommendation**: Add "and it is used by production code"; cite `.agent_instructions/testing.md:109-111` (no `InternalsVisibleTo`) instead of "none exists under `src/`".

**Fix type**: rephrase

---

### 11. Router threading assumption not stated (Score: 35)

**ADR**: 0078

The non-thread-safe lazy producer cache is safe only because each performer has its own consumer (`GcpPubSubConsumerFactory.cs:83`, `:97-99`).

**Recommendation**: One sentence stating the single-performer-per-consumer assumption.

**Fix type**: clarify-add

---

### 12. 0077 still says 0078 is "to be drafted" (Score: 35)

**ADR**: 0077 (`:28`, `:316`)

**Recommendation**: Replace with a reference to `0078-gcp-rejection-routing-and-dlq-channel-creation` (Proposed).

**Fix type**: rephrase

---

### 13. Two drifted line citations (Score: 30)

**ADR**: both

0078 `Parser.cs:292-295` for `OrderingKey` (assignment at `:296`); 0077 "bag read `:69`" in `SqsMessageCreator` (`ReadMessageBag` at `:70`). ~60 other citations spot-checked and correct.

**Fix type**: rephrase

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 7 |

**Total findings**: 13
**Findings at or above threshold (60)**: 4

Verified by the reviewer and not filed: providers map `deadLetterRoutingKey` onto `DeadLetterPolicy` today; conformance templates pass `Create`; `Assume` returns at once in `EnsureSubscriptionExistsAsync`; stream settle calls are `TrySetResult` and cannot throw; "Reject returns true" conflicts with no AC; the `s_ignoreHeaders`/`HandledCount`/discriminator constraints are consistent across both ADRs; every ADR MUST and every "not writable until the ADR exists" AC (AC-11, AC-18/43, AC-21, AC-33, AC-34, AC-42) is discharged.

## Main-agent validation (round 1)

Summary counts recounted: High 82/76/70, Medium 68/58/55, Low 48/45/40/35/35/35/30 — 13 total, 4 ≥ 60. Correct.

Each ≥ 60 finding re-verified in source:
1. **Confirmed.** `GcpPullMessageGatewayProvider.cs` is `namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;` with `using …MessagingGateway.Pull;` (`:35`, `:41`) and `new RejectionMetadataKeys(` at `:364`; generated record at `Pull/Generated/RejectionMetadataKeys.cs:5,19`. 0077 `:70` declares the core type. Enclosing-namespace lookup precedes compilation-unit usings — shadowing is real.
2. **Confirmed.** `GcpPullMessageGatewayProvider.cs:141-159` builds `DeadLetterPolicy(deadLetterRoutingKey, …) { MaxDeliveryAttempts = 5 }` only when a routing key is passed; 0078 step 5 removes that mapping without saying what replaces it.
3. **Confirmed.** `GcpPubSubChannelFactory.cs:54-55` delegates `CreateAsyncChannel` to `CreateAsyncChannelAsync`; 0077 `:222` places the call in both.
4. **Confirmed.** `GcpPubSubConsumerFactory.cs:89-95` — `s_consumers.GetOrAdd(pubSubSubscription, …)` then `consumer.Start()`.

## Remediation (round 1) — the user's decisions and what was applied

Decisions (user, 2026-09-24): F1 rename the **core** type (harness record keeps its name); F2 keep a
native `DeadLetterPolicy` on a distinct topic alongside the Brighter route; apply every below-threshold
finding (F5–F13). All fixes are rephrases or one-clause additions — no
requirement, AC or NFR text was touched.

Applied, each verified by grepping the text back from the ADR (not from script output):

| # | ADR | Applied text (grep anchor) |
|---|---|---|
| 1 | 0077 | `RejectionMetadataKeys` → `RejectionMetadataKeyNames`, all 7 occurrences (0 old remain) |
| 1 | 0078 | "0077's core `RejectionMetadataKeyNames`"; step 5 "generated harness record `RejectionMetadataKeys` … distinct from 0077's core `RejectionMetadataKeyNames`" |
| 2 | 0078 | step 5: routing keys to the Brighter route **plus** `DeadLetterPolicy { MaxDeliveryAttempts = 5 }` on `{deadLetterRoutingKey}.native`; no-routing-key subscriptions unchanged |
| 2 | 0077 | step 7: "(GCP's native-policy shape: 0078 Implementation step 5)" |
| 3 | 0077 | R-26: "once on each non-delegating channel-creation path"; sites AWS `CreateSyncChannelAsync` `:204` / `CreateAsyncChannelAsync` `:92`, GCP `CreateSyncChannel` `:26` / `CreateAsyncChannelAsync` `:65` (verified in source) |
| 4 | 0078 | provisioning subscription uses "`SubscriptionMode.Pull` on every configuration" |
| 5 | 0078 | "disposal is inside the same guarded region"; unverified row adds dispose-completes-promptly |
| 6 | 0077 | "Input: AC-39's measurement (the selector is this ADR's amended conclusion …)" |
| 7 | 0078 | R-21 members-unset: "This case therefore needs outbound network …" |
| 8 | 0078 | missing handle: "unreachable by construction"; "holds no lease" removed |
| 9 | 0078 | "met in its stated sense" |
| 10 | 0078 | public justification cites `testing.md:109-111` (verified: "No InternalsVisibleTo") |
| 11 | 0078 | router row: "not thread-safe, which is sound …" |
| 12 | 0077 | both "to be drafted" → `0078-…` (Proposed); 0 remain |
| 13 | both | `Parser.cs:296`; `bag read :70` (both verified in source) |

Index regenerated (no frontmatter change). Round 2 to follow.

---

# Review: design — 0037-delivery-count-and-rejection-routing (round 2)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Step 5's `.native` move leaves the DLQ reader and subscription names unspecified — Brighter-routed copy read from the wrong place or dropped (Score: 64)

Round 1's remediation moves the harness's native `DeadLetterPolicy` to topic `{deadLetterRoutingKey}.native` and has the providers "pre-provision each destination topic with a reading subscription", but does not say (a) that `GetMessageFromDeadLetterQueue(Async)` — which today reads `subscription.DeadLetter.Subscription` on `DeadLetter.TopicName` — must be re-pointed at the reading subscription on the Brighter topic `{deadLetterRoutingKey}` (with R=3 < M=5 the routed copy lands there, not on `.native`); nor (b) how the native policy's subscription is named. It is `new ChannelName(deadLetterRoutingKey.Value)` today; if the new reading subscription also takes the routing key's name, `EnsureSubscriptionExistsAsync`'s static per-name cache returns early, the Brighter topic is left without a subscription, and routed copies are discarded — the hazard 0078 itself lists under Negative consequences.

**Evidence**: 0078:281-282; `GcpPullMessageGatewayProvider.cs:143`, `:152`, `:290-292`, `:325-327`; `GcpPubSubMessageGateway.cs:201-205`; FR-23 and rejection-routing templates call `GetMessageFromDeadLetterQueue`.

**Recommendation**: In step 5: `GetMessageFromDeadLetterQueue(Async)` reads the reading subscription on `{deadLetterRoutingKey}`; the native policy's subscription is also named `{deadLetterRoutingKey}.native`; AC-9/AC-43's native read keeps `DeadLetter.Subscription`.

**Fix type**: ADR addition

---

### 2. AC-18's Given: "identical but with Assume" makes the subscription under test Pull on Stream configurations and leaves broker-side attributes unassigned (Score: 62)

0078:185 gives the provisioning subscription `SubscriptionMode.Pull` "on every configuration"; 0078:186 makes the subscription under test "identical but with `makeChannels: OnMissingChannel.Assume`" — read literally, Pull on `GCP / Stream` and `GCP / StreamOrdering`, so AC-18/AC-43 would exercise the pull consumer there. Also, under `Assume` `EnsureSubscriptionExistsAsync` returns immediately (`:188-192`), so the broker learns the ack deadline, ordering and `DeadLetterPolicy` only from the provisioning subscription. AC-18's release proof rests on `AckDeadlineSeconds: 60`; the provider's non-DLQ shape uses `10` (`GcpPullMessageGatewayProvider.cs:166`), so an unreleased message would return inside `W` = 10 s and AC-18 could pass vacuously.

**Evidence**: 0078:185-186; `GcpPubSubMessageGateway.cs:188-192`; `GcpPubSubConsumerFactory.cs:80-85`; requirements AC-18 "on the pull consumer `AckDeadlineSeconds: 60`".

**Recommendation**: (2) the subscription under test is the configuration's own subscription, own `SubscriptionMode`, `makeChannels: Assume`; (1) the provisioning subscription differs only in `Create` + `Pull` and carries every broker-side attribute the scenario relies on (`AckDeadlineSeconds`, `EnableMessageOrdering`, AC-43's `DeadLetterPolicy`).

**Fix type**: ADR rephrase

---

### 3. RocketMQ AC-24: `ReadHandledCount` runs before the bag exists (Score: 50)

0077:194 says `ReadHandledCount` uses `DeliveryCount.Resolve(header, view.DeliveryAttempt, bag)`, but `ReadHandledCount(message)` (static, `MessageView` only, `RocketMessageConsumer.cs:422`) is called at `:292` before the header is built; `header.Bag` is filled from `message.Properties` afterwards. The ADR records the equivalent ordering for SQS and GCP but not RocketMQ.

**Recommendation**: resolve after the bag loop: `header.HandledCount = DeliveryCount.Resolve(header.HandledCount, view.DeliveryAttempt, header.Bag)`.

**Fix type**: ADR rephrase

---

### 4. Async path's post-`Failed` producer disposal unspecified (Score: 42)

0078:135 puts disposal in the guarded region but not that `RouteAsync` uses `DisposeAsync`; `GcpMessageProducer.Dispose` is sync-over-async (`GcpMessageProducer.cs:141-143` vs `:150-152`).

**Recommendation**: "`Route` uses `Dispose`, `RouteAsync` uses `DisposeAsync`."

**Fix type**: ADR rephrase

---

### 5. "Created once per consumer" contradicts re-creation after `Failed` (Score: 40)

0078:173 vs 0078:135.

**Recommendation**: "created on first use, and again after each `Failed` outcome".

**Fix type**: ADR rephrase

---

### 6. 0077 says the inline creator reads the handled count "before the bag and topic" (Score: 35)

`SqsInlineMessageCreator.cs` reads topic `:55`, handled count `:60`, bag `:76`.

**Recommendation**: "before the bag (`:60` vs `:76`)".

**Fix type**: ADR rephrase

---

### 7. `GcpIamCallTolerance` contract omits `public` on two methods (Score: 35)

0078:229, :232 — `TryCallAsync`, `TryCreateProjectsClientAsync` have no modifier; `IsTolerated` is `public`.

**Recommendation**: mark both `public`.

**Fix type**: ADR rephrase

---

### 8. Line citation drift (Score: 25)

0077:185 cites `GcpPubSubConsumerFactory.cs:88-91`; the derivation is at `:89-92`.

**Fix type**: ADR rephrase

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 5 |

**Total findings**: 8
**Findings at or above threshold (60)**: 2

## Regression check (round 2)

- 0077 R-26 site rule — OK (delegation verified: AWS `:82-83`, GCP `:54-55`; RocketMQ none delegate).
- 0077 step 7 pointer — OK (target incomplete: finding 1).
- 0078 step 5 `.native` policy — **finding 1**.
- Provisioning `SubscriptionMode.Pull` — **finding 2** (mode correct; "identical" wording is the problem).
- Router dispose guard — OK (async detail: finding 4).
- Missing-handle paragraph — OK (`Parser.cs:78`, `:130`).
- `RejectionMetadataKeyNames` rename — OK; the only `RejectionMetadataKeys` (0078:284) names the harness record.
- ADR MUST cross-check — all obligations discharged; no contradiction between the ADRs.

## Main-agent validation (round 2)

Summary recounted: Medium 64/62/50, Low 42/40/35/35/25 — 8 total, 2 ≥ 60. Correct.

1. **Confirmed.** `GcpPullMessageGatewayProvider.cs:143` `dlqChannelName = new ChannelName(deadLetterRoutingKey.Value)`, `:152` policy; both DLQ readers (`:290-292`, `:325-327`) read `subscription.DeadLetter!.Subscription`/`TopicName`; `GcpPubSubMessageGateway.cs:201-205` name cache returns early. 0078:281-282 is silent on both.
2. **Confirmed.** 0078:185-186 wording as quoted; `:188-192` Assume returns before any create/update; provider non-DLQ shape `ackDeadlineSeconds: 10` (`:166`).

**Pattern flag (PROMPT.md rule):** both ≥ 60 findings sit on sections round 1 changed — step 5 (round 1 F2) and the AC-18 Given (round 1 F4). Second round running on the same sections → user asked whether to patch the ADR or defer to tasks/tests.

## Remediation (round 2): the user's decisions and what was applied

Decisions (user, 2026-09-25): patch F1 and F2 in the ADR **once**, as short clauses. If round 3 finds
0078 step 5 or the AC-18 Given again, they move to tasks. Apply all the below-threshold findings (F3–F8).
No requirement, AC or NFR text was touched.

Every change was verified by grepping the text back from the ADR, not from the script's output:

| # | ADR | Applied text (grep anchor) |
|---|---|---|
| 1 | 0078 | step 5: "The native policy's subscription is named `{deadLetterRoutingKey}.native` too … per-name creation cache (`GcpPubSubMessageGateway.cs:201-205`)"; "`GetMessageFromDeadLetterQueue(Async)` reads the one on `{deadLetterRoutingKey}`, not `subscription.DeadLetter` (AC-9/AC-43's native read keeps `DeadLetter.Subscription`)" |
| 2 | 0078 | Given (1): "differs from the subscription under test only in `makeChannels: Create` and `SubscriptionMode.Pull` … carries every broker-side attribute … (`AckDeadlineSeconds`, `EnableMessageOrdering`, and AC-43's `DeadLetterPolicy`)"; (2): "the configuration's own subscription, keeping its own `SubscriptionMode`, with `makeChannels: OnMissingChannel.Assume`" |
| 3 | 0077 | AC-24: "`HandledCount` is resolved after the bag loop (`RocketMessageConsumer.cs:328`) … `header.HandledCount = DeliveryCount.Resolve(header.HandledCount, view.DeliveryAttempt, header.Bag)`" (`:292`, `:328` verified) |
| 4 | 0078 | "`Route` uses `Dispose`, `RouteAsync` uses `DisposeAsync`" (`GcpMessageProducer.cs:141`, `:150` verified) |
| 5 | 0078 | NFR-3: "created on first use, and again after each `Failed` outcome" |
| 6 | 0077 | AWSSQS row: "**before** the bag (`:60` vs `:76`) … so `Resolve` can see it." |
| 7 | 0078 | `public Task<(bool completed, T? result)> TryCallAsync<T>(`, `public Task<ProjectsClient?> TryCreateProjectsClientAsync(` |
| 8 | 0077 | `GcpPubSubConsumerFactory.cs:89-92` (verified) |

Old text: "88-91", "once per consumer" and "identical but with" now have 0 occurrences in both ADRs. Frontmatter is unchanged, so the index was not regenerated.

---

# Review: design — 0037-delivery-count-and-rejection-routing (round 3)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address these before approving.

## Findings

### 1. RocketMQ DLQ copy's `HandledCount` is overwritten by the stale bag entry — R-5 / R-28 fail on the AC-24 branch (Score: 74)

0077 says the RocketMQ DLQ copy carries `HandledCount` via `RocketMqMessagePublisher.cs:103` (R-28 table `:127`; "What the dead-letter copy carries" `:60`). But the publisher writes the header count at `:103`, then copies every `Header.Bag` entry as a property (`:54-59`, filtering only `Keys`, `Tag`, local headers). The consumer fills the bag with every broker property, `HandledCount` included (`RocketMessageConsumer.cs:328-331`, unfiltered), and RocketMQ.Client's `AddProperty` overwrites. So the DLQ copy is published with the **received** count, not the stamped `b(R) ≥ R`; on a DLQ read `Resolve` sees `rejectionReason` and keeps that stale count — breaking R-5's `≥ R − 1`, R-28, and the FR-23 template's `dlqMessage.Header.HandledCount >= deliveriesExpected`. SQS avoids this (sender rewrites the bag entry, `SqsMessageSender.cs:130`, `SnsMessagePublisher.cs:110`); GCP too (`HandledCount` in `s_ignoreHeaders`, `Parser.cs:17`; `AddHeaders` skips written keys, `Parser.cs:352`).

**Evidence**: `RocketMqMessagePublisher.cs:40`, `:54-59`, `:103`; `RocketMessageConsumer.cs:328-331`; `RocketMQ/HeaderNames.cs:47`; requirements R-5 (`:198-201`), R-28 (`:245-262`).

**Recommendation**: one clause in 0077's AC-24 bullet and R-28 row: keep the header-owned `HandledCount` out of the republished properties — consumer bag loop skips `HeaderNames.HandledCount` (GCP `s_ignoreHeaders` shape) or publisher bag loop skips keys already written (GCP `!headers.ContainsKey` shape); correct the R-28 row's claim that `:103` alone carries the count.

**Fix type**: ADR addition

---

### 2. The four redelivery behaviours have no "first-delivery arm" (Score: 45)

0077 `:142` "first-delivery arms keep exact equality" and `:161` "FR-2/15/16/22's first-receive arms": each of the eight templates calls `_messageAssertion.Assert` exactly once, redelivered vs the sent message. R-2 still bites via other `Pass` behaviours asserting a first receive.

**Recommendation**: say these templates compare the redelivered message with the sent one only; drop the two clauses.

**Fix type**: ADR rephrase

---

### 3. Citation drift and one ambiguous shorthand (Score: 25)

0078 `:198` "(`:124`/`:151`)" → `Credential = Credential` is at `GcpMessagingGatewayConnection.cs:126`/`:153`; 0078 `:185` "(`:220-236`)" follows a `GcpPubSubConsumerFactory.cs` cite but means `GcpPubSubMessageGateway.cs:220-236`; 0077 `:192` `RocketMessageConsumer.cs:181` is `Requeue` reading the handle — it is set at `:333`.

**Fix type**: ADR rephrase

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 3
**Findings at or above threshold (60)**: 1

## Regression check (round 3)

All round-2 changes OK: step 5 (`.native` cache key from `DeadLetter.Subscription`, `GcpPubSubMessageGateway.cs:201-205`, `:223-231`; reader change stated); AC-18/AC-43 Given (Assume `:188-192`; stream consumer only for non-Pull, `GcpPubSubConsumerFactory.cs:80-95`); RocketMQ AC-24 resolve-after-bag (`:292`, `:328`); router Dispose/DisposeAsync; NFR-3 paragraph; `GcpIamCallTolerance` public; AWSSQS row and `:89-92`. **Step 5 and the AC-18 Given drew no finding — the "move to tasks" rule is not triggered.** ADR MUST cross-check: all discharged, no contradictions; 0078 honours 0077's three constraints.

## Main-agent validation (round 3)

Summary recounted: High 74, Low 45/25 — 3 total, 1 ≥ 60. Correct.

1. **Confirmed.** `RocketMqMessagePublisher.cs:40` `AddHeaderProperties` (writes `HandledCount` at `:103`), then `:54-59` bag loop with only `Keys`/`Tag`/local filters; `RocketMessageConsumer.cs:328-331` copies every property into the bag unfiltered. Not a regression — new defect on the DLQ side of the AC-24 branch.
2. (Low) Confirmed: `grep -c _messageAssertion.Assert` = 1 in all eight templates.
3. (Low) Confirmed: `GcpMessagingGatewayConnection.cs:126`, `:153`.

## Remediation (round 3): the user's decision and what was applied

Decision (user, 2026-09-25): F1 is fixed on the **publisher** side. RocketMQ's bag loop skips any key
`AddHeaderProperties` already wrote, following GCP's `!headers.ContainsKey` shape (`Parser.cs:352`, verified).
The lows were applied too. No requirement, AC or NFR text was touched.

Every change was verified by grepping the text back from the ADR:

| # | ADR | Applied text (grep anchor) |
|---|---|---|
| 1 | 0077 | AC-24: "the publisher's bag loop (`RocketMqMessagePublisher.cs:54-59`) skips any key `AddHeaderProperties` already wrote … so the stamped `HandledCount` (`:103`) reaches the DLQ copy instead of the stale bag entry" |
| 1 | 0077 | R-28 RocketMQ row: "provided the publisher's bag loop (`:54-59`) does not overwrite it … — the AC-24 change below" |
| 1 | 0077 | "What the dead-letter copy carries": "`RocketMqMessagePublisher.cs:103` — kept only once its bag loop skips keys already written, AC-24 below" |
| 2 | 0077 | "these templates compare the redelivered message with the sent one only, so R-2 still bites through the other behaviours that assert a first receive"; residual risk: "(not FR-2/15/16/22, whose templates compare the redelivered message with the sent one only)" |
| 3 | 0077 | AC-23: "(set at `RocketMessageConsumer.cs:333`)" |
| 3 | 0078 | "(`GcpMessagingGatewayConnection.cs:126`/`:153`)"; "(`GcpPubSubMessageGateway.cs:220-236`)" |

Old text: "first-delivery arms keep", "first-receive arms", "`:181`)" and "`:124`/`:151`" now have 0 occurrences. Frontmatter is unchanged.
