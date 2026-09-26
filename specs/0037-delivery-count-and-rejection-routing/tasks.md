# Tasks: 0037 — Delivery Count and Rejection Routing (the FR-23 family)

**Spec:** `specs/0037-delivery-count-and-rejection-routing/requirements.md` (approved): 28 R-n, 8 NFR-n, 43 AC-n
**Issues:** #4341 (SQS budget inert), #4386 (GCP rejection routing), #4353 (RocketMQ requeue no-op), #4354 (GCP DLQ channel creation without IAM admin)
**ADRs (Accepted):** `docs/adr/0077-delivery-count-contract.md`, `docs/adr/0078-gcp-rejection-routing-and-dlq-channel-creation.md`
**Delivery:** one PR. Phase headings are stable so `/spec:gear` can scope to them.

## How to use this list

- **TDD gear.** The approval gate is on by default (`review-before`). Change it only with `/spec:gear`. This file does not record the gear.
- **Task kinds.**
  - `TEST + IMPLEMENT`: use `/test-first`. The ⛔ gate applies.
  - `CHARACTERISE`: use `/test-first`. The ⛔ gate applies. The test is expected **green on arrival** because earlier work already delivers the behaviour; each task names its RED mutation(s). See the note below.
  - `TIDY`: use `/tidy-first`. No behaviour changes, and existing tests stay green.
  - `GATE` / `MEASURE`: manual gates. Each one produces a concrete record in a named file. They do not use `/test-first`.
  - Counts: 79 tasks — 36 `TEST + IMPLEMENT`, 17 `CHARACTERISE`, 4 `TIDY`, 17 `GATE`, 5 `MEASURE`.
- **CHARACTERISE procedure.** Source: `.agent_instructions/testing.md:46-73` on branch `feature/4334-validate-subscription-channel-factory` ("When a new test passes on first run — characterisation"); this note becomes redundant once 4334 merges.
  1. Write the test. Expect it green on arrival. Do **not** weaken, rewrite or delete it to get a failure, and do **not** treat the task as done.
  2. Observe RED through the task's **named mutation**: a temporary change to **production code, never the test**, applied one at a time.
  3. The test must fail **on the assertion it is about**. A compile error or an unrelated exception thrown before that assertion does not count.
  4. Revert the mutation and confirm green; `git status` shows no production file modified.
  5. Run the full suite for the affected project(s).
  6. Commit the test alone as `test:`, noting the mutation in the message. The mutation is never committed.
  - If a `CHARACTERISE` test is unexpectedly **RED** on arrival, or a non-`CHARACTERISE` test is unexpectedly **green**, stop and ask.
- **Conformance templates.** Edit the Liquid templates under `tools/Paramore.Brighter.Test.Generator/Templates`. Then regenerate with `./generate-test.sh`, as described in `.agent_instructions/generated_tests.md`, and run the generated-tree audit (`dotnet test tests/Paramore.Brighter.Test.Generator.Tests/... -f net10.0`). **Never hand-edit a file under `Generated/`.**
- **The ledger drives Skip.** A canonical generated test is emitted with `[Fact(Skip = "Deferred: …")]` while its cell in `specs/0036-universal-transport-conformance-tests/conformance-status.md` is not `Pass`/`Fixed` (see `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_ledger_marks_a_cell_should_emit_skip_only_when_not_proven.cs`). So each cell-moving GATE works like this:
  1. Change the cell.
  2. Regenerate.
  3. Run the suite.
  4. Record the evidence beside the cell.
  5. If the run is red, put the cell back.

  AC-30 is therefore split into one ledger gate per transport phase, plus a final audit in Phase 8.
