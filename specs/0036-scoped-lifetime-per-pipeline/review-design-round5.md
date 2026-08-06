# Review: design — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-06
**Threshold**: 60
**Round**: 5
**Verdict**: NEEDS WORK

45 findings at or above threshold 60. Address these before approving.

Eight reviewers, all on opus, all blind to `PROMPT.md` and to every earlier round's findings file, each with its own scratchpad subdirectory (two collided in round 4). One per ADR plus one whose only remit was set-level properties. Each verified citations against source rather than reading them, recounted every count it reported, rendered every mermaid block, and ran the escaped-entity grep. The 0075 reviewer additionally built and ran a thirteen-case .NET probe suite.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 2 |
| 70-89 (High) | 17 |
| 50-69 (Medium) | 53 |
| 0-49 (Low) | 26 |

**Total findings**: 98
**Findings at or above threshold (60)**: 45

Per reviewer — 0070: 10 findings (5 at or above 60) · 0071: 12 (6) · 0072: 16 (5) · 0073: 11 (6) · 0074: 16 (7) · 0075: 13 (4) · 0076: 9 (4) · set-level: 11 (8).

The at-or-above-60 trend across five rounds is 63 → 71 → 39 → 63 → **45**.

---

## What the blinding produced — independent convergences

Eight reviewers who could not see each other's work reached the same defect from different ends **six** times. This is the round's strongest signal and the fastest route into triage.

| Convergence | Reviewers | Scores |
| --- | --- | --- |
| **0073 denies FR-10 contains the `AlwaysNew` rule; FR-10 states it verbatim** | 0073 #1, set #1 | 85, 85 |
| **The release-note ledger is claimed a superset of AC-24; two of AC-24's enumerated six never break** | 0071 #8, 0072 (implicit), 0076 #4, set #3, 0070 #6 | 78, 60, 58, 52 |
| **"An earlier draft/revision of this ADR said…" — review-loop archaeology** | 0071 #6, 0072 #5, 0073 #5, 0075 #2, set #6 | 70, 65, 65, 62, 62 |
| **NFR-9's ownership: 0074 claims exclusivity, 0075's `Scope` claims discharge, neither is in 0074's `Scope`** | 0074 #4, 0075 #7, set #5 | 70, 70, 55 |
| **AC-46's "no pipeline scope taken" conjunct is reinterpreted and left without an instrument** | 0071 #3, 0072 #7 | 70, 55 |
| **0074's two adjacent type-touch rows contradict each other on `PipelineValidator`** | 0074 #1, set #7 | 92, 66 |

Round 4's blinding produced three convergences and a probe. Round 5 produced six and a probe suite. The shape is working and should not be changed.

---

## The two Criticals

Both were verified independently against source before this file was written.

### 1. `ServiceProviderLifetimeScope.Dispose()` swallows every disposal failure, so the two new `Error` messages ADR 0070 step 4a exists to add can never fire — FR-13's disposal clause and AC-6 are unsatisfiable as specified (Score: 92) — *reviewer 0070*

The whole of 0070's step 4a and step 5 apparatus is predicated on "releasing an **owned** pipeline scope throws". Trace the specified stack: `IAmAScope.Dispose()` → `ServiceProviderPipelineScope.Dispose()` → `ServiceProviderLifetimeScope.Dispose()` — an ownership 0070 fixes in *Technology Choices* ("`ServiceProviderPipelineScope` owns exactly one `ServiceProviderLifetimeScope`"). But that method catches **every** scope-disposal failure and logs it at `Warning`, and 0070 declares it **unchanged**.

**Evidence**: `ServiceProviderLifetimeScope.cs:462-501` guards both disposal paths — the outstanding-scope drain (`try { DisposeScope(scope); } catch (Exception e) { Log.FailedToDisposeScope(s_logger, e); }`) and the root scope inside the `finally` (identical shape). `:520` declares the message `[LoggerMessage(LogLevel.Warning, "Failed to dispose a service scope while tearing down the Brighter DI-backed factory…")]`. 0070's *Where each type is touched*: "`Dispose()` (`:462`), `DisposeScope` (`:406`) and its context suppression (`:422-436`) are **unchanged**". Step 6 says the new `DisposeAsync()` mirrors `Dispose()`, so the async path inherits the same swallow.

Consequences, each the opposite of what the ADR asserts:
- `TransformPipelineDrain`'s new `FailedToDisposePipelineScope` at `Error` is dead code.
- `FailedToDisposePipelineScopeAfterFailedBuild` is dead code, so **AC-6 fails** — it requires a capturing `ILoggerProvider` for `Paramore.Brighter.*` to record the disposal failure at `LogLevel.Error`; what it would capture is `FailedToDisposeScope` at **`Warning`**, the exact level the ADR says must not be used.
- FR-13's disposal-failure clause is delivered by accident, at the wrong level, from the wrong component.
- The same reasoning reaches **AC-33 on 0071's handler side**, which reuses this handle and this owner.

Compounding it, step 4a's survey is incomplete on precisely this point: "the five messages that exist all log at `Warning`, and **all five are about releasing a mapper or a transform, not about disposing a DI scope**". `ServiceProviderLifetimeScope.Log.FailedToDisposeScope` is a sixth existing `Warning` and it is exactly about disposing a DI scope. Missing it from the family is what let the design conclude the failure would propagate.

**Recommendation**: Three mutually exclusive resolutions, each with different consequences for 0071 and for the release-note ledger — (a) `ServiceProviderPipelineScope.Dispose()`/`DisposeAsync()` surfaces the underlying disposal outcome and rethrows, which requires changing the method the ADR calls unchanged; (b) move `FailedToDisposePipelineScope` down to `ServiceProviderLifetimeScope` and raise `FailedToDisposeScope` to `Error` for the root-scope case, accepting a level change the ADR currently rejects; (c) state that the owned scope's disposal cannot throw and that FR-13's disposal clause and AC-6 are discharged inside the DI package — in which case step 4a, the drain's logger and the `finally`-vs-`AggregateException` restructuring all lose their justification. In every case add `FailedToDisposeScope` to step 4a's enumeration.

### 2. ADR 0074's `Where each type is touched` says `PipelineValidator` "calls the extracted evaluator" — contradicting the row above it, step 1, and the whole of *Technology Choices* (Score: 92) — *reviewer 0074*

