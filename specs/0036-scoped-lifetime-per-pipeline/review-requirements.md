# Review: requirements (revision 16) — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-03
**Threshold**: 60
**Verdict**: NEEDS WORK

9 findings at or above threshold 60. Address these before approving.

> Reviews revision 16 — round-2 design decisions 2 (the `AddBrighterAspNetCoreScopes` →
> `AddBrighterRequestScope` rename) and 4 (FR-22.4, the defeated opt-in) — and everything elsewhere
> in the document that revision 16 makes stale or contradictory. Revisions 1–15 are settled after
> eleven review rounds and were not re-litigated. The revision-12 review, whose Appendix is the
> provenance for a number of grounded codebase facts, is preserved as
> `review-requirements-rev12.md`.
>
> **All codebase claims in revision 16's material were verified against source and check out**: the
> four `TryAddSingleton<IBrighterOptions>` sites (`Extensions.DependencyInjection/ServiceCollectionExtensions.cs:74`,
> `:97`; `ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:38`, `:88`);
> `ThrowOnError` defaulting `true` at `BrighterPipelineValidationOptions.cs:47`; and the live test
> pattern at `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/When_A_Handler_Is_Part_Of_An_Async_Pipeline.cs:23`
> — 125 test files register `IBrighterOptions` themselves, so "the pattern is live in the repository
> today" is if anything understated. **The rename is complete and correct**: nine occurrences over
> six lines (FR-24.2, FR-17 ×3, FR-25.11 ×2, AC-26, AC-20, AC-48), with C-11 the only surviving old
> spelling — though see finding 4 for why that survivor no longer reads correctly.

## Findings

### 1. AC-50 asserts an `Error` finding *and* post-startup runtime behaviour, but never states `throwOnError: false` — under the default the host cannot start (Score: 86)

AC-50's **When** is "the host is built, `IBrighterOptions` is resolved, and **a controller action calls `Send`**", and its first **Then** asserts a *runtime* outcome ("the handler does **not** resolve the controller's `Scoped` instance"). Its second **Then** asserts "validation reports exactly one `Error`".

Those two are mutually exclusive under the default. FR-22 itself states (`:318`) that "All errors above are subject to `ThrowOnError` (`BrighterPipelineValidationOptions.cs:47`, default `true`)". Verified against source: `BrighterPipelineValidationOptions.cs:47` is `public bool ThrowOnError { get; set; } = true;`, and `BrighterValidationHostedService.StartAsync` calls `result.ThrowIfInvalid()` when it is set. An ASP.NET test host that serves a controller action is a *started* host, so validation runs at start, `PipelineValidationException` is thrown, and no controller action ever executes.

This is precisely the split the document's own error-rule ACs make explicitly. AC-27: "`ValidatePipelines()` called last with `throwOnError: true` … **And when** the same host is configured with `throwOnError: false`, **Then** startup succeeds and the same message is logged at `LogLevel.Error`." AC-28 does the same. AC-49 gets away with asserting validation output *and* runtime behaviour in one host only because its finding is a `Warning`, which never throws. AC-50 is the first `Error` AC that also asserts runtime resolution, and it inherited AC-49's shape rather than AC-27's.

The same defect affects the fourth and fifth branches: the three other-entry-point hosts register `ServiceActivatorHostedService`, whose `StartAsync` likewise calls `result.ThrowIfInvalid()` under the default.

**Evidence**: `requirements.md:762-763` against `requirements.md:318`, `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationOptions.cs:47` and `BrighterValidationHostedService.cs:71-88`.

**Recommendation**: State `throwOnError: false` in AC-50's Given (so startup proceeds and the `Error` is captured from the validation result / log), and add an AC-27-style companion branch asserting that with `throwOnError: true` startup fails with `PipelineValidationException` carrying the same message.

---

### 2. AC-50's `AddConsumers(Func<IServiceProvider, ConsumersOptions>)` branch cannot report the `Error` — the host throws `InvalidCastException` first (Score: 84)

