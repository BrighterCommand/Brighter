# Review: tasks — 0037-validate-subscription-channel-factory (round 3)

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

> Main-agent verification: finding 1 checked — `DispatchBuilder.cs:146-149` back-fills
> `connection.ChannelFactory = _defaultChannelFactory`, the same instance the rule captured. Finding
> 2's `SharedGenerator` claim checked — `tools/Paramore.Brighter.Test.Generator/Program.cs:90` runs it
> unconditionally.

## Round-2 fix verification
- **1 ([workflow] characterisation shape)**: HOLDS WITH NEW DEFECT — rules do not reach the sub-agent (→ 5), leftover check has no baseline (→ 7), `GENERATE` still broken in the sub-agent cycle (→ 2).
- **2 (unmarked green-on-arrival)**: HOLDS WITH NEW DEFECT — task 7's mutation cannot fail (→ 1); 20/22 name the wrong assertion (→ 3); task 54 missed (→ 4).
- **3 (task 53 generated shape)**: HOLDS WITH NEW DEFECT — label and dispatch row correct, but the sub-agent cycle and "must never drop" still route a green generated test to `ALREADY_COMPLETE`; `SharedGenerator` helpers trip the leftover check (→ 2).
- **4 (stale task-1/2 refs)**: HOLDS.
- **5 (task 43 unrelated exception)**: HOLDS; small exception-type inaccuracy (→ 11).
- **6 (task 2 guard never observed)**: HOLDS WITH NEW DEFECT (minor) — one reading of the mutation does not compile (→ 10).
- **7 (labels)**: HOLDS — every preamble label has a ralph row.
- **8 (task 29)**: HOLDS.
- **9 (`Namespace` render model)**: HOLDS.
- **10 (transient reds 47/48)**: HOLDS.

## Characterisation mutation audit

| Task | Green on arrival? | Mutation fails the named assertion? |
|---|---|---|
| 6 | yes | yes — one-Error |
| 7 | yes | **no** → 1 (same instance both runs) |
| 10 | yes | yes |
| 11 | yes | yes; companion stays green |
| 12 | yes | only via reflection into a private field → 6 |
| 17 | yes | yes |
| 20 | conditional | **no** — ends-with fails first → 3 |
| 22 | conditional | **no** — same → 3 |
| 23 | yes | yes |
| 24 | conditional | yes, if only the body's handed clause is mutated |
| 25 | yes | yes, both |
| 28 | yes | yes |
| 29 | yes | yes |
| 30 | yes | yes |
| 41 | sound yes; ANE RED on arrival | yes (mixed shape → 5) |
| 43 | yes | yes (surfaces as `TargetInvocationException` → 11) |
| 46 | yes | yes |
| 48 | conditional | **no** — `Sweep` throws before any assertion → 3 |
| *(sweep)* 54 | **yes after task 52, unmarked** | none named → 4 |

## Findings

### 1. Task 7's mutation cannot fail the test: the back-fill writes the *same* default instance (Score: 75)

Task 7 (:148): "interpolate `RuntimeHelpers.GetHashCode(effectiveFactory)` … the back-filled and resolved-default runs are separate instances". They are not: `DispatchBuilder.cs:146-149` writes `connection.ChannelFactory = _defaultChannelFactory`, the instance the rule captured. Identity hash equal in both runs → messages byte-identical → test green under the mutation (ralph: `FAILED`). A sub-agent trusting "separate instances" may back-fill a *new* instance, and the test would no longer simulate the real back-fill AC-5 is about.

**Evidence**: tasks.md:146, :148; DispatchBuilder.cs:146-149; requirements.md:344-347.

**Recommendation**: Mutation that separates the two routes, e.g. render a provenance clause ("(its own channel factory)" vs "(the default channel factory)") depending on whether `subscription.ChannelFactory` is non-null. Delete "separate instances"; state the test back-fills the *same* default instance.

---

### 2. [workflow] `GENERATE` still cannot run under ralph; implement.md's `review-after` branch demands a RED it can never have (Score: 72)

- ralph sub-agent cycle has no `GENERATE` branch: "Write the test file … confirm it FAILS … revise the test … or `ALREADY_COMPLETE`"; the only exemption is `CHARACTERISE`. Step 4 prompt items 1-5 do not include the dispatch-table row.
- "What this loop must never drop" (:67-71) exempts only `CHARACTERISE`, contradicting the Generated row.
- The verify-command formula cannot be formed from `tests/<each of the twelve>/…` (:907).
- The leftover check blocks task 53: `SharedGenerator` (Program.cs:90) renders helper files into the three new projects (:917), outside `TEST_FILES`.
- implement.md `review-after` (:194-198) requires RED on arrival or via mutation — `GENERATE` has neither; the approval question (:178) still asks "proceed to make this test pass?".

**Evidence**: ralph-implement.md:67-71, Step 4, RED bullets, leftover paragraph; implement.md:178, :194-198; tasks.md:907, :914-917.

**Recommendation**: [workflow] ralph: `GENERATE` sub-agent branch (run generator, never hand-write, never `ALREADY_COMPLETE`, return all rendered files); exempt in "must never drop"; take verify commands from the task text. implement.md: `GENERATE` review-after step and approval question. tasks.md task 53: state whether the `SharedGenerator` helper files are committed with the conformance files.

---

### 3. Tasks 20, 22 and 48 name an assertion that is not the one the mutation fails (Score: 60)

