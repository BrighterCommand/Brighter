# Tasks — Spec 0030 reject_mapping_errors

> Implementation task list derived from [requirements.md](requirements.md) and
> [ADR 0061](../../docs/adr/0061-reject_mapping_errors.md).
>
> **TDD is MANDATORY.** Each behavioural task below MUST be implemented via the `/test-first`
> command shown in the task. STOP after writing each test and wait for the user to review it in
> their IDE before implementing. Do NOT write tests manually and proceed to implementation.

This is a purely behavioural change (ADR 0061: no structural/Tidy-First step). No structural prep
tasks are warranted — per ADR §"Responsibility-Driven Design framing" the change "carries no
separate structural-change step". Tasks are ordered Proactor-first then Reactor (ADR sequencing),
with regression guards and parity/edge characterization tests after. The only non-test production
edits are the two `MessageMappingException` catch-block bodies (Tasks 1 and 2); the remaining tasks
are characterization tests expected to be green once those land (or to expose a regression if they
are not).

## Tasks

- [ ] **TEST + IMPLEMENT: Proactor rejects a mapping failure as Unacceptable instead of acknowledging it**
  - **USE COMMAND**: `/test-first Proactor routes a MessageMappingException through RejectMessage with RejectionReason.Unacceptable and continues, never reaching the fall-through acknowledge`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/"
  - Test file: UPDATE/repurpose the existing `When_a_message_fails_to_be_mapped_to_a_request_async.cs` (class `MessagePumpFailingMessageTranslationTestsAsync`, fact `When_A_Message_Fails_To_Be_Mapped_To_A_Request_Should_Ack`) — this currently encodes the OLD ack contract (`Assert.Empty(_bus.Stream(_routingKey))`, which still passes for the wrong reason). Strengthen it to prove the REJECT path by constructing the `InMemoryMessageConsumer` with an `invalidMessageTopic` (mirroring the pattern in `When_a_message_mapper_throws_invalid_message_action_async.cs`).
  - Test should verify:
    - The mapping failure is driven through the REAL translate path via `FailingEventMessageMapperAsync` (bare unwrapped `MessageMappingException`) — NOT a hand-thrown exception (satisfies AC-12).
    - Source stream `_bus.Stream(_routingKey)` is empty.
    - IMQ stream `_bus.Stream(invalidMessageTopic)` is NOT empty (the message was rejected/routed, not silently deleted).
    - The dequeued IMQ message's `Header.Bag[Message.RejectionReasonHeaderName]` carries `RejectionReason.Unacceptable` and a Description.
    - The fall-through `await Acknowledge(message)` path did NOT consume the message (proven by the message appearing on the IMQ rather than being plain-deleted).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `src/Paramore.Brighter.ServiceActivator/Proactor.cs`, in the `catch (MessageMappingException messageMappingException)` block (verified at ~374-379, currently ending after `processSpan?.SetStatus(...)` with NO reject/continue):
      - Extract the existing inline interpolation into a shared local `description = $"MessagePump: Failed to map message {message.Id} from {Channel.Name} with {Channel.RoutingKey} on thread # {Thread.CurrentThread.ManagedThreadId}"` (preserve `Thread.CurrentThread.ManagedThreadId` verbatim — do NOT normalise, C-1).
      - Pass `description` to the existing `processSpan?.SetStatus(ActivityStatusCode.Error, description)`.
      - Retain `IncrementUnacceptableMessageCount()` and `Log.FailedToMapMessage(...)`.
      - Add `await RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description));` (helper at ~474) then `continue;` — mirroring the adjacent `InvalidMessageAction` block at ~370-372.
      - Leave the fall-through `await Acknowledge(message)` (~391) unchanged.
  - References: FR-1, FR-6, FR-7, AC-1, AC-12, ADR-0061 (route mapping via RejectMessage+continue; shared description local; retain counter); touches `Proactor.cs`, updates `When_a_message_fails_to_be_mapped_to_a_request_async.cs`.