AC-50's fifth branch requires "a host of the **same shape** on each of the other three registration entry points — … and `AddConsumers(Func<IServiceProvider, ConsumersOptions>)` alone, each consumer host registering `ServiceActivatorHostedService` explicitly". "Same shape" means the host pre-registers `services.AddSingleton<IBrighterOptions>(new BrighterOptions { … })`.

Verified against source, that host cannot start:

- `AddConsumers(Func<…>)` registers `services.TryAddSingleton<IBrighterOptions>(configure)` (no-op — the app won) **and** `services.TryAddSingleton<IAmConsumerOptions>(sp => (IAmConsumerOptions)sp.GetRequiredService<IBrighterOptions>())` (`ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:88-90`).
- `IBrighterOptions` now resolves to a plain `BrighterOptions`, which is **not** an `IAmConsumerOptions` (`ConsumersOptions : BrighterOptions, IAmConsumerOptions`, `ConsumersOptions.cs:10`). The cast throws.
- `ServiceActivatorHostedService` takes `IDispatcher` as a **constructor** parameter (`ServiceActivatorHostedService.cs:31`), and `IDispatcher` is built by `BuildDispatcher`, whose first act is `serviceProvider.GetRequiredService<IAmConsumerOptions>()` (`ServiceCollectionExtensions.cs:143`). So the throw happens while the hosted service is being constructed — before `StartAsync` runs validation at all.

This is exactly the hazard **C-12** records and warns ACs off: "every resolution of `IAmConsumerOptions` throws `InvalidCastException` at resolve time. This is a **pre-existing hazard** … but a mixed-host AC must avoid walking into it." AC-50 walks into it by a different route — an application registration rather than `AddBrighter` winning the `TryAdd` — and it states neither the ordering nor the object type the branch must register, which C-12 requires of "any AC that sets the affinity flag".

