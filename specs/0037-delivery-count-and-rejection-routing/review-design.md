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