**Evidence**: Two consecutive rows of the same table:

```
| `Paramore.Brighter` | — | **nothing.** No new type, no changed signature, no new public API. `PipelineValidator.EvaluateSpecs` (`:152`) stays exactly where and as it is, `private static` |
| `Paramore.Brighter` | `PipelineValidator` | calls the extracted evaluator; no behaviour change, no signature change |
```

Against step 1 ("`PipelineValidator.EvaluateSpecs` (`:152`) is not extracted, not moved and not widened"), against *Technology Choices* ("**Why the harvest loop is NOT extracted to core, and the decorator writes its own**"), and against *Positive* ("Core gains **nothing at all**"). Verified: `EvaluateSpecs` is at `PipelineValidator.cs:152` and is `private static` today. An implementer reading the type-touch table — the artefact the ADR itself designates authoritative — would extract `EvaluateSpecs` to a shared type, adding public API to core's `netstandard2.0` surface, which is the design the ADR rejects.

**Recommendation**: Delete the second `Paramore.Brighter` row. It is a relic of an abandoned extract-to-core design. Set #7 found the same defect independently and adds that `0074:374` and `0074:380` contradict it twice more.

---

## Ranked index — all findings at or above threshold 60

| # | Score | Reviewer | Finding |
| --- | --- | --- | --- |
| 1 | 92 | 0070 | `ServiceProviderLifetimeScope.Dispose()` swallows disposal failures; step 4a's two `Error` messages unreachable; AC-6 unsatisfiable |
| 2 | 92 | 0074 | Type-touch table says `PipelineValidator` "calls the extracted evaluator"; contradicts adjacent row, step 1 and *Technology Choices* |
| 3 | 85 | 0073 | Asserts FR-10 does not contain the `AlwaysNew` rule; FR-10 states it verbatim, and the two "misattributions" are correct |
| 4 | 85 | set | Same defect, reached independently; 0072 builds ladder rows 5–6 and `WarnOnce(IgnoredForAlwaysNewAsk)` on FR-10 |
| 5 | 85 | 0074 | *Negative* says "Nine new types in the DI package **plus one in core**"; core gains no type |
| 6 | 82 | set | FR-25 is owned by no ADR's `Scope`, breaking the protocol 0070 states; 0074's References points at the map's old home |
| 7 | 80 | 0071 | AC-7 does not say what the ADR claims; the swallow-and-log rule for a throwing `Release` has no acceptance criterion |
| 8 | 78 | 0073 | AC-29 misattributed twice — not about the ASP.NET provider, does not need a controller host |
| 9 | 78 | set | Ledger claimed a superset of AC-24 three times; two of AC-24's six enumerated interfaces are absent and never break |
| 10 | 76 | 0071 | Step 6 requires a test asserting "surface the failure composed"; step 2 requires `Dispose()` to throw nothing |
| 11 | 75 | 0072 | `GetOrAdd` faulted-entry eviction specified without the removal discipline that makes it safe |
| 12 | 72 | set | 0072 and 0075 both claim the same edit to the same five types; 0072's commit references a type 0075 introduces |
| 13 | 72 | 0074 | "All reportable as misses" / "**Two** rows report wrongly" contradict C-20(i)'s false-warning direction |
| 14 | 70 | 0071 | Contract table drops half of AC-46's assertion and records no owed amendment |
| 15 | 70 | 0072 | NFR-6 cited for two mutually exclusive readings inside one ADR |
| 16 | 70 | 0074 | "NFR-9 … this is the only place NFR-9 lands" contradicted by 0075's own `Scope` |
| 17 | 70 | 0075 | ADR contradicts approved FR-9(ii) on whether a body-level leak reaches the caller — **probe says the ADR is right and the requirement is wrong** |
| 18 | 70 | set | NFR-9 assigned to 0074 by two siblings and its own clause map, but absent from 0074's `Scope` |
| 19 | 70 | set | Four ADRs record what an earlier revision of themselves said — tone violation named by `documentation.md` |
| 20 | 68 | 0070 | The finalizer now disposes a container DI scope on the finalizer thread; never addressed |
| 21 | 68 | 0074 | FR-22.3's de-duplication specified three ways that do not compose |
| 22 | 68 | 0073 | "A rule that holds without exception across the repository" disproved by two `src/` packages |
| 23 | 68 | 0076 | `BrighterOptionsRegistration` has no declaration and no specified member, yet is the sole input to a sibling rule that fails startup |
| 24 | 68 | 0076 | Risk table claims AC-45 asserts the before-ordering; it does not |
| 25 | 66 | 0072 | `ScopedArtefactCache` fallback contradicts the two places that say the private field is replaced |
| 26 | 66 | set | 0074 contradicts itself in adjacent rows on whether `PipelineValidator` changes |
| 27 | 65 | 0070 | Step 7a misstates AC-24 and contradicts the ADR's own forces bullet |
| 28 | 65 | 0073 | Step 4a says "Nine" three times and "the ten criteria" once — **round-4 residue** |
| 29 | 65 | 0073 | Review-loop aside survives in the Decision — "though an earlier revision said it was" |
| 30 | 65 | 0071 | FR-5 used three times as authority for a rule it does not govern, and absent from References |
| 31 | 65 | 0074 | The four `IBrighterOptions` registration-site citations describe a state ADR 0076 deletes |
| 32 | 65 | 0075 | Two references to an earlier revision of the ADR, one to the authoring process |
| 33 | 64 | 0072 | Ladder row 8's diagnostic assigned to FR-23, whose text does not cover it; no AC guards it |
| 34 | 64 | 0074 | `Scope` names neither FR-25 nor NFR-9, yet the body schedules the page and claims to discharge NFR-9 |
| 35 | 62 | 0071 | Two review-loop asides about an earlier draft |
| 36 | 62 | 0072 | Two references to earlier revisions of this document |
| 37 | 62 | 0073 | `/spec:tasks` referenced as an authority — the only ADR in the set to do it |
| 38 | 62 | 0075 | Alternative 3a rejected on a premise that does not hold: NFR-7 needs a public *read*, not a public *write* |
| 39 | 62 | 0076 | Risk table's "All four sites apply it" contradicts the Decision, which removes three of the four |
| 40 | 62 | set | Four ADRs carry earlier-revision archaeology (set-level form of 19) |
| 41 | 62 | 0071 | Touched table names one new `Log` member; the implementation requires two |
| 42 | 60 | 0070 | Synchronous release of an *async* transform pipeline's scope at two live sites, unacknowledged |
| 43 | 60 | 0070 | The completed-transform-pipeline behaviour has no acceptance criterion and step 9a proposes no substitute |
| 44 | 60 | 0075 | Step 5 presents the cross-subscriber leak as a property of the shipped design |
| 45 | 60 | 0076 | AC-24 cited as pinning the `IBrighterOptions` break; AC-24's enumeration does not reach it |

