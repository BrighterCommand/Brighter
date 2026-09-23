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

---

# Review: requirements (round 4) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: NEEDS WORK

10 findings at or above threshold 60. Address these before approving.

## Findings

### 1. R-2's normalisation and R-5/AC-4's `>= R - 1` bound cannot both hold under the broker-counter mechanism, because the dead-letter destination is read through the same consumer seam (Score: 84)

R-2 places the normalisation in the consumer: "the consumer normalises it to `0` before handing the message to the pump" (line 133). §Candidate mechanisms names the exact seams that would change — `SqsMessageCreator.ReadHandledCount` (`:323`), `SqsInlineMessageCreator.ReadHandledCount` (`:350`), `GcpPubSub/Parser…ReadHandleCount`, `RocketMessageConsumer.ReadHandledCount` (`:422`). All four exist and are the only places `MessageHeader.HandledCount` is populated on receive for those transports.

Those seams serve **every** receive on the transport, including the conformance harness's read of the dead-letter destination, which goes through a real `ChannelFactory`/consumer — verified at `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsStandardMessageGatewayProvider.cs:281-311` (`new ChannelFactory(_awsConnection).CreateAsyncChannelAsync(dlqSubscription, …)` then `dlqChannel.ReceiveAsync(...)`).

So if the ADR picks "read the broker's own delivery counter on receive" — the candidate the document's own evidence favours (NFR-1's baseline says SQS system attributes are already on the wire at no extra cost; NFR-3 penalises the republish route; R-2 mandates a normalisation that is only meaningful under it; AC-33 tests the normalisation function) — then the DLQ read returns the **DLQ's own** receive count, normalised by R-2 to `0`, not the `handled-count` attribute the Brighter-managed send wrote. That falsifies, simultaneously:

- R-5: "a delivery count of at least `R - 1`" (line 181);
- AC-4: "its `Header.HandledCount` is `>= 2`" (line 934);
- the FR-23 template's **existing** assertion `Assert.True(dlqMessage.Header.HandledCount >= deliveriesExpected)` where `deliveriesExpected = _subscription.RequeueCount - 1` (`…/MessagingGateway/Reactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`, and its Proactor twin) — i.e. it turns the eight AWS FR-23 cells red against R-23 and AC-12.

The naive escape ("prefer the `handled-count` attribute when present, else the broker counter") is closed off too: on SQS the stored copy's `handled-count` attribute is never rewritten — that *is* defect #4341 — so a preference for the attribute defeats R-1.

The document was written against exactly this class of trap for the *identity* assertion — R-2's "Why this is an obligation" paragraph and C-7 exist for it — but the same reasoning was not carried to the DLQ read that R-5 and AC-4 depend on. Nothing in the requirements states the constraint (e.g. "a normalising read applies only to the channel the pump consumes, and must not rewrite `HandledCount` on a message read from a dead-letter or invalid-message destination").

**Evidence**: requirements.md:133 ("the consumer normalises it to `0` before handing the message to the pump") against :181 ("a delivery count of at least `R - 1`") and :934 ("its `Header.HandledCount` is `>= 2`"); `SqsStandardMessageGatewayProvider.cs:294-299`; the FR-23 template's `deliveriesExpected` assertion.

**Recommendation**: Add an obligation to Group A stating which reads the normalisation applies to and that the delivery count of a message read back from a rejection destination must be the count the rejecting consumer stamped, not the destination's own receive count — or, if that cannot be guaranteed, restate R-5/AC-4 and the template assertion in terms of a metadata key that survives. Name the conformance cells at risk, as R-2 already does for the identity assertion.

---

### 2. R-27(c)(1) mandates a dispatch-count key that is provably wrong on three of the configurations R-27(c)(4) applies it to, making the new assertion silently vacuous there (Score: 82)

R-27(c)(1) is categorical about the key:

> "**A dispatch count exposed by the shared pump**, keyed on the **originating message's `MessageId`** — reachable from a handler as `Context.OriginatingMessage.Header.MessageId`" (line 694)

and R-27(c)(4) applies the resulting assertion to "both FR-23 templates … and regeneration of the generated FR-23 tests for **every configuration**".

On the three RabbitMQ configurations the originating message's `MessageId` **changes on every redelivery**. Verified in source: `RmqMessagePublisher.RequeueMessageAsync` mints `var messageId = Uuid.NewAsString();` (`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessagePublisher.cs:131`) and records the first id in `x-original-message-id` via `AddOriginalMessageIdOnRepublish` (`:142`). The harness file R-27(c) itself cites says so in its own documentation:

> "Others - RabbitMQ, for one - republish under a fresh id and record the first one in the `x-original-message-id` header"

(`Templates/MessagingGateway/Shared/ConformanceDeferredPump.cs.liquid`), and the companion assertion `AssertIsTheMessageSent` already handles it by checking `deadLetteredId == sentId || republishedFrom == sentId`.

Consequence: on `RMQ.Async / Classic`, `RMQ.Async / Quorum` and `RMQ.Sync / RmqSyncMessagingGateway` the counter records `1` against each of three distinct keys, so `count <= RequeueCount` passes vacuously — the assertion R-27(c) exists to add is absent precisely where the harness knows the id moves. Worse, the same vacuity would hit the **in-scope** transports if the ADR selects "rewrite the stored message on requeue (republish)", which §Candidate mechanisms lists as a live option: the cells the spec exists to prove would then be proved by an assertion that cannot fail.

R-27(c) goes out of its way to exclude the wrong key (`command.Id`) but does not mention `x-original-message-id` at all — `grep -in "original message id\|originalmessageid"` over `requirements.md` returns nothing.

**Evidence**: requirements.md:694-697 versus `RmqMessagePublisher.cs:131,142` and `ConformanceDeferredPump.cs.liquid`'s identity remarks and `AssertIsTheMessageSent`.

**Recommendation**: Restate R-27(c)(1)'s key as the originating message's `x-original-message-id` when present, falling back to `Header.MessageId` — the same identity rule `AssertIsTheMessageSent` already uses — and say explicitly that a transport which republishes under a fresh id must still be counted as one message.

---

### 3. AC-20 hard-asserts a status code that R-20 itself says may never be produced, and R-20's client-construction case has no AC and no defined exception type (Score: 70)

R-20 admits two different failure shapes for the same call:

> "`GetProjectAsync` returns `Unauthenticated` in both helpers" (the emulator example)
> "A failure to *construct* the Resource Manager client — credential resolution failing before any RPC is issued, so there is no status code to inspect — is tolerated on the same terms and logged the same way." (line 453-456)

AC-20 then asserts only the first, as a hard `Then`: "`GetProjectAsync`'s `Unauthenticated` is tolerated in **both** `UpdateIAmRoleForDeadLetterAsync` and `UpdateIAmRoleForSubscriptionAsync`" (line 1092-1095).

Which shape occurs is decided by ambient credentials, not by the spec. `GcpMessagingGatewayConnection.CreateProjectsClientAsync` (`:173`) builds via `new ProjectsClientBuilder { Credential = Credential }` … `await builder.BuildAsync()`, which resolves Application Default Credentials when `Credential` is null. On a machine with no ADC — the ordinary state of a laptop or CI agent running the emulator, which is exactly R-21's mandated bar — construction throws and **no RPC is issued**, so AC-20's first branch cannot be observed. Spec 0036's measurement of `Unauthenticated` was evidently taken where ADC *was* present.

Meanwhile the client-construction case is the only clause of R-20 with no acceptance criterion at all: AC-20 covers the two RPC branches, AC-21 covers the status filter over `RpcException`, and neither reaches a pre-RPC construction failure. NFR-5 specifies it only as "caught by the narrowest exception type that call site can throw" (line 760-762) — which is not a name, and two developers will pick two different types.

**Evidence**: requirements.md:453-456, :760-762, :1088-1095; `GcpMessagingGatewayConnection.cs:173`ff.

**Recommendation**: Loosen AC-20's first branch to "the tolerated outcome of the `GetProjectAsync` step — an `Unauthenticated`/`PermissionDenied` status *or* a client-construction failure — is tolerated in both helpers", and add an AC for the construction case naming the exception type (or state that the ADR must name it and mark it as an ADR-dependent clause the way AC-11 and AC-33 are marked).

---

### 4. AC-19's and AC-40's guards can still both hold, and there is an outcome neither covers; AC-30's conditional row states AC-19's condition but not AC-40's (Score: 65)

Round 3's finding 6 was remediated on the *measurement* half only. The second halves of the two guards remain non-exclusive because they are about different things — a **selection** versus a **conclusion**:

- AC-19: "…**or** the ADR selected a GCP mechanism that does not read `delivery_attempt`"
- AC-40: "…**and** the ADR records that no mechanism satisfies R-1 to R-5 for GCP within NFR-1 to NFR-3"

Overlap: the counter is non-advancing; the ADR selects a `delivery_attempt`-independent mechanism (consumer-side tracking, say) and then records that it does not survive NFR-2. AC-19's second disjunct is satisfied (a mechanism *was* selected) and AC-40's guard is satisfied in full. Both apply, so AC-39's exit criterion (c) — "exactly one of AC-19 and AC-40 is then claimed" — fails.

Gap: the counter **is** populated and strictly advancing (A-2 holds), but the chosen mechanism nonetheless cannot satisfy R-1 to R-5 within NFR-1 to NFR-3 — e.g. it cannot reach R-2's exact `0` on a first delivery whose approximate counter is elevated, which R-2 explicitly contemplates as a possible outcome. AC-19's guard fires, so AC-19 must be claimed and will fail; AC-40's guard ("unpopulated, or populated but not strictly advancing") does not fire. R-13's branch text has the same shape: "**If A-2 holds** — 'done' = the four `GCP / *` FR-23 cells move to `Fixed`". There is no "bound but unimplemented" route out of a good measurement.

AC-30's conditional GCP row states AC-19's condition verbatim ("**if** assumption A-2 holds or the ADR selects a GCP mechanism independent of `delivery_attempt`") and its "otherwise" therefore does **not** match AC-40's stricter guard, so the ledger row and the AC disagree about when a cell stays `Deferred`.

**Evidence**: requirements.md AC-19, AC-40 and AC-39(c); AC-30's third table row.

**Recommendation**: Key both guards on the same two-part fact — the measurement **and** whether the ADR records a mechanism that satisfies R-1 to R-5 within NFR-1 to NFR-3 — so they partition all four combinations, and restate AC-30's conditional row and R-13's branch headers in the same terms.

---

### 5. A-5 says R-27 names "the eight provider files"; R-27 and AC-36 name twelve (Score: 62)

A-5's closing sentence: "**Owned by R-27**, which names the **eight** provider files and the required values." (line 843)

R-27(a): "**`R < M` on all twelve providers** — four AWS, four AWS.V4, four GCP" (line 665), with a table naming four `AWS.Tests` files, four same-named `AWS.V4.Tests` files and four `Gcp.Tests` files. AC-36 agrees: "the **eight** AWS/AWS.V4 gateway providers **and the four GCP** gateway providers named in R-27".

All twelve exist and hold the stated values: AWS/AWS.V4 `requeueCount: 3` with `RedrivePolicy(deadLetterChannelName, 3)` in all eight; GCP `requeueCount: 5` / `MaxDeliveryAttempts = 5` in all four (`GcpPullMessageGatewayProvider.cs:151,155`; `GcpPullOrdering…:161,165`; `GcpStream…:151,155`; `GcpStreamOrdering…:161,165`). So twelve is the correct figure and A-5 is the outlier. A reader working from A-5 reconfigures the AWS family and leaves the four GCP providers at `R == M == 5`, which is R-8's undetermined tie on exactly the configurations AC-19 depends on.

**Evidence**: requirements.md:843 versus :665 and AC-36.

**Recommendation**: Change A-5 to "twelve provider files".

---

### 6. R-27(c)(3) reads the counter at an instant the template proves is later than the one R-4 and AC-3 define as the window's close (Score: 62)

R-4: "That window is the **single** poll the template already runs — every 500 ms, giving up at 60 s, **ending when the dead-lettered message is found**." (line 167-169)

R-27(c)(3): "**Read after the pump has been quit and awaited**, **which is the instant R-4's "when the observation window closes" denotes**." (line 702)

The template proves these are two different instants. The poll loop `break`s on finding the DLQ message; the pump is quit two statements *later*:

```
        _channel.Enqueue(MessageFactory.CreateQuitMessage(_subscription.RoutingKey));
        await pumping;
```

(both FR-23 templates, immediately after the poll loop).

Between the two instants the pump is still dispatching, which is precisely the interval in which a broken budget would keep redelivering. Reading after the quit is the better choice — but then R-4's and AC-3's definition of the window is wrong, and AC-3's parenthetical "(the template's existing single 60 s poll — no additional wait is introduced)" describes a different measurement point from the one R-27(c)(3) mandates. As written, the document asserts an identity that its own cited artefact contradicts.

**Evidence**: requirements.md:167-169, :702, AC-3; the FR-23 templates' poll loop and the quit/await that follows it.

**Recommendation**: Redefine R-4's observation window (and AC-3's parenthetical) as closing when the pump has been quit and awaited, and delete the claim that this is the same instant as the poll's break.

---

### 7. AC-11 — R-11's only acceptance criterion — may have no instantiation at all, and no outcome is defined for that case (Score: 62)

AC-11's Given is entirely dependent on the ADR producing a list: "**Given** a subscription carrying `requeueCount: 3` of one of the shapes the ADR names under R-25 as tripping R-11 — at minimum, **if** the ADR selects a counter-based GCP mechanism, a `GcpPubSubSubscription` with no `DeadLetterPolicy` (A-1)".

