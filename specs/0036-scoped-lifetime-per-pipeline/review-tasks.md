# Review: tasks — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-28
**Threshold**: 60
**Verdict**: NEEDS WORK

**Round 2** — a re-review of `tasks.md` after all eleven round-1 findings were fixed in `0acb3d717`. Round 1's findings are preserved verbatim in `review-tasks-round1.md`.

6 findings at or above threshold 60. Address these before approving.

## Round 1 fix verification

| # | Round 1 finding | Fix landed? | Evidence |
| --- | --- | --- | --- |
| 1 | Six broken task pointers (four→`T7.15`, two→non-existent `T7.16`) | YES | All six repointed: `tasks.md:226` (T1.14→T7.14), `:419` (T3.4→T7.14), `:630` (T5.1→T7.14), `:1091` (T7.2→T7.14), `:701` (T5.5→T7.15), `:1260` (T7.12→T7.15). Sweep re-run independently: 81 ids defined, 81 ids referenced, `comm` shows zero dangling and zero orphans. |
| 2 | `T4.4` registered what `T4.5` created | YES | New `STRUCTURAL: T4.2a` at `tasks.md:481`, positioned before `T4.3` (`:492`); `T4.4:527` now reads "⚠ **`AmbientScopeDiagnostics` is already registered there by T4.2a** — do not register it a second time"; `T4.5:542` reads "⚠ **not** add or register `AmbientScopeDiagnostics` — **T4.2a** landed the type and its registration inert". `T4.3`'s `Depends on` is `T4.2a` (`:509`). |
| 3 | Two incompatible ladder numbering schemes; `T6.13` cited row 6 | YES | Canonical scheme fixed in the Overview at `tasks.md:13`; `T4.3:504` now reads "implement **canonical ladder rows 1, 2, 3, 5 and 6** … **never** the six-step pseudo-code block"; `T6.13:895` now reads "**canonical ladder row 5's** ignore-and-warn. ⚠ **Row 5, not row 6**". Every `row N` mention (11 sites) uses the canonical ten-row scheme; no survivor of the old numbering. |
| 4 | One-AC tasks that are several tests in disguise | PARTIAL | The `Facts:` convention is in the Overview (`:9`) and six `Facts:` lines exist — T3.6 (8, `:437`), T6.9 (8, `:823`), T6.12 (6, `:872`), T7.5 (9, `:1128`), T7.6 (9, `:1156`), T7.10 (7, `:1234`) — exactly the six named. **But the rule as written is general and ~14 further multi-host tasks still carry no `Facts:` line.** See finding 3. |
| 5 | `T1.3` named 3 of 38 files | YES | Discovery `grep` added at `tasks.md:52-57` ("Verified at HEAD: returns exactly **38** files"), and the parallel one at `:244-249` for `T2.1` ("returns exactly **22** files"). |
| 6 | `T7.1` was a big-bang | YES | New `STRUCTURAL: T7.0a` at `tasks.md:1047` lands the snapshot + five entity types inert; `T7.1:1069` now reads "⚠ **not** add the entities or the snapshot — **T7.0a** landed …". (One residue: see finding 6.) |
| 7 | `T2.1` routed `ServiceProviderHandlerFactory` to `T2.4` | YES | `tasks.md:250`: "Only two implementations do more than answer `null`: `ServiceProviderHandlerFactory` (**T2.3**) and `HandlerLifetimeScope` (T2.2)". |
| 8 | Four tasks with no verification signal | YES | `T6.1:710` and `T6.2:719` both carry "**Done when**: …" build-level criteria; `T5.5:693` and `T7.11:1248` both carry "**Verified by**: a line on the PR checklist, and … **T7.15**". |
| 9 | `T1.3` silent on the four container-backed factories | YES | `tasks.md:59`: "⚠ **The four container-backed factories get that same treatment in this commit** — `CreatePipelineScope()` returns `null` and `Create` ignores the parameter … their **behaviour** is T1.5's". |
| 10 | AC-24 coverage row named `T7.14` only | YES | `tasks.md:1415`: "`AC-24 \| T7.14 (release_notes.md), T7.13 (the guidance-page half)`". |
| 11 | NFR-2's "explicit gate" owned by no task | YES | `T1.2:39` reworded to "⚠ It is an **automated test**, so it needs no separate gate of its own: **T6.1 and T6.2 each carry it as a `Done when` condition**"; `T6.1:710` and `T6.2:719` carry it; NFR table row updated at `:1378`. |