---

## Findings by reviewer

### ADR 0070 — 10 findings, 5 at or above 60

1. **(92)** `ServiceProviderLifetimeScope.Dispose()` swallows every disposal failure — see *The two Criticals* above.
2. **(68)** *The finalizer now disposes a container DI scope on the finalizer thread, and the ADR does not address it.* `TransformPipeline<TRequest>` and `TransformPipelineAsync<TRequest>` have finalizers calling `ReleaseUnmanagedResources()` → the drain (`TransformPipeline.cs:50-60`, `:69-71`). Step 5 adds a third drain step disposing the `IAmAScope`, so a pipeline neither `Dispose`d nor `DisposeAsync`ed calls `IServiceScope.Dispose()` from the CLR finalizer thread. `ServiceProviderLifetimeScope.DisposeScope` (`:406-438`) *blocks* on `pending.AsTask().GetAwaiter().GetResult()` when the scope is `IAsyncDisposable` — which every MS DI scope is. Blocking the finalizer thread stalls all finalization. The context suppression at `:422-436` does not help: there is no captured context on that thread and the hazard is the block itself. **Recommendation**: add a clause to step 5 or a *Risks* row stating whether the third drain step is skipped on the finalizer path or runs and why blocking is acceptable — the same question ADR 0068 answered for lease release.
3. **(65)** *`ServiceProviderPipelineScope` gets no contract table and no stated accessibility, and the siblings disagree about what it is.* The skeleton requires each significant type to carry a contract table; every sibling names accessibility in the type heading (0071 `(core, public)`, 0072 `(new, DI package, internal)`, 0075 `(new, core, public, static)`). `ServiceProviderLifetimeScope` is `internal sealed` (`:42`), so a `public` `ServiceProviderPipelineScope` cannot take one on a public constructor (CS0051) — the rule 0074 worked around for `ScopeConfigurationValidator`. 0072 says its borrowed construction path "stays **internal**" (`:233`, `:320`) while its Alternative 2 rejects a public borrowed constructor because it "freezes `ServiceProviderPipelineScope`'s **public shape** forever". 0070, where the type is introduced, settles neither.
4. **(60)** *Synchronous release of an async transform pipeline's scope at two live sites.* `TransformPipelineBuilderAsync.CleanUpAfterFailedBuild` is `private void` (`:231`) delegating to `pipeline.Dispose()` (`:239`); `OutboxProducerMediator.cs:569` builds a `UnwrapPipelineAsync` and releases it at `:582` through `ReleasePipeline`, whose parameter is `IDisposable` (`:1269`). Step 6 enumerates blocking releases as "ADR 0071's handler pipelines" only.
5. **(60)** *The completed-transform-pipeline behaviour has no acceptance criterion.* Step 4a and step 5 introduce non-obvious new behaviour — the drain acquires a logger, gains a `try`/`finally` deferring the `AggregateException` composition, and swallows a scope-disposal failure after logging at `Error`. The ADR states honestly that nothing exercises it, and step 9a's six-row table (AC-1, AC-2, AC-3, AC-4, AC-21, AC-23) does not reach it. Verified: FR-13's disposal clause says "Discharged by AC-33" (`requirements.md:230`) and AC-33's Given is a `Send` with a container-`Scoped` dependency of the **handler** — 0071's family.
6. **(58)** *Step 7a misstates AC-24 and contradicts the ADR's own forces bullet.* Step 7a: "AC-24's own wording **enumerates** a different six". AC-24 enumerates nothing — its clause is a definite description ("for each of the six factory interfaces whose signature changed"); the enumeration belongs to NFR-1's withdrawal paragraph (`requirements.md:352`). ADR line 86 states it correctly, so line 379 contradicts line 86. Under the design the set of factory interfaces whose signatures actually change is **five**, not six, on either reading.
7. **(52)** *`## Context` buries the problem under ~500 words of set-level bookkeeping.* Lines 30–38: a ~230-word paragraph on NFR/constraint distribution and a ~250-word paragraph on FR-13's division, before the reader reaches the actual problem at line 60.
8. **(42)** *Duplicated clause in step 9*, verbatim: "It does not reach Brighter's own paths: In Brighter's own paths this does not arise:".
9. **(38)** *`TransformPipelineBuilderAsync` row not parallel to the sync row it mirrors.* Sync lists five references; async says "the same, on `:93`, `:134`, `:255`, `:231`" — four, positionally implying `:255` is the `FindMessageMapper` analogue (it is the body line; the declaration is `:253`) and silently dropping `BuildTransformPipeline` (`:174`).
10. **(36)** *Citation density outside `Implementation Approach`* — seven `file:line` in three sentences at line 80.

### ADR 0071 — 12 findings, 6 at or above 60

