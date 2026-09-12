# Review: design — 0036-scoped-lifetime-per-pipeline (round 3)

**Date**: 2026-08-04
**Threshold**: 60
**Verdict**: NEEDS WORK

39 findings at or above threshold 60. Address these before approving.

**How this round was run.** Eight reviewers, all on opus, all kept blind to rounds 1 and 2 (`review-design-round1.md`, `review-design-round2.md`) and to `PROMPT.md`: one per ADR over the seven, plus one whose only remit was set-level properties. Each verified `file:line` citations against current source rather than reading them, recounted every count it reported, rendered every mermaid block with `mermaid-cli@11`, and ran the escaped-entity grep. One reviewer (0075) compiled and ran a .NET 10 probe to test a claim about `AsyncLocal` and `Parallel.ForEach`.

**Set-level mechanical state, independently re-verified this round**: 15 mermaid diagrams across the seven, **all 15 render**; escaped entities **0** in all seven; `docs/adr/index.md` byte-identical to a fresh regeneration at `_98 ADRs indexed._`; all seven sibling maps byte-identical after normalisation, correct row bolded in each; the unifying sentence present verbatim in all seven (9 occurrences, no variants); frontmatter uniform; supersession statement in all seven; tone sweep clean in all seven (the two `chain` hits are both legitimate — builder chaining, and the rejected `IAmAChainScope` name).

---

## Convergences — findings reached independently by more than one blind reviewer

These carry more weight than their individual scores. Two reviewers arriving at the same defect from different files, with no shared context, is the strongest signal this round produced.