R-25 explicitly permits that list to be empty:

> "The ADR MUST also name, for each of the four transports in scope, at least one subscription shape that trips R-11 under its chosen mechanism — **or state that none does**" (line 634-635)

If the ADR selects a `delivery_attempt`-independent GCP mechanism, R-14's condition holds, and no other shape trips R-11, then AC-11 has no Given, R-11 — the "anti-silence requirement", the one the document calls "the exact silence this defect family exists to close" (R-26) — is left with no exercised acceptance criterion, and R-25's rule and R-26's channel-creation log go unbuilt and unverified. Every other conditional requirement in this document (R-13, R-14) was given a defined "done" on **both** branches; R-11 was not.

**Evidence**: requirements.md:634-635 and AC-11; R→AC map row "R-11 | AC-11".

**Recommendation**: Give AC-11 a second branch for the "no shape trips R-11" outcome — at minimum, that the rule is still implemented and unit-tested against a synthetic subscription that answers the predicate negatively, and that the ADR's statement is recorded as the evidence.

---

### 8. What a tolerated-IAM Warning must contain is stated four different ways (Score: 62)

Four places specify the content of the same log line, and no two agree:

| Where | Required content |
|---|---|
| R-20 obligation paragraph (line 433-434) | "the RPC, the resource and the status code" |
| R-20's `GetProjectAsync` bullet (line 443) | "the helper and the RPC" |
| NFR-5 (line 757-759) | "the RPC, the resource name and the status code" |
| AC-20 (line 1095) | "each naming **the helper**, the RPC, the resource and the status code" |

A developer implementing to R-20's paragraph or to NFR-5 omits the helper name, and AC-20's assertion then fails. The helper name is the one element that actually distinguishes the two Warnings AC-20 counts, so it is not a decorative difference — it is what makes "exactly two Warnings, one per helper" checkable at all. Given the Warning *count* has now been the subject of findings in rounds 2 and 3, the Warning *content* deserves the same single statement.

**Evidence**: requirements.md:433-434, :443, :757-759, :1095.

**Recommendation**: State the four required elements once, in R-20's obligation paragraph, and have the bullets, NFR-5 and AC-20 all refer to that list rather than restating it.

---

### 9. "The three RMQ rows … need no Brighter-side budget to get there" is false in source, and contradicts R-5's own justification for the `>= R - 1` bound (Score: 62)

The Problem Statement splits the nine conforming configurations into two causal groups:

> "Six of the nine that work — Redis, Kafka ×3, MSSQL, Postgres — requeue by **republishing**, so the header travels; the three RMQ rows reach the dead-letter destination by the broker's own DLX instead **and need no Brighter-side budget to get there**."

R-22 repeats it: "They requeue by republishing (or, for the three RMQ rows, dead-letter natively)".

RabbitMQ also requeues by republishing, and the republished copy carries the count. Verified: `RmqMessageConsumer.RequeueAsync` → `rmqMessagePublisher.RequeueMessageAsync(...)` (`RmqMessageConsumer.cs:438`), which calls `AddCloudEventsHeaders(message)`, which writes `[HeaderNames.HANDLED_COUNT] = message.Header.HandledCount` (`RmqMessagePublisher.cs:174`), and `RmqMessageCreator.ReadHandledCount` reads it back (`:208-210`). The RMQ budget therefore *does* run down; the DLX is only how the message reaches the destination **after** Brighter's reject.

This is not a free-standing inaccuracy: R-5's justification for choosing `>= R - 1` rather than `== R` depends on RMQ having republished —

> "where the broker moves its own stored copy the copy was written by the last requeue and reads one less"

— which is only true if a last requeue republished an incremented count. The Problem Statement's claim and R-5's rationale cannot both be right.

