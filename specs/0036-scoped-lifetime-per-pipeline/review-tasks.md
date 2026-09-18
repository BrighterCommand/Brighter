# Review: tasks — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-28
**Threshold**: 60
**Round**: 3
**Verdict**: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

Rounds 1 and 2 are closed; their records are `review-tasks-round1.md` and `review-tasks-round2.md`. This round had two jobs: verify the ten round-2 fixes landed, and attack the surface those fixes created, since every one of them is text no round had seen.

## Round-2 fix verification

| # | Round-2 finding | Status |
| --- | --- | --- |
| 1 | `T7.6` sited in a project that cannot host a controller | **LANDED** — `tasks.md:1185` now reads `Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"`; a new *"Why this project and not `Extensions.Tests`"* bullet at `:1187` records the reasoning and explicitly declines round 2's own split recommendation; `Depends on` at `:1206` names `T6.2` directly. |
| 2 | `T7.1`'s second registration displaces the core validator; six test files unmigrated | **PARTIAL** — `T7.0b` (`:1070`) lands ahead of `T7.1` (`T7.1`'s `Depends on` at `:1106` names it) and migrates the **seven** `GetRequiredService<IAmAPipelineValidator>()` sites. All seven re-derived at HEAD and every anchor correct. `PipelineValidationResult.Combine` verified `public` at `src/Paramore.Brighter/Validation/PipelineValidationResult.cs:64`, and `Combine` over one result is provably identity (`SelectMany` over a single element). **But** the constructor-parameter change breaks **five further sites** the task does not list — see finding 1. |
| 3 | Fourteen multi-host tasks with no `Facts:` line | **PARTIAL** — 20 `Facts:` lines now exist (6 original + the 14 named), and a sample was re-derived against source: `T6.11`=3 is correct (AC-48's mirror is a single `And given`, `requirements.md:796`, and the *no finding* clause is an `And` over the first two); `T6.10`=4 is correct (AC-18's `And given each of those two hosts built again`, `requirements.md:601`); `T7.5`=9 with six no-warning is correct (AC-42 has exactly nine `Given`s, `requirements.md:659-677`); `T7.6`=9 is correct (AC-50, `requirements.md:811-829`). **But** the sweep was list-driven and four further tasks still span multiple runs with no `Facts:` line — see finding 2. |
| 4 | `T4.2` cannot go green without an ask `T4.3` owns | **LANDED** — `T4.2:476` now lands the minimal ask ("Resolve `_scopeProvider` **once, in the constructor** in each of the five container-backed factories … make an **unconditional** `GetAmbient` call"); round 2's quoted `T4.3:508` bullet is gone and replaced by `T4.3:511`'s "⚠ **not** re-introduce the `_scopeProvider` field … **T4.2** already resolved both". Ownership is stated exactly **once**. `ServiceProviderHandlerFactory.cs:36` re-derived ✓ (`private readonly IServiceProvider _serviceProvider;`). |
| 5 | `T6.20` calls the extension from a project with no package reference | **LANDED** — `T6.20` re-sited to `AspNetCore.Tests` (`:1005`) with a *"Why this project"* bullet at `:1008`. `T6.2:728` narrowed to "in **T6.22's spy fixture alone** … which is the only arrangement"; `T6.22:1045` "no other Brighter test project references (re-derived: zero of the 37)" survives the move, since neither re-sited task lands in `Extensions.Tests`. Both claims re-verified consistent. `T6.20`'s `Depends on` (`:1017`) reaches `T6.2` transitively through the unbroken `T6.19→…→T6.3→T6.2` chain. |
| 6 | `T7.5` re-adds `ArtefactConstructorSelector` | **LANDED** — `T7.0a:1065` now states the selector "lands here as a **shell** — the type and its signature, **no rule body**"; `T7.5:1173` reads "⚠ **not** create `ArtefactConstructorSelector` … **Fill in its rule body**". The ADR-decisions table row `0074 | 3` (`:1531`) records the split. (One rationale wrinkle — finding 6.) |
| 7 | `T6.2` sized for eight, twenty test tasks sited there | **LANDED** — `T6.2:729` now reads "⚠ **Size it for twenty-two, not for eight**", with ADR 0073 step 4c quoted. Re-derived independently: `grep -c 'Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"'` = **21**, plus half of `T6.22` = **22**. The claim is exact. |
| 8 | FR coverage table omits `T4.2a`, `T7.0a` and eight back-references | **PARTIAL** — all ten items round 2 named landed (verified individually: `T4.2a` in FR-23/24.2/24.4; `T7.0a` in all four FR-22 rows; `T3.6`/FR-14; `T5.1`/FR-9(a); `T4.4`/FR-16(a); `T4.8`/FR-12; `T6.10`+`T7.10`/FR-24.3; `T7.6`/FR-17; `T7.4`/FR-20; `T3.5`/FR-17+FR-22.4). `T7.0b` was added to FR-22.1. **But** a full mechanical sweep still finds six residual gaps — see finding 7. |
| 9 | `T7.5`'s `Facts:` line mislabels the C-20(iv) case | **LANDED** — `:1159` now reads "the prefix-excluded *transform* asserts **no** warning, while the C-20(iv) *mapper* in that same assembly asserts **one**. Same assembly, opposite outcome", matching the body at `:1169` and `requirements.md:675`. |
| 10 | `T7.13` names no verifier | **LANDED** — `:1306` now reads "No test beyond AC-36's read and AC-44's walk. **Verified by**: **T7.15**'s reviewer walk". |

