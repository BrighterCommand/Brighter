# Review: design — 0037-validate-subscription-channel-factory (ADR 0072, round 2)

**Date**: 2026-09-16
**Threshold**: 60
**Verdict**: NEEDS WORK

5 findings at or above threshold 60. Address these before approving.

> Round 1's findings file was never committed; it is preserved at
> `<scratchpad>/review-design.round1.md` for this session only.

## Round 1 disposition

1. **(92) `FactoryTypes` shape — FIXED.** Both forms compiled: the prescribed nullable-backing-field + expression-bodied property builds clean (0 warnings), the get-only auto-property initialised from `_factories` produces exactly `error CS0236: A field initializer cannot reference the non-static field, method, or property 'CombinedChannelFactory._factories'`, and the same auto-property initialised from the primary-constructor parameter `factories` compiles — so both halves of the ADR's new explanation are correct.
2. **(78) Null `ChannelFactoryType` — FIXED as a decision, but its implementation is defective.** The `Error`-in-both-arms verdict is now stated consistently in the diagram (`false when D is null`, `D is not null && …`), the helper table and the Risks entry; but it introduces an undeclared NFR-6 exception and undeclared FR-5 deviations (findings 2 and 3 below).
3. **(75) Phantom "one new public type" budget — FIXED.** `grep -niE "budget|quota|one new public type"` over the ADR returns nothing; the only two NFR-5 mentions now characterise it accurately ("confines any new member to `CombinedChannelFactory` itself"). The three replacement arguments are grounded — the `testing.md` quotations were verified verbatim.
4. **(70) Nested `CombinedChannelFactory` in `{F-list}` — NOT FIXED.** It is now addressed at length, but the new passage attributes the wrong template and asserts a routing outcome that is false (finding 1).
5. **(62) AC-9 companion assertion overstated — FIXED.** Both the Decision and Risks now say the pinning is negative-only and that AC-6/AC-10 have no runtime counterpart.
6. **(58) Empty `FactoryTypes` — PARTIALLY FIXED.** `(none)` is defined and shared between body and remedy, but it renders an unactionable remedy and is recorded nowhere in Consequences (finding 6).
7. **(35) Allocation contradiction — FIXED.** The Decision now reads "(The rule still allocates its candidate list; see Performance.)" and Performance agrees.
8. **(25) "localized" — FIXED.** `grep -niE "localiz|organiz|behavior|color"` over the ADR returns nothing.

## Findings

### 1. The nested-composite acceptance names the wrong template and claims a remedy that cannot work (Score: 80)

The revised passage is the ADR's answer to round-1 finding 4, and it is wrong on three counts.

**(a) Wrong template.** AC-10's Given is `CombinedChannelFactory([CombinedChannelFactory([new DeclaredChannelFactory()])])` **and a `DeclaringSubscription`** — so `D == typeof(DeclaredChannelFactory)`, which is *not* `typeof(InMemoryChannelFactory)`. FR-5's normative table selects **T2** for "Combined arm, and `D != typeof(InMemoryChannelFactory)`". The ADR says the message renders **T3b**.

**(b) The named type does not route.** If a developer follows the rendered remedy and declares `typeof(CombinedChannelFactory)`, the outer composite selects the inner composite at `CombinedChannelFactory.cs:34`, then calls `factory.CreateSyncChannel(subscription)` on it; the inner composite scans its own `[DeclaredChannelFactory]` for `f.GetType() == typeof(CombinedChannelFactory)`, finds none, and throws `ConfigurationException` at `CombinedChannelFactory.cs:35-38`. FR-3's word is "matches", not "routes"; the ADR silently upgrades one to the other.

**(c) It contradicts the ADR three lines earlier**, and its AC-7 gloss does not survive AC-7's text. AC-7 forbids naming `Paramore.Brighter.CombinedChannelFactory` "as the type the subscription will be handed" — it draws no outer/inner distinction, and the rendered string is indistinguishable either way. The ADR's "one the rule never makes" is false: in AC-10's configuration the body reads "will be handed one of 'Paramore.Brighter.CombinedChannelFactory'", which is exactly that claim.

**Evidence**: ADR: "the T3b remedy reads '…whose ChannelFactoryType is one of: `Paramore.Brighter.CombinedChannelFactory`'. That is **correct**, not a violation of the rule above: FR-3 states that 'a nested combined factory therefore matches only a subscription whose `D` is literally `typeof(CombinedChannelFactory)`', so the message names the one type that would in fact route." Against the ADR three lines earlier: "telling a developer to declare `typeof(CombinedChannelFactory)` would be advice that can never work." And `src/Paramore.Brighter/CombinedChannelFactory.cs:34-38` (non-recursive `FirstOrDefault` + throw), which makes the nested declaration fail one level down.

