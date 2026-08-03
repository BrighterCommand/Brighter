# Review: design — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-03
**Threshold**: 60
**Scope**: all five ADRs in `.adr-list` (0070–0074) plus set-level cross-reference
**Verdict**: NEEDS WORK

33 findings at or above threshold 60. Address these before approving.

## How this review was run

Six independent reviewers: one per ADR, plus one whose remit was only the set-level properties
(requirements coverage, contradictions between ADRs, the sibling maps, the unifying sentence,
heading-skeleton drift). Every `file:line` citation in every ADR was verified against the
codebase. Every mermaid block was extracted and rendered with
`@mermaid-js/mermaid-cli@11`; the most complex in each file was rendered to PNG and looked at.

**All 12 mermaid blocks render** (0070: 2, 0071: 3, 0072: 2, 0073: 3, 0074: 2). No parse
failures. `grep -c '&lt;\|&gt;\|&amp;'` is 0 on all five files. Two diagrams parse cleanly but
mislead — findings 16, 39 and 53.

**Two set-level properties are clean and worth recording**, because both are the kind that
usually rot: every one of the five `### Where this ADR sits` tables lists all five ADRs with its
own row bolded and marked *(this one)* — no stale map anywhere. And a grep of all five for
authoring-conversation phrasing ("at the user's direction", "we agreed", "per the user's
feedback") and ephemeral state (`PROMPT.md`, spec phase, review rounds) returns **zero hits**.
Frontmatter is uniform, all five are registered in `docs/adr/index.md:107-111`, and 0071–0074
match the canonical heading skeleton exactly.

## Findings by ADR

| ADR | Critical | High | Medium | Low | Total | ≥60 |
| --- | --- | --- | --- | --- | --- | --- |
| 0070 | 1 | 3 | 6 | 1 | 11 | 7 |
| 0071 | 0 | 3 | 6 | 3 | 12 | 6 |
| 0072 | 1 | 3 | 7 | 2 | 13 | 6 |
| 0073 | 1 | 3 | 4 | 1 | 9 | 5 |
| 0074 | 0 | 2 | 6 | 1 | 9 | 4 |
| set-level | 0 | 3 | 4 | 2 | 9 | 5 |
| **Total** | **3** | **17** | **33** | **10** | **63** | **33** |

Four findings were merged across reviewers where two of them found the same defect from different
angles; each is filed once, under the ADR it must be fixed in. Findings 51 and 55 are filed as
set-level because the same edit lands in more than one file.

## Findings

### 1. [0073] The double-call mitigation rests on a diagnostic FR-24.3 explicitly excludes (Score: 92)

The ADR's entire answer to "what happens when `AddBrighterRequestScope` is called twice with
different affinities" is that the resulting inconsistency is *diagnosable* via FR-24.3's
duplicate-provider warning. It is not. Two calls register the **same** implementation type
(`HttpContextScopeProvider`) twice, and FR-24.3 says in terms that this is not a finding. The
claim is load-bearing in three places — the contract row, the `#### The ASP.NET package and its
extension` rationale, and a *Negative* consequence. Remove it and the double-call case is
silently wrong with no signal at all.

**Evidence**: ADR:329 — *"the same two calls also add **two** `IAmAScopeProvider` descriptors of
the same implementation type, which is precisely FR-24.3's duplicate-provider condition"*.
Against `requirements.md:244` (FR-24.3) — *"When the service collection holds descriptors for
`IAmAScopeProvider` with **more than one distinct implementation type** … **Registering the
*same* implementation type more than once is idempotent in effect and is not a finding.**"* ADR
0074 restates the exclusion correctly: *"the *same* implementation type registered twice is not a
finding (AC-32's second branch)."*

**Recommendation**: Either drop the diagnosability claim and record the double-call asymmetry as
genuinely silent — which strengthens the case for making `ScopeAffinityOverride` a plain
`AddSingleton` (last wins, matching the provider) so both halves agree and the asymmetry
disappears — or state that this ADR adds a *new* rule for a duplicate `ScopeAffinityOverride` and
hand it to 0074 explicitly.

---

### 2. [0072] Provider-throw "propagates unwrapped" is impossible on the specified path, and contradicts ADR 0070 (Score: 92)

Three places say a throw from `GetAmbient` reaches the caller of `Send`/`Publish`/`Post`
**unwrapped**. But the ADR puts the ask inside `CreatePipelineScope()`, and every call site of
that member sits inside a builder `try` that wraps every non-`ConfigurationException` into a
`ConfigurationException`. AC-30 fails as the design stands.

**Evidence**: ADR:95 (ladder row 3), :225 (contract table), :388 (pseudo-code) all say
"unwrapped". Against `PipelineBuilder.cs:202-204` — `catch (Exception e) when (e is not
ConfigurationException) { throw new ConfigurationException(...) }`, async twin at `:248-250`; the
handler-pipeline ask reaches `CreatePipelineScope()` via `GetSyncInstanceScope()`
(`PipelineBuilder.cs:190`, `:235`), inside that `try`. Same for transform pipelines at
`TransformPipelineBuilder.cs:116-124`, `:157-165`. And ADR 0070:220 states the opposite for the
same member: *"the builder's existing `catch` turns that into `ConfigurationException`"*.
`requirements.md` AC-30: *"**Then** the caller observes that `InvalidOperationException`
unwrapped"*.

**Recommendation**: Decide the mechanism and record it in `Where each type is touched`, which
today lists no exception-handling change to either builder. Either narrow the two catch filters
to let a designated ambient-source fault through (sentinel exception type, or an exception
filter), or move the ask outside the builder `try`. Then reconcile 0070's contract row — the two
ADRs currently specify contradictory behaviour for one member.

---

### 3. [0070] "Already log at `Error`" is false — every release site logs `Warning`, and AC-6 requires `Error` (Score: 90)

The `IAmAScope` *Error conditions* bullet asserts a code fact that is not true, then uses it to
conclude a requirement is discharged. The same false premise underwrites the AC-6 claim in
`Implementation Approach` step 4. As written the ADR says nothing needs to change and AC-6 would
fail.

**Evidence**: ADR:187 — *"the existing release call sites … **already log at `Error`** and
swallow"*. Verified: `OutboxProducerMediator.cs:1448` is `[LoggerMessage(LogLevel.Warning, ...)]`;
`Reactor.cs:637` and `Proactor.cs:651` likewise. All four release sites log `Warning`. Compounding
it, ADR:302 says release failures are handled by the existing guard — but
`TransformPipelineBuilder.cs:408` declares `FailedToCleanUpAfterFailedBuild` at
`LogLevel.Warning`, while AC-6 (`requirements.md:442-446`) and FR-13 require `LogLevel.Error`.

**Recommendation**: Correct the claim, and add the level change to `Implementation Approach` and
`Where each type is touched` — `FailedToCleanUpAfterFailedBuild`
(`TransformPipelineBuilder.cs:408`, `TransformPipelineBuilderAsync.cs:317`) and
`FailedToReleasePipeline` (`OutboxProducerMediator.cs:1448`, `Reactor.cs:637`, `Proactor.cs:651`)
move to `LogLevel.Error` — or state explicitly that AC-6/FR-13 are 0072's to satisfy and that
0070 leaves them failing.

---

### 4. [0074] The captive-dependency exclusion set has no owner, no capture point, and no disposal story (Score: 82)

The constructor sketch passes `exclusions` in and never says who builds it, when, from what
collaborators, or who disposes what building it creates. It is the one input that cannot be read
from a `ServiceDescriptor` snapshot, and the only one left unsited.

**Evidence**: three gaps. (a) No collaborator — building the set needs a
`PipelineBuilder<IRequest>` *and* a `MessageMapperRegistry`
(`TransformPipelineBuilder.DescribeTransforms` takes one as its first parameter,
`TransformPipelineBuilder.cs:270`); neither appears in the sketch, the roles table, or
`Where each type is touched`. (b) No implementation step — steps 1–7 never mention it, although
*Negative* prices it ("One `Describe()` pass for the exclusion set"). (c) A disposal
contradiction with the ADR's own centrepiece — `PipelineValidator` holds the registry as
`Lazy<MessageMapperRegistry>` (`Validation/PipelineValidator.cs:69-71`) and disposes it only
`if (_mapperRegistry is { IsValueCreated: true })` (`:92-93`). Since *Technology Choices* says
"Nothing is read lazily during `Validate()`", the decorator must compute exclusions inside the
factory delegate and therefore build a **second** `MessageMapperRegistry` with its own
`ServiceProviderMapperFactory` and `ServiceProviderLifetimeScope` — after two paragraphs and a
Risks row about the inner validator's registry leaking to process exit.

**Recommendation**: Give the exclusion set a named role and type (e.g.
`BrighterArtefactExclusions`, built by an `ArtefactExclusionSetBuilder` in the DI package), add it
to the roles table, `Where each type is touched` and `Implementation Approach`, and say which
`MessageMapperRegistry` instance it uses and who disposes it. If the answer is "share the inner
validator's `Lazy`", that is a change to `PipelineValidator` the ADR currently claims is untouched.

---

### 5. [0070] `ServiceProviderPipelineScope.DisposeAsync()` has nothing async to call (Score: 82)

The prescribed container-package implementation cannot do what the ADR says, and the type that
would have to change is in neither the changed nor the deliberately-unchanged list.

**Evidence**: ADR:308 — *"disposes it exactly once under either `Dispose()` or `DisposeAsync()` …
preferring the scope's `IAsyncDisposable` where offered"*. But
`ServiceProviderLifetimeScope.cs:42` is `internal sealed partial class ServiceProviderLifetimeScope
: IDisposable` — no `IAsyncDisposable`. Its only whole-object disposal is `public void Dispose()`
(`:462`), draining through the synchronous `DisposeScope` (`:484`, `:497`). `ReleaseAsync`
(`:361`) is per-release-token only and returns `default` on the `Scoped` path where the token is
always `null` (`:136`). So `DisposeAsync()` would block on a synchronous dispose — exactly the
Proactor stall that Alternative 8's rejection is built on.

**Recommendation**: Add `ServiceProviderLifetimeScope` to the touched table with the change it
needs (async whole-scope disposal routing the root and outstanding scopes through the existing
`DisposeScopeAsync` at `:449`), or state that `DisposeAsync()` deliberately blocks and reconcile
that with Alternative 8.

---

### 6. [0071] Async handler pipelines have no async disposal path, yet the ADR claims full convergence (Score: 80)

`IAmAScope` is `IDisposable, IAsyncDisposable`, and 0070's Alternative 8 rejects an
`IDisposable`-only handle *specifically* because a blocking release stalls the Proactor. ADR 0071
routes the handler handle through `IAmALifetime : IDisposable` → `HandlerLifetimeScope.Dispose()`
→ `PipelineBuilder.Dispose()`, none of which has an async path, and never mentions the word.

**Evidence**: `grep -i "IAsyncDisposable\|DisposeAsync"` over the ADR → **0 hits**.
`IAmALifetime.cs:34` is `IDisposable`; `IAmAnAsyncPipelineBuilder.cs:37` likewise; both **async**
paths use `using var builder` not `await using` (`CommandProcessor.cs:394`, `:575`). Meanwhile
*Consequences* claims *"**One mechanism.** … One story to teach, one ordering rule, one handle
type."* The transform family gets `TransformPipelineDrain.DrainAsync`; the handler family gets no
twin. Since NFR-1's signature freeze was withdrawn, "we can't" is no longer the answer.

**Recommendation**: State the decision. Either the handler handle is disposed synchronously and
the ADR says why the Proactor argument does not bite here (e.g.
`ServiceProviderLifetimeScope`'s synchronization-context suppression at `:422-436`), recorded as a
documented asymmetry in *Negative* with the "one mechanism" claim softened — or `IAmALifetime` and
`PipelineBuilder` gain `IAsyncDisposable` and `CommandProcessor` moves to `await using`, which
contradicts the current "unchanged" entries and must be priced.

---

### 7. [0073] The thread-safety claim contradicts the ADR's own §289 (Score: 78)

`### Technology Choices` closes with an unqualified claim that no reader can observe a
half-configured options object. Twelve paragraphs earlier the ADR states the opposite for the
consumer `Action` path and argues at length why it is tolerable.

**Evidence**: ADR:382 — *"**No reader can observe the object in a half-configured state
(NFR-4).**"* Against ADR:289 — *"on the `Action` path both service types name the *same*
`ConsumersOptions` instance … **between a first resolution of `IAmConsumerOptions` and a first
resolution of `IBrighterOptions`, that object's `DefaultScopeAffinity` still holds whatever the
application set.**"* Verified: `ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:38-39`
registers the same instance for both service types.

**Recommendation**: Narrow the Technology Choices claim to what MS DI actually guarantees — no
reader holding the reference *the `IBrighterOptions` factory returns* — and cross-reference §289
for readers that reach the same object another way.

---

### 8. [0071] `SimpleHandlerFactory` is missing from the touched-types table, and all three implementation counts are wrong (Score: 78)

The ADR says `IAmAHandlerFactory` has "19 implementations here: 4 in `src/`, 15 test doubles". The
real numbers are 5 and 16, and the missing `src/` class is **public**.

**Evidence**: a multi-line sweep (single-line greps miss it — its base list is on the next line)
finds `SimpleHandlerFactory` at `src/Paramore.Brighter/SimpleHandlerFactory.cs:11` —
`public class SimpleHandlerFactory(...) : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync` — a
public core type that will not compile once `CreatePipelineScope()` is added, and which is in
neither the touched table nor the "unchanged, named so the omission is not read as an oversight"
list. Test doubles are 16; the 16th is `DummyHandlerFactory` at
`tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/When_There_Is_No_Sync_Or_Async_Handler_Factories.cs:56`
— `sealed class DummyHandlerFactory : IAmAHandlerFactory;`, a body-less implementation of the bare
marker, which is a distinct break class the ADR does not call out. (`IAmALifetime`'s count of 7 is
exactly right.)

**Recommendation**: Add a `SimpleHandlerFactory` row (`CreatePipelineScope()` returns `null`),
correct the counts to 21 / 5 / 16 in both *The forces* and *Negative*, and note that
implementations of the bare `IAmAHandlerFactory` marker must gain a body.

---

### 9. [0070] The scope is acquired outside the `try`, contradicting the contract table (Score: 78)

Two developers would implement the failure-to-create-a-scope case differently, because the ADR
states it twice, incompatibly.

**Evidence**: contract table, ADR:220 — *"may throw …; **the builder's existing `catch` turns that
into `ConfigurationException`**"*. But step 3's snippet places `var scope = CreatePipelineScope();`
*before* the `try`. Verified against `TransformPipelineBuilder.cs:93-125`: the `catch (Exception e)`
at `:116` covers only the block opened at `:98`, and `:124` is the only site producing the
`ConfigurationException`. A throw as written escapes raw — AC-5 requires
`ConfigurationException` with the original as inner.

**Recommendation**: Move the acquisition inside the `try` in the snippet (declaring
`IAmAScope? scope = null` alongside the existing declarations at `:95-97`), or change the contract
table to say the throw propagates unwrapped and reconcile that with AC-5.

---

### 10. [0072] This is two decisions, and the ADR says so itself (Score: 74)

The contract: *"State the unifying rule once, in one sentence … If it will not fit in one
sentence, it is not yet one decision."* Both siblings' `Where this ADR sits` tables assert "Five
ADRs deliver the parent requirement, **one decision each**." This Decision is a ~90-word compound
sentence joined by a semicolon.

**Evidence**: ADR:83 — *"Two behaviours are decided here, and **they are independent of one
another**."* The `Scope` paragraph adds a third ("which object computes a pipeline's
`ScopeAffinity`"). `The mechanism, end to end` carries two `####` sub-sections with no shared
invariant, and `Alternatives Considered` splits cleanly into adoption alternatives (1, 2, 3, 4, 7,
9, 10) and suppression alternatives (5, 6, 8) with no overlap. At 73KB it is the largest file in
the set.

**Recommendation**: Split `Publish` suppression into its own ADR — it has its own forces
(`AsyncLocal` vs a parameter, the public-for-write cost, `Parallel.ForEach`'s `ExecutionContext`
restore), its own requirements (FR-8, FR-9, NFR-4), its own alternatives and its own consequence.
What stays in 0072 is the seam: the ladder, the role interface, the policy, the cache, the
latches. If the split is refused, compress the Decision to one sentence and say in `Scope` *why*
two independent behaviours are one record.

---

### 11. [SET] FR-25's guidance page is an 11-clause deliverable with no owning ADR (Score: 72)

FR-25 requires `docs/guides/lifetimes-and-scoping.md` and enumerates eleven mandatory contents.
NFR-10 makes it the *acceptance bar* for the whole opt-in, and every FR-22/FR-24.3 message 0074
specifies is required to name it. No ADR owns it. What exists is five obligations discharged as
side-notes: 0073 step 6 (FR-25.11), 0074 step 7 (FR-25.10), and passing citations of .6, .8, .9.
Clauses **.1** (the get/release cycle for all three lifetimes), **.2**, **.3** (NFR-9's truth
table), **.4** (the `IAmAScope`/`IAmALifetime` distinction — NFR-8), **.5** (`Publish` subscribers
cannot join a caller's transaction — C-4) and **.7** (the joint-lifetime rule) have no owner at
all. Clause .9's decision guide — the largest piece of writing FR-25 mandates — is named by 0074
only as something an error message must *reach*.

**Evidence**: `grep -c 'FR-25'` → 0070:1, 0071:1, 0072:3, 0073:9, 0074:10; every 0073/0074 hit is
a sub-clause reference. 0074:331 is the closest any ADR comes to owning the page and scopes itself
to one clause.

**Recommendation**: Either add a paragraph to 0074's `Scope` — it is the ADR whose errors are
unactionable without the page — stating that FR-25's page is an implementation-plan deliverable,
not an ADR-level decision, and listing which ADR supplies each clause's substance; or accept that
FR-25.9's decision guide is genuinely architectural and give it a home in 0074 beside the FR-22.2
rule it exists to make actionable. Do not leave it distributed across two `Implementation
Approach` steps.

---

### 12. [0074] AC-45 is cited as the acceptance evidence for a decision it does not assert (Score: 72)

The central "read the resolved `IBrighterOptions`, never `IOptions<BrighterOptions>.Value`"
decision is justified by AC-45 in three places. AC-45 asserts nothing about validation.

**Evidence**: `requirements.md:725-732` — AC-45's subject is the affinity option on the resolved
`IBrighterOptions` and adoption behaviour across four registration paths; it is ADR 0073's
acceptance criterion, and 0073 cites it for exactly that at its :378. No AC in requirements.md
asserts what the *validator* reads — AC-27/28/40/41 all use a single registration path.

**Recommendation**: Either state plainly that no AC pins this ADR's input-source choice, and flag
it as a gap needing a spec amendment, or reword to "AC-45 pins the affinity on the resolved
options object; this ADR reads that same object" — true, and weaker than the claim made.

---

### 13. [0073] The `IOptions` path mutates a framework-owned shared object, and that is nowhere named (Score: 72)

On `AddBrighter(Action<BrighterOptions>)` the object `RegisterBrighterOptions` mutates is
`IOptions<BrighterOptions>.Value` — a singleton the options machinery owns and hands to *anyone*
resolving `IOptions`/`IOptionsSnapshot`/`IOptionsMonitor`, not only to Brighter. The *Negative*
bullet on this hazard enumerates three of the four paths and omits the one where the shared object
is not Brighter's.

**Evidence**: ADR:417 — *"On the two `Func` paths and the consumer `Action` path the application
constructs the options object itself."* Verified at
`Extensions.DependencyInjection/ServiceCollectionExtensions.cs:69-75` —
`AddOptions<BrighterOptions>()`, `Configure(configure)`, then
`TryAddSingleton<IBrighterOptions>(sp => sp.GetRequiredService<IOptions<BrighterOptions>>().Value)`.
The ADR's rewrite wraps exactly that delegate.

**Recommendation**: Extend the bullet to all four paths and say that on the `IOptions` path a
`PostConfigure`-style reader or a diagnostic dump can observe either value depending on resolution
order. This also matters to 0074, which reads `IBrighterOptions` post-write.

---

### 14. [0071] A non-null `PipelineScope` the factory does not recognise leaves the pipeline holding one scope and resolving from another (Score: 72)

Step 4 disposes of the case in a subordinate clause, with no error condition and no consequence.

**Evidence**: *"…resolve through `lifetime.PipelineScope` when it is a
`ServiceProviderPipelineScope`, falling back to `GetOrCreateLifetimeScope` when it is not."* When
`PipelineScope` is non-null but foreign (a third-party `IAmAScope` per NFR-7/OOS-3, a hand-rolled
`IAmALifetime`, or an ambient shape this factory does not accept after 0072), the factory silently
creates a **second** `ServiceProviderLifetimeScope` in `_lifetimeScopes`
(`ServiceProviderHandlerFactory.cs:127-131`) while `HandlerLifetimeScope.Dispose()` also disposes
the handle. Two DI scopes for one pipeline, against NFR-5 and NFR-6 (`requirements.md:351-352`),
silently — the failure mode 0070's Alternative 1 was rejected for. The second scope is disposed
only on the *first* `Release`, reproducing the latent leak this ADR claims to close.

**Recommendation**: Give `CreatePipelineScope()`/`PipelineScope` a contract table with an explicit
row for "handle present but unrecognised", stating the outcome, whether a second scope is created,
who disposes it, and whether a diagnostic fires. 0070's ignore-path row is the precedent to mirror
— but 0070's ignore costs nothing, whereas here it costs a DI scope.

---

### 15. [0070] "Call sites are unaffected" is true of source only — the change is binary-breaking, per NFR-1(c) (Score: 72)

The ADR prescribes the release-note wording, so the inaccuracy propagates into user-facing
guidance. It is also internally inconsistent: the next Negative bullet gets the distinction right.

**Evidence**: ADR:358 — *"**Call sites are unaffected, because the parameter is defaulted.**"* A
defaulted parameter is compiled into the call site — which the ADR itself states at :224 — so an
already-compiled caller binds to a method that no longer exists. `requirements.md` NFR-1(c): *"The
break is a **source and binary breaking change** for any application that implements one of the
six by hand"*. Contrast ADR:370 on the pipeline constructors, which frames it correctly.

**Recommendation**: Restate as "source-compatible for call sites; binary-breaking for any caller
or implementer not recompiled", citing NFR-1(c) and AC-24.

---

### 16. [SET] `CreatePipelineScope()`'s documented ownership contract is falsified by 0072, and no ADR amends it (Score: 70)

0070 and 0071 both ship an XML doc comment saying **"The caller owns the returned scope and must
release it"**, and 0070's contract table gives the output as *"a new, **owned** `IAmAScope`"*.
0072's whole point is that the member sometimes returns a scope the caller does **not** own
(ladder row 9: *"**BORROWED** — resolve from it, own nothing, dispose nothing"*). 0072's
*Unchanged* list says *"no member is added to any of them here"* — literally true, and silently
redefines the member's semantics. The hazard is concrete: a developer implementing "the caller
owns and must release it" over a borrowed `HttpContext.RequestServices` disposes the caller's
request scope, violating FR-12.

**Evidence**: `grep -n 'owns the returned scope'` → `0070:199` and `0071:189`, identical wording,
against 0072's ladder row 9 and its flowchart node *"borrowed: resolves from Services and disposes
NOTHING"*.

**Recommendation**: Restate the contract once, in whichever ADR is canonical, in terms that
survive 0072: *"returns a handle the caller must always release; releasing may or may not dispose
an underlying scope, and the handle alone knows which."* Amend both doc comments and 0070's
contract table, and add a line to 0072's *Unchanged* paragraph saying the signature is unchanged
but the ownership contract is widened here.

---

### 17. [SET] FR-20 and NFR-1(c) — the breaking-change record has no owner, and 0070 cites FR-20 without deciding anything about it (Score: 70)

0070 makes the break: six public interfaces, plus `MapperLifetime.Scoped` changing from
process-lifetime caching to per-pipeline. FR-20 requires the behavioural break in
`release_notes.md`; NFR-1(c) requires the six-interface source-and-binary break recorded there,
naming each interface and its migration (AC-24). 0070 lists FR-20 in References but its Decision,
Implementation Approach and Consequences say nothing about release notes, and it never cites NFR-1
at all — the requirement that authorises and constrains its central breaking change.

**Evidence**: `grep -n 'release_notes' docs/adr/007*.md` → hits in 0073 and 0074 only, each
recording its own smaller break. `grep -c 'NFR-1\b'` → 0070:0, 0071:0. This is the set's only
clear claimed-but-not-delivered instance.

**Recommendation**: Add a numbered step to 0070's `Implementation Approach` recording both breaks
(FR-20 behavioural; NFR-1(c)/AC-24 six-interface), add NFR-1 to its References, and cross-reference
from 0073:396 and 0074:331 so the breaks read as one release-note entry.

---

### 18. [0073] The new package's targeting and packaging are asserted without grounding, and there is no precedent in the repo (Score: 70)

Step 4 specifies *"A `netstandard2.0`-and-up class library referencing
`Paramore.Brighter.Extensions.DependencyInjection` and `Microsoft.AspNetCore.Http`"*. That is not
implementable as written.

**Evidence**: `src/Directory.Build.props:43` —
`<BrighterTargetFrameworks>netstandard2.0;net8.0;net9.0;net10.0</BrighterTargetFrameworks>`.
`grep -rn "AspNetCore" --include="*.csproj" src` → **no matches**; `grep -rn "FrameworkReference"`
→ **no matches**. `Directory.Packages.props` has no `Microsoft.AspNetCore.Http` entry, and central
package management requires one. On `netstandard2.0` the only shippable `Microsoft.AspNetCore.Http`
is the end-of-life 2.2.x line; on `net8.0`+ the types come from the shared framework via
`<FrameworkReference Include="Microsoft.AspNetCore.App"/>` — different mechanisms per TFM, so this
is a conditional-ItemGroup decision, not a one-line reference. Minor: `IHttpContextAccessor` is
declared in `Microsoft.AspNetCore.Http.Abstractions`.

**Recommendation**: State which TFMs the package targets and, per TFM, whether it uses a
`PackageReference` or a `FrameworkReference`, plus the `Directory.Packages.props` entry. If
`netstandard2.0` is not worth the 2.2.x dependency, say the package is `net8.0+` only and record
that as a deliberate departure from `BrighterTargetFrameworks`.

---

### 19. [0072] `ScopedArtefactCache` becomes shared state across concurrent pipelines, with no contract and no thread-safety statement (Score: 70)

The central structural move — taking `_scopedInstances` off the per-pipeline handle and making it
a request-`Scoped` service — converts a cache confined to one pipeline into one contended by every
concurrent pipeline in an HTTP request. That is squarely inside NFR-4, and the type gets no
contract table, no members, no concurrency semantics. Same omission for `AmbientScopeDiagnostics`
(three shared, concurrently-written latches) and `ScopeAffinityPolicy`.

**Evidence**: the documentation contract requires *"each significant type with a contract table
(Member / Input / Output / Error conditions)"* under `### Key Components`. `IAmAScopeProvider`,
`IAmAServiceProviderScope` and `AmbientScopeSuppression` have one; the other three do not. Today's
implementation gets its safety from a per-pipeline `ConcurrentDictionary<Type, Lazy<object?>>`
(`ServiceProviderLifetimeScope.cs:49`) plus the `Lazy` publish protocol at `:163-178`; nothing
says whether `ScopedArtefactCache` preserves that, nor what "one instance per type" means when two
pipelines race the first resolution. AC-11 asserts *exact* warning counts, and `WarnOnce` is
unspecified as to atomicity.

**Recommendation**: Add contract tables for all three. For `ScopedArtefactCache`, state the member
set, the concurrency guarantee, and what `Dispose` does under a concurrent `GetOrAdd`. For
`AmbientScopeDiagnostics`, state that `WarnOnce` is atomic per (condition, provider type) so
AC-11's counts hold under a concurrent `Publish`.

---

### 20. [0072] The `Where the pieces live` diagram attributes the type test to the wrong object (Score: 70)

The prose says the *factories* type-test for `IAmAServiceProviderScope`; the diagram says
`ServiceProviderPipelineScope` does.

**Evidence**: ADR:260 — *"**The four container-backed transform factories and the handler factory
type-test for the interface**"*; pseudo-code at :397 and the roles table at :192 agree. But the
diagram's last edge, :176, is `pipescope -. "type-tests for" .-> role`, and the rendered PNG
confirms the arrow leaves the `ServiceProviderPipelineScope` node.

**Recommendation**: Change :176 to `facs -. "type-tests for" .-> role`.

---

### 21. [0070] NFR-1 — the requirement that authorises the entire breaking change — is never cited (Score: 68)

**Evidence**: the forces bullet at :76 paraphrases NFR-1 almost verbatim but cites only ADR 0014
and NFR-7; the bullet at :77 ("The interfaces are alterable, and the cost is understood") cites
**no requirement at all** — it is NFR-1's withdrawal clause. The References line lists neither
NFR-1 nor AC-24. NFR-3 is likewise the source of the *"`ServiceActivator` keeps its single project
reference"* Positive bullet and is uncited.

**Recommendation**: Cite NFR-1 on both bullets, name obligations (a)/(b)/(c) where discharged,
cite AC-24 on the release-note consequence, and add NFR-1, NFR-3 and AC-24 to References.

---

### 22. [0071] "Today it releases handlers and cannot fail meaningfully" is factually wrong, and under-scopes the work (Score: 68)

The *Negative* bullet mis-states the baseline, making the new error handling look additive when it
is a repair of an existing defect.

**Evidence**: `HandlerLifetimeScope.cs:74-93` — `Dispose()` is two bare `.Each(...)` loops calling
`_handlerFactorySync?.Release(trackedItem, this)` with **no try/catch anywhere**, then
`_trackedObjects.Clear()`. A user factory's `Release` can throw —
`SimpleHandlerFactorySync.Release` (`:40-44`) calls `disposable?.Dispose()` — and a throw from the
first tracked handler aborts the loop, skips every remaining `Release`, and skips both `Clear()`
calls. The transform family already has this fixed and regression-guarded
(`tests/Paramore.Brighter.Core.Tests/MessageSerialisation/When_a_transform_release_throws_the_scope_still_releases_the_rest.cs`).
Step 2 asks only that a throwing release not prevent scope disposal — not handler-to-handler.

**Recommendation**: Correct the bullet, extend step 2 so the per-handler loop is fault-tolerant
(release every handler, compose failures, then dispose the handle), and add the ADR-0068-shaped
regression test to step 6 mirroring the transform test above.

---

### 23. [0072] Ladder row 1 is ambiguous between a per-factory and a per-pipeline test, and one reading contradicts the ADR's own rule (Score: 68)

**Evidence**: :93 reads as a pipeline-level test — *"mapper **or** transformer not `Scoped`"* —
restated in the pseudo-code at :377-378. Against :421 — *"A `{Scoped mapper, Singleton
transformer}` transform pipeline **adopts**"*. They reconcile only if row 1 means "this one
factory's own lifetime", which further requires 0070's first-non-null routing (`0070:298`) to
guarantee exactly one ask per pipeline (D16, AC-13). That routing appears nowhere in 0072's body,
only obliquely in a References bullet at :529.

**Recommendation**: Reword row 1 and the pseudo-code to "**the factory being asked** is not
`Scoped` (mapper factory: `MapperLifetime`; transformer factory: `TransformerLifetime`; handler
factory: `Singleton`)", and add a sentence in step 2 stating that the single ask is guaranteed by
0070's first-non-null routing, walking `{Transient mapper, Scoped transformer}` explicitly.

---

### 24. [SET] 0070 and 0071 both route FR-22 to the wrong ADR (Score: 68)

**Evidence**: 0070 `Scope` — *"or the `ValidatePipelines()` rules of FR-22. Those are deferred to
ADRs 0072 and 0073."* 0071 — *"or the `ValidatePipelines()` rules of FR-22 — 0072 and 0073."*
Against 0073 `Scope` — *"It does **not** decide where FR-22's validation rules are evaluated —
that is ADR 0074"* — and 0072 says the same. A reader chasing FR-22 from either of the first two
lands on two ADRs that both explicitly disclaim it.

**Recommendation**: In both files, name 0074 for the FR-22 clause. Same edit both files.

---

### 25. [0070] The invariant read off the sequence diagram contradicts the diagram (Score: 66)

**Evidence**: diagram message — `Builder->>Transforms: CreatePipelineScope(), only if the registry
offered nothing`. Two paragraphs later, :127 — *"The transformer factory **is asked and passed the
scope** whether or not the mapper declares a transform (D12)."* When `MapperLifetime = Scoped` the
registry offers and the transformer factory is never asked. And with no transform attributes
nothing is passed either: `TransformPipelineBuilder.cs:174-196` only reaches
`new TransformerFactory<TRequest>(…)` (`:193`) inside the loop over `transformAttributes`. The
overstatement recurs at :300. D12 (`requirements.md:779`) is about the *participating set*, which
the mechanism does deliver.

**Recommendation**: Restate as the participation rule the mechanism actually gives — *"the
transformer factory counts as a participant whether or not the mapper declares a transform, so
`TransformerLifetime = Scoped` alone makes the pipeline take a scope"*.

---

### 26. [0071] Neither new member has a contract table, which the skeleton and all four siblings require (Score: 66)

**Evidence**: `grep -n "| Member |" docs/adr/007*.md` → 0070:218, 0072:223/251/287,
0073:207/243/321, 0074:203. ADR 0071: **no match**. Consequence: error conditions for
`CreatePipelineScope()` are one prose sentence (that claim does hold —
`PipelineBuilder.cs:179-205` verified), and error conditions for `PipelineScope` are stated
nowhere — is a `null` handle under a non-`Singleton` lifetime an error? May the property change
between reads? Is it read once per `Create` or once per pipeline?

**Recommendation**: Add Member/Input/Output/Error-conditions tables for both, covering: throw from
`CreatePipelineScope()`, unrecognised handle (finding 14), `null` under a non-`Singleton`
lifetime, and double-disposal.

---

### 27. [0074] Alternative 2's rejection rests on an incomplete enumeration of container-free shapes (Score: 65)

**Evidence**: *"The only container-free shape left is opaque: an
`IEnumerable<Func<IEnumerable<ValidationError>>>` of pre-bound rules"*. The counter-example is the
precedent this ADR leans on four times — ADR 0064's `IAmATransformerResolvabilityProbe`, a named,
core-defined, container-free question interface implemented in the DI package
(`src/Paramore.Brighter/Validation/IAmATransformerResolvabilityProbe.cs`, implementation
`ServiceCollectionTransformerResolvabilityProbe`). A lifetime-probe analogue would be neither
opaque nor a delegate bag. It probably *does* fail — FR-22.1/22.2 require messages listing all
three lifetimes and their values, which a bool-returning probe cannot supply without mirroring
`ServiceLifetime` into core (Alternative 3) — but the ADR never joins those arguments.

**Recommendation**: Add the named-probe variant explicitly and reject it by pointing at
Alternative 3.

---

### 28. [SET] 0071 claims its `CreatePipelineScope()` mirrors 0070's contract "exactly" — the null semantics differ on `Transient` (Score: 65)

**Evidence**: 0070's doc comment — `null` when *"it is not container-backed, **or its configured
lifetime is not `Scoped`**"*. 0071's role table — *"`null` for `Singleton`; **a handle for
`Transient` and `Scoped` alike**"*. Both are right in isolation (the handler family needs a handle
for `Transient` because ADR 0067's per-resolution scope rides on `ServiceProviderLifetimeScope`),
and 0072's ladder row 1 confirms the asymmetry. What is wrong is 0071's claim of identity — *"This
mirrors ADR 0070's `CreatePipelineScope()` exactly, **including its contract**"* — which is the
sentence an implementor acts on. Applying 0070's rule to the handler factory returns `null` for
`Transient` and regresses ADR 0067, which C-6 forbids.

**Recommendation**: Replace "including its contract" with the difference stated plainly: the
member's shape and throw behaviour are 0070's; its null rule is not.

---

### 29. [0070] "Only when its configured lifetime is `Scoped`" is contradicted by 0072, and FR-27 is never cited (Score: 64)

**Evidence**: ADR:309 and :333 fix the offer rule per-factory. But `0072:296` states: *"ADR 0070's
protocol calls `CreatePipelineScope()` on one factory and D16 requires exactly one ask per
pipeline. The factory that creates the pipeline scope must therefore know every participant's
configured lifetime."* With `{Transient mapper, Scoped transformer}`, 0072's `ScopeAffinityPolicy`
must have the mapper registry answer for a set containing `Scoped`. 0070's rule is superseded, not
extended. FR-27 appears nowhere in 0070, although it cites D12 twice and D12's stated landing
(`requirements.md:779`) is *"Terms; **FR-27.2**; AC-13, AC-46"*.

**Recommendation**: Reword to "when `Scoped` participates in this pipeline", note that 0072 fixes
how a single factory computes over the whole set, and add FR-27 to Scope and References.

---

### 30. [0071] The frontmatter summary contradicts the body on whether the dictionary survives (Score: 62)

**Evidence**: frontmatter `summary` (:8) — *"`ServiceProviderHandlerFactory` stops keying a DI
scope on `IAmALifetime` in a dictionary, and `Release` stops being the thing that disposes it."*
Body, *Technology Choices* — *"**The dictionary survives as the no-handle path.**"* Body,
*Negative* — *"`_lifetimeScopes`, `GetOrCreateLifetimeScope` and `ReleaseLifetimeScope` remain."*
The summary is what `adr:read_adr_metadata` and the ADR index surface.

**Recommendation**: Qualify — "stops keying a DI scope on `IAmALifetime` **on the path Brighter
itself takes**; the dictionary survives as a fallback for callers that supply no handle."

---

### 31. [0072] Five `file:line` citations inside one forces bullet (Score: 62)

**Evidence**: the contract caps this at one per forces or Consequences bullet. ADR:72 carries five
(`CommandProcessor.cs:591-599`, `:601`, `:481`, `PipelineBuilder.cs:187-198`, `:232-244`). All five
verified correct — a placement defect, not an accuracy one.

**Recommendation**: Keep the behavioural claim plus one anchor; move the rest to `Implementation
Approach` step 5, which already restates them.

---

### 32. [0074] The body cites ADRs by bare number, which the ADR itself says is ambiguous in this repo (Score: 62)

**Evidence**: References opens with *"Related ADRs (cited by slug — ADR numbers are not unique in
this repo, C-16)"*. The body then uses `ADR 0064` ×8, `ADR 0053` ×2, `ADR 0054` ×1. `docs/adr` has
two `0064-*`, three `0053-*` and two `0054-*` files. Worse, `requirements.md:383` (C-16) assigns
the bare number to the *other* document — *"ADR 0064 = `0064-pipeline-cache-type-key`" — so a
reader applying the spec's own convention resolves all eight to the wrong ADR.

**Recommendation**: Use slugs on first use per section, or gloss the number immediately after its
first occurrence.

---

### 33. [0073] "A third break" is at least a fourth (Score: 62)

**Evidence**: :217, :396 and :415 all say "a third break", counting FR-20's behavioural break and
FR-22.2's compatibility break. AC-24's final clause requires a fourth: release notes for the six
factory interfaces whose signatures change, which 0070 and 0071 deliver (`0070:192`, touched table
`:237-238`).

**Recommendation**: Say "a fourth break", or drop the ordinal.

---

### 34. [0073] The extension's namespace is unprecedented here and undercuts the ADR's own IntelliSense argument (Score: 58)

**Evidence**: the extension class is placed in `namespace Microsoft.Extensions.DependencyInjection`.
`grep -rln "^namespace Microsoft.Extensions.DependencyInjection" src` → **no matches**; every
Brighter `IServiceCollection` extension uses its own namespace
(`Extensions.DependencyInjection/ServiceCollectionExtensions.cs:43`, consumer equivalent at `:12`).
The stated rationale — *"`AddBrighterRequestScope` sorts next to `AddBrighter` in IntelliSense"* —
is only true in a file that has imported both namespaces.

**Recommendation**: Either use `Paramore.Brighter.Extensions.AspNetCore`, or keep the Microsoft
namespace and justify it on the better ground: ASP.NET's implicit usings guarantee it is in scope
in `Program.cs` without a second `using`.

---

### 35. [0070] The extra `### Forward compatibility` section is non-canonical and now stale (Score: 58)

**Evidence**: the canonical skeleton (`documentation.md:71-85`) goes `### Implementation Approach`
→ `## Consequences`. 0070 interposes `### Forward compatibility with ADRs 0071, 0072 and 0073` at
:327 — the only heading in the set with no counterpart in the other four (0071 :248→:283, 0072
:368→:454, 0073 :384→:400, 0074 :323→:333). 0072 carries the same material as a numbered item
*inside* Implementation Approach, which is the shape the set settled on. Three things have gone
stale: the heading omits 0074; the final bullet is titled *"`Publish` and the opt-in (0073)"* when
`Publish` suppression landed in 0072; and its content now duplicates in prose what four siblings
decide in full.

**Recommendation**: Delete the section now that all four siblings exist. If anything must survive,
the one non-obvious claim — why `ServiceProviderPipelineScope` owns a `ServiceProviderLifetimeScope`
rather than an `IServiceScope` — belongs in `Technology Choices`. (Note `documentation.md:87` names
0070 as the worked example of the skeleton, so exemplar and contract currently disagree — one
should move.)

---

### 36. [0071] `TransformPipelineDrain` does not today enforce the ordering rule the ADR twice says it enforces (Score: 58)

**Evidence**: two present-tense claims — *"the same rule `TransformPipelineDrain` enforces for
transform pipelines"*. Actual code, `TransformPipelineDrain.cs:40-77`: `Drain(Action disposeScope,
Action releaseMapper)` disposes the **transform lifetime scope** (an artefact tracker) then
releases the mapper lease. No DI scope participates. `0070:305` is explicit that the DI-scope step
is new — the drain *"gains a third step"*.

**Recommendation**: Change both to "the same rule ADR 0070 adds to `TransformPipelineDrain` as its
third drain step", and move the citation into `Implementation Approach`.

---

### 37. [SET] 0072 uses `AddBrighterAspNetCoreScopes`, a spelling 0073 explicitly rejects (Score: 58)

**Evidence**: 0073 devotes a section to rejecting the name and replacing it with
`AddBrighterRequestScope(ScopeAffinity affinity = ScopeAffinity.JoinAmbient)`. `grep -c` →
`AddBrighterAspNetCoreScopes`: 0072:1, 0073:2 (both in the rejection);
`AddBrighterRequestScope`: 0073:17, 0072:0. 0072 does disclaim it ("written against the working
spellings"), which is why this is not scored higher — but the set otherwise back-propagates
newest-ADR facts meticulously.

**Recommendation**: One-word edit in 0072, and trim its Scope disclaimer now that 0073 has settled
all three names. `requirements.md` FR-17 also still shows the rejected spelling in its two worked
examples.

---

### 38. [0074] FR-22.3's snapshot staleness is priced only for FR-24.3 (Score: 58)

**Evidence**: *Negative* records call-time staleness for the duplicate-provider rule alone. But by
the ADR's own design both the artefact candidate set and every parameter's `ServiceLifetime` come
from the same call-time `ContainerRegistrationSnapshot` — so a mapper registered after
`ValidatePipelines()` is not a candidate, and a later `AddScoped<IOrderDbContext>()` makes a real
captive dependency invisible. The failure-mode table claims *"none is a case where it reports
wrongly"*, but the last-descriptor-wins rule can report wrongly if a later registration changes the
effective lifetime after the snapshot.

**Recommendation**: Add a failure-mode row and generalise the Negative bullet from the provider
case to the whole snapshot.

---

### 39. [0070] The `Where the pieces live` flowchart points the dependency the wrong way and omits the six changed interfaces (Score: 56)

**Evidence**: the contract (`documentation.md:120-124`) reserves this form for *"assemblies and
packages, and **which way dependencies point**"*. The diagram draws
`builder -- "CreatePipelineScope(), then Create with the scope" --> facs`, from the
`Paramore.Brighter` subgraph into the DI-package subgraph — the reverse of the real reference
direction and of the ADR's own load-bearing claim. The six interfaces the ADR changes do not
appear in the core subgraph at all, although they are its subject.

**Recommendation**: Add a node for the six changed interfaces, point `facs` at it with
`implements`, and either drop the core→package arrow or label it explicitly as a call, not a
reference. (Same class of defect as findings 20 and 53 — worth one convention for the set: a
legend line distinguishing call edges from reference edges.)

---

### 40. [0072] The stated diagnostic evaluation order contradicts the ladder and pseudo-code (Score: 55)

**Evidence**: :337 — *"Evaluation order … is **FR-24.4, then FR-23, then FR-24.2**"*. But the
ladder evaluates FR-24.2 (row 6) before FR-23 (row 8), and the pseudo-code at :390-401 does the
same. The outcome is identical because the conditions are mutually exclusive, which
`requirements.md` itself notes.

**Recommendation**: Say what is true — FR-24.4 first because it short-circuits the probe; FR-23 and
FR-24.2 are mutually exclusive so their order is immaterial.

---

### 41. [0072] Out-of-order disposal of nested suppression brackets is unspecified (Score: 55)

**Evidence**: :290 — *"a bracket that **restores the previous value** when disposed, so brackets
nest"*. That is correct only under LIFO: take A, take B, dispose A, dispose B → B restores `true`
and the flow stays suppressed for life. The ADR argues at :471 that *"the failure direction is
toward isolation"*, covering the consequence but never the case — and *Technology Choices* (:360)
makes non-lexical use an intended scenario by justifying a public mutator.

**Recommendation**: State the disposal discipline as part of the contract, or specify a
generation-counter implementation that makes out-of-order disposal correct.

---

### 42. [0071] NFR-4, NFR-5, C-1 and D0 are cited in References but never addressed; concurrency and nesting are undiscussed (Score: 55)

**Evidence**: each of `NFR-4`, `NFR-5`, `NFR-6`, `NFR-7`, `C-1`, `D0` occurs **exactly once** in
the ADR — on the References line (versus `NFR-8` ×8, `FR-7` ×6). NFR-4 (`requirements.md:350`)
requires safety under concurrent pipelines and under `Publish`'s `Parallel.ForEach`
(`CommandProcessor.cs:481`, verified). This ADR moves scope creation off a
`ConcurrentDictionary.GetOrAdd` and disposal off a `TryRemove`
(`ServiceProviderHandlerFactory.cs:129`, `:135`) onto a `HandlerLifetimeScope` with plain
`List<T>` state and no disposal guard — today `TryRemove` is what makes a concurrent double-`Release`
dispose exactly once. The *Positive* bullet celebrates the removal without pricing it. Re-entrancy
and nested pipelines are unaddressed although FR-8/D6 makes nesting first-class.

**Recommendation**: Add a forces bullet or Risks row stating the concurrency story plainly — each
`HandlerLifetimeScope` is confined to one `PipelineBuilder`, `Dispose()` runs on one thread,
`CreatePipelineScope()` shares nothing, a nested pipeline builds its own handle — or drop the four
IDs from References.

---

### 43. [0073] The out-of-range-enum contract rests on an implementation detail no ADR fixes (Score: 55)

**Evidence**: :209 — *"any value that is not `JoinAmbient` behaves as `AlwaysNew`, because
`ScopeAffinityPolicy` tests for `JoinAmbient` positively."* `0072:294-309` gives that type's
constructor and two member signatures and nothing about how it tests. (It does fix `AlwaysNew = 0`,
which supports the spirit but not the claim.)

**Recommendation**: Restate as an obligation this ADR places on 0072 — every reader tests for
`JoinAmbient` positively so an unrecognised value degrades to `AlwaysNew` — and add the same
sentence to 0072.

---

### 44. [0074] The new-type count is wrong and the structure diagram omits two of them (Score: 55)

**Evidence**: *Negative* says *"Seven new types in the DI package plus one in core."* The
`Where each type is touched` table lists eight: `ScopeConfigurationValidator`, `ScopeConfiguration`,
`ScopeProviderRegistration`, `ArtefactRegistration`, `ArtefactKind`, `ContainerRegistrationSnapshot`,
`ArtefactConstructorSelector`, `ScopeConfigurationRules`. The `Where the pieces live` flowchart's
`ents` node names only four — `ScopeProviderRegistration` and `ArtefactKind` are absent — and the
roles table has a row for neither.

**Recommendation**: Say "eight", and either add the two to the flowchart and roles table or fold
them into their owners (`ArtefactKind` into `ArtefactRegistration`, `ScopeProviderRegistration`
into `ScopeConfiguration`) and drop them from the touched table.

---

### 45. [0074] Context opens by naming five types before it states the problem (Score: 55)

**Evidence**: the contract is explicit — *"**Do not open by naming four interfaces** — a reader
cannot hold type names before they know the problem."* The first two sentences name
`IAmAScopeProvider`, `services.AddSingleton<IAmAScopeProvider, T>()`, `TryAddSingleton`,
`ScopeAffinity DefaultScopeAffinity { get; set; } = ScopeAffinity.AlwaysNew;`, `IBrighterOptions`
and `BrighterOptions` — a registration-model recap of two sibling ADRs. The problem arrives in
sentence three.

**Recommendation**: Lead with *"Four configurations are now expressible that an application almost
certainly did not intend … what no prior record decides is which component evaluates those rules"*,
and move the recap into `Where this ADR sits`.

---

### 46. [0070] "Defect 1b is closed" omits FR-3's `both Scoped` qualifier, and the lifetime table has one column for two options (Score: 52)

**Evidence**: :345 claims closure citing FR-3, which reads *"With `MapperLifetime` **and**
`TransformerLifetime` **both** `Scoped`…"*. Under `{Scoped, Transient}` the transforms resolve
from ADR 0067 per-resolution scopes and it is not one instance — which `0072:425` states plainly
and 0070 never does. The *Behaviour by configured lifetime* table (:315) has a single "Configured
lifetime" column although `MapperLifetime` (`BrighterOptions.cs:52`) and `TransformerLifetime`
(`:69`) are independent.

**Recommendation**: Add the qualifier, and add a row or sentence for the mixed case.

---

### 47. [0071] AC-9 is never named, although it is FR-7's acceptance criterion and owns the tests the ADR rewrites (Score: 52)

**Evidence**: `AC-9` and `AC-10` appear zero times. `requirements.md:505` designates
`FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`:36-55`) and its
async twin (`:154`) as *"regression guards for AC-9"* that *"must keep passing unchanged"* — which
is precisely the paragraph step 6 argues against (*"they no longer guard the path Brighter itself
takes … **Both must be duplicated onto the handle path**"*). Both line citations verified. The
argument is the ADR's strongest honest catch, made without naming the AC it amends.

**Recommendation**: Name AC-9 in step 6 and in *Negative*, and state that the "regression guards
for AC-9" designation now attaches to the duplicated handle-path pair.

---

### 48. [0072] NFR-6 is cited for a cost it does not govern (Score: 52)

**Evidence**: :229 — *"one virtual call per pipeline … which is **within NFR-6**"*. NFR-6
(`requirements.md:352`) is a budget on **DI scopes**: *"at most one DI scope begin/release per
pipeline; it must not add a DI scope per resolved instance"*. The unconditional ask allocates no
scope, so NFR-6 is silent on it and cannot bless it.

**Recommendation**: Drop the citation and justify the ask on D16's observability grounds (AC-13,
AC-46).

---

### 49. [0072] AC-8 is cited for the borrowed case, but AC-8 is about Brighter-created scopes (Score: 52)

**Evidence**: :429 calls the borrowed handle's `Dispose()`/`DisposeAsync()` *"idempotent no-ops
(AC-8)"*. AC-8 (`requirements.md:452-455`) is written over two live pipelines *"each holding a
**Brighter-created** `IAmAScope`"*. The borrowed non-disposal claims are AC-16 and AC-38, which the
ADR cites correctly elsewhere.

**Recommendation**: Replace `(AC-8)` with `(AC-16, AC-38)`, or say AC-8's rule is being *extended*
by design rather than asserted.

---

### 50. [0074] `The forces` breaches the one-citation-per-bullet rule three times (Score: 52)

**Evidence**: *"The inputs must be read as the factories read them"* carries **five**
(`ServiceProviderMapperFactory.cs:44`, `ServiceCollectionExtensions.cs:69`, `:97`,
`ServiceActivator…/ServiceCollectionExtensions.cs:38`, `:88`); *"`ValidatePipelines()` is opt-in"*
carries **three**; *"Both host shapes must fire"* carries **four**. All verified correct — a
placement defect. The Consequences bullets are clean.

**Recommendation**: One anchor per bullet; move the enumerations to `Implementation Approach` and
the "How the inputs reach the rules" table, which already carries most of them.

---

### 51. [SET] Three ADRs' Decision sections break the one-bold-sentence, no-signatures rule (Score: 52)

**Evidence**: the contract for `## Decision`: *"the decision in **one bold sentence**, then one
short paragraph on the shape it takes. **No signatures, no file paths.**"* 0070, 0071 and (for
sentence count) 0074's bold block aside, the breaches are: **0073** — three bold sentences (:86)
and no follow-on paragraph at all, going straight to `### The mechanism` at :88, naming
`ScopeAffinity DefaultScopeAffinity`, `AddBrighterRequestScope(ScopeAffinity affinity =
ScopeAffinity.JoinAmbient)` and `ScopeAffinityOverride`; **0072** (:79) — names
`IAmAServiceProviderScope : IAmAScope` including its base-interface declaration; **0074** — a
four-sentence ~110-word bold block naming eight types and members.

