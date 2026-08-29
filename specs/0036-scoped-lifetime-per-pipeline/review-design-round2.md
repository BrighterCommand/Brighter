# Review: design — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-03
**Threshold**: 60
**Round**: 2 — round 1's ledger closed at `1a519d5f5`; this run is against `78634598b`.
Round 1's findings are preserved verbatim in `review-design-round1.md`; its numbering is what
PROMPT.md §10.4 refers to. **This file uses its own numbering.**
**Verdict**: NEEDS WORK

35 findings at or above threshold 60. Address these before approving.

## How this round was run

Six parallel reviewers at threshold 60 — one per ADR (0070–0075) plus one whose only remit was the
set-level properties (requirements coverage, cross-ADR contradictions, the sibling maps, the
unifying sentence, heading drift, and whether the decomposition itself still holds). Reviewers were
**blind to round 1's ledger**, so nothing here is anchored on the previous round's conclusions.

74 raw findings were returned. Three pairs were the same defect seen by two reviewers and have been
merged (noted inline), leaving **71 distinct**.

**Proved clean this round — verified, not assumed:**

- **All 13 mermaid diagrams render.** Every block in all six files extracted and run through
  `@mermaid-js/mermaid-cli@11`: 0070×2, 0071×3, 0072×1, 0073×3, 0074×2, 0075×2 — all exit 0, all
  `.svg` produced. The most complex in each file also rendered to PNG at 1600px and looked at.
- **Escaped markdown is 0** in all six.
- **`docs/adr/index.md` is byte-identical to a fresh regeneration** and reads `_97 ADRs indexed._`.
  No frontmatter drift.
- **Heading skeleton matches in all six** — wording, order and nesting, against
  `.agent_instructions/documentation.md` § *ADR structure*. No drift anywhere.
- **All six `### Where this ADR sits` maps are byte-identical**, six rows, own row bolded and marked
  *(this one)*, each positional sentence agreeing with its table. One cell is stale in content
  (finding 29), not in structure.