**Evidence**: requirements.md Problem Statement (¶2) and R-22 versus :181-187 (R-5's bound rationale); `RmqMessageConsumer.cs:438`, `RmqMessagePublisher.cs:129-142, 160-174`, `RmqMessageCreator.cs:208-210`.

**Recommendation**: Restate as "all nine requeue by republishing, so the header travels; the three RMQ rows differ only in that the rejected message reaches the destination via the broker's DLX rather than a Brighter-managed send" — which is also what the FR-23 template's own explanatory comment says.

---

### 10. NFR-4's "once per subscription" and AC-29's "at most one" contradict R-25's three independently-firing rules and R-26's double reporting (Score: 60)

NFR-4: "A budget that will not behave as its author probably intended is visible at Warning, **once per subscription**."
AC-29: "…the total count of budget-configuration Warnings **for that channel remains at most one**." (line 1169)

R-25 registers three independent rules, and their conditions are not mutually exclusive. A `GcpPubSubSubscription` with `requeueCount: 0` and no `DeadLetterPolicy` (the very shape AC-11 names) trips R-7's rule *and* R-11's rule — two validation findings for one subscription. R-26 then adds a third Warning for R-11 at channel creation: "R-11 is therefore reported by **both** routes: the R-25 rule, and a single Warning at channel creation".

AC-29 never says whether it counts validation findings, channel-creation logs, or both, so as written it is either unsatisfiable (if it counts everything) or trivially satisfiable (if it counts only channel-creation logs, where R-7 and R-10 contribute zero by R-26's own rule). Two developers will read it two ways, and the intended obligation — "nothing is raised per message" — is only one of its two clauses.

**Evidence**: requirements.md NFR-4, AC-29 (:1167-1170), R-25's three-rule table, R-26.

**Recommendation**: Rewrite NFR-4 as "at most one Warning per subscription **per rule**, never per message", and rewrite AC-29's second clause to bound the *channel-creation* log at one and say explicitly that validation findings are counted by AC-7/AC-10/AC-11 instead.

---

### 11. AC-3 — the acceptance criterion for the spec's central requirement — names neither a transport nor a variant, where its siblings now do (Score: 55)

Round 1's finding 10 ("AC-5 and AC-6 name no transport") was remediated in AC-5 ("**Given**, on each transport in scope and in both variants…"), AC-6 (same) and AC-35 ("on a transport in scope in both variants"). AC-3, the sole AC for R-4, was not:

> "**AC-3** (R-4) — **Given** `requeueCount: 3`, a `deadLetterRoutingKey`, a handler that always defers, and no native redrive limit at or below 3, **When** the pump runs, **Then** …"

AC-1 says "any transport in scope"; AC-3 says nothing. One developer writes it once; another writes it four times across two variants.

(Separately, AC-6's "on **each** transport in scope" and AC-35's "on **a** transport in scope" disagree about the same kind of obligation under the same requirement, R-7.)

**Evidence**: requirements.md AC-1, AC-3, AC-5, AC-6, AC-35.

**Recommendation**: Give AC-3 the same Given prefix as AC-5 and AC-6, and reconcile AC-6 and AC-35 on "each" versus "a".

---

### 12. AC-6's and AC-35's "`Requeue` is never called" is specified only by what it must not be (Score: 52)

R-27(c)(5) closes round 3's finding 5 for the *count*, but for the second observation it says only:

> "AC-6's and AC-35's "`Requeue` is never called" is observed on the consumer the test constructs, not by mocking one (C-10)."

That names the prohibited technique, not the permitted one. "Observed on the consumer the test constructs" admits at least three readings: a recording decorator over a real consumer, a counter added to the production consumer, or inference from the absence of a redelivery within some unstated interval. The dispatch counter got five numbered obligations; this observation got half a sentence.

**Evidence**: requirements.md R-27(c)(5).

**Recommendation**: Add a sixth numbered obligation specifying how the requeue observation is made, in the same detail as the dispatch counter (key, reset, read point).

---

### 13. R-25's "or state that none does, which is itself the classification R-3 requires" cross-references the wrong requirement (Score: 45)

R-3's classification is the exact-versus-approximate four-row table ("The ADR must record, for each of the four transports in scope, which of the two classifications applies and on what evidence, as a four-row table"), and AC-34 tests that table. A statement that no subscription shape trips R-11 is not that classification and does not satisfy it.

**Evidence**: requirements.md:634-635 ("— or state that none does, which is itself the classification R-3 requires") against R-3's obligation paragraph and AC-34.

**Recommendation**: Drop the trailing clause, or point it at AC-11 rather than R-3.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 9 |
| 0-49 (Low) | 1 |

**Total findings**: 13
**Findings at or above threshold (60)**: 10

## Main-agent validation of this round

Counts re-checked one by one and they match the findings listed (High: 84, 82, 70; Medium: 65, 62, 62, 62, 62, 62, 60, 55, 52; Low: 45).

The findings that make factual claims about the codebase were verified in source before this file was
written:

- **Finding 1** — `SqsStandardMessageGatewayProvider.GetMessageFromDeadLetterQueueAsync:281-316`
  builds a real `ChannelFactory` channel and calls `ReceiveAsync`, so the DLQ read does go through
  the same consumer seam R-2 would normalise. The template's `deliveriesExpected =
  _subscription.RequeueCount - 1` assertion confirmed in both FR-23 templates.
- **Finding 2** — `RmqMessagePublisher.cs:131` mints `Uuid.NewAsString()` on requeue and `:142`
  records the original via `AddOriginalMessageIdOnRepublish`; `ConformanceDeferredPump`'s own
  remarks say RabbitMQ "republish[es] under a fresh id", and `AssertIsTheMessageSent` already
  accepts either id. Confirmed.
- **Finding 3** — `GcpMessagingGatewayConnection.CreateProjectsClientAsync:173` builds
  `ProjectsClientBuilder { Credential = Credential }`, so a null credential resolves ADC and can
  fail before any RPC. Confirmed.
- **Finding 5** — `grep` confirms requirements.md:665 says "twelve providers" and :843 says "eight
  provider files". Confirmed.
- **Finding 6** — both FR-23 templates `break` out of the poll loop on finding the DLQ message and
  only then `Enqueue(CreateQuitMessage(...))` / `await pumping`. The two instants are genuinely
  different. Confirmed.
- **Finding 9** — `RmqMessageConsumer.cs:438` requeues via `RmqMessagePublisher.RequeueMessageAsync`;
  `RmqMessagePublisher.cs:174` writes `HeaderNames.HANDLED_COUNT`; `RmqMessageCreator.cs:208-210`
  reads it back. RMQ does republish and does carry the count, so the Problem Statement's causal
  split is wrong. Confirmed.

Findings 4, 7, 8, 10, 11, 12 and 13 are internal-consistency findings; each was checked against the
current text of `requirements.md` rather than against a remediation log.

**Round 3's fourteen remediations were also spot-checked in `requirements.md` itself before this
review was launched — all fourteen are present in the document.** The round-3 process trap (logged
but not applied) did not recur.

# Remediation log — round 4

**Date**: 2026-09-22. **Applied to**: `requirements.md`. **Every edit verified by reading the file
back from disk and grepping for the applied text, and this log written from the document rather than
from any script's success output** — round 3's process rule.
**Outcome**: all 10 findings at or above threshold remediated, plus all 3 below. Document now holds
**28 `R-n`, 8 `NFR-n`, 41 `AC-n`** (was 27/8/40) and **C-1..C-12** (was C-1..C-11).
Integrity re-checked programmatically after the last edit: no numbering gap in `R-n` or `AC-n`, every
requirement and NFR has a map row (36 rows), no map row references an undefined AC, no `R-n` or
`AC-n` is mentioned anywhere without being defined, AC-30 and AC-31 remain the only deliberate
omissions from the map, and the constraint list reads C-1 through C-12 in order.

## The decision this round turned on — NFR-3 kept, and what that settles

Findings 1 and 9 were two faces of one gap, and probing it surfaced something neither the reviewer
nor the three prior rounds had named: **the document claimed not to choose a mechanism while NFR-3
had already excluded one.** Republish adds a net broker call per requeue on all three transports in
scope (SQS `ChangeMessageVisibility` → `SendMessage` + `DeleteMessage`; Pub/Sub `ModifyAckDeadline` →
`Publish` + `Ack`; RocketMQ, whose requeue issues no broker call at all today), which is exactly what
NFR-3 forbids — yet §Candidate mechanisms listed republish as a live candidate and even annotated its
own cost as "adds a broker round trip on requeue (NFR-3)".

**The user's decision: keep NFR-3.** Reasons, both recorded in the new C-12 — bypassing the broker's
own redelivery is counter-intuitive for a user who configured a visibility timeout or ack deadline,
and SQS FIFO would break silently, because content-based deduplication hashes the body while a
requeue changes only a header attribute, so a republished copy is discarded as a duplicate unless
Brighter mints a fresh `MessageDeduplicationId` per requeue, which it does not do today
(`SqsMessageSender.cs:100-102`). C-12 also records the **cost** of the decision, so it is not
rediscovered as a surprise: republish would have made the contract exact everywhere and dissolved
both of this spec's conditionals, so keeping NFR-3 keeps R-14's branch and A-2's risk live and
accepts that GCP FR-23 and RocketMQ FR-23 may both end at *bound but unimplemented*.

Consequences applied to the document: §Candidate mechanisms' intro no longer claims neutrality it
does not have ("does not choose between the mechanisms that remain open, but it does close one"), the
republish row is marked ⛔ excluded rather than left as a candidate to re-weigh, and R-28 becomes
load-bearing rather than defensive — with republish excluded, a count synthesised on read is the
likely mechanism, which is the ordinary case R-28 governs.

| # | Score | Remediation |
|---|---|---|
| 1 | 84 | **New R-28** at the end of Group A: a delivery count read from a rejection destination is the count stamped by whatever routed the message there, never that destination's own delivery count. It tabulates the three routes and their counts (`R` for a Brighter-managed send, `R - 1` for broker-routed rejection, unbounded for R-9's native case), states that R-5's `>= R - 1` bound is exactly the envelope of the first two, and records the evidence that made this necessary — all three in-scope providers read their rejection destination through the production consumer (`ChannelFactory` for AWS `:294` and GCP `:298`, a real `RocketMessageConsumer` for RocketMQ `:298`), so a mechanism that substituted the broker counter for the stamped header would report the destination's own count, `0` after R-2's normalisation, and falsify R-5, AC-4 and the FR-23 template's existing `HandledCount >= RequeueCount - 1` assertion on every cell this spec moves. The ADR MUST record per transport how its mechanism satisfies R-28 and name the discriminator where it uses one. **New AC-41** asserts it behaviourally and carries the ADR-table clause as a marked manual gate. R-5's rationale blockquote now points at R-28; the broker-counter row of §Candidate mechanisms carries the caveat; map rows added for R-28 → AC-41 and R-5 → AC-4, AC-41. |
| 2 | 82 | R-27(c)(1)'s key restated: **`x-original-message-id` when the originating message carries one, `Header.MessageId` otherwise** — the identity rule `ConformanceDeferredPump.AssertIsTheMessageSent` already applies via `Message.OriginalMessageIdHeaderName`. The reason is recorded in the obligation: a transport that requeues by republishing mints a fresh id per redelivery and records the first in that header (`RmqMessagePublisher.cs:131`, `:142`), so a count keyed on `MessageId` alone records `1` against three distinct keys and `count <= RequeueCount` passes vacuously — which is the state of the three RMQ configurations that R-27(c)(4) regenerates, so the fallback is load-bearing today rather than hypothetical. A republishing transport is still one message for counting. `command.Id` remains explicitly excluded. |
| 3 | 70 | AC-20's first branch no longer hard-asserts a status: it asserts the **`GetProjectAsync` step's tolerated outcome** — an `Unauthenticated`/`PermissionDenied` status *or* a client-construction failure, whichever the ambient credentials produce — and a blockquote records why the spec cannot fix which (`CreateProjectsClientAsync` `:173` builds `ProjectsClientBuilder { Credential = Credential }`, so with no ADC construction throws before any RPC, which is the ordinary state of the emulator-only machine R-21 makes the bar). NFR-5 now **requires the ADR to name the exception type** caught there, saying plainly that "the narrowest type that call site can throw" is not a specification. AC-21 gains a clause exercising the construction case directly, and its ADR-dependent half is listed among the clauses not writable until design. |
| 4 | 65 | AC-19 and AC-40 re-keyed onto one binary fact — whether the ADR records a GCP mechanism that satisfies R-1 to R-5 within NFR-1 to NFR-3, or records that none does. A new blockquote under AC-40 states that they partition every outcome and that **the selector is the ADR's conclusion, not AC-39's measurement**, closing both the overlap (a mechanism selected and then found wanting) and the gap (a good measurement whose mechanism still cannot reach R-2's exact `0`). R-13's first branch header, AC-30's conditional GCP row and AC-39's exit criteria were all restated in the same terms; AC-39 now has four exit criteria, with (c) requiring the ADR's conclusion to be recorded and (d) selecting the AC from it. |
| 5 | 62 | A-5 now reads "the twelve provider files", agreeing with R-27(a) and AC-36. |
| 6 | 62 | R-4's observation window redefined as closing **when the pump has been quit and awaited**, with the template's own `_channel.Enqueue(CreateQuitMessage(...)); await pumping;` cited and the reason stated — the pump is still dispatching between the poll's break and the quit, so closing at the break would blind the observation to the failure it exists to catch. The false identity claim is gone from R-27(c)(3), which now says the read point is strictly later than the poll's break and why that matters. AC-3's parenthetical rewritten to match. |
| 7 | 62 | AC-11 gains a second branch for the outcome R-25 permits — the ADR stating that no subscription shape trips R-11 — requiring the rule and R-26's channel-creation log to be built and exercised anyway, against a subscription answering the predicate negatively and one answering it positively, with the ADR's statement recorded as the evidence. It closes with why: R-11 is the anti-silence requirement, so "nothing trips it today" is not a licence to leave it unimplemented, because the predicate is what a future transport answers. |
| 8 | 62 | The tolerated-IAM Warning's content is now stated **once**, in R-20's obligation paragraph, as four elements — the helper abandoned, the RPC, the resource, the status code — with the note that the helper name is what makes AC-20's "exactly two Warnings, one per helper" checkable. R-20's `GetProjectAsync` bullet, NFR-5 and AC-20 now all refer to that list instead of restating it. |
| 9 | 62 | The Problem Statement's causal split corrected: **all nine** conforming configurations requeue by republishing and the header travels, the three RMQ rows included, which republish through `RmqMessagePublisher.RequeueMessageAsync` carrying `HANDLED_COUNT` (`:174`, read back at `RmqMessageCreator.cs:208-210`); they differ only in that a *rejected* message reaches its destination by the DLX rather than a Brighter-managed send, and the budget is as load-bearing there as anywhere — at `requeueCount: -1` an RMQ message is requeued for ever and nothing reaches the DLX. R-22's parenthetical corrected the same way. **R-5 was left untouched: its rationale was already correct, and it is what proves the Problem Statement wrong.** The Terms table was a third wrong site and is fixed: *Native dead-lettering* is now threshold-driven with **no Brighter call at all** (RabbitMQ DLX removed from its examples), and a new term **Broker-routed rejection** names the third case — Brighter calls `Reject`, the broker moves its own stored copy (`BasicRejectAsync(deliveryTag, requeue: false)`, `RmqMessageConsumer.cs:362`), the budget still has to run down to get there, and no metadata is stamped — noting that no transport in scope uses that route and R-22 protects the three that do. |
| 10 | 60 | NFR-4 restated as "at most once per subscription **per rule**, and never per message", with a paragraph explaining that R-25's three rules are independent and non-exclusive, so two validation findings plus R-26's one log line for a single subscription is correct behaviour rather than a breach. AC-29 rewritten to count only the **channel-creation** route — exactly one where R-11's condition holds, zero otherwise — to re-take the count after a further 100 messages, and to say explicitly that validation findings are counted by AC-7, AC-10 and AC-11 instead. |
| 11 | 55 | AC-3 gains "on each transport in scope and in both variants", matching AC-5 and AC-6; AC-35 changed from "on **a** transport" to "on **each** transport", so the two ACs under R-7 now state the same obligation. |
| 12 | 52 | A **sixth numbered obligation** added to R-27(c) for the requeue observation, specified to the same degree as the dispatch counter: a recording consumer the test composes *around* the real consumer, forwarding every call and counting `Requeue`/`RequeueAsync` by obligation 1's key, reset and read per obligations 2 and 3, so "never called" is `count == 0` — explicitly not a mock standing in for a transport (C-10), not a counter in production code, and not an inference from an absent redelivery. Obligation 5's half-sentence now defers to it. |
| 13 | 45 | R-25's trailing clause no longer claims that "state that none does" satisfies R-3's classification; it points at AC-11's second branch, which is what now defines "done" for that case. |

## Verification performed

- Each of the three edit batches asserted a unique anchor per edit and wrote nothing if any anchor
  failed to match exactly once. All three reported success, **and were then verified from disk**: 28
  applied strings each found exactly once, 8 superseded strings each found zero times.
- The integrity check above was run against the file as written, not against an in-memory copy.
- R-28, the AC-19/AC-40 partition note, NFR-4 and AC-39 were read back in full and reviewed as prose,
  which caught two defects the presence-greps could not: NFR-4's inserted paragraph had run into the
  original sentence and was restructured, and AC-39's exit criteria still said the *measurement*
  selected the branch, which was realigned to the ADR's conclusion.

## Carried forward, still needing the user's say-so

Unchanged from round 3: raising the SQS failed-DLQ-send question as its own issue; commenting on
#4341 / #4386 / #4353 / #4354; re-pointing the ledger cells. AC-30 and AC-31 still have no owning
requirement, by the deliberate decision recorded in round 1.

## Next step

Round 5. The seams worth probing, given what this round changed:

1. **R-28 against R-2, R-5, R-9, AC-4, AC-9 and AC-41** — R-28 is new and load-bearing, and it is the
   first requirement in this document to constrain *which read* a rule applies to. Does it hold
   against the FR-23 template's assertion as generated, and does AC-41's "not the value the
   destination's own counter would yield" survive a transport whose rejection destination has no
   counter at all (GCP without a `DeadLetterPolicy` on the DLQ subscription, A-1)?
2. **C-12 against NFR-3, §Candidate mechanisms and R-13/R-14** — the exclusion is now explicit, so
   check nothing elsewhere still reads as though republish were available, and that C-12's recorded
   cost matches what R-13 and R-14 actually promise on their pessimistic branches.
3. **R-27(c)'s six obligations against AC-3, AC-5, AC-6, AC-35, AC-36 and AC-41** — obligation 6 is
   new and obligation 1's key changed; the ACs were not all re-read against them.
4. **The AC-19/AC-40/AC-39 rewrite against AC-22 and AC-30** — AC-22's selector was found stale while
   writing this log ("whenever AC-19 is the branch AC-39 selected") and was **fixed in the same pass**:
   it now reads "whenever AC-19 is the branch that applies, which the ADR's recorded conclusion selects
   rather than AC-39's measurement". Round 5 should check for any remaining site that still treats the
   measurement as the selector.

---

# Review: requirements (round 5) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

## Findings

### 1. AC-27 still places all five compile-only samples in `Core.Tests`, contradicting R-24's own table and R-24's explicit prohibition — and is unsatisfiable as written (Score: 84)

Round 3's finding 1 (90) was that R-24 and AC-27 both named `tests/Paramore.Brighter.Core.Tests/`
for all six types, which cannot compile. Round 3's log row 1 records the fix as "R-24's five-row
table placing each sample in a project that already references the assembly it exercises".
**The R-24 half landed; the AC-27 half did not.** R-24 (`requirements.md:608-621`) now carries the
five-row table and closes with:

> `Paramore.Brighter.Core.Tests` references no messaging gateway and must not acquire one for this
> purpose. *(line 621)*

AC-27 (line 1325) still reads:

> **AC-27** (R-24) — **Given** the compile-only V10 compatibility sample R-24 requires, committed
> under `tests/Paramore.Brighter.Core.Tests/` and exercising `Subscription`, `SqsSubscription`,
> `GcpPubSubSubscription`, `RocketMqSubscription`, `MessageHeader` and `Message` …

AC-27 is R-24's **only** acceptance criterion, and it directly contradicts the requirement it tests:
singular "sample", one location, and three gateway types that `Core.Tests` cannot name.

**Evidence**: Verified in source. `tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj:7-13`
has seven `ProjectReference` entries — `BoxProvisioning`, `Extensions.DependencyInjection`,
`Mediator`, `Paramore.Brighter`, `Outbox.Hosting`, `ServiceActivator`, `Testing` — and **not one
messaging gateway**. By contrast `Paramore.Brighter.AWS.Tests.csproj:31` and
`Paramore.Brighter.Gcp.Tests.csproj:48` each reference exactly the gateway R-24's table assumes. A
file under `Core.Tests` naming `SqsSubscription` does not compile, so AC-27's "**Then** it compiles
with no new errors" is unsatisfiable, and the automated build gate R-24 exists to create does not
exist.

> **Main-agent note.** Independently re-verified before this file was written: AC-27's text at
> `:1325-1331`, R-24's table at `:608-621`, and the seven `ProjectReference` lines. This is a
> round-3 remediation that half-landed — the exact class of defect round 3's process rule exists to
> catch, surviving because round 4 checked *round 4's* thirteen rows and round 3's check was scoped
> to round 2's twelve. **The spot-check must cover the requirement's ACs, not just the requirement.**

**Recommendation**: Rewrite AC-27's Given to mirror R-24's table — *"Given the five compile-only V10
compatibility samples R-24 requires, each committed in the project R-24's table names (`Core.Tests`
for `Subscription`, `MessageHeader` and `Message`; `AWS.Tests`, `AWS.V4.Tests`, `Gcp.Tests` and
`RocketMQ.Tests` for their respective subscription types)"* — and keep the Then clause as it stands.

---

### 2. The delivery-count ACs are stated unconditionally across all four transports in scope, but R-13 and R-14 permit GCP and RocketMQ to finish at "bound but unimplemented" on R-1 to R-5 (Score: 72)

R-13 and R-14 each define a branch on which the transport is **bound by the contract but not
implemented here**, and on that branch R-1 to R-5 are *not* satisfied for that transport:

> "GCP is *bound but unimplemented* on R-1 to R-5, and 'done' = (a) R-11's Warning fires … (b) the
> four `GCP / *` FR-23 cells stay `Deferred` …" *(R-13, lines 362-372)*
> "**If the condition does not hold** — RocketMQ is *bound but unimplemented*." *(R-14)*

But the ACs that carry R-1 to R-5 are written with no branch guard at all:

- **AC-1** (R-1): "Given a subscription on **any transport in scope**…" (line 1037)
- **AC-2** (R-2): "…to **any transport in scope**…"
- **AC-3** (R-4): "Given, **on each transport in scope** and in both variants…" (line 1047)
- **AC-4** (R-5): "Given the run in AC-3…"
- **AC-41** (R-28, R-5): "Given, **on each transport in scope** and in both variants, a run in which
  **the budget is exhausted** and Brighter routes the message…" (line 1099)
- **AC-34**'s second clause: "each transport the table classifies **exact** … three deliveries
  present counts of exactly `0`, `1` and `2`"

On R-13's or R-14's pessimistic branch the budget is never exhausted for that transport, so AC-3,
AC-4 and AC-41 cannot pass and AC-1 cannot pass — yet nothing marks them conditional, and neither
the §manual-gates list nor the "not writable until the ADR exists" list mentions them. Round 3's
finding 8 fixed exactly this disease for **NFR-7**, which now says evidence "is unconditional for
the eight AWS cells … and conditional for the rest"; the same treatment was never applied to the
Group A ACs, which is where the contract's own acceptance lives.

AC-5, AC-6 and AC-35 are genuinely unaffected — a budget of `-1`, `1`, `0` or `-3` needs no count to
advance, because `HandledCountReached` is reached on the first deferral (verified at
`Reactor.cs:494-508`, `Message.cs:161-164`) — so the gap is specific and fixable rather than
pervasive.

Two implementers will disagree at sign-off about whether a red AC-3 on RocketMQ blocks this spec or
is the defined outcome of R-14's second branch. That is the definition of a High.

**Evidence**: `requirements.md:1037, 1047, 1099` (unconditional "each/any transport in scope")
against R-13's and R-14's branch definitions and against NFR-7's corrected wording at lines 869-874.

**Recommendation**: Add one sentence at the head of §Delivery-count contract, parallel to NFR-7's:
*"AC-1 to AC-4, AC-34's second clause and AC-41 are unconditional for `AWSSQS` and `AWSSQS.V4`
(R-12). For `GcpPubSub` and `RocketMQ` they apply only on the branch R-13/R-14 selects as
implemented; on the 'bound but unimplemented' branch, R-13(a)-(c) and R-14(a)-(d) define 'done'
instead, and AC-40 / AC-25 are the criteria that apply."*

---

### 3. AC-3 does not say whether a GCP subscription under test carries a native `DeadLetterPolicy` — the one axis A-1 makes decisive (Score: 68)

AC-3 is the acceptance criterion for R-4, this spec's central requirement. Its Given fixes
everything except the one GCP-specific fact that determines whether the delivery counter exists at
all:

> "**Given**, on each transport in scope and in both variants, `requeueCount: 3`, a
> `deadLetterRoutingKey`, a handler that always defers, and **no native redrive limit at or below
> 3**…" *(lines 1047-1048)*

"No native redrive limit at or below 3" is satisfied on GCP **both** by a subscription with
`MaxDeliveryAttempts: 5` **and** by a subscription with no `DeadLetterPolicy` at all. A-1 is
*verified* that the second shape yields `delivery_attempt == 0`/`null`:

> "**A-1.** Pub/Sub populates `delivery_attempt` only when the subscription carries a
> `DeadLetterPolicy`… **Consequence**: if the ADR chooses the broker-counter mechanism for GCP,
> R-11's Warning is load-bearing for subscriptions without a `DeadLetterPolicy`."

And AC-11 names that same shape as the canonical **R-11-tripping** subscription — the one whose
budget *cannot* run down. So under the broker-counter mechanism, which C-12 makes the likely one,
one developer's AC-3 GCP instantiation passes and the other's is the very configuration AC-11
expects to warn is unenforceable. AC-19 does pin it (`requeueCount: 3` and a `DeadLetterPolicy` with
`MaxDeliveryAttempts: 5`), but AC-19 is R-13's branch-selected AC, not R-4's, and nothing says
AC-19 *is* AC-3's GCP instantiation.

**Evidence**: `requirements.md:1047-1052` (AC-3) against A-1 and AC-11's Given. Verified in the
harness that today's GCP DLQ read-back subscription itself carries no `DeadLetterPolicy`
(`tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpPullMessageGatewayProvider.cs:292-298`), so
the "no policy" shape is not hypothetical in this repo.

**Recommendation**: Add to AC-3: *"…and, on GCP, a `DeadLetterPolicy` with `MaxDeliveryAttempts: 5`
(C-6's floor; a GCP subscription with no `DeadLetterPolicy` is AC-11's R-11 case, not AC-3's)."*

---

### 4. Two sites still name AC-39's measurement, not the ADR's conclusion, as the selector of the GCP branch (Score: 66)

Round 4's finding 4 re-keyed AC-19/AC-40 onto the ADR's recorded conclusion, and AC-39 now says so
in terms:

> "**AC-39 is a measurement, not a pass/fail gate** … It does **not** by itself select between AC-19
> and AC-40; the ADR's recorded conclusion does."

Two summary sentences elsewhere were not brought into line and still read the old way:

1. **NFR-7**, line 870: *"two of the three ACs are branch-guarded: AC-19 (the four GCP cells)
   applies only on **the branch AC-39 selects**, and AC-24 (RocketMQ) only when R-14's condition
   holds."* — AC-39 selects no branch. The RocketMQ half is correct (AC-23's measurement genuinely
   *is* R-14's selector), which makes the parallel construction actively misleading rather than
   merely stale.
2. **AC-30's summary**, lines 1389-1391: *"**5 conditional**: the 4 GCP FR-23 cells **on assumption
   A-2** (R-13), and the 1 RocketMQ FR-23 cell on R-14's condition."* — the table row two lines
   above states the correct condition ("**if** the ADR records a GCP mechanism that satisfies R-1 to
   R-5 within NFR-1 to NFR-3"), so the same table contradicts itself.

This is the precise failure mode AC-40's blockquote exists to prevent: someone moving the four GCP
FR-23 cells to `Fixed` on a good A-2 measurement alone, before the ADR has concluded that a
mechanism reaches R-2's exact `0`.

> **Main-agent addendum — a third, weaker site.** `requirements.md:357` heads R-13's branch
> discussion with "**R-13 is conditional in the same shape R-14 is, on assumption A-2**". Its own
> first bullet corrects this in place ("A-2 holding is what makes the counter *available*; it is the
> ADR's recorded conclusion, not the measurement alone, that selects this branch"), so it misleads
> far less than the two sites above and is **not** separately scored. Fix it in the same pass for
> consistency: the conditionality is on the ADR's conclusion, which A-2 supplies one route to.

**Evidence**: `requirements.md:870`, `requirements.md:1389-1391` (both re-verified by the main agent
against the file on disk), against AC-39's own text and AC-40's blockquote.

**Recommendation**: NFR-7 → "AC-19 (the four GCP cells) applies only on the branch the ADR's
recorded conclusion selects (see the note under AC-40)". AC-30's summary → "the 4 GCP FR-23 cells on
the ADR's recorded conclusion about a satisfying GCP mechanism (R-13; A-2's measurement chooses
which mechanisms it can reach for)". R-13's header sentence at :357 → same substitution.

---

### 5. The Terms table defines "delivery count" only for the pump hand-off, but R-5, R-28, AC-4 and AC-41 use the term for a message read from a rejection destination (Score: 55)

> | **Delivery count** | The value of `MessageHeader.HandledCount` (`MessageHeader.cs:226`) on a
> message as it is handed to the pump by the consumer, **before** the pump calls
> `UpdateHandledCount()`. |

Round 4 edited two rows of this table (*Native dead-lettering*, and the new *Broker-routed
rejection*) but not this one. R-5 ("The message arriving there carries … a delivery count of at
least `R - 1`"), R-28 ("A delivery count read from a rejection destination…"), AC-4 and AC-41 all
apply the term to a message a **test or DLQ consumer** reads, never one handed to a pump. R-28
resolves the substance ("R-2's normalisation is a property of the delivery a **pump** consumes"), so
nothing would be built wrong — but the defined term does not cover its two most load-bearing uses,
and the reconciliation sits three hundred lines from the definition.

**Evidence**: Terms row at `requirements.md:101`; R-5 at `:194`; R-28 at `:237-242`; AC-41 at
`:1099-1106`.

**Recommendation**: Extend the row: *"…before the pump calls `UpdateHandledCount()`. Where a rule
reads the count on a message taken from a rejection destination rather than a source channel — R-5,
R-28, AC-4, AC-41 — it means the `HandledCount` that message presents on that read; R-28 governs
what that value must be."*

---

### 6. The spec README's status block is stale by a full round (Score: 35)

`README.md:115-118` still reads "Now **27 `R-n`, 8 `NFR-n`, 40 `AC-n`**" and its checklist ends at
round 3 with "**Re-run `/spec:review requirements` (round 4) before approving.**" The document holds
28/8/41 and round 4 is complete with its log in this file.

**Evidence**: `README.md:115`, `:118`; counted programmatically in `requirements.md` — 28 `R-n`,
8 `NFR-n`, 41 `AC-n`.

**Recommendation**: Update the counts and add the round-4 row. Round 5's row follows when this round
is remediated.

---

## Round-4 remediation spot-check

Each of round 4's thirteen remediations grepped against `requirements.md` as it stands on disk.
**All thirteen are PRESENT.** The round-3 process rule held.

| # | Score | Applied text checked | Result |
|---|---|---|---|
| 1 | 84 | `R-28. A delivery count read from a rejection destination` | PRESENT, line 237. Also `**AC-41** (R-28, R-5)` line 1099; map rows `\| R-28 \| AC-41 \|` line 1451 and `\| R-5 \| AC-4, AC-41 \|` line 1428; R-5's blockquote pointing at R-28 line 204; §Candidate mechanisms broker-counter caveat line ~901 |
| 2 | 82 | `x-original-message-id` when it carries one | PRESENT, line 764 (R-27(c)(1)), with the `RmqMessagePublisher.cs:131, :142` rationale |
| 3 | 70 | `GetProjectAsync` step's tolerated outcome | PRESENT, line 1249 (AC-20); NFR-5's "ADR MUST name the exception type" PRESENT line 854; AC-21's construction clause PRESENT line ~1275 |
| 4 | 65 | `the selector is the ADR's conclusion rather than` | PRESENT, line 1238 (AC-40 blockquote); AC-39's four exit criteria PRESENT lines ~1216-1222 |
| 5 | 62 | `names the twelve provider files` | PRESENT, line 936 (A-5) |
| 6 | 62 | `That window closes when the pump has been quit and awaited` | PRESENT, line 175 (R-4); R-27(c)(3)'s "strictly later than the dead-letter poll's break" PRESENT line ~782; AC-3 parenthetical PRESENT line ~1052 |
| 7 | 62 | `no** subscription shape trips R-11` | PRESENT, line 1144 (AC-11's second branch) |
| 8 | 62 | `names four things: the helper it abandoned, the RPC, the resource, and the` | PRESENT, line 495 (R-20 obligation paragraph; wraps to "status code" on 496) |
| 9 | 62 | `All nine** that work requeue by **republishing` | PRESENT, line 28; `Broker-routed rejection` term PRESENT line 107; R-22's corrected parenthetical PRESENT line 574 |
| 10 | 60 | `per subscription per rule` | PRESENT, line 834 (NFR-4); AC-29's channel-creation-only count PRESENT lines 1339-1345 |
| 11 | 55 | `**AC-3** (R-4) — **Given**, on each transport in scope and in both variants` | PRESENT, line 1047; `**AC-35** (R-7) — **Given**, on each transport in scope` PRESENT line 1091 |
| 12 | 52 | `A requeue observation, specified to the same degree` | PRESENT, line 788 (R-27(c) obligation 6) |
| 13 | 45 | `its second branch defines what "done" means for R-11` | PRESENT, line 702 (R-25); the wrong "R-3 classification" cross-reference is gone |

No round-4 remediation is recorded-but-absent. Round-3 remediations were also sampled and are
present, with the **one exception** that is finding 1 above: round 3 row 1's R-24 table landed but
its AC-27 counterpart did not.

> ⚠️ **Process lesson for round 6's spot-check.** Round 4 verified round 4's own thirteen rows;
> round 3 verified round 2's twelve. Nobody re-verified round 3's fourteen *against their ACs*, and
> that is exactly where finding 1 survived four rounds. **When a remediation edits a requirement,
> the spot-check must also read that requirement's ACs** — a half-applied fix leaves the document
> internally contradictory, which is worse than leaving it wholly unfixed.

## Integrity checks

All mechanical checks **pass**:

- **Every `R-n` and `NFR-n` has a map row** — 36 rows for 28 `R-n` + 8 `NFR-n`; none missing.
- **No map row references an undefined `AC-n`** — all map ACs resolve to definitions.
- **Nothing referenced without being defined** — every `R-n` (1-28), `NFR-n` (1-8), `AC-n` (1-41),
  `C-n` (1-12) and `A-n` (1-5) mentioned anywhere in the document is defined.
- **No numbering gaps or duplicates** — `R-1..R-28` (28, each defined once), `NFR-1..NFR-8` (8),
  `AC-1..AC-41` (41), `C-1..C-12` **in document order**, `A-1..A-5`. R-28 sits after R-7 in Group A
  and several ACs are defined out of numeric order; both are deliberate placements, not gaps.
- **AC-30 and AC-31 are the only omissions from the map** — confirmed programmatically, and the
  document says so explicitly.

## Source-citation verification

A broad sample was verified, plus every citation load-bearing to a finding. **All checked citations
are accurate**, including: `Reactor.cs:494/:498/:416`, `Proactor.cs:500/:504/:483`,
`MessageHeader.cs:226`, `Subscription.cs:109/:203`, `Message.cs:161-164`, `MessagePump.cs:171`,
`DefaultMessageAssertion.cs.liquid:59`, `ConformanceDeferredPump.cs.liquid:128/:152/:112-115` and
its `AssertIsTheMessageSent` / `OriginalMessageIdHeaderName` route,
`RmqMessagePublisher.cs:131/:142/:174`, `RmqMessageCreator.cs:208-210`, `RmqMessageConsumer.cs:362`,
`SqsMessageConsumer.cs:193/:307-316/:384/:496/:541`, V4 `:276/:377`, `SqsMessageSender.cs:100-102/:130`,
`SqsMessageCreator.cs:323`, `SqsInlineMessageCreator.cs:350`,
`SqsStandardMessageGatewayProvider.cs:104/:108/:294`, `GcpPullMessageGatewayProvider.cs:151/:155/:298`,
`RocketMqMessageGatewayProvider.cs:298`,
`GcpPubSubMessageGateway.cs:220-236/:235/:251/:477/:482-491/:506-511/:527/:534-543`,
`GcpMessagingGatewayConnection.cs:173`, `GcpPullMessageConsumer.cs:276/:306/:335/:369`,
`GcpPubSubStreamMessageConsumer.cs:84/:217`, `RocketMessageConsumer.cs:179/:422/:433`,
`SqsAttributes.cs:110`, `DeadLetterPolicy.cs:27/:47`, `GcpPubSubSubscription.cs:102`,
`PipelineBuilder.cs:280/:323`, `BrighterPipelineValidationExtensions.cs:78-84`.
`grep -rn "ApproximateReceiveCount" src/` returns nothing, as NFR-1 states.

Two seams round 4 flagged were probed and found **correct** — recorded so round 6 does not re-probe
them:

- **R-27(b)'s `??=`-on-empty-string argument is right.** `publishMember ??= …`
  (`GcpPubSubMessageGateway.cs:491`) does not fire for `""`, so `""` trips the `IsNullOrEmpty` guard
  *and* derives nothing — exactly as the document says.
- **R-27(c)(1)'s fallback key is right and necessary.** The FR-23 template's quit/await really is
  two statements after the poll's break (`…too_many_times….cs.liquid:104-105`), before the
  assertions; and the "republish mints a fresh id" claim holds for RMQ (`RmqMessagePublisher.cs:223`,
  guarded so the *first* id sticks) and for Postgres/MsSql (`PostgresMessageConsumer.cs:338-340`,
  `MsSqlMessageConsumer.cs:278-280`), while Kafka and Redis resend the same `Message` object and
  keep `MessageId` stable — so obligation 1's key is correct for all nine non-regression
  configurations, not only the three RMQ rows it names.

Ledger claims were checked against
`specs/0036-universal-transport-conformance-tests/conformance-status.md`'s Conformance Matrix: 24
configurations, 9 FR-23 `Pass`/`Fixed`, 15 failing, 13 in scope; R-23's eleven `Pass` columns for
`AWS / SqsStandard`; AC-38's nine `Fixed` RocketMQ cells; and AC-31's "could not move" list is
**complete** — every currently-`Deferred` cell outside AC-30 appears in it.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 1 |

**Total findings**: 6
**Findings at or above threshold (60)**: 4

## Main-agent validation of this round

- Summary table counted against the findings list: 84, 72, 68, 66 (≥60) and 55, 35 (<60) — 0/2/3/1,
  total 6, four at or above threshold. Table, verdict line and finding list agree.
- Findings 1 and 4 were **independently re-verified against the file and the source tree** before
  this file was written, because finding 1 alleges a half-landed remediation (the round-3 trap) and
  finding 4 alleges stale cross-references. Both confirmed: AC-27 `:1325-1331` versus R-24's table
  `:608-621`; `Core.Tests.csproj:7-13` carries no gateway reference; NFR-7 `:870` and AC-30's
  summary `:1389-1391` read as quoted.
- The main agent's own grep for the finding-4 pattern surfaced a **third** site at `:357` that the
  sub-agent did not name. It is materially weaker — R-13's own bullet corrects it in place — so it
  was folded into finding 4 as an addendum rather than scored separately, to keep the count honest.
- No finding re-opens a settled decision (one spec; #4354 folded in; RocketMQ conditional;
  `ValidatePipelines`; NFR-3 kept and republish excluded; AC-30/AC-31 unmapped by choice).

---

# Remediation log — round 5

**Date**: 2026-09-23. **Applied to**: `requirements.md` and `README.md`. Each edit was applied
separately (no batch script) and checked by reading the file back and grepping for the applied text.
This log was written from the document, never from a tool's success output (round 3's rule).
**Outcome**: all 4 findings at or above threshold remediated, plus both below. Counts unchanged at
**28 `R-n`, 8 `NFR-n`, 41 `AC-n`**, C-1..C-12. Integrity re-checked programmatically after the last
edit: no numbering gap, no `R-n`/`NFR-n`/`AC-n` mentioned without being defined.

| # | Score | Remediation | Verified at |
|---|---|---|---|
| 1 | 84 | AC-27's Given now reads "the **five** compile-only V10 compatibility samples R-24 requires, each committed in the project R-24's table names" and lists all five projects against their types, adding "no test project having gained a reference" to match R-24's prohibition. Then clause unchanged. | `requirements.md:1335` |
| 2 | 72 | New paragraph "**Which transports these ACs bind.**" at the head of §Delivery-count contract. It says AC-1 to AC-4, AC-34's second clause and AC-41 are unconditional for `AWSSQS`/`AWSSQS.V4` and branch-guarded for GCP/RocketMQ, with R-13(a)-(c)/R-14(a)-(d) and AC-40/AC-25 applying on the *bound but unimplemented* branch. AC-5, AC-6 and AC-35 are stated as unguarded, with the reason. The labels (a)-(c) and (a)-(d) were checked against R-13 and R-14 as written. | `requirements.md:1037` |
| 3 | 68 | AC-3's Given now pins the GCP shape: "on GCP, a `DeadLetterPolicy` with `MaxDeliveryAttempts: 5` (C-6's floor; a GCP subscription with no `DeadLetterPolicy` is AC-11's R-11 case, not AC-3's)". | `requirements.md:1057` |
| 4 | 66 | All three sites re-keyed from AC-39/A-2 onto the ADR's recorded conclusion: NFR-7 ("the branch the ADR's recorded conclusion selects … AC-39 is a measurement, not a selector"), AC-30's summary ("on the ADR's recorded conclusion about a satisfying GCP mechanism (R-13; A-2's measurement chooses which mechanisms it can reach for)"), and R-13's header ("on the ADR's recorded conclusion — to which assumption A-2 supplies one route"). A grep for `branch AC-39 selects` and `on assumption A-2 (R-13)` now returns 0 matches. | `:871`, `:1404`, `:357` |
| 5 | 55 | Terms row *Delivery count* extended: a read from a rejection destination (R-5, R-28, AC-4, AC-41) means the `HandledCount` that message presents on that read, with R-28 governing its value. | `requirements.md:101` |
| 6 | 35 | `README.md` counts corrected to 28/8/41; round-4 and round-5 checklist rows added; the "re-run" pointer now names round 6. | `README.md:115, :119-120` |