**Recommendation**: Demote signatures to `Key Components`, where all three already restate them.
Each has a recoverable one-sentence rule: 0073's is already written at its :90 (*"The extension
deposits a value into the collection; the one place that does have the object — the factory that
produces it — picks the value up"*); 0074's is *the four container rules are evaluated by a
validator in the DI package that decorates the core one, so the existing hosts fire it unchanged*.
Add 0073's missing shape paragraph.

---

### 52. [0073] FR-19 and FR-21 are listed as *discharged* but no mechanism here delivers them (Score: 50)

**Evidence**: `Scope` claims *"It discharges FR-14, FR-15, FR-17, FR-19 and FR-21."* FR-19 (the
flag is inert on the consumer side) is delivered by the pump publishing no ambient (D0b/C-2, ADR
0072); FR-21 (affinity applies to `Scoped` only) is delivered by `ScopeAffinityPolicy` and the five
factories (0072). `0072:32` claims neither, so a reader auditing coverage finds them attributed to
the one ADR that does not implement them.

**Recommendation**: Move both to the *serves* list naming which ADR's mechanism discharges each —
or say what in this ADR makes them true (the default value, FR-25.11's documentation obligation).

---

### 53. [0072] Cross-assembly edges in the `Where the pieces live` flowchart render into subgraph borders, not their target nodes (Score: 50)

**Evidence**: rendered to PNG at 1600px and inspected. All four cross-subgraph edges —
`facs --> provider`, `facs --> suppress`, `asp -.-> provider`, `role -.-> handle` — terminate at
the `core` subgraph boundary rather than at `IAmAScopeProvider`, `AmbientScopeSuppression` and
`IAmAScope`, which render with **no incoming arrowheads at all**. Cause: the `direction TB`
declarations inside each subgraph (:144, :154), which mermaid degrades once a subgraph has
cross-boundary edges. The diagram parses cleanly and loses exactly the information this form
exists to convey.

**Recommendation**: Remove the three `direction TB` lines and re-render; verify by reading the PNG,
not the exit code.

---

### 54. [0072] "A defaulted constructor argument" — `PipelineBuilder` has three constructors (Score: 48)

**Evidence**: :433 says `PipelineBuilder<TRequest>` takes `bool isolateSubscribers = false`.
`PipelineBuilder.cs` declares three public constructors: `:59` (sync handler factory), `:76`
(async), and `:92` — `PipelineBuilder(IAmASubscriberRegistryInspector, InboxConfiguration? = null)`,
the describe-only constructor used by the two validation sites the ADR names
(`BrighterPipelineValidationExtensions.cs:75`, `:116`, both verified), which resolves nothing and
for which the flag is meaningless.

**Recommendation**: Name the two dispatch constructors (`:59`, `:76`) and note that the
describe-only constructor does not need it.

---

### 55. [SET] The unifying sentence has two spellings, and is absent from three of the five (Score: 48)

**Evidence**: `grep -i 'per-pipeline object carries'` → 5 hits, all in 0070 and 0071, in two
spellings: *"the per-pipeline object carries **the DI scope**"* (0070:46, 0071:46) and *"…carries
**the scope**"* (0070's Forward compatibility, 0071:109, 0071:240). Zero hits in 0072, 0073, 0074 —
although 0070:46 instructs that it is *"the sentence to carry into the rest"*. The concept survives
intact, so this is not a paraphrase-into-three-versions problem; the contract nonetheless says
*"repeat that **exact** sentence"*.

**Recommendation**: Use "the per-pipeline object carries the DI scope" (unambiguous against
`IAmALifetime` per NFR-8) at all five existing sites, and either add it to 0072/0073/0074's
`Where this ADR sits` — 0072 genuinely applies it — or soften 0070's claim to "the sentence that
unifies the first two".

---

### 56. [0071] A Risks and Mitigations cell carries four `file:line` citations (Score: 48)

**Evidence**: *"Decorators resolve through the same `IAmALifetime` instance they do today (`:272`,
`:316`, `:430`, `:499`)"*. All four verified correct — density, not accuracy. The
`### How a handler pipeline reaches its DI scope today` Context subsection carries ~15 more, all
correct but outside the two sections the contract designates.

