# Review: tasks — 0037-validate-subscription-channel-factory

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

6 findings at or above threshold 60. Address these before approving.

> Main-agent verification: findings 1 and 3 were spot-checked against the filesystem and hold —
> task 44 (tasks.md:690-700) introduces `Base<T>` while closing is only added in task 46 (:728);
> task 8's Given (:132) uses a collection expression, and `CombinedChannelFactory` (:12-14) would
> re-enumerate it successfully, so the re-enumeration defect cannot fail the test.

## Findings

### 1. Task 44 cannot go green without task 46's generic-closing step, so task 46 then passes before its own code is written (Score: 80)

Task 44 adds `Base<T>` to `ChannelFactoryDeclaration/TestDoubles/`, an open generic candidate in the `Core.Tests` assembly. Its implementation adds only step 2 (subsumption). Step 3 (closing with `MakeGenericType`) is not scheduled until task 46, and the per-type catch not until task 47. In task 44, therefore, `Sweep` reaches step 4 with `typeof(Base<>)` and calls `GetUninitializedObject` on an open generic definition. That throws, and nothing catches it yet, so the whole sweep fails. Task 44's assertion that "`typeof(Base<>)` **is** present among the subjects" can never pass. The same failure turns tasks 42 and 43, which sweep that same assembly, red again. The only way to make task 44 green is to implement step 3 inside task 44. Task 46's test then passes before task 46 writes any code, so its RED cannot be observed. Today `Core.Tests` has no generic `Subscription` subclass, so task 44 is the first task to bring one in.

**Evidence**: Task 44: "a base/derived pair … where the base is **generic** … `Derived : Base<Command>` … `typeof(Base<>)` **is** present"; implementation: "Add step 2 (**subsumption**)". Task 46: "Add step 3 (**generic closing**)". Task 47: "Catch **per type** inside `Sweep`".

**Recommendation**: Put generic closing (task 46) before the constructed-generic-base subsumption pair. One order that works is 42 → 43 → 46 (closing) → 44 → 45 → 47 → 48. Alternatively, have task 44 implement closing and redefine task 46 so it still has a RED of its own (for example the `Subject` being the open definition rather than the closed construction). Update the `Depends on` lines and the ADR 0073 step-2 cross-reference to match.

---

### 2. About ten `/test-first` tasks will pass on first run because an earlier task already implemented what they test, and the list never says how to observe RED (Score: 74)

CLAUDE.md says RED-first holds in both gears ("the test is observed failing for the right reason before any production code … A run that drops any of those is defective"). Several tasks test behaviour that an earlier task's *prescribed* implementation already delivers:

- **Task 10 (AC-8)**: task 4 already prescribes `declared.IsAssignableFrom(candidates[0])`.
- **Task 12 (AC-10)**: task 8 already prescribes exact equality with no recursion.
- **Task 16 (AC-11)**: task 7's implementation bullet already requires "FR-2 step 3 must be `typeof(InMemoryChannelFactory)`" — misplaced; belongs to task 16.
- **Task 17 (AC-19)**: the task itself says "there is no guard to add".
- **Tasks 28, 29, 30**: each says "Require no production change beyond task 27" or "Require **no guard inside the rule**".
- **Task 43 (AC-29)**: task 42 already prescribes `GetUninitializedObject`.
- **Task 45**: task 44 already prescribes the `BindingFlags.DeclaredOnly` conjunct.
- **Task 41, sound case**: after tasks 38-40, `Check` already returns null for a sound type.
- **Task 48**: task 47's per-type catch, if written around the whole per-type body, already turns the `MakeGenericType` failure into a non-null reason; task 48 asserts only "present and carries a non-null reason".

D12 (one task per AC) is settled and forces some of these to exist, but the task list must then say how an implementer satisfies RED-first for them.

**Evidence**: Task 28: "Require no production change beyond task 27"; task 17: "there is no guard to add, only a guard to *not* add"; task 7: "FR-2 step 3 must be `typeof(InMemoryChannelFactory)`".

**Recommendation**: Mark each as a characterisation test and state its RED observation (e.g. a named temporary mutation of the earlier implementation, reverted after RED is seen), or move the relevant implementation bullet out of the earlier task into this one — in particular FR-2 step 3 from task 7 to task 16. Give task 48 an assertion task 47's generic catch cannot satisfy (e.g. the reason identifies the constraint failure).

---

### 3. Task 8 cannot catch the defect task 1 relies on it to catch (Score: 70)

Task 1 skips its own test because "**task 8 (AC-6)** is the named regression guard for the re-enumeration defect". Task 8 claims "if `FactoryTypes` were built from the primary-constructor `factories` parameter the list would be empty and this test would report two findings instead of none". That is false for task 8's own Given: `new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()])` passes a collection expression — a re-enumerable array-backed sequence. A `FactoryTypes` built from `factories` would re-enumerate it and get both types back; the test stays green with the defect present. The defect only appears with a single-pass `IEnumerable`, and no task constructs one.