- **The unifying sentence is verbatim in all six** — *the per-pipeline object carries the DI scope*
  — at `0070:49`, `0071:47`/`:112`/`:260`, `0072:50`, `0073:53`, `0074:71`, `0075:51`. Zero variant
  spellings. (Round 1's finding 55 is fully closed.)
- **Frontmatter uniform**; `status: Proposed`; author `"Ian Cooper"` in all six.
- **Tone clean** — no authoring-conversation phrasing, no `PROMPT.md`, no spec phase, no review
  rounds, no commit hashes, in any of the six.
- **"chain" survives only as the rejected name `IAmAChainScope`** (`0070:431`, under D4).
- **No supersession** — no `supersedes:`/`superseded by:` frontmatter, no body claim, anywhere.
- **The FR-25 clause→ADR map in 0074 is complete and orphan-free** — 11 clauses, 11 rows, every
  named target exists.
- **Several seams read consistently**: 0070↔0072 on artefact identity; 0070↔0071 on the deliberate
  `Transient` null-rule difference; 0072↔0073 on `AddSingleton`/last-wins; 0072↔0074 on FR-24.3's
  model/site split; 0072↔0075 on the one-line suppression coupling.

---

## Findings

### 1. 0075 — The Decision and the forces both assert a false consequence, contradicted twice later in the same ADR (Score: 75)

Two places state that placing the *suppression* bracket around the whole build loop "would give
every subscriber one shared scope". That is not true, and the ADR itself says so twice further down.
Suppression is a `bool` on an `AsyncLocal`; it has no bearing on how many pipeline scopes get
created. Per-subscriber scopes come from `GetSyncInstanceScope()` / `GetAsyncInstanceScope()`,
called unconditionally once per loop iteration. This also breaks the ADR's own framing: line 114
says "Three invariants are readable off the diagram", and the sequence diagram shows nothing about
scope sharing.

**Evidence**: `0075:77` — "a bracket around the loop would give every subscriber one shared scope,
which is ADR 0039 undone." `0075:118` — "**Neither is ever placed around the whole loop**, which
would give every subscriber one shared scope and undo ADR 0039". Contradicted at `0075:218` —
"Around the loop, every subscriber would resolve under one suppression bracket — **which is
correct**"; and `0075:285` — "Around the build loop it is *behaviourally* adequate". Source:
`PipelineBuilder.cs:187-198` — `var instanceScope = GetSyncInstanceScope();` is inside the
per-subscriber lambda and is conditioned on nothing.

**Recommendation**: Rewrite `:77` and `:118` to say what `:218` and `:285` say — a loop-level
suppression bracket is behaviourally adequate and is rejected on *extent* and on the adjacency
hazard, not because it causes scope sharing. Then either drop invariant 2 from the "readable off the
diagram" list or put something in the diagram that carries it.

---

### 2. 0072 — The no-provider case has no ladder row, the pseudo-code warns on it, and the requirements require no warning there (Score: 74)

The ADR's prose enumerates **six** failures converging on create-and-own, the first being "no
provider". The nine-row ladder has a row for five of them and none for "no provider registered". The
pseudo-code silently folds that case into row 6 and emits a `Warning` that FR-11(a) and FR-19 do not
ask for; and the `WarnOnce` key type as declared cannot represent the "no provider" latch key.

**Evidence**: `0072:84` — "six distinct failures — **no provider**, nothing offered, a stale
ambient, …". Ladder row 6 reads "the ask carried `JoinAmbient`, **and nothing came back**" — with no
provider registered no ask is carried at all, so no row matches. Step 2's pseudo-code has
`4. ambient = _scopeProvider?.GetAmbient(affinity)` then `if ambient is null ->
diagnostics.WarnOnce(NoAmbientOffered, providerType)` with no `_scopeProvider is null` guard. The
`WarnOnce` contract (`0072:297`) states its input as "the implementation type of the provider that
was asked **(or the fact that none is registered)**" while giving the mechanism as
`ConcurrentDictionary<(Condition, Type), byte>.TryAdd` — a `(Condition, Type)` key has no spelling
for "none is registered". `requirements.md:220` FR-11(a) — "With no `IAmAScopeProvider` registered,
no pipeline consults or joins any ambient; **the affinity option is irrelevant. Adoption behaviour
is exactly as before this change.**" `requirements.md:287` FR-19 — "**Where an ambient source is
registered** there are exactly two". Reachable: `ScopeAffinityPolicy` reads only `IBrighterOptions`,
so `{affinity = JoinAmbient, all three Scoped, no provider}` yields `JoinAmbient` and walks into the
warning.

**Recommendation**: Add a ladder row above row 4 — "no `IAmAScopeProvider` is registered → OWNED, no
ask, **no diagnostic** (FR-11(a))" — mirror it as an explicit guard in the pseudo-code before step 4,
and delete "(or the fact that none is registered)" so the key stays `(Condition, Type)`. If a
warning here is genuinely wanted it needs a requirements amendment and a fourth condition with a
defined key, not a parenthetical.

---

### 3. 0070 — Implementation step 3 states the opposite of what `### The mechanism, end to end` states about D12, and both cite the same line (Score: 72)

The Decision section says the scope reaches a transform's `Create` **only where there is a transform
to create**. Step 3 says the transformer factory **is handed the scope even when the mapper declares
no transform**. Both cite `TransformPipelineBuilder.cs:193`, which is inside the `foreach` over
transform attributes — so when the mapper declares none, the loop body never executes,
`TransformerFactory<TRequest>` is never constructed, and `factory.Create` is never called. Step 3 is
the implementor's section.

**Evidence**: `0070:132` — "the scope reaches a transform's `Create` only where there is a transform
to create (`TransformPipelineBuilder.cs:193`, inside the loop over the mapper's transform
attributes)." `0070:325` — "This is D12: the transformer factory is handed the scope even when the
mapper declares no transform". Source `TransformPipelineBuilder.cs:193` is
`var transformerLease = new TransformerFactory<TRequest>(attribute, _messageTransformerFactory).CreateMessageTransformer();`,
inside `foreach (var attribute in transformAttributes)`.

**Recommendation**: Rewrite step 3's last sentence to match `:132`'s already-correct framing — D12 is
discharged by *asking* the transformer factory for a scope (`CreatePipelineScope()`) regardless of
whether a transform is declared; participation is about which lifetimes are consulted, not which
factory resolved something. Delete "the transformer factory is handed the scope even when the mapper
declares no transform".

---

### 4. 0071 — "Changes nothing an application can observe" is contradicted by Implementation step 2 (Score: 72)

The Scope paragraph and the Negative section both assert the ADR is observationally inert.
Implementation step 2 changes both the exception an application sees and the release side-effects it
gets, on a path reachable from ordinary user code. (This is a consequence of round 1's fix to its
finding 22 — the fault-tolerant release loop — introducing an inconsistency elsewhere.)

**Evidence**: `0071:30` — "It is **behaviour-preserving**: it discharges no new requirement and
changes nothing an application can observe." `0071:326` — "**This ADR delivers no behaviour.**
Nothing an application can observe changes." Against step 2 (`:272-277`): "catching per item and
holding the failure rather than letting it abort the loop"; "clear both tracking lists
unconditionally"; "if anything was held, throw them composed as an `AggregateException`". The ADR's
own Negative bullet at `:334` concedes the baseline differs. Verified: `HandlerLifetimeScope.cs:74-93`
has no `try`/`catch`, and `Extensions/Each.cs:39` is a plain `foreach`, so today the original
exception propagates unwrapped out of `Send`. Trigger is reachable —
`src/Paramore.Brighter/SimpleHandlerFactory.cs:27-33` calls `disposable?.Dispose()` on a user
handler.

**Recommendation**: Qualify the two blanket statements — the *scoping* behaviour is preserved (one
DI scope per pipeline, same resolution points, same release point), and one observable does change:
an application whose handler factory's `Release` throws today sees that exception and loses the
remaining releases, and afterwards sees a composed `AggregateException` with every release
attempted. Say whether that needs a release note alongside the interface break. **See decision 3
below — this may instead be grounds for moving the fault-tolerance out of 0071.**

---

### 5. SET — The release-note entry is enumerated as four breaks and it is five; ADR 0071's two interface breaks are in nobody's list (Score: 72)

*(Merges the 0070 reviewer's finding, which reported the same defect from inside 0070 at 62.)*

The set treats the upgrade breaks as a single `release_notes.md` entry owned by 0070 step 7a. That
entry enumerates four items. 0071 introduces a fifth — `IAmAHandlerFactory` and `IAmALifetime` —
declares it "**Needs a release note**", and never joins the entry; and the two sentences in 0073
that would have caught the omission mis-attribute 0070's six interfaces to 0070 *and* 0071, erasing
0071's rather than adding them.

**Evidence**: `0070:361` — "ADR 0073 adds the `IBrighterOptions` member and ADR 0074 C-18's
compatibility note; **all four** belong in the same release-note section". `0071:325` — "**Two more
public interfaces break at compile time.** `IAmAHandlerFactory` (21 implementations here…) and
`IAmALifetime` (7…). … **Needs a release note**" — and 0071 never cites step 7a anywhere
(`grep 'step 7a' 007*.md` returns 0070, 0073, 0074 only). `0073:222` and `:430` — "the six
factory-interface signatures ADRs 0070 **and 0071** change" — wrong on count (it is eight), on
attribution, and on the word *factory* (four of the eight are registries or `IAmALifetime`).
`0073:411` — "the **other three breaks** ADR 0070 step 7a lists". An implementor writing
`release_notes.md` from step 7a ships a note omitting a source-and-binary break on two public core
interfaces with 28 implementations between them.

**Recommendation**: Amend step 7a to enumerate five, naming 0071's break explicitly. Change 0071's
Negative bullet from a free-floating "Needs a release note" to a pointer at step 7a's single entry,
as 0073 and 0074 both do. Fix 0073's two sentences to "the eight factory, registry and handler
interface signatures ADRs 0070 and 0071 change". Re-check every "four"/"three" count in the set
afterwards. **See also finding 48 — `PipelineBuilder`'s public constructors may be a sixth.**

---

### 6. 0070 — The failed-build cleanup order, as specified, leaks the pipeline scope when the mapper release throws (Score: 70)

Step 4 specifies the scope release as happening "after releasing whatever leases were taken", with
no `finally` and no guard. In the existing method the transform releases *are* individually guarded
(`:219-223`, with a source comment explaining exactly this hazard) but
`_mapperRegistry.Release(messageMapperLease)` at `:244` is **not**. An implementor who appends the
scope release after `:244` — the literal reading — produces a path where a throwing mapper `Release`
skips the scope release entirely, leaking the resource FR-5 and NFR-5 bound. Two developers would
implement this differently: one appends a statement, one wraps in `try/finally`.

**Evidence**: `0070:327` — "when it was not, the cleanup releases the scope directly, after
releasing whatever leases were taken." Source `TransformPipelineBuilder.cs:243-244`:
`if (transformLeases is not null) ReleaseTransforms(transformLeases);` /
`if (messageMapperLease is not null) _mapperRegistry.Release(messageMapperLease);`.
`ReleaseTransforms` guards each release (`:221-222`); `:244` does not. The source's own comment at
`:215-218`: "release every transform even when one Release throws … skipping the rest would leak
their DI scopes permanently."

**Recommendation**: State the ordering as a `finally`, not a sequence — the owned scope release must
run whether or not the lease releases threw, naming the existing per-transform guard at `:219-223`
as the precedent and `:244` as the gap.

---

### 7. 0072 — "The four builder `catch` blocks" — the ADR then enumerates six (Score: 70)

The count is stated twice and the enumeration beside it names six distinct code sites. An
implementor who patches four leaves two wrapping catches unable to let `AmbientScopeSourceException`
through, silently reinstating the `ConfigurationException` degradation FR-24.1 forbids on those
paths.

**Evidence**: `0072:322` — "teach **the four** builder `catch` blocks to recognise it". `0072:336` —
"**1a. The four builder `catch` blocks learn one clause.** Ahead of each existing wrapping `catch` —
`PipelineBuilder.cs:202` and `:248`, `TransformPipelineBuilder.cs:116` and `:157`, **and the same
two lines in `TransformPipelineBuilderAsync`**" — which is six. The `Where each type is touched`
table agrees with six. Verified: `grep -n catch` gives `PipelineBuilder.cs:202`, `:248`,
`TransformPipelineBuilder.cs:116`, `:157`, `TransformPipelineBuilderAsync.cs:116`, `:157`.

**Recommendation**: Say "six" in both places, or "the wrapping `catch` in each of the three
builders' two build paths".

---

### 8. 0071 — `SimpleHandlerFactory` is public, in core, and missing from `Where each type is touched` (Score: 68)

The forces bullet counts **5** `IAmAHandlerFactory` implementations in `src/`. The implementor's
table lists **4**. The missing one is a public type in `Paramore.Brighter` that will not compile
after the change.

**Evidence**: `0071:106` — "implemented by 21 classes in this repository (5 in `src/`, 16 test
doubles)". The table (`:246-252`) lists `IAmAHandlerFactory`, `IAmALifetime`, `HandlerLifetimeScope`,
`PipelineBuilder<TRequest>`, `SimpleHandlerFactorySync`/`SimpleHandlerFactoryAsync`,
`ControlBusHandlerFactorySync`, `ServiceProviderHandlerFactory`.
`src/Paramore.Brighter/SimpleHandlerFactory.cs:11` —
`public class SimpleHandlerFactory(Func<Type, IHandleRequests> factory, Func<Type, IHandleRequestsAsync> asyncFactory) : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync`.
`grep -n "SimpleHandlerFactory"` on the ADR returns only `:250` (the *Sync*/*Async* pair) and `:334`
— the type itself is named nowhere, and is not in the "Unchanged" sentence at `:254`.

**Recommendation**: Add a row: `Paramore.Brighter` | `SimpleHandlerFactory` (`:11`) |
`CreatePipelineScope()` returns `null`. As it implements both twins it is also the second in-repo
case (with `ServiceProviderHandlerFactory`) that alternative 6's "one declaration, not two" argument
is about — worth the cross-reference.

---

### 9. 0072 — Row 1's outcome and the "exactly one ask" derivation are stated over both pipeline families but hold only for the transform family (Score: 68)

Row 1 explicitly covers the handler factory, and its Outcome cell promises 0070's routing will ask
"the next participant". For a handler pipeline there is no next participant and 0070's routing does
not apply — the handler participating set is `{HandlerLifetime}` alone. The same over-generalisation
is repeated as the *derivation* of D16, the one property that most needs deriving rather than
asserting: for handler pipelines it is asserted.

**Evidence**: Ladder row 1 Situation — "**the factory being asked** has no scope to offer — … **or,
for the handler factory, is `Singleton`**"; Outcome — "`null`: this factory offers nothing, and **ADR
0070's routing asks the next participant**". `0072:106` — "what makes them hold is **ADR 0070's
first-non-null routing**". Step 2 — "D16's *exactly one ask per pipeline* is delivered by **ADR
0070's first-non-null routing**: the mapper registry is asked first and the transformer factory only
if the registry offered nothing". But that routing is a private helper of the *transform* builder
(`0070:323`). The handler side has one factory and no routing (`0071:179`, `:227`). 0072's own
participating-set table (`:386`) confirms the handler set is `{ HandlerLifetime }` "alone".

**Recommendation**: Split row 1's Outcome by family — transform: "0070's routing asks the next
participant"; handler: "the pipeline takes no pipeline scope and makes no ask". State D16's delivery
in two clauses: 0070's first-non-null routing for transforms, one-factory-called-once (0071) for
handlers.

---

### 10. 0073 — The `InternalsVisibleTo` rejection is credited to ADR 0072, but that argument lives in ADR 0075 (Score: 68)

`0073:253` justifies making `ScopeAffinityOverride` public by pointing at a passage that does not
exist in the ADR it names — fallout from the 0072 → 0072 + 0075 split. 0072 no longer contains any
suppression rationale and says so in terms, so a reader following the citation lands on a
disclaimer.

**Evidence**: `0073:253` — "`InternalsVisibleTo` was rejected for the reason ADR 0072 gives about
suppression". `grep -rn "InternalsVisibleTo" docs/adr/007*.md` returns **only** `0075:214` and
`:281`. `0072:33` explicitly disclaims the topic: "It does not decide **how a `Publish` subscriber
suppresses adoption** … that is ADR 0075, which owns the flag."

**Recommendation**: Change to "for the reason ADR 0075 gives about the suppression holder (`0075`
*Technology Choices*, 'Why the holder is public for read *and* write')", and add 0075 to
`Related ADRs` (finding 31).

---

### 11. SET — ADR 0073 still carries three decisions (Score: 68)

The set claims one decision per ADR, and 0072 was split on exactly this ground. 0073 has not been.
Its own text separates the three parts by kind, its bolded Decision sentence unifies only the third,
its title is three clauses joined by commas, it is the only row in the shared sibling map that lists
three things, and its Alternatives partition into three non-overlapping groups — the same signature
that justified splitting 0072.

**Evidence**: `0073:32` — "This ADR decides **three things** that are one gesture — the opt-in
property on `IBrighterOptions`, the ASP.NET package and its single registration extension, and the
mechanism by which that extension's affinity argument reaches the object `IBrighterOptions` resolves
to". `0073:27` — "Two of its three parts are naming; the third is not." `0073:89`, the one bold
Decision sentence — "The opt-in gesture does not write the affinity onto the options object; it
deposits the value in the service collection, and the one place that does have the options object …
picks the value up." Neither the property's existence and `AlwaysNew` default, nor the new package,
its name, its target frameworks or its SDK choice, follows from that sentence. The sibling-map row,
identical in all six — "the **opt-in** property, the ASP.NET package, and how that setting reaches
all four registration paths" — where every other row states one thing. Alternatives partition: **2**
and **6** belong to the property; **9** and **10** to the package; **3, 4, 5, 7** to the
write-through. Independence is stated at `:425`: "an `AsyncLocal`-backed provider package for
console hosts registers its provider and its override in exactly the same two lines".
`#### The three C-11 working names` (`:338-365`) and `:391-395` are self-contained decisions.

**Recommendation**: Split 0073 into two — the option and the order-independent write-through in one;
the ASP.NET package, its names, its extension signature and its target frameworks in the other. Both
sibling maps then become seven rows with one thing each. If the set is to stay at six, 0073 must
state a single unifying sentence covering all three parts, and its map row must reduce to it.
**Owner decision 1 below.**

---

### 12. 0072 — "The same two lines" in the other four factories resolves for only two of the five (Score: 66)

The premise `ScopeAffinityPolicy` rests on — that all five container-backed factories read
`IBrighterOptions` — is true and was verified for all five. The citation given for it is not: three
of the five are at different lines. This is the family-without-re-reading-every-member failure mode.

**Evidence**: `0072:231` — "all five container-backed factories already read `IBrighterOptions` in
their constructors (`ServiceProviderMapperFactory.cs:44`, and **the same two lines** in
`ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`,
`ServiceProviderTransformerFactoryAsync` and `ServiceProviderHandlerFactory`)". Actual:
`ServiceProviderMapperFactory.cs:44-45` ✓; `ServiceProviderTransformerFactory.cs:44-45` ✓;
`ServiceProviderMapperFactoryAsync.cs:`**`45-46`**; `ServiceProviderTransformerFactoryAsync.cs:`**`45-46`**;
`ServiceProviderHandlerFactory.cs:`**`49-50`**.

**Recommendation**: Replace with the five explicit citations, or drop "the same two lines" and cite
only the exemplar plus the class names.

---

### 13. 0070 — `Reactor.cs:636` does not resolve; it is a blank line (Score: 65)

Cited three times as the site of `FailedToReleasePipeline`. Line 636 is empty; the `[LoggerMessage]`
attribute is at `:637`. The ADR's two parallel citations use the *attribute* line consistently, so
this is a one-line slip in a set of three that are otherwise uniform — and one of the sites step 4a
tells an implementor to edit.

**Evidence**: `0070:196`, `:257`, `:329` all cite `Reactor.cs:636`. Source: `R636` blank, `R637`
`[LoggerMessage(LogLevel.Warning, "MessagePump: Failed to release the transform pipeline …")]`,
`R638` the method. Comparators: `Proactor.cs:651` = attribute ✓; `OutboxProducerMediator.cs:1448` =
attribute ✓.

**Recommendation**: Change all three to `Reactor.cs:637`.

---

### 14. 0073 — `RegisterBrighterOptions` has no contract table, and a pre-existing `IBrighterOptions` registration silently defeats the whole opt-in (Score: 65)

`#### RegisterBrighterOptions — where the override is applied` (`:257-297`) is where the whole design
lives, and it is the only significant new member without a **Contract.** table. Three error
conditions are unspecified. The third is the serious one: `TryAddSingleton` no-ops when
`IBrighterOptions` is already registered, so an application or test harness that registers it before
`AddBrighter` gets no override **on any path in any order** — the silent-total-failure mode FR-17
and AC-45 exist to prevent. The ADR reasons about `TryAdd` first-wins only *between the four Brighter
sites* (`:296`), and the Risks table only anticipates "a fifth registration path" (`:444`).

**Evidence**: `.agent_instructions/documentation.md` § *ADR structure* requires "each significant
type with a contract table (Member / Input / Output / Error conditions)". Unstated: null `services`
or `optionsFunc` (every neighbouring entry point guards explicitly —
`ServiceCollectionExtensions.cs:65-66`, `:92-95`; ServiceActivator `:33-34`, `:82-85`); a null
`optionsFunc` result, given the body does `options.DefaultScopeAffinity = over.Affinity` unguarded;
and the pre-existing-registration case. That pattern is live in this repo —
`tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/When_A_Handler_Is_Part_Of_An_Async_Pipeline.cs:23`
and ~10 siblings do `container.AddSingleton<IBrighterOptions>(new BrighterOptions {…})`.

**Recommendation**: Add the contract table covering the two null arguments, a null `optionsFunc`
result, and — explicitly — that a pre-existing `IBrighterOptions` registration wins the `TryAdd` and
the override is then never applied, stating whether that is accepted or diagnosed. **The
accepted-or-diagnosed half is owner decision 4 below.**

---

### 15. 0073 — The mixed-host risk row cites AC-43; the AC that carries that configuration is AC-20 (Score: 65)

AC-43 is about validation messages naming the guidance page and says nothing about mixed hosts,
ordering or overloads.

**Evidence**: `0073:446` — "**AC-43's mixed-host configuration** states which entry point registers
first and which `AddConsumers` overload is used, per C-12". `requirements.md:692-696` AC-43 —
"Every validation message points at the guidance. **Given** five hosts, each configured to trigger
exactly one finding…" — no mixed host, no ordering, no overload. `requirements.md:638-639` AC-20 —
"**Given** a mixed host that calls `AddBrighter(...)` before **`AddConsumers(Action<ConsumersOptions>)`**
— the `Action` overload specifically, because per C-12 the `Func<IServiceProvider, ConsumersOptions>`
overload throws `InvalidCastException` in this ordering…" — the cited content, verbatim. AC-20 is
also absent from 0073's References, which does carry AC-43.

**Recommendation**: Replace AC-43 with AC-20 in that row and add AC-20 to References. The same
substitution likely applies at `:292`.

---

### 16. 0074 — `ArtefactExclusionSet.Build(registry)` cannot produce the handler half of the exclusion set from the inputs it is given (Score: 65)

The exclusion is a conjunction over **two** attribute families, and the handler half is read from
`PipelineBuilder<IRequest>.Describe()` — which needs the pipeline builder, the publications, the
subscriptions and the registered handlers. The one factory signature written down takes only the
mapper registry. AC-42's `[UsePolicyAsync]` clause is pinned on that handler half.

**Evidence**: `0074:222` — `ArtefactExclusionSet.Build(registry.Value),   // Brighter's own
attribute-returned artefacts`. Step 5a (`:367`) ranges wider than its arguments allow: "makes that
pass once, over every request type reachable from the publications, the subscriptions and the
registered handlers." `:280` confirms both halves are needed. Verified: `PipelineBuilder.cs:151` is
`public IEnumerable<HandlerPipelineDescription> Describe()` — an instance method on the
`PipelineBuilder<IRequest>` the delegate constructs at `BrighterPipelineValidationExtensions.cs:75`,
and nothing in `Build(registry)` reaches it.

**Recommendation**: Give `ArtefactExclusionSet.Build` its real parameter list — the
`PipelineBuilder<IRequest>`, the `MessageMapperRegistry`, and the publications/subscriptions the
request-type enumeration walks — in both the sketch and step 5a, with one sentence on which input
serves which half.

---

### 17. 0074 — The `The mechanism, end to end` sequence diagram evaluates only four of the five rules; FR-17 is missing (Score: 65)

FR-17's repeated-opt-in rule is the fifth rule added by requirements revision 15, and the ADR's
tables carry it consistently everywhere else. The orienting artefact for the section the house style
says must lead with behaviour under-counts the decision the ADR exists to make, and the two diagrams
disagree with each other.

**Evidence**: `0074:138` — `Dec->>Dec: evaluate FR-22.1, FR-22.2, FR-22.3 and FR-24.3` (confirmed in
the rendered PNG). Against `0074:164` —
`rules["ScopeConfigurationRules — NEW, internal<br/>FR-22.1, FR-22.2, FR-22.3, FR-24.3, FR-17"]` —
and the five-row rule table at `:81-85`.

**Recommendation**: `Dec->>Dec: evaluate FR-22.1, FR-22.2, FR-22.3, FR-24.3 and FR-17`.

---

### 18. 0075 — The `References` requirement list does not match the body in either direction (Score: 65)

Two ids listed and never used; one FR used and not listed; and six AC ids cited in the body while
the References line carries no AC ids at all — 0075 is the only ADR of the six whose References line
omits them.

**Evidence**: `0075:289` lists "… C-5, C-13, D0b, D0c, D6, D10, D16, OOS-14". **`D16`** — `grep`
returns line 289 only; nothing in 0075 turns on it. **`C-13`** — same; it *is* relevant
(`requirements.md:382`) but the body never invokes it. **`FR-5` used and unlisted** — `:212`
"FR-5's failed-build release has nowhere to live". Six ACs used, none listed: AC-10 (`:255`), AC-11
(`:116`, `:270`), AC-12 (`:116`, `:239`, `:269`, `:285`), AC-22.3 (`:222`, `:254`), AC-39 (`:237`,
`:239`, `:269`), AC-47 (`:74`, `:233`, `:271`, `:279`). Every sibling lists ACs (`0070:437`,
`0072:461`, `0074:438`).

**Recommendation**: Drop `D16`; either cite `C-13` in the body or drop it; add `FR-5`; add
"; AC-10, AC-11, AC-12, AC-22.3, AC-39, AC-47" in the siblings' format.

---

### 19. 0075 — The risk table misdescribes what AC-47's two branches exercise (Score: 65)

The mitigation for "the two brackets drift apart" rests on AC-47 covering both bracket placements.
It does not. AC-47's first branch is a **`Send`**, not a subscriber, and both branches run
`{HandlerLifetime = Transient, …}` — so neither has a subscriber that takes a pipeline scope, and
neither exercises the resolution-time bracket. Removing bracket 1 would not fail AC-47.

**Evidence**: `0075:271` — "**AC-47's two branches exercise a subscriber that takes a pipeline scope
and one that does not**, so a bracket removed from either path fails a test rather than degrading
quietly." Against `requirements.md:764-768`: branch 1 is a `Send` — "the parent handler pipeline is
`Transient`, so it takes no pipeline scope (FR-27.1) and, **not being a subscriber**, suppresses
nothing"; branch 2 — "**Suppression here must come from FR-9's execution-time bracket**". The ADR
reads AC-47 correctly elsewhere (`:208`, `:233`, `:279`), so `:271` is the outlier.

**Recommendation**: Restate against the criteria that actually cover the two brackets — AC-11's
closing note ("this AC fails unless FR-9's **resolution-time** bracket (a) is implemented; an
execution-time bracket alone cannot make it pass", `requirements.md:483`) and AC-12/AC-39 for
bracket 2, with AC-47 branch 2 for the takes-no-scope case.

---

### 20. SET — ADR 0073 renames the registration extension, and the approved requirements — including three ACs — still name the method it replaced (Score: 65)

C-11 authorises the ADR to change the spelling, and 0073 does so with a full rationale. Nothing in
the set records the propagation obligation, and revision 15 still spells the old name in seven
places, three of them ACs that tests will be written from. The requirements are now internally
inconsistent too: the revision-15 history entry uses the new name while the body uses the old.

**Evidence**: `0073:344` — "**`AddBrighterAspNetCoreScopes(...)` — rejected, and replaced by
`AddBrighterRequestScope(ScopeAffinity affinity = ScopeAffinity.JoinAmbient)`.**"
`grep -n 'AddBrighterAspNetCoreScopes' requirements.md` returns seven hits: `:235` (FR-24.2), `:274`
(FR-17, twice), `:343` (FR-25.11), `:372` (C-11's working-name list), and **`:583` AC-26**, **`:639`
AC-43**, **`:738` AC-48** — each naming the call the test host must make. Against `:824`, the
revision-15 history entry, which already uses the new spelling. 0073 has updated its own half
(`:411`), so the inconsistency is invisible from inside any single ADR.

**Recommendation**: Either amend the requirements to the new spelling in all seven places, or have
0073 state the obligation explicitly under *Implementation Approach*. Do not leave three ACs naming
a method no ADR will produce. **Owner decision 2 below** — round 1 deliberately left this alone on
the grounds that C-11 reserved the spelling to the ADR as a working name, and that calculus has
changed now the naming decision is made.

---

### 21. SET — FR-13 is claimed as discharged by 0072, but the half of it that specifies behaviour is decided only in 0070, which does not claim it (Score: 64)

FR-13 has two clauses. 0072's Scope claims the whole requirement; its body only ever cites FR-13 for
the *ownership* clause and says nothing about the disposal-failure clause, its log level, its
swallow-and-do-not-latch rule, or AC-33. 0070 decides all of that, in step 4a, while its Scope claims
only FR-1…FR-7 and C-19. An auditor tracing FR-13 lands on 0072 and finds no message specification.

**Evidence**: `requirements.md` FR-13 second clause — "Where releasing an **owned** pipeline scope
throws… the failure must be logged at `Error` and swallowed, and the caller's result returned
unchanged. … Discharged by AC-33." `0072:31` claims FR-13; all six of its FR-13 citations (`:68`,
`:96`, `:225`, `:413`, `:461`) are about ownership, and its References omit **AC-33** entirely.
`0070:334` — "`FailedToDisposePipelineScope` — `LogLevel.Error`, emitted where an owned scope's
release throws … nothing is latched (**FR-13, AC-33**)" — yet `0070:30` claims only FR-1…FR-7 and
C-19.

**Recommendation**: Split the claim the way 0072 and 0074 already split FR-24.3 and FR-17 — 0070's
Scope discharges FR-13's disposal-failure clause (AC-33), 0072's the ownership clause only. One
sentence in each Scope.

---

### 22. 0070 — The `References` requirement list omits eleven ids the body uses (Score: 62)

**Evidence**: Present in the body, absent from the References line: `AC-8` (`:196`, `:338`, `:410`),
`AC-30` (`:230`, `:293`, `:308`), `C-2` (`:264`, `:363`, `:429`), `C-18` (`:361`), `D7` (`:279`),
`FR-16` (`:279`), `FR-22` (`:34`, `:357`), `FR-24` (`:230`, `:293`), `FR-25` (`:414`), `OOS-7`
(`:349`, `:384`), `OOS-8` (`:88`, `:394`). (`FR-2`…`FR-6` are covered by the "FR-1 … FR-7" range and
`FR-27.1/.2` by "FR-27"; these eleven are not.)

**Recommendation**: Add all eleven. `D7`, `FR-16`, `AC-8` and `AC-30` are load-bearing — they are
what a reader checking the 0070/0072 boundary searches for.

---

### 23. 0070 — "The three lifetimes all five container-backed factories already read from `IBrighterOptions`" is false, and mis-states what 0072 decided (Score: 62)

This is the hand-off sentence telling the reader FR-27.2's affinity computation costs nothing to add
later. It is wrong twice. No factory reads *the three lifetimes*: each reads exactly one, and the
four mapper/transformer factories retain neither the options object nor the `IServiceProvider`
afterwards — their only field is `_lifetimeScope`. And the ADR says the computation "belongs to the
factory that answers `CreatePipelineScope()`"; 0072 decides the opposite siting explicitly.

**Evidence**: `0070:345` — "That computation belongs to the factory that answers
`CreatePipelineScope()`, and **ADR 0072 supplies it**, over the three lifetimes all five
container-backed factories already read from `IBrighterOptions`. Nothing here changes when it
arrives". Source: `ServiceProviderMapperFactory.cs:44-46` reads only `options?.MapperLifetime`;
`ServiceProviderTransformerFactory.cs:44-46` only `options?.TransformerLifetime`; the sole field in
each of the four is `private readonly ServiceProviderLifetimeScope _lifetimeScope;`. Sibling —
`0072:80` "The affinity is computed by a **policy object rather than by each factory**"; `:231`
"**Today each factory keeps only its own**; from here each keeps the policy instead."

**Recommendation**: Replace with the accurate version — each container-backed factory reads
`IBrighterOptions` in its constructor and keeps only its own lifetime; `IBrighterOptions` carries all
three, so the information is reachable, and 0072 decides both what computes the affinity and what
each factory retains instead. Drop "belongs to the factory" and "Nothing here changes when it
arrives".

---

### 24. 0071 — The `CreatePipelineScope()` throw contract claims identity with ADR 0070's, but states only one of its two failure modes (Score: 62)

The paragraph exists to say "everything about this member is 0070's *except* the null rule". That is
not true of the throw behaviour: 0070 documents two failure modes discriminated by exception type,
precisely because folding both into `ConfigurationException` breaks FR-24.1 — and AC-30 is written
over `Send`, the handler pipeline this ADR is about.

**Evidence**: `0071:204` — "The member's **shape** is ADR 0070's, and so is its throw behaviour — a
throw is turned into `ConfigurationException` by the caller's existing guard." Contract table
(`:227`) gives one error condition. `0070:230` — "**Two failures, discriminated by exception type…**
A throw from the **ambient source** ADR 0072 adds inside this member is wrapped in that ADR's
`AmbientScopeSourceException`, which the builders' `catch` blocks let past cleanup and rethrow
unwrapped (FR-24.1, AC-30)." `requirements.md:509` AC-30 is a **`Send`** scenario. `0072:307`
correspondingly amends `PipelineBuilder.cs:202-204` and `:248-250`.

**Recommendation**: Say the shape and the *create-failure* behaviour are 0070's, and that 0070's
second failure mode is not yet in play because this ADR makes no ambient ask — with a forward
pointer to 0072, which widens the contract and amends both `PipelineBuilder` `catch` filters.

---

### 25. 0073 — The namespace departure's second justification is a non-sequitur (Score: 62)

The departure to `namespace Microsoft.Extensions.DependencyInjection` rests on two grounds. The
first (ASP.NET implicit usings) is true and verified. The second — that a Brighter namespace would
need a `using` the application may not have — only holds if the new package must declare a *new*
namespace, which nothing forces. The ADR never considers declaring the extension in
`Paramore.Brighter.Extensions.DependencyInjection`: the namespace `AddBrighter` already lives in,
already in scope in every `Program.cs` that calls it. That third option would meet the zero-import
goal *without* the departure, so the departure is argued against a field of two when there are
three.

**Evidence**: `0073:363` — "…while the Brighter namespace would need one that the application may
otherwise not have, **because it is a *new* package's namespace and not the one `AddBrighter` lives
in**." C# namespaces are independent of assemblies — the sentence treats a repository convention
(verified: every Brighter package declares a namespace matching its assembly) as a constraint, in
the paragraph whose purpose is to depart from convention.
`grep -rn "namespace Microsoft.Extensions.DependencyInjection" src/` returns nothing, confirming the
convention claim itself.

**Recommendation**: Either add `Paramore.Brighter.Extensions.DependencyInjection` as a rejected third
option with its real cost (a package declaring another assembly's namespace, itself a convention
break that can confuse tooling), or drop the second ground and rest on implicit usings alone.

---

### 26. 0073 / SET — "It adds no validation rule" is contradicted three times in the same document, the FR-17 discharge claim conflicts with 0074, and the two ADRs specify different messages (Score: 62)

*(Merges the 0073 reviewer's finding and the set-level reviewer's — the same boundary breach, seen
from both sides.)*

`Scope` makes two unqualified claims — that 0073 discharges FR-17, and that it adds no validation
rule. The body then specifies the FR-17 repeat rule's condition, severity and message content; 0074
claims the FR-17 evaluation site for itself; and the message content 0073 states is a proper subset
of 0074's. An implementor working from 0073 writes a message missing a clause; one working from 0074
writes a message with a clause AC-49 does not ask for.

**Evidence**: `0073:32` — "It discharges FR-14, FR-15 and FR-17." `0073:36` — "**it adds no
validation rule**." `0073:336` — "validation reports a `Warning` naming every affinity registered,
identifying the last as effective, and naming the guidance page (AC-49)" — a rule's message
contract. `0073:434` — "costs a fifth validation rule in ADR 0074". `0074:32` — "It discharges FR-22
and **the evaluation-site half of FR-24.3 and of FR-17**." Message divergence: `0074:264` requires
**four** elements — "every `ScopeAffinity` value registered; which is effective (the **last**…);
**that the extension is called once and its argument is how an affinity is selected**; the guidance
page" — against 0073's three and AC-49's three ("naming both `AlwaysNew` and `JoinAmbient`,
identifying `JoinAmbient` as the effective value, and containing the literal string
`docs/guides/lifetimes-and-scoping.md`"). 0073 demonstrates it knows the distinction — `:34`
separates *served* from *discharged* for FR-19 and FR-21, and `:413` hands the site to 0074.

**Recommendation**: In 0073's `Scope`, "discharges FR-14, FR-15, and the registration half of FR-17
(its evaluation site is ADR 0074's)"; replace "it adds no validation rule" with "it decides no
evaluation site and adds no rule against FR-17's *other* configuration error". Cut `0073:336` back
to the registration mechanism and end with a pointer. Keep the message specification in exactly one
place — 0074 — and reconcile its fourth element against AC-49.

---

### 27. 0074 — `Both host shapes, enumerated` says the two mixed rows fire, without the hosting-package precondition that row 3 makes decisive (Score: 62)

Rows 4 and 5 both have `ConsumerOwnsValidation = true`, exactly the condition under which
`BrighterValidationHostedService` defers and `ServiceActivatorHostedService` — which nothing in
`src` registers — must be added by the application. Row 2 states that precondition; rows 4 and 5
silently assume it and answer "Yes". A mixed host that does not add the hosting package validates
nothing, and the table billed as walking "all five combinations" says otherwise. This is the D14 gap
the ADR is otherwise careful about.

**Evidence**: `0074:326-329`. Row 2: "Consumer — `AddConsumers`, hosting package registered | true
(`:60` or `:127`)". Row 4: "Mixed — `AddBrighter` then `AddConsumers(Action)` | true |
`ServiceActivatorHostedService`; … | Yes". Verified:
`ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:60` and `:127` set
`ConsumerOwnsValidation = true`; `BrighterValidationHostedService.cs:73` returns
`Task.CompletedTask` on that flag; no `AddHostedService<ServiceActivatorHostedService>` exists
anywhere in `src`.

**Recommendation**: Add "hosting package registered" to rows 4 and 5, or add a sixth row for the
mixed-without-hosting-package case answering **No** (D14), and adjust the sentence beneath.

---

### 28. 0074 — The first Positive consequence, "Core gains nothing. Not a type", is contradicted by the ADR's own change table and Negative bullet (Score: 62)

The ADR adds `SpecificationEvaluator` to `Paramore.Brighter`. The intended claim is "no *container*
type", but as written the bullet is false, and a reviewer skimming Consequences reaches the opposite
conclusion from `Where each type is touched` four sections earlier. Relatedly,
`SpecificationEvaluator` must be **public** for the DI-package decorator to call it — new public core
API surface whose accessibility the ADR never states.

**Evidence**: `0074:379` — "**Core gains nothing.** Not a type, not a parameter, not a reference."
`0074:337` — "| `Paramore.Brighter` | `SpecificationEvaluator` | **new** …". `0074:396` — "**Nine new
types in the DI package plus one in core.**" `0074:365` — the decorator "evaluates both entity
families through `SpecificationEvaluator`". Source: `PipelineValidator.cs:152` is
`private static void EvaluateSpecs<T>(...)`; the AC-22.3 guard returns **0 matches today**, so the
zero-before/zero-after claim holds — only the "not a type" clause is wrong.

**Recommendation**: "Core gains no *container* concept — not a type, not a parameter, not a
reference; the one core addition, `SpecificationEvaluator`, names none," and state its accessibility
in `Where each type is touched`.

---

### 29. SET — The sibling map's one-line description of 0074 names two of the five rules it owns, and FR-17's rule appears in no row of any of the six maps (Score: 62)

All six maps are byte-identical, six rows, correctly bolded — that part is clean. But 0074's row
describes it as "the lifetime and captive-dependency rules", covering FR-22.1/22.2/22.3 and neither
FR-24.3 nor FR-17. 0074's own title carries the same stale scope, and 0074 elsewhere argues
explicitly that the rule set is wider. A reader consulting the six maps for where FR-17's
repeated-opt-in warning is evaluated finds it nowhere.

**Evidence**: The row, identical in all six (`0070:46`, `0071:44`, `0072:47`, `0073:50`, `0074:68`,
`0075:48`): "| 0074 | **where** the lifetime and captive-dependency rules are evaluated |". Against
`0074:32` — "the evaluation site for FR-22's three rules, FR-24.3's duplicate-provider rule and
FR-17's repeated-opt-in rule" — and `:84-85`, which list both as rules four and five. `0074:357`
acknowledges it: "It is chosen over `LifetimeValidator` because **the rule set is wider than
lifetimes** (FR-24.3 is about a registration)" — while the map row and the file title still say
"lifetime and captive-dependency".

**Recommendation**: Change the row in all six to "**where** the five scope-configuration rules are
evaluated", and retitle 0074 to match. Regenerate `docs/adr/index.md` afterwards — never hand-edit
it. **Whether the slug changes too is owner decision 5 below.**

---

### 30. 0072 — "The requirements say the same" about the FR-23 / FR-24.2 ordering — they say the opposite (Score: 60)

The behaviour the ADR specifies is right; the justification misreports the document it is
discharging, so a later reader reconciling the two concludes one of them is wrong.

**Evidence**: `0072:299` — "FR-23 and FR-24.2 are **mutually exclusive** … so their relative order
is immaterial … **The requirements say the same.**" `requirements.md:244` — "**The overlap is real**
and survives FR-24.2's retitling … FR-23 instructs that a failed usability probe be **treated
exactly as 'no ambient'**, which is FR-24.2's condition verbatim. … **The evaluation order this
fixes is FR-24.4, then FR-23, then FR-24.2**".

**Recommendation**: Say what is true — the requirements fix FR-24.4 → FR-23 → FR-24.2; the ladder's
separation of "nothing came back" (row 6) from "came back and unusable" (rows 7–8) is a *stronger*
condition than FR-23's "treat as no ambient", which is what makes the two orders equivalent here.
Drop "The requirements say the same."

---

### 31. 0073 — `Related ADRs` omits 0074 and 0075, both of which the body relies on (Score: 60)

**Evidence**: The Related ADRs list yields `0072`, `0071`, `0070`, `0014`, `0067`, `0053`, `0064`,
`0033` — no `0074`, no `0075`. Body mentions of 0074: `:36`, `:72`, `:84`, `:177`, `:336`, `:387`,
`:413`, `:463`. 0075's substance appears at `:214` and, misattributed, at `:253`. `0074:443` carries
a `0073` entry, so the omission is one-sided. Same asymmetry in the id list: **D3** (`:224`), **D11**
(`:344`), **D14** (`:434`) and **FR-25.10** (`:434`) are used and absent.

**Recommendation**: Add both ADRs to Related ADRs with one-line reasons; add D3, D11, D14 and
FR-25.10 to the requirement id list.

---

### 32. 0074 — The `Failure modes` lead sentence is contradicted by the first row of the table it introduces (Score: 60)

**Evidence**: `0074:295` — "All but the last are cases where the rule reports nothing; the last is
the one case where it can report wrongly". `0074:299`, row 1 — "A registration made **after**
`ValidatePipelines()` was called | invisible, and on one branch **wrong**: … so a warning can be
raised about a dependency that is no longer `Scoped`, or withheld about one that now is".

**Recommendation**: "Two rows can report wrongly — the snapshot-staleness row and the Brighter-mapper
row — and both are marked; the rest are silent misses," which is what the table and the matching
Negative bullet (`:392`) already say.

---

### 33. 0074 — The FR-17 rule's inputs have no named entity type, and the roles-table sentence describing them is circular (Score: 60)

`ScopeConfiguration` is said to carry two families of registrations; only the first is given a type,
so an implementor has nothing to build the FR-17 rule's collection from, while the type inventory is
presented as complete. The parenthetical is also self-referential: it pairs a thing that may itself
be a position "with that position".

**Evidence**: `0074:192` — "the ambient-source and affinity-override registrations in registration
order — **each of the former** a `ScopeProviderRegistration`, which pairs an implementation type (or,
where none is statically known, a position) with that position". The flowchart entity node (`:165`)
lists only `ScopeProviderRegistration`; `Where each type is touched` (`:340`) lists the same eight
internal types. The rule needs those values — `:268` "The values are read from the descriptors'
`ImplementationInstance`", consistent with `0073:317`'s
`services.AddSingleton(new ScopeAffinityOverride(affinity));`.

**Recommendation**: Name the affinity-override element type (or state that the two families share
`ScopeProviderRegistration`), and rewrite the parenthetical as two clauses: "pairs an implementation
type with its registration position; where no implementation type is statically known, the position
stands in for it."

---

### 34. 0074 — Both `DescribeTransforms` citations point at the overload that takes `includeAsync`, and neither states the value (Score: 60)

`:270` is the three-argument overload; the two-argument one at `:255` defaults to
`includeAsync: false`. Under `false`, transforms declared only on an async-resolved mapper never
enter the exclusion set, so a Brighter-shipped transform reached only that way is warned against as
the user's — the precise failure the conjunction exists to prevent, and one AC-42's
`ClaimCheckTransformer` case (sync **and** async) cannot detect.

**Evidence**: `0074:280` and step 5a (`:367`) both cite `:270`. Source
`TransformPipelineBuilder.cs:255-257`:
`public static TransformPipelineDescription? DescribeTransforms(MessageMapperRegistry mapperRegistry, Type requestType) => DescribeTransforms(mapperRegistry, requestType, includeAsync: false);`
and `:270-272` the three-arg overload, whose doc comment says the async mapper's transforms are
unioned in "so a request type served only by an async mapper is still described".
`TransformPipelineBuilderAsync` has no describe path of its own, so this flag is the only route to
the async side.

**Recommendation**: Say `DescribeTransforms(registry, requestType, includeAsync: true)` in both
places, with one sentence giving the reason.

---

### 35. 0075 — The sequence diagram's bracket-2 ordering is only correct for the synchronous path (Score: 60)

The diagram covers both publish paths ("Handle or HandleAsync") but draws the restore *after* the
nested pipeline is created. On the async path the ADR's own Implementation Approach requires the
opposite order. The diagram also omits `Task.WhenAll` entirely, so nothing signals that the async
loop only *starts* subscribers. This is the one subtlety the ADR treats as load-bearing, and the
orienting artefact hides it — the exact misreading Alternative 5 exists to foreclose.

**Evidence**: `0075:104-111` draws `CP->>CP: Suppress()` / `CP->>Sub: Handle or HandleAsync` /
`Sub->>Nested: …` / `CP->>CP: restore, explicitly`. Against `:231` — "around the **invocation** of
`handleRequests.HandleAsync(@event, cancellationToken)` inside the start loop (`:596`), never around
`Task.WhenAll` (`:601`) … **disposing the bracket immediately afterwards**". Source:
`CommandProcessor.cs:591-599` is a start loop adding to `tasks`; `:601` is
`await Task.WhenAll(tasks)`. The sole participant is also named `CommandProcessor.Publish`, not both
methods.

**Recommendation**: Either split into two `alt` branches (sync: restore after `Handle` returns;
async: restore immediately after the invocation, nested dispatch on the branched flow after the
restore), or narrow the diagram to the sync path and let step 4's prose carry the async ordering.

---

### 36. 0070 — The test-double blast-radius counts are understated (Score: 58)

The ADR gives 67 test doubles (61 factory doubles across 34 files, 6 registry doubles across 3) and
79 total. The registry figure is right; the factory figure is not. Actual: **64 factory doubles
across 37 files**, giving **70 test doubles** and **82 implementations**. The numbers are repeated
four times and are the implementor's completion check for a change the ADR says "must land as one
commit or the build is broken in between".

**Evidence**: A multi-line-tolerant scan of `tests/**/*.cs` for classes whose base list names any of
the four factory interfaces yields 64 across 37 files (registry doubles 6 across 3 ✓). The
one-line regex yields 60/33; the misses are declarations with the base list on a following line or a
primary constructor — e.g.
`tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/When_a_mapper_release_throws_the_mapped_message_is_still_dispatched.cs:83`
and `.../When_disposing_a_running_dispatcher_it_drains_before_disposing_factories.cs:94`. The `src/`
count of 12 is correct and every one was verified.

**Recommendation**: Update to 64 / 37 / 70 / 82 at `0070:80`, `:291`, `:390`, `:412`. Use a
multi-line scan for the recount.

---

### 37. 0071 — Citation density in `The forces` and `Consequences` breaks the house rule by 4× and 5× (Score: 58)

**Evidence**: `.agent_instructions/documentation.md` § *ADR readability*: "**At most one per forces
or Consequences bullet.**" Forces bullet on NFR-4 (`0071:103`) carries four —
`ServiceProviderHandlerFactory.cs:129`, `:135`, `PipelineBuilder.Dispose()` (`:269-270`),
`CommandProcessor.cs:481`. Negative bullet (`0071:328`) carries five — `IAmALifetime.cs:34`,
`IAmAPipelineBuilder.cs:36`, `IAmAnAsyncPipelineBuilder.cs:37`, `CommandProcessor.cs:394`, `:575` —
with `:422-436` in the continuation. All six resolve correctly; the defect is placement.

**Recommendation**: Keep one anchoring citation per bullet and move the rest into `Implementation
Approach` or the `Where each type is touched` table.

---

### 38. 0073 — Context's second sentence names four types before the problem is stated (Score: 58)

The house style forbids this by name, and it is the one structural rule 0073 breaks — its heading
order, nesting and wording otherwise match the five siblings exactly.

**Evidence**: `0073:26` — "ADR 0072 built the seam. A pipeline that takes a pipeline scope asks an
**`IAmAScopeProvider`** exactly once, carrying a **`ScopeAffinity`** that **`ScopeAffinityPolicy`**
computes from **`IBrighterOptions`**…". `.agent_instructions/documentation.md` § *ADR structure*,
`## Context`: "**Do not open by naming four interfaces**". Compare `0072:23` and `0075:24`, which
open on behaviour.

**Recommendation**: Lead with the problem in plain language and defer the type names to the second
paragraph, which already carries the architectural problem well.

---

### 39. SET — AC-7 and FR-6's handler-pipeline clause are claimed by no ADR in the set (Score: 58)

FR-6 is cited only by 0070, and only for transform pipelines; AC-7, its handler-family criterion, is
cited by none of the six. 0071 is the ADR that changes *who* disposes a handler pipeline's DI scope
and makes that disposal unconditional and idempotent — precisely FR-6's guarantee for handlers — but
declares it discharges nothing and cites FR-7/AC-9 only.

**Evidence**: Coverage map across the six: `FR-6: 0070(2)`, both transform-side (`0070:338`,
`:437`). `AC-7` returns zero hits across all six. `requirements.md:449` — "**AC-7 (FR-6) — A throwing
handler still releases the pipeline scope, exactly once.**" `0071:30` — "It **discharges no new
requirement**" — while `:143` states the property AC-7 asserts: "the handle is disposed
**unconditionally**, which closes a latent leak", and `:344` — "`IAmAScope`'s disposal is
idempotent."

**Recommendation**: Have 0071 add FR-6 and AC-7 to its References and one sentence to its Scope — it
preserves FR-6 for the handler family and AC-7 is its regression guard on the handle path, alongside
the AC-9 duplication step 6 already requires. Or say in 0070 that FR-6's handler clause is preserved
by 0071 and guarded by AC-7.

---

### 40. 0071 — The "foreign handle" analysis misses the case where the foreign handle *is* recognised (Score: 55)

**Evidence**: `0071:230` states the rule as a type test — "Where `PipelineScope` is non-null but is
**not** a `ServiceProviderPipelineScope`, `ServiceProviderHandlerFactory` resolves through
`GetOrCreateLifetimeScope` exactly as it does today." `:232` enumerates the arrivals: "a caller
invoking the public `Create(Type, IAmALifetime)` with an `IAmALifetime` of its own, **or a lifetime
scope built by one factory and passed to another**." The second case, where both factories are
`ServiceProviderHandlerFactory`, produces a handle that **passes** the type test; under step 4 the
factory would resolve handlers from the *other* container's provider, and nothing says what should
happen.

**Recommendation**: Either narrow the enumeration (drop the second arrival, since the first covers
the reachable case), or make the rule discriminate — an identity check against the creating factory,
or an explicit statement that resolving from another factory's handle is accepted and is the
caller's error. The contract table's error column is where it belongs.

---

### 41. 0071 — The `References` requirement list does not match the body (Score: 55)

**Evidence**: References (`:364`) lists FR-7, NFR-1, NFR-3…NFR-8, C-1, C-2, C-6, D0, D2, D10; AC-9.
Body also uses **FR-22** (`:32`), **FR-25** (`:346`), **FR-8** (`:350`). Sibling 0072 lists both
FR-22 and FR-25 in its own References (`0072:461`), so the omission is 0071's, not a set convention.

**Recommendation**: Add FR-8, FR-22 and FR-25, marking the first two as *deferred to* rather than
*discharged* if the list distinguishes them.

---

### 42. 0072 — The `References` requirement list does not match the body in either direction (Score: 55)

**Evidence**: `D1` appears twice in the body (`:73`, `:453`) and is not in the `:461` list. `D10`
appears twice (`:316`, `:417`) and is not in the list. `D2` is in the list and appears nowhere in the
body. (Every FR/NFR/AC/C/OOS id matches, so these three are the whole delta.)

**Recommendation**: Add `D1` and `D10`; drop `D2` or cite it — 0073 owns the one-flag decision, so
dropping is probably right.

---

### 43. 0073 — `AddHttpContextAccessor` does not live in `Microsoft.AspNetCore.Http.Abstractions` (Score: 55)

Nothing breaks under the `FrameworkReference` — both are in the shared framework — but the error
propagates into the `netstandard2.0` argument, which is the paragraph's load-bearing claim.

**Evidence**: `0073:395` — "**`IHttpContextAccessor` and `AddHttpContextAccessor` live in
`Microsoft.AspNetCore.Http.Abstractions`**". Against
`/usr/local/share/dotnet/packs/Microsoft.AspNetCore.App.Ref/9.0.0/ref/net9.0/`:
`strings Microsoft.AspNetCore.Http.Abstractions.dll | grep -c AddHttpContextAccessor` → **0**;
`strings Microsoft.AspNetCore.Http.dll | grep -c AddHttpContextAccessor` → **1**. Consequently
`:393` and `:438` understate the dependency a `netstandard2.0` target would need. The conclusion
(drop `netstandard2.0`) is unaffected and if anything strengthened.

**Recommendation**: "`IHttpContextAccessor` lives in `Microsoft.AspNetCore.Http.Abstractions` and
`AddHttpContextAccessor` in `Microsoft.AspNetCore.Http`; the framework reference brings in both."
Adjust `:393` and `:438` to name both 2.2.x packages.

---

### 44. 0075 — The roles table lists `AmbientScopeSuppression` as a role, then the next paragraph says it is not one (Score: 55)

**Evidence**: `0075:161` — "| Suppression state | `AmbientScopeSuppression` (core) | **knowing**,
with a bracketing verb | …". `0075:166` — "`AmbientScopeSuppression` is deliberately **not** a role".
The stereotype is also a hybrid where the house style specifies one of knowing / doing / deciding.

**Recommendation**: Change `:166` to "is deliberately not an *injected* role" and settle the
stereotype on **knowing**, moving "with a bracketing verb" into the Responsibility cell.

---

### 45. 0075 — `Where the pieces live` renders with the two non-core subgraphs visually nested inside the core subgraph (Score: 55)

The diagram's whole point is the assembly boundary. Rendered, the
`Paramore.Brighter.Extensions.DependencyInjection` and "any other container package" boxes sit
**inside** the `Paramore.Brighter — core` box, reading as core containing the container packages —
the inverse of the claim. 0072's equivalent flowchart, rendered the same way, separates its three
subgraphs cleanly, so this is 0075's edge topology, not a mermaid limitation.

**Evidence**: `0075:130-151`; rendered at `-w 1600 -b white`, the core subgraph rect spans the full
width and encloses the other two. `:153` then reads the opposite off it: "The flag is the only thing
that crosses the assembly boundary, in one direction".

**Recommendation**: Declare `flowchart LR` (or place the two reader subgraphs above core), or
reverse the edge direction and label it `suppress -- "read by" --> facs`, then re-render and look at
it.

---

### 46. 0075 — The split left 0075 using `AlwaysNew` and "affinity" as bare terms it never introduces (Score: 55)

0075 is meant to stand alone, but the single line where its mechanism meets adoption is written in
vocabulary the reader can only have got from 0072.

**Evidence**: `0075:124` — "`affinity = AmbientScopeSuppression.IsSuppressed ? AlwaysNew : the policy
over the whole participating set`" — first occurrence of `AlwaysNew`, with no statement that it is a
`ScopeAffinity` value, where that type lives, or that it means "do not adopt". `:279` is the first
and only occurrence of `ScopeAffinity`, 155 lines later. "Affinity" is used bare from `:34`. By
contrast `0072:160` defines the enum before its ladder uses it. Similarly "the five container-backed
factories" appears at `:143`, `:164` and `:206` without 0075 ever naming them.

**Recommendation**: One clause on first use — "`AlwaysNew`, the `ScopeAffinity` value ADR 0072
defines meaning *do not adopt an ambient*" — and one parenthetical naming the five factories, or a
pointer to 0072's row that does.

---

### 47. 0075 — `Suppress()`'s contract omits cross-flow disposal, the most likely misuse of a public `AsyncLocal` bracket (Score: 55)

The contract enumerates double disposal, out-of-order disposal and non-disposal, but not disposal on
a *different* logical flow from the one that created the bracket — which with `AsyncLocal<bool>`
writes the restored value into the disposing flow and leaves the originating flow suppressed
forever. Since the entire justification for a **public mutator** is that third-party packages and
hosts will call it (NFR-7), and the worked example offered is "a background job started from a
request whose `HttpContext` still flows" — precisely a flow-crossing scenario — this is the error
condition the contract most needs to name.

**Evidence**: `0075:193` (the contract row) and `0075:214` (the use case).

**Recommendation**: Add an error-condition clause: the bracket must be disposed on the flow that
created it; disposal on another flow restores the captured value into *that* flow and leaves the
originating flow suppressed. State whether the implementation detects it.

---

### 48. 0075 — The `PipelineBuilder` constructor change is a change to a **public** type's **public** constructors, and Consequences does not say so (Score: 55)

Adding an optional parameter is source-compatible but binary-breaking for anything already compiled
against the three-argument signature. The Technology Choices rationale carefully establishes that
`IAmAPipelineBuilder<TRequest>` and `IAmAnAsyncPipelineBuilder<TRequest>` are `internal` so a `Build`
parameter would widen public surface — but does not notice that the class it chooses instead is
itself public. The Negative bullet reduces the change to two internal call sites.

**Evidence**: `PipelineBuilder.cs:37` —
`public partial class PipelineBuilder<TRequest> : IAmAPipelineBuilder<TRequest>, IAmAnAsyncPipelineBuilder<TRequest>`;
`:59` and `:76` are `public PipelineBuilder(...)`. The interfaces are internal as claimed
(`IAmAPipelineBuilder.cs:36`, `IAmAnAsyncPipelineBuilder.cs:37`). `0075:216` and `0075:261`.

**Recommendation**: Add to Technology Choices that `PipelineBuilder<TRequest>` is public and the
defaulted parameter is a binary-breaking (source-compatible) change to two public constructors, why
an added overload was not preferred, and the same to Negative. **Reconcile with finding 5 — this may
be a sixth release-note break.**

---

### 49. SET — C-14, the assumption that makes FR-19's inertness bound true, is cited by none of the six (Score: 54)

**Evidence**: Coverage map: `C-14` has zero citations across 0070–0075 (every other constraint C-1
to C-20 is cited by at least one). `requirements.md:383` — "**C-14 — A consumer pump thread is
assumed to carry no usable ambient `HttpContext`.** This is an **assumption, not a verified
invariant**: `IHttpContextAccessor` is `AsyncLocal`-backed and a `Dispatcher` started from within a
request could inherit one." The two ADRs describing exactly this case do so without the citation —
`0073:220` and `0072:287`.

**Recommendation**: Add C-14 to 0073's forces beside its FR-19 paragraph, in one sentence, and to
0073's References.

---

### 50. 0072 — `ScopeAffinityPolicy`'s null-options behaviour is specified against `BrighterOptions`' defaults, which are not the defaults the factories apply (Score: 52)

The outcome coincides today (both routes reach `AlwaysNew`), but the ADR states a premise about
existing code that is not true of it, and an implementor following it writes a policy that disagrees
with row 1's test.

**Evidence**: `0072:249` — "A `null` options object means the three lifetimes and the affinity option
take **their documented defaults**, so the policy answers `AlwaysNew`". `BrighterOptions.cs:20`,
`:52`, `:69` document all three as `Transient`. But `ServiceProviderMapperFactory.cs:45` and
`ServiceProviderMapperFactoryAsync.cs:46` are `options?.MapperLifetime ?? ServiceLifetime.`**`Singleton`**;
`ServiceProviderTransformerFactory.cs:45` and its async twin `:46` likewise; only
`ServiceProviderHandlerFactory.cs:50` is `?? ServiceLifetime.Transient`.

**Recommendation**: State the rule as "the policy answers `AlwaysNew` unconditionally when options
are `null`", and note the factories' existing null fallbacks are unchanged and are not the documented
property defaults.

---

### 51. 0074 — `ScopeConfigurationValidator` is public but is constructed with two internal types; its constructor's accessibility is never stated (Score: 52)

As written the sketch does not compile: C# rejects a public constructor whose parameter types are
less accessible (CS0051). The fix is an `internal` constructor, which works because the only call
site is in the same assembly — but the ADR is explicit about the public/internal split everywhere
else, so leaving this implicit invites the implementor to widen the entity types instead.

**Evidence**: `0074:201`, `:340` (both entity types "**new**, internal"), and the construction at
`:218-223`.

**Recommendation**: State that the constructor is `internal` while the type is public.

---

### 52. 0070 — Step 4a leaves it unspecified whether a scope-disposal failure on the failed-build path is logged once or twice (Score: 50)

Step 4 says release failures "are caught by the existing guard", which logs
`FailedToCleanUpAfterFailedBuild` at `Warning`. Step 4a says the new `Error` message is "emitted by
`CleanUpAfterFailedBuild`". Whether that catches-logs-and-swallows (one record) or
catches-logs-and-rethrows (two records) is not stated, and AC-6 only pins that the `Error` record
exists. Two implementors would produce different observable log streams from the same AC.

**Evidence**: `0070:327` and `0070:333`.

**Recommendation**: One sentence saying which. Swallow-after-logging is the natural choice given the
scope release is the last step and the outer guard exists to stop cleanup masking the build error.

---

### 53. 0071 — Nothing in 0071 says the `Transient` handle is not FR-27's pipeline scope (Score: 50)

0071 correctly justifies giving handlers a handle for `Transient`. What it never says is the
consequence at the seam — that this handle makes no ambient ask and is not what FR-27.1 calls a
pipeline scope. The member it hangs the rule on is named `CreatePipelineScope()`.

**Evidence**: `0071:99` — "a handler pipeline takes a handle whenever its lifetime is **not**
`Singleton`". FR-27.1 — "A pipeline none of whose participating factories is `Scoped` takes no
pipeline scope and asks nothing." AC-46's first branch requires "**zero** adoption decisions and **no
pipeline scope taken**". FR-27 is cited nowhere in 0071. The reconciliation exists only in the
sibling — `0072:398`: "That handle-for-`Transient` … is **not** FR-27's pipeline scope". 0071's
Positive section (`:317`) compounds it: "A borrowed ambient becomes what `CreatePipelineScope()`
returns, **for handler pipelines and transform pipelines alike**", with no lifetime qualifier.

**Recommendation**: Add one clause to `:99` or to the behaviour-by-lifetime table's `Transient` row,
cross-referencing 0072; add FR-27 to the deferral sentence and to References.

---

### 54. 0073 — The `IOptions` blast-radius bullet overstates what shares the mutated instance (Score: 50)

**Evidence**: `0073:432` — the object "hands to **anyone** resolving `IOptions`, `IOptionsSnapshot`
or `IOptionsMonitor`". `AddOptions()` registers `IOptions<>` → `UnnamedOptionsManager<>` (singleton,
own `_value`), `IOptionsSnapshot<>` → `OptionsManager<>` (**scoped**, own `OptionsCache<T>` per
scope), `IOptionsMonitor<>` → `OptionsMonitor<>` (backed by the separate `IOptionsMonitorCache<>`).
Only the `IOptions<T>` path shares the instance the delegate mutates.

**Recommendation**: Narrow to `IOptions<BrighterOptions>`, and note that `IOptionsSnapshot` /
`IOptionsMonitor` readers get their own instances and therefore see the application's value — an
arguably worse inconsistency worth stating in its own right.

---

### 55. 0074 — The `Dispose()` contract row omits the `MessageMapperRegistry` the code sketch says the decorator owns (Score: 50)

The contract table is what an implementor reads for the disposal obligation the ADR itself calls
"easy to miss and expensive to get wrong". It names only the inner validator; the registry — the
thing holding the mapper factory's DI scope — appears only in the sketch's trailing comment and in
step 5a.

**Evidence**: `0074:238`, against `:223` ("registry);  // owned: disposed with the decorator") and
`:369`. The double-disposal safety claim checks out: `MessageMapperRegistry.cs:360-362` is
`if (Interlocked.Exchange(ref _disposed, 1) != 0) return;` with a remark naming exactly this case.

**Recommendation**: "Disposes the inner `PipelineValidator` and the `MessageMapperRegistry` this
validator owns. Idempotent; safe against the inner validator disposing the same registry
(`MessageMapperRegistry.cs:360-362`)."

---

### 56. SET — 0070 and 0072 cite different line ranges for the same two `catch` blocks in `TransformPipelineBuilder` (Score: 50)

**Evidence**: Source (both builders line-identical here): wrap `catch` at `:116`, closing brace
`:125`, `throw` `:124`; unwrap `catch` `:157`, brace `:166`, `throw` `:165`. `0070:327` — "`:116-125`
for wrap and `:157-166` for unwrap … thrown at `:124` and `:165`" — correct. `0072:308` —
"**`:116-124` and `:157-165`**" — one line short at both ends. 0072 applies the same
catch-through-throw convention at `:307` for `PipelineBuilder`, so it is a convention divergence
rather than a typo — but the two ADRs still hand an implementor two different ranges.

**Recommendation**: Pick one convention — catch line through closing brace reads more naturally with
"the guarded region" — and apply it in 0072's touched table and step 1a.

---

### 57. 0072 — The concurrency citation for `WarnOnce` names the sync `Publish`, while the AC whose counts it protects uses `PublishAsync` (Score: 48)

The atomicity argument is correct and the conclusion survives; the twin cited is not the twin AC-11
exercises.

**Evidence**: `0072:297` cites `Parallel.ForEach` (`CommandProcessor.cs:481`) — the **synchronous**
`Publish`. AC-11 (`requirements.md:471-473`) is written on `PublishAsync`, whose concurrency is
`await Task.WhenAll(tasks)` at `CommandProcessor.cs:601`.

**Recommendation**: Cite both.

---

### 58. 0074 — D19 is listed in `References` but never used in the body (Score: 48)

**Evidence**: `0074:438` lists "D5, D8, D9, D11, D14, D15, D18, D19". Extracting every `D<n>` from
`:16-435` yields `D5 D8 D9 D11 D14 D15 D18` — no D19. The `Scope` section explicitly disclaims D19's
subject as 0072's.

**Recommendation**: Drop D19.

---

### 59. 0070 / SET — Step 7a numbers two of the breaks in the same paragraph that says no ADR numbers them (Score: 45)

*(Merges the 0070 reviewer's finding and the set-level reviewer's.)*

**Evidence**: `0070:361` — "**First**, the behavioural break… **Second**, the source and binary
break… **No ADR numbers them — the order they are written in is not a fact about the release.**"

**Recommendation**: Replace "First"/"Second" with a bulleted list or with the substance ("The
behavioural break: …"). Fold into finding 5's rewrite of step 7a.

---

### 60. 0074 — `Where the pieces live` has a subgraph that is not an assembly, and puts a DI-package type outside the DI package (Score: 45)

**Evidence**: The house style specifies "one `subgraph` per assembly". `0074:170-173` —
`subgraph hosts["the two validation hosts — unchanged"]`. But `BrighterValidationHostedService` lives
in `Paramore.Brighter.Extensions.DependencyInjection` and `ServiceActivatorHostedService` in
`Paramore.Brighter.ServiceActivator.Extensions.Hosting`.

**Recommendation**: Put `h1` inside the `di` subgraph and give `h2` its own subgraph named for its
assembly, keeping "unchanged" in the edge labels or the prose.

---

### 61. 0075 — Alternative 5's first half is rejected on taste, without citing the requirement that forecloses it (Score: 45)

**Evidence**: `0075:285` rests the rejection on "it is the placement that invites" a different
mistake and on "extent". `requirements.md:200` FR-9(a) — "The pipeline scope, **and the ambient
suppression FR-8 requires**, must be established around **each subscriber's own iteration** of the
build" — which settles it outright and is not cited.

**Recommendation**: Lead with FR-9(a) — per-iteration is required, not preferred — and keep the
extent argument as the *why* behind the requirement.

---

### 62. 0075 — Nine bare "ADR 0039" citations, including the opening sentence, where four ADRs carry that number (Score: 45)

**Evidence**: `ls docs/adr | grep ^0039` returns four files.
`grep -c "ADR 0039" 0075…md` = 9 (0071 = 2; the rest 0). `0075:26` — "ADR 0039 gives every `Publish`
subscriber its own DI scope". The disambiguating slug appears only at `:294`, 268 lines later.
C-16 (`requirements.md:385`) spells out the collision.

**Recommendation**: Give the first mention its slug once — "ADR 0039
(`0039-scoping-dependencies-inline-with-lifetime-scope`)" — as FR-8 does at `requirements.md:466`.

---

### 63. 0070 — `MessageMapperRegistry` is a core type but is specified under Implementation step "6. The container package" (Score: 42)

**Evidence**: The touched table correctly assigns it to `Paramore.Brighter` (`0070:249`), but step
6's heading is "**6. The container package.**" (`:340`) and its third bullet (`:346`) specifies the
registry's two new members. Source: `src/Paramore.Brighter/MessageMapperRegistry.cs:41`. Since
`Implementation Approach` is ordered in commit terms, this puts a core edit in the container-package
step.

**Recommendation**: Move the bullet into step 2, or retitle step 6 to cover both.

---

### 64. 0072 — Implementation step 1a bundles a structural tidy into a behavioural step (Score: 42)

**Evidence**: House style requires the numbered list "in commit order, structural changes separated
from behavioural ones per Tidy First". `0072:336` adds a new catch clause (behavioural) and
normalises two existing filter spellings (structural) in one step — `:248`'s
`when(!(e is ConfigurationException))` against `:202`'s `when (e is not ConfigurationException)`,
both verified verbatim.

**Recommendation**: Split the filter-spelling normalisation into its own numbered step ahead of 1a,
marked structural.

---

### 65. 0073 — The `AddBrighterRequestScope` code block omits the null guard its own contract promises (Score: 42)

**Evidence**: `0073:328` — "Throws `ArgumentNullException` on a null collection, matching
`AddBrighter` (`:65-66`)" — the cited lines are correct. But `:311-319`'s body opens straight on
`services.AddHttpContextAccessor();` with no guard.

**Recommendation**: Add the two-line guard to the code block, or drop "matching `AddBrighter`".

---

### 66. SET — "This ADR supersedes no prior ADR" is in Context in four files, buried in Implementation Approach in 0070, and absent from 0071 (Score: 42)

The set-level claim is clean — no supersession anywhere. The *placement* is inconsistent.

**Evidence**: `grep -n 'supersede' 007[0-5]-*.md` — `0072:35`, `0073:38`, `0074:56`, `0075:36` all in
`## Context`; `0070:369` inside `### Implementation Approach` at the tail of step 10; `0071` no hit.

**Recommendation**: Move 0070's sentence into `## Context`, and add one to 0071.

---

### 67. 0071 — "The four decorator resolutions" names threading methods, not resolution sites, and the two actual sites are never mentioned (Score: 40)

The error is conservative — a parameter would travel through *more* places than six, so Alternative
2's rejection stands — but it tells an implementor there are six touchpoints when there are eight,
and omits two internal types from the unchanged list.

**Evidence**: `0071:100` — "the two direct `Create` calls and the four decorator resolutions";
`:260` repeats it as "all six". Source has exactly four `Create(handlerType, lifetime)` call sites:
`PipelineBuilder.cs:191`, `:236`, `HandlerFactory.cs:47`, `AsyncHandlerFactory.cs:47`.
`PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`) each handle sync *and* async decorators
in one method and neither resolves anything itself. `HandlerFactory<TRequest>` and
`AsyncHandlerFactory` appear nowhere in the ADR.

**Recommendation**: Reword to name the four `Create` call sites reached through four methods that
thread `IAmALifetime`, and add both types to the unchanged list.

---

### 68. 0071 — "Keep passing unchanged" is true of the assertions but not of the file (Score: 35)

**Evidence**: `0071:308` — the two `FactoryLifetimeTests` "exercise the **no-handle** path and keep
passing unchanged." `TestLifetimeScope`
(`tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs:311`) implements `IAmALifetime`
with three members and no `PipelineScope`. It is one of the six test doubles the ADR's own Negative
bullet counts as breaking, so the file must be edited before it compiles.

**Recommendation**: "their assertions and setup keep passing unchanged; the double gains the one new
member, like the other five."

---

### 69. 0074 — In the FR-25 clause map, one "step" reference is unqualified where every other is (Score: 35)

**Evidence**: Rows 1, 6 and 11 name the ADR ("ADR 0070 step 7", "ADR 0070 step 7a", "ADR 0073 step
6"). `0074:48`, row 7, says only "step 7" — this ADR's own. All other targets resolve; the map has
eleven rows and no orphans.

**Recommendation**: "…and C-18's compatibility note in **this ADR's** step 7."

---

### 70. 0073 — "the user" appears once, where the rest of the ADR says "an application" (Score: 32)

Not an authoring-conversation reference — it means the API consumer — but it is the only occurrence
in a document otherwise disciplined about it, and the house style calls the word out specifically.

**Evidence**: `0073:457` — "reads as the yes/no question **the user** is actually answering".

**Recommendation**: "the question an application author is actually answering".

---

### 71. 0072 — The `Where the pieces live` diagram drops the nullability the ladder turns on (Score: 30)

**Evidence**: The node reads `IAmAScope GetAmbient(ScopeAffinity)`; the declaration at `0072:172` is
`IAmAScope? GetAmbient(ScopeAffinity affinity);` and the contract's Output column is "an ambient the
pipeline may adopt, **or `null`**". D17 fixes the contract with the `?`. `?` is safe in a mermaid
label, so there is no rendering reason for the omission.

**Recommendation**: `IAmAScope? GetAmbient(ScopeAffinity)` in the label.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 7 |
| 50-69 (Medium) | 49 |
| 0-49 (Low) | 15 |

**Total findings**: 71
**Findings at or above threshold (60)**: 35

### By ADR

| ADR | Findings | At or above 60 |
| --- | --- | --- |
| 0070 | 9 | 5 |
| 0071 | 9 | 3 |
| 0072 | 10 | 5 |
| 0073 | 10 | 5 |
| 0074 | 12 | 7 |
| 0075 | 11 | 4 |
| Set-level | 10 | 6 |
| **Total** | **71** | **35** |

*(0070's row excludes the two of its findings merged into findings 5 and 59; 0073's excludes the one
merged into finding 26.)*

---

## Decisions — all five taken, 2026-08-03. Do not relitigate.

Everything not listed here is mechanical — "no design decision needed, just fix". These five could
not be fixed without a choice; each was put with context, options and a recommendation, and chosen
explicitly.

| # | Question | Chosen | Consequences |
| --- | --- | --- | --- |
| 1 | **Split ADR 0073?** (finding 11) | **SPLIT into two.** One takes the `DefaultScopeAffinity` option and the order-independent write-through (`ScopeAffinityOverride`, `RegisterBrighterOptions`, all four registration sites); the other takes the ASP.NET package, its name, the extension's name and signature, and its target frameworks and SDK | Set goes to **seven** ADRs; **all seven** `### Where this ADR sits` maps become seven rows; index **97 → 98**; `.adr-list` gains an entry; the C-11 naming block splits across both. Same evidence shape that justified round 1's 0072 split — three parts separated by kind in the ADR's own text (`:32`, `:27`), a Decision sentence (`:89`) covering one of them, alternatives partitioning with zero overlap |
| 2 | **Amend the requirements for the `AddBrighterAspNetCoreScopes` → `AddBrighterRequestScope` rename?** (finding 20) | **AMEND — six of the seven sites.** `C-11:372` **keeps** the old spelling: it is the rejected-candidates list and recording what was rejected is its job | Rewrites `:235` (FR-24.2), `:274` (FR-17, twice), `:343` (FR-25.11), and **AC-26 `:583`, AC-43 `:639`, AC-48 `:738`**. Round 1's deliberate "leave it" is superseded: C-11 reserved the spelling to the ADR *as a working name*, and `0073:344` has since made the call, so those three ACs name a method no ADR will produce |
| 3 | **Is ADR 0071 still behaviour-preserving?** (finding 4) | **KEEP THE FIX, DROP THE CLAIM.** 0071 keeps round 1's fault-tolerant release loop and stops claiming inertness | The `AggregateException` break joins ADR 0070 step 7a's single release-note entry — which finding 5 is rewriting anyway. Rejected moving the fault-tolerance out: 0071 introduces a handle disposed *after* the handlers are released, so without the fix it would ship a scope leak on the very path it adds — which is why round 1 put the fix here |
| 4 | **A pre-existing `IBrighterOptions` registration silently defeats the opt-in** (finding 14) | **DIAGNOSE — a sixth validation rule in 0074.** An affinity override is registered but the resolved `IBrighterOptions` did not come from Brighter's own factory, so the override was never applied: `Error`, naming the guidance page | Sixth rule in 0074, a **requirements amendment** and a **new AC**. Follows round 1 decision 1's precedent — diagnosable over silently documented. Note the failure defeats the opt-in on **all four** registration paths in **any** order, including AC-48's before-ordering, and the pattern is live in ~10 test files today |
| 5 | **Retitle ADR 0074?** (finding 29) | **RETITLE, KEEP THE SLUG.** Frontmatter `title:`, the `# 74.` body heading, and the 0074 row in all seven sibling maps name the scope-configuration rules; filename unchanged | No cross-reference breaks. Regenerate the index in the **same commit** — expect a clean 1 insertion / 1 deletion. **With decision 4 the row must say SIX rules, not five.** Rejected the slug rename: a slug is an identifier, and churning it costs cross-reference integrity for a readability gain the title already delivers |

### What decisions 2 and 4 together mean for the requirements

Revision 16 carries **both** the rename (alignment) **and** a new rule plus a new AC (new scope). By
the owner's standing rule — *revision 15 was alignment, not scope, so it needed no independent
review round; an amendment that is more than alignment gets one* — **revision 16 needs its own
`/spec:review requirements` round** before re-approval. The rename alone would not have.

Re-run §7's count script afterwards. Expect **27 FRs · 10 NFRs · 50 ACs** and the bullet count up by
one from 121; update the script's expectations in PROMPT.md §7 to match, and re-stamp
`.requirements-approved` with the new revision so the marker does not go stale again.

### Sequencing — decisions 1 and 5 move content, so they go first

1. **Decision 1**, the 0073 split, and **decision 5**, 0074's retitle — both rewrite the sibling maps
   in every file. Do them together, in one commit, and regenerate the index.
2. **Decision 2 and decision 4's requirements amendment** — revision 16, then its review round.
3. **Decision 3** and the remaining 66 mechanical findings — every `file:line` in this review predates
   steps 1 and 2, so **verify each against the current file before acting on it**.
