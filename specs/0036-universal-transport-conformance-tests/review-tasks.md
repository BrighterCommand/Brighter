# Review: tasks — 0036-universal-transport-conformance-tests

**Date**: 2026-07-21
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

_Round 4 (confirming review of the round-3 remediation). All five round-3 findings verified fixed; no new problems introduced by the edits._

## Verification of the five round-3 remediations

**1. (was Critical 90) `Brighter.sln` → `Brighter.slnx`.** LANDED. `ls *.slnx` shows `Brighter.slnx`; `*.sln` has no matches. Every solution-build reference now reads `dotnet build Brighter.slnx` (lines 350, 354, 727). No `dotnet build Brighter.sln` remains in the tasks doc.

**2. (was High 70) Phase 1 "generate everywhere" task.** LANDED. Structural test file `When_generating_everywhere_should_emit_skipped_canonical_suite_in_all_wired_projects.cs` now in Implementation files (line 352); RALPH-VERIFY (line 354) ends with `&& dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~When_generating_everywhere"` after the build — a build alone cannot see Skip markers.

**3. (was Medium 66) Broker-up decoupled.** LANDED and correctly ordered. Every Phase 2/3 + MQTT/RMQ.Sync step-3 RALPH-VERIFY uses `{ docker compose -f <file> up -d || true; } && dotnet test …` (correct bash: space after `{`, `; }` before close; always returns success). The generator build + per-project regenerate are `&&`-joined BEFORE the brace group, so a generator/regenerate failure still hard-fails the task; only broker-up is best-effort.

**4. (was Medium 54) Config-scoped `dotnet test` filters.** LANDED, verified against real namespaces: `…AWS.Tests.MessagingGateway.SqsStandard.Reactor` / `.SqsFifo.` / `.SnsStandard.` / `.SnsFifo.`; `…Gcp.Tests.MessagingGateway.Pull.` / `.PullOrdering.` / `.Stream.` / `.StreamOrdering.`; `…RMQ.Async.Tests.MessagingGateway.Classic.` / `.Quorum.`. Both RMQ.Async tasks scoped (lines 526, 538). Trailing dot disambiguates `Pull`≠`PullOrdering`, `Stream`≠`StreamOrdering`. Hand-written siblings live in different namespaces (`MessagingGateway.Sqs.Standard`, bare `MessagingGateway`) and are correctly excluded. Single-config transports stay `~MessagingGateway`; Kafka Phase 2 stays project-wide (proves both configs) — both correct.

**5. (was Low 48) Phase 0 exhaustion-template edit.** LANDED. Task now unambiguously passes `deadLetterRoutingKey:` explicitly and states "Do NOT drop the DLQ args" (lines 55, 59–60). Trap verified real: bare positional `true` as 4th arg to `CreateSubscription` at line 48 of the Reactor exhaustion `.liquid`; interface template still declares `bool setupDeadLetterQueue = false` at line 45.

## Additional grounding checks (all confirmed)

- SkipTest legacy gate branches at 122/127/132/145; retained gates untouched.
- Config properties at 91/96/106; `HasSupportToValidateBrokerExistence` retained at 101.
- All four legacy template filenames exist under `Templates/MessagingGateway/Reactor/`.
- All referenced compose files exist at repo root; no `docker-compose-gcp.yaml`, no ASB compose — matching the tasks' explicit statements.
- FR/AC coverage: every live FR (FR-1, FR-2, FR-4…FR-17, FR-19, FR-20, FR-21, FR-22) referenced by a task. ADR 0066/0067 decisions all map to tasks.

## Findings

No findings at or above threshold.

Low-scored observations (below threshold, informational only):

### 1. AC-14 and AC-15 covered in substance but not cited by number (Score: 25)

AC-14 (FR-14 both-variant parity) and AC-15 (NFR-1 naming) numbers do not appear in ralph-tasks.md, but their substance is fully exercised (FR-14 cited e.g. line 355 + "Both variants emitted" on every canonical task; NFR-1 cited throughout). Not a coverage gap — only a missing label.

**Recommendation**: Optionally add "AC-14"/"AC-15" to the References of the both-variants and naming assertions for traceability. No behavioural change.

### 2. AWS/RMQ ledger greps unanchored where GCP's are anchored (Score: 20)

GCP row greps are anchored (`grep -E 'GCP / Pull[[:space:]]*\|'`); AWS/RMQ.Async row greps are plain substring. Currently safe (row labels distinct, none a proper prefix that cross-matches — `AWS / SqsStandard` is not a substring of `AWS.V4 / SqsStandard`). No live defect.

**Recommendation**: For consistency/future-proofing, optionally anchor AWS/RMQ greps the same way. Not required for correctness today.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0