**For round 6's spot-check**, following round 5's process lesson: finding 1's fix is an AC and
finding 2's is a guard over ACs. Read R-24 alongside AC-27, and R-13/R-14 alongside the new guard
paragraph, not only the edited lines.

---

# Review: requirements (round 6) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

## Findings

### 1. R-13's second branch is still keyed on A-2, so the requirement defines no "done" for a good measurement with no satisfying mechanism — and the new guard's "R-13(a)-(c)" lives only under that A-2-keyed bullet (Score: 74)

Round 4's finding 4 named this gap ("There is no 'bound but unimplemented' route out of a good
measurement"). The round-4 fix re-keyed only R-13's **first** bullet onto the ADR's conclusion; round
5 re-keyed only its header at :357. The **second** bullet still opens on the measurement:

> "- **If A-2 is refuted** — R-13's obligation is unchanged … If no such mechanism satisfies R-1 to
> R-5 within NFR-1 to NFR-3, GCP is *bound but unimplemented* … and 'done' = (a) … (b) … (c) …"
> (:367-373)

So the case "A-2 holds, but the ADR records that no mechanism satisfies R-1 to R-5" (e.g. a counter
that advances but where R-2's exact `0` cannot be reached) falls between the bullets: the first needs
a satisfying mechanism, the second needs A-2 refuted. AC-40 covers the case in terms ("a satisfying
mechanism can also fail to exist after a *good* measurement", :1239-1241), so the AC defines an
outcome its requirement does not. Round 5's new guard (:1039-1040) sends that branch to
"R-13(a)-(c)", which are defined only inside the A-2-refuted bullet. A-2's own text (:918-921, "If
it is refuted, R-13's second branch applies") repeats the A-2-keyed framing.

This is the half-landed-remediation pattern again: header and one bullet re-keyed, not the bullet
that carries the fallback's definition.

**Evidence**: requirements.md:362-373, :1037-1040, :1236-1254; this file's round 4 finding 4 and log row 4.

**Recommendation**: Restate the second bullet as "**If the ADR records that no GCP mechanism
satisfies R-1 to R-5 within NFR-1 to NFR-3** (whether A-2 was refuted, or held but no mechanism
reaches R-2/R-28) — GCP is bound but unimplemented, and 'done' = (a)-(c)". Keep "if A-2 is refuted,
the ADR must select a `delivery_attempt`-independent mechanism" as guidance on the route. Align A-2's
"If it is refuted" sentence.

---

### 2. AC-41's "specifically `3`", and R-5's and R-28's "the count reads `R`", are false for approximate counters — R-3's own example yields `4` (Score: 72)

For a delivery presenting count `c`, the pump sets the header to `c + 1` and rejects when
`c + 1 >= R` (Reactor.cs:494-498, Message.cs:161-164). The final header equals `R` only when the
rejecting delivery presented exactly `R - 1`. An approximate counter may jump; R-3 promises only "at
least `n - 1`". With R-3's own approximate example `0`, `1`, `3` (:167) at `requeueCount: 3`, the
third delivery presents `3`, the pump writes `4`, `4 >= 3` rejects, and Brighter sends
`HandledCount == 4`.

The document says `R`, or exactly `3`, in four places: R-5's blockquote (:202-203), R-28's first
bullet (:244), R-28's example on `AWS / SqsStandard` — approximate by A-4 — "presents
`HandledCount == 3`" (:272-273), and AC-41 "specifically `3` for `requeueCount: 3` on the
Brighter-managed route" (:1113). AC-41 applies to each transport in scope, including SQS. A correct
R-3-compliant implementation can fail AC-41's equality. R-5's `>= R - 1` bound is unaffected.

**Evidence**: :167, :202-203, :244, :272-273, :1109-1115; Reactor.cs:494-498; Message.cs:163; A-4 :927-929.

**Recommendation**: R-5 and R-28: "at least `R` (exactly `R` where the counter is exact)". AC-41:
restrict "specifically `3`" to transports the ADR's R-3 table classifies exact (as AC-34's second
clause does), or use `>= 3`. Fix R-28's SQS example the same way.

---

### 3. AC-5 does not exclude a native redrive policy, and in this repo's harness the native redrive target *is* the Brighter DLQ (Score: 64)

AC-5: "**Given**, on each transport in scope and in both variants, `requeueCount: -1` and a handler
that always defers, **When** the pump runs for 60 s, **Then** … reading the dead-letter destination
returns `MessageType.MT_NONE`" (:1070-1072). It says nothing about a native policy. With one, SQS
redrives on its `maxReceiveCount`th receive, and Pub/Sub dead-letters after `MaxDeliveryAttempts`
(≥ 5, C-6); GCP requeue is `ModifyAckDeadline(…, 0)`, so five deliveries fit in 60 s. The harness
points the native policy at the destination the test reads (`SqsStandardMessageGatewayProvider.cs:95-108`,
`RedrivePolicy(deadLetterChannelName, 3)`; `GcpPullMessageGatewayProvider.cs:151-156`,
`new DeadLetterPolicy(deadLetterRoutingKey, …)`). A developer copying the harness shape, or AC-3's
newly pinned GCP shape, sees AC-5 fail; one without a policy sees it pass. R-6's "requeued
indefinitely" (:211) is likewise true only with no native limit (under R-8 the effective limit at
`R = -1` is `M`). R-27(c)(5) says AC-5 uses bespoke subscriptions (:784-786) but not their shape.