- [ ] **TEST + IMPLEMENT: Reactor rejects a mapping failure as Unacceptable instead of acknowledging it**
  - **USE COMMAND**: `/test-first Reactor routes a MessageMappingException through RejectMessage with RejectionReason.Unacceptable and continues, never reaching the fall-through acknowledge`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: UPDATE/repurpose the existing `When_a_message_fails_to_be_mapped_to_a_request.cs` (sync equivalent encoding the old ack contract). Strengthen it the same way: `InMemoryMessageConsumer` with an `invalidMessageTopic`, driven by `FailingEventMessageMapper`.
  - Test should verify:
    - Mapping failure driven through the REAL translate path via `FailingEventMessageMapper` (bare unwrapped `MessageMappingException`) — NOT hand-thrown (AC-12).
    - Source stream empty; IMQ stream NOT empty; dequeued IMQ message's `Header.Bag[Message.RejectionReasonHeaderName]` carries `RejectionReason.Unacceptable` + Description.
    - Fall-through `AcknowledgeMessage(message)` did NOT consume the message.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `src/Paramore.Brighter.ServiceActivator/Reactor.cs`, in the `catch (MessageMappingException messageMappingException)` block (verified at ~339-344): apply the identical change to the Proactor task but DROP `await` — `RejectMessage(message, new MessageRejectionReason(RejectionReason.Unacceptable, description));` (helper at ~407) then `continue;`, mirroring the sync `InvalidMessageAction` block at ~335-337.
    - Shared `description` local with the verbatim mapping string (same as Proactor; preserve `Thread.CurrentThread.ManagedThreadId`).
    - Leave fall-through `AcknowledgeMessage(message)` (~356) unchanged.
  - References: FR-3, FR-6, FR-7, AC-3, AC-12, NFR-1, ADR-0061 (Reactor drops await; sequencing Proactor then Reactor); touches `Reactor.cs`, updates `When_a_message_fails_to_be_mapped_to_a_request.cs`.
  - Depends on: prior Proactor task (apply Proactor first per ADR sequencing; no file-level dependency, but keep order).

- [ ] **TEST + IMPLEMENT: Rejection Description equals the process-span status string and contains the message Id (both pumps)**
  - **USE COMMAND**: `/test-first the MessageRejectionReason.Description on the mapping path is non-empty, contains the message Id, and is the same string passed to processSpan.SetStatus`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" and "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_a_message_fails_to_be_mapped_the_rejection_description_matches_the_span_status_async.cs` (Proactor) and `When_a_message_fails_to_be_mapped_the_rejection_description_matches_the_span_status.cs` (Reactor)
  - Test should verify:
    - Use a test/recording consumer (or capture from the IMQ-routed message header) that exposes the captured `MessageRejectionReason`.
    - Captured `Description` is non-empty and contains the substring of the message `Id`.
    - Capture the string passed to `processSpan?.SetStatus(...)` on the same path and assert it EQUALS the captured `Description` (use a recording/in-memory `Tracer`/exporter to read the span status description).
    - Do NOT assert any literal thread-id token (environment-dependent, C-5).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No new prod code beyond the shared `description` local introduced in the Proactor/Reactor tasks above. This test PROVES the shared-local refactor (C-5). If the description and span string diverge, the test fails — investigate the shared-local extraction.
  - References: FR-1, FR-3, FR-6, AC-10, C-5, ADR-0061 (shared description local); depends on both prior pump tasks.

- [ ] **TEST + IMPLEMENT: Catch-all dispatch exception still acknowledges (async regression guard)**
  - **USE COMMAND**: `/test-first an unexpected dispatch Exception in the Proactor still falls through to Acknowledge and is NOT rejected`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/"
  - Test file: `When_a_dispatch_exception_is_thrown_the_catch_all_acknowledges_async.cs`
  - Test should verify:
    - A non-mapping, non-action `Exception` (e.g. `InvalidOperationException`) thrown during dispatch of message `def-456`.
    - Recording consumer records ZERO rejects for the message and exactly ONE fall-through acknowledge.
    - Failure logged via `Log.FailedToDispatchMessage2`, process span status Error, span ended.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Characterization — expected GREEN; no prod code (the catch-all `catch (Exception e)` block at ~380-385 is LEFT UNCHANGED per FR-8). If RED, investigate: the adjacent edit accidentally altered the catch-all.
  - References: FR-8, AC-2, NFR-2, ADR-0061 (catch-all unchanged; regression guard); touches `Proactor.cs` catch-all only as a guard.

- [ ] **TEST + IMPLEMENT: Catch-all dispatch exception still acknowledges (sync regression guard)**
  - **USE COMMAND**: `/test-first an unexpected dispatch Exception in the Reactor still falls through to AcknowledgeMessage and is NOT rejected`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_a_dispatch_exception_is_thrown_the_catch_all_acknowledges.cs`
  - Test should verify:
    - `InvalidOperationException` thrown during dispatch of message `jkl-012`.
    - Recording consumer records ZERO rejects and exactly ONE fall-through `AcknowledgeMessage`.
    - Logged via `Log.FailedToDispatchMessage2`, span Error, span ended.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Characterization — expected GREEN; no prod code (catch-all at ~345-350 unchanged per FR-8). If RED, investigate accidental edit.
  - References: FR-8, AC-4, NFR-2, ADR-0061 (catch-all unchanged); touches `Reactor.cs` catch-all only as a guard.

