# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 4)

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

8 findings at or above threshold 60. Address these before approving.

> **Verification note.** All nine findings re-checked against the working tree before filing; **all
> nine hold, none rejected, and no grounding error was found in the reviewer's own evidence this
> round.** Findings 1, 2, 3 and 7 are defects **round 3's fixes introduced** — including, for the
> **fourth consecutive round**, a stale copy of a superseded argument (finding 3), and, for the
> **second consecutive round**, an assertion added to discharge AC-27's "at most once" that does not
> discharge it (findings 1 and 2). The pattern is recorded in `PROMPT.md`: an assertion is not a
> guard until someone has said what would make it fail.

## Findings

### 1. The replacement ancestry assertion contradicts `Sweep`'s own subsumption contract — it forbids a base/derived pair the design explicitly permits, and would fail a correct sweep (Score: 80)

Round 3 replaced distinctness with ancestry. The new assertion is stated **unconditionally**: no
`Subject` may have another `Subject` from the same result in its base chain. But `Sweep`'s subsumption
is **conditional** — it drops a derived candidate *only* when that candidate declares no
`ChannelFactoryType` of its own, and the Risks section says so in terms. So a sound `Sweep` result may
legitimately contain both a base and a derived type, and in exactly that case the generated test fails
on correct behaviour.

Work the case through. Take `Foo<T> : Subscription` declaring
`override Type ChannelFactoryType => typeof(FooChannelFactory)`, and `FooBar : Foo<Bar>` also
declaring its own override. Step 2 drops neither (both declare). Step 3 closes `Foo<>` to
`Foo<Command>`. Result = `{ Foo<Command>, FooBar }`, both `Reason` null — a correct, sound result. Now
the template's third assertion: `FooBar`'s base chain yields `Foo<Bar>`, reduced by
`GetGenericTypeDefinition()` to `Foo<>`; the other `Subject` `Foo<Command>` reduces to `Foo<>`; they
match → **assertion fails**. The guard reports a subsumption defect where there is none, and a
maintainer's only remedies are to delete the derived override or to edit a generated file.

This is not a hypothetical corner the document ignores — it is the shape the document itself
introduces. Step 2 of the Implementation Approach adds "a synthetic generic subscription in
`Core.Tests` that declares its own override", and the Risks bullet promises that such a type "is never
dropped". The moment that shape appears in a *gateway* assembly, the generated guard rejects it.

