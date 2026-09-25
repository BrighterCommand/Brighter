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
