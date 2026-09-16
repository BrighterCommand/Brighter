# Review: design — 0037-validate-subscription-channel-factory (ADR 0072, round 3)

**Date**: 2026-09-16
**Threshold**: 60
**Verdict**: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

> **Main-agent verification.** Per the standing rule that agent findings are claims, not facts, all
> seven were checked against the working tree before this file was written. All seven hold; none was
> rejected this round. Specifically verified: the five "four remedy templates" sites (440, 466, 467,
> 604, 777 — the other six `four` hits are unrelated); the two body templates at 471-472; the AC-7
> gloss at 484-487 against the nested paragraph at 491-493; `DeclaredChannelFactory`'s real namespace
> (`Paramore.Brighter.Core.Tests.Validation.TestDoubles`, pinned by AC-12) against the ADR's
> `Paramore.Brighter.MessagingGateway.…` at 503; the prescribed XML snippet's missing thread-safety
> remark; and `Arm` appearing only at line 393. **C-13's population claim was re-verified
> independently and is correct**: the only *reads* of `ChannelFactoryType` in `src/` are
> `CombinedChannelFactory.cs:34/46/59`; all eleven other mentions are declarations or overrides on
> subscription types. FR-5's five-condition table was also re-checked by hand and **is** total and
> non-overlapping over the nine reachable cells (3 + 2 + 2 + 1 + 1 = 9).

## Round 2 disposition