**Re-derived independently in this round** (rule 22a — totals are claims): 81 tasks = **62** `TEST + IMPLEMENT` + **11** `STRUCTURAL` + **2** `PROJECT` + **6** `DOC`, confirmed by `grep -c`. All 81 carry `Depends on` and `References`, with `Depends on` immediately preceding `References` in every case. 73 `USE COMMAND` lines = 62 test + 11 structural; 62 ⛔ gates; 62 `Test location`/`Test file` pairs. All 55 ACs present in the AC table; ADR step tables match the ADRs' actual step sets (0070: 14, 0071: 6, 0072: 13, 0073: 9, 0074: 9 incl. 5a/5b, 0075: 9, 0076: 5). Spot-checked anchors `ServiceCollectionExtensions.cs:142`, `:74`, `:97`, `src/Directory.Build.props:43` and `:45` — all correct at HEAD. Traps 1, 2, 3, 4 and 5 all honoured.

## Findings

### 1. `T7.6` is sited in a test project that cannot host the ASP.NET controller AC-50 requires, and its own text still says "the controller" (Score: 85)

AC-50's `Given` is **"an ASP.NET host"** and its `When` is **"a controller action calls `Send`"** (`requirements.md:811-812`). `T7.6` places the whole nine-fact test in `tests/Paramore.Brighter.Extensions.Tests` — a plain `Microsoft.NET.Sdk` project with no web host. `T6.2` exists precisely because **no** project in the repository could host a controller action before Phase 6 (`tasks.md:720`: "the repository has **37** test projects and **zero** reference `Microsoft.AspNetCore.*` … `Brighter.slnx` has no ASP.NET **test** entry").

`T7.6`'s own `Then` clauses retain the controller language, so this is not a deliberate re-siting — it is a mis-placement that the task text itself contradicts.

**Evidence**:
- `tasks.md:1154` — `- Test location: "tests/Paramore.Brighter.Extensions.Tests"`
- `tasks.md:1159` — "the resolved `IBrighterOptions` carries **`AlwaysNew`** and the handler does **not** resolve **the controller's** `Scoped` instance"
- `tasks.md:1167` — "a **control host** … **no** finding, the resolved options carry `JoinAmbient`, and the handler resolves **the controller's own instance**"
- `tasks.md:1158` — the same host also "calls `AddBrighterRequestScope()` with no argument", an extension that lives in `Paramore.Brighter.Extensions.AspNetCore`