**Re-derived independently this round** (rule 22a: a total is a claim): **82** tasks = 62 `TEST + IMPLEMENT` + 12 `STRUCTURAL` + 2 `PROJECT` + 6 `DOC`. 82 ids defined, 82 ids mentioned, `comm` shows **zero dangling and zero orphans**. 74 `USE COMMAND` lines (62 + 12), 62 ⛔ gates, 62 `Test location` lines, 82 `Depends on` and 82 `References`. 55 ACs in `requirements.md`, 55 rows in the AC table, 51 distinct `TEST + IMPLEMENT` tasks named there and exactly **eleven** not — `T1.11, T1.14, T2.2, T3.2, T3.3, T3.6, T4.8, T4.9, T5.3, T5.4, T6.8` — matching the Scope-creep list exactly, and exactly **five** of those (`T1.14, T3.6, T4.9, T5.3, T5.4`) carry an AC cross-reference on `References`. **Both Scope-creep counts are correct.** `T7.14`'s entry contains exactly **thirteen** numbered items and item 12 is repointed to `T7.0b` (`:1333`) ✓. ADR step counts re-extracted from the ADRs: 0070 **14**, 0071 **6**, 0072 **13**, 0073 **9**, 0074 **9** (numbered list: 1, 2, 3, 4, 5, 5a, 5b, 6, 7), 0075 **9**, 0076 **5** — **all seven claims hold**. Traps 1–5 all honoured: `T4.7:578` keeps `ValidateScopes` off deliberately; `T6.22` keeps AC-14 as one criterion in two projects; `T7.4`/`T7.5`'s negative-fact counts re-derived correct; `T5.4:689` keeps `Performer` distinct from C-2's five frozen types.

## Findings

### 1. `T7.0b` changes a public constructor's parameter type but its migration list stops at the seven resolution sites — five direct construction sites in four files are not named, and its discovery `grep` cannot find them (Score: 72)

`T7.0b` changes `BrighterValidationHostedService`'s **constructor parameter** at `:60` from `IAmAPipelineValidator` to `IEnumerable<IAmAPipelineValidator>`. That is a compile break at every site that calls the constructor. The task's migration clause and its discovery command cover only *resolution* sites (`GetRequiredService`/`GetService`), which is a different set. Five construction sites exist and none is listed:

- `tests/Paramore.Brighter.Core.Tests/Validation/When_both_validate_and_describe_registered_should_describe_once.cs:51`
- `tests/Paramore.Brighter.Core.Tests/Validation/When_validation_hosted_service_starts_without_consumers_should_validate.cs:50` (plus its `BuildService` helper signature at `:41`)
- `tests/Paramore.Brighter.Core.Tests/Validation/When_throw_on_error_false_should_log_errors_not_throw.cs:48` (plus its helper signature at `:42`)
- `tests/Paramore.Brighter.Core.Tests/Validation/When_validation_hosted_service_has_warnings_should_log_them.cs:49` **and** `:71`

This is the exact failure mode round 1's finding 5 and round 2's finding 2 both targeted — a migration whose *count* is asserted from a `grep` that does not cover the whole change. `T1.3` and `T2.1` both handle this correctly by making the discovery command match the edit being made; `T7.0b` does not.