**Evidence**: `requirements.md:767`; `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:88-90`, `:143`; `src/Paramore.Brighter.ServiceActivator.Extensions.Hosting/ServiceActivatorHostedService.cs:29-38`; `requirements.md:377` (C-12's own warning).

**Recommendation**: State in AC-50 that the two consumer hosts pre-register a `ConsumersOptions` instance as `IBrighterOptions` (which satisfies both interfaces and keeps the cast at `:89-90` valid), or that the `Func` overload branch registers `IAmConsumerOptions` as well. Say so explicitly and cite C-12 as the reason — otherwise a reader implements the branch literally and gets a crash rather than the `Error`.

---

### 3. The defeat is framed as a *before*-ordering phenomenon, but MS DI's last-descriptor-wins defeats the opt-in in the *after* ordering too — and one of FR-22.4's two sanctioned detection mechanisms cannot see it (Score: 82)

FR-17's new sentence, FR-22.4 and the revision-history row all state the defeat as arising when the application registers its own `IBrighterOptions` **before** `AddBrighter`/`AddConsumers`:

> "an application that registers its own `IBrighterOptions` *before* `AddBrighter`/`AddConsumers` **wins that registration**" (FR-17, `:274`)
> "an application or test host that registers its own `IBrighterOptions` **first** wins that registration" (FR-22.4, `:315`)

But a plain `services.AddSingleton<IBrighterOptions>(…)` placed **after** `AddBrighter` defeats it equally: `TryAddSingleton` at `ServiceCollectionExtensions.cs:74` succeeds, the application's `AddSingleton` then appends a second descriptor, and MS DI resolves the service type to the **last** descriptor — the application's. Brighter's factory delegate is registered but never resolved; the write-through never reaches any reader. The document establishes this exact resolution rule itself, in FR-24.3: "MS DI resolves `IAmAScopeProvider` to the **last** descriptor registered. The last registration wins."

Three consequences, all live:

1. FR-22.4's *condition* ("the `IBrighterOptions` the container resolves is not the one Brighter registered") does cover the after-ordering — but its **narrative** and FR-17's do not, so an implementer reading the rationale builds only for the before case.
2. The second of the two detection mechanisms FR-22.4 explicitly sanctions — "recorded by Brighter's own registration at the point its `TryAddSingleton` finds the service already present" — **structurally cannot** detect the after-ordering, because `TryAddSingleton` does *not* find the service already present. FR-22.4 offers a mechanism that satisfies only half its own condition.
3. AC-50 never tests it. Its ordering branch varies the position of the *extension* call, not the position of the application's `IBrighterOptions` registration. So the case is unspecified in the narrative, undetectable by a sanctioned mechanism, and untested.

**Evidence**: `requirements.md:274`, `:315-316`, `:766`, `:838`; `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:74`; the document's own last-wins statement at `requirements.md:237`.

**Recommendation**: Restate FR-17 and FR-22.4 as "in either registration order — a pre-existing registration wins the `TryAdd`; a later plain `AddSingleton` wins resolution as the last descriptor (the same rule FR-24.3 relies on)". Either drop the `TryAddSingleton`-time recording mechanism or state that it must be paired with a snapshot check identifying the *last* `IBrighterOptions` descriptor. Add an AC-50 branch that registers the application's `IBrighterOptions` **after** `AddBrighter`.

---

### 4. C-11 still presents `AddBrighterAspNetCoreScopes(...)` as a live working name the ADR "may confirm or change", directly contradicting FR-17 (Score: 78)

Revision 16's stated intent is that "C-11 keeps the old spelling: recording the superseded name is that constraint's job." C-11's text does not do that job. It lists the extension among **three names that are still open**:

> "Three names are **working names**, and for each the *contract* is fixed while the spelling is not: … and the registration extension `AddBrighterAspNetCoreScopes(...)`, whose contract … is fixed by FR-17 (**D13**, **D18**). **The ADR may confirm or change any of the three spellings**"

That is now false on two counts: the ADR has already made the call (that is why revision 16 exists), and nothing marks the spelling as superseded. Meanwhile FR-17 says "The exact member name is a working name (**C-11**)" and spells it `AddBrighterRequestScope` — so FR-17 routes the reader to C-11 as the naming authority, and C-11 gives a *different* name. Two developers following the citation chain get two different method names for the same member.

C-11 already has the machinery to record superseded names — "`IAmAChainScope`, `IAmAPipelineScope` and `IAmAUnitOfWorkScope` were considered and rejected" — but the extension name was not moved into that clause; it was left in the open-working-names clause.

**Evidence**: `requirements.md:374` (C-11) against `:274` (FR-17), `:345` (FR-25.11), `:585` (AC-26), `:641` (AC-20), `:741` (AC-48), all of which now say `AddBrighterRequestScope`.

**Recommendation**: Rewrite C-11's third bullet so the extension's spelling is **settled** as `AddBrighterRequestScope(...)` by ADR 0073, with `AddBrighterAspNetCoreScopes(...)` recorded as the superseded working name — and drop the extension from "the ADR may confirm or change any of the three spellings", which now applies to two names, not three.

---

### 5. FR-22.4's "detection must not rest on comparing affinity values" has no discharging AC — AC-50 is passed by exactly the implementation the rule forbids (Score: 76)

FR-22.4's second paragraph is a new normative constraint on the *implementation*:

> "**Detection must not rest on comparing affinity values.** An override carrying `AlwaysNew` — the option's own default (FR-14) — is by value indistinguishable from an override that was never applied…"

Every branch of AC-50 uses the same configuration: the extension is called **with no affinity argument**, so the override carries `JoinAmbient`, while the resolved (application-registered) object carries `AlwaysNew`. The two values *differ* in every branch. A naive implementation that simply compares "override value" against "resolved value" and reports an `Error` when they differ passes every clause of AC-50 — including the control host, where no mismatch exists.

The rule's whole point — that a host calling `AddBrighterRequestScope(ScopeAffinity.AlwaysNew)` over a pre-registered `IBrighterOptions` must **still** be reported — is asserted nowhere. This is the same class of gap the document has caught and closed before, and it holds itself to that standard explicitly: AC-45's "The non-default starting value is what makes this clause falsifiable"; AC-31's second branch "Without this branch an implementation that warns on every `AlwaysNew` ask … would pass"; AC-42's prefix case "the only case in this AC that **fails** under an `== "Paramore.Brighter"` implementation".

**Evidence**: `requirements.md:316` (the rule) against `:761-769` (every AC-50 branch calls `AddBrighterRequestScope()` with no argument).

**Recommendation**: Add an AC-50 branch in which the extension is passed `ScopeAffinity.AlwaysNew` over a pre-registered `IBrighterOptions` that also carries `AlwaysNew` — values identical, override defeated — and assert the same single `Error` still fires, with a note in AC-45's and AC-31's idiom saying this branch is the only one that fails under a value-comparison implementation.

---

### 6. FR-17 still offers "writing through to the resolved instance" as a permitted design, under which FR-22.4's premise is false and AC-50's first Then fails (Score: 72)

FR-17's third paragraph — untouched by revision 16 — leaves the write-through mechanism open:

> "*How* — bringing the other three paths onto `IOptions`, **or writing through to the resolved instance** — is design work (C-13); that it must hold on all four is not."

"Writing through to the resolved instance" reads as: obtain whatever object `IBrighterOptions` resolves to, and set the affinity on it. Under that reading the application's own pre-registered object *receives* the affinity, the opt-in works, and there is no defect to report — which falsifies FR-22.4's premise ("the write-through never runs and the affinity the factories read is not the one the extension carried") and makes AC-50's first **Then** ("the resolved `IBrighterOptions` carries **`AlwaysNew`**") fail on a conforming implementation.

FR-22.4 and AC-50 are therefore not statements about observable behaviour required of any conforming design — which is what C-13 says this document is limited to — but statements about the behaviour of one particular permitted design.

**Evidence**: `requirements.md:277` against `:315` and `:763`.

**Recommendation**: Narrow FR-17 ¶3 so the write is applied by **Brighter's own `IBrighterOptions` registration**, which is what makes FR-22.4's condition meaningful — and note that the resolve-then-mutate reading is already dead on FR-17's own terms, since AC-48's before-ordering leaves no descriptor to wrap. Alternatively condition FR-22.4/AC-50 on the write-through being applied by Brighter's own registration. As it stands the two paragraphs of FR-17 answer the same question differently.

---

### 7. Five other places still state that the extension wins unconditionally, or that the opt-in reaches all four paths, with no reference to FR-22.4's exception (Score: 70)

FR-17's *first* paragraph gained the qualifier. Every other member of the family did not:

- **FR-17 ¶3** (`:277`): "It is a requirement of this work that the affinity **passed to the extension** takes effect on whatever object `IBrighterOptions` resolves to, on **all four** paths (AC-45)." Unqualified.
- **C-10** (`:373`): "that argument **wins unconditionally** (**D18**)". Unqualified, and C-10 is the constraint a reader consults for the ordering question.
- **C-12a** (`:375`): "FR-17 requires the opt-in to work on all four paths **regardless** (AC-45)". Unqualified.
- **C-12** (`:376`): the `TryAdd`-first-wins constraint — *the very mechanism FR-22.4 is about* — discusses only the mixed producer+consumer host and says nothing about an application-supplied `IBrighterOptions` or FR-22.4. This is the constraint that should carry the new hazard.
- **D18** in the Decisions table (`:813`): "The extension's affinity **argument is the value** and **wins unconditionally**". Its "Where it lands" column also omits FR-22.4 and AC-50 (weighed as a reading aid per that section's own caveat — the Decision cell's wording is the substantive part).

The signature failure mode this document warns about is exactly this: a new rule added over a family without re-reading every member.

**Evidence**: `requirements.md:277`, `:373`, `:375`, `:376`, `:813`.

**Recommendation**: Add the one-clause exception ("except where a pre-existing `IBrighterOptions` registration defeats it, which FR-22.4 reports") to FR-17 ¶3, C-10 and D18's Decision cell; and add to C-12 the sentence that its `TryAdd` first-wins hazard is what FR-22.4 diagnoses, with AC-50 named.

---

### 8. AC-44's new bullet states the defeated affinity is always `AlwaysNew`, which is only true of AC-50's particular host (Score: 62)

AC-44's added bullet requires the guidance page to state, for the defeated-opt-in error, "**what the affinity was while the override was defeated (the option's default, `AlwaysNew`)**".

