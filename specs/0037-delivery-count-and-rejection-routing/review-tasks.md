# Review: tasks — 0037-delivery-count-and-rejection-routing (round 1)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

## Findings

### 1. The 7.3 test goes red when 7.10 lands on the AC-24 branch (Score: 75)

Task 7.3 sits in "Phase 7 common — both branches". Its test publishes a message with `Header.HandledCount = 3` and a stale `Bag[HandledCount] = "0"`, then asserts it is "received back with `HandledCount == 3`" through the consumer. The bag carries no `rejectionReason`. On the AC-24 branch, 7.10 makes the consumer set `header.HandledCount = DeliveryCount.Resolve(header.HandledCount, view.DeliveryAttempt, header.Bag)`. With no discriminator that returns the normalised broker count (0 on first delivery), not 3. The common test is therefore valid only on AC-25, or until 7.10 lands.

**Evidence**: 7.3 "received back with `HandledCount == 3`"; 7.10 `Resolve(...)`; 2.2 (no discriminator → broker count − 1); ADR 0077 :88-91, :194.

**Recommendation**: give the test the routed copy's shape (`rejectionReason` in the bag), or assert on the raw wire property (`MessageView.Properties[HandledCount]`). Say explicitly that it stays green on both branches.

**Fix type**: task rephrase

---

### 2. The Phase 2 validation tests are placed in `Core.Tests`, which cannot run them as written, and an existing test will break (Score: 72)

2.3, 2.4, 2.5 and 2.7 put tests in `tests/Paramore.Brighter.Core.Tests/Validation` that verify through `.ValidatePipelines(throwOnError: true)` and "host starts". The rules reach `ValidatePipelines` only through `RegisterConsumerValidationSpecs`, which `AddConsumers` calls in `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection`. `Core.Tests` does not reference that project. The host-level tests live in `Paramore.Brighter.Extensions.Tests`, where `When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:81` asserts `Assert.Equal(4, specs.Count)`, which becomes 7 and is not tasked.

**Evidence**: `Core.Tests.csproj` ProjectReferences; `ServiceCollectionExtensions.cs:29`, `:62`, `:78`, `:129`; `BrighterPipelineValidationExtensions.cs:79`.

**Recommendation**: split each rule's test in two. Test the specification directly in `Core.Tests/Validation`, following the existing pattern. Test registration, `ValidatePipelines` surfacing and AC-32's "host starts" in `Extensions.Tests`. Add the 4 → 7 spec-count update explicitly.

**Fix type**: task rephrase

---

### 3. Many TEST + IMPLEMENT tasks are expected to be green on first run, which contradicts the mandatory RED-first rule (Score: 70)

`.agent_instructions/testing.md:67` requires RED "for the right reason before any production code exists", in both gears. The following tasks' implementation sections say "Expect green from 4.3", "Need no production change" or similar: 2.7; 4.4, 4.6, 4.7, 4.8, 4.9, 4.10; 6.5, 6.6, 6.12, 6.14, 6.15, 6.30; 7.5, 7.6, 7.11, 7.12. They fall into two groups:
- **Can be RED if reordered:** 4.4, 4.6 and 4.7 fail today, so they can come before 4.3. The same holds for 6.12 before 6.10, 6.14 before 6.11, and 7.11 before 7.10.
- **Pure regression guards,** green before and after: 4.8, 4.9, 4.10, 6.5, 6.6, 7.5 and 7.6.

**Recommendation**: reorder the RED-able ones ahead of their enabling task, or fold them into it. Give the pure guards an explicit task kind, or get a user decision.

**Fix type**: reorder/dependency

---

### 4. Task 1.5 cites `Reject`'s lines as `Acknowledge`, which leaves the helper's source and semantics ambiguous (Score: 62)

1.5 says to extract `AckByHandle` "from `Acknowledge` (`:285/288`, `:315/317`)". Those lines are inside `Reject` (`:276`) and `RejectAsync` (`:306`). `Acknowledge` is at `:29` and `AcknowledgeAsync` at `:55`, and the two pairs log differently. The helper's failure contract is also unstated, yet 5.8 relies on it.

**Recommendation**: cite `Acknowledge` `:29` / `AcknowledgeAsync` `:55` as the source, and state the helper's contract: it throws as today, and the caller decides.