**Recommendation**: Correct the template to T2. Drop the "would in fact route" justification — it is false — and replace it with an honest one: the message enumerates the candidate the *outer* composite would select, and following it does not produce a working configuration, so the nested case is a known message-quality limitation of a configuration that is broken either way. Record it under Negative. If that is unacceptable, filter nested composites from `{F-list}` and let it render `(none)`, which at least does not advise an impossible fix. Either way, reconcile with the earlier statement explicitly rather than by re-scoping AC-7 to "the outer composite".

---

### 2. The direct-arm null-`D` verdict is new breakage under NFR-6, and the Risks entry mis-states it (Score: 76)

The ADR decides "We therefore treat a null declared type as a mismatch in both arms". For the **direct** arm this makes a configuration that starts and runs correctly today fail at startup. `grep -rn "ChannelFactoryType" src --include="*.cs"` excluding declarations returns **only** `CombinedChannelFactory.cs:34`, `:46`, `:59`. Nothing else in `src/` reads it. So in a non-combined configuration a `Subscription` subclass that overrides `ChannelFactoryType` to return `null` is never inspected at runtime, the host starts, and the consumer works. After this change it is an `Error` and, under the default `throwOnError: true`, blocks startup.

NFR-6 permits exactly four exceptions (C-2, C-10, C-11, C-12) and states: "Any newly discovered case MUST be added as its own constraint and to C-8's release-note obligations, not absorbed silently here." This case is none of the four — C-10 is about subclasses that *declare no override* and inherit `typeof(InMemoryChannelFactory)`, a different shape. The ADR adds no constraint, no release note, and nothing in Consequences → Negative.

Worse, the Risks entry asserts the opposite of what the code shows, and its qualifier quietly covers only the *other* arm.

**Evidence**: ADR Risks: "The affected population is narrower than C-10's: a subclass that overrides `ChannelFactoryType` and returns `null` already fails at Dispatcher start **under `CombinedChannelFactory`**, so the rule converts a certain runtime failure into a named startup finding **rather than creating new breakage**." The sentence's evidence is combined-arm-only; the conclusion is claimed for both. Against `src/Paramore.Brighter/CombinedChannelFactory.cs:34/46/59` as the sole readers.