**Recommendation**: One citation per Risks cell; let `Implementation Approach` carry the rest — the
four decorator sites are already listed verbatim in `Technology Choices` and Alternative 2.

---

### 57. [0074] Six requirement IDs are listed in References and used nowhere; two used IDs are unlisted (Score: 48)

**Evidence**: everything before `## References` contains **zero** occurrences of FR-14, FR-17,
FR-21, C-17, D19 and FR-25.6, each enumerated in References. Conversely `D11` and `C-11` are used
in the body and absent from the enumeration.

**Recommendation**: Prune the six and add D11 and C-11. A References list that does not match the
body's citations is what a later reviewer uses to check coverage.

---

### 58. [0070] Off-by-one and imprecise line citations (Score: 45)

**Evidence**: four verified misses — `BuildTransformPipeline<TRequest>` is at
`TransformPipelineBuilder.cs:174`, not `:173` (blank); `ResolveMapperInfo` is called at `:172`, not
`:171` (a comment), and the async twin `ResolveAsyncMapperInfo` (`TransformPipelineBuilderAsync.cs:172`)
is unnamed; the `SynchronizationContext` suppression code is `ServiceProviderLifetimeScope.cs:422-436`,
not `:384-388` (XML `<remarks>`); the builder throws are at `:124`/`:165`, not `:122`/`:163`, and
are identical in both builders, so the "async builder's" framing implies a difference that does not
exist. Everything else in the ADR's citation set is accurate, including the counts *"12 classes in
`src/`"*, *"61 factory doubles across 34 test files"* and *"six registry doubles in three more"* —
all exactly right.