1. **(80)** *AC-7 does not say what the ADR claims, and the ADR's only observable behavioural change has no acceptance criterion.* The ADR designates AC-7 as regression guard for "a throwing handler `Release` is logged at `Error` and swallowed" (lines 291, 334, 358) and specifies a test with a Given AC-7 does not contain. AC-7's Given has no throwing `Release` — only a throwing handler. The ADR's own Context (line 30) uses AC-7 correctly, so the file contains both readings. The rule is a behavioural break the ADR calls "the one thing an application can observe change" (line 368), and it is uncovered by any AC. **Recommendation**: strike the AC-7 designation for the handler-release half; record an owed AC amendment alongside the AC-14 one.
2. **(76)** *Step 6 requires a test asserting behaviour step 2 forbids.* Step 2: "log every held failure at `LogLevel.Error` … and **throw nothing**"; "**`Dispose()` never throws**". Step 6: a `HandlerLifetimeScope` whose factory's `Release` throws on the first of three "must still release the other two … and **surface the failure composed**" — the transform family's shape, and the residue of the design 0071 rejected. The vocabulary of step 2 leaks the same way ("catching per item and **holding** the failure").
3. **(70)** *The contract table drops half of AC-46's assertion and records no owed amendment.* AC-46's Then: "the recorder shows **zero** adoption decisions **and no pipeline scope taken**". The ADR quotes the clause and then denies it is there. Under this design a `{Transient, Transient, Transient}` handler pipeline *does* hold a non-null `IAmAScope`, so AC-46's second conjunct is false as literally worded. The ADR handles exactly this correctly for AC-14 (line 365) and gives AC-46 no such treatment.
4. **(65)** *FR-5 used three times as authority for a rule it does not govern.* FR-5's text is scoped to a failed **transform-pipeline** build (`CleanUpAfterFailedBuild`); the ADR applies it to a handler pipeline at execution time (lines 30, 289, 370). FR-6 alone carries the argument. FR-5 is in the frontmatter summary and three times in the body but **not** in `## References`.
5. **(62)** *Touched table names one new `Log` member; the implementation requires two.* Table names `FailedToDisposePipelineScope`; step 2 requires that **and** `FailedToReleaseHandler`, both on the existing `Log` partial (`HandlerLifetimeScope.cs:95`). Verified: `:95-108` is a `private static partial class Log` with exactly four `Debug` members.
6. **(62)** *Two review-loop asides about an earlier draft* — lines 328 and 365.
7. **(52)** *The contract table lists `IAmALifetime.PipelineScope` twice*, one row carrying ~120 words of test-design prose in the "Error conditions" column, with no error condition in it.
8. **(52)** *Alternative 6's decision leaves two of AC-24's enumerated six never breaking.* NFR-1's withdrawal names exactly the four mapper/transformer factories plus `IAmAHandlerFactorySync`/`IAmAHandlerFactoryAsync`; alternative 6 puts `CreatePipelineScope()` on the base `IAmAHandlerFactory`, so those two never break. A superset entry covers extra breaks; it does not cover two enumerated interfaces with nothing to say about them.
9. **(45)** *The forces bullet's routing claim is inaccurate for two of the four resolution sites.* The two handler sites (`PipelineBuilder.cs:191`, `:236`) are inside `Build`/`BuildAsync` directly and pass through none of the six threading methods, which route only to the two decorator sites. The counts themselves all reproduce.
10. **(42)** *Cross-ADR: 0070 says AC-33 "is named in 0071 and not here", then names it twice* (`0070:338`, `0070:477`). 0071's own AC-33 claims are correct.
11. **(35)** *Diagram 3's `implements` edge routes through the `HandlerLifetimeScope` node* and reads as a false dependency. Found by rendering to PNG at 1600px and looking at it.
12. **(30)** *NFR-4 forces bullet cites `:129` for a `TryRemove` that is at `:135`.*

### ADR 0072 — 16 findings, 5 at or above 60

1. **(75)** *`GetOrAdd`'s faulted-entry eviction is specified without the removal discipline that makes it safe.* The `ScopedArtefactCache` contract claims the concurrency protocol is inherited verbatim from `ServiceProviderLifetimeScope.cs:163-178`, then introduces one change — evict a faulted `Lazy` — as a bare rule. With `ConcurrentDictionary<Type, Lazy<object?>>` and `Lazy` in default `ExecutionAndPublication` mode, **every** thread that awaited the faulting `Lazy` observes the exception and will try to evict. The naive `_cache.TryRemove(type, out _)` removes whatever is under the key, including a healthy `Lazy` a concurrent resolver published in between. Under `JoinAmbient` that yields **two `Scoped` artefacts in one borrowed request scope** — precisely what FR-16(a) and AC-17 forbid. The safe form requires removing the observed pair (`TryRemove(KeyValuePair<…>)`, .NET Core 3.0+). Verified: `:167-176` has no removal at all today, so nothing about eviction is inherited. This is squarely inside NFR-4, which the ADR cites for this section.
2. **(70)** *NFR-6 cited for two mutually exclusive readings inside one ADR.* Line 202 disclaims NFR-6 as a budget for anything but DI scopes; lines 402 and 458 invoke it as forbidding a per-pipeline **artefact resolution**. NFR-6 verbatim: "Cost proportional to pipelines, not to resolutions … at most one DI scope begin/release per pipeline; it must not add a DI scope per resolved instance." An artefact resolution per pipeline is cost proportional to pipelines — exactly what NFR-6 permits.
3. **(66)** *The `ScopedArtefactCache` fallback contradicts the two places that say the private field is replaced.* Two statements say `_scopedInstances` *becomes* a resolution; one says the handle "falls back to a private cache" when the borrowed provider cannot supply the service. The fallback needs the field to survive, and the ADR never says where it lives, whether the owned path has one, how "cannot supply" is detected, or whether the degradation earns a diagnostic. It is also arguably the same input as the "residue" case, which gets a *different* outcome (`ConfigurationException`, `PipelineBuilder.cs:193` — citation verified).
4. **(64)** *Ladder row 8's diagnostic assigned to FR-23, whose text does not cover it, and no AC guards it.* FR-23 is written entirely about a **stale** resolution source and AC-29 over a *capturing* provider whose scope was disposed. Nothing in the requirements assigns a diagnostic to a foreign-role ambient.
5. **(62)** *Two references to earlier revisions of this document* — the `Scope` paragraph ("Those five were **previously listed as *served***") and *Technology Choices* ("the second half was missing from an earlier revision").
6. **(58)** *`AmbientScopeSourceException` — a new public core type with a load-bearing invariant — has no Key Components entry and no contract table.* Third parties are obliged to construct it (NFR-7) and its `InnerException is never null` invariant licenses `e.InnerException!` in six catch blocks; its contract appears only in a *Technology Choices* paragraph.
7. **(55)** *AC-46's "no pipeline scope taken" clause reinterpreted and left without an instrument.* AC-13's own note says the recorder "characterises which affinity each pipeline asked with, **and nothing else**". **Recommendation**: state the equivalence — under FR-27.1 "asks exactly once" and "takes a pipeline scope" are the same condition — rather than naming an instrument that cannot see it.
8. **(52)** *Citation density outside `Implementation Approach`* — six `file:line` in one sentence of `#### ScopeAffinityPolicy`, eight in *Technology Choices*' first paragraph. All ten verified correct.
9. **(50)** *The `**Scope**:` block is one ~700-word paragraph* carrying twenty-odd identifiers and four sibling ADRs, with three nested em-dash asides.
10. **(44)** *`AmbientScopeDiagnostics` resolution failure in a hand-constructed factory is not addressed.* The five factories are `public` and take a bare `IServiceProvider`; a factory constructed over a provider that never ran `AddBrighter` resolves `null` for the diagnostics singleton, making `WarnOnce` a null dereference. The `ScopeAffinityPolicy` ctor explicitly handles a `null` `IBrighterOptions`, so the pattern is established but not applied.
11. **(42)** *An internal cross-reference points the wrong way* — "see *which providers reach rows 8 and 9* **below**" at line 466; that section is line 396.
12. **(40)** *Catch-block line ranges inconsistent with the table's own stated convention.* The table cites `:202-204`/`:248-250` while stating the convention as "quoted catch-line through closing brace", which the transform rows follow and gives `:202-205`/`:248-251` — what step 1b uses. **`0071:207` carries the same spelling, so fixing one file without the other creates set-level drift.**
13. **(38)** *Row 5's diagnostic spelled three different ways*, one of which is not the required condition name.
14. **(36)** *`References` cites AC-15 and AC-34, neither used in the body.* AC-34 is arguably the natural guard for FR-16(b), which the ADR discusses twice.
15. **(34)** *The mapper registry credited with a `MapperLifetime` it does not have.*
16. **(32)** *Context's first sentence names a member and a type before the problem.*

