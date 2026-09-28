# Review: tasks — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-28
**Threshold**: 60
**Verdict**: NEEDS WORK

7 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Six broken internal task references in Phase 7 — an off-by-one that points four release-note obligations at the wrong task and twice at a task that does not exist (Score: 90)

Phase 7 ends at **T7.15**. Four tasks route their release-note obligation to `T7.15`, which is the *guidance-page verifier* (`DOC: T7.15 — the guidance page is self-sufficient, and the truth table's citations still hold`, line 1250) whose content list contains no release-note item at all. The release-notes task is **T7.14** (line 1227). Two further references name **T7.16**, which does not exist.

**Evidence**:
- T1.14, line 213: "be release-noted: this is one of ADR 0070's five own breaking items, recorded by **T7.15**"
- T3.4, line 398: "…so nothing in this repository breaks. **Release-noted by T7.15**"
- T5.1, line 594: "Binary-breaking on two public constructors … — **release-noted by T7.15**"
- T7.2, line 1040: "be release-noted: an application-supplied `IAmAPipelineValidator` no longer replaces Brighter's validation wholesale (**T7.15**)"
- T5.5, line 664: "**T7.16 re-verifies every row's citation after Phase 6 lands**" — no T7.16 exists; the verifier is T7.15
- T7.12, line 1205: "No test beyond AC-44's reviewer walk (**T7.16**)" — same

The release-note *content* is in fact present in T7.14 (items 3, 10, 11, 12 cover exactly those four breaks), so this is a broken pointer rather than a coverage gap — but an implementor following T3.4 or T5.1 lands on a task that does not accept the item, and T5.5's and T7.12's only stated verification signal resolves to nothing.

**Recommendation**: Renumber every `T7.15` release-note reference to `T7.14`, and every `T7.16` reference to `T7.15`. Then re-scan the whole document for `T\d\.\d+` references and check each against the task headings — this looks like a Phase 7 renumbering that was not propagated.

---

### 2. T4.4 registers a type that T4.5 creates — Phase 4 does not compile in the stated order, and the registration is claimed twice (Score: 80)

The compile-dependency ordering is the whole justification for the phase scheme (Overview, line 5). Inside Phase 4 it is violated: **T4.4** registers `AmbientScopeDiagnostics`, but **T4.5** is the task that *adds* the type, and T4.5's `Depends on` is `T4.4` — so T4.4 lands first, referencing a type that does not exist.

**Evidence**:
- T4.4, line 492: "register `ScopedArtefactCache` (`TryAddScoped`) and **`AmbientScopeDiagnostics`** (`TryAddSingleton`) in `ServiceCollectionExtensions.BrighterHandlerBuilder` (`:142`), the single registration point (step 5)"
- T4.5, line 507: "**add `AmbientScopeDiagnostics`**, registered `TryAddSingleton` on the **Brighter container** … and **never a `static`** (D19)"
- T4.5, line 511: "**Depends on**: T4.4"

Both tasks also claim the `TryAddSingleton` registration, so it is duplicated as well as mis-ordered. A secondary instance of the same problem: T4.3 (line 471) says it implements "rows 4–6 … the single ask, and the `AlwaysNew` outcome", and ADR 0072's step-2 pseudo-code puts three `diagnostics.WarnOnce(...)` calls inside exactly that block — so T4.3, two tasks earlier still, also touches the type.

**Recommendation**: Move the `AmbientScopeDiagnostics` type declaration into T4.3 (or a small structural task ahead of it) and leave only the *behaviour* — the atomic latch and the three conditions — in T4.5; or move the registration bullet out of T4.4 into T4.5 and state explicitly that T4.3/T4.4 hold the diagnostics field nullable and unregistered until then.

---

### 3. Two incompatible "ladder row" numbering schemes are used interchangeably, and T6.13's citation is wrong against ADR 0072 (Score: 74)

