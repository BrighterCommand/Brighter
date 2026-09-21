# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 3)

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

7 findings at or above threshold 60. Address these before approving.

> **Verification note.** Every finding was re-checked against the working tree by the main agent
> before filing. **All ten hold; none rejected.** Three of them are defects introduced by round 2's
> own fixes (findings 1, 2 and 6), and one of those — finding 1 — means round-2 finding 4 was **not
> fixed at all**. Round 2's fix count therefore goes 4 at threshold → 7, on a document that improved
> in other respects. That is the cost of patching an argument instead of re-deriving it, which is
> this spec's oldest recorded lesson.

## Findings

### 1. The template's new third assertion is vacuous — `Subject` distinctness cannot fail, and cannot catch the subsumption regression the ADR says it catches (Score: 85)

Round 2's fix for AC-27's "reported at most once" clause added a third assertion to the generated
template: that `Subject` values are distinct. The ADR then justifies it with a concrete example. That
example is wrong, and it is wrong in a way that shows the assertion guards nothing.

`Sweep` is specified to return "**one entry per candidate it examined**", with candidates drawn from
`Assembly.GetTypes()` (which yields no duplicates) and generics closed with `MakeGenericType`. Every
entry's `Subject` is therefore a distinct `Type` **by construction** — two distinct open definitions
cannot close to the same type. In particular, the ADR's own stated failure case — a subsumption
regression reporting `RocketSubscription` *and* `RocketMqSubscription<Command>` — produces two
*distinct* `Type` objects, so a distinctness assertion passes over it exactly as the first two
assertions do. The assertion cannot fail under any behaviour of `Sweep` consistent with its stated
contract, so AC-27's at-most-once clause is still guarded nowhere over the twelve real assemblies —
which is precisely what round-2 finding 4 asked to be fixed.

What would actually guard the clause is an assertion about the *relationship* between subjects (no
reported `Subject` has another reported `Subject` in its base chain, matching on the generic type
definition — the same reduction the Subsumption bullet specifies), or a per-gateway expected subject
set. Both are real work and neither is in the design.

**Evidence**: ADR lines 211-214 — "It returns **one entry per candidate it examined**, carrying the
subject type and its `Reason`". ADR lines 276-282 — "that the `Subject` values are distinct, which is
AC-27's 'a base/derived pair reported at most once' … asserting only the first two would let a
subsumption regression report `RocketSubscription` and `RocketMqSubscription<Command>` as two sound
entries and still pass." `typeof(RocketSubscription) != typeof(RocketMqSubscription<Command>)`, so
those two entries *are* distinct.

**Recommendation**: Replace the distinctness assertion with an ancestry assertion — no `Subject` in
the result has another `Subject` from the same result in its base chain, reducing constructed
generics with `GetGenericTypeDefinition()` — and correct the justifying sentence. If distinctness is
kept at all, describe it as what it is: a restatement of `Sweep`'s contract, not a guard on
subsumption.

---

### 2. The new fourth test case is specified as "a sweep over the two AC-28 doubles", but `Sweep` is assembly-scoped and that assembly already contains a subscription whose `ChannelFactoryType` reads instance state (Score: 75)

`Sweep`'s only parameter is an `Assembly`. The design offers no way to sweep a *subset* of types.
Both the new fourth case ("a sweep over the two AC-28 doubles") and AC-29's case
("`Sweep(itsOwnAssembly)`") therefore sweep the whole `Paramore.Brighter.Core.Tests` assembly, and
the ADR never says how the resulting set is narrowed to the doubles under test. Two developers will
implement this differently: one filters the result by `Subject`, one asserts over the whole result
(and it then breaks the first time any `Subscription` subclass is added to `Core.Tests`).