### ADR 0073 — 11 findings, 6 at or above 60

1. **(85)** *Asserts a defect in FR-10 that does not exist.* Line 72: "(FR-10 states that the seam exists and names its three types; **the `AlwaysNew` rule is not in it**, though the requirements twice attribute it there.)" `requirements.md:215`, inside FR-10: "`ScopeAffinity.AlwaysNew` means exactly one thing — *do not adopt an ambient*: a `GetAmbient(AlwaysNew)` call **must neither consult nor adopt an ambient and must return nothing** … **That obligation is stated on the provider and enforced on Brighter's side.**" The two "misattributions" (AC-18's and FR-24.4's) are therefore correct. The parenthetical is a false claim about an approved requirement, used as design justification, and it implicitly invites an edit to a closed phase.
2. **(78)** *AC-29 misattributed twice.* Used as a reason `HttpContextScopeProvider` must be `public` and as one of the nine criteria forcing a new ASP.NET test project. AC-29's Given explicitly excludes the ASP.NET provider ("ASP.NET Core's built-in accessor clears itself at end of request and so offers no ambient at all") and its provider is AC-35's capturing shape in a **console** host. This contradicts the ADR's own text twice (lines 65 and 293: FR-23's condition "does not arise here").
3. **(68)** *"A rule that holds without exception across the repository" is disproved by two `src/` packages.* `Paramore.Brighter.DynamoDb/DynamoDbTableFactory.cs:33` declares `namespace Paramore.Brighter.Outbox.DynamoDB` — a separate assembly in the same solution, exactly the pattern Alternative 6 says no package does; `Paramore.Brighter.Archive.Azure.csproj:10` sets `<RootNamespace>Paramore.Brighter.Storage.Azure`. The narrow form of the claim reproduces; the broad form does not.
4. **(65)** *Step 4a says "Nine" three times and "the ten criteria" once*, with no tenth criterion named. **This is round-4 residue**: round 4 dropped AC-14 from the list and changed ten→nine, missing line 280.
5. **(65)** *A review-loop aside survives in the Decision* — "though an earlier revision said it was".
6. **(62)** *`/spec:tasks` referenced as an authority* — ephemeral workflow state, and `grep` returns 0 for `/spec:` across the other six ADRs, so 0073 is alone in this.
7. **(55)** *The new package's position in the release-note ledger is never stated*, while every sibling states its own. The honest answer may be "nothing", but the set's convention is that each sibling says so.
8. **(52)** *"The three C-11 working names" — C-11 says two remain and records the third as settled* by this very ADR (requirements revision 18 absorbed the choice).
9. **(48)** *D11 paraphrased into something D11 does not say.* The conclusion drawn is right; the citation supporting it is not.
10. **(48)** *The NFR-8 sentence is copied from a sibling where it applied.* This package declares neither `IAmAScope` nor `IAmALifetime`, the pair NFR-8 requires disambiguated.
11. **(42)** *`## Decision` runs to three paragraphs*, the third an ~180-word accessibility argument placed ahead of the mechanism.

### ADR 0074 — 16 findings, 7 at or above 60

