# Review: design — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-06
**Threshold**: 60
**Verdict**: NEEDS WORK

53 findings at or above threshold 60. Address these before approving.

**Round 6.** Eight reviewers, all on opus, all blind to `PROMPT.md` and to every earlier round's
findings file, each with its own scratchpad subdirectory. One per ADR (0070–0076) plus one whose
only remit was set-level properties. A ninth run covered three gaps the set-level reviewer declared
— the `Alternatives Considered` blocks in 0073–0076, the C-N/D-N/OOS-N citation sweep, and
Given/When/Then fit for ACs previously checked only for existence. Reviewed at HEAD `d51a26a2f`.

## Counts

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 2 |
| 70-89 (High) | 22 |
| 50-69 (Medium) | 57 |
| 0-49 (Low) | 21 |

**Total findings**: 102
**Findings at or above threshold (60)**: 53

Per reviewer — 0070: 14 (9) · 0071: 16 (8) · 0072: 13 (6) · 0073: 11 (5) · 0074: 15 (7) ·
0075: 8 (4) · 0076: 7 (3) · set-level: 10 (6) · gap-coverage: 8 (5).

**The at-or-above-60 trend is 63 → 71 → 39 → 63 → 45 → 53.** It rose.

## Where the findings landed

Round 5's fixes were committed ADR-by-ADR in eight commits. Both Criticals and most of the Highs
are in that text, and several are a change that landed on one side of a cross-ADR claim without its
counterpart:

- **0072 #1 (92)** — round 5 replaced the private-cache fallback with "decline at the probe"; step 2's
  probe definition has no such test and a probe shows the decline never fires.
- **0074 #1 (90)** — round 5's one-record-per-type collapse landed as an addition without the old
  sentence being deleted; the sentence now asserts both shapes.
- **0070 #1 / set #1 (85 / 72)** — round 5 made `ServiceProviderPipelineScope` public and recorded that
  this settled 0072's internal disagreement. It settled 0070's end only.
- **0070 #2, #3 (80, 76)** — round 5's finalizer skip has no mechanism that could perform it, and its
  justification is falsified by probe.
- **0072 #2 (85)** — round 5's observed-pair eviction uses an API absent on `netstandard2.0`.
- **0074 #2, #3 (72, 68)** — round 5's report-wrongly row count landed in the table and not in the
  Negative bullet, and the condition the new row states does not produce the stated outcome.
- **0070 #4 / 0071 #1, #5 / set #2, #3** — round-5 decision 4 landed AC-51 in three places; it needed
  five, and one of the five got AC-51 where AC-33 belongs.

## Independent convergences

Six, one of them three-way. The reviewers were blind to each other.

| Convergence | Reviewers |
| --- | --- |
| `0070:417` cites AC-51 where FR-13's disposal clause is guarded by AC-33 | 0070 #4, 0071 #5, set #3 |
| `0071:360` cites AC-7 for the rule its own step 2 says AC-7 does not cover | 0071 #1, set #2 |
| `ServiceProviderPipelineScope` is public on a justification 0072 denies in three places | 0070 #1, set #1 |
| The NFR-1 signature arithmetic, wrong at both ends ("five + two" over six; "five, not six" where four) | 0070 #5, 0071 #8 |
| AC-24 does not reach most of step 7a's twelve ledger bullets | 0070 #5, 0076 #1, set #6 |
| `ServiceProviderLifetimeScope.cs:520` points at a brace, not at `FailedToDisposeScope` | 0071 #15, set #9 |
| ADR 0075's alternative 3a is broken — a malformed four-asterisk emphasis run in its key concession | 0075 #3, gap #7 |

## Ranked index — findings at or above threshold

| Score | Reviewer | Finding |
| --- | --- | --- |
| 92 | 0072 #1 | `ScopedArtefactCache` "decline at the probe" is not deliverable by the specified probe |
| 90 | 0074 #1 | `ArtefactRegistration`'s shape stated two contradictory ways in one sentence |
| 85 | 0070 #1 | `ServiceProviderPipelineScope` public on a justification 0072 contradicts |
| 85 | 0071 #1 | The ADR contradicts itself on which AC guards the handler-release rule |
| 85 | 0072 #2 | The mandated eviction API does not exist on `netstandard2.0` |
| 80 | 0070 #2 | The third drain step is asserted skipped on the finalizer path with no mechanism to skip it |
| 80 | 0073 #1 | C-14's falsification produces adoption, not a stale ambient; 0072 asserts the opposite |
| 76 | 0070 #3 | The finalizer skip is licensed by an MS DI claim a probe falsifies |
| 76 | 0072 #3 | `AmbientScopeSourceException`'s never-null invariant asserted, not guaranteed |
| 76 | 0075 #1 | Alternative 5's async half states a narrower harm than the runtime produces |
| 75 | 0071 #2 | The NFR-4 confinement argument rests on a false claim; the eight-method enumeration is short by four |
| 74 | 0071 #3 | "The six release sites" — the count is three, and 0070 says three |
| 74 | 0073 #2 | The ASP.NET reference AC-14 forces onto existing test projects appears in no step or table |
| 72 | 0070 #4 | Step 9a cites AC-51 where every other authority says AC-33 |
| 72 | 0073 #3 | FR-15's package-inertness half has no criterion exercising a package reference |
| 72 | 0074 #2 | Negative bullet and failure-mode table disagree on which C-20 bound is a false positive |
| 72 | 0076 #1 | AC-45 cited as asserting an ordering it does not fix; the second half is false of AC-48 |
| 72 | set #1 | 0070 justifies the public type with a cross-reference 0072 explicitly denies |
| 72 | set #2 | 0071 cites AC-7 for the rule its own Implementation Approach says AC-7 does not cover |
| 72 | gap #1 | 0075 alternative 3a rejects the narrow counter-proposal on a use case the ADR never writes |
| 70 | 0070 #5 | Step 7a's AC-24 defence does not add up: "five by signature and two by base" over six |
| 70 | gap #2 | 0076 alternative 6 cites FR-14 as requiring what C-9 explicitly leaves open |
| 70 | gap #3 | AC-30 covers a `Send` only, but is cited for three verbs and six builder catch sites |
| 70 | gap #4 | 0074's AC-25 claim is not delivered by step 7, which also drops the no-provider dimension |
| 68 | 0074 #3 | The constructor-divergence row's condition does not produce the stated outcome |
| 68 | 0074 #4 | The registration snippet drops `transformerProbe` and disables the wrap-transform rule |
| 68 | 0075 #2 | "Two `AsyncLocal` writes per subscriber" undercounts by half |
| 68 | 0076 #2 | FR-22.4's condition restated with one conjunct where 0074 requires two |
| 68 | set #3 | 0070 states the same fact twice with two different ACs |
| 66 | 0070 #6 | Step 4a's enumeration of existing `Warning` messages is short by two |
| 66 | 0071 #4 | No implementation step moves the 21 + 6 implementations NFR-1(b) requires to move together |
| 66 | 0073 #4 | Both stated reasons for the public accessibility fail on inspection |
| 65 | 0072 #4 | The probe/resolve TOCTOU adoption introduces is dismissed as pre-existing |
| 65 | 0074 #5 | `ContainerRegistrationSnapshot` cannot supply three of the six rules' inputs |
| 65 | set #4 | FR-7's owning ADR is 0070, which does not touch handler pipelines |
| 65 | set #5 | "All eight interfaces the set breaks" undercounts by one — `IBrighterOptions` is a ninth |
| 64 | 0070 #7 | `## Context` states a set-wide invariant and breaks it two paragraphs later |
| 64 | 0071 #5 | 0070 names AC-51 as the handler criterion where 0071 and 0072 name AC-33 |
| 64 | 0073 #5 | "Structural rather than checked" does not establish the non-null invariant |
| 62 | 0070 #8 | Step 7a's framing sentence is falsified by its own bullet 7 |
| 62 | 0071 #6 | The ordering rule the ADR exists to place has no criterion and no test asserting order |
| 62 | 0072 #5 | Row 8's AC gap said to be recorded in *Negative*, which does not record it |
| 62 | 0074 #6 | "Six configurations are now expressible" contradicted by C-18 and by the Negative section |
| 62 | 0075 #3 | Malformed emphasis and a duplicated rejection in Alternative 3a |
| 62 | 0076 #3 | "Would throw during `BrighterHandlerBuilder`" — nothing is resolved during registration |
| 62 | gap #5 | Six constraints and out-of-scope items bearing on the design are cited by no ADR |
| 60 | 0070 #9 | "The four that create an artefact take the scope" contradicts the code block beneath it |
| 60 | 0071 #7 | 0071 is the only one of seven without the ADR 0067 `Terms` paragraph |
| 60 | 0071 #8 | "A reader counting signatures finds five, not six" — by its own preceding paragraph, four |
| 60 | 0072 #6 | Verbatim duplicated sentence and a malformed nested-bold span in the Scope paragraph |
| 60 | 0074 #7 | NFR-4 invoked for a claim it does not make, and absent from References |
| 60 | 0075 #4 | The sequence diagram shows a `loop` for bracket 1 but none for bracket 2 |
| 60 | set #6 | The 12-bullet ledger has one AC behind it, reaching four bullets |

## What the round proved clean — do not re-derive

Recorded so a later round does not spend effort re-establishing it.

**Mechanical set properties — all pass.** Seven files, all `Proposed` in frontmatter and body, all
seven in `.adr-list`. All 21 sibling-map pairs byte-identical modulo bolding, own row bolded and
marked *(this one)* in each, correct positional ordinal in each. Unifying sentence verbatim in all
seven, no paraphrase variants. Supersession statement in all seven. Heading skeletons identical in
wording, order and nesting — no drift finding from any reviewer. Escaped entities **0** in all
seven. `docs/adr/index.md` **byte-identical to a fresh regeneration**, `_99 ADRs indexed._`, and
`ls docs/adr/[0-9]*.md | wc -l` = 99. **All 15 mermaid diagrams render** — confirmed independently
by four reviewers. All 64 cited `.cs` files exist. All 21 cited ADR slugs exist with matching
status tags. Terminology clean: no use of "chain" for the pipeline concept, no use of "lifetime
scope" for the new concept. No authoring-conversation references, no ephemeral-state references, no
commit hashes, no review-round numbers anywhere in the set.

**AC sweep complete.** Union of `AC-\d+` across the seven ADRs = exactly AC-1 … AC-51, contiguous.
**No orphans. No out-of-range citations. AC-51 is cited** (0070 `:417`, 0071 `:8`, `:291`, `:334`,
`:411`). Per-ADR counts — 0070: 14, 0071: 8, 0072: 20, 0073: 12, 0074: 14, 0075: 11, 0076: 6.

**Requirement coverage.** No FR-1…FR-27 is unclaimed. No requirement is claimed exclusively by two
ADRs. The three deliberate splits — FR-13, FR-15, FR-17 — have agreeing reciprocal statements in
every direction (all six checked for FR-17, all three for FR-13). No clause of a multi-clause
requirement falls in a gap. All ten NFRs are cited by at least one ADR; none orphaned.

**0075's runtime reasoning holds — three independent confirmations this round.** The 0075 reviewer
ran fifteen probe groups and the set-level reviewer six more. Every runtime claim in 0075 came out
as stated except Alternative 5's async half (0075 #1). Specifically re-confirmed: an `async` method
is its own `ExecutionContext` boundary (`async Task` 0/5000 and `async void` 0/5000 leak) while the
plain-`void` twin does leak (5000/5000 and non-async `Task`-returning 5000/5000) — **the boundary is
the `async` keyword, not the return type**; the cross-body leak inside one `Parallel.ForEach` worker
is real (124,677/128,000 bodies, and 4,992/5,000 in the set-level probe); an unrestored body write
does **not** reach the caller (0/2000, with the inlined replica present in 2000/2000 runs); nothing
survives onto unrelated thread-pool work (0/4000 and 0/64); out-of-order, double and cross-flow
bracket disposal each behave exactly as the contract table states. **Do not "restore" any earlier
version of that paragraph.**

**0076's order independence survived a probe assault.** Eleven probe groups against a verbatim
transcription of `RegisterBrighterOptions` with all four entry points reconstructed: 4 paths × 2
orderings all resolved `JoinAmbient` (8/8); AC-45's third clause passes in both orderings (4/4);
descriptor reference-equality survives the snapshot because it copies the list; the `ServiceKey`
clause is load-bearing and correctly argued; `IOptions<T>.Value` is the mutated object while
`IOptionsMonitor`/`IOptionsSnapshot` are not; the singleton factory ran exactly once under 64
concurrent resolutions. *"I could not construct a path on which the opt-in is silently lost that the
ADR does not already name."*

**0070's blast-radius counts are exact.** Independently recounted with a multi-line
class-declaration scan that caught and corrected two false positives (body-less C# 12 declarations
whose buffer ran into the next class): **src 12, tests 70, total 82**, across 12 src files and 38
test files; 64 factory doubles across 37 files; 6 registry doubles across 3 files. Cross-checked by
an independent grep over base-list lines: 82 lines across 50 files.

**0071's blast-radius counts are exact.** `IAmAHandlerFactory` = **21** (5 in `src/`, 16 test
doubles); `IAmALifetime` = **7** (1 in `src/`, 6 doubles). `samples/` contains no implementation of
either.

**Other verified counts.** The release-note ledger is **12** bullets, attributed 0070 ×5, 0071 ×2,
0072 ×1, 0074 ×2, 0075 ×1, 0076 ×1, 0073 ×0 — and 0073 correctly declares its zero contribution.
Every break-bearing sibling points back at step 7a rather than opening its own entry; none is
missing and none contributes more than it claims. 0074's rule set is **six**, consistent across the
rule table, both diagrams, all seven sibling-map rows and ten prose mentions, decomposing as FR-22's
four + FR-24.3 + FR-17. Three errors / three warnings, matching AC-43. Nine new types in 0074, both
from the flowchart and from the touched table. FR-25 has **eleven** clauses. **125** files under
`tests/` register `IBrighterOptions` themselves. 37 test projects, **0** referencing
`Microsoft.AspNetCore.*`. 24 `src/` projects on the core TFMs. 21 `Use*` extension methods in
`src/`, every one on `IBrighterBuilder`. `IAmConsumerOptions` has 5 members and none of its 7
consumers downcasts. **0** `InternalsVisibleTo` attributes in the repository.

**Citation accuracy.** Several hundred `file:line` citations were opened against source across the
eight reviewers. Two failures found, both the same line: `ServiceProviderLifetimeScope.cs:520`
points at a brace (correct is `:521-522`), cited twice in 0070 and once in 0071. One range too
narrow: `ServiceCollectionTransformerResolvabilityProbe.cs:40-56` excludes the `Contains` at `:58`.
Everything else resolved to the member claimed.

---

## Findings

### ADR 0070 — `per-pipeline-di-scope-for-mapper-and-transform-factories`

14 findings, 9 at or above threshold. 0 Critical, 5 High, 8 Medium, 1 Low.

#### 1. `ServiceProviderPipelineScope` is made `public` on a justification ADR 0072 explicitly contradicts (Score: 85)

0070 makes a new DI-package type public — permanent public surface — and gives exactly two reasons.
The first is a factual claim about ADR 0072, and 0072 says the opposite in three separate places,
including rejecting that very design as *Alternative 2*.

**Evidence**: `0070:276` — "`ServiceProviderPipelineScope` is **`public` with an `internal`
constructor** — public because **ADR 0072 type-tests it across the package boundary** and NFR-7
makes a non-Microsoft container package a first-class implementer".

Against `0072:233` — "**The four container-backed transform factories and the handler factory
type-test for the interface**, never for a class: `if (ambient is IAmAServiceProviderScope src)`.
`ServiceProviderPipelineScope`'s borrowed construction path therefore stays **internal** to the DI
package, and its shape is free to change without breaking a third party."

`0072:330` (*Where each type is touched*) — "`ServiceProviderPipelineScope` | an **internal**
borrowed construction path with non-owning disposal".