1. **(80) Nested-composite acceptance names the wrong template and claims an impossible remedy — PARTIALLY FIXED.** Sub-parts (a) and (b) are genuinely fixed. (a): the ADR now reads "*so `D == typeof(DeclaredChannelFactory)`, which is not the in-memory default, and FR-5 selects **T2***" — correct against the amended FR-5 table (Combined arm, non-empty candidate set, `D` not null, `D != typeof(InMemoryChannelFactory)` → T2). (b): the false "would in fact route" justification is gone and replaced with the honest account — "*Following that second half does **not** produce a working configuration, and the ADR should not pretend otherwise … the inner composite scans its own `[DeclaredChannelFactory]` … finds none, and throws at `CombinedChannelFactory.cs:35-38`*" — verified against `src/Paramore.Brighter/CombinedChannelFactory.cs:34-38` (non-recursive `FirstOrDefault` + throw). (c) is **not** fixed: round 2 asked to "reconcile with the earlier statement explicitly rather than by re-scoping AC-7 to 'the outer composite'", and the re-scoping is still there verbatim — "*The rule never does: in the combined arm `{F-list}` is built from `FactoryTypes`, which reports the *inner* factories, and the outer composite's own type never enters it*" — eight lines above "*`{F-list}` is literally `Paramore.Brighter.CombinedChannelFactory`*". See finding 3.
2. **(76) Direct-arm null-`D` is new breakage under NFR-6 — FIXED.** `requirements.md` now carries **C-13** in full, NFR-6 lists it ("*except in the cases documented as deliberate exceptions in **C-2**, **C-10**, **C-11**, **C-12** and **C-13***"), C-8 carries its release-note obligation, and the ADR's Risks entry now says the opposite of what round 2 objected to: "*In the *direct* arm there is no such consolation … Blocking it **is** new breakage, and it is recorded as **C-13** with its own release-note obligation under C-8*". A matching Consequences → Negative bullet exists. The Forces section also lists five exceptions, not four.
3. **(72) Two undeclared FR-5 deviations presented as costing "no new normative surface" — FIXED.** The claim is not merely deleted but retracted by name: "*An earlier draft of this ADR claimed the rendering 'costs no new normative surface'. That was wrong twice*", followed by the two reasons and the statement that "*Both are now settled in `requirements.md` rather than asserted here*". FR-5 item 2 now admits `no ChannelFactoryType`, and the selection table is restated as five ordered conditions. `grep` finds no surviving "no new normative surface" in the ADR.
4. **(66) AC-15's token rule never resolved as substring-or-token — FIXED.** AC-15 now defines token as a word-boundary match and pins `Regex.Matches(message, @"(?<![.\w])ChannelFactory(?!Type)")`, and the ADR states it: "*where "token" is now defined in the requirements as a word-boundary match and pinned to a normative regex. A composite identifier that merely *ends* in the word — `InMemoryChannelFactory`, `CombinedChannelFactory` — is a different token and is not an occurrence*". Round 2's specific complaint that `InMemoryChannelFactory` was never mentioned is answered.
5. **(62) Lazy `FactoryTypes` contradicts "materialised once"/"stable"/"not observable" — PARTIALLY FIXED.** The prose contradictions are all gone and replaced with an honest statement: "*If two threads first read `FactoryTypes` concurrently they may each build a list and one write wins, so a caller can observe two equal-but-distinct instances*", the contract now says "*Stable in the sense that every read yields an **equal** list; not guaranteed to yield the same instance*", and Performance is qualified ("*once per `CombinedChannelFactory` for the single-threaded startup path that is its only caller*"). The one sub-point round 2 raised that is still open is the prescribed XML doc: the normative snippet an implementer copies still carries no thread-safety remark, while the prose says "*`FactoryTypes` is therefore documented as *not* thread-safe*". See finding 5.
6. **(58) `(none)` renders a remedy with no remedy — PARTIALLY FIXED.** The *remedy* half is properly fixed: FR-5 gained T4, the ADR explains why ("*an empty list interpolated into T2/T3b would have ended the message at "is one of:" with nothing after it*"), and round 2's "state that it is combined-arm-only" is honoured ("*T4 is combined-arm only — the direct arm's candidate set is always exactly one type*"). The *body* half is not: the combined body template still interpolates `{F-list}` unconditionally, and the ADR asserts the opposite. See finding 1.
7. **(55) New test double extends C-9's closed set without flagging an amendment — FIXED.** The ADR now routes it correctly: "*that was the wrong route — C-9's double set is closed … so cases that need a sixth double are a requirements change. `requirements.md` was amended instead*", and C-9 now lists `NullDeclaringSubscription` with AC-10a/AC-10b/AC-10c added. Round 2's "name the subscription double in case 3" is done — AC-10c names `DeclaringSubscription` named `empty-sub`, and the ADR echoes "*against a named `DeclaringSubscription`*".
8. **(45) Implementation step 1 re-plants the wrong CS0236 reading and omits a `using` — FIXED.** Step 1 now reads "*— **not** a get-only auto-property initialised from `_factories`, which cannot compile (CS0236; an auto-property initialised from the primary-constructor `factories` parameter compiles fine, and is the wrong answer for the re-enumeration reason above). Add `using System;`*". The `using` claim was verified independently: `src/Paramore.Brighter/CombinedChannelFactory.cs:1-4` imports only the four namespaces named, no `ImplicitUsings` is set in `Directory.Build.props` or `src/Paramore.Brighter/Paramore.Brighter.csproj`, and there is no `GlobalUsings.cs` in the project — so `IReadOnlyList<Type>?` does require it.
9. **(45) `IsCompatible`'s signature cannot decide the two arms — FIXED.** The helper table now reads `IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)`, with "*Takes the arm explicitly: it is the discriminator `ResolveCandidates` returns, never re-derived by testing `F is CombinedChannelFactory` again*".

On the maintainer's four standing concerns, the verified positions are: the FR-5 remedy table **is** total and non-overlapping over the nine reachable cells (finding 1 concerns the *body*, not the remedy); T4 and every other template **pass** AC-15's regex; and C-13's grep claim **is** correct.

## Findings

### 1. The two message-body templates are not total over the input space the amended requirements added, and the ADR asserts the opposite (Score: 74)

The maintainer asked whether FR-5's remedy table is total. It is. The space — arm (Direct/Combined) × `D` (null / `InMemoryChannelFactory` / other) × candidate set (empty / non-empty), noting that the direct arm's set is never empty — gives nine reachable cells, and the five conditions cover them exactly once: T4 takes the three Combined×empty cells, T3a the two Direct×{null, InMemory}, T3b the two Combined×non-empty×{null, InMemory}, T1 the one Direct×other, T2 the one Combined×non-empty×other. 3+2+2+1+1 = 9, no cell double-covered, and the ADR's "T4 is selected first" matches FR-5's stated order.

