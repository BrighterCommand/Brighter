# Review: tasks — 0037-validate-subscription-channel-factory (round 2)

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

5 findings at or above threshold 60. Address these before approving.

> Main-agent verification: finding 1's workflow claims were checked against
> `.claude/commands/spec/ralph-implement.md` — :67-69 ("A task whose test passes on first run is
> `ALREADY_COMPLETE`"), :244-245 ("revise the test … or RETURN status `ALREADY_COMPLETE`"),
> :333-345 (tick committed alone, test files not staged) and the dispatch table :178-191 (`DOC`,
> `DOCUMENT` only; unlisted labels skipped). All hold.

## Round-1 fix verification
- **F1 (phase 8 order)**: HOLDS. 44 closing → 45 no-override pair over `Base<T>` → 46 declares-own (🔁); `Depends on` chain 43→44→45→46→47 correct; 45 RED on arrival; cross-reference rows match; task 44's note that 42/43 go red is accurate.
- **F2 (characterisation convention)**: HOLDS WITH NEW DEFECT — the green-on-arrival sweep is incomplete (→ 2), task 43's mutation fails via an unrelated exception (→ 5), task 29's fails differently than stated (→ 8), and no consumer command can execute the shape (→ 1). The FR-2 step 3 move to task 16 is correct.
- **F3 (tasks 1/2 swap; single-pass guard; ADR 0072 Risks)**: HOLDS WITH NEW DEFECT — two cross-reference statements still use pre-swap numbering (→ 4); the guard's detecting power is never observed (→ 6). ADR 0072 Risks sentence correct; task 8's false claim gone.
- **F4 (task 40 wording assertions)**: HOLDS.
- **F5 (task 50 test-first template render)**: HOLDS — `Parser`/`ParseContext(SourceFilePath, DestinationFilePath, Model)` exist as described (`tools/Paramore.Brighter.Test.Generator/Parser.cs:61`). Minor model-shape ambiguity (→ 9).
- **F6 (task 25 via `PipelineValidator`)**: HOLDS — constructor and precedent match; `EvaluateSpecs` iterates entities outermost, so mutation (a) compiles and fails the ordering assertion; (b) sound.
- **F11 (headings)**: HOLDS — `## Phase 5: Transport corrections` matches the preamble example under gear.md:70-73; `tasks 31-35` resolves.

## Findings

### 1. [workflow] No consumer command can run a 🔁 Characterisation task; ralph-implement actively throws the test away (Score: 80)

The tasks.md preamble (:16-24) defines a new task shape — test green, apply named production mutation, observe RED, revert, commit the test as `test:`. None of the four consuming documents knows this shape, and the unattended one does the opposite:

- **ralph-implement.md**: the sub-agent cycle (:244-245) says "If the test PASSES with no implementation change … Either **revise the test to verify something genuinely new**, or RETURN status `ALREADY_COMPLETE`"; "What this loop must never drop" (:67-69) says "A task whose test passes on first run is `ALREADY_COMPLETE`". On `ALREADY_COMPLETE` the main agent writes no change commit and commits only the tick (:333-345, "do NOT re-stage [TEST_FILES]"). Under ralph every 🔁 test is either rewritten into something the task did not ask for, or ticked done with its test file never committed — contradicting preamble step 5. The dispatch table (:178-187) has no row for the shape; 🔁 tasks are labelled `TEST + IMPLEMENT` so route to Behavioural.
- **implement.md**: Step 4 RED (:151-153) "Verify the test FAILS (Red) — … behavior doesn't exist yet"; approval (:155-168) asks "I've written a **failing** test…"; the review-after branch (:183-184) "Confirm RED is already proved … If it has not, go back and prove it" — no mention of mutation.
- **test-first.md** (:44-45, :108-109) and **ADR 0071** have no concept of RED via mutation.
- **Gate position**: the preamble puts ⛔ "after step 1 and before step 2" (a *green* test), while each 🔁 task's ⛔ line reads "before implementing", and there is nothing to implement.

🔁 tasks fall in phases planned for review-after/ralph: Phase 4 (28, 29, 30), Phase 8 (43, 46, 48), Phase 2 (10, 12, 17 and those in finding 2).

**Evidence**: ralph-implement.md:67-69, :244-245, :333-345; implement.md:151-153, :168, :183-184; tasks.md:16-24.

**Recommendation**: [workflow] Add a "Characterisation (🔁)" shape to ralph-implement.md's dispatch and sub-agent cycle with a distinct return status (e.g. `CHARACTERISED`) carrying the test file and the observed mutation; the main agent commits `test:` plus the tick. Forbid "revise the test" when the task is 🔁. Add the same branch to implement.md Step 4 and test-first.md's RED phase; record D14 in ADR 0071. Until then, tasks.md should say phases with 🔁 tasks must not run under ralph.

---

### 2. At least four more tasks are green on arrival with no 🔁 marker and no named mutation (Score: 72)

- **Task 6 (AC-4)**: task 5 already implements `subscription.ChannelFactory ?? defaultChannelFactory` (:124); task 6's config already falls back and fails the direct arm. Its implementation bullet (:136) describes task 5's code.
- **Task 11 (AC-9)**: task 8 mandates the combined arm as exact equality (:163), so `CombinedChannelFactory([new DerivedChannelFactory()])` already yields one Error; the companion assertion exercises unchanged `CombinedChannelFactory.cs:34-38`. Natural mutation: combined arm → `IsAssignableFrom`.
- **Task 7 (AC-5)**: implementation is "Confirm…"/"Never interpolate…" (:148-149); after task 6 the factory is already reduced to a `Type`.
- **Task 23 (AC-14)**: task 18 prescribes the full generic-stripping `DisplayName` (:317) and its own test is a closed generic.
- **Conditionally green**: task 20 (T3a exists from task 13; task 19's T1 excludes InMemory, :331); task 22 (same via 21/14, :359); task 24 (depends on body wording).

Under implement.md a developer meets an unexpected green with no named mutation and improvises; under ralph each becomes `ALREADY_COMPLETE` and the test is dropped (finding 1).

**Evidence**: tasks.md:124, :136, :148-149, :163, :190, :317, :331, :359, :387.

**Recommendation**: Mark 6, 7, 11, 23 🔁 with named mutations — 6: ignore `defaultChannelFactory` when `ChannelFactory` is null; 7: interpolate `RuntimeHelpers.GetHashCode(factory)`; 11: combined arm → `IsAssignableFrom`; 23: render `t.FullName` for generics. Give 20, 22, 24 a conditional 🔁 in task 48's style.

---

### 3. Task 53's generated-test shape is unexecutable, or silently lossy, under ralph-implement (Score: 65)

Task 53 is labelled `TEST + IMPLEMENT (generated)`. Ralph (:173-176, :189-191) matches the leading label; unlisted → skipped `- [!]`. It is ambiguous whether the parenthetical makes it unlisted. If it routes Behavioural: the sub-agent is told to "Write the test file" (:241) against "do NOT hand-write the file"; the verify command (:217-219) cannot be formed from `tests/<each of the twelve>/…`; and since task 53 depends on 35 the generated tests pass on first run → `ALREADY_COMPLETE` → the twelve generated files are never committed while the task reads done, leaving tasks 55 and 59 expecting files not in the tree. implement.md has no generated-test path either (:149).

**Evidence**: tasks.md:894-907; ralph-implement.md:178-191, :241, :244-245, :333-345.

**Recommendation**: Make task 53 explicitly attended-only, or [workflow] add a `GENERATED` row to ralph: run the generator, build, run the generated tests, commit the generated files as `test:`, never `ALREADY_COMPLETE`.

---

### 4. The swap of tasks 1 and 2 left two cross-reference statements describing the old numbering (Score: 62)

- :1092 (Scope creep): "**Tasks 2, 26 and 37 are fixture-only**" — task 2 is now `TEST + IMPLEMENT` for `FactoryTypes`; the fixture task is 1.
- :1078 (ADR 0073 table): "The cross-ADR constraint on 0072's doubles … | **2 (stated)**, 42" — stated in task 1 (:47).

**Evidence**: tasks.md:47, :52, :1078, :1092.

**Recommendation**: "Tasks 1, 26 and 37 are fixture-only"; "1 (stated), 42".

---

### 5. Task 43's mutation makes the test fail through an unrelated exception (Score: 60)

The mutation (:732) replaces the uninitialised read with `Activator.CreateInstance(type, nonPublic: true)`, which needs a parameterless constructor. The phase-1 doubles and `MockSubscription` are swept too and have none, so the sweep throws `MissingMethodException` on an unrelated candidate, possibly before reaching the AC-29 double — the task admits "(or cannot be found)". Preamble step 3 (:20) forbids failure by "an unrelated exception". It also turns task 42 red identically, so it shows nothing about task 43's test.

**Evidence**: tasks.md:20, :732; `MockSubscription` at `tests/Paramore.Brighter.Core.Tests/MessagingGateway/When_constructing_a_channel_with_combined_factory.cs:85`.

**Recommendation**: Name a discriminating mutation — e.g. try `Activator.CreateInstance`, fall back to `GetUninitializedObject` on `MissingMethodException`, so only the AC-29 double's constructor runs and throws `ConfigurationException` (`Subscription.cs:213`).

---

### 6. Task 2's single-pass guard is never observed catching the defect it exists for (Score: 55)

Task 2's RED is a compile error (`FactoryTypes` does not exist). The "What makes this fail" paragraph (:59) describes failure against a defective implementation nobody is told to try. A C# `yield` iterator — invited by "one-shot iterator" — restarts on a second `GetEnumerator()` rather than throwing. The double's location is unspecified and it is outside task 1's closed list.

**Evidence**: tasks.md:57-59, :70.

**Recommendation**: After GREEN apply the named mutation "build `FactoryTypes` from the `factories` parameter", see it fail, revert. Specify a hand-written `IEnumerable<IAmAChannelFactory>` class (not `yield`) in `MessagingGateway/TestDoubles/`, one per file.

---

### 7. Several task labels fall outside ralph-implement's vocabulary or misclassify the change (Score: 55)

- `DOCS` (36, 56) is not among ralph's labels (`DOC`, `DOCUMENT`, :184); unlisted → skipped (:189-191).
- `VERIFY (risk mitigation)` (58, 59) — same parenthetical ambiguity as task 53.
- `STRUCTURAL` → Tidy First, "Behaviour must not change … commit as `refactor:`" (:183). Task 51 adds a new generator (new behaviour); 55 is CI config; 49/52 are configuration data — these fit `SETUP` (`chore:`).

**Evidence**: tasks.md:601, :823, :856, :870, :936, :948, :976, :985; ralph-implement.md:178-191.

**Recommendation**: Rename to `DOC` and plain `VERIFY` (parentheticals into the title). Relabel 49, 52, 55 `SETUP`; relabel 51 honestly. [workflow] Or extend ralph's synonyms to `DOCS`.

---

### 8. Task 29's mutation fails at host start, not on "its no-results assertion" (Score: 45)

`ValidatePipelines(enabled: false)` leaves `throwOnError` at default `true` (`BrighterPipelineValidationExtensions.cs:58`). Removing the early return (:60) registers the validator and `BrighterValidationHostedService` (:96) with blocking on; the mismatch stops the host, so the test fails at `StartAsync`, not on the no-results assertion (:478).

**Evidence**: `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs:58-62, :96`; tasks.md:478.

**Recommendation**: Describe the actual failure, or pass `throwOnError: false` alongside `enabled: false`.

---

### 9. Where `Namespace` comes from in the render model is left open between tasks 49 and 50 (Score: 45)

Task 49 lists `SubscriptionType`, `AdditionalExpectedSubjects`, `Category` — no `Namespace`; task 50 renders "a configuration carrying `Namespace = "MyApp.Tests"`" and the template uses `{{ Namespace }}`. Sibling sections each carry `Namespace` plus a merge from the top-level value (`MessagingGatewayConfiguration.cs:41, :170-172`; `OutboxConfiguration.cs:51, :120-122`). Nobody schedules that for the new section.

**Evidence**: tasks.md:825-828, :837, :845, :872.

**Recommendation**: State that `GatewayConformanceConfiguration` gains `Namespace` with the siblings' top-level merge, and that the section is the model.

---

### 10. Tasks 47 and 48 also turn earlier phase-8 tests red at their RED step, unremarked (Score: 30)

Task 47's throwing-getter double makes 42-46 throw before a per-type catch exists; task 48's `where T : IEvent` double does likewise if task 47's catch does not enclose step 3. An unattended sub-agent running the full suite at RED could read this as a regression.

**Evidence**: tasks.md:747, :791, :806.

**Recommendation**: Add task 44's one-line note to 47 and 48.

---

## Gear readiness summary

| Task shape | Handling command | Result |
|---|---|---|
| `TEST + IMPLEMENT` + `/test-first`, RED on arrival | implement.md Step 4 / ralph Behavioural / test-first.md | Works; ⛔ wording matches the review-before gate |
| 🔁 Characterisation | none | **Gap** — ralph drops the test via `ALREADY_COMPLETE`; implement/test-first expect a failing test (→ 1); unmarked instances (→ 2) |
| `STRUCTURAL` (1, 26, 37, 49, 51, 52, 55) | ralph Structural (`refactor:`) | Works for fixtures 1/26/37; mislabelled for 49/51/52/55 (→ 7) |
| `DOCS` (36, 56) | ralph lists `DOC`/`DOCUMENT` only | **Gap** — skipped (→ 7) |
| `VERIFY` / `VERIFY (risk mitigation)` (57-59) | ralph Checkpoint | Plain works; parenthetical ambiguous (→ 7) |
| Generated (53) | none | **Gap** — skipped, or `ALREADY_COMPLETE` with files uncommitted (→ 3) |
| Phase scopes | gear.md:65-73 | Works |
| Two-commit shape | implement.md:256-281, ralph :303-345 | Works for behavioural/structural/docs; fails for 🔁 and task 53 under ralph (→ 1, 3) |

## Carried forward (round-1, below threshold, not re-scored)
- **7** (task 53 gives up natural RED): still present (:907).
- **8** (task 36 cites task 20 for the null-`D` clause): still present (:609).
- **9** (task 41 bundles two behaviours under one command): still present; now also a mixed 🔁 / RED-on-arrival shape under one gate (:686).
- **10** (ADR 0072 step-2 ordering note): still present (:1065).
- (12, NFR-5 mapping, fixed incidentally.)

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 3 |

**Total findings**: 10
**Findings at or above threshold (60)**: 5