1. **(92)** *`PipelineValidator` "calls the extracted evaluator"* — see *The two Criticals* above.
2. **(85)** *"Nine new types in the DI package **plus one in core**".* The DI count of 9 reproduces from the type table and the diagram; "plus one in core" contradicts *Positive* ("Core gains **nothing at all** — not a container concept, and not a type"), the type table, AC-22.3 and the ADR's central claim.
3. **(72)** *"All reportable as misses" / "**Two** rows can report wrongly" contradict C-20(i).* FR-22.3's detection contract: "The divergence — **a warning may be raised against**, or missed on, a constructor MS DI would not have chosen". The false-positive direction has no row in the failure-mode table, so the count is understated (three, not two) and the table is not exhaustive over C-20(i).
4. **(70)** *"NFR-9 is discharged by writing it, and **this is the only place NFR-9 lands**" is contradicted by 0075's own `Scope`*, which says it "discharges … NFR-9's `Publish`-subscriber and nested-pipeline rows". 0070:32 sides with 0074, so the set does not agree with itself.
5. **(68)** *FR-22.3's de-duplication specified three ways that do not compose.* Findings de-duplicated by (artefact type, dependency service type); the snapshot yields one `ArtefactRegistration` per **(type, kind)** so duplicates survive; de-duplication applied "where the candidate list is built" while the rule "appends every failure". Reachable: `{Transient, Singleton, Singleton}` is FR-22.2-conformant and a dual-kind type is then evaluated twice with both governing lifetimes `Singleton`.
6. **(65)** *The four `IBrighterOptions` registration-site citations describe a state ADR 0076 deletes.* Line numbers verified correct against today's source, but 0076 step 3 deletes all four registrations and replaces the `TryAddSingleton`s with a guard plus `Add` inside `RegisterBrighterOptions`, called once from `BrighterHandlerBuilder` (`:142`).
7. **(64)** *`Scope` names neither FR-25 nor NFR-9*, yet step 7 makes the whole guidance page a deliverable of this plan, carries the eleven-row clause map, and asserts NFR-9 is discharged here. 0070:32 states the convention that an NFR resolving to one decision is named in that ADR's `Scope`.
8. **(58)** *"AC-42 tests each" of D15's three cases* — AC-42 has a widest clause and a tie clause, and no clause for a type with no public constructor or only a parameterless one.
9. **(56)** *The one input FR-22.4 cannot derive is never named.* 0076 names the carrier — `BrighterOptionsRegistration`, deposited in the collection — and says 0074's validator is its only reader; 0074 never names it and never says it is a descriptor in the snapshot, which is the fact that makes the input reachable.
10. **(55)** *Instance-registered descriptors read for FR-24.3/FR-17 and ignored for FR-22.3, with no reason given.* The stated reason ("no statically known implementation type") is true of a factory delegate but not of an instance.
11. **(54)** *The diagram puts `Specification` and `ValidationResultCollector` in two separate core boxes*, and draws a commentary note as a component with a solid edge into it. Found by rendering to PNG at 1600px.
12. **(52)** *Concurrency and thread safety never addressed.* `grep` for `thread|concurren|NFR-4|race` returns one hit, in References, in an unrelated sense. The *Both host shapes* table establishes the fact that would settle it; the ADR never draws the conclusion.
13. **(50)** *`(AC-24)` attached to a release-note item AC-24 does not require.* The item is legitimate — 0070 step 7a carries it — but AC-24 is not its warrant.
14. **(45)** *"AC-44 walks each message to a concrete triple" is true of three of the six.* For the three registration messages AC-44 requires a corrective **registration** action.
15. **(45)** *`ScopeConfigurationValidator` is public for a reason the ADR itself treats as a defect* — the cast the *Negative* section records as a break.
16. **(40)** *Reference-list drift*: AC-25 listed but never cited; FR-13 cited but not listed.

### ADR 0075 — 13 findings, 4 at or above 60

1. **(70)** *The ADR flatly contradicts approved FR-9(ii), and never says so.* Step 5: "Nor does an unrestored write inside a `Parallel.ForEach` **body** reach the caller." `requirements.md:207` (FR-9(ii)): "setting it inside a body and never restoring it **leaves the caller suppressed after `Publish` returns**. That is detectable, and AC-39's final clauses detect it." **The probe shows the ADR is right and the approved requirement is wrong** — see the probe log below. The two documents now disagree about what AC-39 can detect, and only one is true.
2. **(65)** *Two references to an earlier revision of the ADR, one to the authoring process.*
3. **(62)** *Alternative 3a rejected on a premise that does not hold.* 3a satisfies NFR-7 completely — NFR-7 needs a public **read**, and the ADR everywhere else defines the NFR-7 case as reading the flag. The real reasons for the public write are testability (no `InternalsVisibleTo` in this repository) and the host use case, and the ADR states both, then obscures them by re-invoking NFR-7.
4. **(60)** *Step 5 presents the cross-subscriber leak as a property of the shipped design.* With bracket 2 restoring on every exit path there is nothing left to leak; the conditional surfaces ten lines later in step 5a. FR-9(i) keeps the conditional ("**Without an explicit restore**…"); the ADR's heading does not.
5. **(58)** *Suppression carried onto background work a subscriber's handler starts is never addressed.* Probe F′ confirms a branch started under a live bracket stays suppressed after the bracket is disposed — intended under D6, but it is the one way suppression genuinely outlives the publish, and step 5's third bullet asserts the opposite ("nothing survives onto unrelated work").
6. **(56)** *The "costs nothing" claim ignores the consumer path.* Every `MT_EVENT` message dispatches through `Publish`/`PublishAsync` (`Reactor.cs:406`, `Proactor.cs:130`), so under sustained consumption two `AsyncLocal` writes per subscriber per message are on the hot path — the path NFR-5/NFR-6 and AC-23 are graded over.
7. **(55)** *0075 and 0074 disagree on who discharges NFR-9's `Publish`-subscriber rows.*
8. **(55)** *FR-9(b)'s "or around its task" option is neither adopted nor rejected by name.*
9. **(54)** *The public mutator's only stated benefit is also the ADR's named likeliest misuse*, and the correct shape for that use is never given — though it is exactly bracket 2's async shape.
10. **(52)** *"The same kind of break" as the eight interface signatures — it is not.* Those are source **and** binary; a defaulted constructor parameter is binary only. 0070 step 7a draws the distinction itself and names the correct comparator.
11. **(50)** *The sequence diagram draws bracket 1 inside an explicit loop but bracket 2 in neither branch*, so the ADR's central placement invariant is not readable off it.
12. **(45)** *"AC-39's final clause" names the wrong clause, twice* — the final clause is the no-ordering/no-overlap constraint; the leak assertions are the penultimate clause, and AC-39 calls them "the clauses".
13. **(38)** *ADR 0067, whose `Terms` block 0075 leans on, still describes the set as 0070–0074.* Owner's call whether an `Accepted` ADR is edited for this.

### ADR 0076 — 9 findings, 4 at or above 60