**Fix type**: task rephrase

---

### 5. AC-11's first clause is never tested on a real `GcpPubSubSubscription` under `.ValidatePipelines()` (Score: 56)

2.5 uses a core test double, 6.1 checks only the predicate, and 6.2 checks only channel-creation logging.

**Recommendation**: add a clause to 6.1 (or a small Gcp.Tests test) that runs `ValidatePipelines` over a real `GcpPubSubSubscription` with `DeadLetter == null` and `requeueCount: 3`.

**Fix type**: task rephrase

---

### 6. The settle-failure behaviour is implemented in 5.8 without a failing test; its only test comes later in the 5.10 GATE (Score: 55)

**Recommendation**: move the settle-failure bullet into 5.10, or order 5.10's fault-injection tests so they run RED first. They stay GATE evidence.

**Fix type**: reorder/dependency

---

### 7. 6.2 covers AC-29 for R-11 only; the R-7 and R-10 channels are never exercised on the receive path (Score: 52)

**Recommendation**: make 6.2's negative case an R-7 (`requeueCount: 0`) and/or R-10 subscription, and assert zero budget Warnings over the 2 × 100 receives.

**Fix type**: task rephrase

---

### 8. 5.5 is large for one session (Score: 45)

It bundles the router, the ctor parameters, the factory wiring, disposal and the `Reject` composition.

**Recommendation**: consider splitting the router's publish/stamp behaviour from the consumer's `Reject` composition.

**Fix type**: task split

---

### 9. 6.30 on the AC-40 branch does not depend on 6.4 (Score: 40)

ADR 0078 step 7 orders AC-43 "after 0077's GCP parser work".

**Recommendation**: add 6.4 to 6.30's dependencies.

**Fix type**: reorder/dependency

---

### 10. 5.7's behaviour is already implemented by 5.5 (Score: 35)

**Recommendation**: narrow 5.7 to the Warning, or merge it into 5.5.

**Fix type**: task rephrase

---

### 11. 1.5 goes beyond ADR 0078 step 1 (Score: 30)

It adds stream `Accept`/`Nack` helpers that step 1 does not mention.

**Recommendation**: trace them to 5.6 or 5.8, or drop them.

**Fix type**: task rephrase

---

### 12. 8.5 has no "Depends on" line (Score: 25)

**Recommendation**: add "Depends on: 6.7, 7.1, Phases 4–7".

**Fix type**: reorder/dependency

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 5 |

**Total findings**: 12
**Findings at or above threshold (60)**: 4

## Coverage check