- Task 20: under the mutation T1 ends `…, or use a subscription type whose ChannelFactoryType is {F}`, so the **ends-with** assertion (:346) fails first, not the named no-occurrence (:348).
- Task 22: same via T2 (:361, :374-376).
- Task 48: `Sweep` throws an unhandled `ArgumentException` before any assertion, not "fails on its entry-present assertion" (:819).

A sub-agent obeying ralph's "mutation does not produce the named failure → `FAILED`" fails three tasks whose tests do catch the defects.

**Evidence**: tasks.md:20, :332, :346-348, :361, :374-376, :817-819.

**Recommendation**: Restate the observed failures (20/22: ends-with; 48: `Sweep` throws the constraint `ArgumentException`), or reorder assertions.

---

### 4. Task 54 is green on arrival after task 52 and is still labelled `TEST + IMPLEMENT` (Score: 60)

Twelve gateway directories, all configured by task 52, so neither claimed failure ("unconfigured fails; configured twice fails", :933) exists in the real tree. "Implementation should" names no production file; RED is at most a compile error. Under ralph → "revise … or `ALREADY_COMPLETE`". Attended, nothing says how the two failure cases are exercised — divergent implementations likely.

**Evidence**: tasks.md:927-940; 12 `src/Paramore.Brighter.MessagingGateway.*` dirs; `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/`.

**Recommendation**: Relabel `CHARACTERISE` with named mutations, or specify an auditor taking its inputs, its location, and synthetic unconfigured/twice-configured inputs.

---

### 5. [workflow] The per-file and mixed-shape `CHARACTERISE` rules never reach the ralph sub-agent (Score: 55)

Per-file RED, RED-on-arrival → Behavioural, `CHARACTERISED` vs `GREEN`, `FAILED` on mismatch live only in the main-agent dispatch table. The sub-agent cycle's characterisation text is singular. For task 41 a sub-agent may return `CHARACTERISED` with the guard unimplemented or in `IMPL_FILES` (then a contract violation). testing.md's "stop and ask" contradicts "Do not ask the user anything". The verify command is singular; task 41 has two files.

**Evidence**: ralph-implement.md Characterisation row, Step 4, sub-agent RED bullet, CHARACTERISED step 1; testing.md (306e92075); tasks.md:688-694.

**Recommendation**: [workflow] copy the rules into the sub-agent cycle; mixed case returns `GREEN` with `RED_EVIDENCE`; one verify filter per test file; "stop and ask" means `FAILED` when unattended.

---

### 6. Task 12's mutation is only writable by reflecting into `CombinedChannelFactory`'s private `_factories` (Score: 52)

The rule sees the outer composite; `FactoryTypes` exposes `Type`s only; inner instances are private (`CombinedChannelFactory.cs:14`) in another assembly.

**Recommendation**: e.g. combined arm `candidates.Any(t => t == declared || typeof(CombinedChannelFactory).IsAssignableFrom(t))`; fails on the one-Error assertion.

---

### 7. [workflow] The leftover-mutation check exempts pre-existing untracked files but nothing records that baseline (Score: 48)

No step snapshots `git status --porcelain` before dispatch; this repo has pre-existing untracked files; a tracked file already modified before the run is not exempted at all. Revert is unverifiable when the mutated file is also in `IMPL_FILES` (task 41).

**Recommendation**: [workflow] record a porcelain baseline before Step 4 and diff against it; require a re-run green after revert, listed in `RED_EVIDENCE`.

---

### 8. [workflow] test-first.md's gate and RED-first bullet not adapted; ADR 0071 over-claims (Score: 40)

test-first.md's approval gate asks only "proceed to implement"; its RED-first bullet has no characterisation clause; ADR 0071 says both shapes are defined in test-first.md, but it has no `GENERATE` text.

**Recommendation**: [workflow] add the characterisation gate question and RED-first clause; correct ADR 0071's sentence.

---

### 9. [workflow] The attended characterisation path skips the full regression suite (Score: 40)

implement.md :178-181 sends a characterisation test "straight to Step 5", bypassing "Run All Tests"; testing.md's four steps omit it too.

**Recommendation**: [workflow] add "run the full suite for the affected project" after the revert in implement.md and testing.md.

---

### 10. Task 2's guard mutation has a reading that does not compile (Score: 40)

Capturing `factories` in the lazy getter raises CS9124 (error under `TreatWarningsAsErrors`, `src/Directory.Build.props:17`); only the get-only auto-property initialiser (ADR 0072's defect form, :530-531) compiles, and it throws at construction.

**Recommendation**: Name the initialiser form; say the failure surfaces at construction.

---

### 11. Minor accuracy nits in 43 and 41 (Score: 25)

- Task 43: `Activator.CreateInstance` wraps the constructor's exception in `TargetInvocationException` (:740).
- Task 41: mixed shape returns `GREEN`, whose commit template has no `RED_EVIDENCE` line.

---

## Carried forward (below threshold, not re-scored)
- Round-1 7: task 53 gives up natural RED — present (:914).
- Round-1 8: task 36 cites task 20 for the null-`D` clause (belongs to 13/14) — present (:617).
- Round-1 9: task 41 bundles two behaviours — present; now mixed shape (→ 5).
- Round-1 10: ADR 0072 step-2 ordering note — present (:1077).
- Round-2 lows 8, 9, 10 fixed; round-2 6 fixed apart from finding 10.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 5 |

**Total findings**: 11
**Findings at or above threshold (60)**: 4