The defect is one level up: the ADR owns the **body**, and it supplies exactly two body templates, neither of which covers two cells the amendment explicitly created acceptance criteria for.

**(a) Empty combined candidate set (AC-10c).** The combined body interpolates `{F-list}` unconditionally, so `new CombinedChannelFactory([])` renders `… but will be handed one of '' — add a channel factory to the combined channel factory`. The ADR's claim that this cannot happen is false *as a claim about its own rendering plan*, and it is falsified by the sentence directly before it, which says `{F-list}`'s separator is "shared between body and remedy" — i.e. `{F-list}` appears in both. T4 replaces only the remedy. Note that AC-10c does not catch this: it asserts the message ends with the T4 literal and contains no `is one of:`, both of which the degenerate body satisfies.

**(b) Null `D` (AC-10a, AC-10b).** Round 2 correctly forced the deletion of the old "costs no new normative surface" passage, but the sentence that carried the null-`D` body rendering ("the body reads 'declares no ChannelFactoryType'") went with it and was not replaced. The only surviving statement is the FR-5-level "*FR-5 item 2 admits the literal phrase `no ChannelFactoryType`*" — which says what the requirement permits, not what the message renders. Read literally, the prescribed direct template with a null `D` renders `declares ChannelFactoryType ''`, which does **not** contain the literal AC-10a demands — so the template as written fails its own acceptance criterion. Two implementers patching around that will produce `declares no ChannelFactoryType but will be handed '{F}'` and `declares ChannelFactoryType 'no ChannelFactoryType' but will be handed '{F}'` respectively; both satisfy AC-10a's `contains the literal` assertion, so the test does not discriminate either.

**Evidence**: ADR, message rendering section (lines 471-472):
```
direct   : Subscription type '{S}' declares ChannelFactoryType '{D}' but will be handed '{F}' {remedy}
combined : Subscription type '{S}' declares ChannelFactoryType '{D}' but will be handed one of '{F-list}' {remedy}
```
and, twelve lines later: "*`{F-list}` joins display names with `", "` in constructor order. The separator is defined **once** and shared between body and remedy, so the two cannot disagree. An **empty** candidate set never reaches `{F-list}`: FR-5's template **T4** is selected first*". Against requirements AC-10c ("*Given `options.DefaultChannelFactory = new CombinedChannelFactory([])`*") and AC-10a ("*whose `Message` contains the literal `no ChannelFactoryType` in place of a declared type name*").

**Recommendation**: Give the body four forms, not two — direct/combined × declared/undeclared — or state one rule that derives all four (e.g. the declared clause renders `declares ChannelFactoryType '{D}'` when `D` is non-null and `declares no ChannelFactoryType` when it is null; the handed clause renders `will be handed one of '{F-list}'` when the candidate set is non-empty and `will be handed no channel factory at all` — or similar — when it is empty). Then correct "An empty candidate set never reaches `{F-list}`" to say what is true: T4 keeps it out of the *remedy*, and the body handles it separately. The wording of C-13's release-note obligation in requirements ("*a startup `Error` reading `declares no ChannelFactoryType`*") is the natural anchor for the null-`D` body.

---

### 2. The ADR still says "four remedy literals/templates" in five places, contradicting the amended FR-5 and its own record of the amendment (Score: 64)

FR-5 as amended is explicit: "*Item 4 MUST be rendered as one of exactly **five** literals*", and the ADR itself records the amendment — "*FR-5's selection table is restated as five ordered, total conditions*". But five other passages, including a section heading and the mitigation for a named risk, still count four. The Risks sentence is the one with teeth: it is the ADR's licence for future editors to revise anything outside the normative set, and under it T4 is revisable body wording — which would break AC-10c.