An implementor working the task as sited has two bad options: drop the controller (silently under-implementing AC-50's two runtime clauses, which are the ones that prove the opt-in was *defeated at runtime* and not merely reported), or discover mid-task that the project cannot compile the test.

**Recommendation**: Move `T7.6`'s `Test location` to `tests/Paramore.Brighter.Extensions.AspNetCore.Tests` (as `T7.8`/AC-49 already is at `tasks.md:1196`), and add `T6.2` to its `Depends on`. If the owner instead intends the criterion to be split — validation output in `Extensions.Tests`, runtime clauses in `AspNetCore.Tests` — say so explicitly with a two-file `Test file` line in the shape `T6.22` uses at `:1027`.

---

### 2. `T7.1` adds a second `IAmAPipelineValidator` registration that displaces the core validator, breaking six existing test files that no task migrates — and the repair is sequenced one task later (Score: 80)

`ValidatePipelines()` today registers the core validator with `TryAddSingleton<IAmAPipelineValidator>` (`BrighterPipelineValidationExtensions.cs:71`), and both hosted services resolve **one**: `BrighterValidationHostedService.cs:47,60` takes a bare `IAmAPipelineValidator`, and `ServiceActivatorHostedService.cs:50` calls `GetService<IAmAPipelineValidator>()`. `T7.1` adds a second registration with a plain `AddSingleton`, so from that commit onward the *last* descriptor wins and Brighter's core validation stops running. `T7.2` repairs the two hosted services — but not the direct resolutions in the existing suite, and no task carries that migration at all.

**Evidence**:
- `tasks.md:1072` — "wire it in `ValidatePipelines()`: keep the existing `TryAddSingleton` returning the core validator at `:71` and **add one `AddSingleton` returning this one** (step 6)"
- `tasks.md:1088-1089` (T7.2, the *next* task) — "change `BrighterValidationHostedService`'s field and constructor parameter (`:47`, `:60`) to `IEnumerable<IAmAPipelineValidator>` … change `ServiceActivatorHostedService` (`:50-54`) from `GetService` to `GetServices`"
- `tasks.md:1298` (T7.14 item 12) — the document itself records the consequence: "`GetService<IAmAPipelineValidator>()` **now returns whichever descriptor is last**"

Six existing test files resolve the single validator directly and assert the *core* validator's findings; all six go red at `T7.1` and stay red, because `T7.2` only touches the hosted services:

- `tests/Paramore.Brighter.Core.Tests/Validation/When_validator_resolved_from_di_should_validate_through_full_path.cs:50`
- `tests/Paramore.Brighter.Core.Tests/Validation/When_validation_step_present_and_no_provider_through_di_should_surface_warning.cs:52`
- `tests/Paramore.Brighter.Core.Tests/Validation/When_validate_pipelines_with_producers_should_receive_publications.cs:57` and `:88`
- `tests/Paramore.Brighter.Core.Tests/Validation/When_throw_on_error_true_with_transform_and_provider_triggers_should_not_block.cs:72`
- `tests/Paramore.Brighter.Core.Tests/Validation/When_publication_wrap_transform_unresolvable_through_di_should_surface_warning.cs:64`
- `tests/Paramore.Brighter.Extensions.Tests/When_validate_pipelines_with_consumers_should_receive_subscriptions.cs:60`

`T2.3` is the model for what is missing here: it says outright "⚠ **migrate the 26 facts in the same commit** — the solution does not compile otherwise" (`tasks.md:290`). `T7.1`/`T7.2` have no equivalent clause, and unlike `T2.3`'s case the failure is a *silent green-to-red at runtime*, not a compile error.

**Recommendation**: Either (a) fold ADR 0074 step 5b's hosted-service half into `T7.1` so the `IEnumerable` resolution lands in the same commit as the second registration, or (b) add a `STRUCTURAL` task ahead of `T7.1` — the `T4.2a`/`T7.0a` shape — that widens both hosted services to `IEnumerable` while only one validator is registered (behaviour-preserving). Either way, add an explicit "migrate the seven `GetRequiredService<IAmAPipelineValidator>()` sites across the six files listed, in the same commit" clause, matching `T2.3:290`.

---

### 3. The `Facts:` convention was applied to six tasks; roughly fourteen more span multiple hosts, and the Overview's default rule now contradicts their bodies (Score: 76)

Round 1's fix added the convention as a *general* rule with a hard default:

> "any task whose `Test should verify` spans more than one host carries an explicit **`Facts:`** line … **A task with no `Facts:` line is a single fact.**" (`tasks.md:9`)

Six tasks got the line. A sweep of all 62 behavioural tasks finds at least fourteen more that state their `Then` over two or more hosts or runs and carry no `Facts:` line, so the Overview's default declares them single-fact against their own text.

**Evidence** (each is a task with no `Facts:` line):
- `T2.6`, `tasks.md:329-332` — four explicitly numbered branches: "branch 1 — the handler **completes normally**", "⚠ branch 2 — a handler whose `Handle` throws", "branch 3 — three tracked handlers whose factory's `Release` throws on the first", "branch 4 — a second `Send`"
- `T6.13`, `:889-891` — "branch 1", "branch 2: the same application except…", "⚠ branch 3: a **second host of the same shape**"
- `T6.10`, `:841-844` — branch 1, branch 2 ("the same host with its own delegate setting `AlwaysNew`"), branch 3 ("**each of those two hosts built again**") = four host builds
- `T7.8`, `:1199-1204` — a host, "a second host calling the extension in the **reversed** order", "a third host calling the extension twice with the **same** affinity"
- `T7.9`, `:1218-1222` — a host, "a second host registering **two** overrides", "a third host registering its override as a **constructed instance**"
- `T7.4`, `:1115-1116` — "over four triples … a fifth triple `{Scoped, Singleton, Transient}`"
- `T7.3`, `:1100-1102` — "run once with affinity `JoinAmbient` and once with `AlwaysNew`", then again "with `throwOnError: false`"
- `T6.19`, `:984-986` — the commit run, "⚠ re-run with the controller **rolling back**", "⚠ **the negative control**"
- `T6.11`, `:858-861` — the base host, "the same with the extension call placed **before** `AddBrighter`", "the **mirror**"
- `T6.20`, `:1000` — "run once passing `AlwaysNew` and once passing `JoinAmbient`"
- `T7.7`, `:1185` — "the *same* implementation type registered twice instead: **no** warning"
- `T4.5`, `:539` — "⚠ **a second host of the same shape**"
- `T4.3`, `:498-499` — an all-`Transient` host and a `{Transient, Scoped, Transient}` host
- `T7.1`, `:1064-1066` — `throwOnError: true` and `throwOnError: false`

Several of these are exactly the branches the Overview warns are "the negative and control branches … the ones that make the criterion falsifiable" — `T2.6`'s branch 2 (which ADR 0071 step 6 names *"the one an implementation is most likely to fail"*, `tasks.md:336`), `T7.9`'s third host, `T7.7`'s same-type control.

**Recommendation**: Either add a `Facts:` line to every task whose `Test should verify` names more than one host or run — the same shape as the six that have one — or reword the Overview's default at `:9` so that absence of a `Facts:` line does not assert single-fact, and instead require the count only where the task itself is ambiguous. The first is safer: the count is the thing that stops a multi-branch task being called done when the first fact passes.

---

### 4. `T4.2`'s test cannot go green without the ambient ask, which `T4.3` — the next task but one — is the task that introduces (Score: 74)

`T4.2` is the first task in Phase 4 to require an `IAmAScopeProvider` to actually be *consulted*, but nothing before it puts a `_scopeProvider` field on any factory or calls `GetAmbient`. `T4.3` is the task that does, and says so. This is the same defect shape as round 1's finding 2, one phase over.

**Evidence**:
- `T4.2`, `tasks.md:470` — the `Given` is "an `IAmAScopeProvider` whose `GetAmbient` throws `InvalidOperationException`", and `:471-472` assert the caller sees it unwrapped from both `Send` and `Post`
- `T4.2`'s entire `Implementation should` list (`:475-477`) covers only the six `catch` clauses: "wrap a throw from **the ask** in `AmbientScopeSourceException` inside `CreatePipelineScope()`, and add a clause **ahead of** each existing wrapping `catch` at all **six** sites". It never says who makes the ask, resolves the provider, or computes an affinity to ask with.
- `T4.3`, `tasks.md:508` — "keep each factory's `_scopeProvider` resolved **once, in the constructor**, from the root `IServiceProvider` it already receives, and keep that root provider as a field — `ServiceProviderHandlerFactory` already holds it (`:36`); **the other four gain one**"
- `T4.3`, `tasks.md:505` — "⚠ **the affinity computation is not a ladder row.** It sits between rows 3 and 5, **ahead of the ask**"
- `T4.3`, `tasks.md:506` — "the ladder's other rows belong to other tasks and must not be implemented here: **row 4** (the source throws) is **T4.2's**"

`IAmAScopeProvider` is only declared in `T3.1` (Phase 3) and nothing reads it before Phase 4, so with `T4.2` implemented as written, `GetAmbient` is never called, no exception is raised, and the `Send`/`Post` branches fail. One developer will read `T4.2` as also landing the minimal ask; another will read `T4.3:508` as owning it and conclude `T4.2` is unimplementable in place.

**Recommendation**: Either move `T4.2` to sit after `T4.3` (ADR 0072's ladder row 4 lives inside its step 2 protocol, so this does not violate the "ADR steps keep their order" rule any more than `T4.2a` did), or add an explicit bullet to `T4.2` stating that it lands the minimal ask — `_scopeProvider` resolved in each factory's constructor plus an unconditional `GetAmbient` — and reword `T4.3:508` from "keep … resolved" to "the affinity computation replaces `T4.2`'s unconditional ask", so ownership of the field is stated once.

---

### 5. `T6.20` calls `AddBrighterRequestScope(...)` from a project that has no reference to the new ASP.NET package, and no task adds one (Score: 68)

AC-20 requires the ASP.NET provider to be registered *through the extension* (`requirements.md:682`: "with the ASP.NET provider registered by calling `AddBrighterRequestScope(...)` and the affinity supplied **as that extension's argument**"). `T6.20` sites that test in `tests/Paramore.Brighter.Extensions.Tests`, whose project file at HEAD has five `ProjectReference`s, none to the new package (which `T6.1` creates), and no task adds one. `T6.1` and `T6.2` add only the two new projects to `Brighter.slnx`.

**Evidence**:
- `tasks.md:996` — `- Test location: "tests/Paramore.Brighter.Extensions.Tests"`
- `tasks.md:1000` — "the ASP.NET provider registered by calling `AddBrighterRequestScope(...)` with the affinity supplied **as that extension's argument**"
- `tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj` — `ProjectReference`s to `Extensions.DependencyInjection`, `Outbox.Sqlite`, `ServiceActivator.Extensions.DependencyInjection`, `ServiceActivator.Extensions.Hosting`, `Sqlite.EntityFrameworkCore` only
- `tasks.md:711` (T6.1) and `:723` (T6.2) — each adds only its own project to `Brighter.slnx`

Adding the reference is the obvious fix, but it silently falsifies two stated rationales the task list relies on:
- `T6.2:724` — "it **references the src package and deliberately never calls the extension**, which is **the only arrangement** in which AC-14's spy clause is about anything"
- `T6.22:1034` — "⚠ **why the spy half cannot live beside half 1**: `IHttpContextAccessor` lives in `Microsoft.AspNetCore.Http.Abstractions`, which **no other Brighter test project references** (re-derived: zero of the 37) … so a spy registered beside the suite would record zero accesses whatever the package did"

Once `Paramore.Brighter.Extensions.Tests` takes the package reference (transitively acquiring `Microsoft.AspNetCore.App`), "no other Brighter test project references" it is false and "the only arrangement" is no longer the only one. The two-project split itself is a settled ADR 0073 decision and is not re-opened here — the defect is the unowned project change and the now-stale justification.

**Recommendation**: Add a bullet to `T6.1` (or a line to `T6.20`) stating that `tests/Paramore.Brighter.Extensions.Tests` gains a `ProjectReference` to `Paramore.Brighter.Extensions.AspNetCore`, and add `T6.1` to `T6.20`'s `Depends on`. Then amend `T6.2:724` and `T6.22:1034` to say the spy half must live in `AspNetCore.Tests` because that is the project that takes the reference *and never calls the extension*, rather than because it is the only project holding the reference at all.

---

### 6. `T7.5` instructs the implementor to "add `ArtefactConstructorSelector`", which `T7.0a` has already landed (Score: 62)

Round 1's finding-6 fix introduced `T7.0a` and gave `T7.1` an explicit guard against re-adding what it landed. `T7.5` — the other task that touches those types — did not get the same guard, and still carries a bare "add" instruction for a type `T7.0a` creates.

**Evidence**:
- `T7.0a`, `tasks.md:1049` — "Files, all new under `src/Paramore.Brighter.Extensions.DependencyInjection/`: `ContainerRegistrationSnapshot.cs`, `ScopeConfiguration.cs`, `DescriptorRecord.cs`, `ArtefactRegistration.cs`, `ArtefactKind.cs`, **`ArtefactConstructorSelector.cs`**"
- `T7.0a`, `tasks.md:1054` — "`ArtefactConstructorSelector` is **testable with a `Type` alone**, so **its own selection rule** (the public constructor with the most parameters; where two public constructors have the same parameter count, the type is not inspected — D15) is exercised by T7.5's AC-42 clauses"
- `T7.5`, `tasks.md:1142` — "**add `ArtefactConstructorSelector` as its own object implementing only D15's rule** (widest, tie → none, no public constructor or parameterless → nothing inspected)"
- Contrast `T7.1`, `tasks.md:1069` — "⚠ **not** add the entities or the snapshot — **T7.0a** landed `ContainerRegistrationSnapshot`, `ScopeConfiguration`, `DescriptorRecord`, `ArtefactRegistration`, `ArtefactKind` and `ArtefactConstructorSelector` inert"

It is left genuinely undecided whether `T7.0a` lands the type with D15's rule implemented (its `:1054` reads that way) or as a shell that `T7.5` fills. Two developers resolve this differently, and the second reading also leaves `T7.5` adding a type in a task whose `Depends on` chain has already created the file.

**Recommendation**: Change `T7.5:1142` to match `T7.1:1069`'s shape — "⚠ **not** add `ArtefactConstructorSelector` — **T7.0a** landed it with D15's rule; this task only *exercises* it through AC-42's constructor-selection clauses" — and, if the rule is meant to arrive with `T7.5` instead, say so in `T7.0a:1054` and drop the rule's description from there.

---

### 7. `T6.2` states its test project hosts eight criteria; the task list itself sites twenty test tasks there (Score: 50)

`T6.2` faithfully repeats ADR 0073 step 4a's "eight … stay eight", but that ADR is counting only the ACs *it* cites, and it explicitly hands the distribution question to task breakdown ("Which fixtures the project holds, and how those criteria are distributed across them, is task-breakdown work and is not decided here", `0073-aspnet-core-request-scope-package.md`, step 4c). The task list then sites twenty test tasks in that project.

**Evidence**:
- `tasks.md:724` — "The project has **two roles**: it hosts the **eight** criteria that need a running ASP.NET Core host with a controller action (AC-15, AC-16, AC-17, AC-18, AC-19, AC-34, AC-48, AC-49); and it **references the src package** … so **the eight stay eight**"
- Actual `Test location: "tests/Paramore.Brighter.Extensions.AspNetCore.Tests"` count from the task bodies: `T6.3`–`T6.19` (17), `T6.21`, `T6.22` half 2, `T7.8` = **20** tasks, covering AC-11, AC-12, AC-14 (half), AC-15, AC-16, AC-17, AC-18, AC-19, AC-26, AC-34, AC-37, AC-38, AC-39, AC-45, AC-47, AC-48, AC-49, AC-52, AC-55 plus `T6.8`'s design-owed test

Nothing gets built wrong, but an implementor sizing the project from `T6.2` plans for eight fixtures and finds twenty.

**Recommendation**: Reword `T6.2` to say the eight are ADR 0073's own cited criteria and that the task list places twenty test tasks there in total, listing the phases (`T6.3`–`T6.19` except `T6.20`, plus `T6.21`, `T6.22` half 2 and `T7.8`).

---

### 8. The FR coverage table omits both round-1-added tasks and several task→FR back-references (Score: 45)

`T4.2a` and `T7.0a` were added to the ADR-decisions table (`tasks.md:1483`, `:1495-1496`) but to no row of the FR table, even though each carries a `References` line naming FRs. Several older tasks have the same gap.

**Evidence** (task `References` line vs the FR row that should name it):
- `T4.2a`, `tasks.md:490` references `FR-24.2, FR-24.4, FR-23` — the FR-24.2 row (`:1354`) reads `T4.5 (AC-31), T6.7 (AC-19)`; FR-24.4 (`:1356`) reads `T6.13 (AC-11)`; FR-23 (`:1352`) reads `T4.6, T4.7, T4.8`. `T4.2a` appears in none.
- `T7.0a`, `tasks.md:1057` references `FR-22.1, FR-22.2, FR-22.3, FR-22.4` — it appears in none of rows `:1348-1351`.
- `T3.6`, `:451` references `FR-14`; the FR-14 row (`:1338`) reads `T3.4, T6.3, T6.5, T6.9, T7.6` — no `T3.6`.
- `T5.1`, `:633` references `FR-9(a)`; the FR-9(a) row (`:1331`) reads `T5.2, T6.13, T6.14` — no `T5.1`.
- Same shape for `T4.4`/FR-16(a), `T4.8`/FR-12, `T6.10` and `T7.10`/FR-24.3, `T7.6`/FR-17, `T7.4`/FR-20, `T3.5`/FR-17 and FR-22.4.

No FR is left uncovered, so this is table incompleteness rather than a coverage hole — but the table is the artefact a reviewer uses to answer "which task makes this FR true", and it currently answers wrongly for the two newest tasks.

**Recommendation**: Add `T4.2a` to the FR-23/FR-24.2/FR-24.4 rows and `T7.0a` to the four FR-22 rows (marked "scaffolding", as the ADR table already does), and sweep the remaining eight back-references. The check is mechanical: every FR named in a task's `References` should name that task in its row.

---

### 9. `T7.5`'s `Facts:` line mislabels one of the nine cases (Score: 35)

The `Facts:` line describes the C-20(iv) case as one of "the two that assert **no** warning"; the body correctly asserts a warning **is** reported for it. The body is right; the summary line is wrong.

**Evidence**:
- `tasks.md:1128` — "including the **two that assert no warning for different reasons** (the `Paramore.Brighter.*` prefix exclusion, and **the mapper case that pins C-20(iv)'s deliberate asymmetry**)"
- `tasks.md:1138` — "⚠ the *same* `Paramore.Brighter.Extensions.Tests` assembly with a `Singleton` **mapper** … : a warning **is** reported — pinning C-20(iv)'s gap as a deliberate **asymmetry**. Same assembly, opposite outcome"
- `requirements.md:675` confirms the body: "**Then** a warning **is** reported against that mapper"

**Recommendation**: Reword `:1128` to "including the pair that share an assembly and differ in outcome (the `Paramore.Brighter.*` prefix exclusion asserting *no* warning, and the C-20(iv) mapper asserting one)".

---

### 10. `T7.13` is the only `DOC` task that does not name its verifier task (Score: 25)

`T5.5`, `T7.11`, `T7.12` and `T7.14` each name a verifying task or artefact; `T7.13` names only the ACs.

**Evidence**: `tasks.md:1271` — "No test beyond AC-36's read and AC-44's walk", against `T7.12:1260` — "No test beyond AC-44's reviewer walk (**T7.15**)" and `T7.11:1248` — "**Verified by**: a line on the PR checklist, and by **T7.15**'s reviewer walk".

**Recommendation**: Add "(T7.15)" to `T7.13`, matching `T7.12`.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 3 |

**Total findings**: 10
**Findings at or above threshold (60)**: 6