- No R-n, NFR-n, AC-n or ADR step is left without a task; AC-11 (#5) and AC-29 (#7) are covered only partially.
- Ledger state verified (`conformance-status.md:819-839`): the GCP cells are `Deferred -> #4240`, the AWS FR-23 cells `Deferred -> #4341`, and RocketMQ has nine `Fixed` cells, matching 7.30.
- The oracle change (3.1) lands before 4.3, and 3.5 reruns AWS and RocketMQ first.
- The cross-branch dependencies of 6.30, 6.31, 7.30, 8.1 and 8.6 hold on either branch, apart from #1 and #9.
- Coverage-table claims spot-checked as OK: AC-3, 12, 20, 22, 30, 36, 38, 42. AC-11, AC-29 and AC-7/10/32 are covered by findings #5, #7 and #2.
- All other cited line numbers were verified correct; the only wrong citation is 1.5's (#4).

## Main-agent validation (round 1)

Summary recounted: High 75/72/70, Medium 62/56/55/52, Low 45/40/35/30/25. That is 12 total, 4 ≥ 60. Correct.

1. **Confirmed.** tasks.md:838 asserts the resolved `HandledCount == 3` through the consumer with no `rejectionReason`; on AC-24, 7.10's `Resolve` path overrides it.
2. **Confirmed.** The `Core.Tests.csproj` ProjectReferences do not include ServiceActivator.Extensions.DependencyInjection. `AddConsumers` → `RegisterConsumerValidationSpecs` is at `ServiceCollectionExtensions.cs:29/62`. `Extensions.Tests/When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:81` has `Assert.Equal(4, specs.Count)`.
3. **Confirmed.** `testing.md:67-68`: "RED first … observed to fail for the right reason before any production code exists", in both gears.
4. **Confirmed.** `GcpPullMessageConsumer.cs:276` `Reject`, `:288` `client.Acknowledge(...)` inside it; `Acknowledge` at `:29`, `AcknowledgeAsync` at `:55`.

## Remediation (round 1): the user's decisions and what was applied

Decisions (user, 2026-09-25):
- **F3.** Use the `CHARACTERISE` convention from branch `feature/4334-validate-subscription-channel-factory` (`testing.md:46-73` there; not on this branch), with a self-contained procedure note in `tasks.md`.
- **Tasks green on arrival.** Every task expected green on first run becomes `CHARACTERISE` with a named production mutation. The enabling tasks (4.3, 6.10, 6.11, 7.10) keep real RED. Reordering alone was rejected, because one production change turns a whole group green and only the first test in the group would see RED.
- **F1, F2, F4–F12.** Apply them all.

The edits were made by a sub-agent. Every anchor was grepped back from the file by the main agent:

| # | Applied text (grep anchor) |
|---|---|
| F3 | "CHARACTERISE procedure." note under Task kinds; "Counts: 79 tasks"; 17 tasks converted (2.7, 4.4, 4.6–4.10, 6.5, 6.6, 6.12, 6.14, 6.15, 6.30, 7.5, 7.6, 7.11, 7.12), each with a 🔁 line naming its RED mutation and the "once RED is observed" gate line |
| F1 | 7.3: "shaped like a routed dead-letter copy"; "re-run it after 7.10 or 7.20" |
| F2 | 2.3: "(two tests; `Core.Tests` does not reference …"; 2.4, 2.5 and 2.7: "split as in 2.3"; spec-count bumps 2.3 → 5, 2.4 → 6, 2.5 → 7 |
| F4 | 1.5: "Helper contract: each helper wraps **only** …"; sources `:29`/`:55`/`:276`→`:288`/`:306`→`:317` |
| F5 | 6.1: `When_validating_a_gcp_subscription_without_dead_letter_policy…`, in `Gcp.Tests`, through the real `PipelineValidator` (`Gcp.Tests` cannot reach `AddConsumers`, and `Extensions.Tests` has no GCP reference) |
| F6 | 5.8's settle-failure bullet removed (0 remain); "5.10 TEST + IMPLEMENT: A failed ack or release RPC …"; "Record the evidence:" kept |
| F7 | 6.2: "an R-7 subscription (`requeueCount: 0`) and an R-10 subscription" |
| F8 | 5.5 split into 5.5a/5.5b; see the correction below |
| F9 | 6.30: "Depends on: 5.8, 6.4, 6.16 or 6.20" |
| F10 | 5.7: "The test's RED comes from the Warning assertion" |
| F11 | 1.5: "(used by 5.6's accept after routing and 5.8's Nack on failed routing)" |
| F12 | 8.5: "Depends on: 6.7, 7.1, Phases 4–7" |

**Main-agent correction to F8.** The sub-agent's first split tested the internal router directly, via an `InternalsVisibleTo` bullet. `testing.md:109-111` forbids that, and ADR 0078 relies on the same rule to make `GcpIamCallTolerance` public. It was replaced by a split along behaviour visible through the consumer's public `Reject`:
- **5.5a:** a delivery-error `Reject` publishes a stamped copy to the DLQ, acks the original and returns `true`. This covers the router, the ctor parameters, the factory wiring, disposal and the `Reject` composition.
- **5.5b:** `Unacceptable` goes to the invalid channel, falling back to the DLQ, and `NoDestination` is reported when neither key is set.

The ADR step 3/4 coverage rows were updated to match; `InternalsVisibleTo` now has 0 occurrences.

**Checks, from the file:**
- 79 tasks: 36 TEST + IMPLEMENT, 17 CHARACTERISE, 4 TIDY, 17 GATE, 5 MEASURE.
- Every TEST + IMPLEMENT and CHARACTERISE task has exactly one `/test-first` and one ⛔ line, and every CHARACTERISE task has a 🔁 mutation line. No GATE, MEASURE or TIDY task has a ⛔ line.
- Every cited task id resolves, and no bare `5.5` reference remains.
- Mutation targets spot-checked in the source: `MessagePump.cs:171-174`, `Message.cs:161-164`, `Reactor.cs:498`, `Proactor.cs:504`, `ConsumerValidationRules.cs:114`.

---

# Review: tasks — 0037-delivery-count-and-rejection-routing (round 2)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address these before approving.

## Findings

### 1. 6.15's mutation (a) cannot turn the GCP test RED, so its main assertion has no working mutation (Score: 65)

6.15's mutation (a) is "`Resolve` ignores the discriminator → the DLQ copy presents `0`". On GCP, however, the Brighter DLQ is read through 5.4's pre-provisioned reading subscription, which has no `DeadLetterPolicy`. Pub/Sub therefore leaves `DeliveryAttempt` unset (A-1), and 2.2's "broker count `null`, `0` or negative → header count" returns the stamped 3. Mutation (b), no stamping, fails only the metadata clause. So no named mutation fails the R-28/AC-41 count assertion, and on GCP the test does not exercise the "DLQ's own counter" half of R-28 the way its SQS (4.6) and RocketMQ (7.12) twins do.

**Evidence**: 6.15 🔁 line; tasks.md:498 (5.4, reading subscription); tasks.md:97, :111 (2.1, 2.2); ADR 0078:282; AC-41.

**Recommendation**: (i) read the Brighter DLQ through a subscription that carries a `DeadLetterPolicy`, so its own counter is populated and mutation (a) bites; or (ii) replace (a) with a publish-side mutation (`Parser.cs:307` stops writing `HandledCount`) and note that the discriminator is exercised on GCP only when the DLQ subscription has a policy.

**Fix type**: task rephrase

---

### 2. 6.4's RED depends on 6.3's measurement, and the Given that puts the attribute on the message is not specified (Score: 52)

The pull path exposes `DeliveryAttempt` as a field, not an attribute, and stream injection by `SubscriberClient` is 6.3(i)'s open question. Pub/Sub may also reserve `goog*` attribute keys (6.3(ii)).

**Recommendation**: state the Given for each 6.3 outcome. Publish the attribute explicitly if the emulator accepts it; otherwise use a DLQ-backed stream subscription, and the pull clause becomes CHARACTERISE (mutation: remove the `s_ignoreHeaders` entry).

**Fix type**: task rephrase

---

### 3. 5.6's "RejectAsync is genuinely async" clause cannot be observed through the public API (Score: 45)

The router is internal and `InternalsVisibleTo` is forbidden; the public observables are identical either way.

**Recommendation**: move the clause to "Implementation should", checked by code review, or name an observable (the returned task does not complete synchronously while the publish is pending).

**Fix type**: task rephrase

---

### 4. 1.5 says the client lookup goes "inside each helper's `try`", but the helper contract gives helpers no try/catch (Score: 40)

**Recommendation**: "each helper performs the client lookup and the RPC with no try/catch of its own; every caller invokes it inside its existing `try`".

**Fix type**: task rephrase

---

### 5. 4.8's mutation is contrived rather than the realistic defect (Score: 30)

`HandledCountReached(Math.Min(RequeueCount, 2))` is valid, but it is an arbitrary constant.

**Recommendation**: use an over-counting `Resolve` mutation instead, or keep it with a note on why.

**Fix type**: task rephrase

---

### 6. 6.30 mutation (b) fails on "arrival", not on "topic still absent" as stated (Score: 30)

**Recommendation**: "fails on 'the message arrives' (count at or below M); the topic now exists".

**Fix type**: task rephrase

---

### 7. Leftover cross-references from the 5.5 split (Score: 25)

5.5b's RED comes only from the `.Invalid` clause, because `NoDestination` exists from 5.5a. 5.10 says "composed in 5.5b/5.8" where it should say 5.5a.

**Recommendation**: note the RED source in 5.5b, and change 5.10's "5.5b/5.8" to "5.5a/5.8".

**Fix type**: task rephrase

---

### 8. 6.1's second test needs the R-11 rule from 2.5, but 6.1 depends only on 2.4 (Score: 25)

**Recommendation**: "Depends on: 2.5, 5.1".

**Fix type**: reorder/dependency

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 6 |

**Total findings**: 8
**Findings at or above threshold (60)**: 1

## Regression check (round 2)

- All round-1 changes are OK apart from #1 and some minor wording (#4, #6, #7, #8):
  - F1 (7.3) is RED today and holds on AC-24.
  - F2's spec-count bumps are right: only 2.3, 2.4 and 2.5 register a spec.
  - F5: `Gcp.Tests` can construct `PipelineValidator` (ServiceActivator reference, csproj:39).
  - F6: 5.10 is genuinely RED, since today's `Reject` rethrows (`GcpPullMessageConsumer.cs:290-293`).
  - F8: 5.5b is RED on `.Invalid`, and `InternalsVisibleTo` appears nowhere.
- CHARACTERISE tasks: 16 of 17 are OK (their mutations are effective and their citations verified). 6.15 is #1. No CHARACTERISE task is RED on arrival, and the enabling tasks 4.3, 6.10, 6.11 and 7.10 are genuinely RED.

## Main-agent validation (round 2)

Summary recounted: Medium 65/52, Low 45/40/30/30/25/25. That is 8 total, 1 ≥ 60. Correct.

1. **Confirmed.** tasks.md:498: 5.4's reading subscription carries no policy. tasks.md:97 and :111: an unset counter normalises to 0, and `Resolve` then returns the header count. So 6.15's mutation (a) leaves the DLQ copy at the stamped 3, and the test stays green.

## Remediation (round 2): the user's decisions and what was applied

Decisions (user, 2026-09-25): F1 is fixed by giving 6.15's DLQ read a subscription that carries its own
`DeadLetterPolicy`, so the discriminator mutation bites and GCP exercises R-28's "not the destination's
own counter" half. Apply F2–F8, then run round 3.

The main agent applied the changes with exact-anchor replacements scoped to each task's block, and grepped every one back:

| # | Task | Applied text (grep anchor) |
|---|---|---|
| F1 | 6.15 | "over a reading subscription that **carries its own `DeadLetterPolicy`**"; "Depends on: 5.4, 6.10, 6.11" |
| F2 | 6.4 | "**Given, chosen by 6.3's outcome:**" (a) explicit publish; (b) `SubscriberClient` injection, pull clause characterised by removing the `s_ignoreHeaders` entry |
| F3 | 5.6 | The "genuinely async" clause moved from the test to Implementation: "checked at code review and by 8.2's broker-call review, not asserted" |
| F4 | 1.5 | "with **no try/catch of its own**; every caller invokes it inside its existing `try`" |
| F5 | 4.8 | Mutation (a) is now "`DeliveryCount.Resolve` (2.2) over-counts — returns `Normalise(brokerCount) * 5`" |
| F6 | 6.30 | Mutation (b) "fails on \"the message arrives\" … the routing-key topic now exists" |
| F7 | 5.5b, 5.10 | "RED comes from the `Unacceptable` → `.Invalid` clause …"; "composed in 5.5a/5.8" |
| F8 | 6.1 | "Depends on: 2.5, 5.1" |

Old text now has 0 occurrences: `Math.Min(RequeueCount, 2)`, "**inside** each helper's", "5.5b/5.8". The task count is unchanged at 79.

---

# Review: tasks — 0037-delivery-count-and-rejection-routing (round 3)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

## Findings

### 1. No task covers ADR 0078's missing-receipt-handle decision for the pull consumer, and the stream version is implemented with no test (Score: 72)

ADR 0078 decides the missing-handle case for **both** consumers: "The router still runs, so a configured destination still receives the message … The consumer then logs an Error that the original cannot be settled, and returns `true`." R-16 makes this an ADR MUST (requirements.md:634-638).

- **Pull.** Today the pull `Reject` returns `false` before doing anything when the handle is missing (`GcpPullMessageConsumer.cs:278-281`). A `false` makes the pump fall through to its ack, which discards the message. 5.5a's composition ("copy the handle first → route → `AckByHandle` … → return `true`") never says to remove that early return, and no task tests or changes the case. The coverage row "Step 4: … always returns `true`" overstates coverage.
- **Stream.** 5.6 puts the behaviour only under Implementation ("A missing handle still routes, logs an Error and returns `true`"). Its test ("The same clauses as 5.5a and 5.5b") never exercises it, so production code is written without a failing test.

The case is reachable through the public API: call `Reject` on a `Message` built without a `ReceiptHandle` bag entry, with a destination configured. The ADR calls it "unreachable by construction" only for parsed messages.

**Evidence**: ADR 0078 §"Missing receipt handle (both consumers)" (:169-171); `GcpPullMessageConsumer.cs:278-281`; tasks.md 5.5a, 5.6, Step 4 coverage row; requirements.md:634-638.

**Fix type**: task rephrase

**Recommendation**: Add a clause to 5.5a's test (no `ReceiptHandle`, DLQ key configured → destination receives the copy, an Error is logged, `Reject` returns `true`; RED today). Add to 5.5a's Implementation: replace the `:278-281` early return with route → log Error → `true`. Move 5.6's missing-handle bullet into its "Test should verify" list.

---

### 2. In 6.4's Given (b), the pull clause's named mutation cannot turn it RED, and there is no Given when neither 6.3 outcome holds (Score: 62)

Round 2 wrote that under (b) "the pull clause then carries the attribute only if published, so it is characterised by the mutation 'remove the `s_ignoreHeaders` entry'". Under (b) nothing publishes the attribute, and the pull path has no `SubscriberClient` to inject it (`Parser.ToBrighterMessage(ReceivedMessage)`, `Parser.cs:84`; `DeliveryAttempt` is a field, not an attribute). Removing the ignore entry leaves the pull assertion green: the mutation is ineffective and the clause vacuous.

There is also a third outcome with no Given: 6.3(i) finds no injection **and** the emulator refuses the published attribute. And 6.3's output never records whether the emulator *accepts* the publish, yet 6.4 branches on it.

**Evidence**: tasks.md 6.3 (i)/(ii) and Output; 6.4 Given; `Parser.cs:10`, `:12`, `:84`, `:125`.

**Fix type**: task rephrase

**Recommendation**: Under (b), mark the pull clause not applicable (the stream clause and routed-copy clause evidence the single shared `s_ignoreHeaders` entry). Add a (c) outcome for "no injection and publish refused": record in ADR 0077 that the attribute cannot reach the bag on this emulator and keep the ignore entry as a defensive change. Make 6.3 record whether the emulator accepted the publish in (ii).

---

### 3. 6.15 does not say its policy-carrying reading subscription must exist before the rejection is published (Score: 40)

Pub/Sub delivers only messages published after a subscription exists. 6.15's reading subscription is not the one 5.4 pre-provisions (5.4's has no policy). If created at DLQ-read time, after the pump, it never receives the copy — RED for a harness reason.