`0072:489` (*Alternatives*, rejected) — "**2. A concrete-class hand-off: a public borrowed
constructor on `ServiceProviderPipelineScope`** … **Rejected on two counts.** It freezes
`ServiceProviderPipelineScope`'s public shape forever … And it does not generalise: **a package over
Autofac cannot construct a Microsoft-container class**, so NFR-7's 'implementable over another
container' would be met only by the class not being involved".

That last sentence also falsifies 0070's second reason: NFR-7 cannot be served by a
Microsoft-container-specific class with an `internal` constructor. Nothing in the set needs the type
public — 0070 step 6 (`:366`) and 0071 (`:235`) both type-test it *inside* the DI package, and 0073
never names it.

**Recommendation**: Make it `internal sealed` and delete the two-part justification at `:276`, or,
if it must be public, replace the justification with one 0072 agrees with. Either way the sentence
naming 0072's type test has to go — it is false as written.

#### 2. The third drain step is asserted to be skipped on the finalizer path, but no mechanism can skip it (Score: 80)

Step 5 says the new third drain step runs in a `finally` inside `TransformPipelineDrain.Drain`, and
then states it does not run on the finalizer path. Both the explicit `Dispose()` and the finalizer
funnel through the *same* private method into the *same* `Drain` overload, with nothing
distinguishing them. Two developers will implement this differently: one adds a parameter/overload,
one does not and ships a finalizer-thread stall the ADR itself calls out as unacceptable.

**Evidence**: `0070:358` — "**On the finalizer path the third step is skipped**: a pipeline reaching
finalization is unreachable … and `DisposeScope` blocks on `GetAwaiter().GetResult()` … so running
it there would block the finalizer thread and stall all finalization for the process."

`src/Paramore.Brighter/TransformPipeline.cs:37-72`:
```
public void Dispose()      { try { ReleaseUnmanagedResources(); } finally { GC.SuppressFinalize(this); } }
~TransformPipeline()       { try { ReleaseUnmanagedResources(); } catch { } }
private void ReleaseUnmanagedResources()
{
    if (Interlocked.Exchange(ref _released, 1) != 0) return;
    TransformPipelineDrain.Drain(disposeScope: …, releaseMapper: …);
}
```
`TransformPipelineAsync.cs:96-118` is identical in shape — `~TransformPipelineAsync()` also calls
`ReleaseUnmanagedResources()` → the *synchronous* `TransformPipelineDrain.Drain`. Neither *Where
each type is touched* (`0070:261`) nor step 5 gives `Drain`/`DrainAsync` any parameter, flag or
overload by which the finalizer could opt out.

**Recommendation**: Specify the mechanism. The obvious one is
`Drain(Action disposeScope, Action releaseMapper, Action? releaseScope)` with
`ReleaseUnmanagedResources` split into a disposing/finalizing pair (the standard
`Dispose(bool disposing)` shape), passing `null` from the finalizer. Name it in step 5 and in the
`TransformPipelineDrain` row of *Where each type is touched*.

#### 3. The finalizer skip is licensed by a claim about MS DI that a probe falsifies (Score: 76)

The reason given for skipping the scope release on the finalizer path is that the container reclaims
the scope itself. Microsoft's container has no such behaviour: `ServiceProviderEngineScope` declares
no finalizer, and an abandoned scope disposes *nothing* it tracked. So the skip does not "let the
container reclaim it" — it silently abandons every container-`Scoped` `IDisposable` the pipeline
resolved.

**Evidence**: `0070:358` — "a pipeline reaching finalization is unreachable, its DI scope is
unreachable with it and **the container's own finalization reclaims it**".

Probe (net10.0 + `Microsoft.Extensions.DependencyInjection`), a scoped `IDisposable` resolved into a
scope that is then abandoned, followed by three forced Gen-2 collections with
`WaitForPendingFinalizers`:

```
A. scope runtime type            : Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceProviderEngineScope
A. scope is IAsyncDisposable     : True          <- 0070's other claim, confirmed
B. abandoned scope -> Tracked.Dispose() calls  : 0
B. abandoned scope -> Tracked finalizer calls  : 1
B. scope type has a finalizer                  : False
B. ServiceProvider type has a finalizer        : False
C. explicitly disposed scope -> Dispose calls  : 1   <- control
D. scope.Dispose() surfaced : InvalidOperationException  <- step 4b's premise, confirmed
```

Zero `Dispose()` calls. The object's memory was reclaimed (its own finalizer ran once), but nothing
the container tracked was disposed — which for a `DbContext` or a pooled connection is precisely the
leak FR-6 and NFR-5 exist to prevent.

**Recommendation**: Replace the clause. The honest justification is the one already in the sentence
and sufficient on its own — running a blocking `GetAwaiter().GetResult()` on the finalizer thread is
unacceptable, and ADR 0068 makes the finalizer a best-effort net — plus an explicit statement that
scoped disposables reached only by the finalizer are *not* reclaimed.

#### 4. Step 9a cites AC-51 where every other authority — including this ADR twice — says AC-33 (Score: 72)

The `9a` verification table names the AC that covers the handler-family instance of FR-13's
disposal-failure clause. It names AC-51. AC-51 is a different clause (handler *release*); AC-33 is
the scope-*disposal* one, as `## Context` says twice, as 0071 says, and as FR-13 itself says.

**Evidence**: `0070:417` — "**design-owed test** (FR-13) | … AC-6 covers the *failed-build* case and
**AC-51** the handler one; this is the transform instance of FR-13's disposal clause".

Against `0070:34` — "AC-6 covers the *failed-build* case and **AC-33** the handler one, so the
completed-transform-pipeline case rests on FR-13 alone"; and `0070:340` — "ADR 0071 puts the same
rule, and a member of the same name, on the handler family's `HandlerLifetimeScope.Log`, where
**AC-33** guards it."

`requirements.md:230` (FR-13, disposal-failure clause) — "**Discharged by AC-33.**"
`requirements.md:538` (AC-51) — "the sibling case to AC-33, **which covers the *scope disposal* half**
of FR-13's teardown rule **while this covers the *handler release* half**".
`0071:291` — "**AC-33 is that rule's regression guard for the scope-disposal half and AC-51 for the
handler-release half.**"

Secondary: `0070:502` (References) lists "… AC-24, AC-30, AC-33" and omits AC-51 entirely, so the
ADR cites in step 9a an AC it does not carry in its reference list.

**Recommendation**: Change `AC-51` to `AC-33` at `:417`. If AC-51 is to be mentioned at all, add it
to the References list at `:502` with its actual subject.

#### 5. Step 7a's AC-24 defence does not add up: "all six of NFR-1's list, five by their own signatures and two by their base" is seven (Score: 70)

The passage exists specifically to make the arithmetic work — "stated exactly because the arithmetic
does not look as though it does" — and the arithmetic it states is wrong. The "five" silently
includes `IAmAHandlerFactory`, which is not on NFR-1's list at all.

**Evidence**: NFR-1's withdrawal list (`requirements.md:352`) names exactly six:
`IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`,
`IAmAMessageTransformerFactoryAsync`, `IAmAHandlerFactorySync`, `IAmAHandlerFactoryAsync`.

`0070:391` — "Under this design that is **five**: the four mapper and transformer factories, plus
`IAmAHandlerFactory`."
`0070:393` — "The entry therefore covers **all six of NFR-1's list**, **five by their own
signatures** and **two by their base**, and states more besides."

Five + two = seven over a six-item list. Of NFR-1's six, **four** change by their own signatures
(the mapper/transformer factories) and **two** by their base. `IAmAHandlerFactory` — verified a bare
marker, `src/Paramore.Brighter/IAmAHandlerFactory.cs:7`: `public interface IAmAHandlerFactory;` — is
a *seventh* interface outside NFR-1's list.

Relatedly, the same bullet claims "**Eight interfaces break across the two ADRs, not six**" while
`:393` argues the two handler twins also stop compiling for out-of-repo implementers — by that
reasoning ten interfaces break, eight by signature and two by base.

**Recommendation**: Rewrite as "four of NFR-1's six by their own signatures and two by their base,
plus `IAmAHandlerFactory` and `IAmALifetime`, neither of which is on that list". Then state plainly
that AC-24's "six factory interfaces whose signature changed" has no referent of size six under this
design, and either amend AC-24 or record the mismatch as a known requirements defect.

#### 6. Step 4a's enumeration of the existing `Warning` messages is short by two — in the passage that insists enumerations here must be complete (Score: 66)

Step 4a asserts a closed count of six pre-existing `Warning`-level messages in the release/disposal
family and rests an argument on the closure. A grep of every `LoggerMessage(LogLevel.Warning` in the
named files finds eight. The two it misses report *transform release failures during pipeline
cleanup* — exactly the category the ADR says it is enumerating.

**Evidence**: `0070:331` — "**Six** messages exist and all log at `Warning`. **Five** are about
releasing a **mapper or a transform**, not about disposing a DI scope —
`FailedToCleanUpAfterFailedBuild` (`TransformPipelineBuilder.cs:409`,
`TransformPipelineBuilderAsync.cs:318`) and `FailedToReleasePipeline`
(`OutboxProducerMediator.cs:1448`, `Reactor.cs:637`, `Proactor.cs:651`)." Followed at `:333` by "Any
enumeration of this family that stops at five is incomplete in the one place that matters."

Missed, both `LogLevel.Warning`, both in the cleanup path this ADR edits:
- `TransformPipelineBuilder.cs:411-412` — `[LoggerMessage(LogLevel.Warning, "Failed to release a
  transform while cleaning up a partially-built pipeline; releasing the remaining transforms…")]
  FailedToReleaseTransform`
- `TransformPipelineBuilderAsync.cs:320-321` — the same member.

They are emitted by `ReleaseTransforms` (`TransformPipelineBuilder.cs:221-222`), which step 4 cites
by line for a different purpose (`:329` cites `:215-223`), so the ADR reads that code and still omits
the message. The correct statement is **eight**: seven about a mapper or transform release, one
about disposing a DI scope. The decision is unaffected; the count is wrong on the page.

**Recommendation**: Change "Six" to "Eight" and "Five" to "Seven", and add `FailedToReleaseTransform`
to the enumeration at `:331`.

#### 7. `## Context` states a set-wide invariant and then breaks it two paragraphs later (Score: 64)

**Evidence**: `0070:32` — "**Every FR has exactly one owning ADR, named in that ADR's `Scope`, so a
coverage audit lands on the mechanism that makes the requirement true.**" Against `0070:34` — "**So
FR-13 divides by family rather than by clause**, and no ADR claims the whole of it." `0071:30`
repeats the second sentence verbatim, so this is the set's settled position and `:32` is wrong. A
coverage audit run against the stated rule reports FR-13 as unowned; run against actual practice it
finds FR-13 owned in halves by 0070 and 0071 with a third piece routed to FR-12 in 0072.

**Recommendation**: Weaken `:32` to the actual rule — every FR is discharged by a named mechanism in
one or more ADRs' `Scope`, and where a requirement divides, each ADR names its part and its
siblings' — so the audit procedure still works.

#### 8. Step 7a's framing sentence is falsified by its own bullet 7 (Score: 62)

**Evidence**: `0070:383` — "**This ADR is where the first four originate; the rest arrive with its
siblings**". Counting top-level bullets `:385`–`:400`: 1 this ADR, 2 this ADR, 3 this ADR, 4 this
ADR, 5 ADR 0071, 6 ADR 0071, **7 this ADR** (`:395`, the disposal-failure surfacing), 8 ADR 0072,
9 ADR 0075, 10 ADR 0076, 11 ADR 0074, 12 ADR 0074. **Twelve bullets; five "this ADR".**

**Recommendation**: Either move bullet 7 up beside 1–4 and say "the first five", or change the
sentence to "five of these originate here — bullets 1–4 and 7".

#### 9. "The four that create an artefact take the scope" contradicts the code block directly beneath it, the touched table, and the diagram (Score: 60)

