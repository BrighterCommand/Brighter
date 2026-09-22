# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 6)

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: NEEDS WORK

5 findings at or above threshold 60. Address these before approving.

> **Verification note.** All eight findings re-checked against the working tree; **all eight hold, none
> rejected, and no grounding error for the third consecutive round.**
>
> **Threshold trend: 7 → 4 → 7 → 8 → 8 → 5 — the first fall in three rounds**, and the composition
> changed more than the count. The duplication-drift defect that recurred in five consecutive rounds
> happened **once** here (finding 2), on material the re-derivation itself added, in the diagram and the
> step list — both *outside* the section that was re-derived. Key Components' internal consistency is
> clean: D10 checked at six sites, the candidate/subject vocabulary at all seventeen occurrences, the
> branch names across the table, Risks, Alternatives and `Core.Tests`. Four of the five at-threshold
> findings are in material the re-derivation newly **specified** rather than restated. See *Did the
> re-derivation work?* below.

## Findings

### 1. The reason-path coverage argument's backstop is false in the one case the guard exists for — an unconfigured second subject that is silently skipped leaves the exact set matching (Score: 70)

Key Components leaves two of the six reason paths unasserted and justifies it with a two-part claim: the
paths are unreachable, and *if* one were reached, a silent skip would be caught by the exact subject set.
The second half does not hold generally.

Walk the case. The exact-set assertion compares `Sweep`'s reported subjects against `SubscriptionType` ∪
`AdditionalExpectedSubjects`. The ADR states the purpose of that union two paragraphs earlier: "An
assembly that grows a second declaring subscription fails its sweep until that list is updated — which is
the point: a new gateway subscription type is exactly the event this guard exists to notice."

Now suppose a thirteenth gateway, or growth of an existing one, adds a second declaring subscription that
is generic with a constraint `typeof(Paramore.Brighter.Command)` does not satisfy — `where T : IEvent`, or
arity 2. That is precisely the "unsatisfiable arity or constraints" path. The type is **not** in
`AdditionalExpectedSubjects`; nobody has added it, and its absence is the event being guarded. If the
implementation silently skips it instead of emitting a reason, the reported set is unchanged, it equals
the configured expectation exactly, every `Reason` is null, and the test is **green**. The exact-set
assertion catches nothing, because the skipped subject was never expected.

The claim is true only for a subject *already configured* — skipping such a subject shrinks the set and
fails. It is false for exactly the class of subject the guard was built to notice. The `Core.Tests` cases
cannot cover it either, being subject-scoped (`result.Single(e => e.Subject == typeof(X))`), so they too
only detect the disappearance of a subject the test already names. The parallel claim for the "instance
cannot be produced" path fails for the same reason.

**Evidence**: ADR:420-427 "The remaining two … are **not** asserted, and that is accepted: both are
unreachable for any shipped or synthetic type this design declares, and a *silent skip* in either is
caught by the exact subject set." Against ADR:309-311 "An assembly that grows a second declaring
subscription fails its sweep until that list is updated — which is the point". And ADR:429-432, the
subject-scoped `Core.Tests` convention.

**Recommendation**: Either state the claim accurately — a silent skip is caught only for a subject the
configuration already expects, and an *unconfigured* subject silently skipped is a genuine vacuous pass
this design does not detect — or close the hole. The cheapest close is one `Core.Tests` synthetic whose
constraints `Command` does not satisfy (`where T : IEvent`, one line), asserted to carry a non-null
reason, making "never a silent skip" tested on the path where a skip is invisible.

---

### 2. Sixth stale copy: the throwing double and the null-branch `Check` case — both introduced by the re-derivation — appear in no Implementation Approach step, and the diagram's `Core.Tests` box omits the throwing double (Score: 68)

The re-derivation added two test obligations, both the substance of round 5's finding 4: a `Core.Tests`
double whose `ChannelFactoryType` getter throws on an uninitialised instance, and an explicit assertion of
`Check`'s **null** branch with literal arguments. Key Components specifies both. Neither reaches the
passages that *describe* Key Components.