- [ ] **TEST + IMPLEMENT: Unacceptable limit trips after N mapping rejections**
  - **USE COMMAND**: `/test-first with UnacceptableMessageLimit=3, three consecutive MessageMappingExceptions each reject and increment, and the pump terminates on the next iteration`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" (async) — extend coverage in Reactor too if a parallel file does not already assert reject
  - Test file: UPDATE/repurpose existing `When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached_async.cs` and its sync sibling `When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached.cs`
  - Test should verify:
    - `UnacceptableMessageLimit = 3`, window unset; three consecutive real-translate mapping failures each trigger a reject with `Unacceptable` and increment the count.
    - On the iteration after the third failure, `UnacceptableMessageLimitReached()` returns true and the pump terminates (4th message not dispatched).
    - Strengthen the existing assertions to confirm each failure REJECTED (not acked) in addition to the limit-trip behaviour they already assert.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Characterization for the limit-trip — the guardrail is unchanged; the reject behaviour comes from the Proactor/Reactor tasks above. No NEW prod code in `MessagePump.cs`. If the limit no longer trips, investigate: `IncrementUnacceptableMessageCount()` was dropped from the mapping block.
  - References: FR-5, AC-5, ADR-0061 (retain IncrementUnacceptableMessageCount); depends on Proactor/Reactor tasks.

- [ ] **TEST + IMPLEMENT: Default limit of 0 never trips despite repeated mapping rejections**
  - **USE COMMAND**: `/test-first with the default UnacceptableMessageLimit=0, many consecutive MessageMappingExceptions each reject and increment but the pump never terminates due to the limit`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/"
  - Test file: `When_the_unacceptable_message_limit_is_zero_mapping_failures_never_trip_the_limit_async.cs` (and Reactor sibling if not already covered)
  - Test should verify:
    - `UnacceptableMessageLimit = 0` (default); a large run (e.g. 100) of real-translate mapping failures each reject with `Unacceptable` and increment.
    - The pump does NOT terminate due to the limit (loop continues until externally stopped).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Characterization — expected GREEN; no new prod code (`UnacceptableMessageLimitReached()` already returns false when limit <= 0). If RED, investigate.
  - References: FR-5, AC-6, ADR-0061; depends on Proactor/Reactor tasks.