1. **(68)** *`BrighterOptionsRegistration` has no declaration and no specified member.* Every other type this ADR introduces gets a full C# block; this one gets a diagram node, a roles row, a contract row and one constructor call. 0074 explicitly declines to define it ("defines nothing about it beyond the question it puts"), and 0074 calls this "the one rule that depends on a sibling's implementation detail" whose wrong answer is "a false `Error` that fails startup".
2. **(68)** *The risk table claims AC-45 asserts the before-ordering. It does not.* AC-45's Given never states where the extension call sits relative to the entry point. The before-ordering is pinned positively on exactly one path (AC-48's ASP.NET host); AC-50 pins it only in the *defeated* case. Also, AC-48's first branch starts from `AlwaysNew`, which **is** the property's default, so the "non-default" qualifier is wrong for it.
3. **(62)** *The risk table's "All four sites apply it" contradicts the Decision*, which removes three of the four sites and replaces `TryAddSingleton` with an explicit guard.
4. **(60)** *AC-24 cited as pinning the `IBrighterOptions` release-note break; AC-24's enumeration does not reach it.* 0070 handles the same situation honestly ("deliberately a **superset** of AC-24"); 0076 reads as though an AC pins the obligation.
5. **(57)** *Thread-safety subsection enumerates one route by which a partially-applied options object is observable; there are two.* The `AddBrighter(Action)` path has the same exposure and the ADR says so — in a *Negative* bullet, not here.
6. **(55)** *"every one of them reads only subscriptions, the channel factory or the inbox" is false of `BuildDispatcher`*, which also reads `InstrumentationOptions` (`:182`) and `ShutdownTimeout` (`:183`). The load-bearing half — none downcasts — survives.
7. **(52)** *"AC-45's second clause" names two different clauses in two places*; line 210's reading is right, line 77's is off by one and attributes the opposite outcome to the same label.
8. **(50)** *A Negative bullet carries three `file:line` references.* The forces section is clean (zero across seven bullets).
9. **(45)** *`Where the pieces live` draws a read as an unlabelled solid arrow that reads as a write*, while the same relationship is drawn correctly (dotted, reversed, labelled) in the second diagram.

### Set-level — 11 findings, 8 at or above 60