This is not hypothetical. `Core.Tests` already contains `MockSubscription`, whose override is an
**auto-property assigned in the constructor** — so on an uninitialised instance it returns `null`,
and the sweep will report it with the *null* branch's reason ("declares no channel factory type").
ADR 0072 will additionally add `NullDeclaringSubscription` (C-9's closed double set), which overrides
`ChannelFactoryType` to return `null` and will likewise be reported. So a
`Sweep(Core.Tests.Assembly)` returns unsound entries for types that have nothing to do with AC-28 or
AC-29, and the design's own Risks bullet ("Today none does — all nine overrides are constant
`typeof(...)` expressions") is true only of the *shipped gateways*, not of the assembly the new test
sweeps.

**Evidence**: ADR line 211 —
`public static IReadOnlyList<(Type Subject, string? Reason)> Sweep(Assembly gatewayAssembly)`. ADR
lines 371-374 — "**A fourth case closes that seam**: a sweep over the two AC-28 doubles, asserting
their entries carry the expected non-null `Reason`." ADR line 362 — "It appears among
`Sweep(itsOwnAssembly)`'s subjects". Working tree:
`tests/Paramore.Brighter.Core.Tests/MessagingGateway/When_constructing_a_channel_with_combined_factory.cs:85-87`:

```csharp
public class MockSubscription : Subscription
{
    public override Type ChannelFactoryType { get; }
```

assigned from a constructor argument, so `null` on an uninitialised instance. It is the only
`Subscription` subclass in `Core.Tests` today. Requirements C-9 lists
`NullDeclaringSubscription : Subscription` — "Overrides `ChannelFactoryType` to return `null`".

**Recommendation**: State explicitly that the `Core.Tests` cases sweep the whole assembly and assert
only over the entries whose `Subject` is the double under test
(`result.Single(e => e.Subject == typeof(X))`), and add a sentence noting that other `Subscription`
subclasses in `Core.Tests` — `MockSubscription` today, `NullDeclaringSubscription` from 0072 — will
appear with non-null reasons, which is why the assertions must be subject-scoped. If a type-scoped
overload is intended instead, say so and account for it against FR-12's one-new-public-type
allowance.

---

### 3. The two-readings argument is not entitled to the design-signal bullet, and "§ *Narrow and deep*'s two tests" misdescribes the rule's structure (Score: 75)

The decision to add the type is settled (D7) and this finding does not re-litigate it. The
*expression* of the argument does not hold up against the rule it cites.

(a) **The antecedent is unsatisfied.** The design-signal bullet the ADR leans on is explicitly
conditional: "**Having to widen is a design signal, not just a cost.** *If no existing export reaches
the behaviour*, the module's contract may be missing a name for something it already depends on
internally." The ADR concedes, in the immediately preceding paragraph, that under the other reading
"a path plainly exists" and "the alternative wins". A conditional whose antecedent the document
itself declares satisfiable cannot be invoked by choosing a different description of the behaviour;
the rule's antecedent is about *the behaviour under test*, and the behaviour under test in this
design — what the twelve sweeps and AC-27/28/29 actually assert — is reading 1's "are the gateway
assemblies' declarations sound?". Nothing in the design tests "which declarations can this module
route on" except by calling the proposed export. That is the circularity round 2 set out to remove,
relocated from C-10 into the choice of reading.

(b) **There are not "two tests".** `testing.md` § *Narrow and deep* has one test (the path test),
then guidance on what a *necessary* widening signals, then an after-the-fact check. The design-signal
material is not a second, independent criterion for choosing between alternatives — it is what to
conclude *once the path test has already failed*. So rejecting the generator-emitted predicate "on
the second of § *Narrow and deep*'s two tests" applies to an option that widens nothing a rule which
only speaks about widenings — and which, for an option with a path, prescribes the opposite ("test
through that path").

**Evidence**: `.agent_instructions/testing.md:109-129`, verbatim structure: bullet 1 the goal; bullet
2 "Read that way, 'do not export to test' is **conditional, not absolute** … Before widening, ask:
**is there a path through the module's existing exports to the behaviour under test?** — If there is,
widening is unjustified — test through that path. — If there is not, exporting may be the only way…";
bullet 3 "**Having to widen is a design signal, not just a cost.** If no existing export reaches the
behaviour…"; bullet 4 "The honest check, after the fact…". ADR lines 83-90 ("a path plainly exists …
the alternative wins; this ADR does not pretend otherwise") and line 525 ("rejected on the second of
§ *Narrow and deep*'s two tests"); the phrase also survives in the References line at :557.

**Recommendation**: Stop deriving the justification from § *Narrow and deep*'s conditional. State
plainly what is true and sufficient: FR-12 *expressly permits* one new public type notwithstanding
NFR-5 (`requirements.md:191` — "Introducing **one new public type** to host the predicate is
therefore expressly permitted notwithstanding NFR-5"), on the stated ground that only
`Paramore.Brighter` is referenced by all twelve test projects. Cite § *Narrow and deep* for the
*obligation* it does create — widen honestly, record what was widened, apply the after-the-fact check
— and drop the "two tests" framing and the claim that the alternative fails a second test of that
section.

---

### 4. The Architecture Overview's edges are stale: `Core.Tests` is shown calling only `Check`, and the new fourth case is missing from the box (Score: 70)

Round 2 corrected `Sweep`'s *signature* in the diagram but not the diagram's *edges*. `Core.Tests`
calls `Sweep` in at least three of the four cases the design now specifies — AC-29's
`Sweep(itsOwnAssembly)`, the new sweep-over-the-AC-28-doubles case, and the synthetic generic that
exercises the closing path (Implementation step 2) — yet the arrow from the `Core.Tests` box is
labelled `Check`, and the box lists only "AC-28 synthetic negatives" and "AC-29 constructs-nothing".
The one case the ADR calls "the only assertion in the design that the reading path can produce a
failure at all" does not appear in the diagram at all. This is the same class of defect as round-2
finding 1, in the same artefact.

**Evidence**: ADR lines 162-169:

```
        │ Sweep                                  │ Check
  ┌─────┴──────────────────────────┐    ┌────────┴─────────────────────────┐
  │ 12 GENERATED sweep tests       │    │ Core.Tests, hand-written         │
  │ one per gateway test project   │    │  - AC-28 synthetic negatives     │
  │ from ONE Liquid template       │    │  - AC-29 constructs-nothing      │
```

against ADR line 362 ("`Sweep(itsOwnAssembly)`"), lines 371-373 (the fourth case) and lines 401-404
(step 2's synthetic generic).

**Recommendation**: Label the `Core.Tests` edge `Check + Sweep` and add the two missing lines to the
box ("AC-28 doubles swept — reading path's negative", "synthetic generic — closing path").

---

### 5. Fifth site of the argument: the shared-test-support-assembly alternative still rejects on C-10 as one of "three grounds", contradicting C-10's demotion from criterion to evidence (Score: 68)

Context now says C-10 is "the **evidence** that this is the second case and not the first, **rather
than a criterion this design is selected against**", and that "the requirements do not ask the
feature to reach out-of-repo gateway authors". The Alternatives Considered entry for widening a
shared test-support assembly was not updated: it rejects that option "on three grounds", the third of
which is C-10 used exactly as a selection criterion. (The generator-emitted-predicate entry *was*
updated to the evidence framing; this neighbouring entry was not.)

**Evidence**: ADR lines 108-109 — "C-10 is the **evidence** … rather than a criterion this design is
selected against." ADR lines 510-514 — "Rejected on three grounds. … And a test assembly cannot serve
the out-of-repo gateway authors of C-10, who face the identical defect."

**Recommendation**: Rewrite that third ground the way the generator-emitted entry was rewritten — the
objection is that the rule would sit outside the module (C-10 being the evidence that a non-test
caller wants it), not that the requirements ask the feature to reach out-of-repo authors. Or reduce
it to "two grounds" and drop the C-10 clause, since the first two grounds (new public surface in a
test assembly; eleven project references) already carry the rejection.

---

### 6. `CombinedChannelFactory.cs:33` is the wrong line — a citation added in round 2 as the anchor of the central argument (Score: 68)

The quoted expression is at line **34**, and it occurs three times (34, 46, 59 — `CreateSyncChannel`,
`CreateAsyncChannel`, `CreateAsyncChannelAsync`). Line 33 is the method signature. This matters more
than an ordinary off-by-one because this citation is the sole grounding for reading 2's claim that
"`CombinedChannelFactory` already implements it at runtime", and it appears only in the body, not in
the verified grounded-references list at the end.

**Evidence**:

```
$ grep -n "FirstOrDefault" src/Paramore.Brighter/CombinedChannelFactory.cs
21:        get => _factories.OfType<IAmAChannelFactoryWithScheduler>().FirstOrDefault()?.Scheduler;
34:        var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
46:        var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
59:        var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
```

ADR line 96-97 — "(`CombinedChannelFactory.cs:33`)".

**Recommendation**: Cite `CombinedChannelFactory.cs:34, :46, :59` (three call sites strengthens the
"already depends on this rule" point), and add it to the grounded-references list so it is covered by
the "verified against the working tree" claim.

---

### 7. The "honest check" is already answerable in-repo, and the ADR does not address the obvious non-test consumer that exists (Score: 65)

§ *Narrow and deep*'s after-the-fact check is "does anything other than a test ever call it? If
nothing ever does, it was a testing concession after all, and should be revisited." The ADR defers
this to future observation ("if in practice nobody does") while its own Negative bullet already
answers it for this repository: "whose only in-repo consumer is a test guard."

That is avoidable, and the ADR never says why it was not avoided. Two in-repo non-test consumers of
the same judgement already exist or are being built in the sibling: `CombinedChannelFactory` (the
runtime match the ADR cites as proof the rule is real) and ADR 0072's startup rule, whose T3a/T3b
case turns on exactly `D == typeof(InMemoryChannelFactory)` — FR-12's condition 2. The design
therefore ships a *second* expression of the inherited-default judgement in the same package without
stating which is authoritative, which is the drift hazard FR-12 invokes against twelve copies, at a
smaller scale. Whether or not refactoring 0072's rule onto `Check` is in scope, the ADR should say so
and say why; as written, its strongest available support for the design-signal argument is left on
the table.

**Evidence**: `.agent_instructions/testing.md:127-129` (the honest check). ADR lines 444-445,
453-455. ADR 0072:457 — "**T3a/T3b's suppression is architectural, not cosmetic.** When `D` is
`InMemoryChannelFactory` the subscription is a plain `Subscription`/`Subscription<T>` …".

**Recommendation**: Add a sentence to the Negative bullet (or to Context) stating whether 0072's rule
is expected to consume `Check` and, if not, why the duplication of the inherited-default judgement is
accepted — and note in the Negative bullet that the in-repo answer to the honest check is already
"no", so the future test is about out-of-repo callers only.

---

### 8. "All three of AC-27's conjuncts" — non-emptiness is not one of AC-27's conjuncts (Score: 50)

AC-27's Then clause has three parts: implements `IAmAChannelFactory`; is not
`typeof(InMemoryChannelFactory)`; base/derived pair reported at most once. Non-emptiness is the ADR's
own (good) anti-vacuous-pass requirement, not something AC-27 states. Calling the template's three
assertions "all three of AC-27's conjuncts" misattributes one of them and silently merges AC-27's
first two conjuncts into "every `Reason` is `null`". Cosmetic — it does not change what gets built —
but the sentence is the one a reader checks AC-27 coverage against.

**Evidence**: `requirements.md` AC-27 — "Then the type implements `IAmAChannelFactory` and is not
`typeof(InMemoryChannelFactory)`, for every type found, with a base/derived pair reported at most
once." ADR lines 276-277 — "The test asserts **all three** of AC-27's conjuncts: that the result is
non-empty…".

**Recommendation**: "The test asserts three things: that the result is non-empty (this ADR's
anti-vacuous-pass requirement, not AC-27's); that every `Reason` is `null` (AC-27's first two
conjuncts); and … (AC-27's at-most-once clause)."

---

### 9. The blockquote of the *do not export to test* bullet silently drops half of it (Score: 45)

The bullet has four sub-bullets in `testing.md`; the ADR quotes two with no ellipsis. The two omitted
ones ("By following the rules for only testing behaviors, you only need to write tests for the
behaviors exposed from the module not its details" and "Private or Internal classes … do not need
tests") are the ones most directly about *what* a test should target, so trimming them shapes the
reader's view of the rule the ADR then argues against.

**Evidence**: `.agent_instructions/testing.md:103-107` has four sub-bullets under "Do not expose more
than is necessary from an assembly"; ADR lines 70-73 quote two.

**Recommendation**: Add a trailing `> - …` or quote all four.

---

### 10. "All nine overrides are constant `typeof(...)`" will be stale the moment 0072 lands (Score: 40)

The Risks bullet's mitigation rests on there being nine overrides, all constant. ADR 0072 adds three
(the "declaring no override at all" cases) and rewrites two, so on merge there will be twelve. The
claim is true of today's tree and the substance survives, but the count is dated and the reader
cannot tell whether "nine" is deliberate.

**Evidence**: ADR line 476. ADR Context lines 41-43 — "Five of the twelve … three declaring no
override at all — … ADR 0072 corrects those five."

**Recommendation**: "all twelve overrides after 0072's corrections (nine today) are constant
`typeof(...)` expressions".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 10
**Findings at or above threshold (60)**: 7

## Round-2 fixes verified

- **Finding 1 (85) — diagram carried the pre-D6 `Sweep` contract**: **partial, and it introduced a
  new problem.** The signature in the diagram now matches the tuple contract at all five sites, so
  the stated defect is closed. But the diagram's *edges* were not revisited: `Core.Tests` is still
  shown calling only `Check`, and the new fourth case is absent from its box (finding 4 above).
- **Finding 2 (72) — "no path through existing exports" contradicted by the ADR's own
  alternative**: **partial.** The contradicted sentence is gone, no "there is simply no path" remnant
  survives, and C-10 is demoted in Context, the Negative bullet and the generator-emitted-predicate
  entry. Two gaps: the replacement argument invokes a conditional the ADR itself declares unsatisfied
  and misdescribes § *Narrow and deep* as offering "two tests" (finding 3); and a fifth site — the
  shared-test-support-assembly alternative — still uses C-10 as a selection criterion (finding 5).
- **Finding 3 (70) — nothing asserted `Sweep` can return a non-null `Reason`**: **partial, and it
  introduced a new problem.** The fourth case is stated consistently in both places (synthetic-types
  paragraph and Implementation step 2), and the withdrawal of the "AC-29 covers it negatively" claim
  is clean and explicit. But `Sweep` is assembly-scoped and the ADR never says how the case is
  narrowed to the two doubles — and `Core.Tests` already contains `MockSubscription`, whose
  auto-property override returns `null` on an uninitialised instance, so the sweep of that assembly
  will report unrelated non-null reasons (finding 2).
- **Finding 4 (65) — AC-27's "at most once" clause had no assertion**: **not fixed.** The assertion
  added (`Subject` distinctness) is guaranteed by `Sweep`'s own contract and passes over the exact
  regression the ADR cites as its justification, so the clause remains unguarded over the twelve real
  assemblies (finding 1).

Of the six sub-threshold round-2 fixes checked, all are correct: *Test Scope and Isolation* does carry
two top-level bullets and the ADR names the second correctly; § *Narrow and deep* is now cited in
Context and References; `--configuration Release` is in the CI fragment; "three reasons belong to
`Sweep`" matches the three stated; `ci.yml:708-769` is exact (`:708` is `#  rocketmq-ci:`, `:769` is
the file's last line); and `Observability` is a namespace in `Paramore.Brighter` with the xunit
dependency now noted.

## Grounded references sampled and correct

`Subscription.cs:35/172/213/258`, `Command.cs:42`, `RocketMqSubscription.cs:10/50/117-118`, the
Redis/MQTT/Kafka/MsSql/AzureServiceBus override line numbers, `TestConfiguration.cs:38`,
`ci.yml:228/:361`, fourteen existing `test-configuration.json` files (nine of the twelve gateway
projects have one; AzureServiceBus, MQTT and RMQ.Sync do not), twelve
`src/Paramore.Brighter.MessagingGateway.*` directories each with a root namespace equal to its
directory name.