**Evidence**: `0070:207` — "Six interfaces change. Each gains `CreatePipelineScope()`; **the four
that create an artefact also take the scope on the call that creates it**." Contradicted by the code
block at `:225` (`Get<T>(IAmAScope? scope = null)` on `IAmAMessageMapperRegistry`), by `:230` ("…
**and `IAmAMessageMapperRegistryAsync`** change the same way — … **`GetAsync<T>(IAmAScope?)`**"), by
the contract table at `:238`, by *Where each type is touched* `:254`, and by the diagram at `:148`
("The six changed interfaces … **each** gains CreatePipelineScope, and takes the scope on the call
that creates").

**Recommendation**: `:207` should read "all six take the scope on the call that creates an artefact —
`Create` on the four factories, `Get<T>`/`GetAsync<T>` on the two registries".

#### 10. AC-5 is cited as covering a failure it does not exercise (Score: 58)

**Evidence**: `0070:236` — "A failure to **create the container scope** may throw and is an ordinary
build failure: the builder's existing `catch` turns it into `ConfigurationException` carrying it as
the inner exception **(AC-5)**." Repeated at `:299`. `requirements.md:441-445` (AC-5) — **Given** "a
mapper whose constructor depends on an unregistered service, **so pipeline build throws**" … **Then**
"each call throws `ConfigurationException` whose inner exception is the original resolution failure,
**And** the count of Brighter-created scopes begun equals the count released." AC-5's second clause
requires a scope to have been *successfully created*, the opposite of the failure the row describes.

**Recommendation**: Either add a criterion whose Given makes `CreatePipelineScope()` itself throw, or
drop the `(AC-5)` attribution and state that this failure mode has no criterion, as the ADR does
honestly elsewhere for FR-13.

#### 11. 0070 carries the whole set's release-note ledger, and it has already drifted (Score: 58)

Step 7a enumerates twelve breaking changes, eight of which belong to five other ADRs. The stated
reason is that `release_notes.md` should have one entry — but that is a property of the release
notes, not a reason for one ADR to hold the canonical list of another's breaks. Findings 5 and 8 are
both drift inside this ledger, which is the concrete cost.

**Evidence**: `0070:383`; bullets at `:389`, `:394` (0071), `:396` (0072), `:397` (0075), `:398`
(0076), `:399`, `:400` (0074). Every sibling now points back: `0071:351`, `0074:392`, `0075:232`,
`0076:401`. So any change to a sibling's break requires an edit to 0070.

**Recommendation**: Keep step 7a as a pointer — "one `release_notes.md` entry, whose contents are the
union of the breaks each ADR states in its own *Consequences*" — and move each sibling's bullet into
the sibling that owns it. If the enumeration must stay, add an explicit note that it is a mirror and
the owning ADR is authoritative.

#### 12. The stated migration for implementers omits that the default value must be repeated (CS7036) (Score: 55)

**Evidence**: `0070:240` — "`IAmAScope? scope = null` keeps every existing *call site* compiling … It
does nothing for *implementers*, **who must still declare the parameter**". `0070:297` — "the same
**two-line treatment**: return `null`, ignore the parameter."

Probe: an implementation declaring `Create(Type t, IAmAScope? scope)` without the default compiles,
but a call through the concrete type fails —
`error CS7036: There is no argument given that corresponds to the required parameter 'scope'`.
Calls through the interface bind fine; calls through the class do not. This matters here:
`ServiceCollectionExtensions.MessageMapperRegistry(provider)` (`:802`) returns the **concrete**
`MessageMapperRegistry`, and there are 369 `new MessageMapperRegistry(` sites across `src` and
`tests`. The release-note text at `:443` gets it right; the implementor-facing sentences do not.

**Recommendation**: At `:240` add "and must repeat the `= null`, or every call site holding the
concrete type fails with CS7036." At `:297` make the two-line treatment explicit.

#### 13. "The scope would leak on exactly the paths FR-6 names" has an ambiguous antecedent and reads as a violation of FR-6 (Score: 52)

**Evidence**: `0070:358`, in order: "A third step merely appended after those two would therefore
never run on any failure path … **On the finalizer path the third step is skipped**: [three clauses]
… ADR 0068's rule … is what licenses the skip. **The scope would leak on exactly the paths FR-6
names, one `IServiceScope` per failure, until process exit.**" Read with its nearest antecedent this
says the accepted design leaks a scope per failure, contradicting FR-6 (`requirements.md:190`).

**Recommendation**: Move the leak sentence up so it sits immediately after "would therefore never run
on any failure path", and make its subject explicit: "*that* shape would leak…".

#### 14. Two `The forces` bullets carry two `file:line` citations each (Score: 35)

**Evidence**: `.agent_instructions/documentation.md:105-106` — "At most one per forces or Consequences
bullet." `0070:88` — "(`ServiceCollectionExtensions.cs:945`, `:957`)". `0070:93` — "(`Proactor.cs:239`
then `:241`)".

**Recommendation**: Drop the second line number from each; the pairs are already in *Implementation
Approach* steps 6 and 8.

---

### ADR 0071 — `pipeline-scope-handle-for-handler-pipelines`

16 findings, 8 at or above threshold. 0 Critical, 3 High, 8 Medium, 5 Low.

#### 1. The ADR contradicts itself on which AC guards the handler-release rule (Score: 85)

Step 2 goes out of its way to say **AC-7 is not** the criterion for the handler-`Release`-throws
rule, and that AC-51 is. A *Consequences* bullet then says the opposite, naming AC-7 for exactly
that clause and omitting AC-51 entirely. Two developers reading the two paragraphs would write
different tests — and one would write a test that cannot pass, because AC-7's **Given** contains no
throwing `Release`.

**Evidence**: `0071:291` — "**AC-33 is that rule's regression guard for the scope-disposal half and
AC-51 for the handler-release half.** **AC-7 is not the second of those**, and the distinction is
worth stating because the two read alike: AC-7's Given has a throwing **handler**, not a throwing
`Release` … AC-51 is the criterion written for this rule."

Against `0071:360` — "Both of that requirement's clauses are its own for this family (step 2, AC-33
for the disposal-failure half, **AC-7 for the handler-release rule it extends to**)".

`requirements.md:452-455` gives AC-7 the Given "a handler … whose `HandleAsync` throws
`InvalidOperationException`"; `requirements.md:538-546` gives AC-51 "a handler factory whose
`Release` throws `InvalidOperationException`". The touched table row at `:256` compounds it, citing
only "(FR-13, AC-33)" for a pair of log members of which one (`FailedToReleaseHandler`) is AC-51's.

**Recommendation**: Change `:360` to "AC-33 for the disposal-failure half, AC-51 for the
handler-release rule it extends to", and add AC-51 to the `HandlerLifetimeScope.Log` row at `:256`
beside AC-33.

#### 2. The confinement argument for NFR-4 rests on a false claim, and the eight-method enumeration is short by four (Score: 75)

The NFR-4 forces bullet justifies deleting the `ConcurrentDictionary`'s atomicity by asserting the
`HandlerLifetimeScope` is confined to the builder. It is not.
`IHandleRequests.AddToLifetime(IAmALifetime)` is a **public** member of the handler interface,
`PipelineBuilder` calls it on every built pipeline, and `RequestHandler.AddToLifetime` forwards it
down the whole decorator chain — so after this ADR every handler and every decorator in the
pipeline, including user-written ones, holds an object exposing `PipelineScope` and can dispose the
DI scope out from under the pipeline. That is a widening of public surface the ADR neither states nor
prices, and `IHandleRequests`/`IHandleRequestsAsync`/`RequestHandler`/`RequestHandlerAsync` appear
nowhere in the "Unchanged, and named so the omission is not read as an oversight" list at `:264`.

**Evidence**: `0071:106` — "one `HandlerLifetimeScope` is constructed per subscriber and **never
leaves the `PipelineBuilder` that made it**"; `:270` — "so **eight** methods carry an `IAmALifetime`
in all"; `:381` (Risks) — "all eight methods that carry it are listed in *Technology Choices*".

Source: `IHandleRequests.cs:71` `void AddToLifetime(IAmALifetime instanceScope);`;
`IHandleRequestsAsync.cs:82` (same); `RequestHandler.cs:83-86` (`public void
AddToLifetime(IAmALifetime instanceScope)` … `_successor?.AddToLifetime(instanceScope);`);
`RequestHandlerAsync.cs:97-100`; called at `PipelineBuilder.cs:195` and `:241`. An exhaustive grep of
`src/` for methods declaring an `IAmALifetime` parameter returns 32 declarations; the eight the ADR
names are a subset of the resolution path only.

**Recommendation**: Replace "never leaves the `PipelineBuilder`" with the true statement — the object
is threaded to every handler through `AddToLifetime` and to the factory on every `Create`/`Release`,
and what makes the design safe is that `PipelineScope` is fixed at construction and disposal is
issued from one place. Either add `AddToLifetime` to the "eight" or drop the "in all". Add the four
types to the unchanged list, and state in *Consequences* that a handler can now reach the pipeline's
`IAmAScope`.

#### 3. "The six release sites" — the count is three, and ADR 0070 says three (Score: 74)

**Evidence**: `0071:342` — "On the transform side the drain composes and throws, and **the six
release sites** catch it and log `FailedToReleasePipeline` at `Warning`".

Against `0070:270` — "**the three pipeline-release sites that swallow today — `OutboxProducerMediator`
(`:1448`), `Reactor` (`:637`) and `Proactor` (`:651`)**"; and `0070:331` — "**Six** messages exist …
`FailedToCleanUpAfterFailedBuild` (×2) and `FailedToReleasePipeline` (×3)."

Source recount: `grep -rn "FailedToReleasePipeline" src/` returns exactly three `[LoggerMessage]`
declarations — `Reactor.cs:638`, `Proactor.cs:652`, `OutboxProducerMediator.cs:1449` — with four call
sites. Nothing in `src/` gives six.

**Recommendation**: Change "the six release sites" to "the three release sites".

#### 4. No implementation step moves the 21 + 6 implementations that NFR-1(b) requires to move together (Score: 66)

The blast radius is stated twice in prose (a forces bullet and a *Negative* bullet) but never appears
as work. *Implementation Approach* step 1 is one sentence — "Add the two members" — and the touched
table lists only the five `src/` factories. Sixteen test doubles for `IAmAHandlerFactory` and six for
`IAmALifetime` have no step, no file count and no instruction, yet NFR-1(b) makes moving them
non-optional and the solution will not compile without them. The sibling facing the same problem does
state it as a step, with a file count.

**Evidence**: `0071:280` — "**1. Core.** Add the two members." — and `:249-262`, the touched table.
`requirements.md:352` NFR-1(b): "**Every implementation in this repository moves together** … **and
every test double** — so the solution compiles with no partial adopters." Contrast `0070:297`: "then
**move every implementation in the repository in the same change**: 12 classes in `src/` … and 70
test doubles … **38 test files in all**." The recount confirms 0071's numbers are right — it is the
*step*, not the arithmetic, that is missing.

**Recommendation**: Add a step naming the mechanical move: 21 `IAmAHandlerFactory` implementations
(5 in `src/`, 16 doubles across 15 test files) return `null` from `CreatePipelineScope()`; 7
`IAmALifetime` implementations (1 in `src/`, 6 doubles in 6 files) add `PipelineScope => null`. Give
the test-file count as 0070 does.

#### 5. ADR 0070 names AC-51 as the handler criterion for FR-13's disposal clause; 0071 and 0072 both name AC-33 (Score: 64)

**Evidence**: `0070:417` — "AC-6 covers the *failed-build* case and **AC-51 the handler one**".
Against `0071:291` and `0072:31` — "that requirement's disposal-failure clause is ADR 0070's for
transform pipelines and ADR 0071's for handler pipelines, **where AC-33 guards it**."
`requirements.md:229-231` settles it: FR-13's disposal-failure clause says "Discharged by **AC-33**."

**Recommendation**: Fix `0070:417`. 0071 should not have to carry a correction paragraph for a
sibling's misattribution.

#### 6. The ordering rule the ADR exists to place has no acceptance criterion and no test that asserts the order (Score: 62)

"Releases every tracked handler, **then** disposes the handle. Never the other way round" is the
responsibility that justifies putting the logic in `HandlerLifetimeScope` rather than
`PipelineBuilder` (alternative 5), and the rationale is concrete: a factory whose `Release` still has
work to do must not be resolving against a dead scope. Nothing asserts it. Step 6's second required
test asserts that both happen, not that they happen in that order; AC-51's third branch likewise. The
transform family got an explicit criterion for its ordering (AC-21); the handler family gets none.

**Evidence**: `0071:184` (roles table) and `:245-247`. The required test at `:330` — "must still
release the other two, still clear both tracking lists, still dispose the handle, and record exactly
one `LogLevel.Error`" — has no ordering clause. `requirements.md:544` (AC-51 third branch) likewise.
Contrast `0070:414`, where AC-21's row reads "The transform pipeline's handle is disposed in its own
drain, **before the handler pipeline is built**".

**Recommendation**: Add an ordering assertion to step 6's second required test — a recording factory
whose `Release` records a tick, and a handle whose `Dispose()` records one, with the handle's tick
last.

#### 7. 0071 is the only one of the seven without the ADR 0067 `Terms` paragraph — and it is the ADR that uses "lifetime scope" most (Score: 60)

**Evidence**: `grep -c "0067's \`Terms\` block"` across the set: 0070 → 1, **0071 → 0**, 0072 → 1,
0073 → 2, 0074 → 1, 0075 → 1, 0076 → 1. `0071:50-52` goes straight from the unifying sentence to
`### How a handler pipeline reaches its DI scope today`. This is the ADR whose text carries
`HandlerLifetimeScope`, `ServiceProviderLifetimeScope`, `IAmALifetime`, `TransformLifetimeScope` and
`IAmAScope` in the same paragraphs, and whose own *Negative* bullet says "`IAmALifetime` now holds
something that is not a handler … Mitigated only by documentation."

**Recommendation**: Add the paragraph in the sibling wording, and say what this ADR does about
"lifetime scope" — here uniquely it *keeps* the term, because `HandlerLifetimeScope` is pre-existing.

#### 8. "A reader counting signatures against NFR-1's list finds five, not six" — by the ADR's own preceding paragraph the answer is four (Score: 60)

**Evidence**: `0071:353` — "NFR-1's withdrawn signature freeze names exactly six interfaces, and
**neither `IAmAHandlerFactory` … nor `IAmALifetime` is among them**"; then `:355` — "Alternative 6
puts `CreatePipelineScope()` on the shared base rather than on each twin, so **those two twins' own
signatures do not change**, and a reader counting signatures against NFR-1's list **finds five, not
six**." Six minus the two twins is four. (`0070:391` has the same slip in the other direction.)

**Recommendation**: "…finds **four**, not six — the two mapper and two transformer factories; the
fifth changed signature, `IAmAHandlerFactory`, is not on that list at all."

#### 9. The Scope paragraph promises the release point is unchanged; the rest of the ADR says it moves (Score: 56)

**Evidence**: `0071:32` — "It does not change **when** a handler pipeline has a DI scope, **which**
lifetimes get one, or **when** it is released." Against `:101` — "the handle is disposed after the
handlers are released" — and `:146`. Source: `HandlerLifetimeScope.cs:74-93` shows the sync loop,
then the async loop, then the two `Clear()` calls; `ServiceProviderHandlerFactory.cs:133-137` shows
`ReleaseLifetimeScope` disposing on the first `TryRemove` that succeeds. Today the DI scope is
disposed by the *first* `Release` inside the sync loop; afterwards it is disposed after both loops.
Step 5's table prints "**No**" in its Changed? column for reclamation.

**Recommendation**: Qualify to "when it is released **relative to the enclosing
`PipelineBuilder.Dispose()`**". Add a footnote to step 5's table.

#### 10. FR-5 is load-bearing for step 2 and is in the frontmatter, but is absent from the References requirement list (Score: 52)

**Evidence**: `0071:8` (summary) — "(FR-5, FR-6, FR-13, AC-7, AC-33, AC-51)"; `:289` — "**FR-5
requires that 'a release failure must not mask it'**". `:411` — "Requirements: … — **FR-6, FR-7,
FR-13**, NFR-1, … C-1, C-2, C-6, D0, D2, D10; AC-7, AC-9, AC-14, AC-24, AC-33, AC-51." FR-12 and C-5
are also missing.

**Recommendation**: Add FR-5 and C-5 to the primary list and FR-12 to the deferred list.

#### 11. The `Where the pieces live` flowchart routes the cross-boundary "implements" edge so it reads as `HandlerLifetimeScope` implementing `IAmAHandlerFactory` (Score: 52)

**Evidence**: `0071:150-174`, rendered to PNG at 1600px and inspected. The `sphf -- "implements" -->
factory` edge is routed from the right-hand subgraph across and behind the `HandlerLifetimeScope`
node, and mermaid places its label immediately to the right of that node. Three edges carry the same
label. The prose under the diagram is correct but is doing work the picture should do.

**Recommendation**: Give the two cross-boundary edges distinct labels, or move `IAmAHandlerFactory`
below `HandlerLifetimeScope`.

#### 12. The AC-46 / FR-27.1 re-reading is recorded as implementor guidance where the comparable AC-14 re-reading is recorded as an amendment (Score: 48)

**Evidence**: `0071:367` — "That designation now has to attach to the duplicated handle-path pair as
well, **which is an amendment to AC-14**, not merely extra test coverage." `:232` — "**FR-27.1's
'takes no pipeline scope' is not asserted over this property, and AC-46's 'no pipeline scope taken'
must not be tested by its nullness.**" The reading is defensible and 0072 states the identical
reconciliation at `:102` and `:433`, so this is a consistency question, not a contradiction.

**Recommendation**: Either record it symmetrically as an amendment in effect, or drop the amendment
framing from the AC-14 paragraph so the two are treated alike.

#### 13. Two *Negative* bullets state the same observable break twice (Score: 45)

**Evidence**: `0071:370` and `:372` describe the throwing-`Release` change in near-identical terms.

**Recommendation**: Merge. Keep `:372`'s framing, which adds the operator cost.

#### 14. "A latent leak closes" is unqualified but is true only on the handle path (Score: 45)

**Evidence**: `0071:346` — "**Under this ADR `HandlerLifetimeScope.Dispose()` disposes the handle
unconditionally.**" Against `:237` — "On that path the leak this ADR closes is **not** closed".
Source: `ServiceProviderHandlerFactory.cs:129` runs inside `Create` before resolution; `:135` runs
only from `Release`; `PipelineBuilder.cs:192-193` throws before any `Add`.

**Recommendation**: Add "on the handle path" to `:346`, and point at the *Negative* bullet at `:366`.

#### 15. `ServiceProviderLifetimeScope.cs:520` points at a brace, not at `FailedToDisposeScope` (Score: 40)

**Evidence**: `0071:256`. Source: `:519` `private static partial class Log`, `:520` `{`, `:521` the
`[LoggerMessage(LogLevel.Warning, …)]` attribute, `:522` the declaration. The companion citation
`:462-501` is exact. 0070 carries the same `:520` at `:333` and `:395`.

**Recommendation**: `:521-522`. Fix in both ADRs.

#### 16. "The only one that is substantially structural" sits awkwardly against ADR 0072's characterisation of the first two (Score: 38)

**Evidence**: `0071:38` — "This is the second, and **the only one that is substantially structural**".
Against `0072:42` — "This is the third; it is where the feature starts, **the first two having only
closed defects**." 0071 spends a *Negative* bullet insisting it is not observationally inert.

**Recommendation**: Align. "The first two closed defects and converged the two families onto one
seam" would fit both.

---

### ADR 0072 — `ambient-scope-adoption-seam`

13 findings, 6 at or above threshold. 1 Critical, 2 High, 7 Medium, 3 Low.

#### 1. The `ScopedArtefactCache` "decline at the probe" is not deliverable by the probe this ADR specifies, and the ADR says so itself two sections later (Score: 92)

`#### ScopedArtefactCache` states that a borrowed provider which cannot supply a `ScopedArtefactCache`
is declined **at the probe**. `Implementation Approach` step 2 then defines the probe exhaustively —
and it contains no such test — and a further paragraph states flatly that the probe *cannot*
discriminate the ambient's container. Three passages of one ADR give three different answers, and a
.NET probe confirms the specified probe passes a container that supplies no cache.

The same paragraph also describes two mutually exclusive outcomes in consecutive sentences. If the
borrow is **declined**, the pipeline creates and owns a scope, so dependency sharing — "the headline
of adoption" — is exactly what is lost.

**Evidence**: `0072:289` — "Where a borrowed provider cannot supply a `ScopedArtefactCache` … the
handle **declines the borrow at the probe** rather than falling back. Dependency sharing, which is
the headline of adoption, is unaffected; artefact identity reverts to per pipeline. … a provider that
cannot supply one does not pass the probe."

`:404` — "It reads `Services`, then resolves `IServiceScopeFactory` from it … **Three outcomes are a
failed probe**: a `null` `Services`, a `null` `IServiceScopeFactory`, and any exception thrown either
by reading `Services` or by the resolution". No fourth outcome.

`:410` — "Neither asks which container built it, **and neither could**: `System.IServiceProvider` is
exactly the interface every container's Microsoft-DI adapter exposes."

Probe (net9.0, `Microsoft.Extensions.DependencyInjection` 9.0.0): a scope taken from a provider built
from an **empty** `ServiceCollection` returns a non-null `IServiceScopeFactory` (probe passes) and
`null` for a type that container never registered. So the row-9 decline never fires and
`ServiceProviderLifetimeScope`'s `Scoped` path resolves `null` for its cache with no specified
behaviour.

**Recommendation**: Pick one and state it once. Either (a) add a fourth failed-probe outcome —
`Services.GetService(typeof(ScopedArtefactCache)) is null` — to step 2's enumeration, delete "and
neither could" from `:410`, and delete the "Dependency sharing … is unaffected" sentence; or (b) keep
the probe as specified and say what `ServiceProviderLifetimeScope` does when the borrowed scope
yields no cache. Whichever is chosen, `:289`, `:404` and `:410` must agree.

#### 2. The mandated eviction API does not exist on `netstandard2.0`, one of the DI package's four target frameworks (Score: 85)

The `ScopedArtefactCache` contract makes `TryRemove(KeyValuePair<Type, Lazy<object?>>)` a bolded
*must*, and the reasoning is correct — a key-only removal really can delete a healthy replacement.
But that overload was added in .NET Core 2.0 and is absent from `netstandard2.0`, which the DI
package targets. An implementor following the ADR literally gets a compile error on one TFM; the
obvious "fix" is the key-only removal the ADR says produces two `Scoped` artefacts in one borrowed
request scope, violating FR-16(a)/AC-17.

**Evidence**: `0072:295` — "**The removal must take the observed pair, not the key** —
`TryRemove(KeyValuePair<Type, Lazy<object?>>)`, not `TryRemove(type, out _)`."

`src/Directory.Build.props:43` — `<BrighterTargetFrameworks>netstandard2.0;net8.0;net9.0;net10.0</…>`,
used by `Paramore.Brighter.Extensions.DependencyInjection.csproj:5`.

Compile probe, `netstandard2.0` class library: `error CS7036: There is no argument given that
corresponds to the required parameter 'value' of
'ConcurrentDictionary<Type, Lazy<object?>>.TryRemove(Type, out Lazy<object?>)'`.

The same file already documents an equivalent gap: `ServiceProviderLifetimeScope.cs:507-508` —
"`ReferenceEqualityComparer` from the BCL is not available on `netstandard2.0`, so this supplies the
same behaviour."

Runtime probe confirms both halves of the reasoning: key-only `TryRemove` removed the healthy
replacement; `((ICollection<KeyValuePair<Type,Lazy<object>>>)dict).Remove(pair)` returned `false` and
left the healthy entry in place. That cast **is** available on `netstandard2.0`.

**Recommendation**: Specify the removal as
`((ICollection<KeyValuePair<Type, Lazy<object?>>>)_cache).Remove(new KeyValuePair<…>(type, observedLazy))`,
noting that the public `TryRemove(KeyValuePair<,>)` overload is unavailable on `netstandard2.0` and
that the explicit-interface `Remove` has identical pair-matching semantics.

#### 3. `AmbientScopeSourceException`'s never-null invariant is asserted rather than guaranteed, and the reason given for not guarding it is contradicted in the next paragraph (Score: 76)

The ADR calls the invariant "load-bearing" and "guaranteed rather than incidental", and licenses
`e.InnerException!` at six sites on it — then declines to validate on the grounds that there is
exactly one caller. Two paragraphs later it obliges third-party container packages to construct the
type. Both cannot be true. Since the type is `public` in `Paramore.Brighter`, which targets
`netstandard2.0` (nullable-oblivious consumers), a `null` argument is reachable and produces a
`NullReferenceException` at six rethrow sites inside the pipeline builders.

**Evidence**: `0072:272` — "none — the constructor does not validate, because **the only caller is the
factory that just caught the inner exception**". `:274` — "**Any factory that asks a provider for an
ambient — including one in a third-party container package (NFR-7) — must wrap a throw from that ask
in this type**"; and "it is what licenses `e.InnerException!` at the six sites that unwrap it, so a
provider that constructs one without an inner exception breaks those call sites". `:344` — "Its
contract is one line and is **guaranteed** rather than incidental".

**Recommendation**: Make it structural: `AmbientScopeSourceException(Exception inner)` throws
`ArgumentNullException` on `null`, and the contract's Error-conditions cell says so. Then delete "the
only caller is the factory that just caught the inner exception", which is false under NFR-7 either
way.

#### 4. The probe/resolve TOCTOU that adoption introduces is not addressed, and the one row that touches it dismisses it as pre-existing (Score: 65)

`AmbientScopeProbe` "runs once per pipeline", before anything is resolved. Under a borrowed scope the
owner can dispose the request scope at any point *after* the probe and *before* a later `Create`. A
verified probe shows resolution then throws `ObjectDisposedException` from Brighter's own resolution,
which is precisely what FR-23 forbids. The ADR's only acknowledgement calls it "the caller's error
and the same error it is today" — but under `AlwaysNew` the scope is Brighter's own and no caller can
dispose it mid-pipeline. Adoption creates the hazard, so it is this ADR's residue.

**Evidence**: `0072:296` — "A `Dispose` racing a `GetOrAdd` is the container disposing a scope while a
pipeline resolves from it, which is the caller's error and **the same error it is today**". `:404` —
the probe "runs once per pipeline"; `:476` (Risks) bounds the mitigation to "before any pipeline
instance is resolved from the ambient". `requirements.md:284` (FR-23) — "It must not surface
`ObjectDisposedException` from Brighter's own resolution to the caller".

Probe: `scope.Dispose(); scope.ServiceProvider.GetService(typeof(IServiceScopeFactory))` and
`GetService(typeof(Cache))` both threw `ObjectDisposedException`.

**Recommendation**: Add a sentence to "The residue is stated rather than claimed away" naming the
window: the probe bounds the *first* resolution only, and a caller that disposes a borrowed scope
while a Brighter pipeline is live surfaces `ObjectDisposedException`; state whether that is accepted
as caller error or bounded, and correct the `Dispose()` row.

#### 5. Row 8's missing acceptance criterion is said to be "recorded in *Negative*"; *Negative* records the behaviour but not the gap (Score: 62)

**Evidence**: `0072:234` — "**no acceptance criterion guards this row**. … and the gap is recorded in
*Negative* rather than left to be discovered." `:467` (the only matching bullet) — "**An ambient that
does not implement this package's hand-off role is declined with a `Warning` and nothing else.** …
That is the fail-safe behaviour C-7 asks for, but it is a quiet one." No mention of an AC gap.

**Recommendation**: Add one clause to `:467`: "and no acceptance criterion exercises it — FR-23 is
written about a *stale* source and AC-29 uses a capturing provider, so this row is covered by
extension of FR-23's diagnostic rather than by a criterion."

#### 6. The Scope paragraph carries a verbatim duplicated sentence and a malformed nested-bold span (Score: 60)

The nested `**` is not cosmetic: CommonMark closes the outer emphasis at the inner opener, so the
rendered sentence bolds and unbolds in the wrong places and "FR-16b" — the thing being emphasised —
comes out plain.

**Evidence**: `0072:31` — "… and **FR-26**. **Each is discharged by a named mechanism here. Each is
discharged by a named mechanism here:** …" (the sentence appears twice, once terminated by a full
stop and once by a colon); and "**FR-16, including both FR-16a and **FR-16b** — the borrowed scope
gives dependency identity … and AC-34's assertion**" (four `**` delimiters in one span, nested).

