# Review: requirements — 0037-validate-subscription-channel-factory (round 5)

**Date**: 2026-09-15
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Both sub-threshold items have since been applied.

## Round 4 disposition

| # | Prior finding | Status | Justification |
|---|---|---|---|
| 1 | AC-13b's Given produces a match, not a mismatch (85) | **FIXED** | AC-13b states its own arrangement in full: `CombinedChannelFactory([NonMatchingChannelFactory, AlphaBus.ChannelFactory])` with a `DeclaringSubscription`. Neither inner factory is `DeclaredChannelFactory`, so FR-3's combined arm finds no match and a finding is genuinely produced; `D != typeof(InMemoryChannelFactory)`, so T2 is genuinely selected. The inherited chain to AC-7/AC-6 is severed, with a blockquote recording why. |
| 2 | NFR-6's exception set incomplete — a fourth, runtime-broken case (65) | **FIXED** | C-12 added, enumerated in NFR-6, carried into C-8's release-note obligations with the explicit statement that `throwOnError: false` does not rescue it. Every substantive code claim verifies. |
| 3 | FR-5 "exactly three literals" but four specified (62) | **FIXED** | Now "exactly four", T3 split into T3a (direct) / T3b (combined), each spelled out. AC-13c pins T3b including the `DoesNotContain` assertion. C-11's obligation updated to cite T3a/T3b. |
| 4 | FR-12's Unknown-defaulting list names MsSql (60) | **FIXED**, and the rejected sub-claim **correctly rejected** | `MsSql` → `RocketMQ`: verified `RocketMqSubscription.cs:90`/`:148` default to `Unknown`; GCP, Postgres and Kafka likewise. The Reactor/Proactor list verifies item-for-item. On the rejection: `grep -n` returns `15: if (subscription is not RocketSubscription)`. Round 4's claim that `:15` is a brace was wrong; the document was already correct. |
| 5 | `TestRequest` placeholder scope (55) | **FIXED** | C-9 now reads "FR-1 to FR-11 examples"; FR-5's illustration uses `…TestDoubles.FakeChannelFactoryRequest`, matching AC-12. The disallowed `Greetings.Ports.Events.TestRequest` is gone. |
| 6 | FR-12's predicate has no stated home (50) | **FIXED** | Placement deferred to the ADR, one new public type expressly permitted notwithstanding NFR-5 (correctly characterised as constraining *existing* abstractions), twelve copies forbidden. |

Six of six fixed.

## Findings

### 1. FR-12 said `Paramore.Brighter.ServiceActivator` is referenced by five of the twelve gateway test projects; it is six (Score: 55) — APPLIED

**Evidence**: Grepping `Paramore.Brighter.ServiceActivator.csproj` across exactly the twelve `.csproj` files of C-9's table returns **six**: `AWS.Tests`, `AWS.V4.Tests`, `MQTT.Tests`, `RMQ.Async.Tests`, `RMQ.Sync.Tests`, `RocketMQ.Tests`. The other two counts verify within the twelve: `Base.Test` → `Gcp.Tests`, `MSSQL.Tests`, `PostgresSQL.Tests` (three — the six further referencing projects are not gateway projects); `Paramore.Test.Helpers` → `MQTT.Tests` only (one). C-9's separate, narrower claim about the five corrected-transport projects is correct and unaffected.

Nothing downstream changes — six is still not twelve, so the conclusion and the new-type permission both stand.

**Resolution**: corrected to "by six", with the six projects named.

---

### 2. C-12 cited `CombinedChannelFactory.cs:36` for the throw; the throw is at `:37` (Score: 50) — APPLIED

**Evidence**: Verified layout — `34: var factory = _factories.FirstOrDefault(...)` / `35: if (factory == null)` / `36: {` / `37: throw new ConfigurationException(...)`. AC-9 cites the same construct correctly as `:35-38`. The `:34` cites in C-12, the Problem Statement and FR-3 are correct.

**Resolution**: corrected to `:37`.

---

## Notes on the regions flagged for scrutiny (no findings)

- **C-12 is genuinely distinct** from C-2 (direct arm, rule verdict), C-10 (out-of-repo types) and C-11 (MQTT, shipped types, direct arm). It is the only exception caused by the *corrections* rather than the rule, and the only one not suppressible by `ValidatePipelines(throwOnError: false)`. Its scope is correctly limited to the three no-override transports: GCP and MQTT declare a *consumer* factory type today, so neither ever matched an inner `InMemoryChannelFactory` and neither has an accidental in-memory route to withdraw. No contradiction with OOS-5 — `CombinedChannelFactory`'s matching semantics are untouched; only the `ChannelFactoryType` values feeding them change.
- **NFR-6's closing sentence** no longer contradicts C-12; it names C-11 and C-12 as the residue the FR-7-to-FR-11 corrections cannot remove. No fifth in-repo class of configuration could be constructed that starts today and fails after this change.
- **FR-5's four templates are total and non-overlapping** over `(arm, D == InMemoryChannelFactory)` — 2×2, all four cells filled, none doubled. Each has exactly one pinning AC (T1→AC-13, T2→AC-13b, T3a→AC-13a, T3b→AC-13c), and each Given traces to the outcome it asserts. AC-15's `ChannelFactory`-token assertion remains satisfiable alongside T1.
- **The double rename is clean.** No `FakeTransportChannelFactory` / `FakeOtherChannelFactory` / `FakeTransportSubscription` survives; the only remaining `Fake*` tokens are the deliberately-retained request types. No prose was left dangling by the substitution.
- **FR-12's "one new public type" paragraph** conflicts with neither NFR-5 (scoped to *existing* abstractions, explicitly overridden) nor OOS-3 (forbids members on `IAmAChannelFactory`, not new types elsewhere).
- **AC-25a–e / AC-26a–e without individual headings — not a finding.** AC-25 and AC-26 each carry a complete, transport-agnostic Given/When/Then, and the a–e suffixes enumerate the parameter set (transport ⇒ host project) exhaustively. The per-transport subscription types are pinned individually by AC-20 to AC-24, and C-9's table maps each project to its gateway. Two developers would produce the same five tests per criterion. Parameterised instantiation of a single criterion is legitimate practice; expanding to ten headings would restate the same Given/When/Then ten times.
- **Other re-verified cites, all correct**: `Subscription.cs:48`, `:172`, `:213`, `:219`; `IAmConsumerOptions.cs:12`; `ValidationError.cs:33`; `Specification.cs:35`; `DispatchBuilder.cs:146-148`; `ServiceCollectionExtensions.cs:159`, `:199`; `BrighterPipelineValidationExtensions.cs:58`/`:60`; `MqttSubscription.cs:35`, `:132`; all of C-3's throw sites; C-9's twelve test-project directories all exist; `Core.Tests`'s reference list matches C-9 exactly; `Extensions.Tests` references `ServiceActivator.Extensions.DependencyInjection` and not `Core.Tests`; no subscription type in `src/` declares a parameterless constructor; `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs:83` is the Kafka + RMQ.Async `CombinedChannelFactory`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 0 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

Findings at or above threshold across rounds 1-5: **10, 13, 7, 4, 0**.