That parenthetical is a property of AC-50's host, not of the rule. FR-22.4 fires whenever the resolved `IBrighterOptions` is not Brighter's, whatever that object carries. An application that registers `new BrighterOptions { DefaultScopeAffinity = ScopeAffinity.JoinAmbient }` and calls `AddBrighterRequestScope(ScopeAffinity.AlwaysNew)` trips FR-22.4 while the effective affinity is `JoinAmbient` — the opposite of what AC-44 requires the page to say. A guidance page written to AC-44 as drafted would tell such a reader something false, which is a direct NFR-10 problem.

**Evidence**: `requirements.md:707`.

**Recommendation**: Change the parenthetical to "whatever affinity the application's own object carries — the option's default, `AlwaysNew`, unless the application set it", or drop the specific value and require the page to say the effective affinity is the one on the application's own object rather than the extension's.

---

### 9. AC-18's "AC-43's count of exactly four findings" is stale — AC-43 now has six hosts and states no such count (Score: 60)

AC-18's Given explains why it does not call `ValidatePipelines()`:

> "and `ValidatePipelines()` is **not** called, so FR-24.3's duplicate-implementation-type warning does not arise here and **AC-43's count of exactly four findings** is unaffected"

AC-43 has not been about four findings since revision 15 (five) and is now six. AC-43 also no longer states a "count of findings" — it counts *hosts*, one finding each. This is a counting site inside the family revision 16 was obliged to sweep: it is the only remaining "four" in a validation-message context outside the revision-history rows.