ADR 0072's canonical adoption ladder is a **ten-row table** under *The mechanism, end to end* (rows at file lines 156–165). Its step-2 section then restates the same decisions as a **six-step pseudo-code block** with different numbers, and even flags the mismatch inline (`4. if (_scopeProvider is null) return OWNED // ladder row 3`). The task list uses the word "row" for both schemes without saying which.

**Evidence** — canonical ladder: row 3 = *no `IAmAScopeProvider` registered*; row 5 = *`AlwaysNew` ask, something came back* → ignore **and warn**; row 6 = *`AlwaysNew` ask, nothing came back* → OWNED, **diagnostic: none**; row 7 = *`JoinAmbient`, nothing came back* → *no ambient offered*; row 8 = foreign role type; row 9 = failed probe; row 10 = BORROWED.

- **T4.3, line 471** uses the *pseudo-code* numbering while calling it the ladder: "implement **the ladder's rows 1–6**: … **row 3 the affinity computation** …; **rows 4–6 the null-provider return, the single ask, and the `AlwaysNew` outcome**". Under the canonical ladder, row 3 *is* the null-provider return and row 4 is "the ambient source throws".
- **T6.13, line 856** is simply wrong under the canonical numbering: "need nothing beyond T5.2's bracket 1 and T4.5's latch, plus **ladder row 6's ignore-and-warn**". Canonical row 6 emits **no** diagnostic; the ignore-and-warn is **row 5**.
- Meanwhile **T4.6** ("at **ladder row 9**", "the role-type decline at **row 8**", line 525/528), **T6.20** ("reach **ladder row 7** and emit FR-24.2's latched `Warning`", line 966), **T6.12** ("**ladder rows 1 and 2**", line 841) and the **Gaps** section ("ADR 0072 **ladder row 8** (an ambient of a foreign role type)", line 1468) all use the canonical numbering.

Two developers reading T4.3 and T4.6 will not agree on what "row 6" means.

**Recommendation**: Pick the canonical ten-row ladder as the single citation scheme throughout, restate T4.3's scope as "canonical rows 1–7" (or name the outcomes rather than the numbers), and fix T6.13 to cite row 5.

---

### 4. Many one-AC tasks are several tests wearing one task's clothing: one test file, one `/test-first` command, one approval gate, but six to nine distinct hosts (Score: 72)

Owner decision (a) gives each AC one task, one `/test-first` command and one gate. Several ACs have many branches, and the tasks name exactly **one** test file each without saying how many `[Fact]`s or hosts that file needs. `/test-first` produces one test and gates on it; the remaining branches have no gate and no stated shape.

**Evidence**:
- **T7.6** (lines 1104–1114) — one file, `When_the_application_registers_its_own_brighter_options_the_defeated_opt_in_should_be_reported.cs`, but the bullets require: the base host with `throwOnError: false`; the same with `throwOnError: true`; the registration placed *after* `AddBrighter`; the extension passed `AlwaysNew` over a pre-registered `AlwaysNew`; the extension placed *before* `AddBrighter`; "a host of the same shape on **each of the other three entry points**"; and "a **control host**". That is nine hosts.
- **T7.5** (lines 1078–1087) — one file, nine distinct mapper/transform/host configurations, including two that assert *no* warning for reasons that differ from one another.
- **T6.12** (line 835) — "run **three times** with a different triple each time … and each run repeated under **both** affinities" = six host runs, one file.
- **T3.6** (line 417) and **T6.9** (lines 787–790) — four entry points, each rebuilt for the falsifiable direction = eight hosts, one file.
- **T7.10** (line 1182) — "**seven hosts**, each configured to trigger exactly one finding", one file.

**T6.22** is the only task in the document that states its file count ("two test files, two projects", line 988) — proving the format supports it.

**Recommendation**: For each task whose `Test should verify` list spans more than one host, state the expected number of test facts (and whether they belong in one file), and either split the `/test-first` command per fact or state explicitly that the gate is taken once for the file and the remaining facts follow in the same red-green cycle.