**Evidence**: Task 1 / tasks.md:29; task 8 / tasks.md:140. `CombinedChannelFactory.cs:12` takes `IEnumerable<IAmAChannelFactory> factories`; `:14` is `_factories = factories.ToList()`.

**Recommendation**: Either give task 1 its own `/test-first` task that builds a `CombinedChannelFactory` from a single-pass enumerable (e.g. a one-shot iterator that throws on a second `GetEnumerator`) and asserts `FactoryTypes` lists both types in order, or remove the "named regression guard" claim from tasks 1 and 8 and say the risk is covered by inspection only (task 58).

---

### 4. Task 40's test, as described, may already pass against task 38's implementation (Score: 66)

`typeof(IAmAChannelFactory).IsAssignableFrom(null)` returns `false`, so after task 38 `Check(x, null)` goes through the **not-a-channel-factory** branch and returns a non-null reason. Task 40 asks for "a non-null reason *of the form* … declares no channel factory type". Read as `Assert.NotNull`, the test is green on arrival and does not guard the realistic failure: a null declaration mis-routed into the wrong branch. "The branch is evaluated **first**" is not assertable except via the reason's wording.

**Evidence**: Task 40: "returns a non-null reason of the form *'… declares no channel factory type (`ChannelFactoryType` returned null).'*"; "the branch is evaluated **first** of the three". Task 38: "`!typeof(IAmAChannelFactory).IsAssignableFrom(declaredFactoryType)` → …".

**Recommendation**: Make the assertion explicit: the reason contains the null branch's distinguishing text (e.g. `returned null`) and does not contain `does not implement`. State that this is what makes it RED against task 38.

---

### 5. The generator and template contain untested logic, although the repository already tests generators and templates (Score: 66)

Tasks 49-51 are all "STRUCTURAL … no test of its own". Task 50's template converts `Ns.Foo`1` into `typeof(Ns.Foo<>)` / `Ns.Foo<,>` and builds the expected set as `SubscriptionType` ∪ `AdditionalExpectedSubjects`. `AdditionalExpectedSubjects` is absent from all twelve configurations, so task 53's generated files never execute the arity conversion or the union — that logic ships unexercised, and "the template decides nothing" does not match task 50's own description. `GatewayConformanceGenerator` (task 51) is C# with `Suites`/`SuitesFor`/`Plan`; existing audit tests only detect `Plan`/generate disagreement. Precedent exists: `tests/Paramore.Brighter.Test.Generator.Tests/OutboxGenerator/When_generating_with_single_outbox_should_emit_sync_and_async_suites.cs`, `Parser/When_parsing_template_with_outbox_configuration_should_render_correctly.cs`, `SharedGenerator/`.

**Evidence**: Task 50: "Renders a generic `AdditionalExpectedSubjects` entry by replacing the `` `n `` suffix…"; "**Why no test of its own**: … the template decides nothing." Task 52: "`AdditionalExpectedSubjects` is **absent from all twelve**".

**Recommendation**: Add a `/test-first` task in `Paramore.Brighter.Test.Generator.Tests` rendering the template from a configuration carrying arity-1 and arity-2 `AdditionalExpectedSubjects` entries, asserting the `typeof(Ns.Foo<>)` / `typeof(Ns.Foo<,>)` literals and the full expected set. Optionally a generator test in the `OutboxGenerator` style asserting one planned file per project at `MessagingGateway/Generated/Conformance/`.

---

### 6. Task 25's ordering assertion is tautological if the test calls the rule itself (Score: 62)

FR-13 / AC-30 require findings "in the order `sub-a`, `sub-b`". Task 25 says ordering "is supplied by the framework's subscription-outer/spec-inner loop in `PipelineValidator.EvaluateSpecs`" but does not say how the test evaluates the rule. If the test calls `IsSatisfiedBy` for `sub-a` then `sub-b` itself, the asserted order is the order the test imposed and cannot fail.

**Evidence**: Task 25: "the rule is evaluated **twice** … each run produces exactly **two** findings … in the order `sub-a`, `sub-b`"; "Neither is the rule's own responsibility to impose".

**Recommendation**: State that the test drives evaluation through `PipelineValidator` (or the public entry point that calls `EvaluateSpecs`) with subscriptions `[sub-a, sub-b]`, so ordering is observed rather than supplied — or state that ordering is out of the rule's test and covered elsewhere.

---

### 7. Task 53 gives up the natural RED of the generated sweep (Score: 55)

The generated sweeps are "green **only because tasks 31-35 have merged**", and task 53 depends on 35, so they are never observed failing. Against the current tree they would fail in exactly five assemblies with named reasons (GcpPubSub, MQTT: not-a-channel-factory; AWSSQS, AWSSQS.V4, Postgres: inherited-default) — the strongest available evidence that the generated test detects real defects.

**Evidence**: Task 53: "run the twelve and confirm all are green. They are green **only because tasks 31-35 have merged**"; "Depends on: 52, and **35**".

**Recommendation**: Allow generation before task 31, record the five expected reds and their reasons as the RED observation, re-run after task 35 for GREEN. Keep task 55's CI gate unchanged.

---

