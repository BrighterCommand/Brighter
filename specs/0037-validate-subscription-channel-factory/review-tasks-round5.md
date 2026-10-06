# Review: tasks (round 5) — 0037-validate-subscription-channel-factory

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address these before approving.

> Main-agent verification: finding 1 was checked before this file was written. tasks.md:941 says
> "By the preamble's step 3 that is not the right reason". Preamble step 1 (:18) treats any failure
> on arrival as RED; step 3 (:20) governs only step 2's mutation path. implement.md:199-201 requires
> RED "on arrival, or, for a `CHARACTERISE` task, under its named mutation" before implementation
> in `review-after`.

## Round-4 fix verification

| R4 # | Fixed? | New defect / stale copy? |
|---|---|---|
| 1 (task 7 mutation fails only under AC-4) | Yes. tasks.md:146 pins the test to AC-4, and the configuration is described correctly against requirements.md:339-342: AC-3's setup with `sub-a.ChannelFactory` null, a `NonMatchingChannelFactory` default, one Error. The "Why AC-4" bullet (:149) is right on the facts. AC-2 gives zero findings (req :329-332). AC-3's factory is never back-filled, because `DispatchBuilder.cs:146` only fills null factories. The mutation trace fails the byte-identical assertion (table below). The cross-reference table and task 13's reference to task 7 (:429) are still consistent. | No defect. There is one wording nit: the bullet reads AC-5's "any configuration" as "any one of" (→ 4). |
| 2 (task 54 speculative loader / no-op mutation) | Yes on the substance. The loader moves into file 2's cycle. The mutation now names `tests/Paramore.Brighter.AWS.Tests/test-configuration.json`, so it is deterministic. The note that the helper and loader count as production code reaches ralph's sub-agent, because Step 4 item 1 hands it the full task text verbatim. "5 of the 17 configurations" is correct: 14 files exist today, 3 are added and 12 are configured, which leaves DynamoDB, DynamoDB.V4, MongoDb, MySQL and Sqlite without a section. | **New defect → 1.** The fix says file 2's compile-error RED "is not the right reason", which misreads the preamble. That leaves file 2 with no valid RED before GREEN under `/spec:implement` review-after and under the gate wording. With no file green on arrival, the `CHARACTERISE` label no longer fits: task 2, which uses the same pattern, is `TEST + IMPLEMENT`. **Stale copy outside tasks.md → 2** (PROMPT.md:79). |

## Task 7 and task 54 mutation trace

| Task / file | RED on arrival? (reason) | Mutation | First assertion to fail | Verdict |
|---|---|---|---|---|
| 7 (AC-4 config) | No. Task 6's `ChannelFactory ?? default` and the `Type`-reduced rendering already give invariance. | Provenance clause: `(its own channel factory)` if `subscription.ChannelFactory != null`, else `(the default channel factory)` | Before back-fill the factory is null, so the message says "(the default channel factory)". After back-fill the same NonMatching instance is non-null, so it says "(its own channel factory)". Both runs still give 1 Error, so count and `Source` pass. **The byte-identical `Message` assertion fails.** | OK |
| 54 file 1 (synthetic) | Yes. The helper is missing (compile error), then a stub returns nothing and the B/C assertion fails. | None needed | n/a | OK |
| 54 file 2 (real tree) | Compile error only (no loader). After the loader is written it passes, because task 52 configured all 12. | Loader drops the `GatewayConformance` section of `AWS.Tests/test-configuration.json` (post-GREEN guard check) | The reports-nothing assertion fails with `Paramore.Brighter.MessagingGateway.AWSSQS` unconfigured. The comparison is exact: AWS.V4's type sits in namespace `…AWSSQS.V4` (`src/…AWSSQS.V4/SqsSubscription.cs`), which is not equal to `…AWSSQS`. All 12 configured types sit in their assembly's root namespace (verified). A prefix implementation would already fail the unmutated real tree, because AWSSQS would be named twice, so it cannot hide the mutation. | The mutation is sound. The problem is where RED is placed in the workflow (→ 1). |

## Findings

### 1. Task 54 file 2 declares its only pre-GREEN RED invalid, contradicting preamble step 1, the gate wording and implement.md's review-after rule. The `CHARACTERISE` label no longer fits (Score: 62)