It does not change what gets built, but it is a self-referential inaccuracy of exactly the kind revision 13 was raised to fix in the header.

**Evidence**: `requirements.md:561` against `:695-697`.

**Recommendation**: Reword to "AC-43's six single-finding hosts are unaffected", or drop the clause — the substantive point (no duplicate-provider warning arises in AC-18's host) stands on its own.

---

### 10. FR-22's heading and lead still describe a requirement that now carries four rules, only one of which is an inert opt-in (Score: 52)

> "**FR-22 — An inert opt-in is reported by `ValidatePipelines()`.** Because all three lifetimes default to `Transient` … the combination "opted in, nothing `Scoped`" is almost certainly a configuration mistake and must not be silent."

FR-22 is cited throughout as the home of *all four* validation rules (FR-25.10 "FR-22's four"; AC-43 "three errors"; FR-24.3 "the same obligation FR-22 imposes on its own messages"). The heading was already loose at three rules; the fourth — which is about an opt-in that is not *inert* but *lost* — makes it actively misleading as a section title. Pre-existing, but squarely inside revision 16's subject matter.

**Evidence**: `requirements.md:298`.

**Recommendation**: Retitle to something like "**FR-22 — Unworkable lifetime and opt-in configurations are reported by `ValidatePipelines()`**", and move the inert-opt-in rationale into rule 1 where it belongs.

---

### 11. Two rules can fire on one configuration and the document does not say what happens then (Score: 48)

AC-50 correctly derives its "no *other* finding is raised" clause from the rules as written for *its* host (the resolved affinity is `AlwaysNew`, so FR-22.1's `JoinAmbient` precondition fails; the triple is conformant, so FR-22.2 does not fire). But a host whose pre-registered `IBrighterOptions` itself carries `DefaultScopeAffinity = JoinAmbient` with three `Transient` lifetimes trips **both** FR-22.1 and FR-22.4. Unlike FR-24's diagnostic model, which fixes an explicit exclusivity order for its three log conditions, FR-22's four rules state no interaction rule at all.

Reporting both is defensible — they are different defects with different remedies — so this is a gap in statement rather than a contradiction.

**Evidence**: `requirements.md:301`, `:315`, `:765`; contrast the exclusivity model at `:244`.

**Recommendation**: One sentence in FR-22 stating that its four rules are evaluated independently and may each produce a finding on one configuration (unlike FR-24's mutually exclusive diagnostics), so an implementer does not invent a precedence order.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 7 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 1 |

**Total findings**: 11
**Findings at or above threshold (60)**: 9