**Evidence**:
- `tasks.md:1072` — "the field (`:47`), the constructor parameter (`:60`)" — both anchors re-derived correct at HEAD
- `tasks.md:1075` — "Re-derived at HEAD — **7 sites across 6 files**, and the discovery command is `grep -rn "GetRequiredService<IAmAPipelineValidator>\|GetService<IAmAPipelineValidator>" tests/ --include="*.cs"`". Run verbatim it returns exactly the seven listed sites and **none** of the five construction sites.
- `grep -rn "new BrighterValidationHostedService" tests/ --include="*.cs"` returns 5 hits across 4 files.

The failure is loud (a compile error) rather than silent, and `T7.0b`'s `Done when` ("the solution builds") would eventually catch it — but the implementor takes the `/tidy-first` change against an approved, explicitly-counted file list that is wrong, and the "7 sites across 6 files" total is what they check their work against.

**⚠ Premise re-verified after the review, and the scope of the fix NARROWED.** `T7.0b` widens *two* hosts, so "enumerate the family" demands the sibling be checked before the fix is written. It was, and it is clean:

- `ServiceActivatorHostedService` does **not** take the validator as a constructor parameter — it resolves it inside `StartAsync` (`ServiceActivatorHostedService.cs:50`, `_serviceProvider.GetService<IAmAPipelineValidator>()`). Its constructor is `(logger, dispatcher, provider, options)` and is **untouched** by this widening.
- Its **11** construction sites across 4 files in `Extensions.Tests` therefore do **not** break, and must **not** be added to the migration list.
- All 11 build `provider` from a real `new ServiceCollection()` + `BuildServiceProvider()`, several registering the validator with `AddSingleton<IAmAPipelineValidator>(validator)` and two registering none at all. So `GetService` → `GetServices` is genuinely behaviour-preserving there, including the zero-registration cases — which is exactly the case `T7.0b:1078`'s "an empty sequence must behave exactly as today's `null` did" clause exists to protect.

So the defect is confined to `BrighterValidationHostedService`'s constructor. The fix must not over-migrate.

**Recommendation**: Add a second discovery command and site list to `T7.0b` — `grep -rn "new BrighterValidationHostedService" tests/ --include="*.cs"` — naming the five construction sites and the two helper signatures, and restate the total as "seven resolution sites plus five construction sites, across nine files". Keep the two lists separate, since only the resolution sites carry the silent-runtime hazard `:1082` describes. Add a one-line note that `ServiceActivatorHostedService`'s constructor is deliberately not in scope, so a later reader does not read the omission as an oversight.

---

### 2. The `Facts:` sweep closed round 2's enumerated fourteen, not the rule — four further tasks state their `Then` over two or more runs and the Overview still declares them single-fact (Score: 65)

Round 2's finding 3 said "at least fourteen more" and listed fourteen. Exactly those fourteen got a `Facts:` line. The Overview rule at `:9` is unchanged and still general — "any task whose `Test should verify` spans more than one host carries an explicit **`Facts:`** line … **A task with no `Facts:` line is a single fact.**" A rule-driven sweep (not a list replay) finds four tasks whose own text says the opposite. Two of them state in terms that a single-run test does not discharge their AC.

**Evidence** (each verified as carrying no `Facts:` line — the total is still exactly 20):
- `T1.6`, `tasks.md:100` — "asserted for **both twins** — `TransformPipelineBuilder` (sync/Reactor) **and** `TransformPipelineBuilderAsync` (async/Proactor). This task touches both; **a single-twin test does not discharge AC-2**"
- `T5.3`, `tasks.md:665` — "resolve instances that are **not** the ambient's — on **both** twins, sync `Publish` (`Parallel.ForEach`, `CommandProcessor.cs:481`) and `PublishAsync` (start loop `:591-599`, awaited at `:601`)"
- `T4.9`, `tasks.md:613-615` — "asserted on **both** the owned path and the borrowed path — one protocol, not two"; plus a concurrency fact at `:614` and a losing-waiter fact at `:615`
- `T1.14`, `tasks.md:222` — "the same holds on the transformer factories and on both async twins"