1. **(85)** *0073 asserts FR-10 does not contain the `AlwaysNew` rule.* Reached independently of the 0073 reviewer. Adds: 0072 attributes the same obligation to FR-10 and builds ladder rows 5–6 and `WarnOnce(IgnoredForAlwaysNewAsk, …)` on it, and the two clauses 0073 calls misattributions — FR-27.1 (`:249`) and FR-24.4 (`:240`) — are correct.
2. **(82)** *FR-25 is owned by no ADR's `Scope`, and 0074's References points readers at a map that is not where it says it is.* 0070:32 declares the protocol ("Every FR has exactly one owning ADR, **named in that ADR's `Scope`**"). 0074's `Scope` discharges "FR-22 and the evaluation-site half of FR-24.3 and of FR-17" — no FR-25. `0074:482` says "the clause-to-ADR map is in `Scope`"; it is at `0074:398-409`, under *Implementation Approach* step 7. `grep -c 'discharges FR-25'` across all seven = 0. A coverage auditor following both pointers finds nothing at either end.
3. **(78)** *The ledger is claimed a superset of AC-24 three times; two of AC-24's enumerated six are absent.* The ledger names **eight** interfaces; AC-24's six (per NFR-1's withdrawal list, which `0070:379` confirms is the referent) include `IAmAHandlerFactorySync`/`IAmAHandlerFactoryAsync`, which both 0070 (`:270`) and 0071 (`:264`) list as **unchanged**. The entry states more items than AC-24 enumerates but is not a superset of AC-24's *set*. The bite is where 0071 says the external cost is highest (`:355`): "`IAmAHandlerFactorySync`'s documentation says the opposite … an out-of-repo implementation is the expected case, and every one of them stops compiling."
4. **(72)** *0072 and 0075 both claim the same edit to the same five types, and 0072's commit references a type 0075 introduces.* `0072:322` ("the read is at this ADR's step 3") against `0075:220` ("one read of `IsSuppressed`, **specified here**"). 0072's step-3 pseudo-code dereferences `AmbientScopeSuppression`, declared **new** at `0075:217`, so 0072's commit would not compile standalone. 0070 handled exactly this hazard explicitly for `AmbientScopeSourceException` (`0070:323`); nothing equivalent is written here.
5. **(70)** *NFR-9's truth table assigned to 0074 by two siblings and by its own clause map, but absent from 0074's `Scope`* — while 0075's `Scope` claims part of it. 0070:32 names NFR-9 as the exception that *is* meant to appear in an owning ADR's `Scope`.
6. **(70)** *Four ADRs record what an earlier revision of themselves said.* Complete set of matches: `0071:328`, `0072:334`, `0073:84`, `0075:259`. `documentation.md:181` names this shape by example; `:185` bans ephemeral working state. In each case the surviving technical content is already stated.
7. **(66)** *0074 contradicts itself in adjacent rows on whether `PipelineValidator` changes* — `0074:350` against `:351`, with `:374`, `:380` and `:418` contradicting `:351` three more times.
8. **(62)** *The `## References` "Related ADRs" lists are inconsistent across the set.* Sibling coverage ranges from complete (0076: all six) to none (0070). The omissions drop precisely the load-bearing hand-offs: 0074 omits 0075 while twice naming it as a source (`:402`, `:404`), and 0075 omits 0074 while naming it as the ADR that "declares the page and holds the clause-to-source map" (`:272`).

| ADR | Siblings in References | Named in `Scope` but absent |
| --- | --- | --- |
| 0070 | *(none)* | 0071, 0072, 0073, 0074, 0075, 0076 |
| 0071 | 0070 | 0072, 0073, 0074, 0075, 0076 |
| 0072 | 0070, 0071, 0075 | 0073, 0074, 0076 |
| 0073 | 0070, 0072, 0074, 0075, 0076 | 0071 |
| 0074 | 0070, 0071, 0072, 0073, 0076 | **0075** |
| 0075 | 0070, 0071, 0072 | 0073, 0074, 0076 |
| 0076 | all six | — |

9. **(52)** *0070 says AC-33 "is named in 0071 and not here", then names AC-33 in its own References* (`0070:34` against `0070:477`).
10. **(48)** *`docs/adr/index.md` states 98 ADRs; 99 ADR files exist.* Pre-existing and not caused by this set, but the approval commit regenerates the index so it lands in this diff. `0057-replay-outbox-on-inbox-duplicate.md` has no YAML frontmatter, so the generator skips it. All seven of 0070–0076 are present, correctly tagged, and their index summaries match their frontmatter verbatim.
11. **(45)** *FR-16(b) is never named as owned, while FR-16(a) is named twice.* The set is unusually careful about clause-level ownership everywhere else. FR-16(b)'s discharge is AC-34 and its mechanism is 0072's; 0072 cites AC-34 but never FR-16b.

---

## Verification log — what the round proved clean

Recorded so a later round need not re-derive it, and so a regression shows up.

**Mechanical set properties — all clean, verified independently by more than one reviewer.** Seven files, all `Proposed`, all in `.adr-list`; frontmatter field sets identical (`id`, `title`, `status`, `author`, `created`, `summary`, `tags`), each `summary` agreeing with its own bold Decision sentence; every ADR's index row matching frontmatter verbatim; escaped entities **0** in all seven.

**All 15 mermaid diagrams render** under `mermaid-cli@11` — 0070: 2 · 0071: 3 · 0072: 1 · 0073: 2 · 0074: 2 · 0075: 2 · 0076: 3. Every reviewer rendered its own and the set reviewer rendered all fifteen; zero failures on both passes. Several reviewers additionally rendered the most complex to PNG at 1600px and inspected it, which is how findings 0071 #11, 0074 #11, 0075 #11 and 0076 #9 were found — all four are legibility defects invisible to a parse check.

**Sibling maps: PASS.** All seven `### Where this ADR sits` tables are byte-identical after normalisation, each with its own row bolded and marked *(this one)*. No stale map anywhere in the set.

**The unifying sentence: PASS.** "the per-pipeline object carries the DI scope" appears verbatim in all seven, with no paraphrases and no variant spellings.

**Heading skeletons: PASS on order and nesting, and on wording.** All seven run the canonical skeleton; the only variation is optional `####` subsections the skeleton permits. No drift finding from any reviewer.

**Terminology: PASS.** No `chain`/`pipeline` drift survives the rename — the six `chain*` hits are the rejected `IAmAChainScope` name, "chaining" in the fluent sense, and "chain to it" for exception chaining. "lifetime scope" is used only for the existing types, never for a type this set introduces.

**Tone, apart from the earlier-revision archaeology (set #6):** no reference to any conversation participant in any of the seven — `grep` for "the user", "at the user's", "per the user", "reviewer", "review round", "PROMPT.md", "spec phase" returns nothing in 0070, 0074 or 0076, and the near-misses elsewhere ("a reviewer can see at a glance", "reported as the user's") refer to a future code reviewer and to the application author, both durable usage. One ephemeral-workflow reference (`/spec:tasks`, 0073 #6).

**File paths: PASS.** All 60 distinct `.cs` filenames cited across the set exist; all fully-qualified repo paths exist except `docs/guides/lifetimes-and-scoping.md`, which is the page FR-25 requires be created.

**Counts that reproduced exactly** (re-derived independently, several by more than one reviewer): 0070's 12 `src/` implementations, 70 test doubles, 64 factory doubles across 37 files, 6 registry doubles across 3, 82 total · 0071's 21 `IAmAHandlerFactory` implementations (5 in `src/`, 16 doubles), 7 `IAmALifetime` (1 + 6), six threading methods + two resolution helpers = eight, four resolution sites, three constructors, four existing `Debug` members · 0072's five container-backed factories, six builder catch blocks, four registration entry points all routing through `BrighterHandlerBuilder` (`:142` via `:119`), ten ladder rows, three latches, twelve new type names all absent from `src/` · 0073's 24 `src/` projects on `$(BrighterCoreTargetFrameworks)`, 37 test projects, zero ASP.NET references in `tests/` · 0074's six rules, six messages (three errors, three warnings), nine new DI types, FR-25's eleven clauses and the eleven-row map with every row checked at both ends · 0075's 69 test constructions (21 describe-only, 48 dispatch), one `Parallel.ForEach` in `src/` · 0076's four registration sites, five factories, one `IBrighterOptions` implementation repo-wide, five `IAmConsumerOptions` members, 125 files under `tests/` · the release-note ledger at **eleven** bullets, and "eight interfaces break across the two ADRs" consistent in 0070, 0071 and 0076.

**0076's order-independence survived a full independent re-derivation.** The reviewer enumerated all four registration sites from source, found no fifth and no phantom, and walked both orderings at each from the real `TryAdd`/`Add` semantics: "I could not construct a path on which the opt-in is silently lost that the ADR does not already name." Both loss paths it does name reproduce.

**0075's runtime reasoning was settled by probe, not by argument.** Two net9.0 console projects, thirteen cases. Every runtime claim the design leans on is **confirmed**:
- An `async` method is itself an `ExecutionContext` boundary — no write inside `PublishAsync` reaches its caller (C, C′, H). Step 5a is correct, and this is now probe-established for the second round running.
- A plain `void` method and a plain `foreach` **do** leak to the caller (D, E, G), so bracket 1's restore on the synchronous twin is load-bearing.
- The cross-body leak on a shared `Parallel.ForEach` worker is real — 191 of 200 bodies, then 196 of 200 twice.
- Nothing survives onto an unrelated thread-pool work item (A3).
- The async start-loop shape works exactly as step 4 describes: caller restored, every branched task still suppressed (F, F′).
- The contract table's out-of-order-disposal analysis is exactly right, including the "ends suppressed for the rest of its life" tail (I1, I2), and lexical nesting restores correctly (J1, J2).

The one claim that did **not** survive is the requirement's, not the ADR's: with an unrestored body write, the caller reads `IsSuppressed = False` — even in a run where the calling thread demonstrably executed **seven** of the bodies (A, A2). That falsifies FR-9(ii)'s second disjunct (finding 0075 #1).

**Not verified, and declared rather than implied**: no reviewer built or ran the Brighter solution, so 0070 #1's consequence (AC-6 failing) is derived from reading the disposal chain rather than from an executed test. The 0073 reviewer had no reliable network for NuGet and did not check the "only shippable versions are the end-of-life 2.2.x line" claim or the ref-pack assembly split. The 0072 reviewer did not confirm at runtime that `IServiceScopeFactory` is resolvable from every third-party `IServiceProvider` adapter the probe section names. The set reviewer did not check `file:line` citations inside each ADR, that being the single-ADR reviewers' remit.