**Recommendation**: Correct the four.

---

### 59. [0073] No stated behaviour for "extension called, Brighter never registered" (Score: 45)

**Evidence**: the contract row covers `ArgumentNullException` on a null collection and the
double-call case only; step 3 covers partial adoption *across* the four sites, not the absence of
all four. An application that calls `AddBrighterRequestScope()` without `AddBrighter`/`AddConsumers`
leaves `RegisterBrighterOptions` never running, the override never read, and a provider registered
with nothing to consult it. Benign, but unstated.

**Recommendation**: One sentence in the contract row: calling the extension without any Brighter
registration is inert and is not an error.

---

### 60. [0072] References cite four IDs the body never uses, and omit the decision the suppression half rests on (Score: 40)

**Evidence**: `C-13`, `C-14`, `D2` and `D6` appear **only** on the References line (:521) — the
apparent hits elsewhere are substring matches inside `AC-13`, `AC-14`, `AC-17`. `D6`
(`requirements.md:196`) is the decision mandating the execution-time bracket this ADR designs, and
`C-13` assigns suppression's expression to it. Conversely the body cites `D4`, `D8`, `D0c`, `C-2`,
`C-4`, `C-5`, `C-6`, `C-9`, `C-12a`, `OOS-4`, `OOS-7`, `OOS-14`, `FR-22`, `FR-25`, `FR-17`, none of
which appear in References.