- [ ] **TEST + IMPLEMENT: No-IMQ consumer — pump still delegates reject without transport branching**
  - **USE COMMAND**: `/test-first a fake consumer with no IMQ or DLQ configured records exactly one Reject(Unacceptable) and zero fall-through Acknowledge on a mapping failure`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" and "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_a_message_fails_to_be_mapped_with_no_imq_the_pump_still_delegates_reject_async.cs` (and sync sibling)
  - Test should verify:
    - A recording/fake consumer with NO `invalidMessageTopic` and NO `deadLetterTopic` that records every `Reject` (with `MessageRejectionReason`) and every `Acknowledge` call.
    - On a real-translate mapping failure for message `mno-345`: exactly one `Reject` with `RejectionReason.Unacceptable`; zero fall-through `Acknowledge`.
    - Assert the PUMP invoked reject (not acknowledge) — what the consumer's reject then does to the source (e.g. plain delete) is out of scope (C-3). No transport-specific branching exists in the pump.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - May require a small recording test-double consumer in "tests/.../MessageDispatch/TestDoubles/" if no existing double records reject-vs-ack with the reason. Prefer reusing an existing recording double; only add one if none records both calls + the `MessageRejectionReason`. No prod-code change (delegation already follows from the Proactor/Reactor tasks).
  - References: FR-7, NFR-2, NFR-4, AC-8, ADR-0061 (no transport branching); depends on Proactor/Reactor tasks.

- [ ] **TEST + IMPLEMENT: Async/sync parity on the mapping reject path**
  - **USE COMMAND**: `/test-first the Proactor and Reactor reject an identical MessageMappingException scenario identically — same Unacceptable reason, both retain the counter, both continue past acknowledge`
  - Test location: "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/" and "tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/"
  - Test file: `When_the_mapping_reject_path_is_compared_across_pumps_async.cs` (and the Reactor sibling), or a shared-scenario theory if the test layout supports it
  - Test should verify:
    - Same real-translate mapping scenario run against both pumps yields the identical `RejectionReason.Unacceptable`.
    - Both increment the unacceptable count; both `continue` past the bottom-of-loop acknowledge; neither invokes its acknowledge for the failed message.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Characterization — expected GREEN once both pump edits land; no new prod code. If RED, investigate divergence between the two pump bodies (the only intended difference is `await`).
  - References: NFR-1, AC-7, AC-11, ADR-0061 (parity); depends on both pump tasks.

- [ ] **(OPTIONAL — NON-GATING) IMPLEMENT: Live SQS V4 end-to-end confirmation of IMQ/DLQ routing on mapping failure**
  - This is NOT a blocking task and NOT a TDD unit task. It is an optional manual/integration confirmation against real AWS SQS V4 (per AC-9, NFR-4 — the required gate is the fake-consumer unit tests above).
  - Confirm: a message that fails mapping appears on the configured Invalid Message Queue (or DLQ on fallback) and is removed from the source queue via the reject flow, not silently deleted.
  - Do NOT make CI block on this; do NOT add live-AWS dependencies to the unit gate.
  - References: AC-9 (optional/non-gating), FR-7, NFR-2.

## Coverage cross-reference

### Functional Requirements
- FR-1 (Proactor mapping→reject Unacceptable+continue): Task 1.
- FR-2: INTENTIONALLY WITHDRAWN (tombstone) — no task, correctly omitted.
- FR-3 (Reactor mapping→reject Unacceptable+continue): Task 2.
- FR-4: INTENTIONALLY WITHDRAWN (tombstone) — no task, correctly omitted.
- FR-5 (retain counter / guardrail): Task 6 (limit=3 trips), Task 7 (limit=0 never trips).
- FR-6 (logging+tracing+EndSpan preserved): Tasks 1, 2, 3.
- FR-7 (no transport branching — delegated): Tasks 1, 2, 8.
- FR-8 (catch-all UNCHANGED, regression-guarded): Tasks 4 (async), 5 (sync).

### Non-functional Requirements
- NFR-1 (Proactor/Reactor parity on mapping path): Task 9 (and 2).
- NFR-2 (no transport/consumer changes): Tasks 4, 5, 8.
- NFR-3 (no public API change): No standalone task — a non-additive "do-not-change" constraint enforced by all impl tasks (mapping-block body only; reuses existing `RejectMessage`/`MessageRejectionReason`/`RejectionReason`).
- NFR-4 (fake/recording consumer, no live AWS): Task 8 (recording consumer); all unit tasks use in-memory/fake consumers; Task 10 isolates live AWS as optional only.

### Acceptance Criteria
- AC-1 (Proactor mapping reject + log/span): Task 1.
- AC-2 (catch-all unchanged async — regression): Task 4.
- AC-3 (Reactor mapping reject): Task 2.
- AC-4 (catch-all unchanged sync — regression): Task 5.
- AC-5 (limit=3 trips): Task 6.
- AC-6 (limit=0 never trips): Task 7.
- AC-7 (finally/EndSpan runs, no fall-through ack): Tasks 1, 2 (no-ack on mapping path), Task 9 (continue-past-ack).
- AC-8 (no-IMQ delegation, no transport branching): Task 8.
- AC-9 (live SQS E2E — OPTIONAL, NON-GATING): Task 10.
- AC-10 (Description == SetStatus string, contains Id): Task 3.
- AC-11 (async/sync parity): Task 9.
- AC-12 (mapping exception via REAL translate path, not hand-thrown): Tasks 1, 2.

### ADR-0061 decisions
- Route mapping via RejectMessage+continue both pumps: Tasks 1, 2.
- Shared description local: Tasks 1, 2 (introduced), Task 3 (proves equality).
- Retain IncrementUnacceptableMessageCount: Tasks 1, 2 (retained), Task 6 (proves guardrail still trips).
- Catch-all unchanged: Tasks 4, 5.
- No new types / no branching: Tasks 1, 2, 8.
- Reactor drops await: Task 2.
- Sequencing Proactor then Reactor: encoded in task order (Task 1 before Task 2) and dependency notes.

### Scope-creep / gap check
No task introduces behaviour outside the FR/NFR/AC/ADR set. No task touches `SqsMessageConsumer`,
`MessageRejectionReason`, `RejectionReason`, the catch-all body, or the catch-block ordering. The
only non-test prod edits are the two mapping-block bodies (Tasks 1, 2). NFR-3 is the sole
requirement with no dedicated task, by design (a "do not change" constraint enforced across all
impl tasks). No gaps: every FR (FR-1..FR-8, FR-2/FR-4 withdrawn), every NFR, every AC
(AC-1..AC-12, AC-9 optional), and every ADR-0061 decision maps to at least one task.
