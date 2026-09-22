# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 7)

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

> **Verification note.** All four findings re-checked against the working tree; **all four hold, none
> rejected, and no grounding error for the fourth consecutive round.** Both at-threshold findings are
> wrong *statements about* the design rather than design defects — exactly where round 6 predicted the
> remaining risk lay. **Threshold trend: 7 → 4 → 7 → 8 → 8 → 5 → 2.**
>
> Round 7 also confirmed, against the codebase, that round 6's two new synthetics are not vacuous — the
> check this document's history most demands. `Command : ICommand` (`Command.cs:42`), and `IEvent` and
> `ICommand` are *siblings* under `IRequest`, so `where T : IEvent` is unsatisfiable by the representative
> argument and `MakeGenericType` genuinely throws. **D11 verified complete** across `Paramore.Brighter`,
> the twelve gateways and `Core.Tests`.

## Findings

### 1. The constructed generic base is attached to the one pair that cannot exercise the `GetGenericTypeDefinition()` reduction — round 6's fix does not cover the branch it claims to (Score: 82)

Round 6's finding 6 asked that one subsumption pair carry a constructed generic base so the reduction is
tested. Implementation Approach step 2 attached it to the pair **whose derived type declares its own
override**. That pair cannot detect a broken reduction.

Walk it with the design's own rule: "A candidate is dropped when it does **not** itself declare
`ChannelFactoryType` … **and** an ancestor in its base chain is also a candidate in the same assembly." For
`Derived : Base<Command>` where `Derived` **declares its own** override, the first conjunct is false, so the
ancestry comparison — the only place the reduction is applied — is never load-bearing. Candidates are
`Base<>` and `Derived`; both are reported whether or not `Base<Command>` is reduced to `Base<>`. The
asserted outcome ("both are reported") is identical under a correct and a broken reduction, so the assertion
cannot fail on the fault it was added for.

The ADR states the correct condition two hundred lines earlier and then contradicts it: the reduction
matters for a derived type **declaring no override**. On that pair a broken reduction would leave the
derived type unsubsumed, both would be reported, and the assertion "the derived is subsumed" would fail —
which is what covering the branch requires.

**Evidence**: ADR:251-253 — "Without that reduction a `FooBar : Foo<Bar>` **declaring no override** would
never match its own base". Against ADR:517-519 — "a pair whose derived type declares **its own**, asserting
both are reported, **with a constructed generic base** (`Derived : Base<Command>`) so that subsumption's
`GetGenericTypeDefinition()` reduction is exercised". Key Components ADR:257-259 said only "One of the two
… pairs below therefore has a **constructed generic base**" without naming which, so step 2 was the sole
binding statement — and named the wrong pair. Round 6's recommendation did not name a pair, so the error is
new in `0c2125bf8`.

**Recommendation**: Move the constructed generic base to the pair whose **derived type declares no
override**, asserting the reported set is `{Base<>}` and not `{Base<>, Derived}`, and say so in Key
Components' "Two subsumption pairs" paragraph rather than only in step 2.

---

### 2. Seventh stale copy: "no shipped assembly has any of these subsumption shapes" is false — all twelve gateways contain the first shape, as step 2 itself says five lines later (Score: 78)

Three passages assert that neither `Core.Tests` subsumption shape occurs in a shipped assembly. The first
shape — a base/derived pair whose derived type declares no `ChannelFactoryType` — occurs in **all twelve**,
and the exact-subject-set assertion depends on it: it is the only reason each gateway resolves to exactly
one subject. The claim "so neither is reachable from the generated tests" is the opposite of the truth, and
it undermines the "discharged in two places" argument it is offered to support — the generated sweeps *do*
test the first shape's outcome; only the second is unreachable from them.

Verified: every gateway is `XSubscription<T> : XSubscription` with the generic derived type declaring no
override, and all nine existing overrides sit on the non-generic base (e.g.
`AWSSQS/SqsSubscription.cs:161`, `MQTT/MqttSubscription.cs:99` — and MQTT's file contains exactly one
`override Type ChannelFactoryType`). The ADR's own correct statement of the fact is in step 2, in the same
sentence-block as one of the wrong ones.

**Evidence**: ADR:309-310 "neither subsumption shape exists in any shipped assembly, so neither is
reachable from the generated tests"; ADR:426 "No shipped assembly has any of these three shapes."; ADR:515
"because no shipped assembly has any of these shapes". Against ADR:522-523 "the generated sweeps test its
outcome on real assemblies, which contain only the first shape." Both wrong statements predate this round
(`git log -S` → `f5ad8419a` and `906b7e396`).

**Recommendation**: Restate the fact once, correctly, and let the other sites point at it.

---

### 3. The justification for the bad-constraints synthetic rules out the very form the synthetic takes (Score: 55)

The paragraph closing the reason-path gap argues that neither the exact set nor the `Core.Tests` cases can
cover an unconfigured skipped subject, giving "being subject-scoped" as the disqualifier — then concludes
"Hence the synthetic", where the synthetic *is* a `Core.Tests` case asserted subject-scoped. Read
charitably the point is that no `Core.Tests` case can reproduce the generated sweeps' blind spot, so the
path must be closed directly instead; as written the stated reason excludes the remedy.