**Evidence**: :208-212, :1070-1072, :1055-1058; the two harness providers above.

**Recommendation**: Add to AC-5's Given "and no native redrive policy (SQS: no `RedrivePolicy`; GCP:
no `DeadLetterPolicy`)". Qualify R-6's "indefinitely" with "absent a native redrive limit (R-8)".

---

### 4. AC-1 keeps both ambiguities AC-3 and AC-34 were fixed for — the GCP `DeadLetterPolicy` axis, and a budget that can end the run before the third delivery (Score: 64)

AC-1: "**Given** a subscription on any transport in scope with `requeueCount: 3` and a handler that
defers on every delivery, **When** the message is delivered three times, **Then** … strictly
greater…" (:1045-1048).

(a) GCP's `DeadLetterPolicy` is not pinned. Round 5's finding 3 pinned it for AC-3 only. Under the
broker-counter mechanism a no-policy GCP subscription presents `0, 0, 0` (A-1, :908-913) and AC-1
fails; with `MaxDeliveryAttempts: 5` it passes. Same disease one AC over, and AC-1 is R-1's only AC.

(b) The "When" may never occur. At `requeueCount: 3` on an approximate counter, R-4's own example
ends the run at delivery 2 ("For an approximate counter that jumped `0`, `2`, the rejection occurs on
delivery 2", :188-189). "Delivered three times" then never happens on SQS, and the test either passes
vacuously or times out. AC-34 avoids this with `requeueCount: 4` (:1098).

**Evidence**: :1045-1048, :1055-1058, :188-189, :908-913, :1098.

**Recommendation**: Give AC-1 `requeueCount: -1` (or at least `4`) and a native-policy-free
SQS/RocketMQ subscription; on GCP add AC-3's pin, with `MaxDeliveryAttempts` above the number of
deliveries observed.

---

### 5. AC-3's new parenthetical states unconditionally what AC-11 states only for a counter-based mechanism (Score: 56)

AC-3 now says "a GCP subscription with no `DeadLetterPolicy` is AC-11's R-11 case, not AC-3's"
(:1057-1058). AC-11 scopes that shape as "at minimum, **if the ADR selects a counter-based GCP
mechanism**" (:1146-1147), and A-1's consequence is likewise conditional (:912-913). Under a
`delivery_attempt`-independent mechanism, which R-13 allows, a no-policy GCP subscription does not
trip R-11. It is then bound by R-4 on "every channel" (R-1, :128), but neither AC-3 nor AC-11 tests
it.

**Recommendation**: "(where the ADR's GCP mechanism needs the policy, a subscription without one is
AC-11's R-11 case)" — or add a no-policy GCP instantiation of AC-3 for when it does not.

---

### 6. AC-27 does not check two of R-24's own prohibitions (Score: 48)

R-24 forbids `#pragma warning disable` in the samples (:605-606) and forbids creating a new project
(:609). AC-27 checks neither. A sample wrapped in `#pragma warning disable CS0618` would satisfy
AC-27 while hiding exactly the obsoletion R-24 exists to surface. The table mirror itself is now
correct: all five projects exist and reference the stated assemblies.

**Recommendation**: Add to AC-27's Given "no sample containing `#pragma warning disable`, and no new project".

---

### 7. The guard's stated reason for leaving AC-5 unguarded is wrong for `-1` (Score: 42)

"a budget of `-1`, `0`, `1` or below `-1` needs no count to advance, because `HandledCountReached`
is reached on the first deferral" (:1041-1043). At `-1`, `DiscardRequeuedMessagesEnabled()` is false
(MessagePump.cs:171-174), so `HandledCountReached` is never evaluated (Reactor.cs:496-498). The
conclusion is right; the reason is wrong for one value.

**Recommendation**: "…because at `-1` the budget is never consulted, and at `0`, `1` or below `-1`
`HandledCountReached` is true on the first deferral."

---

### 8. The guard drops AC-2 on the *bound but unimplemented* branch, although AC-2 also carries R-23 (Score: 38)

AC-2 maps to R-2 and R-23 (:1050, map :1460). The guard lifts AC-1 to AC-4 for GCP and RocketMQ on
the unimplemented branch (:1037-1041). But AC-38 says that branch "still touches
`RocketMessageConsumer`'s receive path if the ADR normalises counts there" (:1385-1387) — exactly
where AC-2's first-delivery `0` guards against regression. AC-38 partly covers RocketMQ through FR-16
and FR-22; GCP has no such cover.

**Recommendation**: Exclude AC-2 from the guard — a first delivery presenting `0` is true today and
must stay true on both branches — or say why it is not needed.

---

## Round-5 remediation spot-check

| # | Score | Applied text checked | Result |
|---|---|---|---|
| 1 | 84 | AC-27 "the five compile-only V10 compatibility samples R-24 requires, each committed in the project R-24's table names" | PRESENT :1335-1343; read against R-24 :601-621, row for row; csproj references verified. Residual: finding 6. |
| 2 | 72 | "**Which transports these ACs bind.**" | PRESENT :1037-1043. Labels match R-13 :370-373 / R-14 :396-402. Read with R-13/R-14: findings 1, 7, 8. |
| 3 | 68 | AC-3 "on GCP, a `DeadLetterPolicy` with `MaxDeliveryAttempts: 5`" | PRESENT :1057-1058. Consistent with AC-19, C-6, R-27(a), R-10/AC-10. Residual: findings 4, 5. |
| 4 | 66 | NFR-7 / AC-30 summary / R-13 header re-keyed onto the ADR's recorded conclusion | PRESENT :870-871, :1403-1405, :357-358; old wording returns 0 matches. R-13's second bullet still A-2-keyed: finding 1. |
| 5 | 55 | Terms "Where a rule reads the count on a message taken from a rejection destination" | PRESENT :101. Residual: finding 2. |
| 6 | 35 | README counts 28/8/41, round-4/5 rows, "round 6" pointer | PRESENT README.md:115, :119-120. |

## Integrity checks

- `R-1..R-28` (28), `NFR-1..NFR-8` (8), `C-1..C-12` (12), `A-1..A-5` (5), `AC-1..AC-41` (41), each
  defined once, no gaps. (A naive grep double-counts AC-19, AC-30 and AC-33 from bold references at
  line start, :1475/:1500/:1502 — not second definitions.)
- Every `R-n`/`NFR-n`/`AC-n`/`C-n`/`A-n` referenced is defined. Map: 36 rows, every cited AC
  defined; only AC-30 and AC-31 unmapped, by settled decision.
- Citations re-verified: Reactor.cs:494-498, Proactor.cs:500-504, Message.cs:161-164,
  MessagePump.cs:171-174, SqsStandardMessageGatewayProvider.cs:104,108,
  GcpPullMessageGatewayProvider.cs:151,155.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 3 |

**Total findings**: 8
**Findings at or above threshold (60)**: 4

## Main-agent validation of this round

- Summary counted against the list: 74, 72, 64, 64 (≥60); 56, 48, 42, 38 (<60) — 0/2/3/3, total 8,
  four at or above threshold. Table, verdict and list agree.
- Findings 1 and 2 re-verified against the file and source before writing: R-13's second bullet at
  :367 still reads "If A-2 is refuted"; R-5 :202-203, R-28 :244 and :272-273, and AC-41 :1113 read as
  quoted; `HandledCountReached` is `>=` (Message.cs:163) after `UpdateHandledCount()`
  (Reactor.cs:494); A-4 (:927-929) classifies SQS and Pub/Sub as approximate.
- No finding re-opens a settled decision.

---

# Remediation log — round 6

**Date**: 2026-09-23. **Applied to**: `requirements.md` and `README.md`. Every edit verified by
grepping the file on disk for its applied text after the last write; this log written from that
read-back. **Outcome**: all 4 findings at or above threshold remediated, plus all 4 below. Counts
unchanged at **28 `R-n`, 8 `NFR-n`, 41 `AC-n`**, C-1..C-12; integrity re-checked programmatically —
no gaps, no undefined references.

| # | Score | Remediation | Verified at |
|---|---|---|---|
| 1 | 74 | R-13's second bullet re-keyed: "**If the ADR records that no GCP mechanism satisfies R-1 to R-5 within NFR-1 to NFR-3** — whether because A-2 was refuted …, or because A-2 held but no mechanism reaches R-2's exact `0` or R-28's rule". The route guidance moved to a new paragraph, "**A-2 shapes the route, not the branch.**" A-2's "If it is refuted" sentence rewritten to match, adding that A-2 holding does not by itself select the first branch. Remaining "refuted" mentions (AC-19 :1236, AC-40's note :1259) were read and are consistent. | `:370`, `:378`, `:926` |
| 2 | 72 | R-5's blockquote: "reads at least `R` — exactly `R` where the counter is exact, more where an approximate counter jumped past `R - 1`". R-28's first bullet: "at least `R` and exactly `R` where the counter is exact". R-28's SQS example: "`HandledCount >= 3` — `3` unless the approximate `ApproximateReceiveCount` jumped (A-4)". AC-41: "on the Brighter-managed route `>= 3` … specifically `3` on a transport the ADR's R-3 table classifies **exact** (AC-34)". | `:203`, `:245`, `:275`, `:1131` |
| 3 | 64 | AC-5's Given now requires no native redrive policy (SQS: no `RedrivePolicy`; GCP: no `DeadLetterPolicy`; RocketMQ: no broker-side max-retry dead-lettering within the run). R-6's "requeued indefinitely" qualified with "absent a native redrive limit (R-8)". | `:1087`, `:212` |
| 4 | 64 | AC-1 moved to `requeueCount: -1`, with the reason stated (an approximate counter may end a `requeueCount: 3` run before the third delivery), no native redrive limit at or below 3, and on GCP a `DeadLetterPolicy` with `MaxDeliveryAttempts: 5` where the ADR's mechanism needs it (A-1). R-6 confirms the count still advances at `-1`, so R-1/R-3 remain testable. | `:1057` |
| 5 | 56 | AC-3's GCP parenthetical made conditional: where the ADR's mechanism needs the policy, a no-policy subscription is AC-11's case; where it does not, AC-3 also applies to a no-policy GCP subscription. | `:1073` |
| 6 | 48 | AC-27's Given adds "no sample containing `#pragma warning disable`, no new project created". | `:1358` |
| 7 | 42 | Guard's reason corrected: "at `-1` the budget is never consulted (`MessagePump.cs:171`), and at `0`, `1` or below `-1` `HandledCountReached` is true on the first deferral". | `:1054` |
| 8 | 38 | AC-2 removed from the guard. The guard now lists AC-1, AC-3, AC-4, AC-34's second clause and AC-41; AC-2 is named as unguarded, because a first delivery presenting `0` must hold on both branches (AC-38) and AC-2 is R-23's criterion. | `:1046`, `:1050` |

**For round 7's spot-check**: finding 1 edited R-13 and A-2, so read AC-19, AC-22, AC-39 and AC-40
with them. Finding 2 edited R-5 and R-28, so read AC-4 and AC-41 with them. Finding 4 moved AC-1 to
`-1`, so read R-1, R-3 and R-6 with it.

---

# Review: requirements (round 7) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

## Findings

### 1. R-1's lease/visibility/ack-deadline-expiry redelivery has no acceptance criterion, and it is where the candidate mechanisms diverge (Score: 66)

R-1 binds two redelivery paths: "whether the redelivery follows an explicit `Requeue` or a
lease/visibility/ack-deadline expiry" (:128-129). Its only AC is AC-1 (map :1456), which covers only
the explicit path — "a handler that defers on every delivery" (:1061) drives `RequeueMessage` →
`Channel.Requeue` (Reactor.cs:492-514). Nothing tests a redelivery following expiry with no `Requeue`
call (a message received and never acked or requeued, or a handler that outlives the visibility
timeout or ack deadline). §Candidate mechanisms keeps "Track the count consumer-side" open (:911); a
consumer-side count advanced inside `Requeue` satisfies AC-1 and fails R-1 on every expiry
redelivery. AC-23 measures lease-lapse on RocketMQ only, and is a measurement, not an assertion.

**Evidence**: requirements.md:126-129, :1057-1063, :1456, :911; Reactor.cs:492-514.

**Recommendation**: Add an AC (or a second clause to AC-1): **Given** a transport in scope on its
implemented branch and a message received but neither acked nor requeued, **When** its visibility
timeout / ack deadline lapses and it is received again, **Then** the second delivery presents a
strictly greater count. Or narrow R-1 to explicit requeue and say why expiry is excluded.

---

### 2. AC-13 requires the v3 and v4 FR-23 runs to show the same number of deliveries, which R-4 lets vary on an approximate counter (Score: 62)

AC-13: "these are identical: the number of deliveries, …" (:1189-1193). SQS's
`ApproximateReceiveCount` is approximate (A-4, :936-938), so R-4 promises only "at most `R`" and its
own example allows rejection on delivery 2 for a `0`, `2` jump (:188-189). The v3 and v4 runs use
separately provisioned queues, so one may reject on delivery 2 and the other on 3 — both satisfying
R-4 and AC-3, while AC-13 fails. Round 6 fixed this class of defect at R-5, R-28 and AC-41; AC-13
was not touched.

**Evidence**: :1189-1193, :188-189, :936-938, :1076.

**Recommendation**: Compare delivery counts for equality only where the ADR's R-3 table classifies
SQS exact (as AC-34 and AC-41 do); otherwise require both `<= R` and drop the count from the
identity set.

---

### 3. AC-1 at `-1` names no instrument for recording the per-delivery count and no stop condition, and R-27(c)(5) omits it (Score: 52)

With AC-1 at `requeueCount: -1` (:1057) nothing ends the run, so the test must recognise the third
delivery and quit the pump, and must record each delivery's `HandledCount`. R-27(c)'s dispatch
counter "has no relationship to `MessageHeader.HandledCount`" (:803-806), and handlers are created
fresh per dispatch (:762-764). R-27(c)(5) lists bespoke-subscription ACs as "AC-5, AC-6 and AC-35"
(:791-794); AC-1 (and AC-34's `requeueCount: 4`, :1115) also need bespoke subscriptions because
R-27(a) fixes providers at `R = 3`. Below threshold because every observation point sees the same
pre-increment value.

**Recommendation**: Add AC-1 and AC-34 to R-27(c)(5); have obligation 6's recording decorator also
record the `HandledCount` returned on each delivery; quit the pump when the dispatch count reaches 3.

---

### 4. R-27's worked example still closes the window at the DLQ poll and states an exact count of 3 (Score: 50)

"the dispatch count for that message id stands at 3 when the DLQ poll closes" (:815-816) contradicts
R-4 ("The poll itself … is *not* the close", :178-181), R-27(c)(3) (:785-787), and R-4's
approximate allowance on `AWS / SqsStandard` (:188-189). Similar exact-count wording at :290,
:349-350, :387, :812-813. The spec's own trap note (:1594-1596) is that Examples get implemented.

**Recommendation**: "stands at 3 or fewer (3 on an exact counter) when the pump has been quit and
awaited"; qualify the other examples "(or earlier on an approximate counter, R-4)".

---

### 5. AC-39(b) presupposes a mechanism exists after a refuted A-2 (Score: 35)

"if it did not, which `delivery_attempt`-independent mechanism it selected in consequence"
(:1238-1239), while R-13's second branch (:370-372) permits none. Clause (c) covers the case; the
defect is wording only.

**Recommendation**: "…which `delivery_attempt`-independent mechanism it selected, or that none fits".

---

## Round-6 remediation spot-check

| # | Score | Applied text checked | Result |
|---|---|---|---|
| 1 | 74 | R-13 second bullet re-keyed; "A-2 shapes the route, not the branch"; A-2 "does not by itself select R-13's first branch" | PRESENT :370-376, :378-380, :926-930. Read with AC-19, AC-22, AC-39, AC-40, NFR-7: all key on the ADR's conclusion. Residual: finding 5. |
| 2 | 72 | R-5 / R-28 / R-28 example / AC-41 "at least `R`, exactly `R` where exact" | PRESENT :203-204, :245-246, :275-276, :1130-1131. Consistent with the FR-23 template's `>= RequeueCount - 1` and AC-4 `>= 2`; broker-routed `R - 1` holds (RMQ only, republishes, exact). Residual: findings 2, 4. |
| 3 | 64 | AC-5 "no native redrive policy"; R-6 "absent a native redrive limit (R-8)" | PRESENT :1086-1088, :212. |
| 4 | 64 | AC-1 at `-1`, reason, GCP policy clause | PRESENT :1057-1063. `UpdateHandledCount()` runs before the `DiscardRequeuedMessagesEnabled()` check (Reactor.cs:494-496), so R-6 holds. Residual: findings 1, 3. |
| 5 | 56 | AC-3 conditional GCP parenthetical | PRESENT :1072-1074; matches AC-11 :1163-1164. |
| 6 | 48 | AC-27 `#pragma` / no new project | PRESENT :1358-1359; matches R-24. |
| 7 | 42 | Guard's `-1` reason | PRESENT :1053-1055; MessagePump.cs:171-174. |
| 8 | 38 | AC-2 out of the guard | PRESENT :1046-1053; consistent with AC-38 and map row R-23. |

## Integrity checks

- `R-1..R-28`, `NFR-1..NFR-8`, `AC-1..AC-41`, `C-1..C-12`, `A-1..A-5` each defined once, no gaps
  (naive-grep doubles are bold references at line start, not definitions).
- Every referenced identifier is defined. Map: 36 rows; every cited AC defined; only AC-30/AC-31
  unmapped, by settled decision.
- Citations re-verified: Reactor.cs:492-514, MessagePump.cs:171-174, Message.cs:161-164,
  `DefaultMessageAssertion.cs.liquid:59`, the FR-23 template's `RequeueCount - 1` assertion.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 1 |

**Total findings**: 5
**Findings at or above threshold (60)**: 2

## Main-agent validation of this round

- Counted: 66, 62 (≥60); 52, 50, 35 (<60) — 0/0/4/1, total 5, two at or above threshold. Agrees.
- Findings 1 and 2 re-verified against the file: R-1 :128-129 names the expiry path; the map row
  `| R-1 | AC-1 |` is at :1456; AC-13 :1189-1193 lists "the number of deliveries" as identical.
- Convergence: 13 → 6 → 8 → 5 findings, with no High or Critical this round. Every round-6
  remediation was present.

---

# Remediation log — round 7

**Date**: 2026-09-23. **Applied to**: `requirements.md` and `README.md`. Every applied text grepped
back from the file on disk after the write; this log written from that read-back. **Outcome**: both
findings at or above threshold remediated, plus all three below. Document now holds **28 `R-n`,
8 `NFR-n`, 42 `AC-n`** (was 41), C-1..C-12. Integrity re-checked programmatically: no gaps, no
undefined references, map row `| R-1 | AC-1, AC-42 |`.

**The decision this round took (the user's, 2026-09-23):** finding 1 was resolved by **adding an
acceptance criterion** for R-1's expiry path, not by narrowing R-1. The cost (one extra
receive / lapse / receive test per transport) was judged not significant overall.

| # | Score | Remediation | Verified at |
|---|---|---|---|
| 1 | 66 | New **AC-42** (R-1), "the expiry path": on each transport in scope, a short visibility timeout / ack deadline / invisible duration, the message received and neither acked, rejected nor requeued, the timeout lapses, received again through `Receive` and separately `ReceiveAsync` (NFR-8); **Then** the second delivery presents a strictly greater count. It states that a count advanced only inside `Requeue` fails it, and that on RocketMQ it asserts what AC-23 measures. AC-42 added to the guard paragraph's list and to the R→AC map. | `:1074-1083`, `:1054`, `:1478` |
| 2 | 62 | AC-13: "the number of deliveries **only if** the ADR's R-3 table (AC-34) classifies SQS **exact** — otherwise each package's count is `<= R` and the two are not compared for equality". | `:1211` |
| 3 | 52 | AC-1's When now says the test quits and awaits the pump once the dispatch count reaches 3, and its Then reads counts "as recorded by R-27(c)(6)'s recording consumer". R-27(c)(5) now lists AC-1, AC-5, AC-6, AC-34's second clause, AC-35 and AC-42. R-27(c)(6)'s recording consumer now also records each received message's `Header.HandledCount`, in delivery order, by obligation 1's key. The closing "unassertable" sentence names the per-delivery counts of AC-1, AC-34 and AC-42. | `:1070`, `:792`, `:804`, `:815` |
| 4 | 50 | R-27's worked example now reads "3 or fewer … when the pump has been quit and awaited (R-27(c)(3)), not when the DLQ poll breaks". The exact-count examples at R-8 (Brighter wins), R-12, R-13 and R-27(a) qualified "(or earlier / fewer on an approximate counter, R-4)". | `:824`, `:290`, `:349`, `:387`, `:820` |
| 5 | 35 | AC-39(b): "…which `delivery_attempt`-independent mechanism it selected in consequence, or that none fits". | `:1261` |

**For round 8's spot-check**: AC-42 is new. Read it with R-1, R-2 (a first receive must present
`0`), R-14/AC-23 (RocketMQ), A-1 (GCP policy), NFR-8 (variants without a pump) and the guard.
R-27(c)(6)'s decorator now has two jobs; read it with C-10.

---

# Review: requirements (round 8) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address this before approving.

## Findings

### 1. AC-42 cannot be driven on `GcpPubSubStreamMessageConsumer` as written — the client library keeps renewing a held message's ack deadline for up to 60 minutes (Score: 70)

AC-42 relies on "a subscription whose … ack deadline (GCP) … is short enough to lapse within the
test", and has the test receive, "neither acknowledges, rejects nor requeues it, waits for that
timeout to lapse, and receives it again" (:1074-1081). That works for the pull consumer, which does a
raw `Pull` and leaves the deadline alone (`GcpPullMessageConsumer.cs:229-238`). It does not work for
the stream consumer, which R-13 also binds (:357-358):

- `GcpPubSubStreamMessageConsumer.ReceiveAsync` reads a channel filled by
  `BrighterStreamHandler.HandleMessage`, which then awaits `streamMessage.WaitForCompleteAsync()`
  until `Accepted()`/`Reject()` (`GcpStreamConsumer.cs:71-84`, `:115-136`). While that task is
  pending, `SubscriberClient`'s lease management extends the deadline — defaults
  `DefaultAckExtensionWindow` "15 seconds" and `DefaultMaxTotalAckExtension` "60 minutes"
  (Google.Cloud.PubSub.V1 3.36.0 XML docs).
- On the stream path the lease is governed by `SubscriberClient.Settings.AckDeadline` /
  `MaxTotalAckExtension`, not the subscription's `AckDeadlineSeconds` (`GcpPubSubMessageGateway.cs:337`).
- Brighter changes none of these: `GcpPubSubConsumerFactory.CreateSubscriberClient` sets only
  `FlowControlSettings` (`GcpPubSubConsumerFactory.cs:110-121`), after an optional `configure` hook.

A literal AC-42 on `GCP / Stream` or `GCP / StreamOrdering` waits up to an hour or times out, and
teardown also stalls because `StopAsync` uses `ShutdownMode.WaitForProcessing` (`GcpStreamConsumer.cs:49`).
One developer overrides `MaxTotalAckExtension` via `GcpPubSubSubscription.StreamingConfiguration`
(:97, :128), which the spec never mentions; another reads "each transport in scope" as satisfied by
the pull consumer alone, leaving the stream consumer's expiry path unevidenced.

**Evidence**: requirements.md:1074-1083, :357-358, :126-129; `GcpStreamConsumer.cs:49`, `:71-84`;
`GcpPubSubConsumerFactory.cs:106-121`; Google.Cloud.PubSub.V1.xml:11446-11459.

**Recommendation**: Name the knob per consumer class — GCP pull: the subscription's
`AckDeadlineSeconds`; GCP stream: `SubscriberClient.Settings.MaxTotalAckExtension` (and
`AckDeadline`) via `StreamingConfiguration`. State that AC-42 binds both GCP consumer classes, and
that the stream test must release the held first delivery, or bound disposal, before teardown. Or
record the lease extension as an ADR input under §Candidate mechanisms / R-13.

---

### 2. AC-42 sits outside the Terms' definitions of "Delivery", "Delivery count" and "Variant" (Score: 45)

The Terms define a **Delivery** as returned from `Receive`/`ReceiveAsync` "**and dispatched by a
pump**", **Delivery count** as the value "as it is handed to the pump" (:100-101), and **Variant** as
"`Reactor` or `Proactor`" (:116); NFR-8 requires both variants (:890-891). AC-42 has "No pump"
(:1081-1082) and meets NFR-8 with `Receive`/`ReceiveAsync` (:1080). AC-15 set the precedent (:1227-1229),
but the glossary claims "exactly these meanings throughout" (:95). Also, R-27(c)(5) says AC-42 uses "the
same counter and the same key" (:792-796), but obligation 1's counter is the pump's dispatch count,
which a pump-less test never advances.

**Recommendation**: In Terms, say that where an AC drives a consumer directly (AC-15, AC-42) a
delivery is one message returned from `Receive`/`ReceiveAsync` and "both variants" means both calls.
Drop AC-42 from R-27(c)(5)'s "same counter" sentence, or say it uses only the obligation-6 record.

---

### 3. R-27(c)(6)'s per-delivery record must capture the value at return time, because the pump mutates the same header (Score: 38)

Obligation 6 "records the `Header.HandledCount` of each message its `Receive`/`ReceiveAsync` returns"
(:804-807); the pump then increments the same object (`MessageHeader.cs:572-575`). A decorator that
stores references and reads after quit (obligation 3) records every value one high — AC-1 still
passes, AC-34's exact `0, 1, 2` fails. AC-1's stop condition (:1069-1070) also needs a live read
of the dispatch count, while obligation 3 specifies reading only after quit and await (:786-788).

**Recommendation**: "records the integer value of `Header.HandledCount` at the moment
`Receive`/`ReceiveAsync` returns, before the pump sees the message"; in obligation 3 allow a live
read as AC-1's stop trigger, with the asserted read still after quit and await.

---

## Round-7 remediation spot-check

| # | Score | Applied text checked | Result |
|---|---|---|---|
| 1 | 66 | AC-42; guard list; map `R-1 \| AC-1, AC-42` | PRESENT :1074-1083, :1054, :1478. Consistent with R-1, R-2, R-14/AC-23/A-3, A-1, the guard, C-10. Residual: findings 1, 2. |
| 2 | 62 | AC-13 conditional equality | PRESENT :1210-1213; consistent with R-4, AC-34, A-4. |
| 3 | 52 | AC-1 stop condition; R-27(c)(5) list; (c)(6) second job; closing sentence | PRESENT :1069-1072, :792-796, :804-807, :814-815; consistent with C-10. Residual: findings 2, 3. |
| 4 | 50 | Worked example re-anchored; examples qualified | PRESENT :822-824, :289-290, :349-350, :387-388, :819-820. Only unqualified exact example left is R-22 :588-590 (Kafka, exact) — correct. |
| 5 | 35 | AC-39(b) "or that none fits" | PRESENT :1260-1261. |

## Integrity checks

- `R-1..R-28`, `NFR-1..NFR-8`, `AC-1..AC-42`, `C-1..C-12`, `A-1..A-5` each defined once, no gaps.
- Every referenced identifier defined. Map: 36 rows; all cited ACs defined; AC-42 mapped from R-1;
  only AC-30/AC-31 unmapped, as settled.
- Citations re-verified: `GcpStreamConsumer.cs:49`, `:71-84`; `GcpPubSubConsumerFactory.cs:106-121`;
  `GcpPullMessageConsumer.cs:229-238`, `:349`, `:384-388`; `RocketMessageConsumer.cs:68`, `:85`,
  `:186-187`; `MessageHeader.cs:572-575`; `GcpPubSubMessageGateway.cs:229`, `:337`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 3
**Findings at or above threshold (60)**: 1

## Main-agent validation of this round

- Counted: 70 (≥60); 45, 38 (<60) — 0/1/0/2, total 3, one at or above threshold. Agrees.
- Finding 1 re-verified in source: `HandleMessage` awaits `WaitForCompleteAsync()`
  (`GcpStreamConsumer.cs:71-84`); `StopAsync` uses `WaitForProcessing` (:49); `CreateSubscriberClient`
  sets only `FlowControlSettings` after the optional `configure` hook; the 3.36.0 XML docs give
  15 s / 60 min defaults.
- Finding 1 is a defect introduced by round 7's own remediation (AC-42), found on its first review.
- Convergence: 13 → 6 → 8 → 5 → 3, with one High, and it is confined to one consumer class.

---

# Remediation log — round 8

**Date**: 2026-09-23. **Applied to**: `requirements.md` and `README.md`. Each applied text grepped
back from the file on disk; this log written from that read-back. **Outcome**: the one finding at or
above threshold remediated, plus both below. Counts unchanged at **28 `R-n`, 8 `NFR-n`, 42 `AC-n`**;
integrity re-checked programmatically, no gaps and no undefined references.

| # | Score | Remediation | Verified at |
|---|---|---|---|
| 1 | 70 | AC-42 now binds **both** GCP consumer classes and names the knob that governs the lapse per consumer: SQS visibility timeout; GCP pull, the subscription's `AckDeadlineSeconds`; GCP stream, `SubscriberClient.Settings.MaxTotalAckExtension` (with `AckDeadline`) via `GcpPubSubSubscription.StreamingConfiguration`, with the reason (lease extension up to 60 min by default; `AckDeadlineSeconds` does not govern it); RocketMQ invisible duration. It adds that the stream test must complete the held first delivery before teardown, because of `ShutdownMode.WaitForProcessing`. **Workability checked in source**: `GcpPubSubConsumerFactory.CreateSubscriberClient` invokes the `configure` hook *before* `builder.Settings ??= new …` and then overwrites only `FlowControlSettings`, so a `MaxTotalAckExtension` set through `StreamingConfiguration` survives. | `:1080-1089`, `:1097` |
| 2 | 45 | Terms row *Variant* extended: where an AC drives a consumer directly with no pump (AC-15, AC-42), a delivery is one message returned from `Receive`/`ReceiveAsync`, its delivery count is that message's `HandledCount` as returned, and "both variants" means both calls. R-27(c)(5) now says only the pump-driven ACs share obligation 1's counter; AC-42 uses obligation 6's per-receive record or reads the returned message directly. | `:116`, `:799` |
| 3 | 38 | R-27(c)(6) now records "the integer value of `Header.HandledCount` at the moment … `Receive`/`ReceiveAsync` returns each message, before the pump sees it — a value copied, not a reference", citing `MessageHeader.cs:572-575`. R-27(c)(3) now allows a live read of the dispatch count as a stop trigger (AC-1), with the asserted value still read after quit and await. | `:808`, `:789` |

**For round 9's spot-check**: AC-42's new GCP-stream clause, read with R-13 (both consumer classes),
C-10 (configuring `StreamingConfiguration` is real client configuration, not a mock), NFR-7 and
AC-19. The Terms *Variant* extension, read with NFR-8 and AC-15. R-27(c)(3)'s live-read allowance,
read with R-4's window definition.

---

# Review: requirements (round 9) — 0037-delivery-count-and-rejection-routing

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

## Findings

### 1. AC-42's GCP-stream clause deadlocks by default — Brighter caps the `SubscriberClient` at one in-flight handler, and the held first delivery occupies it (Score: 72)

AC-42 has the stream test hold the first delivery unanswered, wait for the lease to lapse, receive
again, and only then complete the first ("acknowledge or reject it after the second receive",
:1096-1099). With the default subscription shape that order cannot complete:

- `GcpPubSubConsumerFactory` builds the client with `maxInFlightMessages = sub.BufferSize *
  sub.NoOfPerformers` (`GcpPubSubConsumerFactory.cs:89-92`) and sets
  `FlowControlSettings.MaxOutstandingElementCount` from it (`:118-121`). `Subscription` defaults are
  `bufferSize = 1`, `noOfPerformers = 1` (`Subscription.cs:200-201`), so the cap is **1**.
- Google.Cloud.PubSub.V1 3.36.0: "the number of messages being processed concurrently is limited by
  these settings, at the level of the whole SubscriberClient" (XML :11606-11613).
- The held delivery is still being processed: `BrighterStreamHandler.HandleMessage` awaits
  `WaitForCompleteAsync()` (`GcpStreamConsumer.cs:78-79`) until `Accepted()`/`Reject()`/cancel.
- After `MaxTotalAckExtension` lapses the server redelivers, but the client will not invoke
  `HandleMessage` for it while the only slot is taken. The second `Receive` times out, and the first
  is never completed because AC-42 completes it only after the second receive. Circular.
- `StreamingConfiguration` cannot fix it: it runs before Brighter overwrites `FlowControlSettings`
  (`:116-121`). The only lever is the subscription's `bufferSize`/`noOfPerformers`, which AC-42 does
  not mention.
- `GCP / StreamOrdering` has a probable second blocker: the redelivery shares the held message's
  ordering key, and the client dispatches same-key messages sequentially. Not confirmed from the XML
  docs.

Round 8's workability check verified that `MaxTotalAckExtension` survives the factory, not flow
control.

**Evidence**: requirements.md:1080-1099; `GcpPubSubConsumerFactory.cs:89-92`, `:116-121`;
`Subscription.cs:200-201`; `GcpStreamConsumer.cs:71-84`; Google.Cloud.PubSub.V1.xml:11590-11614.

**Recommendation**: Require `bufferSize` × `noOfPerformers` ≥ 2 in AC-42's GCP-stream Given; say
`StreamingConfiguration` cannot raise flow control; say whether `GCP / StreamOrdering` is covered and
how. Or record the stream consumer's expiry path as an ADR input under §Candidate mechanisms / R-13,
and state what evidences R-1's expiry clause for `GcpPubSubStreamMessageConsumer`.

---

### 2. The extended "Variant" term redefines "both variants" for AC-15 as `Receive`/`ReceiveAsync`, so `RejectAsync`, which R-16 binds, need not be exercised (Score: 62)

The Terms row says that for pump-less ACs "(AC-15, AC-42) … 'both variants' means both of those
calls" — `Receive`/`ReceiveAsync` (:116) — under a glossary claiming "exactly these meanings
throughout" (:95). AC-15's When is "`Reject` is called with each of …" (:1243-1246), so AC-15 is met
by rejecting only synchronously. R-16 binds `Reject`/`RejectAsync` on both consumer classes
(:439-440), and on the pull consumer they are separate paths (`GcpPullMessageConsumer.cs:276`,
`:306`). AC-16, AC-17 and AC-18 are also pump-less but not listed; AC-18's "delivered again" (:1266)
falls outside both definitions. Round 8's remediation fixed AC-42's reading and introduced a second
reading of AC-15.

**Evidence**: requirements.md:95, :100, :116, :439-440, :1243-1252, :1254-1266.

**Recommendation**: Define pump-less "both variants" as "the synchronous and asynchronous form of
every consumer call the AC makes (`Receive`/`ReceiveAsync`, `Reject`/`RejectAsync`, …)", and list
all pump-less ACs (AC-15 to AC-18, AC-42) or make the list open.

---

### 3. AC-42's teardown rationale overstates the stall and completes only one held delivery (Score: 32)

`StopAsync` passes no `Timeout` (`GcpStreamConsumer.cs:49`), and the library documents "If null, a
default timeout based on the maximum extension duration is used" (XML :11711-11716). With AC-42's
short `MaxTotalAckExtension`, shutdown is bounded, not hung. The second delivery (and any unread
redelivery) is equally pending but unnamed.

**Recommendation**: "complete every delivery the test received, or accept a shutdown bounded by the
configured `MaxTotalAckExtension`".

---

### 4. R-27(c)(6)'s record is read "as obligation 3 requires" (after the pump is quit), but AC-42 runs no pump (Score: 30)

:812-813 against :786, :799-800 and :1094. Harmless in practice.

**Recommendation**: Add "(for a pump-less AC, after its last receive)".

---

## Round-8 remediation spot-check

| # | Score | Applied text checked | Result |
|---|---|---|---|
| 1 | 70 | AC-42 binds both GCP consumer classes; per-consumer knob; complete held delivery before teardown | PRESENT :1080-1099. Citations verified. Consistent with R-13, C-10, NFR-7, AC-19. **Residual: findings 1, 3** — flow control blocks the redelivery. |
| 2 | 45 | Terms *Variant* extension; R-27(c)(5) split | PRESENT :116, :798-800. Consistent with NFR-8 and AC-42. **Residual: finding 2.** |
| 3 | 38 | R-27(c)(6) value copy; R-27(c)(3) live-read stop trigger | PRESENT :808-811, :789-790. Consistent with R-4, AC-1, AC-3. **Residual: finding 4.** |

## Integrity checks

- `R-1..R-28`, `NFR-1..NFR-8`, `AC-1..AC-42`, `C-1..C-12`, `A-1..A-5` defined, no gaps; grep
  doubles are line-start bold references.
- No undefined references. Map: 36 rows; all cited ACs defined; only AC-30/AC-31 unmapped.
- Citations re-verified: `GcpStreamConsumer.cs:49`, `:71-84`, `:115-136`;
  `GcpPubSubConsumerFactory.cs:89-92`, `:106-124`; `GcpPubSubStreamMessageConsumer.cs:84`,
  `:170-205`, `:217`; `Subscription.cs:200-201`; `RocketMqMessageGatewayProvider.cs:137`; 3.36.0 XML.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 4
**Findings at or above threshold (60)**: 2

## Main-agent validation of this round

- Counted: 72, 62 (≥60); 32, 30 (<60) — 0/1/1/2, total 4, two at or above threshold. Agrees.
- Finding 1 re-verified in source: `GcpPubSubConsumerFactory.cs:88-91` passes
  `sub.BufferSize * sub.NoOfPerformers`; `Subscription.cs:200-201` defaults both to `1`; the 3.36.0
  XML (:11607-11613) states the client-wide concurrent-processing limit; `ShutdownOptions.Timeout`
  null → bounded default (:11711-11716), which supports finding 3.
- Findings 1 and 2 are both defects introduced by round 8's remediation, each caught on first review.
  AC-42's GCP-stream clause has now needed three rounds. That is a signal the stream consumer's
  expiry path is an implementation-design question, not a requirements one.

---

# Remediation log — round 9

**Date**: 2026-09-23. **Applied to**: `requirements.md` and `README.md`. Each applied text grepped
back from the file on disk; this log written from that read-back. **Outcome**: both findings at or
above threshold remediated, plus both below. Counts unchanged at **28 `R-n`, 8 `NFR-n`, 42 `AC-n`**;
integrity re-checked programmatically, no gaps and no undefined references.

**The decision this round took (the user's, 2026-09-23): Option A.** AC-42's GCP-stream clause had
needed three rounds. Each fix exposed another layer of client-library behaviour: lease extension,
then flow control, then ordering. The user agreed that *how* to drive a lease lapse through
`GcpPubSubStreamMessageConsumer` is not a requirements call. The obligation stays in AC-42; the
mechanics move to a named ADR input under R-13.

| # | Score | Remediation | Verified at |
|---|---|---|---|
| 1 | 72 | **Option A.** AC-42's GCP-stream knob now points to "the configuration the ADR records under R-13's stream-consumer input". Its closing sentence says the procedure for `GCP / Stream` and `GCP / StreamOrdering` (lapse, second receive, completing held deliveries) is the ADR's, and the obligation is unchanged. The prescriptive teardown sentence was removed. New paragraph under R-13, "**The GCP stream consumer's expiry redelivery is an ADR input, not a requirements decision.**", records the four source-verified facts (lease extension up to 60 min and the surviving `StreamingConfiguration` hook; the client-wide `BufferSize × NoOfPerformers` cap, default 1, which the hook cannot raise; possible same-key sequential dispatch on `StreamOrdering`, marked *not verified*; bounded shutdown). It adds an obligation: "The ADR MUST record, for each of `GCP / Stream` and `GCP / StreamOrdering`, the … configuration and the receive / complete sequence by which AC-42 is driven — **or** … what evidences R-1's expiry clause for it instead and why. It may not leave either configuration unevidenced." AC-42's stream clause is added to §"Assertable, but not writable until the ADR exists". | `:382-407`, `:1112`, `:1122`, `:1582` |
| 2 | 62 | Terms *Variant*: pump-less "both variants" now means "the synchronous **and** asynchronous form of every consumer call the AC makes — `Receive`/`ReceiveAsync`, `Reject`/`RejectAsync`, `Acknowledge`/`AcknowledgeAsync`, and so on"; the AC list is open ("e.g. AC-15 to AC-18, AC-42"). | `:116` |
| 3 | 32 | Dissolved by finding 1's fix: the prescriptive "would otherwise wait on it" sentence is gone. The bounded-shutdown fact is recorded in the ADR input instead. `grep "would otherwise wait on it"` → 0 matches. | `:401` |
| 4 | 30 | R-27(c)(6)'s read clause adds "(for a pump-less AC such as AC-42, after its last receive)". | `:840` |

**For round 10's spot-check**: the new R-13 ADR-input paragraph, read with AC-42, R-1, NFR-7, AC-19,
C-10 and §Candidate mechanisms. Does the "MUST … or …" obligation leave any way for the stream
consumer to go unevidenced? The Terms *Variant* row, read with AC-15 to AC-18 and R-16.