**Recommendation**: Delete the first duplicate and flatten the emphasis to a single span.

#### 7. "Rows 1 and 2 make a factory … offer nothing" contradicts row 2's own outcome cell (Score: 56)

**Evidence**: `0072:31` — "ladder **rows 1 and 2** make a factory whose configured lifetime is not
`Scoped` **offer nothing** and make no ask". `:92` (row 2 outcome) — "**a handle, but not an FR-27
pipeline scope** … and **no ask is made at all** (FR-27.1)". `:102` — "**Rows 1 and 2 are both
FR-27.1's 'no pipeline scope', and row 2 still yields an object.**"

**Recommendation**: "ladder rows 1 and 2 make a factory whose configured lifetime is not `Scoped`
**make no ask** — row 1 by offering nothing, row 2 by offering a handle that is not an FR-27 pipeline
scope".

#### 8. `## Context` is one 410-word block, against the house style's stated shape (Score: 55)

**Evidence**: `.agent_instructions/documentation.md`, ADR structure table: "`## Context` | 2–4
sentences in plain language". `0072:31` — 410 words, single paragraph; `:33` — 175 words. The
*opening* at `:25-27` is 102 words of plain language and orients correctly, which is what makes the
wall that follows avoidable.

**Recommendation**: Keep `:31`'s first two sentences as prose and lift the per-requirement routing
into a two-column table (`Requirement | Mechanism that makes it true here`).

#### 9. `ScopedArtefactCache`'s AC-37 clause 3 counter is specified only on the decrement side (Score: 52)

**Evidence**: `0072:287` — "so its `Dispose` only drops references and **decrements AC-37 clause 3's
counter**." No corresponding statement about the constructor; the contract table `:293-296` has rows
for `GetOrAdd` and `Dispose` only. `requirements.md:729` — "the counter is incremented in its
constructor and decremented in its `Dispose`".

**Recommendation**: Say "its constructor increments and its `Dispose` decrements", and add a
constructor row.

#### 10. "Four naming questions and one siting question" does not cover the list that follows it (Score: 50)

**Evidence**: `0072:33` — the sentence promises five items and enumerates them; the paragraph then
adds a sixth — how a `Publish` subscriber suppresses adoption — which is neither a naming nor a
siting question and is not in the count.

**Recommendation**: "It does not decide four naming questions, one mechanism and one siting
question", or drop the arithmetic.

#### 11. "FR-13's two clauses divide between ADR 0070 and ADR 0071" mislabels the division (Score: 46)

**Evidence**: `0072:31` — "FR-13 routing 'a borrowed scope is never disposed at all' to FR-12 … ;
**FR-13's two clauses divide between ADR 0070 (transform pipelines) and ADR 0071 (handler
pipelines)**". Same line, later, gets it right and uses different words. `requirements.md:229-230` —
FR-13's body is the disposal rule; the second clause is disposal failure. Neither is per-family.

**Recommendation**: "FR-13 divides **by pipeline family** between ADR 0070 and ADR 0071, and its
borrowed-scope carve-out is routed to FR-12 here."

#### 12. "Both `PipelineBuilder` filters now also exclude the new type" is redundant, and the touched row omits step 1a's structural edit (Score: 42)

**Evidence**: `0072:358` — "Ahead of each existing wrapping `catch` … add a clause for
`AmbientScopeSourceException` … **Both `PipelineBuilder` filters now also exclude the new type.**" A
clause placed *ahead* of the general one already shadows it. `:356` (step 1a) describes a
filter-spelling normalisation — verified against `PipelineBuilder.cs:202` and `:248`, both spellings
exactly as quoted — which the touched row at `:326` does not mention.

**Recommendation**: Drop the redundant sentence or justify it; add step 1a's normalisation to the
touched row.

#### 13. `ServiceProviderPipelineScope` in borrowed mode is absent from the roles table (Score: 38)

**Evidence**: `0072:158-167` (roles table, eight rows) omits the type that realises "own nothing,
dispose nothing", while `ScopedArtefactCache` and the probe both get rows. Compare `:129` (diagram)
and `:330` (touched table).

**Recommendation**: Add a row: "Ownership of the pipeline's scope | `ServiceProviderPipelineScope` |
**knowing** | Holds either an owned `IServiceScope` it disposes, or a borrowed `IServiceProvider` it
does not; the pipeline releases it either way without knowing which."

---

### ADR 0073 — `aspnet-core-request-scope-package`

11 findings, 5 at or above threshold. 0 Critical, 3 High, 6 Medium, 2 Low.

#### 1. C-14's falsification is named as this package's, but its actual outcome is never stated — and ADR 0072 asserts the opposite of what a probe measures (Score: 80)

0073 owns the C-14 assumption but stops at naming it. A probe shows the falsification does **not**
produce a stale ambient — it produces **adoption**: a pump thread started from inside a *live* request
sees a non-null `HttpContext` whose `RequestServices` is the live request scope, so 0072's usability
probe **passes** and ladder row 10 fires (BORROWED). That is a violation of FR-19's core clause, not
of its log bound. Neither 0073's Risks table nor any sibling carries the case, and the set
contradicts itself on the underlying fact.

**Evidence**: `0073:206` — "a pump thread is taken to carry no usable ambient `HttpContext`, yet
`IHttpContextAccessor` is `AsyncLocal`-backed, so a `Dispatcher` started from inside a request could
inherit one. FR-19's inertness bound rests on that assumption, and this package is where it would be
falsified." Nothing follows about what the pipeline then does.

`0072:406` states the opposite for the same scenario: "a flow that outlives the response — deferred
work, a `Dispatcher` started from within a request — observes a **null** `HttpContext`, `GetAmbient`
returns `null`, and the ask lands on **row 7**… It never reaches the probe." 0072's reasoning is only
sound for a flow that outlives the **response**; 0073's case is a flow started *during* the request.

`0075:228` sides with 0073: "a background job started from a request whose `HttpContext` still flows"
— and offers `AmbientScopeSuppression.Suppress()` as the recipe, which 0073 does not mention.
`requirements.md` AC-20 routes the case to FR-23, which the probe shows is the wrong rule.

Probe (net9.0, `FrameworkReference Microsoft.AspNetCore.App`), real `HttpContextAccessor` + `Task.Run`
started from inside a "request":
```
A.  pump thread sees a NON-NULL HttpContext while the request is live : True
A2. and that context's RequestServices is the live request scope      : True
B.  pump thread sees a NON-NULL HttpContext after the request ended    : False
C.  resolving from the disposed request scope: ObjectDisposedException
```
Row A is the case 0073 names; row B is the case 0072 describes.

**Recommendation**: In the C-14 paragraph, state the outcome: a `Dispatcher` started from within a
live request adopts that request's scope, which is an FR-19 violation in resolution and identity
rather than in logging, and FR-23 does not govern it because the scope is live and the probe passes.
Add a Risks row naming `AmbientScopeSuppression.Suppress()` as the mitigation. Narrow `0072:406` to
"a flow that outlives the response".

#### 2. The ASP.NET reference AC-14's `IHttpContextAccessor` spy forces onto the *existing* test projects is required work that appears in no step and no table (Score: 74)

0073 correctly excludes AC-14 from its new test project, then disposes of AC-14's actual cost in one
clause. But `IHttpContextAccessor` is an ASP.NET type: any assembly registering a spy needs
`Microsoft.AspNetCore.Http.Abstractions` on its compile closure. No test project has it. Adding it
means a `FrameworkReference` or a reference to the new package — the latter putting the
shared-framework runtime requirement onto Brighter's own test projects.

