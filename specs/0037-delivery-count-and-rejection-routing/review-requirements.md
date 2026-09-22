# Review: requirements — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

14 findings at or above threshold 60. Address these before approving.

## Findings

### 1. R-20's tolerance list excludes the exact status code spec 0036 measured on the emulator, so R-20 cannot deliver R-21 (Score: 92)

R-20 tolerates `Unimplemented` and `PermissionDenied` and states explicitly that "**`Unauthenticated`**, `NotFound`, `InvalidArgument`, `DeadlineExceeded` and every other status still propagate". Its own worked example then asserts that against the emulator "`GetProjectAsync` fails because Resource Manager is a different service that `PUBSUB_EMULATOR_HOST` cannot redirect. Channel creation logs one Warning per tolerated call and returns a usable channel."

Spec 0036's ledger — the document this spec cites as its measurement authority — recorded what that failure actually is, and it is a status R-20 forbids swallowing. R-20 as written therefore leaves DLQ-backed channel creation hard-failing on the emulator one call earlier than the `GetIamPolicy` case it does tolerate. That defeats C-3/C-4, makes R-21 ("the GCP work is verified by a local, repeatable run against the Pub/Sub emulator") unreachable, and invalidates AC-20, AC-22 and 24 of AC-30's "32 unconditional" cells.

**Evidence**: `specs/0036-universal-transport-conformance-tests/conformance-status.md:489`:

> `ProjectsClient.GetProjectAsync` — Cloud Resource Manager… | **`Unauthenticated`** — the request leaves for **real GCP**. `ProjectsClientConfiguration` is the one builder hook the four GCP providers do not wire for emulator detection…

versus `requirements.md` R-20: "No other status code is swallowed — `Unauthenticated`, `NotFound`, … still propagate".

I also confirmed the call is not even an RPC failure in the general case: `GcpPubSubMessageGateway.cs:485-486` and `:540-541` call `Connection.CreateProjectsClientAsync()` first, and `GcpMessagingGatewayConnection.cs:173-193` builds the client via `builder.BuildAsync()` with a possibly-null `Credential` — a construction-time failure with no gRPC status at all, which NFR-5's "No `catch (Exception)` may be used" additionally forbids catching.

**Recommendation**: Either add `Unauthenticated` (and a stated position on credential-resolution failures that carry no status) to R-20's tolerance list with a rationale for why that does not mask a genuine misconfiguration, or adopt the alternative the ledger already isolated at `conformance-status.md:492-494` — "setting `PublisherMember` explicitly on the provider's `DeadLetterPolicy` skips the Resource Manager call" — as a numbered requirement, so the `GetProjectAsync` path is never reached on the emulator. Either way AC-20 must name the status codes it exercises, and AC-21's negative case must not be one that R-20 now has to tolerate.

---

### 2. R-2 ("first delivery presents 0") and R-3 ("approximate counters may present at least n-1") contradict each other at n=1, and no requirement or AC resolves it (Score: 82)

R-2 is absolute: "The first delivery of a message presents a delivery count of 0." C-7 makes it non-negotiable because `DefaultMessageAssertion` asserts `HandledCount` equality. R-3 permits an approximate counter to present *at least* `n - 1`, i.e. at least 0 on first delivery. A-4 states that both SQS `ApproximateReceiveCount` and Pub/Sub `delivery_attempt` are approximate by their vendors' own documentation. The Candidate-mechanisms table notes the further problem — "all three counters read `1` on first delivery, while R-2 requires `0`" — and then does nothing with it, because that table is explicitly an ADR input, not an obligation.

So: if the ADR picks the broker-counter mechanism (which NFR-1 and NFR-3 both push it towards), an approximate counter that over-counts on a *first* delivery normalises to 1, not 0. R-3 permits that. R-2 forbids it. C-7/R-23 say it breaks the round-trip identity assertion on FR-2, FR-15, FR-16 and FR-22 across eight AWS cells. Nothing in the document says which requirement wins, or what the implementation must do (clamp to 0? only trust the counter on redelivery? detect first delivery some other way?). Two developers will resolve this differently, and one of them regresses eleven `Pass` cells.

**Evidence**: I verified the vendor documentation A-1/A-4 rests on, in `~/.nuget/packages/google.cloud.pubsub.v1/3.36.0/lib/netstandard2.0/Google.Cloud.PubSub.V1.xml:5552-5555` — "Upon the first delivery of a given message, `delivery_attempt` will have a value of 1. **The value is calculated at best effort and is approximate.** If a DeadLetterPolicy is not set on the subscription, this will be 0." And `DefaultMessageAssertion.cs.liquid:59` — `Xunit.Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);` — confirmed at exactly that line.

**Recommendation**: State the precedence explicitly. Either narrow R-3 ("a transport whose counter is approximate must still normalise the first delivery to 0; approximation is tolerated only on redeliveries") or weaken R-2 and add a requirement covering what happens to the identity assertion when it cannot hold. Add an AC that exercises a first delivery on an approximate-counter transport with a deliberately elevated broker count.

---

### 3. A-2 is "Not verified", is a single point of failure for the whole GCP half, and AC-30 nonetheless declares those cells unconditional (Score: 76)

A-2 ("The Pub/Sub emulator populates `delivery_attempt` on a DLQ-backed subscription") is flagged *Not verified*, and it cannot be verified until R-20 lands. R-21 makes the emulator the verification bar and says a cloud-only run "is not 'done' under this spec". A-2's own text says only that if it is false "the spec must record that honestly rather than quietly falling back to `gcp-ci`" — which is a disclosure obligation, not a fallback.

Meanwhile AC-30 states "**33 cells** in total, of which **32 are unconditional** and 1 (RocketMQ FR-23) is conditional on R-14." Twenty-four of those 32 are GCP cells whose evidence AC-30 records as "Pub/Sub emulator, both variants". If A-2 is false, 4 of them (GCP FR-23 ×4) cannot be moved on the stated evidence at all, and R-13's "done" is undefined. The spec therefore carries an acknowledged single point of failure while simultaneously asserting the outcome is unconditional.

**Evidence**: `requirements.md` A-2 vs AC-30's closing line, and R-21: "A GCP requirement whose only evidence is a cloud-only run is not 'done' under this spec."

**Recommendation**: Make the GCP FR-23 cells in AC-30 conditional on A-2 the way RocketMQ FR-23 is conditional on R-14, with a defined "done" for both branches (as R-14 does), and restate the 32/33 arithmetic. Alternatively, add a requirement that the ADR must select a GCP mechanism that does not depend on `delivery_attempt` if A-2 refutes.

---

### 4. R-3's actual obligation has no acceptance criterion; the map points it at AC-1, which tests only R-1 (Score: 74)

The R→AC map row is `R-3 | AC-1`. AC-1 reads: "…**Then** the delivery count presented on each delivery is strictly greater than the count presented on the previous delivery." That is verbatim R-1. It does not test either half of R-3 — neither "where a transport's delivery count is exact, the *n*th delivery presents a count of `n - 1`" nor the `>= n - 1` approximate bound — and it does not test R-3's second sentence, which is the load-bearing one: "The ADR must record, for each transport in scope, which of the two applies and on what evidence. **A transport may not be left unclassified.**"

That classification is a prerequisite for R-11 and AC-11 (which say "per the ADR's per-transport classification"), so an untested R-3 propagates. The map claims full coverage — "Every requirement maps to at least one acceptance criterion" — but this row does not deliver it.

**Evidence**: `requirements.md` AC-1 text and the map table row `| R-3 | AC-1 |`.

**Recommendation**: Add an AC asserting the exact-case equality for each transport the ADR classifies as exact, and a separate AC (or an explicit exit criterion alongside AC-30/AC-31) that the ADR contains a four-row classification table with evidence.

---

### 5. NFR-2 is mapped to AC-28, which measures a different thing entirely, leaving NFR-2 unverifiable (Score: 72)

NFR-2: "No new per-message heap allocation on the receive path… must not introduce a new collection, dictionary, string concatenation or boxed value per received message."

AC-28: "**When** the receive and requeue paths of each transport in scope are inspected, **Then** the number of broker calls per delivery and per requeue is the same as before the change."

Broker-call count says nothing about allocation. The AC's own parenthetical labels it "(NFR-1, NFR-3)" — it does not claim NFR-2 — yet the map row asserts `NFR-2 | AC-28`. So the document contradicts itself, and NFR-2 has no verification at all. This matters concretely: the likeliest implementations (reading a `Dictionary<string, object>` bag, `HeaderResult<int>` wrapping, `int.Parse` over a substring) each risk a boxed value or a string per message, and there is no benchmark, allocation-counting harness, or code-inspection criterion specified.

Separately, AC-28 is not automatable as written ("when… are inspected") and is the sole AC for NFR-1 and NFR-3 too.

**Evidence**: AC-28's text, its `(NFR-1, NFR-3)` label, and the map rows `| NFR-1 | AC-28 |`, `| NFR-2 | AC-28 |`, `| NFR-3 | AC-28 |`.

**Recommendation**: Either give NFR-2 a real AC (an allocation-counting test, or a `GC.GetAllocatedBytesForCurrentThread()` delta over N receives with a stated budget), or downgrade NFR-2 to a design constraint the ADR must justify and remove the false map row. Reword AC-28 so its evidence is a diff-reviewable artefact, not "inspection".

---

### 6. R-11's firing condition is not determinable by the mechanism R-25 prescribes, and the R-25 prior-art note addresses only R-10's problem (Score: 72)

R-25's rule table gives R-11's rule the trigger "**the transport cannot satisfy R-1 for this subscription**". The ⚠️ note beneath it then works the "how does a rule in core see a transport-specific value" problem in detail — but exclusively for R-10's integer `M`, and its conclusion depends on that shape: "**Ours are not**: `M` is an integer on a subscription, and a core role interface exposing it… makes the entity a core type again and keeps the rule on the **lower** rung."

R-11's input is not an integer. It is a per-subscription capability judgement that, by A-1's own worked example, depends on transport-specific configuration state (a `GcpPubSubSubscription` with no `DeadLetterPolicy`) *and* on the ADR's mechanism choice. No role interface shape is proposed for it, and the note's "lower rung is expected to suffice" conclusion is argued only from the `M` case. AC-11 inherits the problem — "(per the ADR's per-transport classification)" makes the Given unresolvable at requirements time.

**Evidence**: `requirements.md` R-25 rule table row 3, and the ⚠️ note's reasoning, which never mentions R-11. I verified the seam itself is real: `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs:78-80` does resolve subscriptions and `sp.GetServices<ISpecification<Subscription>>()`.

