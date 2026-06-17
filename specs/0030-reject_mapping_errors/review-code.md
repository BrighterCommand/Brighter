# Review: code — 0030-reject_mapping_errors

**Date**: 2026-06-02
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Proactor RED test and implementation committed together — TDD gate not visibly honored for the async pump (Score: 45)

CLAUDE.md mandates an explicit test-first workflow: commit the failing (RED) test, obtain approval, *then* implement. For the **Reactor**, this was done correctly — `ef187fb12` ("test: Reactor mapping failure should reject (RED)") precedes the implementation `89f727930`. For the **Proactor**, there is no separate RED commit: `967277640` ("feat: Proactor rejects...") atomically rewrites the existing test (`When_a_message_fails_to_be_mapped_to_a_request_async.cs`, which on master only asserted `Assert.Empty(_bus.Stream(_routingKey))`) into a reject-asserting test *and* changes `Proactor.cs` in the same commit. The new behavior is genuinely tested and passes, but the RED→approve→GREEN separation is not observable in history. This also mixes a behavioral test change with a behavioral code change in one commit.

**Evidence**: `git show --stat 967277640` lists `Proactor.cs`, the Proactor test file, and `tasks.md` together. Master's version of that test asserted only `Assert.Empty(_bus.Stream(_routingKey))` (line 56), which is satisfied by both ack-delete and reject — i.e., it did not pin the new behavior until this commit. Contrast `ef187fb12` (Reactor test, RED) landing before `89f727930` (Reactor impl).

**Recommendation**: Process nit only — the behavior is correctly covered. For future parity, land the Proactor RED test in its own commit before the implementation, as was done for the Reactor.

---

### 2. "TEST + IMPLEMENT" task labelling conflicts with the mandatory test-first split (Score: 30)

`specs/0030-reject_mapping_errors/tasks.md` labels every behavioral task "**TEST + IMPLEMENT: ...**" (lines 85, 105, 123, 138, 152, 166, 180, 195, 210). This phrasing invites bundling the test and implementation into one step/commit, which is what happened for the Proactor (Finding 1). CLAUDE.md's TDD section treats TEST and IMPLEMENT as separate gated steps.

**Evidence**: `tasks.md:85` `- [x] **TEST + IMPLEMENT: Proactor rejects a mapping failure as Unacceptable...**`.

**Recommendation**: Documentation nit — split future task entries into discrete TEST and IMPLEMENT items, or annotate that the test must be committed and approved before implementing.

---

### 3. tasks.md checkbox state lags committed work for Tasks 8 and 9 (Score: 20)

Tasks 8 (no-IMQ, AC-8/FR-7) and 9 (async/sync parity, AC-11/NFR-1) are unchecked (`[ ]`) in `tasks.md` (lines 195, 210), yet their test files are committed (`768d93e7d`, `44b2eef1c`) and pass. Pure bookkeeping drift.

**Evidence**: `tasks.md:195` and `:210` show `[ ]`; corresponding test files exist and pass.

**Recommendation**: Tick the boxes for Tasks 8 and 9.

---

### 4. AC-11 "parity" is two independent single-pump tests, not a direct comparison (Score: 15)

The AC-11 parity tests (`When_the_mapping_reject_path_is_compared_across_pumps[_async].cs`) each assert only one pump routes to the IMQ; they do not assert equality of the reason/description across the two pumps in a single test. Parity is established by the two tests being structurally identical mirrors rather than by a shared assertion. This is consistent with the existing mirror-test convention in this codebase and the implementations are verifiably identical (Reactor drops only `await`), so risk is negligible.

**Evidence**: `When_the_mapping_reject_path_is_compared_across_pumps_async.cs:80-89` asserts `Assert.Single(_bus.Stream(_invalidMessageKey))` / `Assert.Empty(_bus.Stream(_routingKey))` for the Proactor only; the Reactor mirror does the same for the sync pump.

**Recommendation**: Optional — the test-name "compared_across_pumps" slightly oversells a mirror pair; no functional change needed.

---

## Verification performed (no findings)

- **FR-1 / FR-3** (route mapping failure through `RejectMessage(Unacceptable) + continue`): present at `Proactor.cs:374-382` and `Reactor.cs:339-347`, matching the ADR implementation shape exactly and mirroring the adjacent `InvalidMessageAction` block.
- **FR-5** (guardrail preserved): `IncrementUnacceptableMessageCount()` retained on both paths; AC-5 asserts 3 rejects + `MP_LIMIT_EXCEEDED`; AC-6 asserts 100 rejects + not `MP_LIMIT_EXCEEDED`.
- **FR-6** (logging/tracing preserved): `Log.FailedToMapMessage`, `processSpan?.SetStatus(Error, description)`, and the `finally { EndSpan }` all retained.
- **FR-7 / NFR-2** (delegation, no transport branching, no consumer changes): no transport-type branch in the pump; AC-8 no-IMQ test shows reject falls back to the DLQ via `InMemoryMessageConsumer`; diff touches no transport consumer.
- **FR-8** (catch-all unchanged): verified byte-for-byte — the diff only touches the `MessageMappingException` blocks; the `catch (Exception)` body and bottom-of-loop acknowledge are untouched. AC-2/AC-4 regression guards confirm dispatch failures still acknowledge and do not reject.
- **C-5** (shared description local containing message Id; same string for span and rejection): the `description` local is reused for both `SetStatus` and `MessageRejectionReason`; AC-10 test asserts the rejection-bag string contains the span `StatusDescription` and both contain the message Id. `Thread.CurrentThread.ManagedThreadId` preserved verbatim per ADR.
- **A-2 / AC-12** (real translate path): mapping failures are induced via `FailingEventMessageMapperAsync` (throws `JsonException`) or a throwing transformer factory, both surfacing through `TranslateMessage`, which wraps in `MessageMappingException` — not hand-thrown.
- **Hygiene**: no `[Skip]`, no commented-out/empty asserts, one `[Fact]` per new file. The two untracked SQLite WAL files in `samples/` are pre-existing and unrelated.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 4 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0