**Evidence**: `0073:278` — "What AC-14 needs is that the spy is visible to the existing projects,
which is a reference question rather than a hosting one, and it is answered where those projects
live." That is where the analysis stops. `0073:247-250` — the touched table's four rows include no
existing test project. `tests/Paramore.Brighter.Extensions.Tests.csproj` — no ASP.NET
`PackageReference` or `FrameworkReference`. Probe: `IHttpContextAccessor` assembly =
`Microsoft.AspNetCore.Http.Abstractions`. TFMs are not the blocker (`tests/Directory.Build.props:4`).

**Recommendation**: Add a touched-table row for `tests/Paramore.Brighter.Extensions.Tests` (and any
other project running AC-14's suite) stating the mechanism chosen, and say in step 4a that this is
the one place Brighter's own existing test projects acquire an ASP.NET dependency, and why that is
acceptable given AC-22.2 guards only the DI package.

#### 3. FR-15's package-inertness half — the clause 0073 claims to discharge — has no criterion that exercises a package reference (Score: 72)

**Evidence**: `0073:34` — "It discharges **FR-15's package-inertness half** — that taking the
reference, and calling nothing, leaves Brighter behaving exactly as it does today". `:64` and `:274`
point at AC-14 for it. `requirements.md` AC-14's **Given** is "an application configured exactly as
before this change … with an `IHttpContextAccessor` spy registered" — the word "package" does not
occur in AC-14's Given, When or Then. FR-15's *example* describes the case, but an example is not a
criterion.

**Recommendation**: Either say plainly that FR-15's package-inertness half is discharged by FR-15's
example with no criterion of its own — recording the gap in *Negative*, as 0072 does for its own
uncovered row — or state in step 4a that whichever project runs AC-14 must take a **reference to the
new package**, which is what turns AC-14 into a genuine inertness test.

#### 4. Both stated reasons for making `HttpContextScopeProvider` and `HttpRequestScope` public fail on inspection (Score: 66)

*Reason 1, NFR-7 composability*: NFR-7 is about not **precluding** other providers, not composing
this one. `requirements.md` NFR-7 in full: "The seam must not preclude a later `AsyncLocal`-based
`IAmAScopeProvider` for non-ASP.NET hosts, nor implementations over other containers… Neither is
delivered here." No composability clause. This is not the argument 0075 makes: 0075's flag must be
**read at run time** by a foreign container package or FR-8 is unhonourable.

*Reason 2, AC-19*: AC-19 asserts that a **log message names** the implementation type. That is a
string assertion; it needs no type reference.

**Evidence**: `0073:84`. The repository-rule half of the sentence **is** correct and was verified:
`grep -rn InternalsVisibleTo` finds one comment (`SpannerBoxMigrationRunner.cs:131`) and no attribute
anywhere.

**Recommendation**: Public is probably still right — but on the honest reason: the diagnostics latch
is keyed on the **implementation type** (D19), the new test project is a separate assembly with no
`InternalsVisibleTo`, and an application decorating or re-registering the provider must be able to
name it. Replace the NFR-7 composability claim and downgrade the AC-19 claim to what it is.

#### 5. "Structural rather than checked" does not establish the non-null invariant it claims, on a type the ADR insists is public (Score: 64)

Capturing the `IServiceProvider` at construction makes `Services` **unable to change**; it does not
make it **non-null**. The ADR locates the null check in the provider, but also makes
`HttpRequestScope` public with a public constructor, so the only enforcement of 0072's "must not be
null" obligation sits outside the type.

**Evidence**: `0073:204` (contract table) — "**Obliged** to be non-null by ADR 0072's role contract,
and this implementation makes that structural rather than checked". `:84` — the constructor is public
and unguarded. `0072:226` — `Services` "Must not throw and must not be `null`."

Probe confirming the surrounding facts, so the finding is about the claim and not the design:
```
1. new DefaultHttpContext().RequestServices == null : True
2. HttpContext.RequestServices setter public        : True
3b. after Features.Set<IServiceProvidersFeature>(null), RequestServices null? : True
```
Row 3b demonstrates that a stored-`HttpContext` design would not hold — the ADR's reason for wrapping
the provider is sound.

**Recommendation**: Add `ArgumentNullException.ThrowIfNull(services)` to `HttpRequestScope`'s
constructor in the sketch and to the contract table, and rewrite the claim: capture makes the value
immutable, the constructor check makes it non-null, and together they make the obligation an
invariant.

#### 6. FR-19's two-entry bound is a consumer-side rule, restated as a bound on any host with an ambient source (Score: 56)

**Evidence**: `0073:65` and `:293` — "FR-19 permits at most two log entries in total for a host with
an ambient source registered". `requirements.md` FR-19 — "**The flag is inert on the consumer
side.**" The whole requirement is scoped to consumption; generally there are **three** latched
conditions (FR-23, FR-24.2, FR-24.4).

**Recommendation**: "FR-19 bounds a consumer host at two entries; for this package's provider the
bound is one, because FR-23's and FR-24.4's conditions cannot arise from an accessor that clears
itself and a provider that never returns an ambient for an `AlwaysNew` ask."

#### 7. "Every `Use*` in the repository extends `IBrighterBuilder`" is false as an absolute (Score: 55)

**Evidence**: `0073:223`. Counterexamples: `UseRpc.cs:9` (`public class UseRpc : IUseRpc`),
`IUseRpc.cs:35`, `UseInboxAttribute.cs`, `UseInboxAsyncAttribute.cs`, `UsePolicyAttribute`,
`UseResiliencePipelineAttribute`, `UseInboxHandler<>`. The narrow claim was verified exhaustively:
**21** `Use*` extension-method declarations in `src/`, every one `this IBrighterBuilder`.

**Recommendation**: "Every `Use*` **extension method** in the repository extends `IBrighterBuilder`."
Optionally note that `Use*` is also Brighter's prefix for pipeline attributes, which strengthens the
rejection.

#### 8. The `GetAmbient` contract promises a non-throw the sketched body cannot guarantee (Score: 52)

**Evidence**: `0073:203` — "Must not throw where there is no current `HttpContext` … nor where a
context is current but carries no `IServiceProvidersFeature`." `:268` — "The provider's whole body is
an affinity check, **two** null checks … and a wrap." `HttpContext` is abstract and `RequestServices`
is an abstract property: a hand-rolled or heavily-mocked context can throw from the getter. Under
0072 (`:94`, ladder row 4) that throw propagates unwrapped to the caller of `Send` — the loudest
outcome for a case the contract says must be quiet. Probe rows 1 and 3b show the `DefaultHttpContext`
path is safe, which is why this is Medium.

**Recommendation**: Narrow the contract clause to the case the body delivers, and state that a throw
from a custom `HttpContext.RequestServices` getter is FR-24.1's propagating case by design.

#### 9. The Decision section carries a quarter-page accessibility argument where the house style allows one short paragraph (Score: 50)

**Evidence**: `.agent_instructions/documentation.md` — "`## Decision` | the decision in **one bold
sentence**, then one short paragraph on the shape it takes. No signatures, no file paths". `0073:84`
runs ~250 words arguing `public` versus `internal`, citing AC-19, AC-29 and AC-18 with two nested
parentheticals.

**Recommendation**: Move it to *Technology Choices* as "**Why the provider and the scope are public
rather than `internal`.**"

#### 10. Seven of the eight criteria need a controller action; AC-19 needs the host but not the controller (Score: 44)

**Evidence**: `0073:276` — "**Eight** of the acceptance criteria … need a running ASP.NET Core host
with a controller action." `requirements.md` AC-19's **When** is "an `IHostedService` in the same host
— and, separately, a background thread with no `HttpContext` — calls `Send`". All eight were checked;
only AC-19 lacks a controller.

**Recommendation**: "…need a running ASP.NET Core host — seven of them with a controller action, and
AC-19 with the host but deliberately without one."

#### 11. The naming argument sits inside `### Key Components` between the contract table and `#### Where each type is touched` (Score: 38)

**Evidence**: `0073:212-241` sits between the contract table ending at `:210` and `#### Where each
type is touched` at `:243`. The namespace argument at `:237-239` duplicates *Alternatives* 5 and 6 at
`:328-330`.

**Recommendation**: Move to *Technology Choices* as "**Why `AddBrighterRequestScope` and not the
working name.**" The three namespace alternatives are already in *Alternatives Considered*.

---

### ADR 0074 — `lifetime-validation-evaluation-site`

15 findings, 7 at or above threshold. 1 Critical, 1 High, 11 Medium, 2 Low.

#### 1. `ArtefactRegistration`'s shape is stated two contradictory ways inside one sentence (Score: 90)

The paragraph that fixes how a type presenting two `ArtefactKind`s is collapsed says the snapshot
yields **one record per type carrying a set of kinds**, explicitly rules out the per-`(type, kind)`
alternative, and then — after a colon in the same sentence — says it yields **one record per
`(type, kind)`**. The nested `**` markers are also unbalanced, so the sentence will not render as
intended. The two readings imply different entity shapes, different rule inputs and a different place
for de-duplication, and the roles table describes only one of them.

**Evidence**: `0074:280` — "**`ContainerRegistrationSnapshot` owns that collapse, and owns it by
yielding **one `ArtefactRegistration` per type carrying the set of kinds it presents** — not one per
(type, kind), which would leave the duplicate to survive into the candidate list with nothing
downstream to remove it. The rule inspects such a type once, and does so if **any** of its governing
lifetimes is `Singleton`**: it yields one `ArtefactRegistration` per (type, kind), and the
de-duplication is applied where the candidate list is built rather than inside the rule".

The roles table at `:174` describes only the singular form — "One candidate artefact: its type, its
`ArtefactKind` … and **that** lifetime's value" — incompatible with "carrying the set of kinds it
presents" and with "if **any** of its governing lifetimes is `Singleton`".

**Recommendation**: Delete one reading outright. If the per-`(type, kind)` form is the decision (which
the roles table, the flowchart node at `:147` and the `Source` format `$"{kind} '{artefactType.Name}'"`
at `:274` all assume), say: *`ContainerRegistrationSnapshot` yields one `ArtefactRegistration` per
(type, kind); a type presenting two kinds therefore appears twice, and the de-duplication by (artefact
type, dependency service type) is applied where findings are collected, not inside the rule.* Fix the
`**` nesting at the same time.

#### 2. The Negative bullet and the failure-mode table disagree about which C-20 bound is a false positive (Score: 72)

**Evidence**: `0074:299` — "**Three** rows can report *wrongly* — the snapshot-staleness row, **the
Brighter-mapper row** and the constructor-divergence row below". The table row at `:311` — "A
Brighter-shipped **mapper** with constructor dependencies | **would be reported** … Latent today".

Against `0074:431` — "**Three surface as silent misses and one, the constructor divergence, can also
report wrongly** — C-20(i) says so in terms." `requirements.md:384` C-20(iv): "were one added it would
be **warned against as if it were the user's**" — a false positive, not a miss.

**Recommendation**: Rewrite the Negative bullet as *two of the four can report wrongly — the
constructor divergence (C-20(i)) and the mapper gap (C-20(iv)) — and two are silent misses*. Both
wrong-report cases matter for FR-25.8's guidance text.

#### 3. The constructor-divergence failure-mode row states a condition that, measured, does not produce the stated outcome (Score: 68)

The row says the rule "would warn wrongly" when the widest constructor's parameter set is not a
superset of every other resolvable candidate's. A probe shows MS DI **throws** in exactly that case —
it never selects a different constructor — so the host fails at first resolution regardless. The case
that genuinely produces a quiet false warning is never stated anywhere in the ADR.

**Evidence**: `0074:309` — "A widest constructor Microsoft's container would **not** select … | **would
warn wrongly**, naming a `Scoped` parameter the container never resolves | C-20(i) … No acceptance
criterion exercises it".

Probe 5: `Artefact` with `ctor(IA, IB)` and `ctor(IC)`, all three registered →
`THREW InvalidOperationException: Unable to activate type 'NotSuperset'. The following constructors
are ambiguous: Void .ctor(IA, IB) / Void .ctor(IC)`.

Probe 5b: `ctor(IScopedDep, INotRegistered)` and `ctor(ISingletonDep)` with `INotRegistered` absent →
`MS DI selected: narrow (Singleton only)` while `Brighter D15 would inspect: (IScopedDep,
INotRegistered)` — a real, silent false warning.

**Recommendation**: Split the row. Keep the superset case but state its outcome as *the type is not
activatable at all — MS DI raises `InvalidOperationException: … constructors are ambiguous* — so the
warning is moot; add a new row for the unregistered-parameter case, where "warns wrongly" is literally
true, and mark it as having no acceptance criterion.

#### 4. The registration snippet, read as written, drops `transformerProbe` and so disables the very rule the surrounding comment protects (Score: 68)

**Evidence**: `0074:201-203`:
```csharp
var inner = new PipelineValidator(
    /* exactly as today */,
    mapperRegistryFactory: registry is null ? null : () => registry.Value);
```
Real signature `PipelineValidator.cs:54-63`: `… ValidationProviderRegistrations? providerRegistrations
= null, Func<MessageMapperRegistry>? mapperRegistryFactory = null, IAmATransformerResolvabilityProbe?
transformerProbe = null`. The gate at `:139`: `if (_mapperRegistry is not null && transformerProbe is
not null)`. Today's call site supplies both (`BrighterPipelineValidationExtensions.cs:91-93`).
Probe 9 compiled the same shape and reported `inner.HasProbe = False`.

**Recommendation**: Show `transformerProbe` explicitly, or move the placeholder comment so it clearly
covers both trailing arguments.

#### 5. `ContainerRegistrationSnapshot`'s stated responsibility cannot supply three of the six rules' inputs (Score: 65)

**Evidence**: `0074:175` — the snapshot "answers 'what lifetime is this service type registered with'
and 'what artefacts are registered'". `:379` step 2 — "**Two queries**". Against `:173` —
`ScopeConfiguration` holds "the ambient-source registrations, the affinity-override registrations and
the `IBrighterOptions` registrations, each list in registration order" — and `:260` — "This rule needs
no new input: the descriptors are already in the `ValidatePipelines()`-time snapshot". `0076:306` puts
the `BrighterOptionsRegistration` in the collection as an instance registration, a fourth thing the
snapshot must return.

**Recommendation**: State four queries: (a) effective lifetime for a service type; (b) artefact
candidates with kinds; (c) descriptor records for a service type in registration order, carrying
implementation type / position / `ImplementationInstance`; (d) the `BrighterOptionsRegistration`
instance if present.

#### 6. The Context's opening claim that all six configurations are "now expressible" is contradicted by C-18 and by the ADR's own Negative section (Score: 62)

**Evidence**: `0074:26` — "**Six configurations are now expressible** that an application almost
certainly did not intend". Against `:427` — "An application that **today** sets, say,
`HandlerLifetime = Scoped` with `MapperLifetime = Transient` **works** … and if it calls
`ValidatePipelines()` it will now fail to start", and `requirements.md:382` C-18 — "Such
configurations work today". The framing misprices the FR-22.2 break.

**Recommendation**: Split the sentence: *four of these become expressible with this work; two — the
mixed triple and the captive `Singleton` — are expressible today and go unreported, which is why
FR-22.2 is a compatibility break (C-18) rather than a new guard.*

#### 7. `NFR-4` is invoked in the contract table for a claim NFR-4 does not make, and is absent from the References list (Score: 60)

**Evidence**: `0074:228` — "a repeated or concurrent call is safe and yields the same result
**(NFR-4)**." `requirements.md:355` NFR-4 — "*Beginning and releasing pipeline scopes, and
establishing and clearing ambient suppression*, must be safe under concurrent pipelines…". `0074:489`
References lists NFR-1, NFR-7, NFR-8, NFR-9, NFR-10 — no NFR-4.

**Recommendation**: Either drop the attribution and state the property as a design invariant, or add
NFR-4 to References and say which criterion covers it. No AC asserts a repeated `Validate()` call.

#### 8. The "two capture points" input table omits the exclusion set, which is a third input from a third source (Score: 58)

**Evidence**: `0074:233-238` — "#### How the inputs reach the rules — **two** capture points". Against
`:209` — `ArtefactExclusionSet.Build(pipelineBuilder, registry?.Value, publications, subscriptions)` —
and `:284`. Those come from `ResolvePublications(sp)`/`ResolveSubscriptions(sp)`
(`BrighterPipelineValidationExtensions.cs:135-149`), none from `builder.Services` and none a
`ServiceDescriptor`. FR-22.3's exclusion half depends entirely on it.

**Recommendation**: Add a third row for the attribute-returned artefact types. The "two well-defined
instants" claim survives; the "two inputs" claim does not.

#### 9. The `AutoFromAssemblies` prefix-filter claim does not hold for `extraAssemblies` (Score: 56)

**Evidence**: `0074:288` — "The same filter bears on AC-42's prefix case — a transform in
`Paramore.Brighter.Extensions.Tests` is **not auto-scanned either**". `ServiceCollectionBrighterBuilder.cs:118-122`
filters `appDomainAssemblies`; `:124` — `var assemblies = extraAssemblies != null ?
appDomainAssemblies.Concat(extraAssemblies) : appDomainAssemblies;` — and `:131` runs over the
concatenation. (The claim is correct for `ClaimCheckTransformer`, which lives in `Paramore.Brighter`.)

**Recommendation**: "a transform in `Paramore.Brighter.Extensions.Tests` reaches the collection either
way — explicitly, or through `AutoFromAssemblies(extraAssemblies)`, which does not apply the prefix
filter to assemblies the caller names."

#### 10. Sharing the `MessageMapperRegistry` inverts the documented purpose of `PipelineValidator`'s factory parameter, and the touched table says core is unchanged (Score: 55)

**Evidence**: `PipelineValidator.cs:45-51` — "The validator invokes it at most once — **lazily, the
first time a validation rule needs the registry** … Taking a factory rather than a live instance keeps
that ownership transfer explicit … **so a caller cannot hand in a registry it still uses elsewhere and
have it disposed underneath them.**" Against `0074:216` — "**And it does force the `Lazy` where one
exists.**" — and `:388`. `0074:349` — "`Paramore.Brighter` | — | **nothing.**"

**Recommendation**: Add a sentence to step 5a acknowledging the ownership rule is deliberately relaxed
— the DI package becomes the owner and the guarantee is carried by `MessageMapperRegistry`'s
`Interlocked` claim — and add a doc-comment amendment to the touched table.

#### 11. AC-40 is described as using "a single registration path" but is a mixed two-path host (Score: 54)

**Evidence**: `0074:362`. `requirements.md:611` AC-40 — "**Given** the AC-27 configuration in a host
that calls `AddBrighter(Action<BrighterOptions>)` **before** `AddConsumers(Action<ConsumersOptions>)`".
The ADR's own host table at `:335` classifies that shape as a distinct *Mixed* row.

**Recommendation**: "AC-27, AC-28, AC-41 and AC-42 each use a single registration path, and AC-40 a
mixed host in which C-12's first-wins decides the object read."

#### 12. Step 5a's "(step 2)" cross-reference points at the wrong step (Score: 52)

**Evidence**: `0074:386` — "with `registry` **nullable** because `mapperRegistryFactory` is (step 2)".
Step 2 (`:379`) is "**The snapshot.**" and says nothing about it; the nullability is established in the
registration snippet (`:191-214`).

**Recommendation**: "(the registration snippet under *The evaluation site*)", and name `Build`'s
parameter type explicitly as `MessageMapperRegistry?`.

#### 13. "every clause of AC-42 except the two that assert host startup" — AC-42 has nine clauses and one startup assertion (Score: 50)

**Evidence**: `0074:421`. AC-42's `Then` plus its eight `And given … Then` pairs
(`requirements.md:630-648`) = nine clauses; exactly one asserts a startup outcome. The `When` is
common to all nine, so no reading yields "two".

**Recommendation**: "every clause of AC-42 except the first, which asserts that startup succeeds".

#### 14. The `ArtefactExclusionSet` roles row states two different responsibilities (Score: 45)

**Evidence**: `0074:177` — "**It answers one question — is this type one Brighter put in the pipeline
itself** — and holds the attribute half of FR-22.3's conjunction". It cannot do the first; the
assembly-prefix half lives outside the type.

**Recommendation**: "It answers one question — was this type put into a pipeline by a Brighter
attribute — which is the attribute half of FR-22.3's conjunction; the assembly-prefix half is applied
by the rule."

#### 15. `ServiceCollectionTransformerResolvabilityProbe` `:40-56` does not include the `Contains` the bullet describes (Score: 40)

**Evidence**: `0074:86`. In the file, `:40` is the class declaration, `:42` the field, `:50-55` the
constructor, and the `Contains` is at `:58`. (The same range appears in `requirements.md:319`, so this
is inherited rather than introduced.)

**Recommendation**: Cite `:40-58`.

---

### ADR 0075 — `publish-subscriber-scope-suppression`

8 findings, 4 at or above threshold. 0 Critical, 1 High, 5 Medium, 2 Low.

#### 1. Alternative 5's async half states a narrower harm than the runtime produces — probe contradicts "the synchronous-prefix harm alone" (Score: 76)

Alternative 5 rejects a single bracket around `Task.WhenAll` on the ground that only each subscriber's
*synchronous prefix* runs unsuppressed. That is measurably wrong: **nothing** in any subscriber is
suppressed under that shape, before or after any `await`, because every subscriber's task branched
from the caller's flow *before* the bracket was taken. The ADR's own step 5a establishes exactly this
mechanism in the opposite direction, then fails to apply it here.

This matters more than a normal imprecision because step 5a closes with: "Stating which is which
matters, because a reader who believes the async restores are what save the caller … has the mechanism
backwards and will place the next bracket by the wrong rule." A reader who takes "the harm is the
prefix" literally concludes that suppression established after a task branches reaches that task once
its prefix is over — the precise inversion the ADR warns against.

**Evidence**: `0075:319` — "a bracket around `Task.WhenAll` (`CommandProcessor.cs:601`) is established
**after every handler's synchronous prefix has already run**, so the resolutions and the dispatch-time
`Post`s that prefix issues are unsuppressed … **The rejection rests on the synchronous-prefix harm
alone**".

Probe `[O]`, 3 subscribers started unbracketed, then one bracket around `Task.WhenAll`, observation
points inside each subscriber:
```
caller's own flow inside the bracket suppressed : True
subscriber SYNCHRONOUS PREFIX suppressed        : 0/3
subscriber AFTER 1st await suppressed           : 0/3
subscriber AFTER 2nd await suppressed           : 0/3
```

**Recommendation**: Replace "the synchronous-prefix harm alone" with the measured fact: a bracket taken
after the subscribers' tasks have branched reaches none of them at any point in their lives, so *every*
resolution and *every* nested `Send`/`Post`/`Publish` a subscriber issues is unsuppressed — which is
why AC-12's resolution-time clause *and* its nested-`SendAsync` clause both fail on it. Keep the
correct note that the caller-flow harm does not arise on the async path.

#### 2. "Two `AsyncLocal` writes per subscriber" undercounts by half — there are four, and the ADR measures the cost off that number (Score: 68)

The design specifies **two brackets per subscriber**, and the ADR insists throughout that each
bracket's restore is *written explicitly* — so each bracket is a set **and** a restore: four writes per
subscriber. The bullet then uses that figure to reason about the consumer hot path.

**Evidence**: `0075:280` — "Suppression costs nothing when nothing is published, and **two `AsyncLocal`
writes per subscriber** when something is". Contradicted by `:85` ("bracketed twice per subscriber …
with the restore written explicitly on both") and `:261`.

Measured `[I]`, 100,000 iterations each, `GC.GetAllocatedBytesForCurrentThread`, .NET 10.0.102:
```
reads  (AL.Value get)             :          0 bytes (0.00/op)
writes, value CHANGES             :    9600000 bytes (96.00/op)
writes, value UNCHANGED           :    9600000 bytes (96.00/op)
full brackets Suppress()+Dispose():   24800000 bytes (248.00/op)
```
So the real figure is **four writes ≈ 496 bytes and 4 `ExecutionContext` allocations per subscriber per
message**. A write allocates *even when the value is unchanged*, so a nested bracket re-setting `true`
is not free — which the "one bit" framing invites a reader to assume.

**Recommendation**: State "four `AsyncLocal` writes per subscriber — two brackets, each a set and an
explicit restore" and give the measured per-bracket allocation.

#### 3. Malformed emphasis and a duplicated rejection in Alternative 3a (Score: 62)

**Evidence**: `0075:313` — a four-asterisk emphasis run that does not pair, wrapping a bolded block
whose two rejection grounds the very next sentence restates in full. `grep -n '\*\*\*\*'` returns this
line and no other. The "tests live in a separate assembly / no `InternalsVisibleTo`" ground appears
twice in consecutive sentences. It reads as an unmerged edit left in place.

**Recommendation**: Fix the emphasis run and collapse the duplication into a single statement of the
two grounds, keeping the sentence that explains why the brackets are most in need of direct testing.
(See also the gap-coverage findings 1 and 7, which land on the same paragraph.)

#### 4. The sequence diagram shows a `loop` for bracket 1 but none for bracket 2, so it depicts the shape Alternative 5 rejects (Score: 60)

The diagram is the orienting artefact for the Decision's second invariant — "Neither is ever placed
around the whole loop" — and it does not carry that invariant for bracket 2. The async branch reads
`Suppress() → HandleAsync → restore → await Task.WhenAll(tasks)` — literally one bracket followed by
the awaited whole, which is Alternative 5's async half.

**Evidence**: `0075:99-103` carries `loop for each subscriber` around bracket 1; `:106-122`
(`alt … else …`) has no `loop` construct at all. Rendered PNG inspected: bracket 1's iteration frame is
visible, bracket 2's branches have none. The note at `:105` says "per subscriber" in prose, but the
prose is what the diagram is supposed to make unnecessary. All 15 mermaid blocks render, so this is a
modelling defect, not a rendering one.

**Recommendation**: Wrap each `alt` branch's `Suppress()/dispatch/restore` in `loop for each
subscriber`, leaving `await Task.WhenAll(tasks) — never bracketed` outside the loop.

#### 5. The Decision denies FR-9(a)'s stated reason without saying which half of FR-9(a) it is talking about (Score: 55)

**Evidence**: `requirements.md:200` (FR-9(a)) — "Establishing them around the whole build loop is wrong
(**it would give all subscribers one pipeline scope**)". Against `0075:129` — "**Not because a
loop-level bracket would share a scope — it would not**". The ADR's statement is correct on the source
(`PipelineBuilder.cs:190`/`:235` call `GetSyncInstanceScope()`/`GetAsyncInstanceScope()` inside the
per-subscriber lambda), so the defect is the missing reconciliation, not the claim.

**Recommendation**: Add half a sentence: FR-9(a) brackets the pipeline scope and suppression together
and its parenthetical is about the scope half, which is ADR 0071's; for the suppression bit alone a
loop-level bracket shares nothing, and the rejection rests on extent-matching and on ADR 0039's
per-subscriber scope not being invited to collapse.

#### 6. NFR-5 and NFR-6 are cited as the guarantee for a cost neither of them covers (Score: 50)

**Evidence**: `0075:280` — "it is bounded by subscribers per message and does not grow with message
count, **so NFR-5 and NFR-6 hold**". `requirements.md:357` NFR-5 is over "Memory attributable to
Brighter **scopes**"; `:358` NFR-6 over "at most one DI scope begin/release per pipeline"; AC-23's Then
counts scopes begun versus released. None observes an `ExecutionContext` allocation. The conclusion is
true but vacuous, and no criterion detects the cost the bullet is actually pricing.

**Recommendation**: Say that NFR-5 and NFR-6 are about scopes and are untouched, and that the
per-subscriber allocation is a cost with **no** acceptance criterion over it — the same pattern the ADR
already uses well for AC-24 at `:232`.

#### 7. The bracket's disposal semantics are split across two contract rows, one of which is not about the member it names (Score: 38)

**Evidence**: `0075:206` (row: "the bracket's `Dispose()`, **on the flow that took it**") and `:207`
(row: `Suppress()`, whose Error-conditions cell begins "Disposing a bracket twice is a no-op. Disposing
brackets **out of order** …"). Both stated behaviours are correct — probe `[D]`/`[D2]` reproduces all
three exactly.

**Recommendation**: Move the double-dispose and out-of-order clauses into the `Dispose()` row and leave
`Suppress()`'s Error-conditions cell as "Cannot throw".

#### 8. A Positive bullet carries two `file:line` citations (Score: 32)

**Evidence**: `0075:280` — "(`Reactor.cs:406`, `Proactor.cs:130`)". Against
`.agent_instructions/documentation.md:106` — "At most one per forces or Consequences bullet." Both
citations are correct.

**Recommendation**: Keep one and let the sentence carry the other path by name.

---

### ADR 0076 — `scope-affinity-option-and-write-through`

7 findings, 3 at or above threshold. 0 Critical, 1 High, 3 Medium, 3 Low.

#### 1. The risk table cites AC-45 as asserting the extension-before-`AddBrighter` ordering; AC-45 fixes no ordering at all, and the second half of the same sentence is false of AC-48 (Score: 72)

The whole decision is order independence, and the risk row that answers "the extension is called before
`AddBrighter` and the affinity is lost" rests on two acceptance criteria. Neither carries what is
claimed of it.

AC-45's **Given** fixes four hosts "each calling the new package's registration extension with no
affinity argument" and never states *where* that call sits relative to the Brighter registration. Its
third clause is an ordering *of assignment against argument*, not of registration calls. AC-48 does
carry a before-ordering — "the same holds with the extension call placed before `AddBrighter` as well
as after it" — but its Given is `AddBrighter(Action<BrighterOptions>)` only, so the before-ordering is
pinned on **one of the four registration paths**. Neither `Func` path nor `AddConsumers(Action)` has
any criterion asserting it positively. (AC-50's extension-before branch is the *defeated* host, and
asserts an `Error`.)

The second half of the sentence is separately wrong. AC-48's first configuration has the application
assign `DefaultScopeAffinity = ScopeAffinity.AlwaysNew`, which is this ADR's own declared default. It
does **not** start from a non-default affinity.

**Evidence**: `0076:415` — "AC-48's second clause and AC-45's four-path clause both assert the
before-ordering, and both start from a non-default affinity so a dropped argument fails them". Against
`requirements.md:744-753` (AC-45) and `:755-761` (AC-48). The Positive bullet at `:392` is fine; it is
the risk row's *assert* that over-claims.

**Recommendation**: Rewrite the mitigation to what the criteria actually pin: "AC-48's second clause
asserts the before-ordering, on `AddBrighter(Action<BrighterOptions>)`; AC-45's third clause makes a
dropped argument fail on all four paths by starting from a non-default affinity. The before-ordering is
pinned on one path and holds on the other three by construction, because the override is a service
rather than a mutation of a descriptor." Either accept that as the coverage, or raise the gap so a
criterion is added for the before-ordering on the two `Func` paths.

#### 2. FR-22.4's condition is restated here with one conjunct where ADR 0074 requires two — immediately beside the "125 files under `tests/`" population (Score: 68)

This ADR declares that the readable surface for FR-22.4 belongs with the writer, then states the rule's
condition three times, and every time as a single question — is the effective `IBrighterOptions`
descriptor Brighter's own? ADR 0074 states it as a **conjunction**, and is explicit about why the first
conjunct exists.

**Evidence**: `0076:343-345` — "The pattern is not exotic: 125 files under `tests/` register
`IBrighterOptions` themselves today. … **So the limit is diagnosed instead**: it is `Error` under
**FR-22.4**, evaluated by ADR 0074, whose rule asks the `BrighterOptionsRegistration` above whether the
effective `IBrighterOptions` descriptor is the one this method added." Repeated at `:169` and `:417`.

Against `0074:262` — "The condition has two conjuncts: an affinity override is present in the snapshot
… **and** the `IBrighterOptions` descriptor Microsoft's container will resolve … is not one Brighter's
own registration produced" — and `0074:272` — "the override conjunct is what keeps that host silent,
and it is not a hypothetical population — 125 files under `tests/` register `IBrighterOptions`
themselves today".

0076 cites the same population two sentences before its single-conjunct statement, in support of the
opposite point. Read from 0076 alone, an implementer builds a rule that raises a startup-failing
`Error` in 125 existing test hosts. The population was independently recounted at **125**.

**Recommendation**: In all three places, state the condition as ADR 0074 does, and say in the 125-file
sentence that those hosts stay silent because they never opt in.

#### 3. "`GetRequiredService<IBrighterOptions>()` would throw during `BrighterHandlerBuilder`" — nothing is resolved during `BrighterHandlerBuilder`, and this ADR says so itself two paragraphs later (Score: 62)

**Evidence**: `0076:314` — "Under a `ServiceType`-only guard it would get **no descriptor at all**,
`GetRequiredService<IBrighterOptions>()` would throw during `BrighterHandlerBuilder`…". Source:
`ServiceCollectionExtensions.cs:142-173`, whose body opens with `// DO NOT build intermediate provider -
defer all resolution` (`:146`) and whose only two `GetRequiredService<IBrighterOptions>()` calls are
inside registered factory delegates (`:161`, `:169`). The ADR's own contract table states the correct
rule for the same object at `:320` — "`optionsFunc` is invoked at first resolution, not here".

**Recommendation**: "…would get **no descriptor at all**, and `GetRequiredService<IBrighterOptions>()` —
reached from the `IAmARequestContextFactory` and `IAmAFeatureSwitchRegistry` delegates
`BrighterHandlerBuilder` registers (`:161`, `:169`) — would throw at the first resolution that builds
the command processor".

#### 4. `ConsumersOptions` is placed in the wrong assembly in `Where each type is touched` (Score: 58)

**Evidence**: `0076:355` lists `ConsumersOptions` under `…DependencyInjection`, the elision this ADR
uses for `Paramore.Brighter.Extensions.DependencyInjection` — the last row of the same table (`:360`)
spells `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` out in full, so the elision
is unambiguous. The type is at
`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ConsumersOptions.cs:10`. The bare
`ConsumersOptions.cs:10` citation appears twice more in the prose (`:72`, `:210`) with no path.

**Recommendation**: Move the row, and give the two prose citations the same path prefix the sibling
citations carry.

#### 5. A Consequences bullet carries three line citations, against the house rule of at most one (Score: 48)

**Evidence**: `0076:404` carries `:38-39`, `(:89-90)` and `:39` in one bullet. The forces bullets in this
ADR are clean (zero citations across all seven), and every other Negative bullet carries at most one.

**Recommendation**: Keep `:38-39` and drop the other two; `:89-90` is already made twice in *Key
Components*.

#### 6. The mechanism flowchart draws four arrows into `RegisterBrighterOptions`, contradicting the prose one section later that it is called from one place (Score: 42)

**Evidence**: `0076:119-129`, the `flowchart LR` block: `a1…a4` each with its own edge into the `RBO`
node; `BrighterHandlerBuilder` appears nowhere on it. The prose that follows is the correct account:
`:269` — "It is called from **one** place: `BrighterHandlerBuilder`, which every registration path
already funnels through", and `:310` — "a fifth path cannot exist without it". The third diagram gets
this right. Since the whole guarantee turns on the funnel, the orienting artefact for it is the one that
hides it.

**Recommendation**: Insert a `BHB["BrighterHandlerBuilder"]` node between the four entry points and
`RBO`.

#### 7. "Exactly one implementation in `src/`" is one short — `ConsumersOptions` implements `IBrighterOptions` too, by inheritance (Score: 38)

**Evidence**: `0076:212` — "a repository-wide search for implementations of `IBrighterOptions` finds
**exactly one** in `src/`", repeated at `:353` and `:401`. A multi-line class-declaration scan confirms
exactly one *direct* implementer, so the conclusion (nothing in the repository breaks) is right — but
`ConsumersOptions : BrighterOptions, IAmConsumerOptions` *is* an `IBrighterOptions`, and this ADR relies
on that three lines away (`:210`). ADR 0070's ledger uses the same wording (`0070:394`), so both move
together.

**Recommendation**: Say "exactly one *direct* implementation" — the distinction is load-bearing, because
a derived type inherits the member and cannot break.

---

### Set-level

10 findings, 6 at or above threshold. 0 Critical, 2 High, 7 Medium, 1 Low.

#### 1. ADR 0070 justifies making `ServiceProviderPipelineScope` public with a cross-reference ADR 0072 denies (Score: 72)

Secondarily, the reason is unsound even on its own terms: `ServiceProviderPipelineScope` and every
factory that type-tests it (`ServiceProviderHandlerFactory`, ADR 0071 step 4; the four transform
factories, ADR 0070 step 6) live in the *same* assembly. No type test in this set crosses a package
boundary onto that class.

**Evidence**: `0070:276` against `0072:233`. (Full quotations at 0070 #1 above.)

**Recommendation**: Delete the "because ADR 0072 type-tests it across the package boundary" clause. The
decision survives on its second reason alone; if 0072's role-interface rule is what makes the
class-level test unnecessary, say so and cite `0072:233`.

#### 2. ADR 0071 cites AC-7 for the exact rule its own Implementation Approach says AC-7 does not cover (Score: 72)

**Evidence**: `0071:291` against `0071:360`; `requirements.md:452` (AC-7's Given is a throwing
**handler**) and `:538` (AC-51's Given is a throwing `Release`). Opened and checked.

**Recommendation**: In `0071:360`, replace "AC-7" with "AC-51".

#### 3. ADR 0070 states the same fact twice with two different ACs — AC-33 in `Scope`, AC-51 in step 9a (Score: 68)

**Evidence**: `0070:34` (AC-33) against `0070:417` (AC-51), about the same thing — FR-13's
**disposal-failure** clause. AC-33 is correct; AC-51 is the handler-*release* half. `0071:291` confirms
the split. The step 9a row is the one a reader building the test plan will use.

**Recommendation**: Change `AC-51` to `AC-33` at `0070:417`.

#### 4. FR-7's owning ADR is 0070, which does not touch handler pipelines; the ADR that puts FR-7 at risk claims only to "protect" it (Score: 65)

ADR 0070 declares the set's coverage rule — "Every FR has exactly one owning ADR … **so a coverage audit
lands on the mechanism that makes the requirement true**" — and then claims FR-7 inside the range
`FR-1 … FR-7`. But 0070's own map section says handler pipelines are "**not** touched here". FR-7 is
entirely about handler pipelines, and the only ADR that could break it is 0071, which replaces the
carrier, re-reads FR-7's "not re-implemented differently" clause, and repairs
`HandlerLifetimeScope.Dispose()`. 0071's `Scope` says it "exists to protect FR-7", not that it
discharges it. So the audit lands on the ADR with no mechanism, and the ADR with the mechanism
disclaims ownership.

**Evidence**: `requirements.md:193` (FR-7); `0070:30`, `:32`, `:56`; `0071:30`, `:101`.

**Recommendation**: Move FR-7's ownership to ADR 0071 (whose step 5 table, AC-9 and the AC-14
duplication in step 6 are the actual mechanism and guard), and have 0070 name it as *served* — the same
treatment 0076 gives FR-19 and FR-21. Otherwise state explicitly in 0070 why an ADR that touches nothing
is the owner.

#### 5. "All eight interfaces the set breaks" undercounts by one — `IBrighterOptions` is a ninth, and it is in the same ledger (Score: 65)

Counting from the ledger itself: the four mapper/transformer factories + the two mapper registries
(0070) + `IAmAHandlerFactory` + `IAmALifetime` (0071) = eight, **plus `IBrighterOptions`** (0076) =
nine, all source-and-binary breaks recorded in the one entry. `0070:389`'s narrower phrasing — "Eight
interfaces break **across the two ADRs**" — is correct; the two set-scoped restatements are not.

**Evidence**: `0070:86` — "names all **eight** interfaces **the set** breaks"; `0075:232` — "the
source-and-binary break the **eight interface signatures** carry"; `0070:398` (ledger bullet 10) —
"**Source and binary, ADR 0076.** `IBrighterOptions` gains `DefaultScopeAffinity`"; `0076:353`, `:401`.

**Recommendation**: Say "nine" in both places, or scope the sentence as `0070:389` does. Pick one number
and use it in all three sentences.

#### 6. The 12-bullet release-note ledger has one acceptance criterion behind it, and that criterion reaches four bullets (Score: 60)

AC-24 has four **Then** clauses: the `MapperLifetime.Scoped` break (bullet 1), C-18's mixing break
(bullet 12), FR-22.2's joint consequence (guidance, not a bullet), and "for each of the six **factory**
interfaces whose signature changed" (bullets 2 and 5, partially). **Eight of the twelve bullets have no
criterion at all**: the factory-level cache removal, the six transform-pipeline constructors,
`HandlerLifetimeScope.Dispose()`'s observable change, the pipeline-scope disposal log-level change, the
faulted-`Lazy` eviction, `PipelineBuilder`'s two constructors, `IBrighterOptions`, and the
`IAmAPipelineValidator` resolution change. Two ADRs notice the gap for their own entry (`0074:392`,
`0075:232`); nothing notices it for the ledger as a whole, and AC-24's verifier is a PR-description
checklist written over AC-24's clauses only.

**Evidence**: ledger bullets `0070:385`–`:400`; `requirements.md:683-690` (AC-24); `requirements.md:700`
— "*Verifier for AC-24, AC-25, AC-36 and AC-43:* a checklist in the PR description with one line per
**Then** clause **above**"; `0075:232`.

**Recommendation**: Either add an AC over the ledger as a whole (one **Then** per bullet), or state at
7a that eight of its twelve entries are guarded only by the PR checklist and extend AC-24's verifier
clause to the ledger rather than to AC-24's own clauses.

#### 7. ADR 0074 discharges FR-25 and writes the guidance page, but never cites AC-25 anywhere in its body (Score: 58)

AC-25 is the criterion that enumerates what `docs/guides/lifetimes-and-scoping.md` must contain. ADR
0074 owns FR-25 and NFR-9, and step 7 plus the eleven-row clause map is the plan for that page — but
AC-25 appears only in the `## References` requirement line. It is the sole AC in either direction that
is out of step.

**Evidence**: `0074:34`; `0074:392-410` (step 7 and the clause map, citing no AC at all);
`requirements.md:693` (AC-25), including "every row of that table cites the AC that asserts it … **And**
any row that cites no AC is itself a finding".

**Recommendation**: Cite AC-25 in 0074 step 7 and in the clause map's row 3 for the truth table, since
AC-25 imposes an obligation — per-row AC citation — that the clause map is the natural place to
discharge. (See also gap-coverage finding 4.)

#### 8. Four `## References` requirement lines omit ACs the body cites (Score: 52)

The seven `## References` blocks are otherwise in excellent shape — all 42 sibling descriptions match
the corresponding row of the sibling map byte-for-byte modulo bolding, and every ADR lists the other
six. The requirement lines drift.

**Evidence** (set-difference of `AC-\d+` in body vs. in the `- Requirements:` line): `0070` — body cites
AC-22 (`:32`) and AC-51 (`:417`); References (`:502`) lists neither. `0073` — body cites AC-35;
References does not. `0075` — body cites AC-23; References does not. `0074` — References lists AC-25;
body does not (finding 7).

**Recommendation**: Regenerate each requirement line from the body's citations rather than maintaining
it by hand.

#### 9. `ServiceProviderLifetimeScope.cs:520` points at a brace, not at `FailedToDisposeScope` (Score: 50)

**Evidence**: `0070:333` and `:395`. Source: `:519` `private static partial class Log`, `:520` `{`,
`:521` the `[LoggerMessage(LogLevel.Warning, …)]` attribute, `:522` the declaration.

**Recommendation**: `:522` (or `:521-522`). Also cited at `0071:256`.

#### 10. ADR 0072's frontmatter carries three tags where its six siblings carry four (Score: 30)

**Evidence**: `0072:9-13` (`di`, `lifetime`, `pipeline`) vs. `0070:9-14`, `0071:9-14`, `0073:9-14`,
`0074:9-14`, `0075:9-14`, `0076:9-14`. Given the set is otherwise byte-uniform in frontmatter, this
reads as an omission — and 0072 is the ADR that introduces the ambient/adoption concept and has no tag
naming it.

**Recommendation**: Add a fourth tag (`api-design` or a new `ambient-scope`), or confirm the asymmetry
is deliberate.

#### The FR → ADR ownership table

Built from each ADR's `Scope` paragraph. **D** = discharges, **S** = serves / cites but disclaims
ownership.

| Req | 0070 | 0071 | 0072 | 0073 | 0074 | 0075 | 0076 | Owner |
|---|---|---|---|---|---|---|---|---|
| FR-1 | **D** | | | | | | | 0070 |
| FR-2 | **D** | | | | | | | 0070 |
| FR-3 | **D** | | | | | | | 0070 |
| FR-4 | **D** | | | | | | | 0070 |
| FR-5 | **D** | S | | | | | | 0070 |
| FR-6 | **D** | S ("preserves") | | | | | | 0070 |
| FR-7 | **D** | S ("protects") | | | | | | ⚠ see set #4 |
| FR-8 | | | S | | | **D** | | 0075 |
| FR-9 | | | | | | **D** | | 0075 |
| FR-10 | | | **D** | S | | | | 0072 |
| FR-11 | | | **D** | | | | | 0072 |
| FR-12 | | | **D** | S | | | | 0072 |
| FR-13 | **D** (transform) | **D** (handler) | via FR-12 (borrowed) | | | | | split by family |
| FR-14 | | | | | | | **D** | 0076 |
| FR-15 | | | | **D** (pkg-inertness) | | | **D** (normative clause) | split |
| FR-16 / 16a / 16b | | | **D** | S | | | S | 0072 |
| FR-17 | | | | **D** (gesture) | **D** (eval site) | | **D** (write-through) | split 3 ways |
| FR-18 | | | **D** | S | | | S | 0072 |
| FR-19 | | | **D** | | | | S | 0072 |
| FR-20 | **D** | | | | | | S | 0070 |
| FR-21 | | | **D** | | | | S | 0072 |
| FR-22 | | | | | **D** | | S | 0074 |
| FR-23 | | | **D** | S | | | S | 0072 |
| FR-24 | | | **D** | | **D** (24.3 eval site) | | | 0072 (+0074) |
| FR-25 | | | | S (25.11) | **D** | S (25.5 substance) | S (25.11) | 0074 |
| FR-26 | | | **D** | | | | | 0072 |
| FR-27.1 | S | | **D** | | | | | 0072 |
| FR-27.2 | | | **D** | | | | | 0072 |
| FR-27.3 | | | S | | | **D** | | 0075 |

**NFRs** — `0070:32` states the set's position: NFRs are held by construction across all seven, not
distributed, and are named in a `Scope` only where they resolve to one decision. Under that rule:
**NFR-9** → 0074 (truth table, confirmed at `0074:34` and `0075:36`), **NFR-10** → 0074 (the only ADR
that cites it). NFR-1…NFR-8 are cited across the set with no single owner, by design. All ten are cited;
none orphaned.

**Defects the table exposes**
- **FR-7 is assigned to an ADR with no mechanism for it** — set #4. The only genuine ownership defect.
- **No requirement is claimed exclusively by two ADRs.** FR-13, FR-15 and FR-17 are the three deliberate
  splits, and in each case every ADR involved names the other holders and the reciprocal statements
  agree (all six directions checked for FR-17, all three for FR-13).
- **No FR-1…FR-27 is unclaimed**, and no clause of a multi-clause requirement falls in a gap.
- **The one requirement with no criterion behind it is acknowledged rather than hidden**: FR-13's
  transform-pipeline disposal clause has no AC (`0070:34`, `:417`), and 0070 declares a design-owed test
  in its place. `requirements.md:232` says FR-13 is "Discharged by AC-33", which is true only of the
  handler half — a requirements-side imprecision the ADRs correctly do not inherit.

#### The AC-1 … AC-51 sweep

**Complete, all 51.** Union of `AC-\d+` across the seven ADRs = exactly `AC-1 … AC-51`, contiguous.
**Orphans: none. Out-of-range citations: none. AC-51 is cited** — 0070 (`:417`) and 0071 (`:8`, `:291`,
`:334`, `:411`). Per-ADR: 0070 cites 14, 0071 8, 0072 20, 0073 12, 0074 14, 0075 11, 0076 6.

Weight-bearing citations spot-checked against the Given/When/Then:

| AC | Cited for | Verdict |
|---|---|---|
| AC-33 | 0071's handler scope-disposal-failure rule | ✅ covers it (`requirements.md:532`) |
| AC-51 | 0071's handler-release-failure rule | ✅ covers it (`:538`) — but see set #2, #3 |
| AC-7 | 0071's FR-6 release-exactly-once guarantee | ✅ that; ❌ **not** the rule `0071:360` attaches it to |
| AC-24 | the release-note ledger | ⚠ covers 4 of 12 bullets — set #6 |
| AC-45 | 0076's write-through | ✅ exactly that (`:744`) |
| AC-46 | 0071/0072's "must not be tested by nullness" | ✅ consistent — asserts over the recorder (`:792`) |
| AC-14 | 0071's "Explicitly NOT excluded" `FactoryLifetimeTests` pair | ✅ verbatim |

---

### Gap coverage — Alternatives, C/D/OOS sweep, AC fit

Commissioned after the set-level reviewer declared three gaps. 8 findings, 5 at or above threshold.
0 Critical, 4 High, 2 Medium, 2 Low.

#### 1. ADR 0075 alternative 3a rejects "public read, `internal` write" on a use case that does not exist anywhere in the ADR (Score: 72)

Alternative 3a is the narrow, obvious counter-proposal to a publicly writable suppression flag, and 0075
concedes it wins on the NFR-7 argument that carries alternative 3. Its rejection then rests on exactly
two grounds, one of which is a forward reference to text that is not there.

**Evidence**: `0075:313` — "**It satisfies NFR-7 completely** — NFR-7 needs a public *read*, and this
ADR everywhere else defines the NFR-7 case as reading the flag. It is rejected on two other grounds:
Brighter's own tests live in a separate assembly and this repository has no `InternalsVisibleTo` to
reach an internal mutator with, and **the host use case below needs the write as well as the read**."

There is nothing below `:313` that supplies a write use case. The section continues with alternative 4
(`:317`) and alternative 5 (`:319`), then `## References` (`:321`). Every place that *does* argue the
third-party-container case argues a **read**: `:79`, `:228`, `:282`, `:302`. So the surviving ground is
testability alone — which the same paragraph disclaims: "Testability is not a reason to widen a surface
where a seam already exists."

The repo convention both alternative 3 and 3a lean on **holds**: `grep -rn "InternalsVisibleTo" src
tests` returns exactly one hit, a comment in `SpannerBoxMigrationRunner.cs:131`, and no attribute or
`.csproj`/`.props` declaration anywhere. The problem is that after 3a's NFR-7 concession it is the
*only* thing left holding up the public write.

**Recommendation**: Either write the host use case the sentence promises (a concrete scenario in which a
container package Brighter does not ship must *set* suppression rather than read it), or drop that
clause and reject 3a on the convention plus testability, accepting that the public write is a
convention-driven choice rather than a design-forced one.

#### 2. ADR 0076 alternative 6 cites FR-14 as *requiring* a non-nullable value, which C-9 explicitly leaves open — and contradicts 0076's own alternative 2 (Score: 70)

**Evidence**: `0076:433` — "And **FR-14 requires a plain non-nullable value**, precisely so that
partially-initialised construction cannot produce an ambiguous state."

FR-14 requires no such thing. `requirements.md:259` gives the shape and flags it as provisional in the
same sentence: "Working name and shape for the FRs and ACs below (**provisional — see C-9**)". C-9
(`:373`) then says "**only the property's name, type and default expression are open**." A nullable
affinity is a change of type — precisely what C-9 hands to the ADR. FR-14 also says nothing about
"partially-initialised construction".

This contradicts 0076's own alternative 2, which uses C-9 correctly in the opposite direction: `:425` —
"Its advantage is real and is **the reason C-9 left it open**".

The rejection's other grounds are sound and sufficient — the tri-state collapse across five factories,
`ScopeAffinityPolicy` and validation, and the "banned by FR-17" ground, which was verified (FR-17's body
contains the sentinel ban at `requirements.md:274-275`, restated at `:758`).

**Recommendation**: Delete the FR-14 sentence or rewrite it as the ADR's own reasoning, and let FR-17
carry the ban. Do not cite a provisional shape as a requirement while citing C-9's openness three
alternatives earlier.

#### 3. AC-30's Given/When covers a `Send` only, but three ADRs cite it as the criterion for a rule stated over `Send`/`Publish`/`Post` and six builder catch sites (Score: 70)

**Evidence**: AC-30 in full (`requirements.md:512-515`) — "**Given** all three lifetimes `Scoped` … and
an `IAmAScopeProvider` whose `GetAmbient` throws `InvalidOperationException`, **When** `Send` is called,
**Then** the caller observes that `InvalidOperationException` unwrapped, and no pipeline scope is
leaked."

Against: `0072:198` — "A throw reaches the caller of **`Send`/`Publish`/`Post`** unwrapped … (FR-24.1,
AC-30)". `0072:358` — step 1b amends "**the six builder `catch` blocks** … `PipelineBuilder.cs:202` and
`:248`, `TransformPipelineBuilder.cs:116` and `:157`, and the same two lines in
`TransformPipelineBuilderAsync`"; AC-30's `Send` reaches exactly one of the six. `0070:236` cites AC-30
inside the **mapper/transformer** factory's contract row — the transform family, which AC-30 never
enters.

