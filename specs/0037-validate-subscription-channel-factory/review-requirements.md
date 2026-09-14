# Review: requirements — 0037-validate-subscription-channel-factory (round 4)

**Date**: 2026-09-14
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

## Round 3 disposition

| # | Prior finding | Status | Justification |
|---|---|---|---|
| 1 | C-9 forbids `TestRequest` (90) | **FIXED** | Prohibition names `GreetingMade` again with the corrected `samples/WebAPI/*` list; a positive placeholder rule was added. Residual scoping gap is finding 5 (below threshold). |
| 2 | AC-13/AC-13a cannot both pass (80) | **FIXED** | T3 suppression is sound: T1/T2/T3 partition `(arm, D)` totally and without overlap, AC-13a's `DoesNotContain` is satisfiable, FR-5's rationale is honoured. Two new defects introduced in the fix — findings 1 and 3. |
| 3 | NFR-6's "exactly two exceptions" (70) | **PARTIALLY FIXED** | Count gone, MQTT documented as C-11, all C-11 code claims verify. But the enumerated set is still incomplete — finding 2. |
| 4 | AC-20/AC-24 Givens throw (68) | **FIXED** | Verified both constructors accept those arguments in that order with `messagePumpType` named-optional; both Givens compile. |
| 5 | C-9's range omits AC-26f (62) | **FIXED** | Now "AC-20 to AC-26f", "five" dropped, AC-26f carved out of reflection-only; AC-section header matches. |
| 6 | FR-12's mechanism rationale false (62) | **PARTIALLY FIXED** | The universal reason is now correct and verified. The supporting list introduces a new false claim — finding 4. |
| 7 | FR-6's `enabled: false` unasserted (62) | **FIXED** | AC-17a added, well-formed, cite verifies. |
| 8 | AC preamble cites C-9 for a rule it lacks (55) | **FIXED** | Cite is valid again. |
| 9 | C-10 bullet 4 overstates (50) | **FIXED** | Carve-out correct against `CombinedChannelFactory.cs:34`. |
| 10 | AC-13's `{F}` conflicts with FR-5 (45) | **FIXED** | `{F}` / `{F-list}` split; "is one of:" for combined. |
| 11 | AC-30's arrangement unnamed (45) | **FIXED** | Named and verified coherent — neither subscription matches any of the three inner factories. |

Nine of eleven fully fixed.

## Findings

### 1. AC-13b's composite Given produces a *match*, not a mismatch — it contradicts AC-6 (Score: 85)

AC-13b is the only criterion pinning template T2, and its arrangement is inherited by reference from AC-7, which inherits its factory list from AC-6. Following the chain, the subscription AC-13b substitutes is served by one of the inner factories, so the rule produces **no** finding and the T2 literal can never be asserted.

**Evidence**: AC-6 — "`options.DefaultChannelFactory = new CombinedChannelFactory([new FakeTransportChannelFactory(), new FakeOtherChannelFactory()])` … a `FakeTransportSubscription` named `sub-a` … **Then no findings are produced for either subscription**." AC-7 — "Given the `CombinedChannelFactory` of AC-6 and a plain `Subscription<FakeChannelFactoryRequest>`". AC-13b — "Given the configuration of AC-7 but with a `FakeTransportSubscription` … against a `CombinedChannelFactory` no inner factory of which matches".

Resolving: `D == typeof(FakeTransportChannelFactory)`; inherited inner set `[FakeTransportChannelFactory, FakeOtherChannelFactory]`; FR-3's combined arm ("compatible iff at least one inner factory `f` satisfies `f.GetType() == D`") is satisfied by inner factory #1. That is exactly AC-6's `sub-a` case, which AC-6 asserts is silent. AC-6 and AC-13b assert opposite outcomes for the same configuration, and AC-13b's own trailing qualifier is unachievable with the arrangement it names.

**Recommendation**: Give AC-13b a self-contained Given that genuinely mismatches, e.g. "Given `options.DefaultChannelFactory = new CombinedChannelFactory([new FakeOtherChannelFactory(), new AlphaBus.ChannelFactory()])` and a `FakeTransportSubscription` named `sub-a` with `ChannelFactory` null". Do not inherit from AC-7, whose inner set is fixed by AC-6's no-false-positive case.

---

### 2. NFR-6's exception set is still incomplete: a fourth in-repo case, broken at *runtime* by FR-9 to FR-11 (Score: 65)

NFR-6 dropped the "genuinely correct" qualifier so that *any* configuration which starts successfully today falsifies it. A fourth such class exists, and unlike C-2/C-10/C-11 it is broken by the transport corrections themselves rather than by the rule — so disabling `ValidatePipelines` does not rescue it.

**Evidence**: The uncovered case — `options.DefaultChannelFactory = new CombinedChannelFactory([new InMemoryChannelFactory(bus, TimeProvider.System), …])` with an `SqsSubscription<T>` (AWSSQS or V4) or `PostgresSubscription<T>`. Verified today: `grep -rn "override Type ChannelFactoryType" src` returns exactly nine hits, none in AWSSQS, AWSSQS.V4 or Postgres, so `ChannelFactoryType` is `typeof(InMemoryChannelFactory)`; `CombinedChannelFactory.cs:34`'s `f.GetType() == subscription.ChannelFactoryType` matches the inner `InMemoryChannelFactory` **exactly**, and the host starts. After FR-9/FR-10/FR-11 the declared type becomes the real transport factory, no inner factory matches, and `CombinedChannelFactory.cs:36` throws `ConfigurationException("No channel factory found for subscription …")` when the Dispatcher builds channels.