`T1.6` and `T5.3` are the load-bearing cases: both are sync/async twin pairs, which the review guardrails name as this spec's signature failure mode, and both are declared single-fact by the Overview's default against their own explicit text.

**Recommendation**: Add a `Facts:` line to each of these four in the same shape as the twenty that have one (`T1.6`: 2 — the sync builder and the async builder; `T5.3`: 2 — the sync `Publish` run and the `PublishAsync` run; `T4.9`: 3 — owned path, borrowed path, concurrent first-resolvers with the losing-waiter no-op; `T1.14`: state whether the four factories and their async twins are one fact or four). Then re-run the rule mechanically over all 62 behavioural tasks rather than over a prior round's list.

---

### 3. Phase 7's preamble instructs "Do not invent one" — and the next two tasks are both `/tidy-first` steps sequenced ahead of the ADR's behavioural work (Score: 62)

The Phase 7 header states flatly that ADR 0074 has no Tidy-First step and forbids inventing one. `T7.0a` and `T7.0b`, the two tasks immediately below it, are both `STRUCTURAL` tasks using `/tidy-first`, both sequenced ahead of `T7.1`. `T7.0a` was added by round 1's fix and `T7.0b` by round 2's, and neither round updated the preamble that now contradicts them.

**Evidence**:
- `tasks.md:1056` — "⚠ ADR 0074 step 1 states there is **no Tidy-First step to sequence ahead of this ADR** — *'a comment amendment is neither structural nor behavioural'*. **Do not invent one.** The `PipelineValidator` XML-comment amendment lands inside T7.5."
- `tasks.md:1058` — "**STRUCTURAL: T7.0a** … **USE COMMAND**: `/tidy-first add the ContainerRegistrationSnapshot …`"
- `tasks.md:1070` — "**STRUCTURAL: T7.0b** … **USE COMMAND**: `/tidy-first widen the two validation hosted services …`"
- ADR 0074's own step 1 (`0074-lifetime-validation-evaluation-site.md:681`) scopes its claim to core: "This ADR changes no **code** in `Paramore.Brighter` at all. The single thing it changes there is one XML doc comment … There is therefore no Tidy-First step to sequence ahead of the behavioural one."

The ADR's claim is about *core*; the preamble restates it as an unqualified prohibition. An implementor working Phase 7 top-down reads "do not invent one" and then meets two.

**Recommendation**: Reword `:1056` to the ADR's own scope — e.g. "ADR 0074 changes no code in `Paramore.Brighter`, so **no Tidy-First step is owed in core**; the `PipelineValidator` XML-comment amendment lands inside T7.5, not in a commit of its own. Two Tidy-First steps *are* owed in the DI package — `T7.0a` and `T7.0b` — for the reasons each states."

---

### 4. `T7.0b` makes a source-and-binary breaking change to a public constructor, and `T7.14`'s entry records only its behavioural half (Score: 55)

`BrighterValidationHostedService` is `public` with a `public` constructor (`BrighterValidationHostedService.cs:44`, `:58`). Changing its second parameter from `IAmAPipelineValidator` to `IEnumerable<IAmAPipelineValidator>` is a source and binary break for any caller — which is precisely why five sites in this repository break. The document's own convention records exactly this class of change: `T5.1:634` ("Binary-breaking on two public constructors … release-noted by T7.14"), `T3.4:420`, `T1.3`, `T2.1`. `T7.0b` does not, and `T7.14` item 12 covers only the behavioural consequence.

**Evidence**:
- `tasks.md:1084` — "Be release-noted: an application-supplied `IAmAPipelineValidator` no longer replaces Brighter's validation wholesale, and both hosts now combine every registered validator (T7.14)" — behaviour only
- `tasks.md:1333` (T7.14 item 12) — "*Behavioural, ADR 0074* — both validation hosted services resolve every registered `IAmAPipelineValidator` and combine the results … `GetService<IAmAPipelineValidator>()` now returns whichever descriptor is last (T7.0b)" — no constructor clause
- Contrast `tasks.md:1331` (item 10) — "*Binary, ADR 0075* — `PipelineBuilder<TRequest>`'s two public dispatch constructors gain a defaulted `bool isolateSubscribers` (T5.1)"