- **Branch sections.** RocketMQ (AC-23 → AC-24 or AC-25) and GCP (AC-39 plus ADR 0077's recorded conclusion → AC-19 or AC-40) each have a MEASURE task and then two branch sections. **Do only the branch the measurement selected.** Mark every task in the other branch `[!] not taken — AC-nn selected AC-mm`. Work needed by both branches sits in the "common" sections and is not repeated.
- **Paired variants (NFR-8).** Every behavioural task covers the sync (Reactor, `Receive`/`Reject`) and async (Proactor, `ReceiveAsync`/`RejectAsync`) variants in one task. The sync test file uses the plain name. The async twin carries an `_async` suffix, in the `Proactor` sibling folder where one exists, otherwise in the same folder.
- **AWS lockstep (NFR-6).** Every Phase 4 task changes `Paramore.Brighter.MessagingGateway.AWSSQS` and `…AWSSQS.V4` in the same commit. Tests land in both `tests/Paramore.Brighter.AWS.Tests` and `tests/Paramore.Brighter.AWS.V4.Tests`.
- **Log capture.** Where a test asserts a log line, use a capturing `ILogger` test double via `ApplicationLogging`. Pattern: `tests/Paramore.Brighter.BoxProvisioning.Tests/TestDoubles/CapturingLogger.cs`, which `Paramore.Brighter.Gcp.Tests` already references, and `tests/Paramore.Brighter.Core.Tests/Validation/TestDoubles/SpyLogger.cs`. For AWS and RocketMQ, add a local copy under the project's existing `TestDoubles/` folder. Do not add a project reference (R-24).
- **Out of scope. Do not task or fix:**
  - #4415 (SQS failed DLQ send)
  - the `RocketMessageConsumer.ReadDelay` defect (it gets its own `/bugfix`)
  - anything in the requirements' "Out of Scope" section

**Why the phase order differs from the suggestion:**
1. The AC-27 compile-only samples move to Phase 1. They must be written against the V10 surface *before* this spec changes it, and once they exist every later commit is build-checked. Phase 8 only re-verifies them.
2. R-27 harness work is spread out. R-27(c) (dispatch count and recording consumer) is in Phase 3 because every pump-driven bespoke test in Phases 4, 6 and 7 depends on it. R-27(a) for AWS is in Phase 4, and R-27(a)/(b) for GCP is in Phase 5 (ADR 0078 step 5), each before the first test that reads those providers. Phase 8 keeps only the audits.

---

## Phase 1 — Structural (Tidy First)

- [x] **1.1 GATE: Commit the five compile-only V10 compatibility samples before any public surface changes (R-24, AC-27)**
  - Add one compile-only sample file (`V10CompatibilitySample.cs`) to each of these existing projects:
    - `tests/Paramore.Brighter.Core.Tests/`: `Subscription`, `MessageHeader`, `Message`
    - `tests/Paramore.Brighter.AWS.Tests/`: `SqsSubscription`
    - `tests/Paramore.Brighter.AWS.V4.Tests/`: `SqsSubscription` (v4)
    - `tests/Paramore.Brighter.Gcp.Tests/`: `GcpPubSubSubscription`
    - `tests/Paramore.Brighter.RocketMQ.Tests/`: `RocketMqSubscription<T>`
  - Construct and assign the types with the argument shapes a V10 application uses today. This includes `requeueCount: 3` and the positional and named ctor arguments that exist now. Assert nothing.
  - No `#pragma warning disable`, no new project, no new `ProjectReference`/`PackageReference`.
  - **Output / verification:** the solution builds with no new errors or warnings. Tick "AC-27 samples committed (commit sha)" in `specs/0037-delivery-count-and-rejection-routing/README.md` Status Checklist.

- [x] **1.2 TIDY: Add core `RejectionMetadataKeyNames` constants for the five rejection-metadata keys**
  - **USE COMMAND**: `/tidy-first add RejectionMetadataKeyNames constants type to Paramore.Brighter`
  - Add a new static class `Paramore.Brighter.RejectionMetadataKeyNames` holding `RejectionReason = "rejectionReason"`, `RejectionMessage`, `RejectionTimestamp`, `OriginalTopic` and `OriginalMessageType`, spelled exactly as `SqsMessageConsumer.RefreshMetadata` spells them today. Source: ADR 0077 Key Components, "Metadata vocabulary".
  - Do not rename the generated harness record `RejectionMetadataKeys` (`Templates/MessagingGateway/Shared/RejectionMetadataKeys.cs.liquid`).
  - Verification: no behaviour change; existing tests stay green.

- [x] **1.3 TIDY: In-scope `RefreshMetadata` methods use `RejectionMetadataKeyNames`**
  - **USE COMMAND**: `/tidy-first replace rejection metadata string literals with RejectionMetadataKeyNames in SQS and RocketMQ consumers`
  - Change `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs` `RefreshMetadata` (`:496-513`), the V4 twin (`:489`), and `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMessageConsumer.cs` `RefreshMetadata` (`:248`). Replace literals only. The null-reason early return stays for now; it changes in 4.5 and 7.2.
  - Depends on: 1.2
  - Verification: no behaviour change; existing tests stay green.

- [x] **1.4 TIDY: Swap the SQS inline creators' call order so the bag is read before the handled count**
  - **USE COMMAND**: `/tidy-first move bag read before ReadHandledCount in SqsInlineMessageCreator`
  - Change `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsInlineMessageCreator.cs` (`ReadHandledCount` called `:60`, bag read `:76`) and the V4 twin (`:60`). Source: ADR 0077 "Where each transport reads its counter".
  - Verification: no behaviour change; existing tests stay green.

- [x] **1.5 TIDY: Extract private settle helpers from the GCP consumers' `Acknowledge`/`Requeue` (ADR 0078 Implementation step 1)**
  - **USE COMMAND**: `/tidy-first extract handle-taking ack and release helpers in GcpPullMessageConsumer and GcpPubSubStreamMessageConsumer`
  - `GcpPullMessageConsumer`: add private `AckByHandle(string ackId)`/`AckByHandleAsync`, from the ack RPC call currently made in `Acknowledge` (`:29`, call `:39`), `AcknowledgeAsync` (`:55`, call `:65`), `Reject` (`:276`, call `:288`) and `RejectAsync` (`:306`, call `:317`), and `ReleaseByHandle(string ackId)`/`ReleaseByHandleAsync`, from `Requeue` (`:344/349`, `:379/384`). Each helper performs the client lookup (`GetOrCreateSubscriberServiceApiClient`/`CreateSubscriberServiceApiClientAsync`) and the RPC with **no try/catch of its own**; every caller invokes it inside its existing `try`, so a client-construction failure is handled like an RPC failure, as today. This deliberately moves ADR 0078's placement of the client lookup inside a `try` from the helper to its callers; the ADR's invariant (a client-construction failure is handled like an RPC failure) is preserved.
  - Helper contract: each helper wraps **only** the client lookup and the RPC, and throws exactly as today. Logging and the catch/return decision stay with the callers (whose log messages differ today). Accepted log-order change: `Reject`'s `RejectMessage` and `Requeue`'s `RequeueStart` logs (today between the lookup and the RPC: `Reject` `:285-288`, `RejectAsync` `:315-317`, `Requeue` `:344-349`, `RequeueAsync` `:379-384`) move before the helper call, so a failed client lookup now emits the start log first.
  - `GcpPubSubStreamMessageConsumer`: add private `Accept(GcpStreamMessage)` and `Nack(GcpStreamMessage)` (used by 5.6's accept after routing and 5.8's Nack on failed routing).
  - The public methods keep their current contracts: pull `Requeue` swallows exceptions and returns `false` (`:354-358`); stream `Requeue` returns `true` with no handle (`:219-222`).
  - Verification: no behaviour change apart from the accepted log-order change above; existing tests stay green.

---

## Phase 2 — Core contract and budget rules

- [x] **2.1 TEST + IMPLEMENT: Normalising a broker delivery counter presents 0 on the first delivery and is never negative**
  - **USE COMMAND**: `/test-first when normalising a broker delivery counter should present zero on first delivery and never be negative`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway"
  - Test file: `When_normalising_a_broker_delivery_counter_should_present_zero_on_first_delivery.cs`
  - Test should verify (AC-33 first clause; R-2, R-3):
    - `DeliveryCount.Normalise(1) == 0`, `Normalise(2) == 1`, `Normalise(3) == 2`
    - `Normalise(null) == 0` and `Normalise(0) == 0`: absent or unset counter, as for Pub/Sub without a `DeadLetterPolicy` (A-1)
    - A negative input yields `0`. The result is never negative.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `public static class Paramore.Brighter.DeliveryCount` with `Normalise(int? brokerCount) => brokerCount is > 1 ? brokerCount.Value - 1 : 0`, as in the ADR 0077 "DeliveryCount contract"
    - Allocate nothing (NFR-2)

- [x] **2.2 TEST + IMPLEMENT: Resolving the delivery count keeps the stamped count on a routed rejection copy and falls back to the header count when no broker counter exists**
  - **USE COMMAND**: `/test-first when resolving delivery count for a routed rejection copy should keep the stamped handled count`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway"
  - Test file: `When_resolving_delivery_count_for_a_routed_rejection_copy_should_keep_stamped_count.cs`
  - Test should verify (R-28, AC-41's discriminator; ADR 0077 "Why the discriminator is `rejectionReason`"):
    - Bag has `RejectionMetadataKeyNames.RejectionReason`, header count 3, broker count 1 → `3`
    - Bag has `"RejectionReason"` (the pump's `Message.RejectionReasonHeaderName`, PascalCase) only → not treated as a routed copy → normalised broker count
    - Broker count `null`, `0` or negative → header count (today's behaviour)
    - Broker count 3, no discriminator → `2`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `DeliveryCount.Resolve(int headerCount, int? brokerCount, IDictionary<string, object> bag)`, exactly as the ADR 0077 contract gives it
    - Make no copy of the bag. Use one `ContainsKey` (ordinal, case-sensitive, matching the `Dictionary` comparer).
  - Depends on: 1.2, 2.1

- [x] **2.3 TEST + IMPLEMENT: A delivery budget of 0 or below -1 reports one startup Warning naming the likely intents**
  - **USE COMMAND**: `/test-first when subscription budget is zero or below minus one should report a warning naming minus one and one as likely intents`
  - Test locations (two tests; `Core.Tests` does not reference `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection`, so `AddConsumers`/`RegisterConsumerValidationSpecs` are reachable only from `Extensions.Tests`):
    - (i) Specification: "tests/Paramore.Brighter.Core.Tests/Validation", file `When_subscription_budget_is_zero_or_below_minus_one_should_report_warning.cs`. Assert directly on the `ConsumerValidationRules` specification (`IsSatisfiedBy` + `Accept(new ValidationResultCollector<Subscription>())`), following `When_subscription_request_type_not_command_or_event_should_report_warning.cs`.
    - (ii) Registration and surfacing: "tests/Paramore.Brighter.Extensions.Tests", file `When_validate_pipelines_with_zero_budget_should_surface_warning_and_start_host.cs`, following `When_validate_pipelines_with_consumers_should_receive_subscriptions.cs`.
  - Test should verify (R-7, R-25, AC-7; the configuration side of AC-35):
    - (i) `requeueCount: 0` → exactly one `ValidationSeverity.Warning` finding, naming the subscription, the value `0`, and both `-1` and `1`
    - (i) `requeueCount: -3` → the same single Warning with value `-3`
    - (i) `requeueCount: 3` and `requeueCount: -1` → no zero-budget finding
    - (ii) `AddConsumers` registers the rule, and a `requeueCount: 0` subscription under `.ValidatePipelines(throwOnError: true)` surfaces the Warning and the host starts (AC-32)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add an `ISpecification<Subscription>` rule to `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`, following the `RequestTypeSubtype` Warning precedent (`:114-123`)
    - Register it in `RegisterConsumerValidationSpecs` (`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:199-215`). It is harvested at `BrighterPipelineValidationExtensions.cs:79`.
    - Bump `Assert.Equal(4, specs.Count)` at `tests/Paramore.Brighter.Extensions.Tests/When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:81` to `5`
    - Nothing logs on the receive path (NFR-4)

- [x] **2.4 TEST + IMPLEMENT: A delivery budget at or above a visible native redrive limit reports one startup Warning**
  - **USE COMMAND**: `/test-first when subscription budget meets or exceeds its native redrive limit should report a warning that the native limit is effective`
  - Test locations (split as in 2.3):
    - (i) Specification: "tests/Paramore.Brighter.Core.Tests/Validation", file `When_subscription_budget_meets_native_redrive_limit_should_report_warning.cs`, asserting directly on the `ConsumerValidationRules` specification
    - (ii) Registration and surfacing: "tests/Paramore.Brighter.Extensions.Tests", file `When_validate_pipelines_with_budget_at_native_limit_should_surface_warning_and_start_host.cs`, with its own test double under `TestDoubles/`
  - Test should verify (R-10, R-25, AC-10):
    - (i) A test-double subscription (under `Validation/TestDoubles`) implementing `IAmADeliveryCountingSubscription` with `NativeRedriveLimit = 5` and `requeueCount: 5` → one Warning naming the subscription, `5` as the budget, `5` as the native limit, and that the effective limit is the native one
    - (i) `requeueCount: 3` with limit 5 → no such finding
    - (i) `requeueCount: -1` → no finding
    - (i) A plain `Subscription` that does not implement the interface → passes vacuously (R-22)
    - (ii) `AddConsumers` registers the rule, and the limit-5/budget-5 double under `.ValidatePipelines()` surfaces the Warning; startup is not blocked
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the core role interface `Paramore.Brighter.IAmADeliveryCountingSubscription { int? NativeRedriveLimit { get; } string? DeliveryBudgetUnenforceableReason { get; } }` (ADR 0077 "Budget rules: which rung")
    - Add the R-10 `ISpecification<Subscription>` rule `R != -1 && NativeRedriveLimit is int m && R >= m` and register it as in 2.3
    - Bump `Assert.Equal(5, specs.Count)` (after 2.3) at `tests/Paramore.Brighter.Extensions.Tests/When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:81` to `6`
  - Depends on: 2.3

- [x] **2.5 TEST + IMPLEMENT: A budget on a subscription that cannot advance its delivery count reports one startup Warning naming the reason**
  - **USE COMMAND**: `/test-first when subscription cannot advance its delivery count should report a warning naming the subscription the budget and the reason`
  - Test locations (split as in 2.3):
    - (i) Specification: "tests/Paramore.Brighter.Core.Tests/Validation", file `When_subscription_cannot_advance_delivery_count_should_report_warning.cs`, asserting directly on the `ConsumerValidationRules` specification
    - (ii) Registration and surfacing: "tests/Paramore.Brighter.Extensions.Tests", file `When_validate_pipelines_with_unenforceable_budget_should_surface_warning_and_start_host.cs`
  - Test should verify (R-11, R-25, AC-11 first clause, and the positive/negative clause of AC-11's third paragraph):
    - (i) A test double answering `DeliveryBudgetUnenforceableReason = "no DeadLetterPolicy"` with `requeueCount: 3` → one Warning naming the subscription, `3`, and the reason
    - (i) The same double answering `null` → no finding
    - (i) `requeueCount: -1` with a non-null reason → no finding
    - (ii) `AddConsumers` registers the rule, and the positive double under `.ValidatePipelines(throwOnError: true)` surfaces the Warning and the host starts
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the R-11 rule `R != -1 && DeliveryBudgetUnenforceableReason is not null`, severity Warning, registered as in 2.3
    - Bump `Assert.Equal(6, specs.Count)` (after 2.4) at `tests/Paramore.Brighter.Extensions.Tests/When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:81` to `7`
  - Depends on: 2.4

- [x] **2.6 TEST + IMPLEMENT: Creating a channel for an unenforceable budget logs exactly one Warning; an enforceable one logs none**
  - **USE COMMAND**: `/test-first when a channel is created for a subscription whose budget cannot be enforced should log one warning and none otherwise`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessagingGateway"
  - Test file: `When_channel_created_for_unenforceable_budget_should_log_warning_once.cs`
  - Test should verify (R-26, AC-11 second and third paragraphs):
    - `DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub)` for a positive test double (`requeueCount: 3`, reason non-null) logs exactly one Warning naming the subscription, `3`, and the reason
    - For a negative double, a `requeueCount: -1` double, and a plain `Subscription`, it logs nothing
    - No R-7 or R-10 text is ever logged here (R-26 gives channel creation to R-11 only)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the core `Paramore.Brighter.DeliveryBudgetDiagnostics.WarnIfUnenforceable(Subscription)`, using `ApplicationLogging` and a source-generated `[LoggerMessage]` in line with `.agent_instructions/code_style.md`
    - Call sites are wired per transport in 4.1, 6.2 and 7.4
  - Depends on: 2.4

- [x] **2.7 CHARACTERISE: Budget-only findings never block startup under `throwOnError: true`**
  - **USE COMMAND**: `/test-first when throw on error is true and the only findings are budget warnings should start the host`
  - Test locations (split as in 2.3):
    - (i) Specification: "tests/Paramore.Brighter.Core.Tests/Validation", file `When_only_budget_rules_fire_should_report_only_warning_findings.cs`. Run the three `ConsumerValidationRules` budget specifications through a `PipelineValidator`, following `When_validator_finds_errors_across_paths_should_aggregate_all.cs`.
    - (ii) Registration and host start: "tests/Paramore.Brighter.Extensions.Tests", file `When_throw_on_error_true_with_only_budget_findings_should_start_host.cs`
  - Test should verify (R-25, R-26, AC-32):
    - (i) Subscriptions tripping R-7, R-10 and R-11 (three doubles) → three Warning findings, one per rule, and no Error. A subscription tripping two rules produces two findings (AC-29's note).
    - (ii) The same application via `AddConsumers` with `.ValidatePipelines(throwOnError: true)` → no `PipelineValidationException`; the host starts
  - 🔁 **Characterisation — expected green on first run:** 2.3–2.5 already create all three budget rules with `ValidationSeverity.Warning`, and a Warning never blocks startup. **RED mutation(s)**, applied and reverted one at a time: (a) in `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`, the R-11 rule added by 2.5 builds its `ValidationError` with `ValidationSeverity.Error` instead of `Warning` — (ii) fails on the "no `PipelineValidationException`; the host starts" assertion, and (i) on "three Warning findings … and no Error".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - If it goes red, the fix is severity, never a new catch.
  - Depends on: 2.3, 2.4, 2.5

---

## Phase 3 — Conformance oracle (templates)

- [x] **3.1 TEST + IMPLEMENT: Redelivery arms of FR-2/15/16/22 accept a redelivered count at least the sent count**
  - **USE COMMAND**: `/test-first when generating redelivery arms should assert redelivered handled count is at least the sent count in both variants`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates"
  - Test file: `When_generating_redelivery_arms_should_assert_handled_count_at_least_sent_both_variants.cs`
  - Test should verify (R-1 vs R-23, C-7; ADR 0077 "Conformance oracle change for the redelivery arms"):
    - Output generated from the Reactor and Proactor templates `When_requeuing_a_failed_message_should_be_redelivered`, `…_with_zero_delay_should_redeliver_immediately`, `…_with_delay_should_redeliver_after_delay` and `When_nacking_a_message_it_should_be_redelivered` contains `Assert.True(redelivered.Header.HandledCount >= message.Header.HandledCount …)`, followed by `redelivered.Header.HandledCount = message.Header.HandledCount;` and then `_messageAssertion.Assert(message, redelivered)`
    - First-receive assertions in other templates are unchanged (R-2 still applies)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Edit those eight `.cs.liquid` templates under `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/`, for example `…should_be_redelivered.cs.liquid:77`, `…zero_delay…:86`, `…with_delay…:85` and `When_nacking…:81`
    - Leave the transport assertion classes (`AwsMessageAssertion`, `RocketMqMessageAssertion`, `DefaultMessageAssertion.cs.liquid`) untouched
    - Regenerate per generated_tests.md; never hand-edit
  - **Must land before 4.3** so no AWS `Pass` cell goes red.

- [x] **3.2 TEST + IMPLEMENT: The shared conformance pump counts dispatches per message, keyed on the original message id, reset between tests**
  - **USE COMMAND**: `/test-first when generating the deferred pump should emit a dispatch count keyed by original message id and reset per test`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates"
  - Test file: `When_generating_deferred_pump_should_emit_dispatch_count_keyed_by_original_message_id.cs`
  - Test should verify (R-27(c)(1)–(3)):
    - Generated `ConformanceDeferredPump` exposes a dispatch count read by message key
    - The key is `x-original-message-id` (`Message.OriginalMessageIdHeaderName`) when present, else `Header.MessageId`. This is the same rule as `AssertIsTheMessageSent`. It is never the command's own id.
    - Both handlers (sync `:45-49`, async `:52-57`) increment it from `Context.OriginatingMessage` before throwing `DeferMessageAction`
    - A reset entry point exists and is called from each generated test's constructor
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Edit `Templates/MessagingGateway/Shared/ConformanceDeferredPump.cs.liquid`. Per-dispatch handler instances (`:128`, `:152`) mean the counter must be static, thread-safe state (for example a `ConcurrentDictionary`).
    - Regenerate per generated_tests.md; never hand-edit
  - Depends on: none (can run parallel to 3.1)

- [ ] **3.3 TEST + IMPLEMENT: A recording consumer, composed around the real consumer, counts Requeue calls and records the presented HandledCount per receive**
  - **USE COMMAND**: `/test-first when generating the deferred pump should emit a recording consumer that counts requeues and records handled count per receive`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates"
  - Test file: `When_generating_deferred_pump_should_emit_recording_consumer_counting_requeues_and_handled_counts.cs`
  - Test should verify (R-27(c)(5)–(6)):
    - Generated decorators implement `IAmAMessageConsumerSync` and `IAmAMessageConsumerAsync`, forward every call, and count `Requeue`/`RequeueAsync` by the key from 3.2
    - They copy the integer `Header.HandledCount` of each message returned by `Receive`/`ReceiveAsync` in delivery order, before the pump sees it (a value, not a reference)
    - The record is reset and read like the dispatch count. Factory helpers build a `Channel`/`ChannelAsync` over the decorator.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Emit the decorator from `ConformanceDeferredPump.cs.liquid`, so no new suite is needed. If a separate Shared template is preferred, register it in `SuitesFor(...)` or the audit reports an orphan (generated_tests.md).
    - This is not a mock and not a production counter (C-10)
    - Regenerate per generated_tests.md; never hand-edit
  - Depends on: 3.2

- [ ] **3.4 TEST + IMPLEMENT: FR-23 asserts that the dispatch count after quit-and-await is within the budget, and that a metadata-stamping route carries `DeliveryError`**
  - **USE COMMAND**: `/test-first when generating the requeue budget test should assert dispatch count within budget and delivery error reason in both variants`
  - Test location: "tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates"
  - Test file: `When_generating_requeue_budget_test_should_assert_dispatch_count_within_budget_both_variants.cs`
  - Test should verify (R-27(c)(4), R-4, AC-3, AC-36 last two clauses, NFR-7):
    - Generated FR-23 (Reactor and Proactor) reads the dispatch count **after** `await pumping` and asserts `<= _subscription.RequeueCount`. The 60 s poll and quit are unchanged, so no extra wait is added.
    - When `RejectionMetadataKeys.StampsRejectionMetadata`, it asserts `dlqMessage.Header.Bag[keys.RejectionReason] == "DeliveryError"`, which shows the Brighter route was taken and not native redrive
    - The existing `HandledCount >= RequeueCount - 1` assertion is kept
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Edit `Templates/MessagingGateway/Reactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` and its Proactor twin
    - Regenerate per generated_tests.md for every configuration; never hand-edit
  - Depends on: 3.2

- [ ] **3.5 GATE: Regenerate, audit, and confirm the nine already-conformant FR-23 configurations still pass (AC-26, R-22, R-23)**
  - Run `./generate-test.sh` and the generated-tree audit, and commit the regenerated tree.
  - Run the scoped conformance suites, both variants, against their compose files for:
    - `Redis / RedisMessagingGateway`
    - `Kafka / Classic`, `Kafka / Consumer`, `Kafka / PartitionKey`
    - `MSSQL`
    - `PostgresSQL`
    - `RMQ.Async / Classic`, `RMQ.Async / Quorum`
    - `RMQ.Sync`
  - FR-23 must pass on all nine, and no `Pass`/`Fixed` cell may regress. The RMQ republish path checks the `x-original-message-id` key fallback from 3.2.
  - Also run the regenerated FR-2/15/16/22 on AWS (LocalStack) and RocketMQ, to show the 3.1 relaxation keeps `Pass` cells green before any receive-path change.
  - **Output:** a dated run record (configurations, variants, result) under the FR-23 section of `specs/0036-universal-transport-conformance-tests/conformance-status.md`. If an out-of-scope configuration fails the 3.4 `rejectionReason` assertion, fix the template's condition, not the assertion, and record why.
  - Depends on: 3.1–3.4

---

## Phase 4 — AWS SQS (#4341; AWSSQS and AWSSQS.V4 in lockstep, NFR-6)

- [ ] **4.1 TEST + IMPLEMENT: `SqsSubscription` reports its redrive policy's `maxReceiveCount` as the native limit, and never reports the budget unenforceable**
  - **USE COMMAND**: `/test-first when an sqs subscription has a redrive policy should expose max receive count as its native redrive limit`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs" and "tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs"
  - Test file: `When_an_sqs_subscription_has_a_redrive_policy_should_expose_native_redrive_limit.cs`
  - Test should verify (R-10, AC-10 transport side; ADR 0077 budget-rules table):
    - `SqsSubscription` is `IAmADeliveryCountingSubscription`
    - `NativeRedriveLimit == 5` for `RedrivePolicy(…, 5)`, and `null` with no policy
    - `DeliveryBudgetUnenforceableReason == null`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement the interface in both `SqsSubscription.cs` files: `QueueAttributes.RedrivePolicy?.MaxReceiveCount` (`SqsSubscription.cs:72`, `SqsAttributes.cs:110`, `RedrivePolicy.cs:34`)
    - Call `DeliveryBudgetDiagnostics.WarnIfUnenforceable` once on each non-delegating channel-creation path. In `AWSSQS/ChannelFactory.cs` that is `CreateSyncChannelAsync` (`:204`) and `CreateAsyncChannelAsync` (`:92`), plus the V4 twins. Never in `CreateAsyncChannel` (`:82-83`), which delegates (R-26). This is a uniform no-op for SQS.
  - Depends on: 2.4, 2.6

- [ ] **4.2 TEST + IMPLEMENT: The eight AWS/AWS.V4 conformance providers hold the budget strictly below the native redrive limit (R = 3, M = 5)**
  - **USE COMMAND**: `/test-first when reading aws conformance providers should configure requeue count three below max receive count five`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway" and "tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway"
  - Test file: `When_reading_conformance_providers_should_hold_budget_below_native_limit.cs`
  - Test should verify (R-27(a), AC-36 first clause, A-5):
    - Build the DLQ-backed subscription from each of `SnsStandard`, `SnsFifo`, `SqsStandard` and `SqsFifo` `MessageGatewayProvider`
    - `RequeueCount == 3` and `NativeRedriveLimit == 5`. This reads through 4.1's interface, so it touches no broker.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Change `new RedrivePolicy(deadLetterChannelName, 3)` to `5` in the four provider files in each project (for example `SqsStandardMessageGatewayProvider.cs:104`). Leave `requeueCount: 3` (`:108`).
  - Depends on: 4.1

- [ ] **4.3 TEST + IMPLEMENT: SQS presents a strictly increasing delivery count across redeliveries, starting at 0**
  - **USE COMMAND**: `/test-first when an sqs message is redelivered should present a strictly greater delivery count than the previous delivery starting at zero`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Sqs/Standard/Proactor", and the V4 twins
  - Test file: `When_an_sqs_message_is_redelivered_should_present_increasing_delivery_count.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-1, R-2, R-3, AC-1, AC-2; NFR-8):
    - Bespoke `SqsSubscription` with `requeueCount: -1`, no `RedrivePolicy` at or below 3, and the deferring handler
    - The pump quits once the dispatch count (3.2) reaches 3; values are read after quit and await
    - The recording consumer's (3.3) per-delivery `HandledCount` sequence is strictly increasing, and its first element is `0` (AC-2)
    - Covers both creators: raw-message delivery (`SqsInlineMessageCreator`) and the SNS-wrapped path (`SqsMessageCreator`), for example one `[Theory]` over `rawMessageDelivery`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In both packages' `SqsMessageCreator.ReadHandledCount` (v3 `:323`, called `:80`, bag `:70`; V4 `:330`, `:77`) and `SqsInlineMessageCreator.ReadHandledCount` (v3 `:350`; V4 `:317`), parse `sqsMessage.Attributes["ApproximateReceiveCount"]` with `TryGetValue` + `int.TryParse`. V4 `Attributes` may be null.
    - Set `HandledCount = DeliveryCount.Resolve(headerCount, brokerCount, bag)`. Absent or unparseable → header count, logging nothing.
    - Add no broker call (the attribute is already requested at `SqsMessageConsumer.cs:188-194`) and no allocation (NFR-1, NFR-2)
  - Depends on: 1.4, 2.2, 3.1, 3.3

- [ ] **4.4 CHARACTERISE: An SQS message whose visibility timeout lapses presents a higher count on the expiry redelivery, with no pump and no Requeue**
  - **USE COMMAND**: `/test-first when an sqs visibility timeout lapses without ack or requeue should present a greater delivery count on redelivery`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_an_sqs_visibility_timeout_lapses_should_present_greater_delivery_count.cs`
  - Test should verify (R-1 expiry path, AC-42):
    - Short queue visibility timeout, no redrive at or below 2, published with `HandledCount = 0`
    - `Receive` returns m1; no ack, reject or requeue; wait past the timeout; `Receive` again returns m2 with `HandledCount > m1`'s
    - Separately through `ReceiveAsync`
  - 🔁 **Characterisation — expected green on first run:** 4.3 already makes both SQS creators read `ApproximateReceiveCount`, and nothing on the expiry path touches the count. **RED mutation(s)**, applied and reverted one at a time: (a) `SqsMessageCreator.ReadHandledCount` (v3 `:323`) and `SqsInlineMessageCreator.ReadHandledCount` (v3 `:350`), or the V4 twins in the package under test, stop consulting `ApproximateReceiveCount` — pass `null` as the broker count to `DeliveryCount.Resolve`, reverting 4.3 — both deliveries present `0`, so it fails on `m2.HandledCount > m1.HandledCount`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - If it is red, the count is being advanced in `Requeue`, which is the defect AC-42 exists to catch.
  - Depends on: 4.3

- [ ] **4.5 TEST + IMPLEMENT: A null-reason SQS Reject stamps `rejectionReason = "None"` so the dead-letter copy keeps its stamped count**
  - **USE COMMAND**: `/test-first when an sqs message is rejected with no reason should stamp rejection reason none on the dead letter copy`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_rejecting_an_sqs_message_with_no_reason_should_stamp_rejection_reason_none.cs`
  - Test should verify (R-28 edge case 1; ADR 0077 "A null-reason Reject stamps `rejectionReason = "None"`"):
    - `channel.Reject(message, null)` routes to the DLQ (existing `DetermineRejectionRoute`, `:275`)
    - The DLQ copy's bag has `rejectionReason == "None"`, has no `rejectionMessage`, and has `originalTopic`/`originalMessageType`/`rejectionTimestamp`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In both `SqsMessageConsumer.RefreshMetadata` (v3 `:506` early return; V4 `:489`), stamp `RejectionMetadataKeyNames.RejectionReason = RejectionReason.None.ToString()` when `reason` is null, and leave `rejectionMessage` absent
  - Depends on: 1.3

- [ ] **4.6 CHARACTERISE: The SQS dead-letter copy of a budget rejection, read through a real channel, carries the stamped count and full rejection metadata**
  - **USE COMMAND**: `/test-first when the sqs budget is exhausted the dead letter copy read through a channel should present the stamped handled count and rejection metadata`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_sqs_budget_is_exhausted_dead_letter_copy_should_keep_stamped_count_and_metadata.cs`
  - Test should verify (R-5, R-28, AC-4, AC-41 behavioural clauses):
    - `requeueCount: 3`, a Brighter DLQ, no native redrive at or below 3, the deferring pump
    - The DLQ is read via a channel from `ChannelFactory` (the provider's ordinary path, not a bespoke reader)
    - `HandledCount >= 3` (Brighter-managed, `>= R-1` and `>= 3`) and **not** `0`, the DLQ's own normalised counter
    - The bag has `rejectionReason == "DeliveryError"`, a non-empty `rejectionMessage`, an ISO-8601-parseable `rejectionTimestamp`, `originalTopic ==` the source topic, and `originalMessageType`
  - 🔁 **Characterisation — expected green on first run:** 2.2's discriminator plus 4.3's broker count already deliver it; the DLQ copy carries `handled-count` (`SqsMessageSender.cs:130`, `SnsMessagePublisher.cs:110`) and the discriminator. **RED mutation(s)**, applied and reverted one at a time: (a) `DeliveryCount.Resolve` (2.2) ignores the `rejectionReason` discriminator and returns the normalised broker count whenever one is present — the DLQ copy presents its own normalised counter `0`, so it fails on "`HandledCount >= 3` and not `0`"; (b) `SqsMessageCreator.ReadHandledCount` (v3 `:323`) and `SqsInlineMessageCreator.ReadHandledCount` (v3 `:350`), or the V4 twins in the package under test, stop consulting `ApproximateReceiveCount` — pass `null` as the broker count to `DeliveryCount.Resolve`, reverting 4.3 — the budget is never reached, the DLQ read returns `MT_NONE`, so it fails on the dead-letter-copy read.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red here points at the `handled-count` attribute on the DLQ send (`SqsMessageSender.cs:130`, `SnsMessagePublisher.cs:110`) or at `DeliveryCount.Resolve`.
  - Depends on: 4.3, 4.5

- [ ] **4.7 CHARACTERISE: With budget 3 below a native limit of 5, SQS dead-letters through Brighter and the native target stays empty**
  - **USE COMMAND**: `/test-first when sqs budget is below the native redrive limit should dead letter through brighter and leave the native target empty`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_sqs_budget_is_below_native_redrive_limit_should_dead_letter_through_brighter.cs`
  - Test should verify (R-8, AC-8):
    - `requeueCount: 3`, `RedrivePolicy(maxReceiveCount: 5)` onto a **distinct** native target queue, the deferring pump
    - The Brighter DLQ copy has `rejectionReason == "DeliveryError"`
    - Reading the native target returns `MT_NONE`
  - 🔁 **Characterisation — expected green on first run:** 4.3 already advances the count so the budget of 3 is reached before the native limit of 5; there is no R-8 code (ADR 0077 "R-8 / R-9"). **RED mutation(s)**, applied and reverted one at a time: (a) `SqsMessageCreator.ReadHandledCount` (v3 `:323`) and `SqsInlineMessageCreator.ReadHandledCount` (v3 `:350`), or the V4 twins in the package under test, stop consulting `ApproximateReceiveCount` — pass `null` as the broker count to `DeliveryCount.Resolve`, reverting 4.3 — Brighter never rejects and SQS redrives natively at 5, so it fails on "the Brighter DLQ copy has `rejectionReason == "DeliveryError"`" (and the native target is no longer empty).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - There is no R-8 code (ADR 0077 "R-8 / R-9"); a red points at 4.3's count reading.
  - Depends on: 4.3

- [ ] **4.8 CHARACTERISE: With native limit 3 below budget 10, SQS redrives natively and the copy carries no rejection metadata**
  - **USE COMMAND**: `/test-first when sqs native redrive limit is below the budget should redrive natively without rejection metadata`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_sqs_native_redrive_limit_is_below_budget_should_redrive_without_rejection_metadata.cs`
  - Test should verify (R-8, R-9, AC-9; AC-41's "no count asserted" clause):
    - Bespoke `requeueCount: 10`, `RedrivePolicy(maxReceiveCount: 3)`, the deferring pump
    - The native target holds the message with **none** of the five `RejectionMetadataKeyNames` keys
    - No `HandledCount` is asserted
  - 🔁 **Characterisation — expected green on first run:** SQS performs the native redrive itself and Brighter neither suppresses nor stamps it (R-9); with 4.3 in place a budget of 10 is never reached in 3 receives. **RED mutation(s)**, applied and reverted one at a time: (a) `DeliveryCount.Resolve` (2.2) over-counts — returns `Normalise(brokerCount) * 5`, the realistic defect of a mis-scaled counter — so the third receive presents 10, the budget is reached and Brighter dead-letters ahead of the native limit, so the native target read returns `MT_NONE` and it fails on "the native target holds the message".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - Brighter neither suppresses nor stamps a native redrive (R-9); a red points at the pump's budget check or at a Brighter stamp reaching the native copy.
  - Depends on: 4.3

- [ ] **4.9 CHARACTERISE: SQS budget of -1 never rejects**
  - **USE COMMAND**: `/test-first when sqs budget is minus one should never reject and never dead letter`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_sqs_budget_is_minus_one_should_never_reject.cs`
  - Test should verify (R-6, AC-5, the `-1` clause of AC-35):
    - `requeueCount: -1`, no `RedrivePolicy`, and a Brighter DLQ routing key, pumped for 60 s
    - Dispatch count `> 3`, no `DeliveryError` rejection issued, and a DLQ read returns `MT_NONE`
  - 🔁 **Characterisation — expected green on first run:** the pump already ignores the count when the budget is `-1` (`MessagePump.cs:171`), so 4.3's newly advancing count never triggers a rejection. **RED mutation(s)**, applied and reverted one at a time: (a) `MessagePump.DiscardRequeuedMessagesEnabled()` (`src/Paramore.Brighter.ServiceActivator/MessagePump.cs:171-174`) returns `true`, treating `-1` as an enabled budget, so `HandledCountReached(-1)` (`Message.cs:161`) rejects on the first deferral — fails on "dispatch count `> 3`" (and the DLQ read is no longer `MT_NONE`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - This test guards R-6 against the newly advancing count; a red points at `MessagePump.DiscardRequeuedMessagesEnabled()` (`MessagePump.cs:171`).
  - Depends on: 4.3

- [ ] **4.10 CHARACTERISE: SQS budget of 1, 0 or below -1 rejects on the first deferral without requeuing**
  - **USE COMMAND**: `/test-first when sqs budget is one zero or below minus one should reject on first deferral without requeue`
  - Test location: "tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor", "…/Proactor", and the V4 twins
  - Test file: `When_sqs_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral.cs`
  - Test should verify (R-7, AC-6, AC-35), as a `[Theory]` over `1`, `0`, `-3`:
    - Handler invoked exactly once (dispatch count == 1)
    - The recording consumer's `Requeue` count for the message is `0`
    - A `DeliveryError` rejection is issued, and the message is on the DLQ
  - 🔁 **Characterisation — expected green on first run:** `HandledCountReached` (`Message.cs:161`) already holds on the first deferral, because `RequeueMessage` calls `UpdateHandledCount` before the check (`Reactor.cs:494-498`), and 3.3 supplies the recording consumer. **RED mutation(s)**, applied and reverted one at a time: (a) `MessagePump.DiscardRequeuedMessagesEnabled()` (`MessagePump.cs:171-174`) returns `RequeueCount > 0` instead of `RequeueCount != -1` — the `0` and `-3` rows requeue forever, failing on "dispatch count == 1" and "`Requeue` count `0`"; (b) `Message.HandledCountReached` (`src/Paramore.Brighter/Message.cs:161-164`) uses `>` instead of `>=` — the `1` row requeues once, failing on "`Requeue` count `0`".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at `HandledCountReached` or at `MessagePump.DiscardRequeuedMessagesEnabled()`.
  - Depends on: 3.3, 4.3

- [ ] **4.11 GATE: AWS FR-23 passes on all eight configurations, both variants; move the eight ledger cells (AC-12, AC-30 row 1, R-12, R-23)**
  - In `conformance-status.md`:
    - Set FR-23 for `AWS / SnsStandard`, `SnsFifo`, `SqsStandard`, `SqsFifo` and the four `AWS.V4` twins to `Fixed (#4341)`
    - Regenerate, run the full scoped AWS and AWS.V4 conformance suites on LocalStack in both variants, and confirm every previously `Pass`/`Fixed` column still passes
    - Rewrite the "Why the eight AWS cells stay `Deferred`" paragraph (`:728`) as a dated evidence note (run date, variants, LocalStack)
    - If any cell is red, revert it and record why
  - **Output:** the eight cells read `Fixed (#4341)`, with evidence beside them.
  - Depends on: 3.5, 4.1–4.10

- [ ] **4.12 GATE: v3 and v4 outcomes are indistinguishable on the FR-23 run (AC-13, NFR-6)**
  - For each configuration pair, compare:
    - rejection reason
    - destination kind
    - the **set** of metadata keys
    - the values of `rejectionReason`, `rejectionMessage` and `originalMessageType`
    - Warning messages
    - delivery counts, each `<= R` (not compared for equality, because SQS is classified approximate)
  - Compare `originalTopic`, the destination name and `rejectionTimestamp` for presence and shape only.
  - **Output:** a v3/v4 comparison table appended to the AWS evidence note in `conformance-status.md`. Any divergence is a defect to fix before this box is ticked.
  - Depends on: 4.11

---

## Phase 5 — GCP rejection routing and IAM tolerance (#4386, #4354; ADR 0078)

- [ ] **5.1 TEST + IMPLEMENT: `GcpPubSubSubscription` offers dead-letter and invalid-message routing keys through the Brighter support interfaces**
  - **USE COMMAND**: `/test-first when a gcp subscription is constructed with dead letter and invalid message routing keys should expose them through the brighter support interfaces`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway"
  - Test file: `When_creating_gcp_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify (R-15, AC-14):
    - `GcpPubSubSubscription` and `GcpPubSubSubscription<T>`, built with `deadLetterRoutingKey` and `invalidMessageRoutingKey`, can be assigned to `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`, and both keys read back
    - Existing positional calls still compile; the existing `DeadLetter` policy is unchanged
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Append `RoutingKey? deadLetterRoutingKey = null, RoutingKey? invalidMessageRoutingKey = null` at the **end** of both ctors (`GcpPubSubSubscription.cs:113`, `:164`)
    - Implement both interfaces with `{ get; set; }`, as `SqsSubscription` does (`:47`, `:52`) (ADR 0078 step 2)

- [ ] **5.2 TEST + IMPLEMENT: The IAM status-code filter tolerates only Unimplemented, PermissionDenied and Unauthenticated, and client construction tolerates only `InvalidOperationException`**
  - **USE COMMAND**: `/test-first when an iam call fails should tolerate only unimplemented permission denied and unauthenticated and rethrow everything else`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway"
  - Test file: `When_an_iam_call_fails_should_tolerate_only_the_permitted_status_codes.cs`
  - Test should verify (R-20, NFR-5, AC-21), using `GcpIamCallTolerance` directly (no broker, no mock transport):
    - `RpcException` with `NotFound`, `InvalidArgument`, `DeadlineExceeded` or `ResourceExhausted` is rethrown unchanged, with no Warning
    - `Unimplemented`, `PermissionDenied` or `Unauthenticated` → `(false, default)` and exactly one Warning with the five elements, in the template `"{Helper} abandoned: {Rpc} on {Resource} failed with {Status}; native dead-lettering may be inactive"`
    - `TryCreateProjectsClientAsync` with a factory throwing `System.InvalidOperationException` → `null` and one Warning with `{Rpc}` `construct ProjectsClient` and `{Status}` `InvalidOperationException`
    - A factory throwing any other type propagates
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add public `GcpIamCallTolerance(ILogger? logger = null)` with `TryCallAsync<T>(IamStep, Func<Task<T>>)`, `TryCreateProjectsClientAsync(IamStep, Func<Task<ProjectsClient>>)`, `static IsTolerated(StatusCode)`, and `record IamStep(string Helper, string Rpc, string Resource)` (ADR 0078 "IAM tolerance")
    - Use an exception filter only; never `catch (Exception)`

- [ ] **5.3 TEST + IMPLEMENT: A DLQ-backed GCP channel is creatable on the emulator whether or not IAM members are configured, with exactly two Warnings**
  - **USE COMMAND**: `/test-first when creating a dlq backed gcp channel on the emulator should tolerate iam failures in both helpers and log two warnings`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_creating_a_dlq_backed_gcp_channel_on_the_emulator_should_tolerate_iam_failures.cs` (async: `…_async.cs`)
  - Test should verify (R-20, R-21, NFR-5, AC-20):
    - Members **unset**: channel creation succeeds and returns a usable channel. The `GetProjectAsync` step's outcome is tolerated in **both** `UpdateIAmRoleForDeadLetterAsync` and `UpdateIAmRoleForSubscriptionAsync`, whichever shape it takes (`Unauthenticated`/`PermissionDenied`, or `InvalidOperationException` on construction); do not hard-assert which. `GetIamPolicyAsync` is not reached, and exactly two Warnings are logged, one per helper, each with the five elements.
    - Members **set** (C-11): no Resource Manager call; `GetIamPolicyAsync`'s `Unimplemented` is tolerated once per helper; exactly two Warnings; creation succeeds
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Wrap in `GcpPubSubMessageGateway` (each `false` ends that helper with `return`):
      - `CreateProjectsClientAsync` (`:484`, `:536`) via `TryCreateProjectsClientAsync`
      - `GetProjectAsync` (`:485`, `:537`)
      - `GetIamPolicyAsync` (`:497`, `:548`)
      - `SetIamPolicyAsync` (`:518`, `:570`)
    - Wrap nothing else (ADR 0078 step 6)
    - Risk note: the members-unset `GetProjectAsync` path needs outbound network. Offline it fails `Unavailable`, which is correctly not tolerated. Record the environment used in the test's summary comment.
  - Depends on: 5.2

- [ ] **5.4 TEST + IMPLEMENT: The four GCP conformance providers use the Brighter route, keep a native policy M = 5 on a distinct `.native` topic, set both IAM members, and declare rejection-metadata keys**
  - **USE COMMAND**: `/test-first when reading gcp conformance providers should route rejections through brighter with budget three below a native limit of five and iam members set`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway"
  - Test file: `When_reading_gcp_conformance_providers_should_hold_budget_below_native_limit_and_set_iam_members.cs`
  - Test should verify (R-27(a)(b), AC-36 first two clauses; ADR 0078 step 5), for each of `GcpPull`, `GcpPullOrdering`, `GcpStream` and `GcpStreamOrdering` `MessageGatewayProvider`:
    - A DLQ-backed subscription has `RequeueCount == 3`, `DeadLetterRoutingKey` set, and `DeadLetter.MaxDeliveryAttempts == 5` on topic **and** subscription `{deadLetterRoutingKey}.native`
    - `DeadLetter.PublisherMember` and `SubscriberMember` are non-empty `serviceAccount:…` strings
    - `RejectionMetadataKeys.StampsRejectionMetadata` is true, with keys equal to `RejectionMetadataKeyNames`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should, in the four provider files (for example `GcpPullMessageGatewayProvider.cs:136-164`):
    - Pass the routing keys to the subscription instead of mapping `deadLetterRoutingKey` onto `DeadLetterPolicy`
    - Keep `DeadLetterPolicy { MaxDeliveryAttempts = 5 }` on `{deadLetterRoutingKey}.native`, and set `requeueCount: 3` (was 5)
    - Set both members
    - Pre-provision each destination topic **with a reading subscription**
    - Make `GetMessageFromDeadLetterQueue(Async)` read `{deadLetterRoutingKey}`; the native read stays on `DeadLetter.Subscription`
    - Implement `GetMessageFromInvalidChannelAsync`
    - Fill `RejectionMetadataKeys` (empty today, `:363-370`)
    - The generated tests stay skipped until 5.11, so no regeneration is needed yet
  - Depends on: 5.1, 5.3

- [ ] **5.5a TEST + IMPLEMENT: A GCP pull Reject for a delivery error publishes a stamped copy to the DLQ, acknowledges the original and returns true**
  - **USE COMMAND**: `/test-first when a gcp pull consumer rejects a message for a delivery error should publish a stamped copy to the dlq and acknowledge the original`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_pull_consumer_rejects_for_delivery_error_should_publish_stamped_copy_to_dlq.cs` (async: `…_async.cs`)
  - Test should verify (R-16, R-18, AC-15; NFR-8), on `GCP / Pull` and `GCP / PullOrdering` with `<topic>.DLQ`, through a consumer built by `GcpPubSubConsumerFactory`. The router is internal, and `testing.md` forbids `InternalsVisibleTo`, so it is observed only through the consumer's public `Reject`:
    - `DeliveryError` → `.DLQ`; `None` with no description → `.DLQ`
    - Each copy carries `originalTopic`, `originalMessageType`, `rejectionReason` (`"None"` for a null or `None` reason) and an ISO `rejectionTimestamp`
    - `rejectionMessage` is present only when a non-empty description was given, so the `None` copy has four keys
    - No copy has a `ReceiptHandle` key
    - A later source read returns `MT_NONE`, and `Reject` returns `true`
    - **Missing receipt handle (ADR 0078 "Missing receipt handle (both consumers)"):** a `Message` built by the test with no `ReceiptHandle` bag entry, with a DLQ key configured: the `.DLQ` copy is still published, an Error naming the message id says the original cannot be settled, and `Reject` returns `true`. RED today: `Reject` returns `false` before routing (`:278-281`, `RejectAsync` `:308-311`)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add internal `GcpRejectionRouter` (`Route`/`RouteAsync` → `RoutingOutcome { NoDestination, Routed, Failed }`). It never throws, and it stamps metadata with `RejectionMetadataKeyNames` (0077's constraint on 0078), removes `ReceiptHandle`, and sets `Header.Topic` to the destination.
    - Create the producer lazily via `GcpPubSubMessageProducerFactory.Create/CreateAsync`, with `MakeChannels` = the subscription's, `ProjectId` = the subscription's, and `EnableMessageOrdering = true`
    - Add the routing-key and `makeChannels` ctor parameters to `GcpPullMessageConsumer`, and wire them in `GcpPubSubConsumerFactory.CreateAsync` (`:83`). The consumer builds and disposes the router (ADR 0078 step 3).
    - Compose `Reject` (`:276`) and `RejectAsync` (`:306`): copy the handle first → route → `AckByHandle` on `Routed`/`NoDestination` → return `true`. Sync calls sync and async calls async, with no `BrighterAsyncContext.Run` nesting.
    - Replace the missing-handle early `return false` in `Reject` (`:278-281`) and `RejectAsync` (`:308-311`) with: route → log an Error that the original cannot be settled → return `true`
    - In this task, the router only needs the dead-letter destination; 5.5b adds selection by reason
  - Depends on: 1.5, 5.1, 5.4

- [ ] **5.5b TEST + IMPLEMENT: A GCP pull Reject routes Unacceptable to the invalid-message channel, falling back to the DLQ, and reports no destination when neither is configured**
  - **USE COMMAND**: `/test-first when a gcp pull consumer rejects an unacceptable message should route to the invalid message channel falling back to the dlq`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_pull_consumer_rejects_unacceptable_should_route_to_invalid_channel_or_dlq.cs` (async: `…_async.cs`)
  - Test should verify (R-16, R-18, AC-15, AC-16; NFR-8), on `GCP / Pull` and `GCP / PullOrdering` with `<topic>.DLQ` and `<topic>.Invalid`, through the consumer as in 5.5a:
    - `Unacceptable` → `.Invalid`, carrying the rejection metadata as in 5.5a
    - With a DLQ key and **no** invalid key, `Unacceptable` → `.DLQ` (AC-16)
    - With neither key, nothing is published, a later source read returns `MT_NONE`, and `Reject` returns `true` (the `NoDestination` outcome; 5.7 adds its Warning)
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Select the destination by reason in `GcpRejectionRouter`: `Unacceptable` → invalid key, falling back to the DLQ key; `DeliveryError`/`None` → DLQ key; neither → `RoutingOutcome.NoDestination`
    - RED comes from the `Unacceptable` → `.Invalid` clause; the DLQ-fallback and no-destination clauses may already be green, because 5.5a routes every reason to the DLQ key and already returns `NoDestination` when it is absent
  - Depends on: 5.5a

- [ ] **5.5c MEASURE: Does `SubscriberClient` inject or overwrite `googclient_deliveryattempt`? (ADR 0077 Risks, "unverified library behaviours")**
  - On the emulator with a DLQ-backed stream subscription:
    - (i) record whether a received `PubsubMessage` carries attribute `googclient_deliveryattempt`, and whether `GetDeliveryAttempt` matches it
    - (ii) publish a message that already carries a stale `googclient_deliveryattempt` attribute (as a routed copy would) and record first whether the emulator **accepts** that publish and, if it does, whether `SubscriberClient` overwrites the attribute or keeps the stale value
  - Use a measurement fixture committed as `[Fact(Skip = "measurement — ADR 0077 risk")]` in `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream`.
  - **Output:** a dated amendment note in `docs/adr/0077-delivery-count-contract.md` (Risks) recording (i), whether (ii)'s publish was accepted, and (ii)'s overwrite result. If (ii) shows a stale value would be read, flag it for 6.11.
  - Runs before 5.5d and 5.6: stream routing re-publishes every non-ignored bag entry (`Parser.cs:350-356`), and 5.4 keeps a `DeadLetterPolicy` on the very subscriptions `SubscriberClient` injects on, so the answer decides whether 5.6 can route at all (ADR 0078:38).
  - Depends on: 5.3, 5.4

- [ ] **5.5d TEST + IMPLEMENT: The GCP parser never admits `googclient_deliveryattempt` into `Header.Bag`, so a routed copy cannot re-publish it**
  - **USE COMMAND**: `/test-first when a gcp message carrying googclient deliveryattempt is received should not copy it into the header bag`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_message_carries_delivery_attempt_attribute_should_not_copy_it_into_bag.cs` (async: `…_async.cs`)
  - Test should verify (R-28; ADR 0077 "Where each transport reads its counter", GCP stream row):
    - A message received through the stream and the pull consumer has no `googclient_deliveryattempt` key in `Header.Bag`
    - **Given, chosen by 5.5c's outcome:** (a) if 5.5c(ii) shows the emulator accepts a published `googclient_deliveryattempt` attribute, publish it explicitly and assert on both consumers — RED today on both; (b) otherwise, if 5.5c(i) shows `SubscriberClient` injects the attribute, use a DLQ-backed stream subscription and rely on that injection — RED on the stream clause. The pull clause does not apply under (b): the pull path has no `SubscriberClient` to inject the attribute and nothing publishes it, so the stream clause (and 5.6's routed-copy clause) evidence the single `s_ignoreHeaders` entry both parsers share; (c) if 5.5c shows neither injection nor an accepted publish, the attribute cannot reach `Header.Bag` on the emulator — record that in 0077's Risks note, drop the receive clauses here and 5.6's routed-copy clause (no test file is committed for this task), and add the ignore entry as a defensive change justified by the real-service `SubscriberClient` behaviour; the gate then reviews the ADR note instead of a RED test. Record which Given was used in the test's comment (or the ADR note, under (c))
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `"googclient_deliveryattempt"` to `Parser.s_ignoreHeaders` (`Parser.cs:12`). The router in 0078 must not reintroduce it. This lands before 5.6 so stream routing never re-publishes the attribute (ADR 0078:38); 5.6 asserts the routed copy.
  - Depends on: 5.4, 5.5c

- [ ] **5.6 TEST + IMPLEMENT: A GCP stream Reject routes by reason with rejection metadata, then accepts the original**
  - **USE COMMAND**: `/test-first when a gcp stream consumer rejects a message should route by reason with rejection metadata and accept the original`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_a_gcp_stream_consumer_rejects_should_route_by_reason_with_metadata.cs` (async: `…_async.cs`)
  - Test should verify (R-16, R-18, AC-15, AC-16; NFR-8), on `GCP / Stream` and `GCP / StreamOrdering`:
    - A copy routed from a DLQ-backed stream subscription carries no `googclient_deliveryattempt` on the destination (ADR 0078:38; 0077's "0078 must not reintroduce it"). Assert on the raw `PubsubMessage.Attributes` of the copy, fetched with a raw `SubscriberServiceApiClient.Pull` on the destination's reading subscription, **not** through `Parser` or a Brighter channel: after 5.5d, `Parser` strips the key on read, so a channel read could not catch the router stamping it (a raw pull does not inject it, because `delivery_attempt` is a separate field). Not applicable under 5.5c outcome (c), nor under (a) when 5.5c(i) recorded no injection
    - The same clauses as 5.5a and 5.5b, including 5.5a's missing-handle clause: a message with no `GcpStreamMessage` handle still routes to the configured destination, logs an Error and returns `true`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - `RejectAsync` routes through `RouteAsync` rather than `Task.FromResult(Reject(...))` (`GcpPubSubStreamMessageConsumer.cs:106` today). This is not observable through the public API — the router is internal and `InternalsVisibleTo` is forbidden — so it is checked at code review and by 8.2's broker-call review, not asserted
    - Add the ctor parameters to `GcpPubSubStreamMessageConsumer`, wired at `GcpPubSubConsumerFactory.cs:99`, and have it dispose the router
    - Compose `Reject` (`:84`) and `RejectAsync` (`:104-106`): copy the `GcpStreamMessage` handle → route → `handle.Accepted()` on `Routed`/`NoDestination`. A missing handle still routes, logs an Error and returns `true` (asserted by the missing-handle clause above).
  - Depends on: 5.5b, 5.5d

- [ ] **5.7 TEST + IMPLEMENT: A GCP Reject with no destination configured acknowledges the message and logs a Warning naming the message id and reason**
  - **USE COMMAND**: `/test-first when a gcp consumer rejects with no destination configured should acknowledge and log a warning naming the message id and reason`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_a_gcp_consumer_rejects_with_no_destination_should_acknowledge_and_log_warning.cs` (async: `…_async.cs`)
  - Test should verify (R-17, AC-17), for both consumers and both variants:
    - A subscription with neither key, and `Reject(new MessageRejectionReason(Unacceptable, "bad payload"))`
    - One Warning containing the message id and `"Unacceptable"`
    - The next source read returns `MT_NONE`; `Reject` returns `true`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Have `GcpRejectionRouter` log a source-generated Warning naming the message id and reason when it returns `NoDestination`. Nothing else: 5.5a already returns `NoDestination`, and 5.5b/5.6 already ack/accept on it.
    - The test's RED comes from the Warning assertion; the ack, `MT_NONE` and `true` clauses are already green from 5.5b/5.6.
  - Depends on: 5.5b, 5.6

- [ ] **5.8 TEST + IMPLEMENT: A failed GCP routing publish releases the original for prompt redelivery, logs an Error, returns true, and leaves the missing topic uncreated**
  - **USE COMMAND**: `/test-first when a gcp rejection routing publish fails should release the original for redelivery log an error and return true`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_a_gcp_rejection_routing_publish_fails_should_release_original_for_redelivery.cs` (async: `…_async.cs`)
  - Test should verify (R-19, AC-18), for both consumers and both variants:
    - The Given uses two subscriptions (ADR 0078 "The destination producer's `makeChannels`"):
      - **Provisioning:** `makeChannels: Create`, `SubscriptionMode.Pull`, carrying `AckDeadlineSeconds: 60` and ordering attributes
      - **Under test:** the configuration's own `SubscriptionMode`, `makeChannels: Assume`, `requeueDelay` zero, and a `deadLetterRoutingKey` naming a non-existent topic distinct from any policy topic
    - `Reject(DeliveryError)` logs an Error naming the message id and `"DeliveryError"`, and returns `true`
    - The message is redelivered **within W = 10 s** (so it was released, not left outstanding), and the topic still does not exist
    - **Risk check (ADR 0078 Risks):** the sync `Reject` returns within W. This verifies the emulator fails promptly on a missing topic and that `GcpMessageProducer.Dispose` (sync-over-async, `:141-143`) returns promptly after a failed publish. If it does not, amend ADR 0078 with a dated note.
    - **Creation failure (ADR 0078 "Divergence from SQS"):** a second row with the subscription under test at `makeChannels: Validate`. The destination producer's creation throws on the missing topic, and the router reports `Failed`, not `NoDestination`, so the same assertions hold: an Error, redelivery within W, `true`, and the topic still absent. The `Validate` row uses a destination name unique to that row and variant (a fresh Guid) and issues exactly one `Reject` before asserting, because `EnsureTopicExistAsync` caches the name before `Validate` checks it (`GcpPubSubMessageGateway.cs:44-60`; ADR 0078 Risks), so a repeated name would exercise the publish failure instead
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - On `Failed`: pull → `ReleaseByHandle` (`ModifyAckDeadline(…, 0)`); stream → `handle.Reject()` (Nack)
    - Discard and dispose the cached producer inside the guarded region (`Dispose` for sync, `DisposeAsync` for async)
    - Put the two-subscription Given in a reusable test helper; 6.30 uses it too
  - Depends on: 5.5b, 5.6

- [ ] **5.9 TEST + IMPLEMENT: After a failed routing publish, a later GCP Reject rebuilds the producer and routes once the destination exists (only success is cached)**
  - **USE COMMAND**: `/test-first when a gcp rejection routing publish failed earlier should rebuild the producer and route once the destination exists`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_a_gcp_rejection_routing_failed_earlier_should_rebuild_producer_and_route.cs` (async: `…_async.cs`)
  - Test should verify (ADR 0078 "Only success is cached"; Risk "ordering key paused after failed publish"), on `PullOrdering` and `StreamOrdering` with a partition key:
    - The first `Reject` fails (topic absent, `Assume`)
    - The test creates the topic and a reading subscription
    - The redelivered message's `Reject` reaches the destination with metadata, and the original is acknowledged
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Ensure the router never caches a failed or null producer (unlike SQS's `Lazy<T>`, `SqsMessageConsumer.cs:104`), and that a fresh `PublisherClient` clears an ordering-key pause
  - Depends on: 5.8

- [ ] **5.10 TEST + IMPLEMENT: A failed ack or release RPC during GCP Reject is logged at Error and Reject still returns true (fault-interceptor evidence, R-16/R-17/R-19)**
  - **USE COMMAND**: `/test-first when a gcp pull settle call fails during reject should log an error return true and leave the message redeliverable`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_pull_settle_call_fails_should_return_true_and_leave_message_redeliverable.cs` (async: `…_async.cs`), on `GCP / Pull` and `GCP / PullOrdering`, `AckDeadlineSeconds: 10`. These are test-only fault-injection tests for the failures a broker cannot produce on demand (ADR 0078 step 8; R-16/R-17 failed ack, R-19 failed release). Write them and observe them RED **before** adding the settle-failure handling.
  - Test should verify (R-16, R-17, R-19; ADR 0078 step 8):
    - **Wiring:**
      - Use a `Grpc.Core.Interceptors.Interceptor` that overrides `BlockingUnaryCall`/`AsyncUnaryCall` and, when armed, throws `RpcException(StatusCode.FailedPrecondition)` (never `Unavailable`) for `/google.pubsub.v1.Subscriber/Acknowledge` or `/…/ModifyAckDeadline`
      - Supply it via `GcpMessagingGatewayConnection.SubscriptionManagerConfiguration` (`:40`) with `builder.Credential = null`, `EmulatorDetection` left at `None`, and `builder.CallInvoker = GrpcChannel.ForAddress("http://" + PUBSUB_EMULATOR_HOST, {Credentials = Insecure}).Intercept(faults)`
    - **Scenarios:**
      - (a) destination exists, `Acknowledge` armed
      - (b) no destination, `Acknowledge` armed
      - (c) AC-18 Given, `ModifyAckDeadline` armed
    - **Assert in each scenario:**
      - `Reject` returns `true` and does not throw
      - An Error names the message id
      - After disarming, the message is redelivered once its deadline lapses
      - In (a), the destination holds the copy
      - **The interceptor's fired-counter is > 0**. This guards against the invoker being silently dropped, and confirms at runtime that `Validate` accepts the invoker (ADR 0078 Risks).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the settle-failure handling to the pull `Reject`/`RejectAsync` composed in 5.5a/5.8: a failed `AckByHandle`/`ReleaseByHandle` RPC is caught, logs a source-generated Error naming the message id, and `Reject` still returns `true` (nothing escapes)
    - Record the evidence: a dated "Evidence" entry in `docs/adr/0078-gcp-rejection-routing-and-dlq-channel-creation.md` naming the test files and results, and the checklist item "R-16/R-17/R-19 failure evidence produced" ticked in `specs/0037-delivery-count-and-rejection-routing/README.md`. These are evidence, not ACs.
  - Depends on: 5.8

- [ ] **5.11 GATE: Move the twenty GCP rejection-routing cells (FR-4, FR-5, FR-6, FR-8, FR-17 × 4 configurations; AC-30 row 2, AC-15/16/17 across all four configurations)**
  - In `conformance-status.md`, set those 20 cells to `Fixed (#4386)`, regenerate, and run the scoped GCP suite on a clean emulator (`docker-compose -f docker-compose-gcp.yaml down -v; up -d`), both variants.
  - Revert any red cell and record why.
  - The double run for AC-22 happens in 6.31.
  - **Output:** 20 cells moved, with a dated evidence note in the GCP paragraph (`:475`).
  - Depends on: 5.4–5.9

---

## Phase 6 — GCP delivery count (R-13; ADR 0077 step 5, AC-39 branch)

### Phase 6 common — before the measurement

- [ ] **6.1 TEST + IMPLEMENT: `GcpPubSubSubscription` reports its `DeadLetterPolicy` limit as the native limit, and reports the budget unenforceable when it has no policy**
  - **USE COMMAND**: `/test-first when a gcp subscription has no dead letter policy should report its delivery budget unenforceable`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway"
  - Test file: `When_a_gcp_subscription_has_no_dead_letter_policy_should_report_budget_unenforceable.cs`
  - Second test: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway", file `When_validating_a_gcp_subscription_without_dead_letter_policy_should_report_one_warning.cs`. Placement checked: `Gcp.Tests` does not reference `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` (so `AddConsumers` is unreachable), and `Extensions.Tests` does not reference the GCP package, which it must not gain (R-24's no-new-reference stance, as for log capture). `Gcp.Tests` does reach `Paramore.Brighter` and `Paramore.Brighter.ServiceActivator`, so the test runs the real `PipelineValidator` that `.ValidatePipelines()` builds (`BrighterPipelineValidationExtensions.cs:71-94`) over the three `ConsumerValidationRules` budget specifications, following `tests/Paramore.Brighter.Core.Tests/Validation/When_validator_finds_errors_across_paths_should_aggregate_all.cs`. 2.5(ii) already covers the `AddConsumers` → `.ValidatePipelines()` wiring.
  - Test should verify (R-10, R-11, A-1, AC-11; AC-10 and AC-11 transport shape):
    - `GcpPubSubSubscription` is `IAmADeliveryCountingSubscription`
    - `NativeRedriveLimit == DeadLetter?.MaxDeliveryAttempts`
    - `DeliveryBudgetUnenforceableReason` is non-null, naming the missing `DeadLetterPolicy`, when `DeadLetter == null`
    - (second test; AC-11 first clause) A real `GcpPubSubSubscription` with `DeadLetter == null` and `requeueCount: 3`, validated → exactly one `ValidationSeverity.Warning` finding naming the subscription, `3` and the reason
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement the interface on `GcpPubSubSubscription` (`GcpPubSubSubscription.cs:75`, `DeadLetterPolicy.cs:47`). The AC-40 branch (6.20) widens the reason.
  - Depends on: 2.5, 5.1

- [ ] **6.2 TEST + IMPLEMENT: Creating a GCP channel whose budget is unenforceable logs exactly one Warning, and receiving never logs another**
  - **USE COMMAND**: `/test-first when a gcp channel is created for an unenforceable budget should log exactly one warning and none on the receive path`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_channel_is_created_for_an_unenforceable_budget_should_log_one_warning.cs` (async: `…_async.cs`)
  - Test should verify (R-11, R-26, NFR-4, AC-11 second clause, AC-29):
    - A subscription with no `DeadLetterPolicy` and `requeueCount: 3`
    - `CreateSyncChannel`, and separately `CreateAsyncChannel` (which delegates), each log exactly one Warning naming the subscription, `3` and the reason, not two
    - After 100 received messages, and again after another 100, the count of budget Warnings is unchanged
    - Negative cases, each with a `DeadLetterPolicy` so R-11's predicate is negative: an R-7 subscription (`requeueCount: 0`) and an R-10 subscription (budget at or above the visible native limit, e.g. `requeueCount: 5` with M = 5). Each logs **zero** budget Warnings at channel creation, and zero over the 2 × 100 receives. Compute the expectation from `DeliveryBudgetUnenforceableReason` so the test holds on both branches.
    - AC-29: for a channel whose budget will not behave as intended (R-7, R-10 or R-11), no budget Warning is logged on the receive path at all; the channel-creation count is exactly one if R-11's condition holds and zero otherwise, and it is unchanged after a further 100 messages
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Call `DeliveryBudgetDiagnostics.WarnIfUnenforceable` in `GcpPubSubChannelFactory.CreateSyncChannel` (`:26`) and `CreateAsyncChannelAsync` (`:65`) only, not `CreateAsyncChannel` (`:54-55`)
  - Depends on: 2.6, 6.1

> **6.3 and 6.4 moved** to Phase 5 as **5.5c** and **5.5d** (tasks review round 4): 5.6's stream routing needs the `googclient_deliveryattempt` ignore entry first. The ids 6.3 and 6.4 are not reused.

- [ ] **6.5 CHARACTERISE: GCP budget of -1 never rejects, on both consumers**
  - **USE COMMAND**: `/test-first when gcp budget is minus one should never reject and never dead letter`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_gcp_budget_is_minus_one_should_never_reject.cs` (async: `…_async.cs`)
  - Test should verify (R-6, AC-5, the `-1` clause of AC-35; unguarded, so true on both branches):
    - `requeueCount: -1`, **no** `DeadLetterPolicy`, and a Brighter DLQ key, pumped for 60 s
    - Dispatch count `> 3`, no `DeliveryError`, and a DLQ read returns `MT_NONE`
  - 🔁 **Characterisation — expected green on first run:** the pump's `-1` guard (`MessagePump.cs:171`) is transport-neutral and unguarded, so it holds on both branches and both consumers. **RED mutation(s)**, applied and reverted one at a time: (a) `MessagePump.DiscardRequeuedMessagesEnabled()` (`src/Paramore.Brighter.ServiceActivator/MessagePump.cs:171-174`) returns `true`, treating `-1` as an enabled budget, so `HandledCountReached(-1)` (`Message.cs:161`) rejects on the first deferral — fails on "dispatch count `> 3`" (and the DLQ read is no longer `MT_NONE`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - This test guards R-6; a red points at `MessagePump.DiscardRequeuedMessagesEnabled()`.
  - Depends on: 3.3, 5.6

- [ ] **6.6 CHARACTERISE: GCP budget of 1, 0 or below -1 rejects on the first deferral without requeuing, on both consumers**
  - **USE COMMAND**: `/test-first when gcp budget is one zero or below minus one should reject on first deferral without requeue`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_gcp_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral.cs` (async: `…_async.cs`)
  - Test should verify (R-7, AC-6, AC-35; unguarded), as a `[Theory]` over `1`, `0`, `-3`:
    - Dispatch count == 1 and recording-consumer `Requeue` count == 0
    - A `DeliveryError` rejection is issued, and the message is on the Brighter DLQ
  - 🔁 **Characterisation — expected green on first run:** `HandledCountReached` already holds on the first deferral (`Reactor.cs:494-498`) and Phase 5 routing (5.5b, 5.6) already delivers the DLQ copy. **RED mutation(s)**, applied and reverted one at a time: (a) `MessagePump.DiscardRequeuedMessagesEnabled()` (`MessagePump.cs:171-174`) returns `RequeueCount > 0` instead of `RequeueCount != -1` — the `0` and `-3` rows requeue, failing on "dispatch count == 1" and "`Requeue` count == 0"; (b) `Message.HandledCountReached` (`src/Paramore.Brighter/Message.cs:161-164`) uses `>` instead of `>=` — the `1` row requeues, failing on "`Requeue` count == 0".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at Phase 5 routing (5.5b, 5.6) or at the pump guard.
  - Depends on: 3.3, 5.6

- [ ] **6.7 MEASURE: AC-39 — the emulator's delivery counter over three deliveries on a DLQ-backed subscription (R-13, A-2)**
  - On a clean emulator, use a DLQ-backed subscription (`DeadLetterPolicy` M = 5, creatable via 5.3) and a deferring handler. Record the raw `ReceivedMessage.DeliveryAttempt` (pull) and `GetDeliveryAttempt` (stream) on each of three deliveries. Use a measurement fixture committed as `[Fact(Skip = "measurement — AC-39")]` in `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull`.
  - **Output (all four exit criteria required):**
    - (a) the observed values written into the GCP paragraph of `conformance-status.md`
    - (b)/(c) a dated *Measurement outcome* amendment in `docs/adr/0077-delivery-count-contract.md` under "R-13 (GCP): the branch rule", stating whether A-2 held and whether the broker-counter mechanism satisfies R-1 to R-5 within NFR-1 to NFR-3. It must be populated, strictly increasing, and start at `1`. Otherwise the pre-recorded independent search concludes "none fits".
    - (d) the claimed branch (AC-19 or AC-40) stated in the same note, with the other marked not applicable, citing (c) and this measurement
  - **Then:** do exactly one of the two sections below, and mark the other's tasks `[!] not taken — AC-39 selected AC-nn`.
  - Depends on: 5.3, 5.4

### GCP — AC-19 branch (the ADR records a satisfying mechanism)

> Do this section only if 6.7 claimed AC-19. Otherwise mark every task `[!] not taken — AC-39 selected AC-40`.

- [ ] **6.10 TEST + IMPLEMENT: The GCP pull consumer presents a strictly increasing delivery count across redeliveries, starting at 0**
  - **USE COMMAND**: `/test-first when a gcp pull message is redelivered should present a strictly greater delivery count starting at zero`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_pull_message_is_redelivered_should_present_increasing_delivery_count.cs` (async: `…_async.cs`)
  - Test should verify (R-1, R-2, R-3, AC-1, AC-2), on `Pull` and `PullOrdering`:
    - `requeueCount: -1`, `DeadLetterPolicy` M = 5, the deferring pump, quitting when the dispatch count reaches 3
    - The recording consumer's sequence is strictly increasing, and the first value is `0`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `Parser.ToBrighterMessage(ReceivedMessage)` (`~:84`; `ReadHandleCount` `:167`), after the bag is filled from attributes, set `HandledCount = DeliveryCount.Resolve(headerCount, receivedMessage.DeliveryAttempt, bag)` (`0` → header count)
    - No allocation and no RPC (NFR-1, NFR-2)
  - Depends on: 2.2, 3.1, 3.3, 5.5d, 6.7

- [ ] **6.11 TEST + IMPLEMENT: The GCP stream consumer presents a strictly increasing delivery count across redeliveries, starting at 0**
  - **USE COMMAND**: `/test-first when a gcp stream message is redelivered should present a strictly greater delivery count starting at zero`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_a_gcp_stream_message_is_redelivered_should_present_increasing_delivery_count.cs` (async: `…_async.cs`)
  - Test should verify (R-1, R-2, R-3, AC-1, AC-2), on `Stream` and `StreamOrdering`:
    - The same clauses as 6.10
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `Parser.ToBrighterMessage(GcpStreamMessage)` (`:33`), read `PubsubExtensions.GetDeliveryAttempt(message)` (`int?`) and apply `Resolve` after the bag is filled
    - If 5.5c(ii) showed a stale attribute survives, record the effect in ADR 0077 before proceeding
  - Depends on: 6.10

- [ ] **6.12 CHARACTERISE: A GCP pull message whose ack deadline lapses presents a higher count on the expiry redelivery, with no pump and no Requeue**
  - **USE COMMAND**: `/test-first when a gcp pull ack deadline lapses without ack or requeue should present a greater delivery count on redelivery`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_pull_ack_deadline_lapses_should_present_greater_delivery_count.cs` (async: `…_async.cs`)
  - Test should verify (R-1, AC-42), on `Pull` and `PullOrdering`:
    - Subscription `AckDeadlineSeconds: 10` and `DeadLetterPolicy` M = 5, published with `HandledCount = 0`
    - `Receive` m1, hold it, wait for the lapse, and `Receive` m2 with a count greater than m1's
    - Separately through `ReceiveAsync`
  - 🔁 **Characterisation — expected green on first run:** 6.10 already resolves `ReceivedMessage.DeliveryAttempt` in the pull parser, and `GcpPullMessageConsumer` does a raw `Pull` and never extends the deadline. **RED mutation(s)**, applied and reverted one at a time: (a) in `Parser.ToBrighterMessage(ReceivedMessage)` (`src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:84`), pass `null` instead of `receivedMessage.DeliveryAttempt` to `DeliveryCount.Resolve`, reverting 6.10 — both deliveries present the header count `0`, so it fails on "m2's count greater than m1's".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at 6.10's parser change or at a deadline extension in `GcpPullMessageConsumer`.
  - Depends on: 6.10

- [ ] **6.13 MEASURE: Does `bufferSize: 2` admit a stream redelivery while the first delivery is held? (ADR 0077 Risks; stream procedure prerequisite)**
  - On the emulator:
    - Configure `bufferSize: 2`, `noOfPerformers: 1`, and `StreamingConfiguration` with `MaxTotalAckExtension = 10 s`
    - Hold m1 and poll `Receive` every 500 ms for 45 s
    - Record whether the redelivery arrives on `Stream`, and on `StreamOrdering` with and without an ordering key
  - Use a measurement fixture committed as `[Fact(Skip = …)]` in `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream`.
  - **Output:** a dated amendment note in ADR 0077 under "GCP stream consumer lease-lapse procedure". Record the outcome, and whether `StreamOrdering` needs the keyless alternative test.
  - Depends on: 6.11

- [ ] **6.14 CHARACTERISE: A GCP stream message whose lease lapses presents a higher count on the expiry redelivery (ADR 0077 stream procedure)**
  - **USE COMMAND**: `/test-first when a gcp stream lease lapses without ack or requeue should present a greater delivery count on redelivery`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_a_gcp_stream_lease_lapses_should_present_greater_delivery_count.cs` (async: `…_async.cs`)
  - Test should verify (R-1, R-13 stream input, AC-42), on `Stream` and `StreamOrdering`:
    - Configuration: `DeadLetterPolicy` M = 5, `AckDeadlineSeconds = 10`, `bufferSize: 2`, `noOfPerformers: 1`, `MaxTotalAckExtension = 10 s`
    - Sequence:
      1. Publish with `HandledCount = 0`
      2. Receive m1 and record its count; do not settle it
      3. Poll every 500 ms, up to 45 s
      4. m2's count is greater than m1's
      5. Then `Acknowledge` m2 and m1, so the `WaitForProcessing` shutdown completes
    - On `StreamOrdering`, if 6.13 recorded that ordering blocks the primary procedure, use the keyless alternative on the same ordering-enabled subscription, and record the switch in the ledger
  - 🔁 **Characterisation — expected green on first run:** 6.11 already reads `GetDeliveryAttempt` in the stream parser, and the `StreamingConfiguration` hook survives because it runs before `builder.Settings ??= …` (`GcpPubSubConsumerFactory.cs:110-121`). **RED mutation(s)**, applied and reverted one at a time: (a) in `Parser.ToBrighterMessage(GcpStreamMessage)` (`src/Paramore.Brighter.MessagingGateway.GcpPubSub/Parser.cs:33`), pass `null` instead of `PubsubExtensions.GetDeliveryAttempt(...)` to `DeliveryCount.Resolve`, reverting 6.11 — fails on "m2's count is greater than m1's".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at 6.11's parser change or at the `StreamingConfiguration` hook (`GcpPubSubConsumerFactory.cs:110-121`).
  - Depends on: 6.13

- [ ] **6.15 CHARACTERISE: The GCP dead-letter copy of a budget rejection, read through a real channel, carries the stamped count and full rejection metadata**
  - **USE COMMAND**: `/test-first when the gcp budget is exhausted the dead letter copy read through a channel should present the stamped handled count and rejection metadata`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_gcp_budget_is_exhausted_dead_letter_copy_should_keep_stamped_count_and_metadata.cs` (async: `…_async.cs`)
  - Test should verify (R-5, R-28, AC-4, AC-41), for both consumers:
    - `requeueCount: 3` and `DeadLetterPolicy` M = 5 on `.native`
    - The Brighter DLQ is read via a `ChannelFactory` channel over a reading subscription that **carries its own `DeadLetterPolicy`** (to a further topic), so the DLQ's own delivery counter is populated (A-1) and the read genuinely exercises R-28's "not the destination's own counter" half, as SQS (4.6) and RocketMQ (7.12) do. Without that policy Pub/Sub leaves the counter unset and `Resolve` falls back to the header count, so no mutation of the discriminator could fail the test
    - The policy-carrying reading subscription (and its further policy topic) is created **before** the message is published and pumped, because Pub/Sub delivers only messages published after a subscription exists; its channel creation logs the two tolerated IAM Warnings (5.3)
    - `HandledCount >= 3` and not `0`
    - The five AC-4 keys are present and valid
  - 🔁 **Characterisation — expected green on first run:** the publish-side `HandledCount` attribute (`Parser.cs:307`), 5.5a's router stamping and `Resolve`'s discriminator (2.2, applied by 6.10/6.11) already deliver it. **RED mutation(s)**, applied and reverted one at a time: (a) `DeliveryCount.Resolve` (2.2) ignores the `rejectionReason` discriminator and returns the normalised broker count whenever one is present — the DLQ copy presents `0`, failing on "`HandledCount >= 3` and not `0`"; (b) `GcpRejectionRouter` (5.5a) skips the `RejectionMetadataKeyNames` stamping — failing on "the five AC-4 keys are present and valid".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at `Parser.cs:307` (`HandledCount` attribute), the router's stamping (5.5a), or `Resolve`'s discriminator.
  - Depends on: 5.4, 6.10, 6.11

- [ ] **6.16 GATE: GCP FR-23 passes on all four configurations, both variants; move the four ledger cells (AC-19, AC-3 on GCP, AC-30 row 3, NFR-7)**
  - Set the four `GCP / *` FR-23 cells to `Fixed (#4386)`, regenerate, and run FR-23 on a clean emulator in both variants.
  - Handler invoked at most 3 times. The message reaches the Brighter DLQ carrying `rejectionReason == "DeliveryError"` inside 60 s.
  - **Output:** four cells moved, with a dated evidence note in the GCP paragraph. Mark 6.20–6.21 `[!] not taken — AC-39 selected AC-19`.
  - Depends on: 6.10–6.15

### GCP — AC-40 branch (the ADR records that no GCP mechanism satisfies)

> Do this section only if 6.7 claimed AC-40. Otherwise mark every task `[!] not taken — AC-39 selected AC-19`.

- [ ] **6.20 TEST + IMPLEMENT: Every GCP subscription with a budget reports it unenforceable, logs the blocker at channel creation, and still offers Brighter rejection routing**
  - **USE COMMAND**: `/test-first when a gcp channel is created with a budget and no gcp mechanism advances the count should warn naming the blocker and keep rejection routing`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull"
  - Test file: `When_a_gcp_channel_is_created_with_a_budget_should_warn_that_the_count_cannot_advance.cs` (async: `…_async.cs`)
  - Test should verify (R-11, R-13(a)–(c), AC-40 behavioural clauses):
    - `requeueCount: 3` **with** a `DeadLetterPolicy` → one Warning naming the subscription and the emulator/`delivery_attempt` blocker
    - `GcpPubSubSubscription` still implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Make `DeliveryBudgetUnenforceableReason` return fixed text naming the blocker for every subscription. The rules already exclude `R == -1`.
    - Leave the parser receive path unchanged apart from 5.5d
  - Depends on: 6.1, 6.2, 6.7

- [ ] **6.21 GATE: Re-point the four GCP FR-23 cells at the emulator limitation (AC-40 ledger clauses, AC-30 row 3)**
  - Change the four `GCP / *` FR-23 cells from `Deferred -> #4240` to `Deferred` pointing at the emulator limitation, citing 6.7's measurement, which is already in the GCP paragraph.
  - **Output:** four cells re-pointed. The 20 routing cells from 5.11 are unaffected.
  - Depends on: 6.20

### Phase 6 common — after the branch

- [ ] **6.30 CHARACTERISE: When GCP routing fails, the release loop outlives the rejection until the native cap forwards the message without rejection metadata**
  - **USE COMMAND**: `/test-first when gcp rejection routing keeps failing should keep releasing until the native dead letter policy forwards the message without rejection metadata`
  - Test location: "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull" and "tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream"
  - Test file: `When_gcp_rejection_routing_keeps_failing_should_release_until_native_cap_forwards.cs` (async: `…_async.cs`)
  - Test should verify (R-19, R-4, R-9, A-6, AC-43), for all four configurations:
    - The Given is the two-subscription Given from 5.8: `requeueCount: 3`, `requeueDelay` zero, `DeadLetterPolicy` M = 5 whose topic exists with a reading subscription, and a failing `deadLetterRoutingKey`
    - The handler follows the branch: on AC-19, always defers; on AC-40, always throws `RejectMessageAction`
    - Poll the policy subscription every 500 ms for up to 60 s. On arrival, snapshot the dispatch count, wait W = 10 s, then quit and await.
    - The message arrives **without** rejection metadata
    - At least one R-19 Error names the message id and `"DeliveryError"`
    - The routing-key topic is still absent
    - The final dispatch count is `> 3` and equals the arrival snapshot
    - ⚠️ If there is no arrival within 60 s **and** the count is past M = 5, A-6 is refuted for that configuration. Still assert the Error, the absent topic and the count past M, and record the refutation per configuration in the GCP paragraph of `conformance-status.md`. No arrival with the count at or below M is an ordinary failure.
  - 🔁 **Characterisation — expected green on first run:** 5.8's release-on-failure, the native policy (5.4, M = 5) and the branch taken already deliver it; there is no new production code beyond 5.8 and the branch taken (ADR 0078 step 7). **RED mutation(s)**, applied and reverted one at a time: (a) on `RoutingOutcome.Failed`, pull `Reject`/`RejectAsync` call `AckByHandle` instead of `ReleaseByHandle`, and stream `Reject`/`RejectAsync` call `Accept` instead of `Nack` (5.8's failure branch) — the release loop ends at the first rejection and the message never reaches the policy subscription, failing on "the message arrives" (with the count at or below M); (b) `GcpRejectionRouter`'s lazy producer (5.5a) is created with `MakeChannels = OnMissingChannel.Create` instead of the subscription's `Assume` — routing succeeds, the original is acked and the loop ends, so it fails on "the message arrives" (count at or below M); the routing-key topic now exists.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - If the branch taken needs a change, make it only in 5.8's failure path or the branch's own task. This test is also the A-6 risk measurement.
  - Depends on: 5.5d, 5.8, 6.16 or 6.20

- [ ] **6.31 GATE: Run the GCP rejection-routing behaviours twice on a clean emulator with identical results, with no gcp-ci (AC-22, R-21)**
  - Run the GCP conformance behaviours FR-4, FR-5, FR-6, FR-8 and FR-17, plus FR-23 if AC-19 was claimed, twice across all four configurations and both variants, with `docker-compose -f docker-compose-gcp.yaml down -v; up -d` between runs.
  - **Output:** a dated record of both runs (identical results) in the GCP paragraph of `conformance-status.md`.
  - Depends on: 5.11, 6.16 or 6.21, 6.30

---

## Phase 7 — RocketMQ (#4353; R-14, AC-23 branch)

- [ ] **7.1 MEASURE: AC-23 — the broker's `DeliveryAttempt` across lease-lapse redeliveries, with a fresh client between deliveries 2 and 3 (R-14, A-3)**
  - Setup:
    - Start from a clean store (`docker-compose -f docker-compose-rocketmq.yaml down -v; up -d`), with `requeueCount: 3`, Reactor
    - Three deliveries across the 10 s invisibility lapses, with no `ChangeInvisibleDuration` call
    - Read the raw `MessageView.DeliveryAttempt` from `Header.Bag["ReceiptHandle"]` (`RocketMessageConsumer.cs:333`)
    - **Dispose the consumer and create a new one between deliveries 2 and 3.** This rules out the client-local `IncrementAndGetDeliveryAttempt` (ADR 0077 Risks).
  - Use a measurement fixture committed as `[Fact(Skip = "measurement — AC-23")]` in `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor`.
  - **Output (all three exit criteria required):**
    - (a) the three values in the RocketMQ paragraph of `conformance-status.md` (`:521`)
    - (b) #4353 updated with the same values (link recorded in the ledger)
    - (c) exactly one of AC-24 or AC-25 claimed, with the other marked not applicable, citing this measurement
  - If the values are strictly increasing and there is vendor evidence of exactness, amend ADR 0077's counter-classification table by a dated note. RocketMQ then becomes "exact", which activates AC-34's second clause and AC-41's "exactly 3", asserted in 7.10 and 7.12.
  - **Then:** do exactly one branch section below.

### Phase 7 common — both branches

- [ ] **7.2 TEST + IMPLEMENT: A null-reason RocketMQ Reject stamps `rejectionReason = "None"` on the dead-letter copy**
  - **USE COMMAND**: `/test-first when a rocketmq message is rejected with no reason should stamp rejection reason none on the dead letter copy`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_rejecting_a_rocketmq_message_with_no_reason_should_stamp_rejection_reason_none.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-28 edge case 1; ADR 0077 null-reason decision):
    - `Reject(message, null)` → the DLQ copy has `rejectionReason == "None"` and no `rejectionMessage`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Change `RocketMessageConsumer.RefreshMetadata` (`:248`, early return `:254`) to stamp `RejectionReason.None.ToString()` when `reason` is null
  - Depends on: 1.3

- [ ] **7.3 TEST + IMPLEMENT: The RocketMQ publisher's header-owned properties win over same-named bag entries, so a dead-letter copy carries its stamped HandledCount**
  - **USE COMMAND**: `/test-first when publishing a rocketmq message whose bag holds a stale handled count should send the header handled count`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_publishing_a_rocketmq_message_with_stale_bag_handled_count_should_send_header_value.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-5, R-28; ADR 0077 "RocketMQ conditional"):
    - A message shaped like a routed dead-letter copy — `Header.HandledCount = 3`, a stale `Bag[HeaderNames.HandledCount] = "0"` (as the consumer copies it, `RocketMessageConsumer.cs:328-331`), **and** `Bag[RejectionMetadataKeyNames.RejectionReason]` present — is published and received back with `HandledCount == 3`. This holds on both branches: on AC-24, `DeliveryCount.Resolve` keeps the header count because the discriminator is present; on AC-25, the header property is read as today.
    - Other header-owned keys behave the same way
    - This test must stay green on both branches; re-run it after 7.10 or 7.20.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Make the bag loop in `RocketMqMessagePublisher.cs:54-59` skip any key `AddHeaderProperties` already wrote (GCP's `!headers.ContainsKey` shape, `Parser.cs:352`), so the stamped `HandledCount` (`:103`) survives
  - Depends on: none within the phase

- [ ] **7.4 TEST + IMPLEMENT: `RocketSubscription` is a delivery-counting subscription whose native limit is not visible to Brighter**
  - **USE COMMAND**: `/test-first when a rocketmq subscription is created should report no visible native redrive limit`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway"
  - Test file: `When_creating_rocket_subscription_should_report_no_visible_native_redrive_limit.cs`
  - Test should verify (R-10, R-11 input; ADR 0077 budget-rules table):
    - `RocketSubscription` / `RocketMqSubscription<T>` is `IAmADeliveryCountingSubscription`
    - `NativeRedriveLimit == null`, because max retry is server-side on the consumer group
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement the interface on `RocketSubscription` (`RocketMqSubscription.cs:10`). `DeliveryBudgetUnenforceableReason` is `null` for now; 7.20 sets it on the AC-25 branch.
    - Call `DeliveryBudgetDiagnostics.WarnIfUnenforceable` in `RocketMqChannelFactory` `CreateSyncChannel` (`:13`), `CreateAsyncChannel` (`:28`) and `CreateAsyncChannelAsync` (`:43`), the three non-delegating paths named in ADR 0077 R-26
  - Depends on: 2.4, 2.6

- [ ] **7.5 CHARACTERISE: RocketMQ budget of -1 never rejects**
  - **USE COMMAND**: `/test-first when rocketmq budget is minus one should never reject and never dead letter`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_rocketmq_budget_is_minus_one_should_never_reject.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-6, AC-5, the `-1` clause of AC-35):
    - `requeueCount: -1`, and no broker max-retry dead-lettering within 60 s
    - Dispatch count `> 3`, no `DeliveryError`, and a DLQ read returns `MT_NONE`
  - 🔁 **Characterisation — expected green on first run:** the pump's `-1` guard (`MessagePump.cs:171`) is transport-neutral, so it holds on either branch. **RED mutation(s)**, applied and reverted one at a time: (a) `MessagePump.DiscardRequeuedMessagesEnabled()` (`src/Paramore.Brighter.ServiceActivator/MessagePump.cs:171-174`) returns `true`, treating `-1` as an enabled budget, so `HandledCountReached(-1)` (`Message.cs:161`) rejects on the first deferral — fails on "dispatch count `> 3`" (and the DLQ read is no longer `MT_NONE`).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at `MessagePump.DiscardRequeuedMessagesEnabled()`.
  - Depends on: 3.3

- [ ] **7.6 CHARACTERISE: RocketMQ budget of 1, 0 or below -1 rejects on the first deferral without requeuing**
  - **USE COMMAND**: `/test-first when rocketmq budget is one zero or below minus one should reject on first deferral without requeue`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_rocketmq_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-7, AC-6, AC-35), as a `[Theory]` over `1`, `0`, `-3`:
    - Dispatch count == 1 and `Requeue` count == 0
    - `DeliveryError` is issued, and the message is on the DLQ
  - 🔁 **Characterisation — expected green on first run:** `HandledCountReached` already holds on the first deferral (`Reactor.cs:494-498`) and 3.3 supplies the recording consumer. **RED mutation(s)**, applied and reverted one at a time: (a) `MessagePump.DiscardRequeuedMessagesEnabled()` (`MessagePump.cs:171-174`) returns `RequeueCount > 0` instead of `RequeueCount != -1` — the `0` and `-3` rows requeue, failing on "dispatch count == 1" and "`Requeue` count == 0"; (b) `Message.HandledCountReached` (`src/Paramore.Brighter/Message.cs:161-164`) uses `>` instead of `>=` — the `1` row requeues, failing on "`Requeue` count == 0".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at `HandledCountReached` or at the pump guard.
  - Depends on: 3.3

### RocketMQ — AC-24 branch (counter holds)

> Do this section only if 7.1 claimed AC-24. Otherwise mark every task `[!] not taken — AC-23 selected AC-25`.

- [ ] **7.10 TEST + IMPLEMENT: RocketMQ presents a strictly increasing delivery count across lease-lapse redeliveries, starting at 0**
  - **USE COMMAND**: `/test-first when a rocketmq message is redelivered should present a strictly greater delivery count starting at zero`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_a_rocketmq_message_is_redelivered_should_present_increasing_delivery_count.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-1, R-2, R-3, AC-1, AC-2; plus AC-34 clause 2 with `requeueCount: 4` → exactly `0, 1, 2`, **only** if 7.1 reclassified RocketMQ exact):
    - `requeueCount: -1`, the deferring pump, quitting when the dispatch count reaches 3
    - The recording sequence is strictly increasing, and the first value is `0`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `RocketMessageConsumer.CreateMessage`, **after** the bag loop (`:328`), set `header.HandledCount = DeliveryCount.Resolve(header.HandledCount, view.DeliveryAttempt, header.Bag)`. `ReadHandledCount` (`:422`, called `:292`) runs before the header exists.
    - `Requeue` stays a broker no-op; do not touch `ReadDelay` (out of scope)
  - Depends on: 2.2, 3.1, 3.3, 7.1

- [ ] **7.11 CHARACTERISE: A RocketMQ message whose invisible duration lapses presents a higher count on the expiry redelivery, with no pump and no Requeue**
  - **USE COMMAND**: `/test-first when a rocketmq invisible duration lapses without ack or requeue should present a greater delivery count on redelivery`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_a_rocketmq_invisible_duration_lapses_should_present_greater_delivery_count.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-1, AC-42):
    - Published with `HandledCount = 0`
    - `Receive` m1, hold it, wait for the invisible duration to lapse, `Receive` m2 with a count greater than m1's
    - Separately through `ReceiveAsync`
  - 🔁 **Characterisation — expected green on first run:** 7.10 already sets `header.HandledCount = DeliveryCount.Resolve(header.HandledCount, view.DeliveryAttempt, header.Bag)` in `RocketMessageConsumer.CreateMessage`; this asserts what AC-23 recorded. **RED mutation(s)**, applied and reverted one at a time: (a) in `RocketMessageConsumer.CreateMessage` (after the bag loop, `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMessageConsumer.cs:328`), pass `null` instead of `view.DeliveryAttempt` to `DeliveryCount.Resolve`, reverting 7.10 — fails on "m2's count greater than m1's".
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at 7.10's `CreateMessage` change.
  - Depends on: 7.10

- [ ] **7.12 CHARACTERISE: The RocketMQ dead-letter copy of a budget rejection, read through a real channel, carries the stamped count and full rejection metadata**
  - **USE COMMAND**: `/test-first when the rocketmq budget is exhausted the dead letter copy read through a channel should present the stamped handled count and rejection metadata`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_rocketmq_budget_is_exhausted_dead_letter_copy_should_keep_stamped_count_and_metadata.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-5, R-28, AC-4, AC-41):
    - `requeueCount: 3`, with the DLQ read via a `ChannelFactory` channel
    - `HandledCount >= 3` (exactly `3` if reclassified exact) and not `0`
    - The five AC-4 keys are valid
  - 🔁 **Characterisation — expected green on first run:** 7.3 (header-owned `HandledCount` wins over the bag) and 7.10 (`Resolve` with the discriminator) already deliver it. **RED mutation(s)**, applied and reverted one at a time: (a) `DeliveryCount.Resolve` (2.2) ignores the `rejectionReason` discriminator and returns the normalised broker count whenever one is present — the DLQ copy presents its own normalised count, failing on "`HandledCount >= 3` and not `0`"; (b) the bag loop in `RocketMqMessagePublisher.cs:54-59` stops skipping keys `AddHeaderProperties` already wrote (`:103`), reverting 7.3 — the stale bag `HandledCount = "0"` wins, failing on the same assertion.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE once RED is observed — before committing if the test was green on arrival, before implementing if it was not** *(fires in the `review-before` gear, which is the default)*
  - Implementation should (only if the test is unexpectedly RED on arrival — otherwise no production change):
    - A red points at 7.3's publisher change or 7.10's `Resolve` call.
  - Depends on: 7.3, 7.10

- [ ] **7.13 GATE: RocketMQ FR-23 passes in both variants from a clean store; move the cell to `Fixed (#4353)` (AC-24, AC-3 on RocketMQ, AC-30 row 4)**
  - From `down -v`, set the cell to `Fixed (#4353)`, regenerate, and run FR-23 in both variants.
  - The message reaches the DLQ in 60 s carrying `rejectionReason == "DeliveryError"`, with dispatch count `<= 3`.
  - **Output:** the cell reads `Fixed (#4353)` with evidence. Mark 7.20–7.21 `[!] not taken — AC-23 selected AC-24`.
  - Depends on: 7.10–7.12

### RocketMQ — AC-25 branch (bound but unimplemented)

> Do this section only if 7.1 claimed AC-25. Otherwise mark every task `[!] not taken — AC-23 selected AC-24`.

- [ ] **7.20 TEST + IMPLEMENT: A RocketMQ channel with a budget warns at creation that the count cannot advance, naming the upstream blocker, and keeps Brighter rejection routing**
  - **USE COMMAND**: `/test-first when a rocketmq channel is created with a budget should warn that the upstream change invisible duration blocker prevents the count advancing`
  - Test location: "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor" and "tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Proactor"
  - Test file: `When_a_rocketmq_channel_is_created_with_a_budget_should_warn_naming_the_upstream_blocker.cs` (Proactor: `…_async.cs`)
  - Test should verify (R-11, R-14(a)–(d), R-26, AC-25 behavioural clauses):
    - `requeueCount: 3` → exactly one Warning naming the subscription and the `ChangeInvisibleDuration` blocker, on each of the three factory paths
    - `RocketSubscription` still implements both support interfaces
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Make `RocketSubscription.DeliveryBudgetUnenforceableReason` return fixed text naming the upstream `ChangeInvisibleDuration` blocker
    - Leave the receive path unchanged
  - Depends on: 7.4, 7.1

- [ ] **7.21 GATE: Re-point the RocketMQ FR-23 cell at the upstream blocker (AC-25 ledger clause, AC-30 row 4)**
  - Change the cell from `Deferred -> #4353` to `Deferred` pointing at the upstream `ChangeInvisibleDuration` C# client fix, citing 7.1's measurement.
  - **Output:** the cell is re-pointed, and #4353 is updated.
  - Depends on: 7.20

### Phase 7 common — after the branch

- [ ] **7.30 GATE: RocketMQ's nine `Fixed` cells still pass on the chosen branch (AC-38, R-22, R-23)**
  - From `down -v`, run the scoped RocketMQ suite in both variants.
  - FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-16, FR-17 and FR-22 all pass. FR-16 and FR-22 depend on 3.1 on the AC-24 branch.
  - **Output:** a dated run record in the RocketMQ paragraph of `conformance-status.md`.
  - Depends on: 7.13 or 7.21

---

## Phase 8 — Samples, audits, ledger and exit criteria

- [ ] **8.1 GATE: ADR document reviews (AC-33 second clause, AC-34 first clause, AC-41 final clause)**
  - Confirm in ADR 0077:
    - "First delivery on an approximate counter" states the residual risk and the exposed cells (AC-33)
    - The four-row exact/approximate table classifies AWSSQS, AWSSQS.V4, GcpPubSub and RocketMQ with evidence, including any 7.1 reclassification. If none is exact, AC-34's second clause binds nothing; say so.
    - The four-row R-28 table names the discriminator (AC-41)
  - **Output:** a dated review note in `specs/0037-delivery-count-and-rejection-routing/README.md` Status Checklist.
  - Depends on: 6.7, 7.1

- [ ] **8.2 GATE: Broker-call enumeration equals the base revision on receive and requeue paths (AC-28, NFR-1, NFR-3)**
  - Enumerate broker-call sites on the receive and requeue paths from the PR diff for AWSSQS/V4, GcpPubSub pull and stream, and RocketMQ.
  - Compare with the base revision.
  - **Output:** ADR 0077's "AC-28 broker-call enumeration" table confirmed or corrected by a dated note, with diff references. Any added call is a defect.
  - Depends on: Phases 4–7

- [ ] **8.3 MEASURE: No new per-message allocation on the receive path (AC-37, NFR-2)**
  - For each in-scope transport (AWSSQS, AWSSQS.V4, GcpPubSub pull and stream, RocketMQ):
    - Measure `GC.GetAllocatedBytesForCurrentThread()` around 1,000 receives
    - Take the median of 5 runs on the base revision (a separate worktree of the merge base) and on this branch
    - Require `post <= base`, with zero tolerance
  - **Output:** a table of medians in a dated note in ADR 0077 (Implementation Approach → Testing). Any increase is a defect in the reader (`int?`, `TryGetValue`, `int.TryParse` on an existing string).
  - Depends on: Phases 4–7

- [ ] **8.4 GATE: The V10 compatibility samples still compile against the final assemblies (AC-27, R-24)**
  - Build the solution. The five samples from 1.1 compile with no new errors or obsoletion warnings, with no `#pragma`, no new project and no new reference.
  - Confirm `Subscription.RequeueCount` still defaults to `-1`.
  - **Output:** the checklist item ticked with the build reference.
  - Depends on: 1.1, Phases 2–7

- [ ] **8.5 GATE: Release notes and dead-letter documentation for the visible contract changes (ADR 0077 edge cases 2 and 3, Consequences → Negative; ADR 0078)**
  - Update `release_notes.md`, and the relevant page under `docs/transports/` if dead-lettering is described there, to cover:
    - A broker counter now overrides a producer-set `HandledCount` on SQS, GCP (AC-19) and RocketMQ (AC-24)
    - A null-reason Reject stamps `rejectionReason = "None"`
    - The rejection-metadata keys are Brighter-reserved
    - A replay tool that puts messages back must strip the metadata and may reset `HandledCount`
    - RocketMQ header-owned properties win over bag entries
    - New GCP `deadLetterRoutingKey`/`invalidMessageRoutingKey` and IAM tolerance Warnings
    - The three budget validation Warnings
  - **Output:** the doc diff in the PR.
  - Depends on: 6.7, 7.1, Phases 4–7

- [ ] **8.6 GATE: Ledger audit — 33 cells moved on evidence, and the cells this spec could not move are stated (AC-30, AC-31)**
  - Check each moved cell:
    - 8 AWS FR-23 cells (4.11)
    - 20 GCP routing cells (5.11)
    - 4 GCP FR-23 cells (6.16 or 6.21)
    - 1 RocketMQ FR-23 cell (7.13 or 7.21)

    Each cell must cite its run.
  - Add an explicit "Not moved by 0037" list to `conformance-status.md`:
    - `MQTT` FR-23 (#4351)
    - ASB FR-23 and other `Deferred` cells (no emulator; dead-letters natively)
    - Remaining GCP FR-2/7/9/15/16/22 × 4 (move only on their own evidence)
    - `AWS/AWS.V4 SqsFifo` FR-9, `MSSQL`/`Redis`/`MQTT` FR-16, `RMQ.*` FR-5, `RocketMQ` FR-2/FR-15
    - Any per-configuration A-6 refutation from 6.30 (R-19's native-cap bound unevidenced on the emulator)
  - **Output:** the ledger edits, plus the checklist item "AC-30/AC-31 ledger audited" ticked in `specs/0037-delivery-count-and-rejection-routing/README.md`.
  - Depends on: 4.11, 5.11, 6.16/6.21, 6.30, 7.13/7.21

- [ ] **8.7 GATE: Final regression and C-7 residual-risk record (R-22, R-23, AC-26 re-run)**
  - Re-run the scoped conformance suites for the nine AC-26 configurations and for all in-scope configurations, both variants.
  - Record any flaky first-delivery identity failure (an approximate counter over-counting on a first delivery; ADR 0077 "First delivery on an approximate counter") against ADR 0077 as a dated observation. Report it; do not mask it.
  - **Output:** a dated final run record in `conformance-status.md`, and a C-7 observation note (or "none observed") in ADR 0077.
  - Depends on: all prior phases

---

## Coverage cross-reference

### Requirements → tasks

| Req | Tasks |
|---|---|
| R-1 | 4.3, 4.4, 6.10, 6.11, 6.12, 6.14, 7.10, 7.11 |
| R-2 | 2.1, 3.1, 4.3, 6.10, 6.11, 7.10, 8.1 |
| R-3 | 2.1, 4.3, 6.10, 6.11, 7.1, 7.10, 8.1 |
| R-4 | 3.4, 4.11, 6.16, 6.30, 7.13 |
| R-5 | 4.6, 6.15, 7.3, 7.12 |
| R-6 | 4.9, 6.5, 7.5 |
| R-7 | 2.3, 4.10, 6.6, 7.6 |
| R-8 | 4.7, 4.8 |
| R-9 | 4.8, 6.30 |
| R-10 | 2.4, 4.1, 6.1, 7.4 |
| R-11 | 2.5, 2.6, 6.1, 6.2, 6.20, 7.20 |
| R-12 | 4.1–4.12 |
| R-13 | 6.1–6.31 (6.7 selects the branch) |
| R-14 | 7.1–7.30 (7.1 selects the branch) |
| R-15 | 5.1 |
| R-16 | 5.5a, 5.5b, 5.6, 5.10 |
| R-17 | 5.7, 5.10 |
| R-18 | 5.5a, 5.5b, 5.6 |
| R-19 | 5.8, 5.9, 5.10, 6.30 |
| R-20 | 5.2, 5.3 |
| R-21 | 5.3, 6.31 |
| R-22 | 3.5, 7.30, 8.7 |
| R-23 | 3.1, 3.5, 4.11, 7.30, 8.7 |
| R-24 | 1.1, 8.4 |
| R-25 | 2.3, 2.4, 2.5, 2.7 |
| R-26 | 2.6, 4.1, 6.2, 7.4, 7.20 |
| R-27 | 3.2, 3.3, 3.4 (c); 4.2 (a, AWS); 5.4 (a and b, GCP) |
| R-28 | 2.2, 4.5, 4.6, 5.5d, 6.15, 7.2, 7.3, 7.12, 8.1 |
| NFR-1 | 4.3, 6.10, 8.2 |
| NFR-2 | 2.1, 2.2, 8.3 |
| NFR-3 | 8.2 |
| NFR-4 | 2.6, 6.2 |
| NFR-5 | 5.2, 5.3 |
| NFR-6 | Phase 4 lockstep, 4.12 |
| NFR-7 | 3.4, 6.16, 6.30, 7.13 |
| NFR-8 | Every behavioural task (sync and async in one task) |

Every requirement has at least one task.

### Acceptance criteria → tasks

| AC | Tasks | AC | Tasks |
|---|---|---|---|
| AC-1 | 4.3, 6.10, 6.11, 7.10 | AC-23 | 7.1 (MEASURE) |
| AC-2 | 4.3, 6.10, 6.11, 7.10 (+3.1, 3.5, 7.30 runs) | AC-24 | 7.10–7.13 (branch) |
| AC-3 | 3.4, 4.11, 6.16, 7.13 | AC-25 | 7.20–7.21 (branch) |
| AC-4 | 4.6, 6.15, 7.12 | AC-26 | 3.5, 8.7 |
| AC-5 | 4.9, 6.5, 7.5 | AC-27 | 1.1, 8.4 |
| AC-6 | 4.10, 6.6, 7.6 | AC-28 | 8.2 (GATE) |
| AC-7 | 2.3 | AC-29 | 6.2 |
| AC-8 | 4.7 | AC-30 | 4.11, 5.11, 6.16 / 6.21, 7.13 / 7.21, 8.6 |
| AC-9 | 4.8 | AC-31 | 8.6 (+ 6.30 A-6 record) |
| AC-10 | 2.4, 4.1, 6.1, 7.4 | AC-32 | 2.7 |
| AC-11 | 2.5, 2.6, 6.1, 6.2 | AC-33 | 2.1, 8.1 |
| AC-12 | 4.11 | AC-34 | 8.1 (+ 7.10 if RocketMQ is reclassified exact) |
| AC-13 | 4.12 | AC-35 | 2.3, 4.9, 4.10, 6.5, 6.6, 7.5, 7.6 |
| AC-14 | 5.1 | AC-36 | 3.4, 4.2, 5.4 |
| AC-15 | 5.5a, 5.5b, 5.6, 5.11 | AC-37 | 8.3 (MEASURE) |
| AC-16 | 5.5a, 5.5b, 5.6, 5.11 | AC-38 | 7.30 |
| AC-17 | 5.7, 5.11 | AC-39 | 6.7 (MEASURE) |
| AC-18 | 5.8 | AC-40 | 6.20, 6.21 (branch) |
| AC-19 | 6.10–6.16 (branch) | AC-41 | 2.2, 4.6, 6.15, 7.12, 8.1 |
| AC-20 | 5.3 | AC-42 | 4.4, 6.12, 6.14, 7.11 |
| AC-21 | 5.2 | AC-43 | 6.30 |
| AC-22 | 6.31 | | |

Every AC has at least one task.

### ADR implementation steps and key decisions → tasks

**ADR 0077**

| Step or decision | Tasks |
|---|---|
| Step 1: structural (`RejectionMetadataKeyNames`, creator call order, `RefreshMetadata` constants) | 1.2, 1.3, 1.4 |
| Step 2: core (`DeliveryCount`, interface, `DeliveryBudgetDiagnostics`, three rules) | 2.1–2.7 |
| Step 3: conformance oracle (`>=` redelivery arms) | 3.1 |
| Step 4: SQS both packages (creators, `"None"` stamping, interface, factory calls) | 4.1, 4.3, 4.5 (tests 4.2–4.12) |
| Step 5: GCP (parser pull and stream, ignore entry, interface, factory calls; FR-23 gated on AC-39) | 5.5d, 6.1, 6.2, 6.7, 6.10, 6.11, 6.16 |
| Step 6: RocketMQ (AC-23, then branch; `"None"` stamping and bag-loop skip on both branches) | 7.1, 7.2, 7.3, 7.4, 7.10–7.21 |
| Step 7: harness (R = 3, M = 5; IAM members; dispatch count and recording consumer; FR-23 `<=` assertion) | 3.2, 3.3, 3.4, 4.2, 5.4 |
| R-26 channel-creation logging sites (AWS ×2 per package, GCP ×2, RocketMQ ×3) | 4.1, 6.2, 7.4 |
| `googclient_deliveryattempt` added to `Parser.s_ignoreHeaders` | 5.5c (risk), 5.5d (before 5.6) |
| Publisher bag-loop skip | 7.3 |
| `RejectionMetadataKeyNames` (core) vs harness `RejectionMetadataKeys` | 1.2, 1.3, 2.2, 5.4, 5.5a |
| Null-reason `"None"` | 4.5, 7.2, 5.5a (router) |
| AC-42 stream procedure and `StreamOrdering` alternative | 6.13, 6.14 |
| Measurement amendments (AC-39(b)/(c), AC-23 reclassification) | 6.7, 7.1 |
| Risks: `SubscriberClient`/`googclient_deliveryattempt`; `bufferSize: 2`; client-local RocketMQ counter; C-7 over-count | 5.5c; 6.13; 7.1; 8.7 |
| Edge case 2 and 3 documentation | 8.5 |

**ADR 0078**

| Step or decision | Tasks |
|---|---|
| Step 1: structural settle helpers | 1.5 |
| Step 2: R-15 ctor params appended, interfaces | 5.1 |
| Step 3: `GcpRejectionRouter`, consumer ctor params, factory wiring, dispose | 5.5a (router, pull ctor params, factory wiring, dispose), 5.5b (selection by reason), 5.6; 0077's ignore entry (5.5d) precedes 5.6, as 0078:38 relies on it |
| Step 4: `Reject` composition (AC-15/16/17/18; NFR-8; always returns `true`) | 5.5a, 5.5b, 5.6, 5.7, 5.8 |
| Step 5: harness (Brighter route, `.native` topic and subscription at M = 5, DLQ and invalid readers, keys record, two-subscription Given) | 5.4, 5.8 |
| Step 6: `GcpIamCallTolerance` (`InvalidOperationException`), applied in both helpers | 5.2, 5.3 |
| Step 7: AC-43 | 6.30 |
| Step 8: fault-injection evidence (test-only interceptor via `SubscriptionManagerConfiguration`) | 5.10 (TEST + IMPLEMENT; tests RED before the settle-failure handling, then the ADR evidence entry) |
| Lazy producer inherits `MakeChannels`; only success cached | 5.5a, 5.8, 5.9 |
| Risks: prompt missing-topic failure and sync dispose; ordering-key pause; interceptor dropped or `Validate`; offline `Unavailable`; `Validate` widening | 5.8; 5.9; 5.10; 5.3; 5.2 |

Every step and decision has a task.

### Scope check

No task falls outside the requirements or an ADR decision. The less obvious links:
- 1.1 is AC-27, placed early.
- 5.9 comes from ADR 0078's "Only success is cached" decision and its ordering-key risk.
- 5.5c and 6.13 come from ADR 0077's Risks table.
- 8.5 comes from ADR 0077 edge cases 2 and 3 ("Brighter documents this").

Nothing is tasked for #4415, `RocketMessageConsumer.ReadDelay`, or the requirements' Out of Scope list. 7.10 says explicitly not to touch `ReadDelay`.

### Critical Files for Implementation
- tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Shared/ConformanceDeferredPump.cs.liquid
- src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageCreator.cs (and the V4 twin and `SqsInlineMessageCreator.cs`)
- src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPullMessageConsumer.cs (and `GcpPubSubStreamMessageConsumer.cs`, `Parser.cs`, `GcpPubSubMessageGateway.cs`)
- src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMessageConsumer.cs (and `RocketMqMessagePublisher.cs`)
- src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs
- specs/0036-universal-transport-conformance-tests/conformance-status.md