---

### 5. T1.3 requires editing 38 test files but enumerates only three of them (Score: 70)

T1.3 must be one atomic commit (`netstandard2.0` has no default interface member, so nothing compiles until every implementation moves — the constraint is real and I verified the counts are correct). But the task hands the implementor the 12 `src/` class names by name and the 38 test files **only as a count**, naming three of them incidentally.

**Evidence**, T1.3 line 44: "**70 test doubles across 38 test files** — re-derived at HEAD: 64 factory doubles (61 on a single-line declaration plus 3 wrapped onto a continuation line, in `When_async_disposing_a_running_dispatcher_it_drains_before_disposing_factories.cs:100`, …) across 37 files, and 6 registry doubles across 3 files, whose union is 38 files."

I re-derived these against HEAD and they are exactly right (64 base-type occurrences across 37 files; 6 registry occurrences across 3 files; union 38 — two files carry both). That accuracy is precisely why the omission matters: the numbers are checkable but the *list* is not, so the implementor must re-derive it mid-commit with no way to confirm completeness before the build breaks. T2.1 has the same shape but is more actionable — it names the suites and the six `TestLifetimeScope` files.

**Recommendation**: Either enumerate the 38 files, or give the discovery command that produces them (e.g. `grep -rln "IAmAMessageMapperFactory\b\|IAmAMessageMapperFactoryAsync\b\|IAmAMessageTransformerFactory\b\|IAmAMessageTransformerFactoryAsync\b\|IAmAMessageMapperRegistry\b" tests --include="*.cs"`) so the count is a check on the list rather than a substitute for it. Do the same in T2.1 for the five `Paramore.Brighter.Core.Tests` factory doubles it leaves unnamed.

---

### 6. T7.1 is a big-bang: one AC-27 test drives eight new types and the whole validation wiring (Score: 68)

T7.1's `Test should verify` is a single rule (FR-22.1, the inert opt-in). Its `Implementation should` stands up ADR 0074 **steps 2, 3, 4, 5 and 6** in one TDD cycle.

**Evidence**, T7.1 lines 1018–1022: "add `ContainerRegistrationSnapshot` … answering **three** queries"; "add `ScopeConfiguration`, `DescriptorRecord`, `ArtefactRegistration`, `ArtefactKind` and `ArtefactConstructorSelector` (step 3), and `ScopeConfigurationRules` with the FR-22.1 specification (step 4)"; "add `ScopeConfigurationValidator` (**public**, `internal` constructor …) … **with its own harvest loop**"; "wire it in `ValidatePipelines()` … (step 6)". Eight new types plus wiring, gated on one test that exercises one of the seven rules; the other six rules' scaffolding ships untested in this commit.

ADR 0074's own *Positive* section says a split is available: "**The rules are unit-testable without a host.** A `ServiceCollection`, an options object and a `Type` are enough for every clause of AC-42 except the two that assert host startup." Nothing in the task list uses that.

**Recommendation**: Precede T7.1 with a `PROJECT`/`STRUCTURAL` task that lands the inert entities and the snapshot (`ContainerRegistrationSnapshot`, `ScopeConfiguration`, `DescriptorRecord`, `ArtefactRegistration`, `ArtefactKind`, `ArtefactConstructorSelector`) uncalled — the same shape T3.5 already uses ("add … the private `RegisterBrighterOptions` helper … **without calling it**") — leaving T7.1 to add only `ScopeConfigurationRules`' FR-22.1 specification, the validator and the wiring.

---

### 7. T2.1 routes `ServiceProviderHandlerFactory` to T2.4; its behaviour lands in T2.3 (Score: 62)

**Evidence**, T2.1 line 229: "Only two implementations do more than answer `null`: `ServiceProviderHandlerFactory` (**T2.4**) and `HandlerLifetimeScope` (T2.2)".

