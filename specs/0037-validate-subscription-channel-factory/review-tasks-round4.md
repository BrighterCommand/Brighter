# Review: tasks (round 4) — 0037-validate-subscription-channel-factory

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

> Main-agent verification: findings 1 and 2 were checked on the filesystem before this file was
> written. `tasks.md:146` offers "AC-2/AC-3/AC-4". `DispatchBuilder.cs:146` back-fills only
> `Where(c => c.ChannelFactory == null)`. No `tests/*/test-configuration.json` has a
> `GatewayConformance` section today, so the section-bearing set after task 52 depends on the
> order the loader reads them. The reviewer's order put finding 3 (50) before finding 4 (55); they
> are listed in descending score order here.

## Round-3 fix verification

| R3 # | Fixed? | New defect / stale copy? |
|---|---|---|
| 1 (task 7 identity-hash mutation) | Yes. The provenance-clause mutation is sound, "same instance" is now stated, and :152 is consistent. | **New defect → 1.** The mutation only fails under AC-4. The task still lets the test pick AC-2, AC-3 or AC-4. Nit: :125 cites `DispatchBuilder.cs:146-148` and :146 cites `146-149` (→ 9). |
| 2 ([workflow] GENERATE under ralph) | Yes. The sub-agent has a Generated cycle. "Must never drop" exempts GENERATE (:70-72). The verify commands come from the task text (Step 4 item 3). implement.md has the GENERATE question and a review-after step. Task 53 commits the SharedGenerator helper files. | No new defect in the fix itself. implement.md's new "run the full suite for the affected project(s)" for GENERATE is only possible with brokers (→ 5). |
| 3 (tasks 20/22/48 name the wrong failure) | Yes. 20 and 22 now name ends-with. 48 names the `ArgumentException` thrown out of `Sweep`. | 48 (and 43) now name an exception, not an assertion. That conflicts with the preamble :21 and ralph's dispatch row, which say "the assertion the task names … not … an unrelated exception" (→ 7). Task 24, the same class, was not re-checked (→ 3). |
| 4 (task 54 green after task 52, mislabelled) | Relabelled `CHARACTERISE`. File 1 uses synthetic inputs. `GatewayConformanceAudit` is specified. | **New defects → 2.** File 2's green-on-arrival claim needs a speculative loader. The "first config it reads" mutation can be a no-op. The mutated code lives in a test project, but ralph's cycle says "production code, never the test". |
| 5 ([workflow] CHARACTERISE rules don't reach the sub-agent) | Yes. There is a per-file Characterisation cycle for the sub-agent, the mixed case returns `GREEN` with `RED_EVIDENCE`, and there is one filter per test file. "Stop and ask" now means `FAILED`. | **Stale copy → 4.** The return-format template still reads `"…; reverted"` without "re-run green". Step 5 now treats a missing "re-run green" as a contract violation in exactly the tasks-41/54 case. |
| 6 (task 12 needs private reflection) | Yes. `candidates.Any(t => t == declared \|\| typeof(CombinedChannelFactory).IsAssignableFrom(t))` compiles. It uses only `FactoryTypes` and fails the one-Error assertion. | None. |
| 7 ([workflow] leftover-check baseline) | Mostly. A porcelain baseline is recorded (Step 4), and the IMPL_FILES blind spot is acknowledged. | A tracked file already modified at baseline keeps the same porcelain entry when mutated, so the check still misses it (→ 8). |
| 8 ([workflow] test-first gate / ADR 0071) | Yes. The RED-first clause and the characterisation question are added, and the ADR 0071 sentence is corrected. | The new instruction sits inside the fenced AskUserQuestion template, between "Question" and "Options", and the options still say "Yes, implement the code" (→ 6). |
| 9 ([workflow] attended path skips the full suite) | Yes (implement.md :181, :206; testing.md step 4). | Same gateway-infrastructure caveat as → 5. |
| 10 (task 2 mutation doesn't compile) | Yes. It names the auto-property initialiser from `factories`. `factories` is not otherwise captured, so there is no CS9124. It throws at construction on the second `GetEnumerator()`. | None. |
| 11 (43 exception type; 41 GREEN without RED_EVIDENCE) | Yes on both: `TargetInvocationException` is named, and the GREEN template has a Characterisation line. | Mixed-shape GREEN commit prefix is unspecified (→ 8). |

## Characterisation mutation audit

| Task | Green on arrival? | Mutation | Fails named assertion first? | Verdict |
|---|---|---|---|---|
| 6 | Yes. Task 5 adds `ChannelFactory ?? default`. | Predicate returns `true` when `ChannelFactory` is null | Yes, one-Error (AC-3 config with a null factory falls back to NonMatching). | OK |
| 7 | Yes (effective factory reduced to a `Type`) | Provenance clause keyed on `subscription.ChannelFactory != null` | **Only with AC-4.** AC-2 gives zero findings in both runs. AC-3's factory is never back-filled (`DispatchBuilder.cs:146` only fills nulls). | **Defect → 1** |
| 10 | Yes (task 4's `IsAssignableFrom`) | Direct arm uses `==` | Yes, no-findings | OK |
| 11 | Yes (task 8's exact equality) | Combined arm uses `IsAssignableFrom` | Yes, one-Error. The companion assertion stays green. | OK |
| 12 | Yes | `… \|\| typeof(CombinedChannelFactory).IsAssignableFrom(t)` | Yes. Outer `FactoryTypes` = `[CombinedChannelFactory]`, so no finding and the one-Error assertion fails. | OK |
| 17 | Yes (rule reads no `RequestType`) | `if (s.RequestType is null) return true;` | Yes, one-Error | OK |
| 20 | Conditional | Drop the InMemory conjunct from T1 and order T1 first | Yes. T1 ends "…, or use …", so ends-with fails first. | OK |
| 22 | Conditional | Same for T2 | Yes, ends-with | OK |
| 23 | Yes (task 18's `DisplayName`) | `FullName` for generics | Yes. The first no-occurrence assertion (`Version=`) fails. | OK |
| 24 | Conditional | Render the handed type with `Type.Name` | **Ambiguous.** If `{F}` in T1 shares the handed display name (task 18 renders it once), the "contains both namespace-qualified names" assertion fails before the regex. | → 3 |
| 25 | Yes | (a) `entities.Reverse()` (`IEnumerable<T>`, LINQ, compiles, `PipelineValidator.cs:153`); (b) `Guid` suffix | Yes: (a) ordering, (b) byte-identical | OK |
| 28 | Yes | Remove the fifth registration | Yes. The host starts, then the reported-finding assertion fails. | OK |
| 29 | Yes | Remove `if (!enabled) return builder;` (`BrighterPipelineValidationExtensions.cs:60`) | Yes, no-results (`throwOnError: false` keeps the host up) | OK |
| 30 | Yes | Predicate returns `false` always | Yes, exactly-one | OK |
| 41 | Mixed. The sound file is green; the ANE file is RED on arrival. | Final `return null` → placeholder | Yes, null assertion | OK. The mutated file is also in IMPL_FILES, so → 4 applies. |
| 43 | Yes | Try `Activator.CreateInstance(type, true)` first | Task 43 runs **before** task 47's per-type catch exists, so an unhandled `TargetInvocationException` escapes `Sweep`. No assertion is reached, and the task names none ("fails because a constructor was invoked"). | → 7 (low) |
| 46 | Yes | Remove the `DeclaredOnly` conjunct | Yes, both-reported | OK |
| 48 | Conditional | Move `MakeGenericType` outside the catch | Throws the named `ArgumentException` before any assertion. It is the related exception and is stated as such. | → 7 (low) |
| 54 | File 1 RED on arrival. File 2 is claimed green, **but only if the loader was written during file 1 (speculative)**. | Loader skips the `GatewayConformance` section of "the first `test-configuration.json` it reads" | **Not reliably.** 5 of the 17 configs have no section, and enumeration order is unspecified. The mutated code is test-project code. | **Defect → 2** |

Mislabel check: I found no `TEST + IMPLEMENT` task that would be green on arrival (spot-checked 5, 9, 13-16, 18, 19, 21, 40, 44, 45, 47). The one `CHARACTERISE` file likely to be RED on arrival (a compile error) is task 54 file 2 (→ 2).

Counts check: 59 tasks. Labels: 27 TEST + IMPLEMENT, 19 CHARACTERISE, 1 GENERATE, 3 STRUCTURAL, 4 SETUP, 2 DOC, 3 VERIFY. There are 46 `USE COMMAND` lines (= 27 + 19) and 47 ⛔ gates (46 + task 53). Every `/test-first` task has exactly one gate. Dependencies 53 → 35, 55 → 53/54/35, 42 → 1 and 44 before 45 are all present.

## Findings

### 1. Task 7's new mutation fails only under AC-4, but the task still lets the test use AC-2 or AC-3 (Score: 70)

The round-3 fix replaced the identity-hash mutation with a route-provenance clause. That clause can only change a message that exists, on a subscription whose `ChannelFactory` actually goes from null to back-filled. Task 7 (:146) still says "a configuration from AC-2/AC-3/AC-4":
- **AC-2** matches, so both runs give zero findings. There is no `Message`, the mutation is invisible, and the byte-identical assertion passes.
- **AC-3** sets `sub-a.ChannelFactory` explicitly. `DispatchBuilder.cs:146` back-fills only subscriptions `Where(c => c.ChannelFactory == null)`, so there is nothing to back-fill and both runs render "(its own channel factory)".
- Only **AC-4** (null factory, mismatched default, one Error) makes the two runs differ.

AC-2 is listed first, so a sub-agent that picks it gets a test the named mutation cannot turn red, and ralph returns `FAILED`. Attended, the reviewer sees a "RED" that never happens. The same weakness was latent under the old mutation and was carried forward by the fix.

**Evidence**: tasks.md:146 ("a configuration from AC-2/AC-3/AC-4"), :148 (the mutation); requirements.md:329-342 (AC-2 no findings; AC-3 `ChannelFactory` set); `src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs:146-148`.

**Recommendation**: Pin task 7's test to the **AC-4** configuration. It is the only one with both a null `ChannelFactory` to back-fill and a `Message` to compare. Say why in the task. AC-5's "any of" is satisfied by choosing one.

---

### 2. Task 54 file 2: the green-on-arrival claim needs speculative code, and the named mutation can be a no-op (Score: 65)

The relabel introduced three problems:
- **Green on arrival is false under strict TDD.** File 1 exercises `GatewayConformanceAudit` on synthetic inputs, so it needs no loader. The "thin loader" that reads `src/…` and `tests/*/test-configuration.json` is required only by file 2. Written test-first, file 2 is RED on arrival, but only as a compile error (the loader does not exist). Once the loader is written it passes, and its reports-nothing assertion is never observed failing. The preamble (:21) says a compile error does not count. Two developers will diverge: one writes the loader early (speculative) and applies the mutation, the other never observes a real RED.
- **The mutation is order-dependent.** "Skip the `GatewayConformance` section of the **first** `test-configuration.json` it reads". After task 52 there are 17 configurations, and 5 have no `GatewayConformance` section: DynamoDB, DynamoDB.V4, MongoDb, MySQL and Sqlite (`ls tests/*/test-configuration.json`). Directory enumeration order is unspecified on APFS and ext4. If one of those five comes first, skipping it changes nothing, the test stays green, and ralph returns `FAILED`.
- **The mutation target conflicts with ralph.** The mutated loader lives in `tests/Paramore.Brighter.Test.Generator.Tests/…`. Ralph's sub-agent cycle says "apply the task's named RED mutation … to production code, never to the test". A literal sub-agent may refuse, or treat the conflict as a stop-and-ask and return `FAILED`.

**Evidence**: tasks.md:931-938 (files, loader, 🔁 bullet); tasks.md:21 (compile error ≠ RED); ralph-implement.md Characterisation cycle step 3; 17 configs (14 today plus 3 new from task 52).

**Recommendation**:
- State that the loader is written in file 2's own cycle, and that file 2 is RED on arrival as a compile error. Keep a post-GREEN guard check (the task-2 pattern) to observe the assertion fail.
- Make the mutation deterministic, e.g. "the loader drops the `GatewayConformance` section of `Paramore.Brighter.AWS.Tests`'s configuration", or "the loader returns one fewer `SubscriptionType` than it reads".
- Say explicitly that the audit helper counts as "production" for the D14 rule in this task, and add the same clarification to ralph's cycle.

---

### 3. Task 24's mutation can fail the "contains both names" assertion before the regex it names (Score: 55)

This is the same defect class as round-3 finding 3, but it was not re-checked. The AC-15 test first asserts that the `Message` contains **both** namespace-qualified names, then the regex. With `AlphaSubscription` handed a BetaBus factory, T1 renders `{F}` = the handed type's display name. Task 18 says to render "the handed type(s) all through `DisplayName`", which invites a single computed value. If the mutation "render the handed type with `Type.Name`" changes that shared value, `BetaBus.ChannelFactory`'s qualified name disappears from both the body and the remedy, so the contains assertion fails first. Ralph then returns `FAILED` ("mutation does not produce the named failure"). The round-3 audit already marked this "yes, *if only the body's handed clause is mutated*", and the task does not say so.

**Evidence**: tasks.md:399-406; tasks.md:322 (task 18 rendering note); requirements.md:141 (T1 `{F}`).

**Recommendation**: Say "in the body's handed clause only, leaving T1's `{F}` on `DisplayName`", or name the contains-both assertion as the expected failure.

---

### 4. [workflow] The ralph return-format template still omits "re-run green", which Step 5 now requires in exactly the mixed tasks (Score: 50)

The new sub-agent cycle records `"<file>: mutation <what> → failed on <failure>; reverted; re-run green"`. Step 5's leftover check says that where the mutated file is also in `IMPL_FILES`, the main agent must rely on "reverted; re-run green" and must "treat `RED_EVIDENCE` missing either as a contract violation". The "Required return format" block was not updated: it still reads `"mutation <what> → failed on <assertion>; reverted"`. Tasks 41 (the mutation is in `Check`, which also gets the ANE guard) and 54 (the mutation is in the loader it writes) are exactly the IMPL_FILES-overlap case. A sub-agent that copies the return template will be bounced.

**Evidence**: ralph-implement.md Characterisation cycle step 3; the Required return format `RED_EVIDENCE:` line; the Step 5 leftover paragraph.

**Recommendation**: [workflow] Make the return-format line match the cycle: `"… → failed on <failure>; reverted; re-run green"`.

---

### 5. Full-suite runs in gateway test projects need brokers, and neither tasks.md nor the workflow scopes them (Score: 45)

Ralph's "must never drop" list, the sub-agent cycles and implement.md all require "the full suite for the affected project(s)". implement.md's new GENERATE step adds the same requirement. For tasks 31-35 and 53, the affected projects are gateway test projects (`AWS.Tests`, `Gcp.Tests`, `MQTT.Tests`, …) whose full suites need live infrastructure. CI itself runs them under `Category=` filters (tasks.md:957). Nothing says whether the "regression suite" means infrastructure-free tests only, a `/test-infra:run-tests` run, or something else. Unattended, the result is `FAILED`/`REGRESSIONS`; attended, different developers will scope it differently.

**Evidence**: implement.md:201-203; ralph-implement.md:73, :267, :288; tasks.md:957; tasks 31-35 and 53 carry no regression-scope note.

**Recommendation**: In the Phase 5 and Phase 10 intros, define the regression run for gateway projects: either the infrastructure-free filter or `/test-infra:run-tests`.

---

### 6. [workflow] test-first.md's characterisation question sits inside the fenced prompt template, and the options are unchanged (Score: 35)

The new sentences sit between `Question:` and `Options:` inside the code block, so they read as part of the literal prompt. Option 1 is still "Yes, implement the code". The "Modify the test first" loop says "verify it still fails appropriately", which is meaningless for a green-on-arrival test unless the mutation is re-applied.

**Evidence**: test-first.md:94-104.

**Recommendation**: [workflow] Move the characterisation variant out of the block, give it its own options ("Commit the test" / "Modify" / "Cancel"), and say a modified characterisation test is re-checked under the mutation.

---

### 7. Tasks 43 and 48 name an exception, while the preamble and ralph's dispatch row require "the assertion the task names" (Score: 30)

The preamble step 3 (:21) and ralph's Characterisation row say the RED must be on the named assertion, "not … an unrelated exception". Task 48 now names a thrown `ArgumentException`. Task 43 (scheduled before task 47's per-type catch exists) produces an unhandled `TargetInvocationException` out of `Sweep`. The sub-agent cycle says "the failure the task names", so it is consistent; the preamble and dispatch row are not. Both exceptions are related, so this is wording only.

**Recommendation**: Say "the assertion **or related exception** the task names" in the preamble and the ralph dispatch row. In task 43, state that the failure is the escaping `TargetInvocationException`.

---

### 8. [workflow] Residual ralph nits (Score: 25)

- A tracked file already modified at baseline keeps the same porcelain entry when mutated, so the leftover check cannot see it.
- The GREEN commit prefix for a mixed `CHARACTERISE` task (41, 54) is not listed in "Match the prefix to the task's shape".

**Recommendation**: [workflow] Diff content hashes, or refuse to start with modified tracked production files. Map mixed `CHARACTERISE` to `feat:`.

---

### 9. `DispatchBuilder` line citation inconsistent (Score: 10)

tasks.md:125 and requirements.md:89/96/583 cite `146-148`; tasks.md:146 cites `146-149`.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 5 |

**Total findings**: 9
**Findings at or above threshold (60)**: 2

Carried forward, below threshold and not re-scored: round-1 #7, #8, #9 and #10 are still present and unchanged.