**Recommendation**: Either add the clause to item 12 ("*and source and binary* — `BrighterValidationHostedService`'s public constructor takes `IEnumerable<IAmAPipelineValidator>` in place of `IAmAPipelineValidator`") or add a fourteenth item and update the entry's title and `T7.14`'s "thirteen breaking-change items" heading accordingly. Note that `ServiceActivatorHostedService`'s constructor is **not** part of this break (see finding 1), so the clause must name only the one type.

---

### 5. `T6.2` names not a single `ProjectReference`, yet four tasks sited in that project need consumer, outbox and EF references — and `T6.20` asserts they are already there (Score: 52)

`T6.2` is the `PROJECT` task that builds `tests/Paramore.Brighter.Extensions.AspNetCore.Tests`. It specifies the SDK, the target frameworks, one `Directory.Packages.props` entry and the `Brighter.slnx` addition, but no project references at all. The 22 tasks it hosts need considerably more: `T6.19` needs `AddDbContext` and a relational outbox; `T6.9`, `T6.20`, `T6.21` and `T7.6` all build consumer hosts and register `ServiceActivatorHostedService` explicitly. For comparison, `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` carries five `ProjectReference`s to cover the same ground.

Worse, `T6.20`'s re-siting rationale asserts the references exist, with no task that supplies them.

**Evidence**:
- `tasks.md:722-731` — the whole of `T6.2`; the only reference-shaped statements are "⚠ Add a `Directory.Packages.props` entry for `Microsoft.AspNetCore.Mvc.Testing`" (`:726`) and "it **references the src package**" (`:728`)
- `tasks.md:1008` (T6.20) — "**T6.21 already sits here with the same consumer packages**"
- `tasks.md:992` (T6.19) — "a `Scoped` `DbContext` registered `AddDbContext`, a relational outbox, and a `Send` handler injecting both that `DbContext` and a transaction provider over it"
- `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` — `ProjectReference`s to `Extensions.DependencyInjection`, `Outbox.Sqlite`, `ServiceActivator.Extensions.DependencyInjection`, `ServiceActivator.Extensions.Hosting`, `Sqlite.EntityFrameworkCore`
- `T6.2`'s `Done when` (`:723`) is "restores and builds … `dotnet test` on it succeeds with **zero tests**" — so nothing in the task's own verification would surface the gap

**Recommendation**: Add a `ProjectReference` bullet to `T6.2` naming `Paramore.Brighter.Extensions.AspNetCore`, `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection`, `Paramore.Brighter.ServiceActivator.Extensions.Hosting`, `Paramore.Brighter.Outbox.Sqlite` and `Paramore.Brighter.Sqlite.EntityFrameworkCore`, saying which sited task needs each. Then `T6.20:1008`'s "the same consumer packages" has an owner.

---

### 6. `T7.0a`'s reason for landing the constructor selector as a shell rules out the snapshot it lands in the same commit (Score: 50)

Round 2's finding-6 fix added a rule to `T7.0a`: unexercised logic may not ship in a `/tidy-first` commit, so `ArtefactConstructorSelector` lands as a signature only. Applied consistently, that rule also forbids `ContainerRegistrationSnapshot`'s three queries — keyed-versus-unkeyed last-descriptor resolution, artefact-candidate discovery with kinds, and registration-order `DescriptorRecord`s — which are substantially more logic than D15's rule and are landed in the same commit with no test.

**Evidence**:
- `tasks.md:1065` — "Landing the rule here would put **unexercised logic in a `/tidy-first` commit, which the TDD mandate forbids**; the type is separated out only so that T7.5 does not also have to create the file"
- `tasks.md:1061` — "`ContainerRegistrationSnapshot` is built from an `IServiceCollection` and answers **three** queries (ADR 0074 step 2): the effective lifetime for a service type — the last **unkeyed** descriptor, matching Microsoft's resolution, or the last for a `(type, key)` pair where a parameter names one; the artefact candidates with their kinds, over keyed and unkeyed descriptors alike; and the `DescriptorRecord`s for a service type **in registration order** …"
- `tasks.md:1063` — "**Nothing calls any of it in this commit.**"

Nothing is built wrong — `:1061` is an unambiguous instruction to implement the queries — but the stated principle and the stated content contradict, and a reader applying the principle strictly would land signatures only and hand the queries back to `T7.1`, restoring the big-bang round 1 removed.