- C-2 does not cover it: C-2 is the *direct*-arm case and expressly reasons that "`InMemoryChannelFactory` does not downcast the subscription and so would not throw" — here it *does* throw, from `CombinedChannelFactory`.
- C-10 does not cover it: these are shipped in-repo types.
- C-11 does not cover it: MQTT only, and MQTT already fails combined routing today.

C-8 records only the positive half ("routable by `CombinedChannelFactory` for the first time"), not the loss of the accidental in-memory route.

**Recommendation**: Add C-12 — "an AWS SQS, AWS SQS V4 or Postgres subscription routed by a `CombinedChannelFactory` containing an `InMemoryChannelFactory` is routed to the in-memory bus today and will throw `ConfigurationException` after FR-9 to FR-11; this is the silent-wrong-bus outcome C-2 exists to prevent, and it is accepted." List it in NFR-6 and in C-8's release-note obligations, noting that unlike the others it is **not** avoidable via `ValidatePipelines(throwOnError: false)`.

---

### 3. FR-5 declares "exactly three literals" but specifies four; T3's combined rendering is elided and pinned by no AC (Score: 62)

The T1/T2/T3 selection rule is total and non-overlapping — the improvement round 3 asked for. But T3's row carries two different literals in one cell, contradicting the normative sentence above the table, and the second is written as an ellipsis fragment.

**Evidence**: FR-5 — "Item 4 MUST be rendered as **one of exactly three literals**". T3's cell — "`— use a subscription type whose ChannelFactoryType is {F}` (direct) / `…is one of: {F-list}` (combined)". That is two literals, four in total.

AC coverage: AC-13 pins T1, AC-13b is intended to pin T2 (finding 1), AC-13a pins T3-direct. T3-combined — exactly AC-7's configuration — is asserted by nothing; AC-7 checks only that both inner display names appear and that `CombinedChannelFactory` is not named. Since T3's purpose is the normative suppression of the `{D}` half, leaving its combined half unasserted leaves the multi-bus headline case unguarded. C-11's obligation explicitly leans on "FR-5's template T3 already renders exactly that remedy direction".

**Recommendation**: Reword to "exactly four literals", split into T3a (direct) and T3b (combined) with T3b spelled out in full, and extend AC-7 (or add AC-13c) to assert the T3b literal and the absence of `configure a channel factory of type`.

---

### 4. FR-12's Mechanism paragraph names MsSql among the `Unknown`-defaulting transports; it defaults to `Proactor` (Score: 60)

Round 3 finding 6 replaced a false blanket claim with a correct universal one. The correct half verifies — no subscription type in `src/` declares a parameterless constructor, so `Activator.CreateInstance(Type)` throws `MissingMethodException`. But the supporting list is wrong about one of the four it names.

**Evidence**: FR-12 — "several transports' constructors default `messagePumpType` to `MessagePumpType.Unknown` (GCP Pub/Sub, Postgres, **MsSql**, Kafka among them)". Verified `MsSqlSubscription.cs:79` (non-generic) and `:128` (generic): both read `MessagePumpType messagePumpType = MessagePumpType.Proactor,`. Neither can hit `Subscription.cs:213`'s `Unknown` guard. (GCP, Postgres and Kafka's non-generic form do default to `Unknown`; RocketMQ also does and is absent from the list, which "among them" tolerates.)

Secondary: `MqttSubscription.cs:131` is cited twice for the `Proactor` default; line 131 is `TimeSpan? unacceptableMessageLimitWindow = null,` and the `Proactor` default is line **132** (the non-generic form at `:80` defaults to `Unknown`). `RocketMqChannelFactory.cs:15` in C-3 points at the guard's opening brace; the `is not RocketSubscription` test is `:14`.

**Recommendation**: Replace "MsSql" with "RocketMQ" in the `Unknown` list, correct `MqttSubscription.cs:131` → `:132` in both places, and `RocketMqChannelFactory.cs:15` → `:14`.

---

### 5. `TestRequest`'s placeholder scope does not reach the FR-1 to FR-5 examples (Score: 55)

**Evidence**: C-9 scopes the placeholder to "the transport-correction criteria (AC-20 to AC-26f) and … the **FR-7 to FR-11** examples". But FR-1, FR-2, FR-3 and FR-4 use `TestRequest` too, unscoped. FR-5's display-name illustration goes further, using `Greetings.Ports.Events.TestRequest` — a namespace C-9 itself says contains `GreetingEvent`. The binding assertion (AC-12) correctly uses `…TestDoubles.FakeChannelFactoryRequest`, so nothing is unimplementable, but the illustration cites a type that cannot exist under the document's own rules.

**Recommendation**: Widen C-9's placeholder sentence to "the FR-1 to FR-11 examples", and change FR-5's illustration to `Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest`, matching AC-12.

---

### 6. FR-12's "pure predicate" has no stated home, yet twelve test projects must call it (Score: 50)

**Evidence**: FR-12 mandates a shared predicate; AC-27 mandates twelve callers. Verified from the twelve `.csproj` files: `Paramore.Brighter.Base.Test` is referenced by only three, `Paramore.Test.Helpers` by one, `ServiceActivator` by five. No shared test-support assembly reaches all twelve except `Paramore.Brighter` itself. An implementer must either add a new public type to a shipped assembly or copy the predicate twelve times — and NFR-5 ("no public API change to existing abstractions beyond what FR-7 to FR-11 require") is silent on whether a *new* type is permitted, which reads as a prohibition to a cautious implementer.

**Recommendation**: Either state that the predicate's placement is an ADR decision and explicitly permit a new type under NFR-5, or name the home directly.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 0 |

**Total findings**: 6
**Findings at or above threshold (60)**: 4
