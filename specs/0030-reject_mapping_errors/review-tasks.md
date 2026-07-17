# Review: tasks — 0030-reject_mapping_errors (round 2)

**Date**: 2026-06-02
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Round 1 (2026-06-01) returned NEEDS WORK with 5 findings (3 High, 2 Medium). The task list was
> revised; round 2 confirms all five are resolved. The round-1 findings are retained at the bottom
> for history.

## Prior findings disposition (round 1 → round 2)

1. **RESOLVED** — The Observability mirror `When_There_Is_An_Unacceptable_Messages_Close_The_Span.cs` exists, class `MessagePumpUnacceptableMessageOberservabilityTests`, and demonstrates exactly the claimed pattern: `Sdk.CreateTracerProviderBuilder().AddSource("Paramore.Brighter").AddInMemoryExporter(...)` (lines 33-40), `ForceFlush()` then `Assert.Equal(ActivityStatusCode.Error, ...Status)` + `Assert.Contains(..., StatusDescription)` (lines 95, 109-110). Tasks 1/2/3 now contain explicit span-Error + span-exported assertions. Coverage table FR-6/AC-1/AC-3/AC-7 updated and accurate.
2. **RESOLVED** — Task 3 (AC-10) now cites the Observability test, requires a real `BrighterTracer` + `InstrumentationOptions.All` + exporter, reads `StatusDescription`, and asserts equality with the recording double's captured `Description`. The null-tracer impossibility is called out.
3. **RESOLVED** — New "Test infrastructure (A)" block defines `RecordingMessageConsumerAsync : IAmAMessageConsumerAsync` / `RecordingMessageConsumer : IAmAMessageConsumerSync`, built in Task 1, reused by Tasks 2/3/4/5/8/9 as the single reject-vs-ack + reason capture. Both interfaces verified to exist with the claimed members; the pump reaches the consumer via `Channel.RejectAsync(message, reason)` (`Proactor.cs:474-481`). Task 8 no longer defers — "Reuse the `RecordingMessageConsumer(Async)` built in Task 1 — no NEW double needed."
4. **RESOLVED** — Corrected citation `When_an_unacceptable_message_is_recieved_async_and_there_is_an_imc.cs:55` (class `AsyncMessagePumpUnacceptableMessageInvalidMessageChannelTests`) is accurate (line 55 has `invalidMessageTopic: _invalidMessageKey`). The wrong file now appears only as an explicit anti-reference ("NOT in …"), not as Task 1's mirror.
5. **RESOLVED** — Tasks 1/2 now name an unambiguous RED gate (recording double records exactly one `Reject(Unacceptable)` and zero `Acknowledge` — fails today because the pump acks), with instruction to confirm the RED is driven by the missing reject. Verified the production catch blocks (`Proactor.cs:374-379`, `Reactor.cs:339-344`) currently lack `RejectMessage`/`continue`, so the RED is genuine.

## Findings (round 2)

No findings at or above threshold. Two sub-threshold nits were identified and have been applied to tasks.md:

### 1. Task 3 line-range citation "95-110" loosely enclosed the actual reads at 109-110 (Score: 30) — FIXED
Tightened to "line 95 `ForceFlush()` then lines 109-110 for reading `activity.Status`/`StatusDescription`." The 33-40 exporter-wiring citation was already exact.

### 2. Async recording-double described with sync member names "Receive"/"Acknowledge" (Score: 25) — FIXED
Clarified to `ReceiveAsync`/`AcknowledgeAsync` for the async double, `Receive`/`Acknowledge` for the sync double. The interface names were already correct, so this was wording only.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 (both applied) |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

## Fresh full-pass verification (all passed)

- **Coverage**: Every FR maps to a task (FR-1→T1, FR-3→T2, FR-5→T6/T7, FR-6→T1/T2/T3, FR-7→T1/T2/T8, FR-8→T4/T5; FR-2/FR-4 correctly omitted as tombstones). NFR-1→T9, NFR-2→T4/T5/T8, NFR-3 (no-task by design), NFR-4→recording double across T1-9. AC-1..AC-12 all mapped; AC-9 correctly flagged optional/non-gating (T10). All seven ADR-0061 decisions mapped.
- **New codebase claims**: `InMemoryMessageConsumer` Unacceptable+no-IMQ+no-DLQ returns `true` without enqueuing (228-235); `SpyExceptionCommandProcessor` exists; catch-all blocks at `Proactor.cs:380` / `Reactor.cs:345`; all `FailingEventMessageMapper(Async)` / `MyFailingMapperEvent` doubles exist; Task 6 references all verified (`MP_LIMIT_EXCEEDED` at `MessagePump.cs:50`).
- **TDD coherence**: Tasks 1/2 have a real RED; Tasks 3-9 honestly framed (Task 3 RED-until-1/2-land; 4/5/7/9 characterization "expected GREEN, if RED investigate"; Task 6 strengthens existing assertions).
- **Internal consistency**: File/class names and the two infra mechanisms (A/B) used consistently across tasks and the coverage table; no task contradicts the infra blocks.

---

## Round 1 findings (2026-06-01) — historical, all resolved above

1. (85) AC-1/AC-3/AC-7 "span status Error and span ended" claimed covered but no task asserted it; every cited mirror built the pump with a null tracer. → Resolved by infra mechanism (B) + span assertions in Tasks 1/2/3.
2. (80) Task 3 (AC-10) required reading the span-status string but gave no tracer wiring and inherited the null-tracer lineage. → Resolved; Task 3 now wires BrighterTracer + exporter.
3. (72) Tasks 1/2/3/8/9 needed to capture the `MessageRejectionReason` but no recording consumer existed; mechanism deferred to Task 8. → Resolved by infra mechanism (A), decided up front in Task 1.
4. (65) Task 1 cited the wrong mirror for the `invalidMessageTopic` pattern. → Resolved; corrected to the `..._and_there_is_an_imc` files.
5. (62) Tasks 1/2 conflated strengthen-a-passing-test + implement with no clean RED. → Resolved; explicit RED gate named.