**Recommendation**: Cite `D6` and `C-13` at the two suppression brackets, drop `C-14` and `D2`
(0073's), and bring the list into line with the body.

---

### 61. [0071] "All six methods that resolve an artefact" — two of the six do not resolve (Score: 38)

**Evidence**: `BuildPipeline` (`PipelineBuilder.cs:272`) and `BuildAsyncPipeline` (`:316`) *thread*
`instanceScope` but resolve nothing. The resolution sites are `:191`, `:236` (direct `Create`),
`:439`/`:461` in `AppendToPipeline`, `:509`/`:535` in `PushOntoPipeline`, plus
`HandlerFactory.CreateRequestHandler` (`HandlerFactory.cs:44`) and
`AsyncHandlerFactory.CreateAsyncRequestHandler` (`AsyncHandlerFactory.cs:42`). The *claim the
argument rests on* — that an `IAmALifetime` is in scope at every handler resolution site — **is
true and was verified exhaustively**: both `Create` signatures take a non-nullable `IAmALifetime`
(`IAmAHandlerFactorySync.cs:44`, `IAmAHandlerFactoryAsync.cs:44`), so no resolution site can exist
without one. Only the count and the verb are wrong.

**Recommendation**: "It reaches every site that resolves an artefact — the two direct `Create` calls
and the four decorator resolutions — because both `Create` signatures require one."

---

### 62. [SET] NFR-3 and NFR-9 are cited by no ADR in the set (Score: 38)

**Evidence**: `grep -c` for each across all five returns 0 in all ten cells. Both are substantively
satisfied — NFR-3 by 0070's and 0071's *"It gains no container dependency — `IAmAScope` is a core
type"* against `ControlBusMessageMapperFactory` and `ControlBusHandlerFactorySync`; NFR-9 by
FR-25.3's truth table, inside the unowned FR-25 page (finding 11). But the contract's *"name what
is unchanged, so a reviewer does not read an omission as an oversight"* is exactly what these miss.

**Recommendation**: One clause each — NFR-3 into 0070's and 0071's *Unchanged* paragraphs beside
the ControlBus sentence; NFR-9 folded into whatever resolves finding 11.

---

### 63. [0071] The `Where the pieces live` flowchart's cross-assembly edge reads backwards and lands next to the wrong node (Score: 35)

**Evidence**: rendered and inspected at 1600px. `builder -- "CreatePipelineScope()" --> sphf`
crosses from `core` into `di`; in the rendered layout the label and boundary crossing sit directly
beneath the `IAmALifetime` node, so the edge reads as though `IAmALifetime` calls
`ServiceProviderHandlerFactory`. The arrow also points opposite to the actual assembly reference.
Same convention question as findings 39 and 53.

**Recommendation**: Re-label as a call (`builder -. "calls CreatePipelineScope()" .-> sphf`), or
move `builder` adjacency so the crossing does not pass `lifetime`. A legend line distinguishing
call edges from reference edges would settle it for the whole set.

---

## Requirements coverage map

`D` = an ADR's Decision decides it · `S` = supports/constrains · `—` = not addressed

| Req | One-line | 0070 | 0071 | 0072 | 0073 | 0074 | Verdict |
| --- | --- | --- | --- | --- | --- | --- | --- |
| FR-1 | `Scoped` mapper is per transform pipeline | **D** | | | | | covered |
| FR-2 | `Scoped` transform is per transform pipeline | **D** | | | | | covered |
| FR-3 | mapper + transforms share scoped dependencies | **D** | | | | | covered (see finding 46) |
| FR-4 | producer pipelines scope per `Post`/`DepositPost` | **D** | | | | | covered |
| FR-5 | failed build releases the owned scope | **D** | | | | | covered |
| FR-6 | released exactly once on every exit path | **D** | | | | | covered |
| FR-7 | handler pipeline scoping preserved | **D** | **D** | | | | covered |
| FR-8 | `Publish` subscriber never adopts, suppresses beneath | S | S | **D** | | | covered |
| FR-9 | two brackets — resolution and execution | | | **D** | | | covered |
| FR-10 | container-agnostic seam types in core | S | | **D** | | | covered |
| FR-11 | no provider ⇒ no adoption, still a real scope | | | **D** | | | covered |
| FR-12 | never dispose a borrowed scope | S | | **D** | S | | covered (see finding 16) |
| FR-13 | dispose every scope Brighter created | | | **D** | | | covered (see finding 3) |
| FR-14 | one flag for both pipeline kinds | | | | **D** | S | covered |
| FR-15 | default `AlwaysNew`, identical to today | | | | **D** | | covered |
| FR-16/16a/16b | pipelines in one request share the request scope | S | | **D** | S | | covered |
| FR-17 | ASP.NET package, registration is the only wiring | | | S | **D** | S | covered |
| FR-18 | no `HttpContext` ⇒ new owned scope | | | **D** | **D** | | covered |
| FR-19 | flag inert on the consumer side | | | S | claimed | | **mis-attributed — finding 52** |
| **FR-20** | **clean break recorded in `release_notes.md`** | cited only | | | S | S | **GAP — finding 17** |
| FR-21 | affinity applies to `Scoped` only | | | S | claimed | S | **mis-attributed — finding 52** |
| FR-22 | inert opt-in / mixed lifetimes / captive dep reported | S | S | S | S | **D** | covered (see finding 24) |
| FR-23 | present-but-unusable ambient falls back and says so | | | **D** | S | | covered |
| FR-24 | scope-provider failure modes | | | **D** (.1/.2/.4) | S | **D** (.3) | covered (see findings 1, 2) |
| **FR-25** | **`docs/guides/lifetimes-and-scoping.md`, 11 clauses** | | | | .11 | .6/.8/.9/.10 | **PARTIAL — finding 11** |
| FR-26 | Brighter state keyed to a borrowed scope must not outlive it | | | **D** | | | covered |
| FR-27 | pipeline whose factories have different lifetimes | | | **D** | S | | covered (see finding 29) |
| **NFR-1** | core stays container-agnostic (+ obligations a/b/c) | unattributed | | S | S | S | **(c) uncovered — findings 17, 21** |
| NFR-2 | no ASP.NET dependency in the DI package | | | S | **D** | | covered |
| **NFR-3** | `ServiceActivator` keeps its dependency set | implied | implied | | | | **cited nowhere — finding 62** |
| NFR-4 | thread safety | S | cited only | S | S | | **thin — findings 19, 42** |
| NFR-5 | bounded resource growth | S | cited only | | | | thin |
| NFR-6 | cost per pipeline, not per resolution | S | cited only | mis-cited | | | see finding 48 |
| NFR-7 | extensibility without ASP.NET | S | S | **D** | S | S | covered |
| NFR-8 | docs disambiguate `IAmAScope` / `IAmALifetime` | S | S | S | S | | covered |
| **NFR-9** | diagnosability truth table in the guidance page | | | | | | **cited nowhere — finding 62** |
| NFR-10 | the lifetime documentation is self-sufficient | | | | | S | thin — rides on FR-25 |

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 3 |
| 70-89 (High) | 17 |
| 50-69 (Medium) | 33 |
| 0-49 (Low) | 10 |

**Total findings**: 63
**Findings at or above threshold (60)**: 33

## Suggested order of work

1. **The three Criticals (1–3)** are each a factual error that would produce wrong code or a
   failing AC. Fix these first; #2 requires a decision reconciled across 0070 and 0072.
2. **The two structural gaps (11, 17)** — FR-25's page and the release-note record — are the
   coverage holes an implementor cannot discover from the ADRs.
3. **The contract-and-count corrections (4, 5, 6, 8, 14, 16, 19, 26)** — every one is a missing or
   falsified contract that two developers would resolve differently.
4. **The 0072 split (10)** is the largest single edit and worth deciding before the smaller
   0072 findings are applied, since several move with it.
5. The remaining Mediums are mostly citation, wording and diagram fixes that can be batched.