**Recommendation**: Extend the R-25 note to R-11 explicitly. Either name the core-visible predicate (e.g. a role interface `IDeclareDeliveryCountCapability` with a `bool CanEnforceDeliveryBudget` the transport subscription answers), or state that R-11's rule takes the upper rung while R-7/R-10 take the lower, and record the #4282 sequencing dependency that follows.

---

### 7. The harness reconfiguration to `R < M` is a real deliverable owned only by an Assumption, with no requirement and no AC (Score: 70)

A-5 ends: "Once the contract works these race (R-8's tie case), so the harness must be re-configured to `R < M` for the FR-23 behaviour to exercise the Brighter route deterministically. **Harness change, in scope.**"

That is a scoped deliverable stated in the assumptions section. No `R-n` carries it. AC-3 ("no native redrive limit at or below 3"), AC-8 (`requeueCount: 3` / `maxReceiveCount: 5`) and AC-19 (`requeueCount: 3` / `MaxDeliveryAttempts: 5`) all *presuppose* it has happened, but none requires it, and nothing names the eight files that must change. Conversely, if it silently does not happen, AC-3/AC-8/AC-19 fail for a reason the spec has classed as "a defect of the harness configuration, not evidence about the transport" (NFR-7) — exactly the confusion the change is meant to prevent.

**Evidence**: I confirmed both cited collisions are real and exactly at the cited lines. `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsStandardMessageGatewayProvider.cs:104` — `redrivePolicy: new RedrivePolicy(deadLetterChannelName, 3)` — and `:108` — `requeueCount: 3`. `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpPullMessageGatewayProvider.cs:151` — `requeueCount: 5` — and `:155` — `MaxDeliveryAttempts = 5`.

**Recommendation**: Promote it to a numbered requirement (e.g. R-27) naming the eight provider files and the target values, cross-referenced from C-6 (which constrains GCP to `R <= 4`), with its own AC.

---

### 8. Negative budgets other than `-1` are unspecified, and the existing code rejects on the first delivery for them (Score: 68)

The document works `R >= 1`, `R == 1`, `R == 0` and `R == -1` carefully, and R-7 goes as far as adding a validation Warning for `R == 0` because "a zero budget is indistinguishable in effect from `1` and is far more likely to be a mistake than an intent". It says nothing about `R < -1`, which is the same class of mistake (a user typing `-3` meaning "three retries") and has the same effect.

I verified the behaviour rather than inferring it: `MessagePump.cs:171-174` is `return RequeueCount != -1;`, so `-3` *enables* enforcement; and `Message.cs:161-164` is `return Header.HandledCount >= requeueCount;`, so after `UpdateHandledCount()` the test is `1 >= -3` — true — and the message is rejected on its first delivery with `DeliveryError`. A user who writes `requeueCount: -3` gets zero retries and a DLQ'd message, silently.

**Evidence**: `src/Paramore.Brighter.ServiceActivator/MessagePump.cs:171-174`; `src/Paramore.Brighter/Message.cs:161-164`.