The same split shows up against the requirements. FR-12's Scope and AC-27 state the obligation
**unconditionally** — "A base/derived pair in the same assembly (e.g. `RocketSubscription` and
`RocketMqSubscription<T>`) MUST be reported at most once" — which is what the template asserts and
what `Sweep` does *not* implement. So the document holds two incompatible contracts: the template's
(and the requirement's) unconditional one, and `Sweep`'s narrowed one.

**Evidence**: ADR, Subsumption bullet: "A candidate is dropped when it does not itself declare
`ChannelFactoryType` (looked up with `BindingFlags.DeclaredOnly`) and an ancestor in its base chain is
also a candidate in the same assembly." ADR, Risks: "It drops a derived candidate only when that type
declares no `ChannelFactoryType` of its own… **A derived type that *does* declare an override is never
dropped.**" ADR, template bullet: "The third discharges AC-27's "a base/derived pair reported at most
once": **no `Subject` in the result has another `Subject` from the same result in its base chain**".
`requirements.md` FR-12 Scope: "A base/derived pair in the same assembly … MUST be reported at most
once."

**Recommendation**: Make the assertion match `Sweep`'s contract, not a stricter reading of it: *no
`Subject` that declares no `ChannelFactoryType` of its own has another `Subject` from the same result
in its base chain* — or, if the unconditional reading is the intended one, change subsumption to drop
every derived candidate with a candidate ancestor and reconcile the Risks bullet. Either way, resolve
it in one place and state which reading of FR-12's MUST the design takes. **Note for the maintainer:
the requirement's wording is the imprecise half.** Conditional subsumption is the correct behaviour —
a derived type declaring its own override may disagree with its base and must be reported — so this
points at a fourth amendment to `requirements.md` under working convention 1, not at a design change.

---

### 2. The ancestry assertion is never exercised over the twelve real assemblies — each returns exactly one subject — and it misses the subsumption failure that keeps the derived and drops the base (Score: 75)

Ancestry *can* fail, and it does fail on the cited regression, so it is not vacuous in principle as
distinctness was. But over the twelve shipped assemblies it is evaluated only ever against a
**one-element list**, where it has no possible failure — and the ADR, which is otherwise candid about
untested paths, never says so.

Every shipped gateway assembly contains exactly two `Subscription` subclasses: a non-generic base
declaring the override and a generic derived declaring none. Subsumption drops the derived. Result
length = 1, for all twelve, today and after 0072 adds its three base-class overrides. Consequently:

- **non-empty** passes on one entry;
- **every `Reason` null** is the only assertion doing work;
- **ancestry** is trivially true (a type is never in its own base chain) and is never exercised by any
  real input.

Worse, it is one-sided. Consider subsumption regressing in the *other* direction — the `DeclaredOnly`
predicate inverted, or the ancestor tested rather than the candidate, so the **base** is dropped and
the derived kept. Result = `{ RocketMqSubscription<Command> }`: one entry, `Reason` null (the derived
inherits the base's override value), no ancestry pair. All three assertions pass. The sweep has
silently stopped reporting `RocketSubscription` — the type the configuration names and the only one
the base-class override lives on — and the guard is green. That is precisely the "vacuous pass this
design exists to prevent", reached through the step the ancestry assertion was added to protect.

The document even provides the missing assertion for free. The configured
`GatewayConformance.SubscriptionType` is, in all twelve rows of the ADR's own table, exactly the
non-generic base that survives subsumption (`SqsSubscription`, `RocketSubscription`, `RmqSubscription`,
…), and the template already renders `typeof(X)` to get `.Assembly`. Asserting `result` contains
`typeof(X)` as a `Subject` holds today for all twelve, catches the inverted-subsumption case, and
catches a `SubscriptionType` pointed at the wrong assembly — which non-emptiness does not.

**Evidence**: `grep -rn "override Type ChannelFactoryType" --include="*.cs"
src/Paramore.Brighter.MessagingGateway.*` → **9** hits, every one on a non-generic base class; twelve
gateway `*Subscription.cs` files each declaring exactly **2** `Subscription`-derived classes. Spot
check: `RocketMqSubscription.cs:10` `class RocketSubscription : Subscription`, `:50` the override,
`:117` `class RocketMqSubscription<T> : RocketSubscription` with no override. So subsumption reduces
every assembly to a single subject. Contrast the candour applied to the analogous case: "**The
generic-closing path has no shipped type to exercise it.** After subsumption, all twelve assemblies
resolve to a non-generic base."

**Recommendation**: Add the assertion that the configured subject appears in the result — which is
what actually discharges "the sweep examined the right thing" — and demote non-emptiness to a
consequence of it. Then state plainly, as the generic-closing risk already does, that the ancestry
assertion is a regression trip-wire with no shipped input that exercises it, and cover the
inverted-subsumption direction with a `Core.Tests` case over a synthetic base/derived pair.

---

### 3. Fourth stale copy of the superseded (D7) argument survives in the grounded-reference list: "the unnamed rule this ADR's Context turns on" (Score: 68)

The predicted drift is there, and round 3 introduced it. Commit `6d29b664b` added the
`CombinedChannelFactory` grounded-reference line describing the comparison as "the unnamed rule this
ADR's Context turns on" — pure D7 framing, in which the justification was that the module already
depends on a rule it has *never named*. Commit `26280cabb` then removed that argument from Context
entirely: "never named", "unnamed", "design signal", "term of the contract" and "evidence" no longer
appear anywhere in Context. Under D8, Context turns on **FR-12's express permission** plus **where the
rule lives** — not on the rule being unnamed. So the References section describes Context as it was two
commits ago, and a reader who follows the pointer finds no such argument.

**Evidence**: `grep -n "unnamed"` over the ADR returns exactly one hit, line 592 — "…in each creation
method, the unnamed rule this ADR's Context turns on, with the `ConfigurationException` on no match."
The same grep for "design signal", "term of the contract", "never named" and "two tests" returns
nothing anywhere in the file.

**Recommendation**: Rewrite that reference line to the D8 framing — e.g. "the same comparison the guard
makes, implemented inline at three call sites; the runtime dependency that makes *where the rule lives*
the question this ADR decides". Then sweep the whole file for *descriptions of* Context, not just
Context itself.

---

### 4. `Sweep`'s contract is stated two ways: "one entry per candidate it examined" versus a Subsumption step that drops examined candidates (Score: 66)

The `Sweep` signature paragraph and the anti-vacuous-pass paragraph both define the result as covering
everything examined. The Subsumption step then removes candidates that *were* examined — it had to read
their `DeclaredOnly` members and walk their base chains to decide. Both statements are therefore false
as written, and the second is load-bearing: the whole anti-vacuous-pass argument is "a contract that
cannot express 'I examined these and they were sound' cannot rule it out", and the contract as
implemented cannot express that for any subsumed type. The one class of type the guard is *least* able
to account for is exactly the class the ancestry assertion is supposed to protect.

It also makes "It is empty only when the assembly contains no candidates at all" an unproved claim
rather than a contract term: it holds only because a root candidate can never have a candidate ancestor
in the same assembly, which the ADR never states.

**Evidence**: ADR: "It returns **one entry per candidate it examined** … It is empty only when the
assembly contains no candidates at all." And: "**It reports what it examined, not only what failed, and
that is deliberate.**" Against: "**Subsumption.** A candidate is dropped when…".

**Recommendation**: Define the term once — *candidates* (step 1's set) versus *subjects* (what survives
step 2) — and restate the contract as "one entry per subject", with an explicit sentence on why
subsumed candidates need no entry (their value is their base's by construction) and why the result is
never empty when candidates exist.

---

### 5. "The same reduction the Subsumption bullet specifies" is not sufficient in the template, where `Subject` values are closed constructions (Score: 65)

Subsumption runs **before** closing, so its comparison is *reduced ancestor* against *open definition*;
only one side needs reducing, and the bullet says exactly that. The template operates on the sweep's
**output**, where any generic `Subject` has already been closed with `MakeGenericType`. There, both
sides can be constructed generics: an ancestor `Foo<Bar>` must be compared against a `Subject` of
`Foo<Command>`. Reducing only the ancestor (the "same reduction") yields `Foo<>` against `Foo<Command>`
— no match, and the assertion silently loses the case it was written for. The template must reduce
**both** sides. Two developers would implement this differently.

**Evidence**: ADR, Subsumption: "Subsumption runs before closing, so the candidate set holds open
definitions (`Foo<>`) while a base chain yields closed constructions (`Foo<Bar>`); an ancestor that is
a constructed generic is therefore reduced with `GetGenericTypeDefinition()` before the comparison."
ADR, template: "constructed generics reduced with `GetGenericTypeDefinition()` — the same reduction the
Subsumption bullet specifies."

**Recommendation**: Spell the template's comparison out as its own rule: normalise *both* the `Subject`
and each base-chain entry with `IsConstructedGenericType ? GetGenericTypeDefinition() : t`, then
compare — and delete "the same reduction the Subsumption bullet specifies", because it is not the same
reduction.

---

### 6. `design_principles.md`'s "do not add new types without necessity" is cited as a governing rule while Context now concedes the type is not necessary — never reconciled (Score: 62)

D8's honesty has a cost the document does not pay. Context now states that a path through existing
exports exists, that the widening is "**not forced by impossibility**", and that it was "chosen over an
alternative that would also have worked" — which is, in the cited principle's own terms, a new type
added without necessity. The References section lists that principle among the project rules this ADR
is argued against, with no gloss and no argument that requirements permission discharges a design
principle. Under D6 and D7 the tension did not arise (the export was claimed to be a missing contract
term); under D8 it arises squarely and is unaddressed. The `testing.md` obligation is handled
explicitly at four sites; this one nowhere.

**Evidence**: `.agent_instructions/design_principles.md:32` — "- Do not add new types without
necessity." ADR Context: "So this widening is **not forced by impossibility**. It is authorised by the
requirements, on reachability grounds, and chosen over an alternative that would also have worked." ADR
References: "…Responsibility-Driven Design and "do not add new types without necessity"." (no
qualification).

**Recommendation**: Add one sentence in Context or Negative stating that the necessity test is not met
on its own terms, that FR-12's express permission is what overrides it, and that the ADR treats the
requirement as the higher authority — then qualify the References line the way the `testing.md` line
now is.

---

### 7. The `Core.Tests` subject enumeration is incomplete: C-9's double set gives 0072 **four** `Subscription` subclasses to add, not one, and their getter shape is unpinned (Score: 62)

Round 3's fix is right about `MockSubscription` and right that whole-result assertions would fail — but
its enumeration of what else lands in that assembly names only `NullDeclaringSubscription`. C-9's
closed double set contains four `Subscription` subclasses: `DeclaringSubscription`,
`NonMatchingSubscription`, `NullDeclaringSubscription` and `AlphaBus.AlphaSubscription`. None exists
today, so all four arrive with 0072, in `tests/Paramore.Brighter.Core.Tests/Validation/`. Three are
sound *if* their overrides are constant `typeof(...)` expressions — but C-9 only pins them as
"identity-only… it overrides `ChannelFactoryType` and nothing else", which is exactly what
`MockSubscription` is, and `MockSubscription` is an auto-property that reads `null` on an uninitialised
instance. The ADR's account of which `Core.Tests` subjects are unsound therefore rests on an unstated
assumption about how 0072 writes three doubles this ADR does not mention. The Risks bullet inherits the
gap: after 0072 plus this ADR's own types, `Core.Tests` holds nine `Subscription` subclasses, of which
the bullet accounts for one.

**Evidence**: `requirements.md:231-234` — `DeclaringSubscription`, `NonMatchingSubscription`,
`NullDeclaringSubscription`, and "plus `AlphaBus.AlphaSubscription` declaring the former";
`requirements.md:320` places the closed set in `tests/Paramore.Brighter.Core.Tests/Validation/`. A grep
for `Subscription` subclasses in `Core.Tests` today returns exactly one match, `MockSubscription` —
confirming the ADR's "the only `Subscription` subclass in `Core.Tests` today".

**Recommendation**: Name all four of 0072's subscription doubles, state which are sound under `Check`
and which are not, and add the one-line constraint the design actually depends on: C-9's subscription
doubles must declare `ChannelFactoryType` as an expression-bodied `typeof(...)`, not an auto-property,
or the sweep reports them under the **null** branch.

---

### 8. The subsumption rule is now expressed twice in the same repository, and only the *other* duplication is recorded as a cost (Score: 60)

Round 3's fix correctly withdrew "the template is three lines of arrangement", but stopped at conceding
that the template "carries logic" and did not follow the consequence: the base-chain walk plus
`GetGenericTypeDefinition()` reduction is `Sweep`'s subsumption rule, re-expressed in the generated
test. The ADR records one duplicated judgement as an accepted cost, in detail, with its drift
consequence spelled out (`Check`'s inherited-default branch versus 0072's T3a/T3b). It records nothing
for this one, while the Positive section still claims "One definition of "sound declaration", in one
place, used by twelve tests" and the Risks mitigation still says the template holds "nothing in it that
decides whether a declaration is sound". The second is literally true — ancestry is not soundness — but
it reads as reassurance about a template that now encodes half of step 2's algorithm, and a change to
the subsumption rule must now be made in two places.

**Evidence**: ADR Negative: "**The inherited-default judgement is expressed twice in the same package,
and that is accepted.** … The cost is that a change to what "inherited default" means must be made in
both." No comparable bullet exists for subsumption. ADR Risks: "the template arranges the call and
makes three assertions, one of them the ancestry check … the only logic the template carries."

**Recommendation**: Add a Negative bullet stating that the subsumption rule is expressed in `Sweep` and
again in the template, that the second exists because there is no other way to assert the first over a
real assembly, and that a change to subsumption is a change to both. Qualify the Positive bullet to
"one definition of *sound declaration*". **If the assertion moves out of the template (see findings 1
and 2), this finding dissolves** — which is a point in favour of doing so.

---

### 9. FR-12's express-permission quote is truncated at the clause that changes its force (Score: 55)

The quotation is accurate as far as it goes but stops mid-sentence with a closing quote and no ellipsis,
dropping FR-12's own gloss: NFR-5 "constrains changes to *existing* abstractions". That clause says
NFR-5 is not in tension with a new type at all. The ADR's "expressly permitted notwithstanding NFR-5",
read alone, implies a rule overridden by exception — which is the reading that makes "authorised testing
concession" sound like a debt. The requirement's actual position is weaker in obligation and stronger in
permission than the ADR's paraphrase.

**Evidence**: `requirements.md` FR-12: "Introducing **one new public type** to host the predicate is
therefore expressly permitted notwithstanding NFR-5, which constrains changes to *existing*
abstractions." ADR: "…is therefore expressly permitted notwithstanding NFR-5" (quote closes here).
NFR-5 confirms the gloss: "No public API change to existing abstractions… `IAmAChannelFactory`,
`Subscription` and `IAmConsumerOptions` keep their current members."

**Recommendation**: Quote the full sentence, or close with an ellipsis and add the gloss in prose — the
distinction between "NFR-5 overridden" and "NFR-5 not engaged" is the difference between a concession
and a non-issue.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 0 |

**Total findings**: 9
**Findings at or above threshold (60)**: 8

## Round-3 fixes verified

1. **Finding 1 (85) — distinctness assertion vacuous.** *Partial, and introduced new problems.* The
   replacement is a genuine improvement: unlike distinctness, ancestry does fail on the cited
   `RocketSubscription` / `RocketMqSubscription<Command>` regression, and the paragraph explaining why
   distinctness was wrong is correct and well argued. But the replacement is unconditional where
   `Sweep`'s subsumption is conditional, so it rejects a correct result (finding 1); it is evaluated
   only against one-element lists over all twelve shipped assemblies and misses the
   inverted-subsumption direction entirely (finding 2); and its generic reduction is under-specified
   for the closed `Subject` values it actually sees (finding 5).
2. **Finding 2 (75) — fourth case assembly-scoped; `Core.Tests` holds an unsound subscription.**
   *Complete, with one gap.* The new paragraph states the whole-assembly sweep and
   `result.Single(e => e.Subject == typeof(X))` scoping, gives the correct reason, and declines the
   type-scoped overload. Gap: the enumeration of what 0072 adds names one of four doubles and leaves
   their getter shape unpinned (finding 7).
3. **Finding 3 (75) — two-readings argument not entitled to the design-signal bullet.** *Complete in
   Context, incomplete downstream.* Context is fully re-derived onto FR-12's express permission, and
   all three `testing.md` quotations verified word-for-word. But the References grounded-reference line
   still describes Context in D7's terms (finding 3), the `design_principles.md` necessity citation is
   now in open tension and unaddressed (finding 6), and the FR-12 quote is truncated (finding 9).
4. **Finding 4 (70) — stale Architecture Overview edges.** *Complete.* The `Core.Tests` edge reads
   `Check + Sweep` and the box carries both missing cases. Matches the prose.
5. **Finding 5 (68) — shared-test-support alternative kept C-10 as a criterion.** *Complete.*
   Consistent with Context, the Positive bullet and the generator-emitted-predicate entry; C-10 is a
   benefit at all of them.
6. **Finding 6 (68) — `CombinedChannelFactory.cs:33` wrong line.** *Complete.* `:34`, `:46`, `:59`
   verified as the three creation-method call sites, cited correctly at both sites.
7. **Finding 7 (65) — honest check answerable in-repo; non-test consumer unaddressed.** *Complete.*
   Two new Negative bullets answer the check "no" in-repo, name `CombinedChannelFactory` and 0072's
   rule as considered-and-not-taken with reasons, and record the two-site duplication as accepted.
   `0072:457` verified.

## Grounded references sampled and correct

`Subscription.cs:35/172/213/258`; `Command.cs:42`; `CombinedChannelFactory.cs:34/:46/:59`; all nine
`override Type ChannelFactoryType` line numbers and their non-generic hosts;
`RocketMqSubscription.cs:10/:50/:117-118`; `MockSubscription` at `:85-87`; `TestConfiguration.cs:38`;
`ci.yml:228`, `:361`, `:708`; fourteen existing `test-configuration.json` files, every one carrying
`Namespace`, with AzureServiceBus, MQTT and RMQ.Sync absent as claimed; the reference topology
(`Base.Test` three, `Test.Helpers` one, `ServiceActivator` six); `SharedGenerator`'s four root-level
templates; `GeneratedTreeAudit` building its expected set from `OutboxGenerator.Plan` and
`MessagingGatewayGenerator.Plan`. **No grounding errors found this round.**