| Finding | Reached by | Scores |
| --- | --- | --- |
| **FR-13's disposal-failure clause / AC-33 has no owner on the handler path, and 0071's `AggregateException` contradicts it** | 0070 (#1), 0071 (#1), set-level (#1) | 78, 84, **92** |
| **"two of the eight are not factories" — it is three** (the two registries and `IAmALifetime`) | 0070 (#4), set-level (#6) | 50, 62 |
| **FR-17 is claimed by three ADRs with three incompatible partitions** | 0076 (#2), set-level (#3) | 65, 74 |

---

## Triage — the owner's two buckets

### Needs a decision (bring one at a time, with context, options and a recommendation)

| # | Finding | Score | Why it is not mechanical |
| --- | --- | --- | --- |
| D-1 | AC-33 / FR-13 on the handler path — 0070 claims it, 0071 specifies the opposite | 92 | Choosing which ADR moves, and whether 0071 keeps composition for handler `Release` failures while logging-and-swallowing scope-disposal failures, changes a decision landed as round-2 decision 3 |
| D-2 | 0070 step 4a's two log levels are not implementable — the three release sites cannot discriminate a scope-disposal failure from a mapper-release failure | 72 | The fix is a new exception type, or moving the logging into `TransformPipelineDrain` — a design change either way |
| D-3 | `FrameworkReference` flows transitively, so a non-web host taking the 0073 package reference acquires the ASP.NET Core shared-framework requirement | 70 | Either qualify the inertness claim, or reopen `FrameworkReference` vs `PackageReference` in *Technology Choices* |
| D-4 | FR-21 is discharged by no ADR — 0076 forwards it to 0072, which never names it | 76 | Assigning an owner is an ownership call across the set |
| D-5 | FR-17's three-way split across 0073 / 0074 / 0076 | 74 | Three `Scope` sentences must be rewritten to one agreed partition |
| D-6 | 0072's `ScopedArtefactCache` is specified two incompatible ways on faulted resolutions; issue #4260's fix is called a prerequisite but has no implementation step | 70 | Either the fix is in scope with a step, or fault-caching is an accepted documented limitation |
| D-7 | Step 7a omits two breaks it should carry (0070's own six pipeline constructors; 0074's `IAmAPipelineValidator` resolution change) | 72 | Changes the release-note ledger's contents and the "seven items" count fixed in the set |

### No design decision needed — fix directly

Everything else: 32 findings at or above threshold plus 28 below it. Miscited requirement ids, stale counts inside an ADR, citation-density violations, a `:636`-for-`:637` line slip, a missing `Where each type is touched` table in 0076, heading wording drift, diagram message placement, and the eleven ACs cited by no ADR.

---

## Findings

## ADR 0070 — per-pipeline DI scope for mapper and transform factories

### 1. FR-13's disposal-failure clause and AC-33 are claimed here, but the case AC-33 specifies (a `Send`) is delivered by no ADR in the set (Score: 78)

*Scope* states it discharges FR-13's disposal-failure clause "(step 4a, AC-33)". Step 4a sites the new `FailedToDisposePipelineScope` message at exactly three places — `OutboxProducerMediator.cs:1448`, `Reactor`, `Proactor` — all **transform**-pipeline release sites. AC-33's Given/When is a **handler** pipeline: a `Scoped` dependency of the handler whose `Dispose()` throws, `Send` called, handler completes normally, failure recorded at `LogLevel.Error`. A `Send` passes through none of the three sites, and this ADR says explicitly that handler pipelines are not touched here.

**Evidence**: `grep -ln "AC-33\|FR-13" docs/adr/007[0-6]*.md` returns only 0070 and 0072. 0072 disclaims it — "its disposal-failure clause and AC-33 are ADR 0070's". 0071 contains no occurrence of `FR-13`, `AC-33` or `LogLevel.Error`; its summary says `HandlerLifetimeScope.Dispose()` composes the failures, i.e. the handler-path disposal failure propagates as an `AggregateException` — the opposite of FR-13's "logged at `Error` and swallowed, and the caller's result returned unchanged" (requirements.md:230).

**Recommendation**: Either narrow the Scope sentence to FR-13's disposal-failure clause *for transform pipelines* and drop AC-33 from line 32 and the touched-table row, or add the handler-pipeline release path here and reconcile with 0071. Either way 0071 must be amended in the same pass so AC-33 has an owner.

---

### 2. The three release sites cannot tell a scope-disposal failure from a mapper/transform release failure, so step 4a's two log levels are not implementable as written (Score: 72)

Step 4a's rationale for new messages is discrimination — "unable to tell a throwing mapper `Release` from a throwing scope disposal, which is exactly the discrimination AC-6 asks for". But step 5 puts the scope release **inside** the pipeline's drain, with the existing hold-and-compose error handling extending to the third step so both surface as an `AggregateException`. The three release sites see only the single exception the drain threw. Nothing says how they choose between `FailedToDisposePipelineScope` at `Error` and `FailedToReleasePipeline` at `Warning`.

**Evidence**: `TransformPipelineDrain.cs:46-76` holds the first failure, runs the second step, and on a double failure throws `new AggregateException(scopeError, releaseError)`; on a single failure rethrows via `ExceptionDispatchInfo`. `OutboxProducerMediator.ReleasePipeline` (`:1269-1279`) is one `try`/one `catch`/one message with no type information distinguishing the third step. `Reactor`/`Proactor` swallow identically. The failed-*build* half **is** specified (`CleanUpAfterFailedBuild` logs and swallows); only the completed-pipeline half is under-specified.

**Recommendation**: Specify that the third drain step's failure is surfaced in a discriminable form — a named exception type declared in the touched table — or state that `TransformPipelineDrain` emits `FailedToDisposePipelineScope` itself and the three release sites are unchanged (which would also simplify the touched-table row and the "unchanged" list, both of which currently promise edits to `Reactor` and `Proactor`).

---

### 3. Two `The forces` bullets carry four and five `file:line` citations each (Score: 60)

`.agent_instructions/documentation.md` § *ADR readability*: "At most one per forces or Consequences bullet."

**Evidence**: The bullet "The registry sits between the builder and the mapper factory" carries five (`:51`, `:332`, `:330`, `:50`, `:255`); the next bullet carries four (`:807`/`:808`, `:945`, `:957`); the Context paragraph carries seven. Every citation resolves correctly — this is a readability defect, not a grounding one. Note documentation.md:87 names this ADR as "the worked example of the full shape", so the corpus currently teaches the violation.

**Recommendation**: Reduce each bullet to the one fact that narrows the solution space and move the refs into *Implementation Approach*, which already restates them.

---

### 4. "two of the eight are not factories" — three of the eight are not factories (Score: 50)

Step 7a's arithmetic is load-bearing: it exists to stop a release-note writer following AC-24's "the six factory interfaces" and undercounting.

**Evidence**: The eight are this ADR's six plus 0071's `IAmAHandlerFactory` and `IAmALifetime`. Non-factories: the two registries **and** `IAmALifetime` — three. Separately, AC-24 (requirements.md:677) and NFR-1's withdrawal block (`:352`) fix a *differently composed* six (four mapper/transformer factories plus `IAmAHandlerFactorySync`/`Async`). The ADR's plan is a superset and satisfies AC-24 in outcome, but the mismatch is never named. **Converges with set-level finding 6.**

**Recommendation**: "three of the eight are not factories — the two mapper registries and `IAmALifetime`", plus a half-sentence noting AC-24's six is a different six and the single entry covers the union.

---

### 5. `Reactor` is cited at `:636` in the touched table and `:637` twice elsewhere; `:636` is a blank line (Score: 45)

**Evidence**: `Reactor.cs:636` is empty; `:637` is the `[LoggerMessage]` attribute, `:638` the method. The sibling citations in the same row (`OutboxProducerMediator :1448`, `Proactor :651`) both land on the attribute line, so `:636` is the outlier.

**Recommendation**: `:636` → `:637`.

---

### 6. The test-double file count double-counts: "three more" files are not additional (Score: 40)

**Evidence**: Multi-line-aware scan gives 12 in `src/` and 70 in `tests/` across **38** distinct files, of which 6 are registry doubles in 3 files — two of those three also contain factory doubles and are already among the 37. Only one file is "more". Every other count in the ADR (70, 64, 12, six core factories, five container-backed factories) checks out exactly.

**Recommendation**: "…64 factory doubles across 37 test files, and six registry doubles in three files, one of which contains no factory double — 38 test files in all."

**Verification (0070)**: diagrams 2/2 render, both read as PNG; escaped entities 0; ~90 `file:line` citations spot-checked across 30+ files, all held except `Reactor.cs:636`; requirement ids FR-1–7, FR-13, FR-16, FR-20, FR-24, FR-25, FR-27, NFR-1–8, C-1/2/3/6/8/16/17/18/19, D0/3/4/7/10/12, OOS-7/8, AC-5/6/8/24/30/33 cross-read; headings an exact match to the skeleton; tone clean.

**Counts (0070)**: 90-100: 0 · 70-89: 2 · 50-69: 2 · 0-49: 2 · total: 6 · at-or-above-60: 3

---

## ADR 0071 — pipeline scope handle for handler pipelines

### 1. The specified `Dispose()` behaviour contradicts AC-33/FR-13, and neither is cited anywhere in the ADR (Score: 84)

*Implementation Approach* step 2 specifies that `HandlerLifetimeScope.Dispose()` disposes `PipelineScope` last and unconditionally, holding any failure, and "if anything was held, throw them composed as an `AggregateException`". That makes a failed pipeline-scope disposal on a successfully-completed handler pipeline propagate out of `PipelineBuilder.Dispose()`, out of `using var builder`, and out of `Send`. AC-33 requires the opposite, and AC-33's scenario is a handler pipeline on `Send`.

**Evidence**: AC-33 (requirements.md:532-537) — "the caller observes normal completion and the handler's result unchanged, and a capturing `ILoggerProvider` records the disposal failure at `LogLevel.Error`; **And when** a second `Send` is issued, **Then** it succeeds identically — the failure is logged and swallowed, not latched." FR-13 (`:230`) — "logged at `Error` and swallowed… must not be latched." `grep -c "FR-13\|AC-33"` on 0071 = 0; `grep -ni "swallow\|LogLevel\|logged"` = no matches. **Converges with 0070 #1 and set-level #1.**

**Recommendation**: Split the composition rule. Handler *release* failures compose and throw (the repair this ADR argues for); the **handle disposal** failure on a completed pipeline is caught, logged at `Error` via a message this ADR names, and swallowed, with nothing latched. Add FR-13 and AC-33 to *References*, and state in *Consequences* which of the two failure kinds an application can observe.

---

### 2. The external cost of breaking the handler factory family is never assessed, on an interface whose own documentation instructs users to implement it (Score: 70)

The ADR discharges no requirement and is justified on structure alone, so its cost accounting has to be strong. It counts only in-repo implementations and stops. 0070 does confront the external question for its six ("no known public implementations… the registry's own documentation says the interface is provided for testing"). That argument does not transfer: the handler factory is the interface Brighter's own XML documentation tells applications to implement.

**Evidence**: `IAmAHandlerFactorySync.cs:32-34` — "we require clients of the Paramore.Brighter library need to implement `IAmAHandlerFactorySync`… Typically you would use an IoC container." Counts independently reverified: `IAmAHandlerFactory` 21 distinct types (5 `src/`, 16 doubles); `IAmALifetime` 7 (1 + 6) — both exactly as stated.

**Recommendation**: In *Negative*, state and argue the external blast radius as 0070 does for its six.

---

### 3. `NFR-1(c)` and `AC-24` are cited as covering breaks they do not cover (Score: 68)

*Negative* routes both interface breaks to "(NFR-1(c), AC-24)". NFR-1's withdrawn freeze names exactly six interfaces; neither `IAmAHandlerFactory` (the bare base marker) nor `IAmALifetime` is among them. AC-24's obligation is written over "the six **factory** interfaces" — `IAmALifetime` is not a factory interface.

**Evidence**: requirements.md:352 (NFR-1), `:677` (AC-24). 0070 noticed this and said so; 0071 does not, though it is meticulous elsewhere about flagging AC amendments (it does so for AC-9).

**Recommendation**: Say plainly, as 0070 does, that AC-24's enumeration needs widening to eight interfaces of which three are not factories, and that NFR-1(c)'s withdrawal is read as extending to the shared base and `IAmALifetime`. State it as an amendment, not a discharge.

---

### 4. `ServiceProviderPipelineScope` is silently widened from 0070's specification, and is absent from `Where each type is touched` (Score: 64)

0070 specifies the type as owning a `ServiceProviderLifetimeScope` **configured `Scoped`**, returned only when the factory's own lifetime is `Scoped`. This ADR needs it configured `Transient` too, carrying `IsolateTransientHandlerScope`. The requirement is stated in *Technology Choices* but framed as a restatement of 0070 rather than a change to it — 0070's word "`Scoped`" is dropped from the quotation — and the type appears nowhere in this ADR's touched table.

**Evidence**: 0070 `:273`, roles table `:176`, step 5 `:347`. The three-argument constructor call matches `GetOrCreateLifetimeScope` (`ServiceProviderHandlerFactory.cs:127-131`).

**Recommendation**: Add a touched-table row and say in *Technology Choices* that this widens 0070's "configured `Scoped`" rather than restating it.

---

### 5. Dangling internal cross-reference, with a count that does not match its own enumeration (Score: 63)

The *Negative* bullet on synchronous release points at *Technology Choices* for "the four declarations". *Technology Choices* contains no interface declarations. The count is also off: "`IAmALifetime` and both builder interfaces" is three, not four.

**Evidence**: `grep -n "IDisposable\|IAsyncDisposable"` returns only lines 212, 334, 336; *Technology Choices* spans 264-272 and contains none. The three types do check out (`IAmALifetime.cs:34`, `IAmAPipelineBuilder.cs:36`, `IAmAnAsyncPipelineBuilder.cs:37`) but are never cited. The sibling cross-reference in the same section *is* valid, which is why the broken one reads as a copy of a working pattern.

**Recommendation**: Inline the three `file:line` declarations, or move them into *Technology Choices* and fix "four".

---

### 6. A single forces bullet carries nine `file:line` citations (Score: 62)

**Evidence**: The third forces bullet carries nine, all of which resolve correctly, and all nine are repeated verbatim in *Technology Choices* and again in Alternative 2 — so nothing is lost by removing them from the argument.

**Recommendation**: Reduce to the claim; keep the citations in *Technology Choices*.

---

### 7. Whether `ServiceProviderHandlerFactory.Release`'s **body** changes is left ambiguous (Score: 55)

*Technology Choices* asserts `Release` "does… nothing, for a `Singleton`, and for the rest, nothing either", while two paragraphs later requiring "today's `GetOrAdd`/`TryRemove` behaviour exactly" on the fallback path — and `TryRemove` lives in `Release`. Step 4 covers `CreatePipelineScope()` and both `Create` overloads and says nothing about `Release`'s body.

**Evidence**: `ServiceProviderHandlerFactory.cs:102-107` returns early for `Singleton` and otherwise calls `ReleaseLifetimeScope(lifetime)` (`:133-137`). An implementor taking "does nothing" literally and deleting that call breaks the no-handle path.

**Recommendation**: One sentence in step 4: `Release` is not modified — its `ReleaseLifetimeScope` call is a no-op on the handle path and is what reclaims the fallback path.

---

### 8. "does what its own documentation already claims it does — nothing" overstates the source (Score: 45)

**Evidence**: `ServiceProviderHandlerFactory.cs:94-99` claims it does not dispose the *handler*, while stating that disposing the scope is what disposes the handler — and today `Release` is precisely what disposes that scope. The ADR's own *Positive* section says so ("`Release` stops having a hidden second job").

**Recommendation**: "…does nothing *to the handler*, which is what its documentation already says; what changes is that it no longer disposes the pipeline's DI scope either."

---

### 9. The two comparison sequence diagrams reorder their participants (Score: 40)

**Evidence**: `today` is `Builder, Lifetime, Factory, Dict`; `mechanism` is `Builder, Factory, Lifetime, Scope`. The Decision invites the comparison ("the same three things happen at the same three moments") but the reader has to re-anchor.

**Recommendation**: Order the second `Builder, Lifetime, Factory, Scope` so only the fourth lifeline changes identity.

**Verification (0071)**: diagrams 3/3 render (exit 0, SVGs 30/31/21 KB), two read as PNG; escaped entities 0; ~40 citations checked, **every cited line number resolved correctly**; counts independently reverified with a multi-line-aware scan and all matched exactly; requirement ids FR-6–13, FR-24, FR-27, NFR-1 (incl. revision-14 withdrawal), NFR-3–8, C-1/2/6/16, D0/0b/0c/2/10/11/16/17, AC-6/7/9/24/33/46 cross-read; headings exact; tone clean.

**Counts (0071)**: 90-100: 0 · 70-89: 2 · 50-69: 5 · 0-49: 2 · total: 9 · at-or-above-60: 6

---

## ADR 0072 — ambient scope adoption seam

### 1. "a pipeline only reaches step 5 with `Scoped` participating and no `Transient` participant" contradicts the ladder, D16 and AC-46 (Score: 74)

Step 5 **is the ask** (`ambient = _scopeProvider.GetAmbient(affinity)`). A `{Scoped mapper, Transient transformer}` pipeline reaches step 5 — that is the whole of D16 and of ladder rows 5 and 6 — it simply reaches it carrying `AlwaysNew`. The property the sentence reaches for belongs to the **BORROWED outcome** (the last line of step 6). As written, a reader concludes the ask is skipped when a `Transient` participates, which is exactly the implementation AC-46's second branch fails.

**Evidence**: line 406. Contradicted by rows 5/6, by line 195 ("The ask is made even when the affinity is `AlwaysNew`"), by line 344, by AC-46 branch 2 (requirements.md:784) and by D16 (`:820`).

**Recommendation**: "a pipeline only reaches the **BORROWED** outcome — the last line of step 6 — with `Scoped` participating and no `Transient` participant". Sweep for other numbered-step references that name the ask when they mean the outcome.

---

### 2. `ScopedArtefactCache` is specified two incompatible ways on faulted resolutions, and the #4260 fix has no implementation step (Score: 70)

The `GetOrAdd` contract table specifies *inheriting today's protocol verbatim* — `ConcurrentDictionary<Type, Lazy<object?>>` in default `LazyThreadSafetyMode`, which caches the fault — and says the issue "is not re-litigated in this ADR". But *Risks* states a flat normative requirement the other way, and *Negative* calls the fix a prerequisite. Neither the contract table nor any of steps 1–6 contains a step for it. Two developers build two different caches.

**Evidence**: lines 274, 281 against 443 and 430. `ServiceProviderLifetimeScope.cs:163-178` and `:152` do use `Lazy<object?>` in default mode, so the characterisation of existing code is accurate; the defect is the ADR's internal split.

**Recommendation**: Decide it here — either add the eviction-on-fault clause to the **Error conditions** cell plus a numbered step, or drop "must be fixed as part of this work"/"prerequisite" and record fault-caching as an accepted documented limitation.

---

### 3. The stated ground for row 8 — "an ambient this container package cannot resolve from is declined" — is not what the type test or the probe discriminates (Score: 64)

`IAmAServiceProviderScope.Services` is typed `System.IServiceProvider`, which every container's adapter exposes (`AutofacServiceProvider` among them), and the probe is specified as resolving `IServiceScopeFactory` and treating **only** `ObjectDisposedException` as failure — an Autofac adapter supplies one. So a foreign-container ambient implementing the role passes both rows and is **borrowed**; if that provider cannot resolve Brighter's artefacts the caller gets `ConfigurationException` (`PipelineBuilder.cs:193`), not the promised latched Warning. The probe's behaviour when `GetService(typeof(IServiceScopeFactory))` returns **`null`** is also unspecified.

**Evidence**: lines 227, 290, 432 against line 383.

**Recommendation**: Either state that a `null` or throwing `IServiceScopeFactory` resolution is a failed probe (which makes the foreign-container claim true), or drop the "cannot resolve from" clause and the Autofac framing and record that a foreign ambient exposing an `IServiceProvider` is borrowed from on its own terms.

---

### 4. `IAmAServiceProviderScope.Services` states an obligation the ADR does not enforce, unlike every other provider obligation (Score: 61)

The ADR argues at length that an obligation stated on a public extension point must also be guarded on Brighter's side — the whole case for the `AlwaysNew` guard at row 5. It then states two obligations on `Services` with no guard and no ladder row. Since the protocol's next act is `Probe(src.Services)`, a violating provider produces a `NullReferenceException` from inside `CreatePipelineScope()`, wrapped by the builders' general `catch` into `ConfigurationException` — exactly the degradation FR-24.1 forbids for the sibling case, by a different route.

**Evidence**: line 219 against line 193. No ladder row and no pseudo-code line covers a null or throwing `Services`.

**Recommendation**: Fold it into the probe — a `null` `Services`, or any exception reading it, is a failed probe taking row 9's decline-and-create path with the *offered but unusable* diagnostic.

---

### 5. Which type resolves and holds `ScopedArtefactCache` is stated three ways (Score: 55)

The cache section puts the resolution on `ServiceProviderLifetimeScope`'s `Scoped` path; the touched table agrees; the `Where the pieces live` diagram draws the edge from `ServiceProviderPipelineScope`; step 4 says the borrowed `ServiceProviderPipelineScope` "holds… the `ScopedArtefactCache` it resolved from it". These are different objects.

**Evidence**: lines 265, 313 against diagram line 133 and line 406.

**Recommendation**: Pick one owner; make diagram, table and step 4 agree. If `ServiceProviderLifetimeScope` resolves in both modes, redraw the edge from a `ServiceProviderLifetimeScope` node — which the diagram currently omits altogether despite the ADR changing it.

---

### 6. `Probe` has no home (Score: 50)

`Probe` is ladder row 9, the FR-23 mitigation in *Risks*, and AC-29 is written over it — but it appears only inside the pseudo-code and one sentence after it. Not in the roles table, no contract table, not in the touched table, so nothing says whether it is a private helper on each of the five factories, a static in the DI package, or a member of `ServiceProviderPipelineScope`.

**Evidence**: `grep -n 'Probe'` returns only lines 377, 383 plus two "usability probe" mentions. Absent from lines 152-160 and 305-318.

**Recommendation**: One touched-table row naming its assembly and type, or one sentence saying it is a private helper the five factories share.

---

### 7. The Scope paragraph over-claims FR-27 and is hard to parse (Score: 48)

Scope says it "discharges… FR-24 and FR-27", but FR-27.3 is the suppression rule the next paragraph defers to 0075 — and 0075's Scope says it only *serves* FR-27.3, so nothing discharges it. The em-dash aside inside the FR list also splits it.

**Evidence**: line 31 against line 33 and `0075:32`. FR-27.3 is requirements.md:252.

**Recommendation**: Narrow to "FR-24 and FR-27.1/FR-27.2", and lift the FR-13 aside into its own sentence.

**Verification (0072)**: 1 mermaid block, renders (28,462 B SVG), read as PNG; escaped entities 0; ~40 citations spot-checked across 12 files, **all held**, including the asserted `Singleton`-vs-`Transient` fallback split and the two `catch`-filter spelling differences; container-backed factories recounted = exactly 5; confirmed the proposed new types do not yet exist (correct for a design ADR); all referenced-ADR statuses verified against frontmatter; ~50 requirement ids cross-read and every one said what the ADR claims; headings exact; tone clean.

**Counts (0072)**: 90-100: 0 · 70-89: 2 · 50-69: 4 · 0-49: 1 · total: 7 · at-or-above-60: 4

---

## ADR 0073 — ASP.NET Core request scope package

### 1. `FrameworkReference` flows transitively, so "taking the package reference changes nothing" is false at build and deploy time (Score: 70)

The frontmatter summary, the *What the application has to write* bullet and the first *Positive* consequence all state that a bare package reference is inert. True of Brighter's behaviour and of the `IHttpContextAccessor` spy, and FR-15/AC-14 ask no more. But *Technology Choices* chooses `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`, and framework references flow transitively to every consuming project. A worker service or console producer that adds the `PackageReference` and never calls the extension acquires a hard build-and-runtime dependency on the ASP.NET Core shared framework (its `runtimeconfig.json` gains the entry, and the runtime must be installed on the target). *Negative* costs the choice only in terms of who cannot take the package (`netstandard2.0`), never what taking it costs a non-web host — the population the ADR's own line 66 is about.

**Evidence**: lines 8, 64, 274 against line 245. `grep -n "PrivateAssets\|ExcludeAssets"` → no hits; no *Negative* bullet addresses it. The external reference the ADR itself cites ("Use ASP.NET Core APIs in a class library") is the document that states this consequence.

**Recommendation**: Qualify the inertness claim to *behavioural* inertness and add a *Negative* bullet. If that is unacceptable, *Technology Choices* must weigh `Microsoft.AspNetCore.Http.Abstractions` as a `PackageReference` on `net8.0`+ against it, rather than dismissing package references on the `netstandard2.0` argument alone.

---

### 2. `HttpRequestScope.Services` is declared "Never null" but the described implementation cannot guarantee it (Score: 68)

0072 imposes a non-null obligation on the role (`0072:219`) and its probe dereferences it directly. 0073 implements that role and asserts the invariant with no guard: the provider's stated body null-checks only `_accessor.HttpContext`. `HttpContext.RequestServices` is not universally non-null — a `DefaultHttpContext` constructed directly, with no `IServiceProvidersFeature`, returns `null`. Not exotic here: *Technology Choices* makes substituting the accessor a design goal. A `null` there produces a `NullReferenceException` out of the DI package's probe, unwrapped to the `Send` caller — the outcome FR-23 exists to forbid.

**Evidence**: lines 194, 257. FR-23 covers only the *disposed-but-non-null* case, so the null case is covered by no requirement and no sibling ADR.

**Recommendation**: Make the guard explicit — return `null` when `HttpContext` **or** `HttpContext.RequestServices` is null — and change the contract row from an asserted invariant to a stated obligation.

---

### 3. The package's build shape is decided; the shape needed to exercise it is not, and no ASP.NET-capable test project exists (Score: 63)

Ten ACs this ADR cites or discharges (AC-14/15/16/17/18/19/29/34/48/49) require an ASP.NET test host with a controller action. No such project exists. The six numbered steps name only the src package; the touched table names only three assemblies. The flat claim "There is **no `Directory.Packages.props` entry**" reads as covering the change, when a web test host needs `Microsoft.AspNetCore.Mvc.Testing` and therefore does need one. `Brighter.slnx` goes unmentioned.

**Evidence**: `grep -rln "Microsoft.AspNetCore\|Mvc.Testing\|WebApplicationFactory" tests --include='*.csproj'` → **zero** across all 38 test projects; `grep -n "AspNetCore" Brighter.slnx` → no hits.

**Recommendation**: Add one step naming the new test project (SDK/TFMs, its CPM entry, and that the src package still needs none) plus the `Brighter.slnx` entry. If test siting is deliberately deferred to `/spec:tasks`, say so in one sentence rather than leaving the CPM claim unqualified.

---

### 4. The ADR claims to discharge FR-15, whose normative content it explicitly disclaims (Score: 60)

FR-15's normative sentence is about the option's **default value**, added by 0076 (which cites FR-15 three times for exactly that); the "no pipeline adopts" half is 0072's ladder. Two paragraphs later this ADR disclaims both. What it actually delivers is FR-15's *example* clause.

**Evidence**: line 34 vs line 36; requirements.md:264; `0076:241, :318, :340`.

**Recommendation**: Halve it as FR-17 is halved — "the package-inertness half of FR-15 (its stated example)… FR-15's default-value clause is ADR 0076's."

---

### 5. Bare `:119`, `:142` chain off the wrong file (Score: 60)

**Evidence**: line 241 establishes the bare-colon shorthand against `BrighterOptions.cs` and carries it on to `BrighterHandlerBuilder`, which lives in `ServiceCollectionExtensions.cs`. The numbers are right; the implied file is wrong, and `BrighterOptions.cs:119`/`:142` are unrelated lines inside the interface.

**Recommendation**: Write `ServiceCollectionExtensions.cs:119`, `:142` explicitly.

---

### 6. "Exactly as if the application had never opted in" contradicts FR-18, FR-19 and the ADR's own Risks table (Score: 58)

FR-18: a `JoinAmbient` ask answered with nothing *is* FR-24.2's condition, so one latched `Warning` per container per provider type is emitted, and FR-19 calls that "the only observable difference from the not-opted-in case". The ADR's own *Risks* row gets this right, so the document contradicts itself.

**Evidence**: lines 65, 114, 276 against FR-18, FR-19 and line 293.

**Recommendation**: Add the qualifier at each of the three sites — "…save one latched `Warning` (FR-18, FR-24.2)" — and keep the *Risks* row as the detail.

---

### 7. The contract table attributes the provider's `AlwaysNew` obligation to FR-24.4 instead of FR-10 (Score: 52)

FR-24.4 is *Brighter's* half. The *provider's* obligation not to consult or adopt is FR-10's, carried by D16. Step 3 splits them correctly; the contract table does not.

**Evidence**: line 193 against line 261 and AC-18's own note.

**Recommendation**: `(D16, FR-10)`.

---

### 8. NFR-8 is cited for two claims it does not make (Score: 50)

NFR-8 is a documentation requirement about one specific ambiguity (`IAmAScope` vs `IAmALifetime`). It establishes no normative vocabulary and forbids no phrase.

**Evidence**: lines 56 and 296 against requirements.md:359.

**Recommendation**: Keep both statements — good hygiene — but attribute them to 0067's `Terms` block (already cited at line 56, verified present at `0067:40`), and cite NFR-8 only where the documentation obligation is discharged.

---

### 9. The illustrative XML doc comment omits `<param>`, `<returns>` and `<exception>` (Score: 38)

**Evidence**: lines 167-175 against `.agent_instructions/documentation.md:12-18`, on a method with an optional parameter whose default is load-bearing (D13) and a documented `ArgumentNullException`. The ADR leans on this comment as the mitigation for the "request" naming ambiguity, so an implementor will copy it verbatim.

**Recommendation**: Add the three tags, or trim to a signature and note that full XML docs follow house style.

---

### 10. "The Microsoft extension surface being integrated" does not describe `OpenTelemetry` (Score: 35)

**Evidence**: line 204. `Paramore.Brighter.Extensions.Diagnostics` in fact references `OpenTelemetry.Api.ProviderBuilderExtensions`, not a Microsoft package. The pattern holds; the stated rule does not.

**Recommendation**: "names the framework or extension surface being integrated".

**Verification (0073)**: diagrams 2/2 render, both read as PNG; escaped entities 0; TFM/SDK claims verified against `src/Directory.Build.props:43`/`:45` and the "24 projects" count recounted as **exactly 24**; the third-party assembly split verified against the on-disk `Microsoft.AspNetCore.App.Ref/8.0.0` pack — `AddHttpContextAccessor` in `Http.dll` only, `IHttpContextAccessor` in `Http.Abstractions.dll`: **the ADR's split is correct**; all eight `Use*` names verified to extend `IBrighterBuilder`; namespace-convention claims verified; ~30 requirement ids cross-read, AC-48's quotation verbatim-accurate; headings exact; tone clean.

**Counts (0073)**: 90-100: 0 · 70-89: 1 · 50-69: 7 · 0-49: 2 · total: 10 · at-or-above-60: 5

---

## ADR 0074 — lifetime validation evaluation site

### 1. A Positive consequence overclaims and contradicts the ADR's own rule-set property (Score: 72)

*Positive* asserts an application can never receive two errors. *The rules, fixed* says the opposite: FR-22.4 is an **Error** and can fire alongside FR-22.1, also an **Error**, with different remedies.

**Evidence**: line 406 against line 95, which states the counter-example in terms; both are `Error` per the severity table (lines 82, 85); requirements FR-22 confirms line 95 is the correct reading.

**Recommendation**: Narrow the consequent — "so an application never receives *those two* errors together" — or restate as "the two lifetime errors are mutually exclusive, but FR-22.4 may accompany either."

---

### 2. Stale rule counts inside the six-rule set (Score: 68)

**Evidence**: line 272 — "the exclusion is exactly why **a fifth rule** is needed rather than **a wider fourth one**" — a residue of a five-rule set predating FR-22.4. In the ADR's own evaluation order FR-17 is sixth and FR-24.3 fifth. Every other count verified to hold: frontmatter, Context ¶1, Scope, the sibling-map row (identical in all six siblings), the 6-row rules table, Decision, `#### The six rules`, the roles table ("deciding ×6"), step 4, and "Nine new types… plus one in core" (8 + validator = 9).

**Recommendation**: "…why a *sixth* rule is needed rather than a wider *fifth* one."

---

### 3. "Five combinations" / "the same five rows" against a six-row host table (Score: 66)

**Evidence**: lines 108 and 348 count five; the table at 337-344 has six data rows. The intervening prose at 346 correctly says "Rows 3 and 6 are D14's accepted gap" — so the table and one paragraph say six while two other passages say five. Line 348's walk covers rows 4, 5, 1, 2, 3 and silently omits row 6.

**Recommendation**: Change both counts to six and add row 6 to the FR-22.4 walk.

---

### 4. The registration snippet drops an existing null guard and forces the lazy registry, contradicting "exactly as today" (Score: 65)

The snippet wraps `registryFactory` in a `Lazy` unconditionally and passes `() => registry.Value` into `PipelineValidator`. Today that factory is deliberately **nullable**, and its null-ness is the gate on the wrap-transform rule.

**Evidence**: `BrighterPipelineValidationExtensions.cs:85-88` builds `Func<MessageMapperRegistry>? mapperRegistryFactory = mapperRegistryBuilder != null ? … : null;`, and `PipelineValidator.cs:69-71` turns a null factory into a null `_mapperRegistry`, which `:139` uses as the gate. Under the snippet `_mapperRegistry` is never null, so the rule runs in hosts where it does not today — while `new Lazy<T>(null)` throws `ArgumentNullException` when the builder is absent. The identifier `registryFactory` also does not exist (`mapperRegistryFactory` does). Separately `ArtefactExclusionSet.Build(pipelineBuilder, registry.Value, …)` forces the `Lazy` at construction, defeating the laziness whose source comment (`PipelineValidator.cs:64-68`) explains — that cost *is* disclosed in a *Negative* bullet, but the null branch and gate flip are not, while the ADR claims "no behaviour change" (lines 234, 357).

**Recommendation**: Show the guarded form and say what the exclusion set does when there is no registry.

---

### 5. Bare "ADR 0064" is used repeatedly, against C-16 and against the ADR's own stated convention (Score: 64)

**Evidence**: line 56 declares bare numbers are avoided deliberately because C-16 assigns bare "ADR 0064" to `0064-pipeline-cache-type-key`; bare uses follow at 372, 385, 419, 443, 445, 455, all meaning the *other* file. C-16 verified; `ls docs/adr` confirms three 0053s, two 0054s, two 0064s.

**Recommendation**: Use the slug at every occurrence (the ADR already does at 240 and 258), or drop the "avoided deliberately" sentence — but not both.

---

### 6. The FR-25 clause map is a second decision, sited in Context, in an ADR whose Scope says it decides one thing (Score: 63)

**Evidence**: line 32 declares the scope as "one thing — the evaluation site"; lines 36-54 add an eleven-row map assigning the source of every clause of a *documentation* requirement, including "NFR-9 is discharged by writing it, and this is the only place NFR-9 lands" — a decision about content 0072 and 0075 supply. `## Context` runs 34 lines here versus 12-18 in every sibling; documentation.md specifies Context as "2-4 sentences in plain language". The map's content was verified *accurate* (FR-25 has exactly 11 clauses; every cited sibling step exists and says what is attributed), so this is placement, not correctness.

**Recommendation**: Move it to `tasks.md` or to a short `### The documentation this set owes` after `### Where this ADR sits`.

---

### 7. FR-22.4's after-ordering branch depends on an ordering precondition the ADR states for FR-24.3 but not for it (Score: 62)

FR-22.4 reads the descriptor snapshot taken at `ValidatePipelines()` call time. In the natural fluent form, an application registration made "after `AddBrighter`" is also after the snapshot, so the rule sees nothing — the exact silent loss it exists to break.

**Evidence**: line 277 states no precondition; line 250 states the parallel one for FR-24.3 ("AC-32 requires `ValidatePipelines()` to be called after both provider registrations"). The ADR's own failure-mode table row 1 confirms the exposure. AC-50's Given does say "`ValidatePipelines()` called last", so the AC is satisfiable — but that is the AC carrying a constraint the ADR does not surface for implementors.

**Recommendation**: Add the same sentence FR-24.3 gets.

---

### 8. The `[UsePolicyAsync]` exclusion claim rests on an evaluation order no Acceptance Criterion can observe (Score: 60)

**Evidence**: line 299 answers the objection with "the exclusion is applied first so that AC-42's `[UsePolicyAsync]` clause pins the mechanism it is written to pin". Both paths yield "no warning"; AC-42's clause is an assertion about output only. The registration claim itself holds (`ServiceCollectionBrighterBuilder.cs:254-260` → `EnsureHandlerIsRegistered` at `:76`), so grounding is right and only the inference is unsound.

**Recommendation**: Drop the claim that AC-42's clause pins the handler half, or specify a non-open-generic Brighter attribute-returned handler as the distinguishing case.

---

### 9. FR-22.3's inputs are partitioned inconsistently in three places (Score: 56)

**Evidence**: lines 28, 102 and 417 put FR-22.3 on the lifetimes side of a "three read lifetimes, three read descriptors" split; line 414 and the *Captive-dependency detection* section say it reads the snapshot for **both** inputs, and requirements FR-22 rule 3 agrees. One of the three is inside `## Consequences`.

**Recommendation**: State the partition as it is — FR-22.1/22.2 read configured lifetimes only; FR-22.3 reads both axes; FR-22.4/FR-24.3/FR-17 read descriptors only. Line 76 already gets this right.

---

### 10. `ScopeProviderRegistration` names one thing and carries three (Score: 52)

**Evidence**: roles table line 195 — one type for ambient-source, affinity-override and `IBrighterOptions` descriptors. The reuse is well argued; the name is not revisited in *Technology Choices*, which does discuss naming. FR-22.4's row hangs an extra field on it the name gives no hint of.

**Recommendation**: Rename (e.g. `DescriptorRecord`) and say in *Technology Choices* why one record serves three service types.

---

### 11. The `### {the problem}` slot is filled by restated inputs rather than the problem (Score: 48)

**Evidence**: the heading is `### The rules, fixed`, and Scope says of its content "It does **not** decide the rules or their severities… restated below as inputs". Every sibling names this section as a behaviour or question. Heading order and nesting match exactly across all seven and the section does lead with its orienting table, so this is wording drift only. The architectural problem is stated well, but only in Context ¶2.

**Recommendation**: Rename to a behaviour, e.g. `### What the six rules need, and where those inputs live`.

---

### 12. Two orderings of the ADR set are stated without reconciliation (Score: 42)

**Evidence**: line 60 "This is the fifth" (of seven); line 74 "written last of the six that shape a configuration". Which six is never named; "last" is a dependency ordering, "fifth" a numbering.

**Recommendation**: "…written last **in dependency order** of the six ADRs that shape a configuration (0070-0073 and 0076)".

**Verification (0074)**: diagrams 2/2 render, flowchart read as PNG (one subgraph per assembly, the two hosts correctly in different boxes); escaped entities 0; `grep -i chain` 0 hits; ~60 citations spot-checked across 25+ files, all held; counts recounted independently — NFR-1 source scan returns **0**, tests registering `IBrighterOptions` = **125** exact, container-backed factories = 5, the duplicate-numbered ADRs confirmed; every sibling cross-reference verified to exist and say what is attributed; the 0074 row is byte-identical in all six siblings; ~45 requirement ids cross-read including all 11 FR-25 clauses (map orphan-free both directions) and AC-42's seven clauses.

**Counts (0074)**: 90-100: 0 · 70-89: 1 · 50-69: 9 · 0-49: 2 · total: 12 · at-or-above-60: 8

---

## ADR 0075 — publish-subscriber scope suppression

### 1. "No diagnostic is emitted" for a suppressed pipeline contradicts AC-11, FR-24.4 and 0072's own ladder (Score: 82)

Step 6 states flatly that suppression produces no diagnostic. False for the exact case AC-11 pins: a suppressed subscriber asks with `AlwaysNew`, and if a non-conforming provider returns an ambient anyway, FR-24.4 requires a once-per-container `Warning`. 0072's ladder — which this ADR defers to and calls "unchanged" — makes the same point in its Diagnostic column. The ADR cites AC-11 twice elsewhere without noticing that AC-11's warning clauses are *about suppressed subscriber pipelines*.

**Evidence**: line 259 against `0072:98` (row 5) and `0072:106` ("a suppressed pipeline takes rows 5 or 6"), and against requirements.md:474-483 (AC-11), whose `When` is `PublishAsync` to two subscribers and whose `Then` is exactly one `Warning` naming the *ambient offered for an `AlwaysNew` ask and ignored* condition. The only `AlwaysNew` asks in that scenario are the two suppressed subscribers'.

**Recommendation**: Suppression adds no *new* diagnostic and no new ladder row, but a suppressed pipeline reaches rows 5 and 6 and therefore emits FR-24.4's warning on row 5 exactly as any other `AlwaysNew` pipeline does (AC-11). Add FR-24.4 and AC-11's warning clause to References.

---

### 2. "Takes the path it would have taken with no provider registered at all" contradicts D16 — the ask is still made, and is observable (Score: 70)

With no provider, no `GetAmbient` call is made at all (FR-11(a), 0072 ladder row 3); a suppressed pipeline still makes the ask, carrying `AlwaysNew`, because D16 makes the ask unconditional so the decision is observable — and AC-13 asserts a recorder sees exactly five decisions, three of them the `Publish` subscribers'. An implementor reading step 6 literally and skipping the ask fails AC-13 and AC-46.

**Evidence**: lines 139 and 259 against requirements.md:249 (FR-27.1/D16) and AC-13 (`:501`). References (line 306) contains no D16, no AC-13, no FR-27.2.

**Recommendation**: Say what is true — suppression changes only the affinity a pipeline asks with; the ask is still made and still recorded. The *outcome* matches the no-provider case; the *path* does not. Add D16 and AC-13 to References.

---

### 3. The claim that an unrestored write inside the `Parallel.ForEach` body leaks onto the caller's flow is empirically false (Score: 68)

Step 5's third bullet credits bracket 2's explicit restore with preventing a caller-flow leak that `Parallel.ForEach` cannot produce. The runtime captures and restores `ExecutionContext` per replica task — including the replica inlined on the *calling* thread. The genuine caller-flow leak comes from **bracket 1**: `PipelineBuilder`'s loop is `observerTypes.Each(observer => …)`, a plain synchronous `foreach` (`Extensions/Each.cs:39-45`), where an unrestored write does persist.

**Evidence**: line 255. A probe compiled and run on .NET 10 (the repo's TFM), `MaxDegreeOfParallelism = 1`, 200 items, one thread: `bodies seeing leaked true = 199/200` (so the cross-body leak step 5 relies on is **real** and correctly described) but `caller flag after = False`. Shape-matched pair: bracket-1 shape (plain `Each`, no restore) → caller flag **True**; bracket-2 sync shape (`Parallel.ForEach` body, no restore, DOP=1 inline) → caller flag **False**. A control setting the flag *around* the loop in the caller's flow gave True, matching FR-9(ii)'s primary case.

**Recommendation**: Attribute the caller-flow leak to the two shapes that cause it — a bracket placed *around* the `Parallel.ForEach` in the caller's flow, and bracket 1's plain `Each` loop. Keep the explicit restore on bracket 2's sync half, justified as defence in depth and symmetry with the async twin. AC-39's own wording already draws the line correctly.

---

### 4. "The two call sites inside `CommandProcessor` are the only ones in this repository" is wrong on both counts (Score: 62)

**Evidence**: line 278. `grep -rn --include="*.cs" "new PipelineBuilder"` returns 75 hits: 6 in `src/` (`CommandProcessor.cs:317, :394, :472, :575` — all four dispatch constructors — plus `BrighterPipelineValidationExtensions.cs:75, :116` on the describe-only constructor) and 69 in `tests/`. The change stays source-compatible so nothing needs editing, but the sentence is offered as reassurance about scope.

**Recommendation**: "the two call sites that pass `isolateSubscribers: true` are in `CommandProcessor`; the two dispatch constructors are called at four sites in `CommandProcessor` and 69 in `tests/`, all of which recompile unchanged."

---

### 5. The public binary break this ADR introduces is documented but pinned by no acceptance criterion (Score: 52)

**Evidence**: lines 232 and 278 cite "(ADR 0070 step 7a, AC-24)". Step 7a does carry it (`0070:370`, verified). AC-24 (requirements.md:671-676) enumerates only the `MapperLifetime` break, C-18's mixing break, FR-22.2's joint consequence, and "each of the six factory interfaces whose signature changed" — nothing detects the omission of a `PipelineBuilder` constructor note.

**Recommendation**: Cite step 7a alone, and either drop the AC-24 citation or note explicitly that no AC covers this break.

---

### 6. The out-of-order-disposal description stops one step short (Score: 46)

**Evidence**: line 207 says out-of-order disposal "can clear suppression early". Walk it: outer captures `false`/sets `true`; inner captures `true`/sets `true`; dispose outer → `false`; dispose inner → restores `true`. Every bracket is now disposed and the flow reads `IsSuppressed == true` for the rest of its life. The asymmetry argument survives (that residue is the benign direction), but the reader is told only about the transient clear.

**Recommendation**: Add the second half, and note the residue falls in the benign direction the paragraph below already identifies.

---

### 7. The two validation-time construction sites cannot "keep the default" — they use a constructor that has no such parameter (Score: 40)

**Evidence**: line 240 groups them with `Send`/`SendAsync` one sentence after saying the describe-only constructor (`:92`) does not take the argument. Both validation sites are `new PipelineBuilder<IRequest>(subscriberRegistry, ResolveInboxConfiguration(sp))` — the `:92` overload.

**Recommendation**: Split the sentence.

---

### 8. `Task.WhenAll` is drawn outside the `alt`, so it reads as applying to the synchronous branch too (Score: 36)

**Evidence**: line 122, placed after the `end` of the `alt`. Confirmed by rendering to PNG at 1600px: the message appears once, below both branch compartments, with no branch attribution.

**Recommendation**: Move it inside the `else asynchronous PublishAsync` branch, where `CommandProcessor.cs:601` lives.

**Verification (0075)**: diagrams 2/2 render, sequence diagram read as PNG; escaped entities 0; every cited line range read against source and held — including `:187-198` and `:232-244` as genuinely symmetric twins, and the three public constructors in the order claimed; runtime behaviour verified empirically on .NET 10 (see finding 3); five container-backed factories recounted; ~35 requirement ids cross-read; headings exact; tone clean.

**Counts (0075)**: 90-100: 0 · 70-89: 2 · 50-69: 3 · 0-49: 3 · total: 8 · at-or-above-60: 4

---

## ADR 0076 — scope affinity option and write-through

### 1. `Key Components` is missing the corpus-standard `#### Where each type is touched` closing table (Score: 65)

**Evidence**: heading scan of all seven — `0070:245`, `0071:247`, `0072:303`, `0073:233`, `0074:352`, `0075:213` all carry it; 0076 has no such heading. Its substance is present but scattered across prose, and the "| Site | Today | After |" table at 289-294 is a *per-call-site* artefact, not the per-assembly/per-type one the skeleton names, and it does not close with the deliberately-untouched list.

**Recommendation**: Add it in the sibling form, closing by naming `:39`, `:89-90`, core and the five factories as deliberately unchanged.

---

### 2. `Scope` claims to discharge FR-17 whole, where the two siblings each claim a named half — and this ADR's own step 5 hands part of FR-17 away (Score: 65)

**Evidence**: `0076:32` claims FR-17; `0073:34` claims "the registration half" and assigns the rest to 0074; `0074:32` claims "the evaluation-site half". `0076:332` then says "**What this leaves to ADR 0074**… FR-17's repeated-opt-in rule". FR-17 (requirements.md:274-275) has three separable obligations. **Converges with set-level finding 3.**

**Recommendation**: "It discharges FR-14 and **the write-through half of FR-17**… FR-17's registration gesture is ADR 0073's and its repeated-call rule's evaluation site is ADR 0074's." Add FR-24.3 and C-16 to the References requirement-id list, since both are cited in the body and neither appears there.

---

### 3. Alternative 3's "second, independent objection" is exactly the residue the chosen design excuses (Score: 62)

**Evidence**: line 373 rejects descriptor rewriting because "on the consumer `Action` path the descriptor's `ImplementationInstance` is also registered as `IAmConsumerOptions`, so rewriting one service type quietly changes the object behind another". That describes what the **chosen** design does: `RegisterBrighterOptions(services, _ => options)` returns the same `ConsumersOptions` instance `:39` registered as `IAmConsumerOptions` and mutates it — and the ADR argues at lines 302 and 350 that the mutation is benign. Verified `ServiceActivator…/ServiceCollectionExtensions.cs:36-39` registers one instance twice, and `IAmConsumerOptions.cs:7-37` has exactly the five members named and no affinity. Both designs mutate the same object; only the chosen one is excused.

**Recommendation**: Delete the second objection or replace it with one that actually distinguishes the two. Alternative 3's primary rejection (AC-48's before-ordering, verified at requirements.md:745) is sound and decisive on its own.

---

### 4. "does not exist yet on two of them" undercounts — three of the four paths have no options object at registration time (Score: 52)

**Evidence**: lines 8 and 93 say two. Only `AddConsumers(Action<ConsumersOptions>)` has a real object at registration time (`new ConsumersOptions()` at `:36`); on `AddBrighter(Action<BrighterOptions>)` the object is produced by the `IOptions` pipeline at first resolution (`ServiceCollectionExtensions.cs:69-75` registers delegates only). *Technology Choices* (line 312) and Alternative 7 (line 381) both get the enumeration right, so this is a compression error in the two most-read sentences.

**Recommendation**: "which exists at registration time on only **one** of the four".

---

### 5. Context opens with the mechanical recap, naming four types before the problem (Score: 50)

**Evidence**: `0076:26` names `IAmAScopeProvider`, `ScopeAffinity`, `ScopeAffinityPolicy` and `IBrighterOptions` in sentence 2, and states the problem in two of those names. Siblings put the plain-language problem first (`0072:25-27`, `0073:26-28`, `0075:26-28`). The real problem lands only in paragraph 2, where it is stated well.

**Recommendation**: Swap the emphasis, as 0073 does.

---

### 6. `RegisterBrighterOptions`'s contract table says "Does not throw" for a public cross-assembly method with no argument guards (Score: 45)

**Evidence**: line 279 against the body at 252-272, which opens `services.Any(…)`. All four in-repo callers null-check first (verified), but the method is `public` precisely so another assembly can call it. The table also does not say what happens when `optionsFunc` **returns null** — today MS DI raises its own error; after the change `options.DefaultScopeAffinity = over.Affinity` dereferences first whenever an override is registered.

**Recommendation**: State the guards, or narrow the claim, and add a clause for a null return.

---

### 7. The funnel flowchart labels `RegisterBrighterOptions` as "TryAddSingleton", which the decision deliberately does not use (Score: 38)

**Evidence**: diagram 2's central node against the code comment at line 256 — "TryAddSingleton spelled out, because the descriptor we add has to be one we can hand on". Since `BrighterOptionsRegistration` exists solely because the descriptor is built by hand, the label contradicts the most load-bearing detail of the mechanism.

**Recommendation**: Relabel to "first registration wins for IBrighterOptions, with a delegate that…".

---

### 8. "the same two lines in the other four" is not true of `ServiceProviderHandlerFactory` (Score: 35)

**Evidence**: the four transform factories read `options?.{Mapper,Transformer}Lifetime ?? ServiceLifetime.Singleton`; `ServiceProviderHandlerFactory.cs:49-51` reads three lines and a different pair of properties (`HandlerLifetime ?? Transient` **and** `IsolateTransientHandlerScope ?? true`).

**Recommendation**: "…and the same `GetService` in the other four".

**Verification (0076)**: diagrams 3/3 render, two read as PNG; escaped entities 0; **the central claim independently reverified** — an exhaustive scan for `{Add,TryAdd}{Singleton,Scoped,Transient}<IBrighterOptions>` and `typeof(IBrighterOptions)` over `src/` finds **exactly four** sites at exactly the four cited lines, all `TryAddSingleton`, all routing through `BrighterHandlerBuilder`, with exactly one pre-built instance; no fifth site in `src/`, `tests/` production code or `samples/`; **order-independence walked at all four sites in both orderings and holds**; the stated limit is honest and agrees with `0074:278, :348, :362, :417, :432`; one implementation of `IBrighterOptions` repo-wide; 125 test files registering it (exact match); ~40 requirement ids cross-read; tone clean.

**Counts (0076)**: 90-100: 0 · 70-89: 0 · 50-69: 5 · 0-49: 3 · total: 8 · at-or-above-60: 3

---

## Set-level findings

### 1. FR-13's disposal-failure clause and AC-33 are contradicted on the handler path — 0070 claims them, 0071 specifies the opposite (Score: 92)

FR-13 requires that when releasing an **owned** pipeline scope throws on a pipeline whose work completed normally, the failure is logged at `Error` and swallowed and the caller's result returned unchanged. 0070 claims to discharge it and cites AC-33. But AC-33 is written over a **`Send`**, and 0070 sites the mechanism exclusively on transform-pipeline release sites, while 0071 specifies the handler-pipeline scope disposal to *throw*, composed. No ADR reconciles them, and 0071 never mentions FR-13 or AC-33.

**Evidence**: `0070:32`; `0070:260` (the three sites, all transform-release per step 8 — a `Send` touches none); `0070:199` (the reasoning is written over `Post`, not `Send`); `0071:278-283` ("throw them composed as an `AggregateException`"); AC-33's full text. Verified the failure escapes `Send`: `CommandProcessor.cs:317` is `using var builder = new PipelineBuilder<T>(…)`, which drives `HandlerLifetimeScope.Dispose()` at end of `Send`. `grep -n "AC-33" docs/adr/007[0-6]*.md` hits only 0070 and 0072's reference list.

As specified, AC-33 fails on **both** of its assertions: the caller observes an `AggregateException`, and nothing logs at `Error`.

**Recommendation**: AC-33 and FR-13 are right; 0071 is the side that must move. Either (a) 0071 states that a pipeline-scope disposal failure inside `HandlerLifetimeScope.Dispose()` is logged at `Error` and swallowed, keeping composition for *handler `Release`* failures only — which is the break 0071 actually argues for — or (b) 0070 stops claiming FR-13's disposal clause for the handler family and 0071 claims it explicitly with its own `Error` message. Either way step 7a's bullet for 0071's `AggregateException` must say it does **not** cover scope-disposal failures.

---

### 2. FR-21 is discharged by no ADR, and 0076 forwards it to a sibling that never names it (Score: 76)

**Evidence**: `0076:34` — "FR-21… is **delivered by ADR 0072's `ScopeAffinityPolicy` and the five container-backed factories**". Per-file mention counts: `0070=0 0071=0 0072=0 0073=2 0074=2 0075=0 0076=7`. `grep -qE "\bAC-26\b"` across the seven → no match. The substance exists (0072's ladder rows 1-2 make a non-`Scoped` factory offer nothing and make no ask), but the requirement is never named at its delivery site, so the coverage ledger points at an empty box.

**Recommendation**: 0072 is the right owner. Add FR-21 and AC-26 to its Scope and References, naming ladder rows 1-2 and `ScopeAffinityPolicy`'s positive-`JoinAmbient` test.

---

### 3. FR-17 is partitioned three mutually incompatible ways across 0073, 0074 and 0076 (Score: 74)

**Evidence**: `0076:32` claims it whole; `0073:34` claims "the registration half" and assigns the rest to 0074; `0074:32` claims "the evaluation-site half". None names 0076 as an owner of any part. FR-17 in fact has three separable obligations. Compare FR-13, where the set handles a split correctly and says so.

**Recommendation**: Rewrite all three `Scope` sentences to the same three-way split, on the FR-13 model, each naming the other two.

---

### 4. The single release-note ledger (0070 step 7a) omits binary breaks the set causes, while listing an identical one (Score: 72)

Step 7a is declared as the one place every upgrade break is enumerated. It lists 0075's `PipelineBuilder<TRequest>` constructor break, and omits the structurally identical break 0070 itself causes on **six** pipeline constructors — which 0070's own *Negative* section states as a break — and 0074's change to what the container returns for `IAmAPipelineValidator`.

**Evidence**: listed at `0070:370`; not listed but stated at `0070:411` ("Six pipeline constructors and one internal drain helper change shape… binary-breaking for anyone who constructed one without recompiling"); verified public surface, not internal — `WrapPipeline.cs:39`/`:53`, `UnwrapPipeline.cs:36`/`:45`, `WrapPipelineAsync.cs:43`/`:57`, `UnwrapPipelineAsync.cs:38`/`:47`, `TransformPipeline.cs:8`/`:21`, `TransformPipelineAsync.cs:9`/`:22`. Not listed — `0074:418` ("`IAmAPipelineValidator` no longer resolves to `PipelineValidator`… a behavioural change in what the container returns").

**Recommendation**: Add two bullets to step 7a. Listing all three is the correct fix; the alternative (dropping 0075's) is worse.

---

### 5. Eleven of the fifty acceptance criteria are cited by no ADR in the set (Score: 70)

**Evidence**: `for i in $(seq 1 50); do grep -qE "\bAC-$i\b" docs/adr/007[0-6]*.md || echo -n "AC-$i "; done` → `AC-2 AC-3 AC-4 AC-15 AC-18 AC-21 AC-23 AC-25 AC-26 AC-34 AC-36`.

The uncited include the headline adoption AC (**AC-15**, "opted in, a `Send` from a controller shares the request scope" — 0073 even says "the opted-in ASP.NET host **of AC-15**"), the ordering AC (**AC-18**), FR-16(b)'s only AC (**AC-34**), FR-21's only AC (**AC-26**) and both documentation ACs. AC-18 additionally describes a **wrapping** recorder delegating to the ASP.NET provider, which is not obviously buildable against 0073's Decision that the package's "entire public surface is one `IServiceCollection` extension" — 0073 states no accessibility for `HttpContextScopeProvider` or `HttpRequestScope`.

None is unsatisfiable by the design as written (unlike AC-33), so this is a ledger completeness problem rather than a design gap.

**Recommendation**: Add each to its owning ADR's References: AC-2/3/4/21/23 → 0070; AC-15/AC-34 → 0072; AC-18 → 0073 (and state whether `HttpContextScopeProvider` is public); AC-25 → 0074; AC-26 → 0072; AC-36 → 0075.

---

### 6. "Two of the eight are not factories" — three of the eight are not factories (Score: 62)

**Evidence**: `0070:368`. Enumerating the eight from 0070 step 2 and 0071's touched table: five are factories; the two registries and `IAmALifetime` are not — **three**. 0076 uses the correct three-way description of the same eight (`0076:212` — "the eight **factory, registry and handler** interface signatures"). The count of eight itself is right and agrees across 0070, 0075 and 0076. **Converges with 0070 finding 4.**

**Recommendation**: "three of the eight are not factories — two mapper registries and `IAmALifetime`".

---

### 7. 0074 describes the four `IBrighterOptions` registration sites in a form ADR 0076 removes (Score: 52)

**Evidence**: `0074:252` — "each `TryAddSingleton`s it" with the four line citations. `0076:291-294` rewrites all four onto `RegisterBrighterOptions`, spelled out as an explicit `services.Any(…)` guard plus `services.Add(descriptor)` — deliberately **not** `TryAddSingleton`. 0074's own FR-22.4 section already knows this (`0074:278`). Same for `0074:280`. The conclusion survives; only the description is stale.

**Recommendation**: 0074 changes: "each registers it through ADR 0076's `RegisterBrighterOptions`, whose first-wins guard preserves C-12's semantics", keeping the four line citations as call sites.

---

### Coverage map

| Id | Discharged by | Covered |
| --- | --- | --- |
| FR-1 … FR-5 | 0070 | yes |
| FR-6 | 0070; preserved for handlers by 0071 | yes |
| FR-7 | 0070; 0071 (regression guards, AC-9) | yes |
| FR-8 | 0075 | yes |
| FR-9 | 0075 | yes |
| FR-10 | 0072 | yes |
| FR-11 | 0072 (ladder rows 3, 8) | yes |
| FR-12 | 0072 | yes |
| FR-13 | 0072 (ownership) + 0070 (disposal-failure) | **partial — handler path contradicted, finding 1** |
| FR-14 | 0076 | yes |
| FR-15 | 0073 | yes |
| FR-16 / 16a | 0072 ("serves"; `ScopedArtefactCache`, AC-17) | yes, in substance |
| FR-16b | 0072, one passing mention (`:402`); AC-34 uncited | weak |
| FR-17 | 0073 + 0074 + 0076, contested | **contested, finding 3** |
| FR-18 | 0072 (row 7) + 0073 — "serves" only | yes, in substance |
| FR-19 | 0072 + 0076 — "serves" only | yes, in substance |
| FR-20 | 0070 step 7a | yes |
| FR-21 | **none** — 0076 disclaims and forwards to 0072, which never names it | **no, finding 2** |
| FR-22 (.1–.4) | 0074 | yes |
| FR-23 | 0072 (probe, row 9) — "serves" only | yes, in substance |
| FR-24 (.1–.4) | 0072; .3's evaluation site 0074 | yes |
| FR-25 (11 clauses) | 0074 | yes |
| FR-26 | 0072 — "serves" only | yes, in substance |
| FR-27 (.1–.3) | 0072; .1 also 0070, .3 also 0075 | yes |
| NFR-1 | 0070 (a/b/c); reaffirmed 0072/0073/0074/0076 | yes |
| NFR-2 | 0073 | yes |
| NFR-3 | 0070 + 0071 | yes |
| NFR-4 | 0070, 0071, 0072, 0075, 0076 | yes |
| NFR-5 | 0070 | yes |
| NFR-6 | 0070; 0072 | yes |
| NFR-7 | 0072, 0073, 0075, 0076 | yes |
| NFR-8 | 0070 + 0071 | yes |
| NFR-9 | 0074 (FR-25 clause 3) | yes |
| NFR-10 | 0074 | yes |

**ACs the design as written could not pass**: **AC-33** (finding 1). **AC-18**'s stated construction is at risk depending on `HttpContextScopeProvider`'s accessibility, which no ADR states (finding 5). No constraint C-1…C-20 is contradicted; C-13 is the only one never named and it is the "how is design work" constraint, so that is correct. All of D0–D19 (incl. D0b, D0c) are named; none is reversed.

**Counts (set-level)**: 90-100: 1 · 70-89: 4 · 50-69: 2 · 0-49: 0 · total: 7 · at-or-above-60: 6

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 14 |
| 50-69 (Medium) | 37 |
| 0-49 (Low) | 15 |

**Total findings**: 67
**Findings at or above threshold (60)**: 39

Per reviewer — 0070: 6 (3 ≥60) · 0071: 9 (6) · 0072: 7 (4) · 0073: 10 (5) · 0074: 12 (8) · 0075: 8 (4) · 0076: 8 (3) · set-level: 7 (6).