0071 notices the restriction and states it correctly for its own family: `0071:207` — "AC-30 is written
over a `Send` — **this family's pipeline**." That makes the inconsistency internal to the set.

**Recommendation**: Either widen AC-30 with a `Post` branch, or have 0070 and 0072 say explicitly that
AC-30 pins the handler-builder clause and the four transform-builder clauses are covered by construction
— the same honesty `0072:439` already shows when it declines to cite AC-8 for a borrowed handle.

#### 4. ADR 0074 claims step 7's truth table carries AC-25's per-row citation obligation; step 7 does not mention it, and drops one of AC-25's table dimensions (Score: 70)

**Evidence**: `0074:489` — "AC-25 (the guidance-page criterion, **whose per-row citation obligation step
7's truth table carries**)".

AC-25's second and third Then clauses (`requirements.md:695-696`) require that "every row of that table
**cites the AC that asserts it** (AC-13, AC-14, AC-15, AC-17, AC-18, AC-19, AC-20, AC-21, AC-26, AC-29,
AC-34, AC-39, AC-46, AC-47) or is marked as derived from a cited row" and that "any row that cites no AC
is itself a finding".

Step 7's clause-3 row (`0074:394`) states where the table's *substance* comes from. It does not mention
row-level AC citation, does not reference AC-25's list of fourteen, and does not say a row citing no AC
is a finding. None of the fourteen AC numbers appears in step 7.