T2.4 (line 274) is *"a throwing handler still releases the pipeline scope, exactly once"* (AC-7), and its implementation bullet says it should "need nothing beyond T2.2's `Dispose()` rewrite". The task that rewrites `ServiceProviderHandlerFactory` is **T2.3** (line 267: "have `ServiceProviderHandlerFactory.CreatePipelineScope()` return a new `ServiceProviderPipelineScope` when `_handlerLifetime` is **not** `Singleton`"; line 268: "**delete** `_lifetimeScopes` (`:40`), `GetOrCreateLifetimeScope` (`:127-131`) and `ReleaseLifetimeScope` (`:133-137`)").

**Recommendation**: Change `(T2.4)` to `(T2.3)`. Sweep the same way as for finding 1.

---

### 8. Two PROJECT tasks and two DOC tasks state no verification signal at all (Score: 55)

`T6.1` and `T6.2` say only "No test. Scaffolding" / "No test of its own. Scaffolding" (lines 673, 682). Neither states what a reviewer checks — not even "the solution builds" or "`Brighter.slnx` restores on `net9.0;net10.0`" — so completion can only be established by looking at the files.

`T5.5` and `T7.11` likewise state no verifier of their own ("No test. This is documentation whose substance is fixed by ADRs…", line 656; "No test. Documentation whose substance is fixed by FR-22.2's rule…", line 1193). Their only signal is T7.15's PR checklist, and T5.5's pointer to it is the dangling `T7.16` of finding 1. By contrast T7.14 does state one ("Verified by a PR checklist, **one line per item in the entry**", line 1228).

**Recommendation**: Give T6.1/T6.2 a build-level completion criterion, and give T5.5 and T7.11 an explicit "recorded on the PR checklist, verified by T7.15" line matching T7.14's.

---

### 9. T1.3 does not say what the four container-backed factories do in the structural commit (Score: 55)

T1.3 line 46: "**Every non-container implementation** except `MessageMapperRegistry` gets the same two-line treatment: `CreatePipelineScope()` returns `null`, `Create` ignores the parameter."

The four container-backed factories (`ServiceProviderMapperFactory`, `…Async`, `ServiceProviderTransformerFactory`, `…Async`) are named in the 12-class list on line 43 but are explicitly excluded from that rule, and the task never says what they do instead. Their real behaviour is T1.5's ("have each of the four container-backed factories return a new `ServiceProviderPipelineScope` … **only when its own configured lifetime is `Scoped`**"). Read literally, T1.3 licenses implementing the container behaviour in a `/tidy-first` commit, which the task's own last bullet forbids ("it must NOT share a commit with behavioural change").

**Recommendation**: Add one clause to T1.3: the four container-backed factories also return `null` and ignore the parameter in this commit; T1.5 replaces those bodies.

---

### 10. The AC-24 coverage row names T7.14 only, hiding a deliberate split (Score: 48)

AC-24's `Then` clauses require **both** `release_notes.md` **and** `docs/guides/lifetimes-and-scoping.md` to state the `MapperLifetime.Scoped` break, C-18's note and the joint consequence. The guidance-page half lands in **T7.13** (FR-25.6 and FR-25.7 bullets, lines 1220–1221), not T7.14, whose `File:` is `release_notes.md` alone. The AC table (line 1360) reads simply `| AC-24 | T7.14 |`, and T7.14 itself asserts an obligation on a file it does not own (line 1246: "Both `release_notes.md` and `docs/guides/lifetimes-and-scoping.md` must state the first three of those clauses").

The substance *is* covered, so this is a table inaccuracy rather than a gap — but the AC-25 row records its split explicitly and AC-24's does not.

**Recommendation**: Change the row to `T7.14 (release_notes.md), T7.13 (guidance-page half)`.

---

### 11. NFR-2's "re-run after T6.1" gate is assigned to no task (Score: 40)