**Recommendation**: Add the `R < -1` boundary to R-7 (either as a fourth validation Warning row in R-25's table, or as an explicit statement that it behaves as `R == 0` and is reported the same way), with an AC. Do not leave it to the ADR — it is a user-facing boundary, not a mechanism choice.

---

### 9. R-4 and AC-3 define "never redelivered again" over two different windows, neither of which the FR-23 template observes (Score: 65)

R-4: "'Never redelivered a further time' means: after the rejection, no further delivery of that message occurs on that channel **within the conformance behaviour FR-23 observation window (60 s in the current template)**."

AC-3: "…and **no further delivery of that message occurs within the following 60 s**."

These are different. R-4's window is the template's existing 60 s DLQ poll, which *ends* when the DLQ message is found — typically well before 60 s. AC-3's is a fresh 60 s starting after the rejection, which doubles the wall-clock cost of every FR-23 run across 13 configurations × 2 variants and does not exist in the template today.

I confirmed what the template actually does, and it asserts neither: it polls for DLQ arrival, breaks on finding it, then asserts message type, identity, and the count bound. There is no no-further-delivery assertion at all.

**Evidence**: `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid:91-101` — "poll every 500 ms, give up after 60 s", `while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))`, `Thread.Sleep(500)` — and `:107-128`, whose only assertions are `Assert.NotEqual(MessageType.MT_NONE, …)`, `ConformanceDeferredPump.AssertIsTheMessageSent(…)` and `Assert.True(dlqMessage.Header.HandledCount >= deliveriesExpected, …)`.

**Recommendation**: Pick one window and state it in both places. If the no-further-delivery assertion is genuinely wanted, say so as template work with an owning requirement and a stated runtime budget (NFR-7 already constrains this).

---

### 10. AC-5 and AC-6 name no transport, and R-7's `R == 0` *behavioural* half has no AC at all (Score: 64)

AC-5 (R-6) and AC-6 (R-7) begin "**Given** `requeueCount: -1`…" / "**Given** `requeueCount: 1`…" with no configuration named, while every other behavioural AC in the document is explicit ("any transport in scope", "each of the eight configurations", "each of the four GCP configurations"). One developer satisfies AC-6 on InMemory in a unit test; another runs it on all four transports in scope in both variants. The second reading is far more expensive and is the only one that actually proves anything, since R-7's `R == 1` case depends on R-1 working on the transport.

Separately, R-7 makes two distinct claims about `R == 0`: that enforcement is enabled and "the first deferral also rejects immediately", and that a Warning is raised. AC-7 tests only the Warning. The behavioural half is untested. (I verified the claim itself is true — `Message.cs:163`, `1 >= 0` — but that is precisely the kind of non-obvious boundary that needs an assertion.)

**Evidence**: AC-5, AC-6 and AC-7 text; the map rows `| R-6 | AC-5 |` and `| R-7 | AC-6, AC-7 |`.

**Recommendation**: Add the transport scope to AC-5 and AC-6 in the same form the rest of the document uses, and extend AC-7 (or add an AC) covering the `R == 0` rejection behaviour.

---

### 11. R-23's non-regression obligation has no AC covering RocketMQ, whose nine `Fixed` cells sit on a path this spec touches (Score: 64)

R-23: "No conformance cell currently recorded as `Pass` or `Fixed` regresses, in any behaviour column, for any configuration." The map gives it AC-2, AC-12 and AC-26. AC-12 covers the eight AWS/AWS.V4 configurations; AC-26 covers the nine already-conforming ones; AC-2 covers the identity assertion. Nothing covers RocketMQ.

That is not a theoretical gap. The Candidate-mechanisms table names `RocketMessageConsumer.ReadHandledCount` (`:422`) as one of the seams the ADR will modify, and the ledger records nine RocketMQ cells as `Fixed (#4240)` — FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-16, FR-17, FR-22. AC-24 and AC-25 assert only the FR-23 cell's state. GCP needs no such AC (all 48 cells are `Deferred`), but RocketMQ does.

**Evidence**: `specs/0036-universal-transport-conformance-tests/conformance-status.md:839` (the RocketMQ matrix row) and `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMessageConsumer.cs:422` — `static int ReadHandledCount(MessageView message)` — confirmed at exactly the cited line.

**Recommendation**: Add RocketMQ non-regression to AC-24/AC-25 or add a dedicated AC, on both branches of R-14 — the "bound but unimplemented" branch still touches the same file if the ADR normalises counts there.

---

### 12. R-19's stated rationale for diverging from SQS applies equally to SQS, so it does not justify the divergence it blesses (Score: 62)

R-19: "…the GCP behaviour is specified here as preserve-then-redeliver **because Pub/Sub's ack is irreversible and a lost message cannot be recovered**. Changing SQS to match is **out of scope**."

SQS's `DeleteMessage` is equally irreversible, and I confirmed the SQS path does exactly what R-19 calls unacceptable: on a failed DLQ send it logs and then deletes the source. The stated reason is therefore an argument for changing both transports, presented as a reason for changing only one. What remains is a scope decision (reasonable) dressed as a principled distinction (not). The Out of Scope section repeats the same framing. A reviewer cannot tell from the document whether the SQS behaviour is a defect the team has chosen to defer or a behaviour the team endorses — and R-9's careful "this is a decision, recorded here so it is not left implicit" treatment elsewhere shows the document knows how to do this properly.

**Evidence**: `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs:309-316`:

```
catch (Exception ex)
{
    // Sending to DLQ failed — delete the original to prevent infinite
    // reprocessing. The message is lost rather than stuck in a retry loop.
    Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id.Value, rejectionReason.ToString());
    await DeleteSourceMessageAsync(receiptHandle!, message.Id.Value, cancellationToken);
    return true;
}
```

The cited range `:307-314` is accurate to within the block.

**Recommendation**: Replace the rationale with the real one (SQS's behaviour is deliberate infinite-loop avoidance, per the code comment; GCP has no such incumbent decision to unwind, so the greenfield choice is preserve-then-redeliver), and state plainly whether the SQS behaviour is now considered a defect with a follow-up issue, or endorsed.

---

### 13. Four exit-criteria ACs are not assertions, and no requirement owns the artefacts they depend on (Score: 62)

- **AC-23** is an experiment with no failure mode: "…the sequence is either strictly increasing (the condition holds) or not (it does not)". Both outcomes pass. It cannot gate anything, yet R-14's entire branch hangs off it.
- **AC-27** requires "a source file that compiles against the current V10 public API and uses `Subscription`, `SqsSubscription`, `GcpPubSubSubscription`, `RocketMqSubscription`, `MessageHeader` or `Message`". No such file is named, and no requirement owns creating or maintaining one.
- **AC-28** — "when the receive and requeue paths… are inspected" — is a human code review (see also finding 5).
- **AC-30/AC-31** are markdown-ledger edits. The document says they are "deliberately not mapped to a single requirement", which is defensible, but it means the ledger update and the R-14(d) "written into `conformance-status.md`'s RocketMQ paragraph" obligation have no owning requirement either.

Taken together, a meaningful fraction of the exit criteria cannot be checked by running anything.

**Evidence**: AC-23, AC-27, AC-28, AC-30, AC-31 text; and the map's closing note excluding AC-30/AC-31.

**Recommendation**: Split AC-23 into a measurement *step* and a pass/fail criterion ("the recorded sequence is committed to `conformance-status.md` and #4353 is updated" — that part *is* assertable). Name the AC-27 compatibility file as a deliverable of a numbered requirement. Mark AC-28/AC-30/AC-31 explicitly as manual gates so nobody expects a green suite to prove them.

---

### 14. NFR-7's only AC is conditional on R-14's branch, so NFR-7 may end up with no coverage (Score: 60)

NFR-7 applies to "every configuration this spec moves" — 8 AWS, 4 GCP, and conditionally 1 RocketMQ. The map gives it a single AC: `| NFR-7 | AC-24 |`. AC-24 is guarded — "**Given** AC-23 recorded a strictly increasing sequence" — and covers RocketMQ alone. If R-14's condition does not hold, AC-25 applies instead and NFR-7 has no acceptance criterion at all; and even when it does hold, the 12 AWS/GCP configurations NFR-7 names are uncovered.

**Evidence**: NFR-7 text ("for every configuration this spec moves"), AC-24's Given clause, the map row `| NFR-7 | AC-24 |`.

**Recommendation**: Map NFR-7 to AC-12 and AC-19 as well, and add an explicit runtime assertion (the template's 60 s ceiling is already the mechanism — state that a configuration exceeding it is a harness defect to be fixed, not a `Deferred` cell).

---

### 15. Bare ADR numbers are ambiguous — three files share 0038, four share 0039, three share 0053 (Score: 58)

The document cites "ADR 0038" six times (C-1, R-12, NFR-6, the Terms table, Additional Context), "ADRs 0038, 0039, 0041", and "ADR 0053" twice, always by bare number. The repository has duplicate ADR numbers, and the README for this very spec flags the problem ("master already carries duplicate 0066/0067 pairs") without applying the lesson to its own citations. A reader following "ADR 0053" could land on box database migration or resilience-pipeline reuse rather than pipeline validation at startup.

**Evidence**: `ls docs/adr/` returns `0038-aws-sqs-dlq-direct-send.md`, `0038-dont-ack-action.md`, `0038-remove-clear-service-bus.md`; `0039-opentelemetry-builder-extension.md`, `0039-redis-dlq-brighter-managed.md`, `0039-scoping-dependencies-inline-with-lifetime-scope.md`, `0039-transport-scheduler-wiring.md`; `0041-add-parallel-split-to-mediator.md`, `0041-postgres-dlq-brighter-managed.md`; `0053-box-database-migration.md`, `0053-fix-resilience-pipeline-reuse.md`, `0053-pipeline-validation-at-startup.md`.

Relatedly, the spec README's links `docs/adr/0039-redis-dead-letter-queue.md` and `docs/adr/0041-postgres-dead-letter-queue.md` are both dead — the files are `0039-redis-dlq-brighter-managed.md` and `0041-postgres-dlq-brighter-managed.md`.

**Recommendation**: Cite ADRs by full filename or slug throughout, as the document already does for `0074-lifetime-validation-evaluation-site`.

---

### 16. The Problem Statement's headline counts and causal story are both inaccurate against the ledger (Score: 55)

"Today that is true on nine configurations… and false on thirteen (AWS ×4, AWS.V4 ×4, GCP ×4, RocketMQ)." That is 9 + 13 = 22, but the conformance matrix has 24 rows. The two omitted rows — `MQTT / MqttMessagingGateway` (`Deferred -> #4351`) and `AzureServiceBus / AzureServiceBusMessagingGateway` (`Deferred -> #4240`) — are also FR-23 failures. They are correctly excluded from *scope*, but the sentence is a statement of fact about the ledger, and as such it is wrong.

Second: "The nine that work requeue by **republishing**, so the header travels." Three of the nine do not. R-22 itself concedes this — "(or, for the three RMQ rows, dead-letter natively)" — and the ledger is explicit that the RMQ rows "are the first configurations to reach the DLQ by the **broker's** own dead-lettering rather than a Brighter-side republish". The Problem Statement's causal framing is the document's central argument, and it is wrong for a third of its own supporting evidence.

**Evidence**: `specs/0036-universal-transport-conformance-tests/conformance-status.md:819-842` (the 24 matrix rows) and `:412-416`.

**Recommendation**: Say "false on fifteen, thirteen of which this spec addresses", and correct the republishing sentence to match R-22's own wording.

---

### 17. AC-31's "could not move" list omits one currently-`Deferred` cell (Score: 52)

AC-31 requires the ledger to state "explicitly which cells this spec **could not** move and why", then enumerates them. Working through the matrix row by row, every `Deferred` cell is accounted for except one: `MQTT / MqttMessagingGateway` FR-16 (`Deferred -> #4240`). AC-31 names MQTT only for FR-23.

**Evidence**: `conformance-status.md:841` — the MQTT row reads `Fixed (#4240)` across FR-2 through FR-15, then `Deferred -> #4240 (sign-off: @iancooper)` in the FR-16 column, `Fixed` for FR-17 and FR-22, and `Deferred -> #4351` for FR-23. AC-31's fourth bullet lists `AWS / SqsFifo` and `AWS.V4 / SqsFifo` FR-9, `MSSQL` FR-16, `Redis` FR-16, `RMQ.*` FR-5, `RocketMQ` FR-2 and FR-15 — all of which I confirmed, but not MQTT FR-16.

**Recommendation**: Add `MQTT` FR-16 to AC-31's fourth bullet.

---

### 18. Minor line-number drift in two citations (Score: 42)

`Reactor.cs:492` / `Proactor.cs:498` are cited in the Problem Statement and the Terms table as the location of the `UpdateHandledCount()` / `HandledCountReached(RequeueCount)` calls. Those lines are the enclosing `RequeueMessage` method declarations; the calls are at `Reactor.cs:494` / `:498` and `Proactor.cs:500` / `:504`. The spec's own README cites the correct ranges (`Reactor.cs:494-509`, `Proactor.cs:500-513`), so the two documents disagree — but both point a reader to the right method, so this is cosmetic.

Also, `SqsMessageCreator.ReadHandledCount` is cited as `:322`; it is at `:323`.

**Evidence**: `src/Paramore.Brighter.ServiceActivator/Reactor.cs:492-498`; `src/Paramore.Brighter.ServiceActivator/Proactor.cs:498-504`; `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageCreator.cs:323`.

**Recommendation**: Align requirements.md with the README's ranges, or drop the line numbers for the call sites and cite the method names.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 6 |
| 50-69 (Medium) | 10 |
| 0-49 (Low) | 1 |

**Total findings**: 18
**Findings at or above threshold (60)**: 14

---

**What survived scrutiny.** This document's citation discipline is unusually good, and almost everything checked was exact. Verified **correct**: `Subscription.cs:109` (`public int RequeueCount { get; }`) and `Subscription.cs:203` (`int requeueCount = -1`) — both cited for different facts, both right; `MessageHeader.cs:226` (`HandledCount`); `MessagePump.cs:171` (`DiscardRequeuedMessagesEnabled()` = `RequeueCount != -1`); `SqsMessageConsumer.DetermineRejectionRoute` `:541`, `RefreshMetadata` `:496`, `RequeueAsync` `:384`, the receive-attribute block `:188-194` including `MessageSystemAttributeNames = ["All"]` at `:193`, and the failed-DLQ-send catch at `:307-316`; AWS.V4 `:276` and `:377`; `GcpPullMessageConsumer` Reject `:276`, RejectAsync `:306`, Requeue `:335`, RequeueAsync `:369`; `GcpPubSubStreamMessageConsumer` Reject `:84`, Requeue `:217`; `GcpPubSubMessageGateway` call sites `:235` and `:251` under `DeadLetter != null`, helpers `UpdateIAmRoleForDeadLetterAsync` `:477` and `UpdateIAmRoleForSubscriptionAsync` `:527` (the requirements are *more* accurate here than the 0036 ledger, which says `:481`), and R-20's ⚠️ point that #4354 names only the first helper — correct and material; `RocketMessageConsumer.Requeue` `:179` with the commented-out `ChangeInvisibleDuration` at `:187`, `ReadHandledCount` `:422`, and the `ReadDelay` `:433` copy-paste defect quoted verbatim; `DefaultMessageAssertion.cs.liquid:59` asserting `HandledCount` equality; `BrighterPipelineValidationExtensions.cs:78-80` collecting `ISpecification<Subscription>`; `ConsumerValidationRules.RequestTypeSubtype()` at `:114-123` as a genuine `ValidationSeverity.Warning` subscription-rule precedent; and — crucially for AC-32 and R-7/R-10/R-25 — that Warnings genuinely never block startup even under `throwOnError: true` (`PipelineValidationResult.IsValid => Errors.Count == 0`, `ThrowIfInvalid()` at `:52-56`, `BrighterValidationHostedService.cs:78`). Also verified: `SqsAttributes.cs:110`; `DeadLetterPolicy.cs:47` with its documented 5..100 range; both harness collisions at exactly `SqsStandardMessageGatewayProvider.cs:104,108` and `GcpPullMessageGatewayProvider.cs:151,155`; `RocketMQ.Client 5.2.1` and `Google.Cloud.PubSub.V1 3.36.0` in `Directory.Packages.props`; `grep -rn "ApproximateReceiveCount" src/` returning nothing (NFR-1); R-15's membership list — exactly those nine types implement the two interfaces and `GcpPubSubSubscription` implements neither, and the "eight transport gateways plus InMemory" arithmetic is right; C-8 (`RocketSubscription : Subscription, IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport`, inherited by `RocketMqSubscription<T>`); A-1's Google XML quote word for word; R-12/NFR-6's lockstep claim against `0038-aws-sqs-dlq-direct-send.md:47-49,121`; R-5's `>= R - 1` bound and its two-routes rationale, which the FR-23 template states almost verbatim; the 500 ms / 60 s observation window; and every cell state and count in AC-30 — the 8 AWS `Deferred -> #4341`, the 20 GCP FR-4/5/6/8/17, the 4 GCP FR-23, the 1 RocketMQ `Deferred -> #4353`, and the 33 total — plus R-22's nine `Pass` rows and R-23's eleven `Pass` columns for `AWS / SqsStandard`.

---

# Remediation log — round 1

**Date**: 2026-09-21. **Applied to**: `requirements.md` (and two dead links in `README.md`).
**Outcome**: all 14 findings at or above threshold remediated, plus findings 15, 16, 17 and 18.
Document went from 26 `R-n` / 32 `AC-n` to **27 `R-n` / 8 `NFR-n` / 38 `AC-n`**, integrity-checked:
no requirement without a map row, no map row citing a non-existent AC, no numbering gap, AC-30 and
AC-31 deliberately unmapped as before.

This log carries the reasoning. `requirements.md` carries only obligations — where a choice was made
between two defensible readings, the choice is stated there as a requirement and argued here.

| # | Score | Remediation |
|---|---|---|
| 1 | 92 | **Both routes taken, not either/or.** R-20 now tolerates `Unauthenticated` alongside `Unimplemented` and `PermissionDenied`, *and* the no-status case where the Resource Manager client fails to construct before any RPC. Separately C-11 records the verified fact that both IAM helpers skip `GetProjectAsync` entirely when the member is set, and R-27(b) makes the four GCP providers set it. R-20 governs the default a user meets (neither member set); C-11/R-27(b) keep the harness off that path altogether. AC-20 now names the codes and covers both member-configured and not; AC-21's negative case restated as "outside R-20's tolerated set", so it cannot drift into one. NFR-5 widened to the third code and the construction case, still forbidding `catch (Exception)`. |
| 2 | 82 | **Precedence resolved in favour of R-2**, which is the one C-7 makes non-negotiable. R-3 now governs `n >= 2` only and grants no latitude at `n == 1`; R-2 states the consumer normalises to `0`. One thing normalisation alone cannot guarantee: a broker that *over-counts on a genuine first delivery* defeats any fixed origin offset. Rather than pretend otherwise, R-2 obliges the ADR to state how its mechanism achieves the exact `0` in that case, or to record the residual risk and name the cells exposed to it — "silence is not an available outcome". AC-33 pins the normalisation at the values that are testable. |
| 3 | 76 | R-13 gained a two-branch structure in the same shape as R-14's, keyed on A-2, with a defined "done" on each side. The finding's count was refined in the process: **the 20 GCP rejection-routing cells do not depend on A-2 at all** — they depend on R-20 — so only the 4 GCP FR-23 cells are conditional. AC-30 restated as 28 unconditional + 5 conditional (4 on A-2, 1 on R-14), and its GCP FR-23 row carries the condition. A-2 now points at the branch instead of promising only to be honest. |
| 4 | 74 | AC-33 and AC-34 added; R-3's map row moved off AC-1 (which tested R-1 verbatim) to AC-33, AC-34. AC-34 asserts both halves: the ADR's four-row classification table exists with every transport classified, and transports classified *exact* present exactly `0, 1, 2`. |
| 5 | 72 | NFR-2 re-mapped to a new **AC-37** — an allocated-bytes delta over 1,000 receives, base revision vs post-change, per transport. The false map row pairing NFR-2 with AC-28 is gone. AC-28 reworded from "when inspected" to an enumeration taken from the diff and compared against the base revision, recorded in the ADR, and marked a manual gate. |
| 6 | 72 | The R-25 note now addresses R-11's shape explicitly: an integer is trivially exposed by a core role interface, a *capability judgement* is not until someone defines the predicate. The ADR must pick a rung **per rule** and, for R-11, either define the core-visible predicate or take the upper rung and own the #4282 dependency. It must also name, per transport, at least one subscription shape that trips R-11 — or state that none does. AC-11's Given now reads that list and names the concrete GCP instance as a floor. |
| 7 | 70 | Promoted to **R-27**, under a new Group H, with a table naming the provider files and the required values (`R = 3`, `M = 5` on both families; `M = 5` is forced by C-6). Correction to the finding: it is **twelve** files, not eight — four AWS, four AWS.V4, four GCP. R-27(b) folds in the C-11 member configuration. A-5 now names R-27 as its owner. New **AC-36** asserts the values and that the Brighter route is the one FR-23 takes. |
| 8 | 68 | R-7 now covers `R < -1` explicitly, with the mechanism stated: the pump's switch is `RequeueCount != -1` (`MessagePump.cs:171`), not a sign test, and `HandledCountReached` compares `1 >= -3` (`Message.cs:161-164`) — so `-1` is the *only* value that disables the budget. R-25's rule renamed "Zero or negative budget" and fires on `== 0` or `< -1`. New **AC-35** asserts the behaviour for `0`, `-3` and `-1`. |
| 9 | 65 | **One window, and the assertion changed to something that is actually true of it.** R-4 and AC-3 both now read: the handler is not invoked again and its invocation count stands at `R` or fewer *when the existing single 60 s poll closes*. No second window, no additional wait, runtime unchanged. The reviewer's observation that the template asserts no no-further-delivery condition today stands — the invocation-count form is what the template can assert without one. |
| 10 | 64 | AC-5 and AC-6 now carry "on each transport in scope and in both variants", the form the rest of the document uses. R-7's `R == 0` behavioural half is covered by the new AC-35. |
| 11 | 64 | New **AC-38**, asserting RocketMQ's nine `Fixed` cells on **both** branches of R-14 — the "bound but unimplemented" branch still touches `RocketMessageConsumer`'s receive path if the ADR normalises counts there. Mapped to R-22 and R-23. |
| 12 | 62 | Rationale replaced. R-19 now argues from the real asymmetry — GCP has no incumbent behaviour to unwind, so the greenfield choice is preserve-then-redeliver, and the looping risk it accepts is bounded by the very budget this spec makes enforceable. The SQS branch is described as what its own code comment says it is (deliberate infinite-loop avoidance), and its status is stated plainly: **an open question, not an endorsement**, to be raised as its own issue. ⚠️ **Raising that issue is outward-facing and needs the user's say-so — not done.** |
| 13 | 62 | AC-23 split into a measurement plus three assertable exit criteria (ledger paragraph written, #4353 updated, exactly one of AC-24/AC-25 claimed and the other recorded N/A). AC-27's artefact is now owned by R-24 — a compile-only sample under `tests/Paramore.Brighter.Core.Tests/`, built by the normal build, `#pragma warning disable` forbidden so obsoletions surface. AC-28, AC-30 and AC-31 marked ⚠️ manual gates, and the map closes with a note listing exactly which criteria a green suite does not prove. **Deliberately not done**: AC-30/AC-31 still have no owning requirement. They are cross-cutting exit criteria for the spec as a whole and the document's existing reasoning for that stands; marking them as manual gates addresses the part that was misleading. |
| 14 | 60 | NFR-7 re-mapped to AC-12, AC-19 and AC-24, so it covers all thirteen configurations rather than only the conditional RocketMQ one, and now states that a configuration which cannot fit the window is a harness defect to fix under R-27 — never grounds for leaving a cell `Deferred`. |
| 15 | 58 | All bare ADR numbers in `requirements.md` replaced with slugs (`0038-aws-sqs-dlq-direct-send`, `0053-pipeline-validation-at-startup`, `0074-lifetime-validation-evaluation-site`, and the Redis/Postgres DLQ pair). The two dead links in `README.md` fixed to the real filenames. |
| 16 | 55 | Problem Statement restated: nine of **twenty-four** configurations satisfy FR-23, fifteen do not, **thirteen of the fifteen are in scope** and the other two (MQTT, ASB) are named as out of scope. Causal sentence corrected — six of the nine republish; the three RMQ rows reach the DLQ by the broker's own DLX and need no Brighter budget to do it. |
| 17 | 52 | `MQTT` FR-16 added to AC-31's fourth bullet, with a parenthetical noting MQTT now appears twice in that list (FR-16 and FR-23) so it does not read as a duplication error. |
| 18 | 42 | Call-site lines corrected to `Reactor.cs:494`/`:498` and `Proactor.cs:500`/`:504` in both the Problem Statement and the Terms table, aligning `requirements.md` with the README. `SqsMessageCreator.ReadHandledCount` `:322` → `:323`. `SqsMessageConsumer`'s catch `:307-314` → `:307-316`. |

## Corrections to this review document itself

Three citations in the findings above were off and have been corrected in place: the RocketMQ matrix
row is `conformance-status.md:839` (not `:833`), the MQTT row is `:841` (not `:838`), and the matrix
rows span `:819-842`. The substantive claims in findings 11, 16 and 17 were each re-verified against
the corrected lines and all three hold.

## Not done — needs the user's decision

- **Raising the SQS failed-DLQ-send question as its own issue** (finding 12). R-19 and §Out of Scope
  now say it will be raised at approval; opening it is outward-facing.
- **Commenting on #4341 / #4386 / #4353 / #4354** to point at these requirements, and re-pointing the
  ledger cells — both already on the spec's carried-forward list, both outward-facing.

## Next step

Re-run `/spec:review requirements`. Round 1's threshold was 60; nothing in this remediation lowers
the bar for round 2.

---

# Review: requirements (round 2) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

8 findings at or above threshold 60. Address these before approving.

## Findings

### 1. R-24's compile-only compatibility sample cannot exist at the location R-24 and AC-27 name (Score: 82)

R-24 says the evidence for API non-breakage "is a committed artefact, not an inspection": a compile-only sample under `tests/Paramore.Brighter.Core.Tests/` constructing `Subscription`, `SqsSubscription`, `GcpPubSubSubscription`, `RocketMqSubscription`, `MessageHeader` and `Message`, "compiled by the normal build, so a breaking change fails the build". AC-27 repeats the location and the six types.

That project does not reference any of the three transport assemblies. A file there naming `SqsSubscription`, `GcpPubSubSubscription` or `RocketMqSubscription` does not fail on a breaking change — it fails to compile today, on the base revision, because the types are not in scope. The requirement's whole point (an automated gate rather than a review) does not work at the site it names, and making it work means adding gateway project references to a **core unit-test** project — an architectural change no requirement proposes or sanctions.

**Evidence**: `tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj` lists exactly seven `ProjectReference` entries: `Paramore.Brighter.BoxProvisioning`, `Paramore.Brighter.Extensions.DependencyInjection`, `Paramore.Brighter.Mediator`, `Paramore.Brighter`, `Paramore.Brighter.Outbox.Hosting`, `Paramore.Brighter.ServiceActivator`, `Paramore.Brighter.Testing`. None is a messaging gateway. `Paramore.Brighter.Testing` references only `Paramore.Brighter`, so there is no transitive route.

**Recommendation**: Name a location that can see all three transport assemblies, and say in R-24 which projects the sample references.

**New in remediation**: yes — R-24's second paragraph and AC-27 were both written in round 1's remediation (finding 13).

---

### 2. R-4 and AC-3 assert a handler-invocation count the FR-23 template cannot produce, and no requirement owns the template work (Score: 78)

R-4 now reads: "the handler is not invoked again for that message, and the count of handler invocations for it stands at `R` or fewer when the FR-23 observation window closes." AC-3 repeats it.

It cannot be asserted. Neither FR-23 template counts handler invocations, and the shared harness has no counter to read: `ConformanceDeferredCommandHandler.Handle` simply throws `DeferMessageAction`, and the handler factory is `new ConformanceHandlerFactory(() => new ConformanceDeferredCommandHandler())` — a **fresh handler instance per dispatch**, so even an instance field would not accumulate. Making AC-3 assertable requires a shared counter in `ConformanceDeferredPump`, a new assertion in both templates, and regeneration of every generated FR-23 test across thirteen configurations.

No requirement owns that. R-27 is explicit: "**Two changes**, both to test-infrastructure code" — the provider values and the GCP IAM members. Template and shared-harness work appears nowhere. This is the same class of defect round 1 filed as finding 7, reintroduced one layer up.

**Evidence**: the Reactor FR-23 template's only assertions are at `:107` (`Assert.NotEqual(MessageType.MT_NONE, …)`), `:112` (`AssertIsTheMessageSent`) and `:126` (`Assert.True(dlqMessage.Header.HandledCount >= deliveriesExpected, …)`); nothing touches the handler. The Proactor twin is identical. `ConformanceDeferredPump.cs.liquid:45-49` (handler) and `:128` / `:152` (per-dispatch handler construction).

**Recommendation**: Either add the template/harness change to R-27 as change (c), with an AC asserting it, or drop the invocation-count clause and assert only what the template can see.

**New in remediation**: yes — R-4's window definition and AC-3 were both rewritten in remediation (finding 9).

---

### 3. R-13's A-2-refuted branch has no acceptance criterion, nothing measures A-2, and AC-19 and AC-22 dangle if it is refuted (Score: 76)

R-13 says it is "conditional in the same shape R-14 is, on assumption A-2, and both sides have a defined 'done'". It gained the prose but not the criteria. Compare:

- R-14 → **AC-23** (the measurement that settles the condition), **AC-24** (condition holds), **AC-25** (condition does not hold). Three ACs, both branches assertable.
- R-13 → **AC-19** only, which asserts the A-2-holds outcome alone.

So: (i) nothing measures A-2 — no AC-23 equivalent, no stated inputs, no "write the measurement into `conformance-status.md`" exit criterion, even though R-13's refuted branch (c) demands exactly that; (ii) the refuted-branch "done" has no AC-25 equivalent; (iii) if A-2 is refuted, AC-19 simply fails with no defined alternative, and AC-22 (which runs "the GCP conformance behaviours named in AC-15 **and AC-19**") fails with it. The map row `R-13 | AC-19` claims coverage the document does not have, and AC-30's conditional GCP FR-23 row points at a branch no criterion can close.

**Evidence**: `requirements.md` R-13's two bullets; the map row; AC-19; AC-22; A-2's "*Not verified.*"

**Recommendation**: Mirror R-14's shape exactly — an AC that measures A-2 with assertable exit criteria, a guarded AC-19, an AC-25 twin for the refuted branch, and a guarded AC-22.

**New in remediation**: yes — R-13's two-branch structure was added in remediation (finding 3); the ACs were not.

---

### 4. R-20 tolerates `GetProjectAsync` per call but does not say what the helper does next, and the code cannot continue past it (Score: 72)

R-20 is framed strictly per-call: "**each** is logged at Warning naming the RPC, the resource and the status code, **and channel creation continues**", with a table enumerating the RPCs individually as if each were independently skippable.

They are not. `GetProjectAsync` exists solely to **derive** the member the subsequent IAM calls consume. Tolerating it and continuing leaves `publishMember` / `subscriberMember` null, and the very next statement passes that null to protobuf. Two developers resolve this differently — one wraps each RPC and continues (into an `ArgumentNullException` on a path NFR-5 forbids catching with `catch (Exception)`), the other abandons the whole helper and never attempts the binding. Only the second works, and the document does not say so. AC-20 does not disambiguate either.

This matters most on exactly the path remediation added: `Unauthenticated` from `GetProjectAsync` is the emulator case, so it is the case R-21's local bar runs through every time.

**Evidence**: `GcpPubSubMessageGateway.cs:479-491` — `var publishMember = …PublisherMember;` then the `IsNullOrEmpty` guard deriving it from `GetProjectAsync` — and `:505-512`, where `publishMember` is passed to `Members.Contains(...)` and `binding.Members.Add(publishMember)`. `:534-543` is the same shape for `subscriberMember`.

**Recommendation**: State the unit of tolerance — a tolerated status from `GetProjectAsync` abandons the **whole IAM helper** for that call site; a tolerated status from `GetIamPolicyAsync`/`SetIamPolicyAsync` abandons only the remainder of that helper. Fold into AC-20's Then.

**New in remediation**: yes — the `GetProjectAsync` tolerance is what remediation added (finding 1).

---

### 5. AC-21's negative case cannot be produced, and after R-20's widening no reachable IAM status is left for it (Score: 70)

AC-21's worked example — `NotFound` for a dead-letter topic that does not exist — cannot happen. When `DeadLetter != null`, `EnsureSubscriptionExistsAsync` **first recursively creates the dead-letter topic and subscription** with `makeChannels: OnMissingChannel.Create` hard-coded, and only then calls `UpdateIAmRoleForDeadLetterAsync`. By the time `GetIamPolicyAsync` runs against the DLT, Brighter has just created it.

Nor is there a replacement. On the emulator every IAM call returns `Unimplemented` (now tolerated). Against a real project the calls succeed, or return `PermissionDenied` / `Unauthenticated` (now tolerated). `InvalidArgument`, `DeadlineExceeded` and `ResourceExhausted` are not producible deterministically, and C-10 forbids mocks for isolation. So R-20's "No other status code is swallowed" — the clause that keeps the widened tolerance honest — and NFR-5, also mapped to AC-21, have no implementable verification.

**Evidence**: `GcpPubSubMessageGateway.cs:220-236` — `if (pubSubSubscription.DeadLetter != null) { await EnsureSubscriptionExistsAsync(new GcpPubSubSubscription(… makeChannels: OnMissingChannel.Create …)); await UpdateIAmRoleForDeadLetterAsync(projectId, pubSubSubscription); }`.

**Recommendation**: Replace the example with a case that is actually reachable and say how it is produced — most honestly, a unit-level test over the status-code filter asserting `NotFound` is rethrown while the three tolerated codes are not.

**New in remediation**: yes — AC-21 was restated in remediation and acquired the `NotFound` example.

---

### 6. R-18 and AC-15 contradict each other on `rejectionMessage` for the `None` case (Score: 70)

R-18: "`rejectionMessage` = the description, **when a non-empty description was supplied**." AC-15: "**When** `Reject` is called with each of `Unacceptable`, `DeliveryError` and `None`, **Then** … **in every case** it carries the five rejection-metadata keys."

R-16's own worked example for `None` is `Reject(message, new MessageRejectionReason(RejectionReason.None))` — **no description**. Under R-18 that message carries four keys; under AC-15 it must carry five. A test written from AC-15 fails; one written from R-18 does not test what AC-15 says.

**Evidence**: R-18, R-16's `None` example, AC-15's "in every case". The reference behaviour is `SqsMessageConsumer.cs:496-513` (`RefreshMetadata`): `rejectionReason` always, then `if (!string.IsNullOrEmpty(reason.Description)) { … rejectionMessage … }`.

**Recommendation**: Reword AC-15 to four keys always plus `rejectionMessage` whenever a non-empty description was supplied, or supply a description in the `None` case in both R-16's example and AC-15's Given.

**New in remediation**: no — pre-existing, and missed by round 1.

---

### 7. The new closing note claims every unmarked AC is test-assertable; AC-33 and AC-34 are not (Score: 66)

The map's closing paragraph states that AC-23, AC-28 and AC-30/AC-31 are manual gates and "**Every other AC in this document is an assertion a test can make.**" Two of the six ACs added in the same remediation falsify it:

- **AC-33**, final clause: "**And Given** the ADR, **When** it is read, **Then** it states how R-2's exact `0` is achieved…"
- **AC-34**, first clause: "**Given** the ADR, **When** it is read, **Then** it contains a four-row table classifying…"

Both are document reviews. Neither is marked. The honesty note exists precisely so nobody expects a green suite to prove them, and it now says the opposite of the truth for the two ACs carrying R-2's and R-3's load-bearing ADR obligations. (AC-11's Given is a softer instance: not unassertable, but not resolvable until the ADR exists.)

**Evidence**: AC-33, AC-34, and the map's closing paragraph.

**Recommendation**: Split each into its assertable half and its ADR-review half, mark the ADR-review halves ⚠️ manual gate where they appear, and add them to the closing note's list.

**New in remediation**: yes — AC-33, AC-34 and the closing note were all added in remediation.

---

### 8. R-20's second and third examples say "one Warning" where two helpers each log one (Score: 64)

R-20's ⚠️ paragraph is dedicated to the point that **both** IAM helpers are in scope. Its examples then forget it: "only `GetIamPolicyAsync`'s `Unimplemented` is tolerated, and **one Warning** is logged"; "`GetProjectAsync` returns `PermissionDenied`, **one Warning** is logged".

Both helpers call `GetIamPolicyAsync`, and both call `GetProjectAsync`. With members set, two `GetIamPolicyAsync` calls fail → two Warnings. With a restricted principal, two `GetProjectAsync` calls fail → two Warnings. R-20's *first* example gets this right ("one Warning per tolerated call"), so the requirement contradicts itself two lines later, and AC-20's second branch inherits the wrong count.

**Evidence**: `GcpPubSubMessageGateway.cs:235` → `UpdateIAmRoleForDeadLetterAsync` (`:477`), `GetIamPolicyAsync` at `:497`; `:251` → `UpdateIAmRoleForSubscriptionAsync` (`:527`), `GetIamPolicyAsync` at `:546`. `GetProjectAsync` at `:485` and `:537`. Both helpers run unconditionally when `DeadLetter != null`.

**Recommendation**: "one Warning per tolerated call — two here, one from each helper", and make AC-20's second branch assert two.

**New in remediation**: yes — the "members configured" example and the tolerance widening are remediation text.

---

### 9. AC-9 and AC-10 use harness values R-27 forbids, with no stated provenance for the subscriptions they need (Score: 52)

R-27 requires all twelve gateway providers to hold `R = 3`, `M = 5`. Two ACs need configurations that will then exist nowhere: **AC-9** needs SQS `requeueCount: 10` with `maxReceiveCount: 3`; **AC-10** needs `requeueCount: 5` against a native limit of `5` — precisely the **old** GCP harness value R-27 removes, and the configuration R-10's example cites as "the current harness configuration".

Neither AC says where its subscription comes from. A reader who assumes "the conformance provider" is contradicted by R-27; one who assumes a bespoke subscription has to invent the fixture, including (for AC-9) a real LocalStack queue with a second redrive policy.

**Evidence**: R-27(a) table, AC-8, AC-9, AC-10, R-10's example.

**Recommendation**: Say in AC-9 and AC-10 that the subscription is constructed by the test rather than taken from a gateway provider, and note in R-10's example that the cited values are pre-R-27.

**New in remediation**: yes — R-27 is new; the collision is the seam between it and untouched ACs.

---

### 10. R-27(b) and AC-36 require the IAM members to be "set explicitly" but never say to what (Score: 50)

No value is given, and the choice is not free. The guard is `string.IsNullOrEmpty`, so an empty string trips the guard *and* fails to derive — the `publishMember ??=` fallback does not fire for `""`, leaving a null-ish member flowing into `Members.Add`.

**Evidence**: `GcpPubSubMessageGateway.cs:482-490`. Both properties are settable as R-27(b) needs: `DeadLetterPolicy.PublisherMember` is `{ get; set; }` (`DeadLetterPolicy.cs:27`); `GcpPubSubSubscription` takes `subscriberMember` as a constructor parameter (`:129`, assigned `:149`).

**Recommendation**: State the shape — "a non-empty `serviceAccount:…` string" — in R-27(b), and assert non-emptiness in AC-36.

**New in remediation**: yes — R-27 and AC-36 are both new.

---

### 11. R-12's headline says "all four AWS configurations" and its body lists eight (Score: 50)

R-12's obligation sentence — the one a reader extracts as the requirement — under-states the scope by half: "across **all four AWS configurations**", then two sentences later "**The eight configurations are** …". AC-12 and AC-30 both use eight. Round 1's finding 16 was the same class of defect in the Problem Statement and was fixed there; this one was untouched.

**Evidence**: R-12, first and third sentences.

**Recommendation**: "across all eight AWS and AWS.V4 configurations and both variants".

**New in remediation**: no — pre-existing, missed by round 1.

---

### 12. NFR-4's framing does not describe the R-7 case it claims to cover (Score: 44)

NFR-4: "**An inert or unreachable budget is visible at Warning, once per subscription.** R-7, R-10 and R-11 are all instances of this." R-11's budget is unreachable and R-10's is outranked, but R-7's `R == 0` / `R < -1` budget is neither — it is the opposite, an *over-eager* budget that rejects on first delivery. AC-29 inherits the framing.

**Evidence**: NFR-4 and AC-29 versus R-7's "the first deferral also rejects immediately".

**Recommendation**: "A budget that will not behave as its author probably intended is visible at Warning, once per subscription."

**New in remediation**: no — NFR-4 is untouched text; R-7's `R < -1` half widens the mismatch.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 6 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 1 |

**Total findings**: 12
**Findings at or above threshold (60)**: 8

**Round-1 findings confirmed genuinely resolved**: 1 (core), 2, 4, 5, 6, 7, 8, 10, 11, 12, 14, 15, 16, 17, 18. Round-1 findings 3, 9 and 13 were addressed in prose but each left a hole — round-2 findings 3, 2 and 1/7 respectively.

**New citations verified correct in round 2**: `MessagePump.cs:171` / `Message.cs:161-164` (R-7's `R < -1` reasoning is sound; `Reactor.cs:492-514` confirms the reject path returns before `Channel.Requeue`, so "Requeue is never called" holds; `Subscription` performs no validation on `requeueCount`, so `0` and `-3` are constructible). C-11 in full — `DeadLetterPolicy.cs:27`, `GcpPubSubSubscription.cs:102`, and the guards at `GcpPubSubMessageGateway.cs:482-491` / `:534-543` each wrapping the only `GetProjectAsync` call (`:485`, `:537`). R-27's table: all twelve provider files exist and hold the stated values — AWS/AWS.V4 at `SnsStandard:115,119`, `SnsFifo:127,132`, `SqsStandard:104,108`, `SqsFifo:113,117` in both projects; GCP at `GcpPull:151,155`, `GcpPullOrdering:161,165`, `GcpStream:151,155`, `GcpStreamOrdering:161,165`. The ledger's `Unauthenticated` measurement at `conformance-status.md:489` and the `PublisherMember` probe at `:492-494`. `Reactor.cs:494`/`:498`, `Proactor.cs:500`/`:504`. `DefaultMessageAssertion.cs.liquid:59`. `SqsMessageConsumer.RefreshMetadata` `:496`. RocketMQ's 10 s lease at `RocketMqMessageGatewayProvider.cs:137` (harness-specific; the `RocketMqSubscription` default is 30 s, correctly stated as such). The full matrix at `conformance-status.md:817-842` — R-22's nine rows, R-23's eleven columns, AC-30's 8+20+4+1=33 with 28/5 arithmetic, AC-38's nine RocketMQ cells. The R→AC map end to end: no row cites a non-existent AC, no AC-1..AC-38 absent, AC-30/AC-31 the only unmapped ones.

---

# Remediation log — round 2

**Date**: 2026-09-21. **Applied to**: `requirements.md`.
**Outcome**: all 8 findings at or above threshold remediated, plus all 4 below it. Document went from
27 R / 38 AC to **27 R / 8 NFR / 40 AC**, integrity re-checked: no requirement without a map row, no
map row citing a non-existent AC, no numbering gap, AC-30/AC-31 still the only deliberate omissions.

**Eight of the twelve findings were defects round 1's remediation introduced.** That is the round's
main lesson: rewriting to close a gap in prose reliably opens a new one where the prose meets
untouched text or meets the code. Reasoning below; `requirements.md` carries only obligations.

| # | Score | Remediation |
|---|---|---|
| 1 | 82 | **Confirmed in source before acting** — `Paramore.Brighter.Core.Tests.csproj` has seven project references and not one is a gateway, so the sample as specified could not compile on the base revision, let alone fail on a breaking change. Rather than add gateway references to a core unit-test project (a layering change no requirement sanctions), R-24 now places each sample in a project that **already** references the assembly it exercises: core types in `Core.Tests`, `SqsSubscription` in `AWS.Tests`, its v4 twin in `AWS.V4.Tests`, `GcpPubSubSubscription` in `Gcp.Tests`, `RocketMqSubscription` in `RocketMQ.Tests` — all four verified to reference their gateway today. R-24 states explicitly that `Core.Tests` must not acquire a gateway reference for this purpose, and that the samples are compile-only so they need no broker. |
| 2 | 78 | **Took the "own the work" branch, not the "weaken the assertion" branch.** Verified the finding first: the pump builds a fresh handler per dispatch and the handler only throws, so nothing accumulates. R-4's clause is load-bearing — without it FR-23 shows only that a message *reached* the DLQ, never that it stopped being delivered — so weakening it would have hollowed out the requirement the whole spec exists to make true. R-27 therefore becomes **three** changes, gaining **(c)**: a per-message-id dispatch count exposed by the shared pump, asserted in both FR-23 templates, with the generated tests regenerated. AC-36 gained a clause asserting that a run dispatching more times than the budget allows fails on that assertion rather than passing on DLQ arrival. |
| 3 | 76 | R-13 now has R-14's full shape, not just its prose: **AC-39** measures A-2 against the emulator with three assertable exit criteria (ledger paragraph, ADR records the outcome and any consequent mechanism change, exactly one branch claimed); **AC-19** is guarded on AC-39 having recorded a populated counter *or* the ADR having selected a counter-independent mechanism; **AC-40** is the refuted-branch twin, and states that R-15 to R-19 are unaffected so the twenty routing cells still move. AC-22's AC-19 clause is guarded to match. Map row is now `R-13 | AC-19, AC-39, AC-40`. |
| 4 | 72 | **The unit of tolerance is now the helper, not the RPC.** R-20 states why — `GetProjectAsync` exists only to derive the member the later calls consume, so continuing past it carries a null into the binding — and specifies that a tolerated `GetProjectAsync` abandons the whole helper while a tolerated `GetIamPolicy`/`SetIamPolicy` abandons only its remainder, with the other helper unaffected either way. **This decision propagated further than the finding did**: with the helper abandoned at `GetProjectAsync`, `GetIamPolicyAsync` is never reached on the emulator's members-unset path, so R-20's first example and AC-20's first branch both had to be rewritten — they previously claimed both RPCs were tolerated in the same run, which the new rule makes impossible. |
| 5 | 70 | AC-21's broker-level negative case was unreachable (Brighter creates the DLT with `OnMissingChannel.Create` immediately before the IAM helpers run), and after R-20's widening no reachable IAM status is left. AC-21 now exercises **the status-code filter directly as a unit test** — `NotFound`, `InvalidArgument`, `DeadlineExceeded`, `ResourceExhausted` rethrown; the three tolerated codes tolerated — with a note recording why a broker-level case is not available and must not be attempted, so nobody re-derives it. This is what keeps R-20's "no other status code is swallowed" clause and NFR-5 verifiable at all. |
| 6 | 70 | AC-15 now asserts four keys always plus `rejectionMessage` whenever a non-empty description was supplied, matching R-18 and the `RefreshMetadata` reference behaviour, and says explicitly that R-16's description-less `None` example therefore carries four keys. |
| 7 | 66 | AC-33's ADR-review clause and AC-34's first clause are marked ⚠️ manual gate inline. The closing note was rewritten to list all of them — AC-23, AC-39, AC-28, AC-30/AC-31, and the two clauses — and to add a distinction the old note lacked: AC-11 is *assertable but not yet resolvable*, since its Given names shapes the ADR must first list. Split inline rather than into new AC numbers, so the assertable and reviewable halves stay adjacent to the requirement they serve. |
| 8 | 64 | Both examples corrected to two Warnings, one per helper, with R-20 now stating the general rule ("both helpers run whenever `DeadLetter != null`, so the tolerated-call count is two in the ordinary case") rather than leaving it to be inferred from each example. AC-20 asserts **exactly two** on both branches. |
| 9 | 52 | AC-9 and AC-10 now say their subscriptions are **constructed by the test**, not taken from a conformance gateway provider, and name R-27's fixed values as the reason. R-10's worked example is re-labelled as the configuration "as it stands before R-27" and notes that R-27 removes both. |
| 10 | 50 | R-27(b) specifies a **non-empty** `serviceAccount:…`-shaped string, with the reason stated: the guard is `string.IsNullOrEmpty`, so `""` trips it *and* fails to derive a replacement, which is the one way to set the members and still reach the path R-27(b) exists to avoid. AC-36 asserts the shape. |
| 11 | 50 | R-12's obligation sentence now reads "across all eight AWS and AWS.V4 configurations", matching its own body, AC-12 and AC-30. |
| 12 | 44 | NFR-4 reframed to "A budget that will not behave as its author probably intended", with the three instances named in-line (rejects on first delivery / outranked / cannot run down). AC-29 follows. |

## ⛔ Correction to this log — five rows above were false when written

**Round 3 found that five of the twelve remediations recorded above were never applied to
`requirements.md`.** Rows **1, 4, 6, 8 and 11** (R-24's sample location, R-20's unit-of-tolerance
paragraph, AC-15's key list, R-20's second example, and R-12's "eight configurations") describe
changes that this log asserts were made and that the document did not carry.

**Cause, established after the fact**: the script applying that batch validated every edit, printed
a success line for each, and then hit a failed anchor assertion on its *last* edit — which aborted
the run **before** the single write at the end. Five edits were reported as applied and none of them
was. The failing edit was then applied on its own, which left the batch looking half-complete rather
than wholly lost. A remediation log written from the script's output rather than from the document
recorded all five as done.

All five were re-applied during round 3's remediation and verified **in the document** afterwards.
The lesson, adopted from round 3 onward: **verify each remediation by reading the file back, never
by trusting the tool's own success output**, and write the log from what the file says.

## Citations introduced in this round, verified in source before writing

`Paramore.Brighter.Core.Tests.csproj`'s seven references and the four transport test projects' gateway
references (finding 1); `ConformanceDeferredPump.cs.liquid:45-49`, `:52-57`, `:128`, `:152` — fresh
handler per dispatch, body throws only (finding 2); `GcpPubSubMessageGateway.cs:220-236` — the
recursive DLT creation with `OnMissingChannel.Create` ahead of the IAM helpers (finding 5). The async
handler range was corrected from `:52-56` to `:52-57` after checking the closing brace.

## Carried forward unchanged from round 1

Still not done, still needing the user's say-so: raising the SQS failed-DLQ-send question as its own
issue; commenting on #4341 / #4386 / #4353 / #4354; re-pointing the ledger cells. AC-30 and AC-31
still have no owning requirement, by the same deliberate decision recorded in round 1.

## Next step

Re-run `/spec:review requirements` for round 3. Two of this round's remediations changed behaviour
that other text depended on — R-20's unit of tolerance (which rewrote AC-20 and one example) and
R-27's new change (c) — so the seams worth probing next are R-20/AC-20/NFR-5, and R-4/AC-3/R-27(c)/AC-36.

---

# Review: requirements (round 3) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

9 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Four of round 2's remediations are recorded in the log but absent from `requirements.md`; the R-24/AC-27 one leaves a requirement that is impossible as written (Score: 90)

The round-2 log states R-24 was rewritten to place each compile-only sample in a project that already references the assembly it exercises. **No such table or text exists in `requirements.md`.** R-24 and AC-27 still both name a single location, `tests/Paramore.Brighter.Core.Tests/`, for all six types — exactly what round-2 finding 1 (82) said cannot work.

Re-verified rather than trusted: `Paramore.Brighter.Core.Tests.csproj` has seven `ProjectReference` entries and not one messaging gateway; `tests/Directory.Build.props` adds none. A file there naming `SqsSubscription`, `GcpPubSubSubscription` or `RocketMqSubscription` does not compile on the base revision, so AC-27's "**When** the solution is built … **Then** it compiles with no new errors" is unsatisfiable and the automated gate R-24 exists to create does not exist.

The same divergence affects round-2 findings 4, 6, 8 and 11 — filed separately below as findings 2, 3, 4 and 12. The meta-problem stated once: **the remediation log cannot be used as evidence that the document was changed.**

**Evidence**: R-24 and AC-27 both naming `tests/Paramore.Brighter.Core.Tests/` and all six types. `Paramore.Brighter.Core.Tests.csproj:7-13`. By contrast `AWS.Tests:31`, `AWS.V4.Tests:30`, `Gcp.Tests:48` and `RocketMQ.Tests:30` each reference exactly the gateway the log's table assumes — the intended fix is sound; it is simply not in the document.

**Recommendation**: Apply the fix the log describes, then re-check the other rows of the log against the document before the next review.

**New in round 2 remediation**: yes — round-2 finding 1, logged as remediated and not applied.

---

### 2. AC-15 still asserts five rejection-metadata keys "in every case", contradicting R-18 and R-16's own `None` example (Score: 72)

Round-2 finding 6 (70) verbatim. The log claims AC-15 "now asserts four keys always plus `rejectionMessage` whenever a non-empty description was supplied". It does not.

**Evidence**: AC-15's "in every case it carries the five rejection-metadata keys"; R-18's "when a non-empty description was supplied"; R-16's description-less `None` example.

**Recommendation**: Apply the remediation as logged.

**New in round 2 remediation**: yes — logged as remediated and not applied.

---

### 3. R-20's "members configured" example says one Warning where AC-20 asserts exactly two (Score: 70)

R-20's first and third examples were corrected to two Warnings; the second was not. Two is correct: `UpdateIAmRoleForDeadLetterAsync` calls `GetIamPolicyAsync` at `GcpPubSubMessageGateway.cs:497` and `UpdateIAmRoleForSubscriptionAsync` at `:548`; both run whenever `DeadLetter != null` (`:235`, `:251`), and with both members set neither enters its `GetProjectAsync` guard.

A worked example that contradicts its own AC is the precise failure mode §"The one trap this document was written against" describes.

**Evidence**: R-20's second example versus AC-20's second branch. `GcpPubSubMessageGateway.cs:221-252`, `:479-500`, `:531-551`.

**New in round 2 remediation**: yes — remediated in two of three examples.

---

### 4. R-20's obligation text still reads per-RPC; the "unit of tolerance" decision exists only inside examples and AC-20 (Score: 68)

Grepping for "abandon" returns only two example lines, AC-20, and NFR-5's unrelated sentence. The normative paragraph is unchanged and still per-call, followed by a table enumerating RPCs as though each were independently skippable.

Not cosmetic: `publishMember` is derived from `GetProjectAsync` (`:482-491`) then passed into `Members.Contains(publishMember)` and `binding.Members.Add(publishMember)` (`:506-511`). A developer implementing R-20 literally carries a null into protobuf on exactly the emulator path R-21's bar runs through. Nothing states the `GetIamPolicy`-tolerated case at all, which AC-20's second branch silently depends on.

For the record, the rule **is** consistent with the code: `EnsureSubscriptionExistsAsync` awaits each helper for effect only and never reads a result (`:239-246`, `:251`). It is right; it simply is not written where a reader will apply it.

**New in round 2 remediation**: yes — logged as remediated and not applied to the obligation.

---

### 5. R-27(c) makes only the FR-23 templates able to count dispatches, but AC-5, AC-6 and AC-35 assert invocation counts outside FR-23, and nothing owns that work (Score: 68)

R-27(c) is scoped to the two FR-23 templates and to R-4/AC-3. Three other ACs make the same kind of claim outside that scope: AC-5 ("invoked more than 3 times"), AC-6 ("invoked exactly once, `Requeue` never called"), AC-35 (same at `0` and `-3`). Worse, R-27(a) fixes all twelve providers at `R = 3`, so no harness subscription supplies `-1`, `1`, `0` or `-3` — these need bespoke fixtures, a bespoke pump, a dispatch counter, and consumer instrumentation that C-10's no-mocks rule constrains. Round-2 finding 2's defect class recurring one AC-group over.

**Recommendation**: Widen R-27(c) to every AC that asserts an invocation count, or state in AC-5/AC-6/AC-35 how the count and the `Requeue` observation are obtained.

**New in round 2 remediation**: yes — R-27(c) is new; the gap is the seam.

---

### 6. AC-19's and AC-40's guards both admit "populated but non-advancing", so AC-39's "exactly one is claimed" can fail (Score: 66)

A counter the emulator populates but never advances — `1, 1, 1` — satisfies both guards' first clauses. Not contrived: it is exactly the shape R-14's condition exists to detect on the other transport, and A-2 is worded as "**populates** `delivery_attempt`", so a literal reading has A-2 *holding* while R-1 still fails. On that reading R-13's first branch promises the four GCP FR-23 cells move to `Fixed` on evidence that cannot support it.

**Recommendation**: Make AC-19's first disjunct "populated **and strictly advancing**", and restate A-2 as advancing rather than merely populated.

**New in round 2 remediation**: yes — AC-39 and AC-40 are both new.

---

### 7. The rewritten manual-gate note is still untruthful in three ways (Score: 64)

1. **AC-23 and AC-39 carry no ⚠️** despite the note saying each gate is "marked ⚠️ where it appears".
2. **Three unmarked ACs contain ledger-edit clauses** — AC-24, AC-25 and AC-40, the last written in the same remediation that rewrote this note.
3. **More than one AC is "assertable but not yet resolvable"** — AC-33's first clause and AC-34's second clause are both unwritable until the ADR exists, on the same footing as AC-11.

**New in round 2 remediation**: yes — the note, AC-39, AC-40 and the AC-33/AC-34 marks are all round-2 text.

---

### 8. NFR-7 claims evidence across all thirteen configurations, but two of its three ACs are now branch-conditional (Score: 60)

The RocketMQ conditionality is acknowledged; the GCP one is not — round 2 guarded AC-19 on AC-39's measurement, so on the AC-40 branch AC-19 is never claimed and NFR-7's coverage claim becomes false. Round-1 finding 14 was this exact defect against AC-24; the fix has been half-undone by the AC-19 guard.

**New in round 2 remediation**: yes — the AC-19 guard is round-2 text; NFR-7 was not updated with it.

---

### 9. AC-13 requires v3 and v4 metadata **values** to be identical, which `originalTopic` and `rejectionTimestamp` cannot be (Score: 60)

The two runs use separately-provisioned queues and topics, so `originalTopic` differs by construction and `rejectionTimestamp` differs always. As written the AC cannot pass, and two developers resolve it differently.

**New in round 2 remediation**: no — pre-existing, missed by rounds 1 and 2.

---

### 10. AC-37 defers to "the measurement's stated tolerance", which nothing states, and NFR-2 says "no increase" (Score: 58)

A tolerance is not the same obligation as "no increase". One developer asserts `post <= base`; another allows a few percent for GC noise and passes a change NFR-2 forbids.

**New in round 2 remediation**: no — AC-37 is round-1 text, unreviewed in round 2.

---

### 11. R-27(c) is implementable, but does not say what the count is keyed from, when it resets, or what the expected value is (Score: 55)

Asked directly whether R-27(c) can be built as written: **yes, but only after the implementer finds a route the document does not mention.** The handler is handed a `ConformanceDeferredCommand`, not a `Message`, so "a dispatch count per message id" is not reachable from the handler body. The route exists — both pumps set `context.OriginatingMessage` (`Reactor.cs:416`, `Proactor.cs:483`) and `PipelineBuilder` assigns `Context` (`:280`, `:323`) — but a second developer will reasonably key on `command.Id`, the guid `CommandBody()` mints at serialise time, which is **not** the message id. Both work for FR-23's single-message test; they are not the same key, and the document picks neither. Also unstated: when the count is read, whether it resets between tests, and the expected value.

**New in round 2 remediation**: yes — R-27(c) is new.

---

### 12. R-12's obligation sentence still says "all four AWS configurations" where its body says eight (Score: 50)

Unchanged from round-2 finding 11, despite the log claiming otherwise.

**New in round 2 remediation**: yes — logged as remediated and not applied.

---

### 13. AC-22 runs "the GCP conformance behaviours named in AC-15 … and AC-19", but neither AC names a conformance behaviour (Score: 50)

AC-15 names four configurations, two consumers, two variants and three rejection reasons — no `FR-n`. AC-19 names configurations and a budget — no `FR-n`. The behaviours are named only in AC-30's table. A reader turning AC-22 into a command line has to guess which suite to run.

**New in round 2 remediation**: yes — AC-22's guard clause is round-2 text; the underlying vagueness is older.

---

### 14. Platform constraints are numbered out of order — C-11 precedes C-10 (Score: 25)

Purely cosmetic; both are cited correctly elsewhere.

**New in round 2 remediation**: yes — C-11 was inserted in an earlier remediation.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 10 |
| 0-49 (Low) | 1 |

**Total findings**: 14
**Findings at or above threshold (60)**: 9

**Round-2 findings confirmed genuinely resolved**: 2 (R-27(c) exists, R-4/AC-3's window defined), 3 (R-13 has AC-39/AC-40 and a guarded AC-22 — shape right, guards overlapping per finding 6), 5 (AC-21 is a unit test over the status filter, with the reachability note), 7 (AC-33/AC-34 marked inline), 9 (AC-9/AC-10 "constructed by the test"; R-10's example labelled pre-R-27), 10 (R-27(b)'s non-empty `serviceAccount:…` shape, asserted by AC-36), 12 (NFR-4/AC-29 reframed). Findings 1, 6 and 11 entirely unapplied; 4 and 8 applied only in part.

**New citations verified correct**: `ConformanceDeferredPump.cs.liquid:45-49`, `:52-57`, `:128`, `:152` (all four exact, including the corrected async closing brace); `GcpPubSubMessageGateway.cs:220-236` (recursive DLT creation with `OnMissingChannel.Create` at `:230` immediately ahead of the helper at `:235`, so AC-21's reachability note is sound), `:482-491`, `:534-543`, `:477`, `:527`, `:497`, `:548`, `:251`; `Core.Tests`' seven non-gateway references and the four transport projects' gateway references. Also re-verified fresh, unlisted by earlier rounds: `MessageHeader.cs:226`, `MessagePump.cs:171`, `Subscription.cs:109`/`:203`, `BrighterPipelineValidationExtensions.cs:78-84`, `ConsumerValidationRules.RequestTypeSubtype()` `:114`, `IUseBrighterDeadLetterSupport.cs` in `src/Paramore.Brighter/`, R-15's gateway list (exact), NFR-1's empty `ApproximateReceiveCount` grep, `SqsMessageCreator.cs:323`, `SqsInlineMessageCreator.cs:350`, `RocketMessageConsumer.cs:422` and the `ReadDelay` defect at `:433-435`. One cosmetic drift not filed: `GcpPubSub/Parser.ReadHandleCount` is at `:167`, not `~:169` — the "~" already signals approximation.

**Convergence**: **not converging** — not because the new text is weak (AC-39/AC-40, AC-21 and R-27(c) are the best-specified material in the spec) but because five of round 2's twelve findings, including its highest-scored one, are recorded as remediated in the log while `requirements.md` still carries the original defective text; until the log and the document are reconciled, each round re-finds what the previous round already closed on paper.

---

# Remediation log — round 3

**Date**: 2026-09-22. **Applied to**: `requirements.md`. **Every edit verified by reading the file
back afterwards** — see the correction notice in round 2's log for why that is now mandatory.
**Outcome**: all 9 findings at or above threshold remediated, plus all 5 below. Document holds at
**27 `R-n`, 8 `NFR-n`, 40 `AC-n`**; integrity re-checked (no requirement without a map row, no
dangling AC reference, no numbering gap, AC-30/AC-31 the only deliberate omissions) and the
constraint list is now in order C-1..C-11.

**Finding 1 is upheld in full, and the fault is process, not judgement.** The five "logged but not
applied" remediations were lost to a script that validated every edit, printed a success line for
each, then aborted on a failed anchor assertion in its last edit — before the single write at the
end. The failing edit was afterwards applied alone, which made the batch look half-done rather than
wholly lost. Round 2's log was then written from the script's output instead of from the document.
All five are now applied and verified in the file; round 2's log carries a correction notice naming
the five rows that were false when written.

| # | Score | Remediation |
|---|---|---|
| 1 | 90 | The five lost edits re-applied and **verified in the document**: R-24's five-row table placing each sample in a project that already references the assembly it exercises (`Core.Tests` for core types only, and the four transport test projects, each verified to reference its gateway today), with the statement that `Core.Tests` must not acquire a gateway reference; plus the four below. |
| 2 | 72 | AC-15 now asserts `originalTopic`, `originalMessageType`, `rejectionReason` and `rejectionTimestamp` always, plus `rejectionMessage` whenever a non-empty description was supplied, and says R-16's description-less `None` example therefore carries four keys. |
| 3 | 70 | R-20's second example corrected to "once in each helper, so two Warnings are logged" — all three examples and AC-20 now agree. |
| 4 | 68 | The unit-of-tolerance rule lifted into R-20's **obligation text**, covering all three RPCs explicitly (`GetProjectAsync` abandons the whole helper; `GetIamPolicyAsync` abandons the remainder so `SetIamPolicyAsync` is not called; `SetIamPolicyAsync` ends that helper), stating that the other helper still runs, and recording the reviewer's own confirmation that this is consistent with the code — `EnsureSubscriptionExistsAsync` awaits each helper for effect only and never reads a result. |
| 5 | 68 | R-27(c) widened from "the two FR-23 templates" to **every AC that asserts an invocation count**, naming AC-5, AC-6 and AC-35, recording that they run against bespoke subscriptions because R-27(a) fixes all twelve providers at `R = 3`, and that AC-6's and AC-35's "`Requeue` is never called" is observed on the consumer the test constructs rather than by mocking one (C-10). |
| 6 | 66 | The guards made mutually exclusive: AC-19 requires a counter "populated **and strictly advancing** across redeliveries"; AC-40 fires on "unpopulated, or populated but **not** strictly advancing (e.g. `1, 1, 1`)". A-2 restated to require both halves, with the `1, 1, 1` case named — it fails R-1 exactly as an absent counter does. |
| 7 | 64 | Note rebuilt as a list and made true: ⚠️ added to AC-23 and AC-39; the **ledger clauses** of AC-24, AC-25 and AC-40 added as gates with the distinction that their behavioural clauses remain ordinary assertions; and the "not writable until the ADR exists" group now names AC-11, AC-33's first clause and AC-34's second clause rather than claiming one AC. |
| 8 | 60 | NFR-7 no longer claims unconditional thirteen-configuration evidence. It states evidence is unconditional for the eight AWS cells (AC-12) and branch-conditional for GCP (AC-19) and RocketMQ (AC-24), and that on the AC-40 or AC-25 branch those configurations are not moved by this spec, so NFR-7 has nothing to evidence for them. |
| 9 | 60 | AC-13 split into what can match — delivery count, rejection reason, destination kind, the **set** of metadata keys, and the values of `rejectionReason`, `rejectionMessage`, `originalMessageType`, plus Warning messages — and what is instance-specific and compared for presence and shape only: `originalTopic`, the destination's own name, and `rejectionTimestamp`. |
| 10 | 58 | **The tolerance is zero**, stated in AC-37 and agreeing with NFR-2's "no increase". To keep zero assertable against allocation noise the figure compared is the median of five runs of 1,000 messages per revision, `post <= base` on the medians. |
| 11 | 55 | R-27(c) fully specified as five numbered obligations: keyed on `Context.OriginatingMessage.Header.MessageId` with the route named and `command.Id` explicitly excluded (it is the guid `CommandBody()` mints, not the message id); reset between tests; read after the pump is quit and awaited; asserted as `count <= RequeueCount`; available to the non-FR-23 ACs. A terminology note fixes "dispatch count", "handler invocation count" and "delivery count presented to the pump" as one thing, and distinguishes it from `MessageHeader.HandledCount`. |
| 12 | 50 | R-12's obligation sentence now reads "across all eight AWS and AWS.V4 configurations". |
| 13 | 50 | AC-22 names the behaviours directly — FR-4, FR-5, FR-6, FR-8, FR-17 across all four GCP configurations and both variants, plus FR-23 when AC-39 selects that branch — and notes these are the same behaviours AC-30's GCP rows move. |
| 14 | 25 | C-10 and C-11 swapped; the constraint list now reads C-1 through C-11 in order. |

## Process change adopted

Every remediation from here is verified by reading the target file back and grepping for the applied
text; logs are written from the document, not from a script's success output. Round 3's batches were
each followed by an explicit verification block, and the integrity check was re-run at the end.

## Carried forward, still needing the user's say-so

Raising the SQS failed-DLQ-send question as its own issue; commenting on #4341 / #4386 / #4353 /
#4354; re-pointing the ledger cells. AC-30 and AC-31 still have no owning requirement, by the
deliberate decision recorded in round 1.

## Next step

Round 4. The seams worth probing: R-27(c)'s five new obligations against AC-3, AC-5, AC-6, AC-35 and
AC-36; R-20's obligation text now that the tolerance rule lives there, against its three examples,
AC-20, AC-21 and NFR-5; and the AC-19/AC-40 guards against AC-39, AC-30 and NFR-7. A round-4 reviewer
should also spot-check that this round's remediations are present in the document rather than only in
this log.