The row also loses a dimension. AC-25 (`requirements.md:694`) requires the table "for each affinity
setting, each of `Transient`/`Scoped`/`Singleton`, **and the no-provider case**". Step 7 specifies "the
cross product of those rows with the three lifetimes and the two affinities" — the no-provider case is
absent from the only place in the set where the table's shape is fixed. FR-11(a)'s no-provider behaviour
is materially different from `AlwaysNew`-with-a-provider (no ask, no `Warning`), which 0072's own ladder
distinguishes.

**Recommendation**: Extend step 7's clause-3 row to state the third dimension and the per-row citation
obligation, naming AC-25's fourteen ACs as the permitted citation set. `requirements.md:700` makes AC-25
one of the four ACs with no automated signal, so an unstated obligation here is one nobody will catch.

#### 5. Six requirements-level constraints and out-of-scope items that bear directly on this design are cited by no ADR — including the one that licenses ADR 0075's entire mechanism (Score: 62)

The set cites `C-1`…`C-20` (all except C-13) and `D0`…`D19` (all), but only four of the fourteen
out-of-scope items: `OOS-4`, `OOS-7`, `OOS-8`, `OOS-14`. Several of the uncited ten are not incidental.

- **OOS-2 is the licence for ADR 0075's mechanism, and 0075 never cites it.** `requirements.md:393` —
  "**OOS-2 — A general `AsyncLocal`-based `IAmAScopeProvider` for non-ASP.NET hosts** … Out of scope …
  **Partially amended by D6**: an `AsyncLocal` **suppression** flag … *is* in scope, because FR-8
  requires it. Suppression is not adoption." 0075's whole decision is that flag, and its alternative 4
  argues the point OOS-2's amendment already settles. 0075 cites D6 and OOS-14 but not the amendment that
  puts its mechanism inside scope.