**Recommendation**: Narrow the reason at `:1065` to what actually distinguishes the two — D15's rule is a **named design decision with an acceptance criterion driving it** (AC-42's two constructor-selection clauses), so it belongs in the red-green cycle that criterion owns; the snapshot's queries are descriptor reads with no criterion of their own and no caller. Say that instead of "unexercised logic … forbids".

---

### 7. The FR-coverage back-reference sweep is still incomplete, and two tasks cite FR rows that do not exist (Score: 38)

Round 2's finding 8 named ten repairs; all ten landed. But the recommendation was a *mechanical* sweep ("every FR named in a task's `References` should name that task in its row"), and running it finds six residual gaps plus two dangling citations.

**Evidence** (task `References` → FR row that omits it):
- `T1.4:71` references `FR-5`; the FR-5 row (`:1362`) reads `T1.9 (AC-5), T1.10 (AC-6), T2.6 (AC-51), T6.18 (AC-38)`
- `T1.10:166` references `FR-13`; the FR-13 row (`:1372`) reads `T1.11, T2.7 (AC-33), T2.6 (AC-51), T6.7 (AC-19)`
- `T2.2:274` references `FR-5, FR-6, FR-13`; **none of the three rows names `T2.2`** — the more striking of the set, because the Scope-creep section (`:1568`) says of the eleven no-AC tasks "each carries its FR/NFR trace"
- `T3.3:413` references `FR-8, FR-9`; the FR-8 row (`:1365`) and both FR-9 rows (`:1366-1367`) omit `T3.3`
- `T7.8:1245` and `T7.10:1279` reference a bare `FR-25` and `FR-22`; the table has only `FR-25.1`–`FR-25.11` and `FR-22.1`–`FR-22.4`, so neither citation resolves to a row

No FR is left uncovered, so this is table incompleteness rather than a coverage hole.

**Recommendation**: Add `T1.4` and `T2.2` to FR-5; `T2.2` to FR-6; `T1.10` and `T2.2` to FR-13; `T3.3` to FR-8 and both FR-9 rows. Replace the bare `FR-25`/`FR-22` citations on `T7.8` and `T7.10` with the sub-numbered clauses they mean. Then run the check as a script rather than by list.

---

### 8. `T7.0a` says the constructor selector is guarded "without a host"; `T7.5`'s nine facts are all full hosts (Score: 30)

`T7.0a:1065` justifies the shell split by saying the selector "is **testable with a `Type` alone**, which is why T7.5's clauses can guard it without a host". `T7.5`'s two constructor-selection clauses (`:1165`, `:1170`) are both configured as producer-only hosts with `ValidatePipelines()` called last, like the other seven facts in that file, and AC-42 states them the same way (`requirements.md:666`, `:676`).

**Evidence**: `tasks.md:1161` — "a producer-only host with `{Transient, Singleton, Transient}` … and `ValidatePipelines()` called last with `throwOnError: true`" governs all nine of `T7.5`'s facts.

**Recommendation**: Change "can guard it without a host" to "can guard it through the two constructor-selection clauses, which need no application constructor to run" — which is what `:1179` ("resolve nothing and run no application constructor") already says and is the property that actually matters.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 2 |

**Total findings**: 8
**Findings at or above threshold (60)**: 3

---

## Resolution — all eight fixed, 2026-08-28