**Evidence**: tasks.md 6.15; 5.4.

**Fix type**: task rephrase

**Recommendation**: "Create the policy-carrying reading subscription (and its policy topic) before publishing and pumping; channel creation logs the two tolerated IAM Warnings (5.3)."

---

### 4. ADR 0078's "a failed producer creation is `Failed`, not no-destination" is never exercised (Score: 38)

Under `Validate`, a missing topic throws at producer creation, which must yield `Failed`, a release and `true`. 5.8's Given uses `Assume`, where creation succeeds and the publish fails, so the router's creation-failure catch is never tested.

**Evidence**: ADR 0078 "Divergence from SQS" and the `Validate` bullet; tasks.md 5.8.

**Fix type**: task rephrase

**Recommendation**: Add a `makeChannels: Validate` row to 5.8's test (same assertions), or record in 5.8 why it is omitted.

---

### 5. 1.5's round-2 wording ("no try/catch of its own") contradicts ADR 0078's literal text (Score: 30)

ADR 0078 says the client lookups "are called **inside** the helper's `try`". 1.5 now has helpers with no try, invoked inside the callers' existing `try`. The invariant holds, but the words disagree.

**Evidence**: tasks.md 1.5; ADR 0078 §"Reject composition".

**Fix type**: task rephrase