**Step 1** covers `Check` and names only "AC-28's two synthetic subscriptions and their doubles" — the null
branch is this ADR's own addition, not an AC-28 shape, so it is not there by implication either. **Step 2**
enumerates its additions exhaustively — "Three further synthetics are added here" — and the three named are
the generic subscription and the two subsumption pairs. The throwing double is a fourth, and the count says
three. An implementer working the steps builds neither new case, so round-5 finding 4's fix is documented
but never scheduled. The **diagram**'s `Core.Tests` box lists the generic and the subsumption pairs but not
the throwing double — the same class of defect for the sixth consecutive round, this time on a case whose
whole purpose is to be visible.

**Evidence**: ADR:416-418 the throwing double; ADR:420-423 the null-branch requirement; against ADR:480-481
(step 1), ADR:484-487 (step 2, "**Three** further synthetics"), and ADR:163-171 (the diagram's box).

**Recommendation**: Add the null-branch assertion to step 1 and the throwing double to step 2, and add a
line to the diagram's `Core.Tests` box.

---

### 3. `AdditionalExpectedSubjects` is not implementable for a generic entry: the ADR fixes what must be rendered (`typeof(Foo<>)`) but not what the configuration string looks like or how arity is recovered (Score: 65)

The property table types the key as `List<string>` and fixes the rendering: "A generic entry renders as
`typeof(Foo<>)`". The other twelve configured values are "Fully-qualified name of the subscription type",
which for an open generic definition is ``Ns.Foo`1``.

The template must turn one into the other and nothing says how. A backtick is not legal in a C# type name
in source, so the string cannot be emitted verbatim. To produce `typeof(Ns.Foo<>)` the generator must strip
the `` `n `` suffix and emit *n*−1 commas inside the brackets. Alternatively the author writes `Ns.Foo<>`
directly in JSON, in which case the value is no longer a name `Type.FullName` could produce and the audit's
namespace comparison must account for it. Two developers implement this two ways and one produces a file
that does not compile.

`SubscriptionType` escapes the problem only because the table separately forbids a generic value there.
`AdditionalExpectedSubjects` has no such restriction — it exists precisely for a second declaring type,
which *can* be generic, and the ADR anticipates that case.

**Evidence**: ADR:305-306, the two property rows.

**Recommendation**: One sentence stating the JSON form and the rendering rule — the arity backtick as
`Type.FullName` reports it, rendered by replacing the suffix with brackets carrying *n*−1 commas — or
restrict the key to non-generic subjects and say a generic second subject is a future change.

---

### 4. Step 1's candidate rule does not settle whether `Subscription` itself is a candidate, and the `{Subscription}` worked example only holds under one of the two readings (Score: 62)

Step 1: "Non-abstract classes whose base chain reaches `Paramore.Brighter.Subscription`." Read strictly, a
type's base chain is its *ancestors*, so `Subscription` is not a candidate in its own assembly. Read
inclusively, it is.

Invisible in the twelve gateway assemblies, but the ADR depends on it in the misaim division-of-labour
argument. `Paramore.Brighter` contains four non-abstract types in the family, only `Subscription` defining
`ChannelFactoryType`: `Subscription` (`Subscription.cs:35`), `Subscription<T>` (`:258`),
`InMemorySubscription` (`InMemorySubscription.cs:26`), `InMemorySubscription<T>` (`:78`).

Under the **inclusive** reading `Subscription` is the root candidate, the other three declare no override
and have a candidate ancestor, so all three are subsumed and the sweep reports `{Subscription}` — the ADR's
claim. Under the **strict** reading `Subscription` is not a candidate, so `Subscription<>` and
`InMemorySubscription` are both roots and neither is subsumed; the sweep reports
`{Subscription<>, InMemorySubscription}`, which does not match a configuration naming
`Paramore.Brighter.Subscription`, so the misaim fails on the **set** check rather than the **reason** check
the ADR names. The References entry hints the inclusive reading is intended — it notes "`Subscription` is
non-abstract (`:35`)", load-bearing only if `Subscription` can itself be a candidate — but Key Components
never says so.

**Evidence**: ADR:233-235 step 1; ADR:288-290 the misaim example; ADR:702 the References note. Working
tree: the four types above; `InMemorySubscription.cs` contains no `ChannelFactoryType`.

**Recommendation**: Change step 1 to "Non-abstract classes that **are** `Paramore.Brighter.Subscription`
or whose base chain reaches it" — the reading the rest of the document assumes, and the one FR-12's
"assignable to `Subscription`" implies — or state the strict reading and correct the example.

---

### 5. The ADR-0072 ordering constraint is recorded inside step 2, not in steps 4 and 6 that it governs (Score: 62)

Round 5's finding 6 asked for the constraint where an implementer sequencing the work would meet it — "a
clause to step 4 or a Negative bullet". The fix put the paragraph at the end of **step 2**, where it is a
non-sequitur: step 2 is `Sweep` plus its synthetics, which have no dependency on FR-7 to FR-11 at all. The
paragraph's own first sentence announces it is about other steps. Steps 4 and 6 contain no reference
either way, so a developer executing step 6 — the step that turns the `build` job red if 0072 has slipped
— reads nothing about it. The re-derivation's commit message claims "steps 4 and 6 now record that they
sequence after 0072's FR-7 to FR-11"; they do not.

The arithmetic is correct: nine overrides today, 0072 adds three (FR-9, FR-10, FR-11) and corrects two
(FR-7, FR-8), so five of the twelve report a non-null reason until those land — matching
`requirements.md:521`.

**Evidence**: ADR:491-497, the closing paragraph of step 2; against ADR:502-513, steps 4 and 6.

**Recommendation**: Move the paragraph to step 4 and add a clause to step 6.

---

### 6. The `GetGenericTypeDefinition()` reduction lost its coverage statement in the rewrite (Score: 58)

Step 2 specifies that an ancestor which is a constructed generic is reduced before comparison. All twelve
shipped pairs are `XSubscription<T> : XSubscription` with a **non-generic** base, so no shipped assembly
exercises the reduction. The old section said so and claimed coverage — "No shipped assembly has that shape
today, so step 2's synthetic types cover it." The re-derivation kept the specification and dropped both the
acknowledgement and the coverage claim, and neither new subsumption pair is specified to have a constructed
generic base. The replacement sentence, "In every shipped assembly this reduces the pair to its non-generic
base", is also slightly off: in a non-generic-base pair no reduction happens at all.

**Evidence**: ADR:240-246 against the removed text in `git show f5ad8419a`.

**Recommendation**: Specify that one of the two subsumption pairs has a constructed generic base
(`Derived : Base<Command>`), covering the reduction at no extra cost, or restore the acknowledgement that
the branch is uncovered.

---

### 7. References attributes a "placement rule" to ADR 0064 that the re-derived CI-placement section no longer invokes (Score: 50)

The old Key Components justified the `build`-job placement partly by analogy to the generator audit. The
re-derived `#### CI placement` section argues entirely from three concrete CI facts and never cites 0064.
References still describes 0064 as "source of the placement rule and of 'rules must not catch'". 0064's
placement rule is its C-8, about *which rule class a rule is declared in* — something this ADR explicitly
does not do ("this guard adds no rule").

**Evidence**: ADR:695; the CI placement section, which contains no mention of 0064; `0064:242`, C-8.

**Recommendation**: Trim to "source of 'rules must not catch', from which this ADR's sweep deliberately
differs, for stated reasons".

---

### 8. The distinctness rebuttal in Alternatives still reasons in terms of closed constructions (Score: 45)

D10 fixed `Subject` as the open definition. The relocated withdrawn-assertion passage argues distinctness
"holds by construction" because "no two open definitions close to the same type" — a closing-based argument
left over from when `Subject` was the closed construction. Under D10 the argument is simply that
`Assembly.GetTypes()` yields distinct types and subsumption only removes.

**Evidence**: ADR:656-658.

**Recommendation**: Drop the closing clause.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 6 |
| 0-49 (Low) | 1 |

**Total findings**: 8
**Findings at or above threshold (60)**: 5

## Round-5 fixes verified

1. **(78) `Subject`'s identity unspecified** — **complete**. D10 stated once in the `Sweep` contract and
   consistent at all six sites: subsumption before closing on open definitions; step 3's closed type
   "exists only to be instantiated and read"; `AdditionalExpectedSubjects` rendering; the `Core.Tests`
   generic assertion; ordering by `Subject.FullName` (an open definition's is `` Ns.Foo`1 ``, non-null);
   the reason message. Both supporting claims verified — `typeof(Foo<>)` is valid C#, and a closed
   construction's assembly-qualified `FullName` is unusable in a message. Residue: finding 3.
2. **(72) The false wrong-assembly claim** — **complete in substance**. The ADR says plainly the exact set
   cannot catch a misaim, names the three mechanisms that do, and `requirements.md:521` matches. The
   `{Subscription}` example inside it holds under only one reading of step 1 — finding 4, a new problem
   surfaced by the fix rather than a failure of it.
3. **(72) Fifth stale copy, "one entry per candidate"** — **complete**. The diagram reads "one entry per
   subject", and the two terms are used consistently at all seventeen sites.
4. **(70) Four of six reason paths untested** — **partial, and it introduced a new problem**. Two are now
   covered and the remaining two explicitly declared unasserted, which is what was asked — but the
   justification is unsound (finding 1), and neither new case is scheduled (finding 2).
5. **(68) `AdditionalExpectedSubjects` under-specified** — **substantially complete**. Property table with
   type and default, union semantics, the audit's `SubscriptionType`-only counting stated and justified,
   the table's silence made explicit, and the survives-subsumption invariant written down. Only the generic
   string form remains open (finding 3).
6. **(65) The five-red ordering constraint** — **partial**. Content and arithmetic correct, filed in the
   wrong step (finding 5).
7. **(62) C-9's throw-rule misapplied; helper types unnamed** — **complete**. The three subscription
   doubles are explicitly exempted with the reason, both helper types named, and C-9's rule attached only
   to the channel-factory double with its three members enumerated.
8. **(62) Truncated FR-12 quote** — **complete**. Step 2 quotes the amended clause with its condition
   inside the quotation marks, matching `requirements.md:193` verbatim.

Grounding sampled and correct: `Subscription.cs:35`/`:172`/`:213`/`:258`, `InMemorySubscription.cs:26`/`:78`,
`RocketMqSubscription.cs:10`/`:50`/`:117`, `Command.cs:42`, `TestConfiguration.cs:38`,
`When_constructing_a_channel_with_combined_factory.cs:85`, `ci.yml:228`/`:361`/`:708`, `0072:457`,
`0064:165`. **No grounding errors — a third consecutive clean round.**

## Did the re-derivation work?

**Partly, and more than the raw count suggests.**

The count fell 8 → 5, the first fall in three rounds. More important is what the findings are made of. The
five previous rounds were dominated by one mechanical failure: a decision settled, the passage stating it
rewritten, and a second passage restating it left behind — five times running. This round it happened
**once**, on the narrowest possible surface: a case added by the rewrite itself, in the diagram and the step
list, both outside the re-derived section. Key Components' internal consistency is clean.

The three relocated passages survived intact, each stated once, each still connected to what depends on it.
The only reasoning genuinely lost is the `GetGenericTypeDefinition()` coverage claim (finding 6) and one
paragraph of CI-placement rationale (finding 7) — both below threshold.

What changed is where the defects live. Four of the five at-threshold findings are in material the
re-derivation newly **specified** rather than restated: an argument that does not survive its own worked
case, a rendering rule with no input format, a discovery rule with two readings, a scheduling note in the
wrong step. That is the normal residue of newly written specification, and a better class of defect than
the fifth copy of a superseded sentence. None is a contradiction; none makes the design unimplementable.

**Round 7 should be small — if the fixes are applied without reopening the re-derived section for another
patch.** Finding 2 is the warning: the passages that *describe* Key Components (diagram, steps, References)
are now the weakest part of the document, and they were not re-derived.