| # | Score | Fix |
| --- | --- | --- |
| 1 | 72 | `T7.0b` gains a **second discovery command** (`grep -rn "new BrighterValidationHostedService" tests/`) and the five construction sites plus the two `BuildService` helper signatures; total restated as **seven resolution plus five construction sites, across nine files**. `USE COMMAND`, `Done when` and the ADR 0074 step 5b row all restated to twelve. ⚠ A bullet now records that **`ServiceActivatorHostedService`'s constructor is deliberately out of scope** and its 11 sites must **not** be migrated — verified: it resolves inside `StartAsync`, and all 11 build a real `ServiceCollection`, two registering no validator, so `GetService`→`GetServices` preserves behaviour |
| 2 | 65 | **Fixed at the rule, not the list.** The Overview trigger was *"spans more than one host"*, which is why two rounds swept by list — tasks arranging **no host at all** fell outside it. It now reads *"cannot be written as a single `[Fact]`"* with four enumerated triggers (second host · the other twin · second act on the same host · distinct arrangement) and an explicit **counter-case** so it is not over-applied: an aggregate assertion over one run (*"two `Send` calls recording exactly one `Warning` between them"* — T4.5, T4.7, T6.7) stays **one** fact. A mechanical sweep of all 62 behavioural tasks then found **14**, not four: `T1.2`(3) `T1.6`(2) `T1.12`(2) `T1.14`(4) `T3.2`(2) `T3.3`(4) `T4.2`(2) `T4.8`(2) `T4.9`(3) `T5.3`(2) `T6.8`(2) `T6.16`(2) `T6.17`(4) `T6.22`(2). **34 `Facts:` lines covering 124 facts** |
| 3 | 62 | The Phase 7 preamble rewritten to the ADR's own scope — *"changes no code in `Paramore.Brighter`, so **no Tidy-First step is owed in core**"* — and now names T7.0a and T7.0b as the two owed **in the DI package**. The ADR 0074 step 1 row carries the same correction |
| 4 | 55 | `T7.14` item 12 becomes *"Behavioural **and** source and binary"* and names the constructor change. ⚠ It names **only `BrighterValidationHostedService`**, per finding 1's verification. Still **thirteen** items, so the entry's heading stays true. `T7.0b`'s own release-note pointer restated to match |
| 5 | 52 | `T6.2` gains a **`ProjectReference` bullet**, each reference attributed to the task that needs it: the AspNetCore package and `Extensions.DependencyInjection` (all fixtures), the two ServiceActivator packages (T6.9, T6.20, T6.21, T7.6), `Outbox.Sqlite` + `Sqlite.EntityFrameworkCore` (T6.19 alone). All five verified to exist in `src/`, and the set matches `Extensions.Tests.csproj:26-30`. A note records that they add no `Directory.Packages.props` entry and leave NFR-2 intact |
| 6 | 50 | `T7.0a`'s rationale narrowed to what actually distinguishes the two: **D15 is a named decision with an AC driving it** (AC-42's two constructor-selection clauses), so its rule belongs in that red-green cycle; the snapshot's queries have **no criterion and no caller**, so nothing could gate them there. The "unexercised logic … forbids" formulation, which also ruled out the snapshot, is gone |
| 7 | 38 | Run **as a script**, as recommended. It confirmed the six named gaps and found a **seventh the review missed** — `T6.1` cites FR-17 and the FR-17 row omitted it. Repaired: FR-5 (+T1.4, T2.2), FR-6 (+T2.2), FR-8 and both FR-9 rows (+T3.3), FR-13 (+T1.10, T2.2), FR-17 (+T6.1). ⚠ **The review's second half was rejected on evidence**: the bare `FR-25`/`FR-22` on T7.8 and T7.10 are **verbatim quotes of the approved ACs' own parentheticals** (`requirements.md:743`, `:799`) — rewriting them would diverge from a closed requirements document. Two **chapeau rows** were added instead, so the citations resolve. Re-run after the edits: **51 rows, zero omissions, zero dangling** |
| 8 | 30 | `T7.0a`'s "guard it without a host" replaced by "guard it **without running an application constructor**", which is what `:1179` already says and the property that actually matters. The neighbouring *"unit-testable without a host"* is a **quotation of ADR 0074's Positive section** and was deliberately left alone |

**Re-derived after the fixes** (rule 22a — not incremented): **1,627 lines, 82 tasks** = 62 `TEST + IMPLEMENT` · 12 `STRUCTURAL` · 2 `PROJECT` · 6 `DOC`. 82 ids defined, 82 mentioned, **zero dangling, zero orphans**. 74 `USE COMMAND` (62 `/test-first` + 12 `/tidy-first`), 62 ⛔ gates, 62 `Test location`/`Test file` pairs, 82 `Depends on` and 82 `References` with `Depends on` immediately before `References` in **every** task. **34 `Facts:` lines covering 124 facts.** 55 ACs in `requirements.md`, 55 rows in the AC table. FR table: 51 rows, mechanically clean.

**Two defects found while fixing, by no review round** — both the same class as round 1's finding 1, a pointer left behind by its own fix:
- `T7.0b`'s `USE COMMAND` and `Done when` still said *"the seven test resolution sites"* after the construction sites were added. **Both restated.**
- The **ADR 0074 step 1 row** in the decisions table still read *"Explicitly no Tidy-First task, per the ADR"* — the same contradiction as finding 3, in a second place. **Corrected.**