### 8. Task 36 cites the wrong task for the `no ChannelFactoryType` clause (Score: 50)

Task 36 says "task 20's `no ChannelFactoryType` body clause". Task 20 is AC-13a (in-memory T3a) and never asserts that literal; the clause is asserted by tasks 13 (AC-10a) and 14 (AC-10b).

**Evidence**: Task 36: "pinned by tasks 13, 19-22 (the remedy literals) and task 20's `no ChannelFactoryType` body clause".

**Recommendation**: Replace "task 20's" with "tasks 13 and 14's"; include 14 in `Depends on`.

---

### 9. Task 41 bundles two behaviours under a `/test-first` command that names only one (Score: 45)

Task 41 has two test files (null → sound; `ArgumentNullException` for a null subject), but one `/test-first` command covering only "should report no reason".

**Evidence**: Task 41 "USE COMMAND: `/test-first when a subscription declares a real channel factory the declaration check should report no reason`"; "Test files: … `When_checking_a_declaration_with_no_subscription_type_should_throw.cs`".

**Recommendation**: Split task 41, or add a second `/test-first` command for the throw case.

---

### 10. ADR 0072 puts the display-name formatter at step 2, before the rule; the tasks put it after (Score: 35)

ADR 0072's Implementation Approach orders "2. The display-name formatter" before "3. `ChannelFactoryCompatible`". The tasks stub `DisplayName` in task 3 and implement it in task 18, after tasks 9 and 13-15 assert display names (non-generic doubles, so `FullName` suffices). The cross-reference "step 2 → tasks 18, 23, 24" presents this as the ADR's order.

**Evidence**: ADR 0072 §Implementation Approach steps 2-3; tasks.md:989.

**Recommendation**: Add one sentence to the cross-reference noting and justifying the reordering.

---

### 11. The phase headings are hard to match exactly as a `/spec:gear` scope (Score: 30)

`gear.md` matches a scope against a heading's full text (stripping only leading `#`s, a leading number and whitespace). Headings such as `## Phase 5 — The five transport corrections (ADR 0072 step 5) — **prerequisite for phase 10 and phase 12**` contain em-dashes, backticks and bold markers a scope string must reproduce exactly. A mismatch fails safe to `review-before`; `tasks N-M` ranges work cleanly.

**Evidence**: `.claude/commands/spec/gear.md:70-73`; tasks.md headings for phases 5, 7, 10, 12.

**Recommendation**: Move decorations out of headings into the following paragraph, or add a note recommending `tasks N-M` scopes.

---

### 12. NFR-5 is mapped to task 49 (Score: 25)

Task 49 is a configuration class in `tools/`, not a public-abstraction change.

**Evidence**: tasks.md:942: "NFR-5 → 1, 49".

**Recommendation**: Change to "NFR-5 → 1, 38".

---

## Rulings on the author's flagged points

1. **Coverage cross-reference honest?** Mostly. Every FR-1..FR-13 and AC-1..AC-31 row points at a task that asserts or delivers it; no AC is uncovered; every ADR Implementation Approach step and Key Component has a task. It fails in three places: task 8 credited with guarding task 1 (finding 3); task 36's null-`D` citation (finding 8); ADR 0072 step 2 presented as in order (finding 10). Tasks 50/51 contain untested behaviour the table cannot show (finding 5).
2. **Task 53 — gate without `/test-first`.** The deviation is justified (`generated_tests.md`, ADR 0073 *Technology Choices*), and the gate sits sensibly before commit. But no RED observation is provided (finding 7), and the Liquid template and generator are not covered test-first anywhere (finding 5) despite repository precedent.
3. **Tasks 40 and 41 trace to ADR 0073, not an AC.** Confirmed genuine — ADR 0073 *Key Components* `Check` defines the null branch, the `ArgumentNullException`, and the sound-returns-null contract; Implementation Approach step 1 requires the null branch be scheduled explicitly. Not scope creep, but weakly specified (findings 4, 9).
4. **Phase headings vs `/spec:gear` scope.** Supported: `##` phases under an `#` title, and the `# Coverage cross-reference` h1 correctly closes Phase 13; `tasks N-M` works since tasks are numbered 1-59 continuously. Only the decorated headings are awkward to type (finding 11).
5. **Ordering.** 0072-before-0073 is explicit and correct (53 → 35, 55 → 35, line 12, task 55's ⚠️; 42 → 2 with reason; 26 → 27, 1 → 8, 37 → 38 correct). Counts verified: 59 tasks, 44 `USE COMMAND`, 45 ⛔ gates, 14 existing `test-configuration.json` (AzureServiceBus, MQTT, RMQ.Sync lack one), exactly 12 `src/Paramore.Brighter.MessagingGateway.*` directories; AWS, AWS.V4, MQTT test projects reference ServiceActivator, Gcp and PostgresSQL do not. **One missing dependency is a real defect**: task 44 needs task 46's closing (finding 1); task 7 carries an implementation bullet belonging to task 16 (in finding 2).

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 4 |

**Total findings**: 12
**Findings at or above threshold (60)**: 6