**Recommendation**: One sentence in 1.5 saying this is deliberate and preserves ADR 0078's invariant (or a dated ADR note).

---

### 6. 5.6's title still promises "RejectAsync is genuinely async" (Score: 25)

Round 2 moved the clause to Implementation as "not asserted"; the title still states it as tested behaviour.

**Evidence**: tasks.md 5.6 title vs Implementation.

**Fix type**: task rephrase

**Recommendation**: Drop "; RejectAsync is genuinely async" from the title.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 4 |

**Total findings**: 6
**Findings at or above threshold (60)**: 2

## Coverage check

- Every R-n / NFR-n / AC-n maps to at least one task. Partial coverage: R-16's missing-handle MUST (#1); ADR 0078's creation-failure → `Failed` (#4).
- ADR 0077 steps 1-7 and key decisions covered. ADR 0078 steps 1-8 covered except #1 and #4.
- No scope creep. Counts verified: 79 tasks (36 TEST + IMPLEMENT, 17 CHARACTERISE, 4 TIDY, 17 GATE, 5 MEASURE).
- All 17 CHARACTERISE mutations re-checked against source and effective; the only ineffective mutation is inside TEST + IMPLEMENT 6.4 (#2).

## Regression check (round 3)

| Round-2 edit | Result |
|---|---|
| 6.15 policy-carrying DLQ read; depends on 5.4, 6.10, 6.11 | OK — mutation (a) now bites (first-delivery attempt 1 → 0). Gap: creation ordering (#3) |
| 6.4 Given chosen by 6.3 | **Defective** (#2) |
| 5.6 async clause moved | OK; stale title (#6) |
| 1.5 helpers without try/catch | Consistent with code; wording diverges from ADR (#5) |
| 4.8 mutation `Resolve` ×5 | OK — fails on "native target holds the message" |
| 6.30 mutation (b) | OK |
| 5.5b RED note | OK |
| 5.10 "5.5a/5.8" | OK |
| 6.1 depends on 2.5 | OK |

## Main-agent validation (round 3)

Summary recounted: High 72; Medium 62; Low 40/38/30/25. That is 6 total, 2 ≥ 60. Correct.

1. **Confirmed.** `GcpPullMessageConsumer.cs:278-281` returns `false` when `ReceiptHandle` is absent; ADR 0078:169-171 decides route → Error → `true`; 5.5a's Implementation does not touch the early return; 5.6 carries the bullet only under Implementation.
2. **Confirmed.** The pull path parses `ReceivedMessage` (`Parser.cs:84`) and filters attributes at `:125`; no client-side injection exists on pull, so under Given (b) no attribute reaches the pull message and removing the `s_ignoreHeaders` entry cannot fail the pull clause. 6.3's Output records "both answers" but not whether the publish in (ii) was accepted.

## Remediation (round 3): the user's decisions and what was applied

Decisions (user, 2026-09-25): F2 takes the reviewer's fix (pull clause not applicable under (b); new outcome (c); 6.3 records whether the publish was accepted). Apply F1 and all Lows as recommended (F4 as a `Validate` row in 5.8; F5 as a sentence in 1.5, no ADR amendment). Then run round 4.

Applied with exact-anchor replacements scoped to each task's block, all asserted before the single write. Every applied text grepped back (1 occurrence each):

| # | Task | Applied text (grep anchor) |
|---|---|---|
| F1 | 5.5a | Test clause "**Missing receipt handle (ADR 0078 …)**" — RED today (`:278-281`, `RejectAsync` `:308-311`); Implementation "Replace the missing-handle early `return false` …" |
| F1 | 5.6 | "including 5.5a's missing-handle clause"; Implementation "(asserted by the missing-handle clause above)" |
| F2 | 6.3 | "record first whether the emulator **accepts** that publish"; Output "whether (ii)'s publish was accepted" |
| F2 | 6.4 | "The pull clause does not apply under (b)"; "(c) if 6.3 shows neither injection nor an accepted publish …" |
| F3 | 6.15 | "is created **before** the message is published and pumped" |
| F4 | 5.8 | "**Creation failure (ADR 0078 \"Divergence from SQS\"):** a second row … `makeChannels: Validate`" |
| F5 | 1.5 | "This deliberately moves ADR 0078's placement of the client lookup inside a `try` from the helper to its callers" |
| F6 | 5.6 | Title ends "then accepts the original**" |

Old text now has 0 occurrences: "RejectAsync is genuinely async", "carries the attribute only if published", "recording both answers". The task count is unchanged at 79.