The round-4 fix added: "File 2 is RED on arrival **only as a compile error**… By the preamble's step 3 that is not the right reason." Preamble step 3 (tasks.md:20) only covers step 2's path, the failure of a green-on-arrival test under its mutation. Step 1 (:18) says "If it **fails**, that is RED — continue as a normal test-then-implement task." Task 2 (:70), which uses the same pattern, says only that "RED on arrival is only a compile error; this check is what shows the guard catches the defect". It does not call that RED invalid. Task 54 now rules out the only RED it can have before implementation, and the right-reason RED can only appear after GREEN, because the mutation targets a loader that does not yet exist. The consequences diverge by path:
- `/spec:implement` in review-after (implement.md, "When the gear is `review-after`" step 1): "Confirm RED is already proved: the test ran and failed for the right reason — on arrival, or, for a `CHARACTERISE` task, under its named mutation… If it has not, go back and prove it. `review-after` never licenses writing implementation first." File 2 cannot satisfy this, so an agent either stalls or overrides the task text.
- The ⛔ line (:942), "once RED is observed … before implementing if it was not", now has two readings for file 2: stop after the compile error (which the task says is not RED), or stop after the guard check (which comes after implementing).
- **[workflow]** Ralph's Characterisation cycle (:279-280) takes the "Fails →" branch and records `"failed on arrival: <assertion>"`, but a compile error has no assertion. The guard check exists only in the task text, not in the cycle the sub-agent is told takes precedence (ralph :230-232). The sub-agent will probably follow the task text, and the `RED_EVIDENCE` mutation form is achievable, but nothing in the cycle requires either.
- The label: neither file is now green on arrival in the D14 sense, so the 🔁 "Characterisation (file 2 only)" heading describes no characterisation. Task 54 is task 2's shape, and task 2 is `TEST + IMPLEMENT` with a "Guard check, after GREEN" bullet. The fix even calls it "(the task-2 pattern)".

**Evidence**: tasks.md:941 ("By the preamble's step 3 that is not the right reason"); tasks.md:18-20 (preamble steps 1 and 3); tasks.md:70 (task 2's guard-check wording); tasks.md:929 (`54. CHARACTERISE`); implement.md review-after step 1; ralph-implement.md:279-280 and :325.

**Recommendation**: Relabel task 54 `TEST + IMPLEMENT`. Delete the "By the preamble's step 3…" sentence. Replace the 🔁 bullet with a "Guard check, after GREEN (D14 mechanism)" bullet worded like task 2's: the compile-error RED on arrival is the normal RED, and the AWS.Tests mutation shows that the guard catches the defect. Use task 2's plain gate ("before implementing"). Keep the note that the helper and loader count as production code, since the mutation still targets test-project code. Update the label counts to 28 `TEST + IMPLEMENT` and 18 `CHARACTERISE` wherever they are stated.

---

### 2. PROMPT.md still describes task 54 as "mixed: one file RED, one green" (Score: 40)

PROMPT.md's label table was written before the round-4 fix. After the fix, task 54 has no file green on arrival. PROMPT.md:108-111, updated in the same round, already says file 2's RED is a compile error, so the two lines in one file now contradict each other. PROMPT.md carries state across sessions, so the next session inherits the stale line.

**Evidence**: PROMPT.md:79: "`CHARACTERISE` | 19 | … 41 and 54 are mixed: one file RED, one green".

**Recommendation**: Correct the row alongside finding 1: remove 54 from `CHARACTERISE` and from the "mixed" note, and adjust the counts.

---

### 3. Task 54 file 1 does not pin exact namespace-vs-directory equality (Score: 25)

File 1's synthetic gateways `[A, B, C]` cannot tell equality from a prefix (`StartsWith`) comparison. Only file 2's real tree catches a prefix implementation, because `…AWSSQS.V4` would then also name `…AWSSQS` twice. This is caught, so it is not a live risk to the audit or to the mutation. But the property that "makes the comparison exact" (:945) is never asserted on synthetic inputs, and a future tree with no prefix-sharing gateway pair would stop catching it.

**Evidence**: tasks.md:938 (file 1 cases), :940 ("comparing the directory name with the namespace"), :945.

**Recommendation**: Optionally make synthetic gateway A's name a prefix of another gateway's name, for example `X` and `X.V2`.

---

### 4. Task 7's "Why AC-4" reads AC-5's "any configuration" as "any one of" (Score: 20)

"AC-5 allows any of the three" treats AC-5's universal "Given any configuration from AC-2, AC-3 or AC-4" (requirements.md:345) as a free choice of one. The choice is defensible, because AC-2 and AC-3 are invariant by construction, as the bullet itself explains. Only the phrasing is off.

**Evidence**: tasks.md:149.

**Recommendation**: Reword to something like "AC-5 quantifies over all three; AC-2 and AC-3 hold by construction, so only AC-4 can discriminate", or leave it as is.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 3 |

**Total findings**: 4
**Findings at or above threshold (60)**: 1

Self-count check against the filesystem: 59 tasks. Labels are 27 `TEST + IMPLEMENT`, 19 `CHARACTERISE`, 1 `GENERATE`, 4 `SETUP`, 3 `STRUCTURAL`, 2 `DOC` and 3 `VERIFY`, which sum to 59. There are 46 `/test-first` commands and 47 ⛔ gates. All match the stated figures. tasks.md itself contains no label-summary line; the only label summary is in PROMPT.md (finding 2).