- **OOS-10 is 0075's subject and is uncited.** `requirements.md:401`.
- **OOS-12 names the alternative this set supersedes, and no ADR mentions it.** `requirements.md:403` —
  "**OOS-12 — Shutdown-hygiene disposal of the consumer factories by the `Dispatcher`** (the alternative
  suggested on #4254). **Superseded by per-pipeline scoping.**" That is the competing fix for 0070's
  Defect 1 (`0070:71`), raised on the originating issue and declared superseded — exactly what an
  Alternatives block exists to record.
- **OOS-13 is the ground 0074's alternative 5 argues without citing.** `requirements.md:404`; `0074:470`
  rejects eager validation partly because "it would fail startup for applications that never asked to be
  validated". 0074 cites C-15 but not OOS-13.
- **OOS-9** (`:400`) is the requirements-level record of the gap `0070:93` describes under C-3, and
  **OOS-3** (`:394`) the record of the third-party-container position all seven invoke through NFR-7.
- **C-13** (`requirements.md:385`) — the constraint that hands all of this to the ADRs — is cited by none.

Two claims checked and found **correct** rather than defective, worth recording because they read as
errors at first glance: 0070's reading of D12 matches the authoritative Terms row at `requirements.md:50`
even though the one-line D12 row at `:826` says only "both transform factories"; and 0076 alt 6's
"banned by FR-17" is right.

**Recommendation**: Add OOS-2's amendment to 0075's Context (it is the strongest single answer to "why is
an `AsyncLocal` acceptable in a design that bans ambient state?" — `0070:287` bans ambient state outright,
and OOS-2's amendment is the reconciliation), add OOS-10 beside D6 in 0075, add OOS-12 to 0070's
Alternatives, and cite OOS-13 in 0074 alternative 5.

#### 6. ADR 0074's Alternatives block hides two alternatives inside the gap between 3 and 2, and the second of them is never rejected (Score: 58)

Eight alternatives are numbered `**1.**` … `**8.**` (`0074:458, 460, 466, 468, 470, 472, 474, 476`). Two
more are argued in unnumbered prose between numbers 2 and 3, at `:462` and `:464`. The section presents
ten alternatives under eight numbers, and neither of the two can be cited or checked off.

The first unnumbered one is properly rejected. The second is not: it is conceded — "That is genuinely
tempting and it is the decorator with the collaboration hidden inside a closure: the same objects, the
same order, one fewer type" — and closes on a preference. There is no bolded **Rejected**, no
requirement, AC or constraint cited against it, and the one concrete cost offered is testability, which
`0075:313` declares is "not a reason to widen a surface". Every other alternative in this block carries
an explicit verdict and a cited ground.

All four Alternatives blocks in scope were read line by line, and this is the only rejection in
0073–0076 lacking a verdict. The others are strong: 0073's alternatives 4, 5 and 6 state the
counter-proposal in its strongest form and reject on a real property each time, and all three codebase
claims they turn on were verified — `AddProducers` and `AddControl` do extend `IBrighterBuilder`
(`ServiceCollectionExtensions.cs:247`, `:383`; `ControlExtensions.cs:11`), and no type in `src/` declares
`namespace Microsoft.Extensions.DependencyInjection`. 0076's alternatives 3, 4 and 7 are the strongest in
the set — 4 gives four separately verified costs and 3 explicitly refuses to offer a second objection
that would not distinguish the two designs.

**Recommendation**: Number the two container-free shapes as 3 and 4 and renumber the rest, and give the
delegate-bag alternative an explicit verdict with a ground stronger than testability — the honest one is
that a closure cannot be named in a `PipelineValidationResult`'s finding, which is the same argument that
rejects the probe interface two paragraphs earlier.

#### 7. Broken bold markers in ADR 0075 alternative 3a render as literal asterisks (Score: 40)

**Evidence**: `0075:313` — a four-asterisk run opens a bold span that is already open; the clause ends
"…needs the write as well as the read.**", closing the outer span. The paragraph's key concession renders
with visible `**` in every Markdown renderer.

**Recommendation**: `**It satisfies NFR-7 completely** — NFR-7 needs a public *read*…`, with the
enclosing bold run removed. (Same paragraph as 0075 #3.)

#### 8. ADR 0075 alternative 5 names a harm AC-12 does not detect among the harms it says AC-12 detects (Score: 38)

**Evidence**: `0075:319` — "so **the resolutions and the dispatch-time `Post`s that prefix issues** are
unsuppressed — which **AC-12's nested-`SendAsync` and resolution-time clauses do detect**." AC-12
(`requirements.md:487-496`) has a resolution-time clause and a nested `InnerCommand` `SendAsync` clause.
It has no dispatch-time `Post` issued from inside a subscriber; its only `Post` is in the leak clause,
"issued from the controller **outside any subscriber**" (`:495`).

**Recommendation**: Drop "and the dispatch-time `Post`s that prefix issues" from the list of harms
attributed to AC-12, or say plainly that the `Post` half is covered by construction rather than by
criterion.

#### Coverage and declared gaps

**Alternatives** — all 30 alternatives across 0073 (7), 0074 (8 numbered + 2 unnumbered), 0075 (6,
numbered 1, 2, 3, 3a, 4, 5) and 0076 (7) read in full. Do-nothing present in all four and honest in each.
0070, 0071 and 0072's blocks were read in full by the set-level reviewer.

**C/D/OOS sweep — complete.** Union cited = `C-1…C-20` less C-13, `C-12a`, `D0, D0b, D0c, D1…D19`,
`OOS-4, OOS-7, OOS-8, OOS-14`. Defined in `requirements.md`: constraints `:365-388`, decisions `:817-835`,
out-of-scope `:392-405`. **Zero non-existent citations, zero out-of-range citations, across all seven
ADRs.** Use-matching spot-checked by opening the requirement text for C-1, C-2, C-3, C-5, C-7, C-8, C-9,
C-10, C-12, C-12a, C-14, C-15, C-16, C-17, C-18, C-19, C-20; D3, D6, D8, D9, D10, D11, D12, D13, D15,
D16, D17, D18, D19; OOS-2, OOS-4, OOS-7, OOS-8, OOS-9, OOS-10, OOS-12, OOS-13, OOS-14. C-4, C-6 and C-11
were read but not traced to every citing sentence.

**AC fit — a prioritised sample, not a sweep.** Opened in full and checked against every citing sentence:
AC-25 (defect, #4), AC-30 (defect, #3), AC-31 (**OK** — `0072:310`'s claim that a process-static latch
"makes AC-31's `AlwaysNew` branch vacuous" is exactly right), AC-34 (**OK** — 0072's FR-16b claim matches
the `Post`-then-`Send`-in-one-action Then verbatim), AC-37 (**OK**, and unusually well fitted —
`0072:287`'s `TryAddScoped` claim maps precisely onto clause 2, and `0072:478`'s "three clauses" is the
right count), AC-47 (**OK** — 0075 alt 2's "AC-47's second branch is exactly that configuration" is
exact), AC-12 (one over-claim, #8). Headers confirmed for AC-48, AC-49, AC-46, AC-13, AC-42. **The
remaining ~30 ACs were not opened**; a fit defect in one of those would not have been caught.

**Also not done**: `file:line` citations *inside* the four Alternatives blocks were not re-verified against
source beyond the three codebase claims named above. No .NET probe was built; 0075 alt 5's
`ExecutionContext` claim was not independently tested here (it is covered by the 0075 reviewer's probe
suite and by the set-level reviewer's).