**Recommendation**: Either (a) flag this as a requirements amendment — a new constraint C-13 with its own C-8 release-note obligation, listed in Consequences → Negative — and correct the Risks entry to say the direct arm *is* new breakage; or (b) reconsider the direct-arm verdict for null `D` (a defensible alternative the ADR did not weigh: treat the direct arm as compatible when `D` is null, since nothing at runtime consults it, while keeping the combined arm's FR-3-mandated `Error`). Do not leave the Risks text asserting "rather than creating new breakage".

---

### 3. Two undeclared deviations from FR-5, presented as costing "no new normative surface" (Score: 72)

FR-5 is normative in two respects the null-`D` decision changes, and the ADR declares neither as a deviation — it declares the *opposite*.

**(a) FR-5 item 2.** Verbatim: "The `Message` of every finding produced by this rule MUST therefore contain: … 2. the declared channel factory type `D`, as `Type.FullName`". A finding whose body reads "declares no ChannelFactoryType" contains no such name. This applies to the **combined** arm too — the arm the ADR says "is not ours to decide" — so it cannot be waved through as a consequence of a discretionary choice.

**(b) FR-5's template selection table.** Verbatim: "Item 4 MUST be rendered as one of exactly **four** literals, **selected by the rules below**", with T1 conditioned on "Direct arm, and `D != typeof(InMemoryChannelFactory)`" and T3a on "Direct arm, and `D == typeof(InMemoryChannelFactory)`". A null `D` satisfies T1's condition as written. The ADR widens T3a/T3b's condition instead. That is a change to a normative selection rule, not merely a change of which literal text ships.

**Evidence**: ADR: "**Rendering costs no new normative surface.** The body reads 'declares no ChannelFactoryType' in place of 'declares ChannelFactoryType '{D}''… The selection condition for T3a/T3b therefore widens from `D == typeof(InMemoryChannelFactory)` to `D == typeof(InMemoryChannelFactory) || D is null`. **FR-5's four literals are untouched and no fifth is introduced.**" The four literal *strings* are indeed untouched; the MUST in item 2 and the MUST governing their selection are not.

**Recommendation**: State plainly that this is a deviation from FR-5 item 2 and from FR-5's selection table for the null-`D` input, give the justification (item 2 is unsatisfiable when there is no type to name; T1 is unrenderable), and route it through the same requirements-amendment path as finding 2. Delete "costs no new normative surface".

---

### 4. AC-15's token rule is never resolved as substring-or-token, and the ADR applies it both ways (Score: 66)

The ADR promotes AC-15's assertion into a general body-wording constraint ("AC-15 forbids **any** occurrence of the token `ChannelFactory` that is neither preceded by `.` nor part of `ChannelFactoryType`") without settling what "token" means, and then applies it inconsistently:

- It **worries** about `Paramore.Brighter.CombinedChannelFactory` in `{F-list}` ("The nested case is the one exception") — an occurrence preceded by `d`, not `.`.
- It **never mentions** `Paramore.Brighter.InMemoryChannelFactory`, which the direct-arm body renders in this feature's headline case (AC-1: plain `Subscription<T>`, so `D == typeof(InMemoryChannelFactory)`, body "declares ChannelFactoryType '{D}'"). That is the identical shape — an occurrence preceded by `y`, not `.`.

Under a word-boundary reading (`\bChannelFactory`) neither is an occurrence and the nested-case discussion is a non-problem. Under a substring reading both are breaches. Two implementers writing the AC-15 assertion will choose differently — `Regex.Matches(msg, @"\bChannelFactory")` vs `msg.IndexOf("ChannelFactory")` — and the substring implementer will get a red test the moment they generalise the assertion the way the ADR invites.

**Evidence**: AC-15 verbatim: "no occurrence of the token `ChannelFactory` appears that is neither immediately preceded by a `.` nor part of the token `ChannelFactoryType`". ADR's general-constraint sentence vs its nested-case paragraph; and the ADR's own body template, which renders `{D}` unconditionally and therefore renders `Paramore.Brighter.InMemoryChannelFactory` for every AC-1/AC-13a-shaped finding.

**Recommendation**: Decide it explicitly in the ADR — "token" means a match at a word boundary, so composite identifiers such as `InMemoryChannelFactory` and `CombinedChannelFactory` are not occurrences — and state the assertion's implementation (the regex) so both test sites agree. Then delete the nested-case "it is not a breach" discussion, which the resolution makes moot.

---

### 5. The lazy `FactoryTypes` contradicts "materialised once", "stable", and "not observable" (Score: 62)

The prescribed shape is right and compiles; the claims made *about* it are not, and they contradict the argument used to reject the alternative.

- The ADR rejects a plain `=> _factories.Select(...).ToList()` because it "weakens the contract's 'stable' to 'equal-but-not-same'" — so "stable" is being used in the **reference-identity** sense.
- It then concedes a race in which "two threads may each build a list, and one wins" and calls the outcome "not observable to a caller". Reference identity *is* observable, and the race produces exactly "equal-but-not-same": thread A can compute L1, be overwritten by thread B's L2, and see a different instance on its next read. That is the defect the alternative was rejected for.
- Performance still asserts "`FactoryTypes` is materialised once per `CombinedChannelFactory`", and the Key Components note claims the shape "preserves the 'materialised once' property" — both false under the race the ADR itself describes.
- Separately, the property is a non-volatile reference field publishing a `List<T>`. "Benign race" is a term of art that presumes safe publication; the ADR asserts it without argument, and the prescribed XML documentation says nothing about thread-safety, so the next editor has no guidance. Nothing in Brighter calls `FactoryTypes` concurrently today (the only consumer is the startup rule, and `ValidateConsumers`/`EvaluateSpecs` is a sequential loop), which is the honest reason the race does not matter — and it is the reason the ADR does not give.

**Evidence**: the ADR's "Two alternatives were weighed and rejected" paragraph and its closing race sentence; the contract's "a non-null, possibly empty, **stable**, ordered list"; Performance's "materialised once per `CombinedChannelFactory`".

**Recommendation**: Either state the honest position — "the only caller is the startup validation rule, which is single-threaded; the property is not documented as thread-safe and callers needing concurrent access must synchronise" — and soften "materialised once" to "materialised at most once per read-path, once in practice"; or make it genuinely once (`Lazy<IReadOnlyList<Type>>`, or `Interlocked.CompareExchange`) and keep the claims. Do not keep both the identity-based rejection of the alternative and the "not observable" dismissal of the race.

---

### 6. `(none)` renders a remedy with no remedy in it, and is absent from Consequences (Score: 58)

For `new CombinedChannelFactory([])` the ADR renders T3b (or T2) with `{F-list}` = `(none)`, producing a message ending "— use a subscription type whose ChannelFactoryType is one of: **(none)**". There is no subscription type that satisfies that, so the message states no remedy. NFR-2 verbatim: "Each message MUST … state a remedy. No message may be satisfiable by a generic phrase such as 'channel factory mismatch'." The ADR's justification ("FR-5 item 3 still requires the message to say what the subscription will be handed") is weak: FR-5 item 3 requires the `Type.FullName` of every inner factory, which an empty set satisfies vacuously — `(none)` is the ADR's own addition, not FR-5's requirement.

It is also unrecorded outside the Decision: neither Consequences → Negative nor Risks mentions the empty-composite rendering, and the ADR does not state that `(none)` is unreachable in the direct arm (it is — `candidates` is `[F.GetType()]` or `[typeof(InMemoryChannelFactory)]`, never empty).

**Evidence**: ADR: "`{F-list}` … renders as the literal `(none)` when the candidate list is empty — a `CombinedChannelFactory` constructed with no inner factories is legal today and routes nothing, so the verdict is an `Error` and FR-5 item 3 still requires the message to say what the subscription will be handed."

**Recommendation**: For the empty-candidate case emit a remedy that is actionable ("add a channel factory of type `{D}` to the combined channel factory"), or accept `(none)` and record explicitly under Negative that this one configuration yields a message with a degenerate remedy clause. State that `(none)` is combined-arm-only.

---

### 7. The new test double extends C-9's closed set without flagging a requirements amendment (Score: 55)

The revised Testing Strategy adds three test cases and a sixth double. C-9 verbatim: "**The doubles (a closed set — no AC may use one not listed here).**" The new tests are not ACs, so the letter is not broken — but the acceptance-criteria preamble also binds every non-gateway criterion to the closed set, and the three new cases exist only because of findings 2 and 3's undeclared deviations. The ADR flags them as uncovered by any AC (good) but does not say what should follow: an amendment to C-9 and new acceptance criteria in the approved requirements.

Case 3 is also under-specified: "A `CombinedChannelFactory` with no inner factories — one `Error` whose `{F-list}` renders `(none)`" does not say which subscription double, which decides whether T2 or T3b is rendered.

**Evidence**: the revised Testing Strategy's three-case list; requirements.md C-9 and the Acceptance Criteria preamble.

**Recommendation**: Say that these cases require a C-9 amendment (a `NullDeclaringSubscription` double) plus new ACs, and route them back through requirements rather than shipping them as ADR-only tests. Name the subscription double in case 3.

---

### 8. Implementation Approach step 1 re-plants the wrong CS0236 reading, and omits a required `using` (Score: 45)

Step 1 compresses the (now correct) Key Components explanation into a sentence that is false as written: a get-only auto-property compiles perfectly well in a primary-constructor class — `public IReadOnlyList<Type> FactoryTypes { get; } = factories.Select(f => f.GetType()).ToList();` builds clean. What cannot compile is an initialiser referencing the *instance field* `_factories`. Since Implementation Approach is the section an implementer works from, this is the same wrong-direction nudge round-1 finding 1 objected to.

Separately, `src/Paramore.Brighter/CombinedChannelFactory.cs` has only four usings (`System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`), the repo sets no `ImplicitUsings`, and the file uses no `System` type today. The prescribed snippet's `IReadOnlyList<Type>?` therefore requires adding `using System;`, which the "pure structural addition" framing does not mention.

**Evidence**: ADR Implementation Approach step 1: "A nullable backing field plus an expression-bodied property materialising once from `_factories` — **not** a get-only auto-property, which cannot compile in a primary-constructor class (CS0236). Pure structural addition". `src/Paramore.Brighter/CombinedChannelFactory.cs:1-4`; no `ImplicitUsings` in `Directory.Build.props` or `src/Paramore.Brighter/Paramore.Brighter.csproj`.

**Recommendation**: "…not a get-only auto-property initialised from `_factories`, which cannot compile (CS0236)". Note the `using System;` addition.

---

### 9. `IsCompatible`'s stated signature cannot decide the two arms it is given (Score: 45)

The helper table gives `IsCompatible(Type? declared, candidates)` the responsibility of "*deciding* — FR-3's two arms, and the null-`declared` case", but its parameters carry no arm discriminator — while the paragraph immediately below insists the discriminator must be explicit and must travel with the list.

**Evidence**: the helper table vs "`ResolveCandidates` returns the arm and the candidate list together… Keeping the arm as an explicit discriminator (rather than re-testing `F is CombinedChannelFactory` at each use site) is what lets the rest of the rule work in types only."

**Recommendation**: Write the signature as `IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)`, or say that `candidates` is the `(arm, types)` pair `ResolveCandidates` returns.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 9
**Findings at or above threshold (60)**: 5