T1.2 line 35: "be re-run as an **explicit gate** at the end of Phase 6, where the NFR-2 clause first becomes falsifiable", and the NFR table (line 1323) reads "NFR-2 | T1.2 (AC-22 clause 2), **re-run after T6.1**". No Phase 6 task carries that gate, and neither T6.1 nor T6.2 mentions it.

In practice T1.2's test is automated and CI re-runs it once T6.1 exists, so nothing is actually lost — but a bullet that calls itself "an explicit gate" with no owning checkbox is the kind of thing that gets dropped.

**Recommendation**: Either add the gate as a line item on T6.1/T6.2, or reword T1.2's bullet to say the automated test re-runs on every build and needs no separate gate.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 11
**Findings at or above threshold (60)**: 7

---

## Note on what was verified and found sound

Recorded so the owner knows where *not* to spend effort.

Every `file:line` anchor spot-checked is correct at HEAD — `TransformPipelineBuilder.cs` `:93/:104/:116/:134/:157/:180/:193/:215-223/:231/:244/:255/:270/:332`, `TransformPipelineDrain.cs` `:46/:67-72/:76`, `PipelineBuilder.cs` `:59/:76/:92/:151/:192-193/:202/:235-236/:248/:269-270/:567/:572/:578/:583`, `CommandProcessor.cs` `:317/:394/:472/:481/:575/:591/:596/:601/:795`, `ServiceProviderLifetimeScope.cs` `:49/:152/:163-178/:185/:406/:422/:436/:449/:462/:522`, `ServiceProviderHandlerFactory.cs` `:34/:36/:40/:49-50/:102-107/:120-125/:127-131/:133-137`, `ServiceCollectionExtensions.cs` `:74/:77-79/:97/:142/:431/:484/:487/:648/:708`, the ServiceActivator DI `:38/:39/:88/:89-90`, `BrighterOptions.cs:9/:37`, `ConsumersOptions.cs:10`, `Performer.cs:31-32/:62-69`, `Each.cs:39-45`, `HandlerLifetimeScope.cs:74-93/:95`, `MessageMapperRegistry.cs:360-362`, `PipelineValidator.cs:45-51/:152`, `BrighterPipelineValidationExtensions.cs:64-66/:68-69/:71/:73-75/:116`, and the six pipeline constructors.

Every count re-derived holds: 12 `src/` classes and 70 doubles across 38 files (T1.3); 6 `src/` classes and 22 test files (T2.1); 26 facts across six files (T2.3); 37 test projects with zero `Microsoft.AspNetCore.*` references and no `tests/Paramore.Brighter.Extensions.AspNetCore.Tests` directory; `src/Directory.Build.props:43/:45`; `tests/Directory.Build.props:4`; no `Microsoft.AspNetCore.Mvc.Testing` entry in `Directory.Packages.props`; ServiceActivator's single `ProjectReference` and zero `PackageReference`s; one `IBrighterOptions` implementation in `src/`; the three AC-14 exclusion files and both "Explicitly NOT excluded" methods at `FactoryLifetimeTests.cs:36` and `:154`.

All 79 tasks carry `Depends on` and `References`; all 62 behavioural tasks carry a `/test-first` command and the ⛔ gate; all 9 structural tasks carry `/tidy-first`. All 55 ACs, all 27 FRs (including every sub-clause) and all 59 ADR steps across the seven ADRs appear in the coverage tables, and the rows sampled — AC-13→T5.2, AC-14→T6.22, AC-46→T4.3, AC-47→T6.16, AC-22→T1.2, ADR 0070 step 9a's seven rows, ADR 0071 step 6's three required tests — are accurate.

The four named traps that could be checked are all honoured: AC-46/AC-55/AC-14 assert their negatives; AC-14 is one task with two files in two projects; AC-54's task says "the container is built **WITHOUT `ValidateScopes`, deliberately** … Do not 'harden' this host"; and no task touches C-2's five frozen types.