**Evidence**: ADR section heading (line 440) "*#### 3. Message rendering — `DisplayName` and the four remedy templates*"; line 466, "*The message is assembled as a body plus one of FR-5's four remedy literals*"; line 467, "*Only the four remedy literals are normative; the body wording below is this ADR's proposal*"; Implementation Approach step 3 (line 604), "*with `ResolveCandidates` / `IsCompatible` / the four templates*"; Risks (line 777), "*Mitigation*: only the four remedy literals are normative; the body wording is this ADR's and may be revised as long as AC-12, AC-14 and AC-15 hold*". Against requirements FR-5's table, which lists T4, T3a, T3b, T1, T2, and the ADR's own "*FR-5's template **T4** is selected first*". (The document's six other uses of "four" — four existing rules, four imported namespaces, four families of host — are correct and unaffected.)

**Recommendation**: Change all five to "five", and in the Risks mitigation add AC-10c to the list of criteria that must continue to hold ("as long as AC-12, AC-14, AC-15 and AC-10c hold"), since T4's literal is now among the normative five.

---

### 3. The AC-7 gloss still claims the rule never names `CombinedChannelFactory` as the handed type, eight lines above the paragraph that shows it doing exactly that (Score: 62)

This is round 2's finding 1(c), which the revision did not address; its recommendation was explicit that the fix must not be a re-scope to "the outer composite", and the re-scope is what remains. The two passages cannot both be true of the AC-10 configuration: the combined body is `… but will be handed one of '{F-list}'`, and in that configuration `{F-list}` is the single string `Paramore.Brighter.CombinedChannelFactory`. The distinction the ADR draws — outer composite's type vs inner composite's type — is invisible in the rendered message, which is where AC-7's prohibition lives.

This does not break AC-7 as a test: AC-7's Given is AC-6's flat `CombinedChannelFactory([DeclaredChannelFactory, NonMatchingChannelFactory])`, which contains no nesting, so no implementation is at risk. It is scored as an honesty defect in the document rather than a functional one — and honesty is precisely what the surrounding passage is claiming for itself ("*and the ADR should not pretend otherwise*").

On the open judgement call the maintainer flagged: the *acceptance itself* is now recorded correctly and the reasoning is sound. The counterfactual was verified — for `CombinedChannelFactory([CombinedChannelFactory([DeclaredChannelFactory])])`, `FactoryTypes` is `[typeof(CombinedChannelFactory)]`, so filtering nested composites does leave an empty list and does select T4. The trade-off as stated (T4 says less about what is configured than a type name that does not route) is a defensible call to leave to the maintainer, and it is recorded under Negative. Only the AC-7 bullet is wrong.

**Evidence**: ADR lines 483-487: "*AC-7 forbids naming `Paramore.Brighter.CombinedChannelFactory` **as the type the subscription will be handed**. The rule never does: in the combined arm `{F-list}` is built from `FactoryTypes`, which reports the *inner* factories, and the outer composite's own type never enters it.*" Against, in the next paragraph (line 491): "*`FactoryTypes` reports the inner factories' concrete types, so `{F-list}` is literally `Paramore.Brighter.CombinedChannelFactory`*", and the body template `… but will be handed one of '{F-list}'`.

**Recommendation**: Replace "The rule never does" with the accurate statement and the scope that saves it — e.g. "The rule does so only when an inner factory is itself a `CombinedChannelFactory`; AC-7's configuration has no nesting, so the criterion holds, and the nested case is accepted below as a known message-quality limitation." That reconciles the two passages instead of re-scoping the criterion.

---

### 4. The worked remedy example renders a namespace the type does not have (Score: 55)

The passage illustrates the nested case's "working half" with a display name for `DeclaredChannelFactory` placed under `Paramore.Brighter.MessagingGateway.…`. `DeclaredChannelFactory` is one of C-9's core test doubles, which live under `tests/Paramore.Brighter.Core.Tests/Validation/TestDoubles/` in `Paramore.Brighter.Core.Tests.Validation.TestDoubles` — the namespace AC-12 pins verbatim for exactly these doubles. No `MessagingGateway` assembly contains it, so the quoted message is a string the rule can never produce for AC-10's configuration.

**Evidence**: ADR line 503: "*The message's *first* half — "either configure a channel factory of type `Paramore.Brighter.MessagingGateway.…DeclaredChannelFactory`" — is a working remedy*". Against requirements C-9 ("*One class per file under `Validation/TestDoubles/`*") and AC-12, which pins `Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest` as the namespace for that folder's types.

**Recommendation**: Render it as `Paramore.Brighter.Core.Tests.Validation.TestDoubles.DeclaredChannelFactory`, or elide the namespace entirely (`…DeclaredChannelFactory`) rather than inventing a wrong one.

---

### 5. The prescribed `FactoryTypes` XML documentation omits the thread-safety statement the ADR says it carries (Score: 52)

The thread-safety analysis is now honest — and the justification survives scrutiny for the in-repo caller: `sp.GetServices<ISpecification<Subscription>>()` occurs at exactly one site (`BrighterPipelineValidationExtensions.cs:79`), and `PipelineValidator.EvaluateSpecs` is a plain nested `foreach` over entities then specs, so the startup path is single-threaded as claimed. But the justification is "who calls it", and the ADR simultaneously sells the property as public surface for third parties ("*`FactoryTypes` makes a composite's routing set inspectable to anyone writing their own diagnostics*"), which is precisely the population that will not know. The mitigation the ADR names for that — documenting it — is not in the artefact an implementer copies.

**Evidence**: ADR prose: "*`FactoryTypes` is therefore documented as *not* thread-safe, and a caller needing concurrent access must synchronise or the property must be promoted to `Lazy<IReadOnlyList<Type>>` at that point.*" Against the normative snippet in the same section (lines 199-206), whose doc comment is three lines and mentions routing identities only:
```csharp
/// <summary>
/// The concrete types of the inner factories, in the order supplied to the constructor.
/// These are the identities this factory routes on: it can serve a subscription exactly when
/// that subscription's <see cref="Subscription.ChannelFactoryType"/> is one of them.
/// </summary>
```

**Recommendation**: Add a `<remarks>` line to the prescribed snippet saying the property caches on first read, is not thread-safe, and that every read yields an equal but not necessarily identical list. That also puts the routing contract and its caveat at the point a future editor of `CombinedChannelFactory` would change it, which is the mitigation the Risks section already promises.

---

### 6. `Arm` and `ResolveCandidates`'s return type are named but never defined (Score: 45)

The rule's internal structure is otherwise prescribed in detail — the `ChannelFactoryCompatible` signature is given in full, as is the `FactoryTypes` shape — but the discriminator the design turns on is left as a bare identifier. The ADR says `ResolveCandidates` "returns the arm and the candidate list together" and gives `IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)`, without saying whether `Arm` is a private enum, where it is declared, or whether `ResolveCandidates` returns a tuple, a private readonly record struct, or out-parameters. The consequence is nil (all of it is private to `ConsumerValidationRules`), which is why this is scored low, but the section is otherwise prescriptive enough that the omission reads as an oversight.

**Evidence**: ADR helper table (line 393) and the paragraph below it; no definition of `Arm` appears anywhere in the document — `grep -n "\bArm\b"` finds only the diagram's `arm = Direct` / `arm = Combined` and the table row.

**Recommendation**: One clause: "`Arm` is a private nested `enum { Direct, Combined }` on `ConsumerValidationRules`, and `ResolveCandidates` returns `(Arm, IReadOnlyList<Type>)`."

---

### 7. "Two lexical constraints" introduces three bullets, and the third is stranded five paragraphs downstream (Score: 42)

The AC-13a/AC-13c bullet is a sibling of the AC-15 and AC-7 bullets, but sits after the three-paragraph nested-composite discussion, orphaned between that discussion's closing sentence and the `{F-list}` paragraph. A reader following the list count will stop at two and miss a constraint the ADR calls "easy to breach accidentally".

**Evidence**: ADR line 475, "*Two lexical constraints shaped that wording and are easy to breach accidentally:*" followed by two bullets, then the nested-case paragraphs ending "*Recorded under Negative as a known message-quality limitation.*", then (line 510) "*- AC-13a and AC-13c forbid the substring `configure a channel factory of type` anywhere in a T3a/T3b message, so the body must not paraphrase the suppressed half.*"

**Recommendation**: Move the AC-13a/AC-13c bullet up to join the other two and change "Two" to "Three".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 7
**Findings at or above threshold (60)**: 3