**Evidence**: ADR:449-450 "The `Core.Tests` cases cannot cover this either, being subject-scoped. Hence the
synthetic." Against ADR:456-459, the subject-scoped convention.

**Recommendation**: Replace "being subject-scoped" with what is meant — the blind spot belongs to the
*generated* sweeps, whose expectation comes from configuration, so the remedy is to make the sweep's
behaviour on that path a tested property.

---

### 4. Round 6's step-4 merge joined the ⚠️ ordering warning and the `SharedGenerator` paragraph into one run-on block (Score: 35)

The 0072 ordering constraint moved into step 4 correctly, but was spliced mid-line onto the
`SharedGenerator` material, so a CI-sequencing warning and an unrelated note about twelve unused helper
files now read as one paragraph.

**Evidence**: ADR:538.

**Recommendation**: Break the paragraph after "red on every pull request."

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 1 |

**Total findings**: 4
**Findings at or above threshold (60)**: 2

## Round-6 fixes verified

1. **(70) Reason-path backstop false for an unconfigured subject** — **complete on substance**. The new
   synthetic can genuinely fail. `Command : ICommand` (`Command.cs:42`), `ICommand : IRequest`,
   `IEvent : IRequest` — `IEvent` and `ICommand` are siblings and `Command` implements neither `IEvent` nor
   `Event`. So `class Bad<T> : Subscription where T : IEvent` compiles, is a candidate, is not subsumed
   (its only candidate-eligible ancestor `Subscription` lives in `Paramore.Brighter`, not `Core.Tests`),
   and step 3's `MakeGenericType(typeof(Command))` throws `ArgumentException` — a reason, and a `Single(...)`
   that fails if the type is skipped. **Not vacuous.** Supporting prose muddled (finding 3).
2. **(68) Sixth stale copy** — **complete**. Step 1 schedules all three `Check` branches, step 2 names five
   synthetics, the diagram box gained the new cases, and the counts ("three branches", "six reason paths",
   "five further synthetics") are internally consistent.
3. **(65) `AdditionalExpectedSubjects` generic entry** — **complete**. The JSON form, the backtick→brackets
   rendering, and the prohibition on writing `Ns.Foo<>` in JSON make it implementable and keep the audit's
   namespace comparison working.
4. **(62) Inclusive candidacy (D11)** — **complete and verified**. `InMemorySubscription` (`:26`),
   `InMemorySubscription<T>` (`:78`) and `Subscription<T>` (`Subscription.cs:258`) declare no override, so
   the inclusive rule yields `{Subscription}` in `Paramore.Brighter` and the misaim example holds; the
   strict rule would leave two roots. No gateway assembly contains `Subscription`, so the twelve are
   unaffected, and `Core.Tests` likewise. The survives-subsumption invariant is unaffected: each gateway's
   non-generic base is both a root candidate and the declaring type.
5. **(62) 0072 ordering constraint relocated to step 4** — **complete**, with the step-6 back-reference;
   cosmetic run-on introduced (finding 4).
6. **(58, below threshold) `GetGenericTypeDefinition()` coverage** — **introduced a new problem** (finding
   1): attached to the pair that cannot exercise the reduction.
7. **(50) ADR 0064 attribution** — **complete and correctly grounded**. 0064's C-8 *is* "Placement"
   (`0064:54`, `:242`), and its no-catch rationale at `:165` matches the ADR's quotation.
8. **(45) Distinctness rebuttal** — **complete**; rests on `GetTypes()` distinctness plus "subsumption only
   removes", no longer on closing.

Other grounding sampled and accurate: `Subscription.cs:172`/`:213`; `CombinedChannelFactory.cs:34`/`:46`/
`:59`; `TestConfiguration.cs:38`; `ci.yml:228`/`:361`/`:708`; all nine override line numbers, all constant
`typeof(...)` on non-generic bases, two of them consumer factories as claimed; the 9/3 constraint split;
all twelve configuration-table type names and the namespace-equals-directory-name premise; fourteen
`test-configuration.json` files with AzureServiceBus, MQTT and RMQ.Sync absent.

**Requirements fidelity, checked end to end**: every normative MUST in FR-12 is satisfied —
construction-free read via `GetUninitializedObject`, a pure `(Type, Type)` predicate returning a reason, no
broker access, generic closing honouring each constraint form, the conditional at-most-once obligation, and
one sweep per gateway project. AC-28's "naming the offending subscription type and the type it declared" is
met by both failure reasons. **Nothing in the ADR is beyond what the requirements ask.**

## Approval readiness

**Not ready — but narrowly.** The design is sound and the requirements are fully met; both blocking
findings are wrong statements about the design.

The single most important item is **finding 1**: the constructed generic base must move to the no-override
pair. As specified, the reduction is claimed to be covered by a test that cannot fail if the reduction is
broken — the same class of defect this document has already produced twice. Finding 2 is a one-sentence
factual correction in three places. **Neither requires touching the Key Components specification, so a
targeted fix followed by `/spec:approve design` is the right next move.**
