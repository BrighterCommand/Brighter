# Review: tasks — 0038-outbox-stale-message-telemetry

**Date**: 2026-10-22
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Review history for the task breakdown of this spec. Pass 1 PASS (0 at or above 60); pass 2 PASS (0); pass 3 PASS
> (0). Pass 4 NEEDS WORK (1: T6.4 would be green before its implementation). Pass 5 NEEDS WORK (1: the mutation
> named by T12.1 does not compile). Pass 6 NEEDS WORK (4: T10.5 and T10.6, T8.23, T11.4, and T9.11 and T9.12).
> Pass 7 NEEDS WORK (3: T10.5, T11.5 and T8.23). Pass 8 NEEDS WORK (2, both on T11.5). Pass 9 NEEDS WORK (1: T10.1
> read the stop token synchronously, before the loop could observe the stop). Pass 10 PASS (3 findings, 0 at or
> above 60); its findings were then fixed, and T11.2 and T11.3 were reworked so that their mutation fails the test
> without hanging it. Pass 11 PASS (7 findings, 0 at or above 60), recorded below.

## Findings

### 1. T8.1 implements the row 3 coalescing with no test that drives it (Score: 40)

T8.1 asks for the row 3 handling, `Skip(count, M - 1)` with one Warning, but no test makes more than one due time pass between two wakes. The task says so and hands it to the review in T8.25.

**Evidence**: tasks.md:663, "A fake clock cannot make row 3, so no test drives it, and T8.25 reviews it". AC-8's coalescing clause allows a review in place of a test.

**Recommendation**: Accept as written. If the review in T8.25 finds the grid code hard to check, extract the grid computation into a pure helper and test that instead.

---

### 2. T8.1, T9.1 and T10.1 put a lot of implementation behind one behaviour (Score: 35)

Each of these tasks has one test, but its implementation list covers the loop start, the timer helper, the stop task, the yield and the count start (T8.1), the deadline, the abandonment and the cancellation off the loop thread (T9.1), and the stop path through rows 1, 5 and 6d (T10.1). Later CHARACTERISE tasks lock most of the parts in with mutations, and the review tasks T8.25, T9.21 and T10.11 check the parts that no test drives, such as row 3 (finding 1), but the first green step is large.

**Evidence**: tasks.md:660-668 (T8.1), :971-975 (T9.1), :1235-1239 (T10.1).

**Recommendation**: Optional. Either split a STRUCTURAL task out ahead of each, or let the gate question for these three tasks say that the implementation is larger than the test and which later tasks pin each part.

---

### 3. T2.11's mutation overlaps T2.3, and its "never throw" clause has no mutation (Score: 30)

The T2.11 mutation drops each requested topic whose count is 0. T2.3 already fails on that, since it checks that "refunds" is present with 0. The "never throw" clause has no mutation; the task explains that the `ConcurrentDictionary` enumerator gives no compiling mutation that makes it fail reliably.

**Evidence**: tasks.md:232 (T2.11 mutation and its note), :142 (T2.3, "refunds" is present with 0).

**Recommendation**: Accept. The clause guards a later change of the collection, and the note at :232 says so.

---

### 4. T13.1 is a CHARACTERISE task that also creates two base classes and a setup class (Score: 28)

T13.1 adds `OutstandingCountOutboxBaseTests<TTransaction>`, `OutstandingCountCancellationOutboxBaseTests<TTransaction>` and `OutstandingCountCancellationSetup<TTransaction>`. The cancellation base and setup are not used until a later task.

**Evidence**: tasks.md:1477-1480, the three classes at :1478-1479.

**Recommendation**: Optional. Move the cancellation base and setup to the first task that uses them, or keep them here and say in the gate question that they arrive early.

---

### 5. The GENERATE tasks T13.7 to T13.13 also write their providers by hand (Score: 25)

Each of these tasks forbids editing a generated file, and none of them edits one, but each also implements the provider interface by hand before it regenerates. T13.12 also sets two keys in both DynamoDB `test-configuration.json` files, and T13.13 also gives Firestore a `Configure` action for its emulator. The "Conformance templates" notes at :49 say this for all seven, but the tag alone reads as if the task only runs the generator.

**Evidence**: tasks.md:49, "Write the provider by hand, then regenerate the project"; :1564 (T13.7); :1642-1643 (T13.12, the providers and both `test-configuration.json` files); :1660-1662 (T13.13, the three providers and the Firestore configuration).

**Recommendation**: Accept. The notes at :49 and each task already say which files are written by hand and which are rendered.

---

### 6. CI stays red from T13.3 to T13.17, held off only by an instruction (Score: 22)

From T13.3 the generated-tree audit reports Missing and the build job fails; from T13.13 the jobs with an unmarked Gap fact fail. Only the instruction not to push between T13.3 and T13.17 keeps this off the PR.

**Evidence**: tasks.md:52, "Do not push between T13.3 and T13.17."

**Recommendation**: Accept. The window is local, and the instruction is in the "Conformance templates (Phase 13)" notes of "How to use this list".

---

### 7. T6.3's "no count call" clause covers only the first case of its theory (Score: 20)

The USE COMMAND says the start "makes no count call" for either missing registration, but the test checks it only when the outbox is registered and the producers' configuration is not.

**Evidence**: tasks.md:516 (USE COMMAND), :522, "in the first case, after the throw, fake time advances by I, and the double records no call of either count".

**Recommendation**: Accept, since with no outbox registered there is nothing to count. Optionally narrow the USE COMMAND to match.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 7 |

**Total findings**: 7
**Findings at or above threshold (60)**: 0
