# Review: requirements — 0037-validate-subscription-channel-factory (round 3)

**Date**: 2026-09-14
**Threshold**: 60
**Verdict**: NEEDS WORK

7 findings at or above threshold 60. Address these before approving.

## Round 2 disposition

| # | Prior finding | Status | Justification |
|---|---|---|---|
| 1 | Remedy template gives wrong advice in headline case (78) | **PARTIALLY FIXED** | Template is now asymmetric and AC-13a was added, but the `{D}` half still literally reads "configure a channel factory of type `Paramore.Brighter.InMemoryChannelFactory`" in AC-1's case — which AC-13a then forbids. See finding 2. |
| 2 | AC-12 asserts a literal that cannot exist / `GreetingMade` (75) | **PARTIALLY FIXED** | AC-12's literal is now `…TestDoubles.FakeChannelFactoryRequest` and is reachable; verified `GreetingMade` appears in no test project. But the global `GreetingMade → TestRequest` replace also rewrote C-9's *prohibition* sentence, which now forbids the very type AC-20..AC-24 use. See finding 1. |
| 3 | AC-15 contradicts FR-5's remedy literal (72) | **FIXED** | Word-boundary restatement plus an explicit `ChannelFactoryType` carve-out and a paragraph explaining why it is needed. Both can pass. |
| 4 | Error verdict for out-of-repo `Subscription` subclasses (70) | **FIXED** (accepted, not softened) | C-10 + D3 discharge it honestly: population named, D1's consequence stated, exposure split into four cases, three concrete obligations imposed. Verified `Subscription.cs:172`; bullets 1 and 2 verified true. Bullet 4 over-broad — finding 9, low. |
| 5 | AC-25/AC-26 cannot be hosted in any test project (70) | **FIXED** | Split into AC-25a–e and AC-26a–f with named host projects. Verified AWS.Tests, AWS.V4.Tests, MQTT.Tests reference `ServiceActivator`; Gcp.Tests and PostgresSQL.Tests do not — exactly as C-9 states. |
| 6 | OOS-2 "six factories" (68) | **FIXED** | Now "the eleven factories that downcast (C-3)". |
| 7 | FR-1's MsSql ctor signature (68) | **FIXED** | Now `new …MsSql.ChannelFactory(msSqlMessageConsumerFactory)`, matching `MsSql/ChannelFactory.cs:32`. |
| 8 | Doubles unreachable from `Extensions.Tests` (66) | **FIXED** | C-9 now requires its own copies. Verified its `.csproj` references no `Core.Tests`. |
| 9 | NFR-3 "the FR-9 sweep" (65) | **FIXED** | Now "The FR-12 sweep is reflection-only." |
| 10 | Two ACs use undefined doubles (65) | **FIXED** | `FakeOtherSubscription` and the `AlphaBus`/`BetaBus` pair are now in C-9's table and the AC preamble's closed set. |
| 11 | AC-28 requires a defective shipped gateway (65) | **FIXED** | FR-12 mandates a pure predicate over `(Type, Type)`; AC-28 restated over synthetic test-assembly types. |
| 12 | FR-5 undercounts same-named factories (62) | **FIXED** | Now eight. Verified exactly eight `class ChannelFactory` declarations in `src/`. |
| 13 | Sweep placement unenumerated (60) | **FIXED** | Twelve-project table added; AC-27 requires one sweep per project. Verified all twelve exist with those names. |
| 14 | NFR-5 scoped to FR-7/FR-8 (55) | **FIXED** | Now "FR-7 to FR-11". |
| 15 | NFR-6 circular "genuinely correct" (50) | **FIXED** | Qualifier dropped and the drop explained. (New exception count is wrong — finding 3.) |

Thirteen of fifteen fixed; #1 and #2 are re-opened below as findings 2 and 1.

## Findings

### 1. C-9 forbids `TestRequest`, which five acceptance criteria and four FR examples require — and its supporting facts are false (Score: 90)

The round-2 fix for `GreetingMade` was applied as a region-wide rename. It rewrote not only the ACs but also C-9's *prohibition* sentence, which now bans the replacement token. The document simultaneously mandates and forbids `TestRequest`, and the justification C-9 gives for the ban is factually untrue of it.

**Evidence**: C-9, "Request types": "**No acceptance criterion may use `TestRequest`**: that type exists only under `samples/WebAPI/WebAPI_Dynamo` in namespaces `GreetingsApp.Requests` / `SalutationApp.Requests`, is not visible to any test project, and the namespace `Greetings.Ports.Events` contains `GreetingEvent`, not `TestRequest`."

Against this, the AC preamble: "`TestRequest` in the transport-correction criteria means a request type local to that gateway test project." And AC-20 `GcpPubSubSubscription<TestRequest>`, AC-21 `MqttSubscription<TestRequest>`, AC-22/AC-23 `SqsSubscription<TestRequest>`, AC-24 `PostgresSubscription<TestRequest>` — plus the examples in FR-1, FR-2, FR-3, FR-5 and FR-7 to FR-11.

Verified: `grep -rn "class TestRequest"` over `src`, `samples` and `tests` returns **nothing** — no type named `TestRequest` exists anywhere in the repository. `GreetingMade` is present in `WebAPI_Dynamo`, `WebAPI_Dapper` and `WebAPI_EFCore`, so even the original claim's "only under WebAPI_Dynamo" was wrong. Every clause of C-9's sentence is false of `TestRequest`.

**Recommendation**: Restore the sentence to its subject: "**No acceptance criterion may use `GreetingMade`**: that type exists only under `samples/WebAPI/*` (`WebAPI_Dynamo`, `WebAPI_Dapper`, `WebAPI_EFCore`) in namespaces `GreetingsApp.Requests` / `SalutationApp.Requests` and is not visible to any test project." Then add the positive rule the preamble already implies: "`TestRequest` in AC-20 to AC-26f is a placeholder for a request type declared locally in that gateway test project, one per file." Audit the FR examples for the same corruption.

---

### 2. AC-13 and AC-13a cannot both pass: the pinned remedy still advises configuring an `InMemoryChannelFactory` (Score: 80)

Round 2's finding 1 was answered by making the remedy asymmetric — but the asymmetric template *keeps* the `{D}` half. In the feature's headline case `D == typeof(InMemoryChannelFactory)`, so the message AC-13 pins still contains, verbatim, advice to configure an in-memory channel factory. AC-13a then asserts the message does not give that advice.

**Evidence**: FR-5, "Remedy clause (normative template)": `— either configure a channel factory of type {D-display-name}, or use a subscription type whose ChannelFactoryType is {F-display-names}`. AC-13: "the `Message` **ends with** the literal … where `{D}` is the display name of the declared channel factory type". AC-1's configuration (used by both AC-13 and AC-13a) is a plain `Subscription<FakeChannelFactoryRequest>`, so `D = typeof(InMemoryChannelFactory)`. Rendered: `— either configure a channel factory of type Paramore.Brighter.InMemoryChannelFactory, or use a subscription type whose ChannelFactoryType is …FakeTransportChannelFactory`.

AC-13a: "the message does **not** advise the developer to configure an `InMemoryChannelFactory` as the way to make the subscription work." A developer asserting `DoesNotContain("configure a channel factory of type Paramore.Brighter.InMemoryChannelFactory")` fails AC-13a; one asserting only the `{F}` half passes. FR-5's own rationale compounds it: it says a `{D}`-only template "would advise the developer to configure an in-memory channel factory, which is precisely the silent-wrong-bus outcome C-2 exists to prevent" — the new template still does so, in its first clause.

**Recommendation**: Either (a) restate AC-13a to what the template actually delivers — "the `{F}` half names `FakeTransportChannelFactory`, and the in-memory option is not the *only* remedy offered" — or (b) suppress the `{D}` half when `D == typeof(InMemoryChannelFactory)` and pin that variant as a second normative template, with AC-13 scoped to the non-in-memory case. (b) honours FR-5's stated rationale and C-2.

---

### 3. NFR-6's "exactly two exceptions" is falsified by a third, in-repo case: a plain `Subscription<T>` with MQTT's non-downcasting `ChannelFactory` (Score: 70)

NFR-6 deliberately dropped the "genuinely correct" qualifier to make itself falsifiable. It is now falsifiable, and false: there is a third class of configuration that starts and works correctly today and becomes an `Error` after this change. It involves only shipped Brighter types, so neither C-2 (transport subscription → in-memory factory) nor C-10 (out-of-repo subscription subclasses) covers it.

**Evidence**: NFR-6 — "No configuration that starts successfully today may be made to fail by this feature, with exactly **two** deliberate, documented exceptions: **C-2** … and **C-10** …". C-3 itself establishes the counter-example: "MQTT's `ChannelFactory` (`MQTT/ChannelFactory.cs:32-73`) passes the subscription through untouched."

Verified: `MQTT/ChannelFactory.cs:61-99` builds channels from `subscription.ChannelName`, `subscription.RoutingKey` and `subscription.BufferSize` only, and `MqttMessageConsumerFactory.Create` (`:63-68`) uses `as IUseBrighterDeadLetterSupport` / `as IUseBrighterInvalidMessageSupport` — null-tolerant casts — with all broker configuration coming from `MqttMessagingGatewayConsumerConfiguration`. A `Subscription<T>` handed the MQTT `ChannelFactory` therefore consumes MQTT correctly today. Under FR-3's direct arm, `typeof(InMemoryChannelFactory).IsAssignableFrom(typeof(MQTT.ChannelFactory))` is false → `Error` → host refused under the default `throwOnError: true`.

**Recommendation**: Either make it a documented third exception (a sibling constraint to C-2/C-10 noting MQTT is the one transport whose factory accepts any `Subscription`, and that such configurations must switch to `MqttSubscription<T>`), or restate NFR-6 as "with the documented exceptions in C-2, C-10 and C-3's MQTT note" and add the MQTT case to C-8's release-note obligations. Do not leave the count at two.

---

### 4. AC-20 and AC-24's Givens construct subscriptions that throw `ConfigurationException` (Score: 68)

Round 2's finding 10 fixed exactly this shape for AC-19. The same defect is present, unfixed, in two of the five transport-correction criteria.

**Evidence**: AC-20 — "Given a `GcpPubSubSubscription<TestRequest>`, When `ChannelFactoryType` is read". AC-24 — "Given a `PostgresSubscription<TestRequest>`, …". Verified:
- `GcpPubSubSubscription<T>` (`GcpPubSubSubscription.cs:164-180`) — `MessagePumpType messagePumpType = MessagePumpType.Unknown`, and `subscriptionName`, `channelName`, `routingKey` are **required positional** parameters.
- `PostgresSubscription<T>` (`PostgresSubscription.cs:146-158`) — `MessagePumpType messagePumpType = MessagePumpType.Unknown`.
- `Subscription.cs:213-214`: `if (messagePumpType == MessagePumpType.Unknown) throw new ConfigurationException(...)`.

An implementer following either Given literally gets a `ConfigurationException` before reading the property. (AC-21 MQTT and AC-22/AC-23 AWS are safe — those generic ctors default to `Proactor`.)

**Recommendation**: Give AC-20 and AC-24 complete Givens, e.g. "Given `new GcpPubSubSubscription<TestRequest>(new SubscriptionName("t"), new ChannelName("t"), new RoutingKey("t"), messagePumpType: MessagePumpType.Proactor)`", noting the required arguments differ per transport. Alternatively restate AC-20 to AC-24 against FR-12's construction-free mechanism, which AC-29 already mandates for the sweep.

---

### 5. C-9's placement range "AC-20 to AC-26e" omits AC-26f (Score: 62)

**Evidence**: C-9, "Real-type tests" — "The five correction criteria (**AC-20 to AC-26e**) and the FR-12 sweep (AC-27) name real gateway types and live in the gateway test project for that assembly, reflection-only, requiring no infrastructure (NFR-3)." AC-26f exists as its own numbered criterion and states its own host projects. Nothing in C-9 covers it. The AC-section header has the same gap: "Each of AC-20 to **AC-26** lives in the corresponding gateway test project, **is reflection-only**" — AC-26f is not reflection-only; it evaluates the rule.

Secondary: "The **five** correction criteria (AC-20 to AC-26e)" miscounts — that range spans sixteen criteria.

**Recommendation**: Change the endpoint to "AC-20 to AC-26f", reword "five correction criteria" to "the transport-correction criteria", and amend the AC-section header to "reflection-only except AC-26f, which evaluates the rule against constructed instances and still touches no infrastructure".

---

### 6. FR-12's mechanism rationale makes a false claim about transport subscription constructors (Score: 62)

FR-12's normative "Mechanism" paragraph justifies `GetUninitializedObject` with a blanket statement about every transport subscription. It is wrong for at least six of the twelve families. The chosen mechanism is still right; the stated reason is not the real reason.

**Evidence**: FR-12 — "**every** transport subscription defaults `messagePumpType` to `Unknown` with `requestType` null, so `Activator.CreateInstance` cannot be used."

Verified defaults: `RMQ.Sync/RmqSubscription.cs:107` and `:166` — `Reactor`; `AWSSQS/SqsSubscription.cs:201`, `Redis/RedisSubscription.cs:125`, `AzureServiceBus/AzureServiceBusSubscription.cs:125`, `RMQ.Async/RmqSubscription.cs:177`, `MQTT/MqttSubscription.cs:131` — `Proactor`. Further, every generic `XSubscription<T>` passes `typeof(T)` to the base as `requestType`, so it is not null for the generic forms. The actual reason `Activator.CreateInstance(Type)` fails is simpler and universally true: none of these types has a real parameterless constructor — all-optional parameters do not produce one.

**Recommendation**: "No shipped subscription type declares a parameterless constructor (all-optional parameters do not create one), so `Activator.CreateInstance(Type)` throws `MissingMethodException`; and several transports' non-generic constructors additionally default `messagePumpType` to `Unknown`, which `Subscription.cs:213` rejects."

---

### 7. FR-6's "no evaluation when validation is disabled" has no acceptance criterion (Score: 62)

**Evidence**: FR-6 — "The rule MUST be evaluated only when `ValidatePipelines()` is enabled and MUST add no startup work when it is not." Its example states the behaviour, but examples are not ACs. The three ACs mapped to FR-6 are AC-16 (`throwOnError: true`), AC-17 (`throwOnError: false`) and AC-18 (existing rules unaffected) — none covers `enabled: false`. NFR-4 depends on the clause; so do C-2 and C-10's third obligation. Verified assertable: `BrighterPipelineValidationExtensions.cs:58-60` — `if (!enabled) return builder;`.

**Recommendation**: Add "**AC-17a** (FR-6, NFR-4) — Given the mismatched configuration of AC-16 with `ValidatePipelines(enabled: false)`, When the host starts, Then the host starts successfully and no validation results are produced by any rule."

---

### 8. The AC preamble cites C-9 for a rule C-9 no longer contains (Score: 55)

**Evidence**: AC preamble, final sentence: "No criterion uses `GreetingMade` (**C-9**)." C-9 does not mention `GreetingMade` anywhere — the rename replaced that token with `TestRequest` (finding 1).

**Recommendation**: Fix in the same edit as finding 1; once C-9's prohibition names `GreetingMade` again, the cite is valid.

---

### 9. C-10's fourth bullet overstates what already throws today (Score: 50)

**Evidence**: C-10 — "Any such subscription used with a `CombinedChannelFactory` **already throws** … today, so it is not a new failure." That holds only when no inner factory is exactly an `InMemoryChannelFactory`. `CombinedChannelFactory.cs:34` matches on exact type equality, so a combined factory containing an `InMemoryChannelFactory` routes such a subscription successfully today. (C-10's conclusion survives — the rule's combined arm also finds a match — but the stated reason is wrong.)

**Recommendation**: "…already throws today unless one of the inner factories is exactly an `InMemoryChannelFactory`, in which case both the runtime and the rule route it successfully; either way it is not a new failure."

---

### 10. AC-13's `{F}` definition conflicts with FR-5's combined-arm definition (Score: 45)

**Evidence**: AC-13 — "`{F}` the display name of the **effective factory**." FR-5 — "`{F-display-names}` is the effective factory's display name in the direct arm, **or the comma-separated display names of the inner factories in constructor order in the combined arm**." In the combined arm the effective factory is the `CombinedChannelFactory`, which AC-7 forbids naming. AC-13's scenario is direct-arm so nothing breaks, but the definitions read as contradictory, and the combined-arm rendering is ungrammatical: "use a subscription type whose ChannelFactoryType is A, B".

**Recommendation**: In AC-13, say "`{F}` as defined in FR-5 (here, the direct arm)". Give FR-5 a distinct combined-arm phrasing: "…whose `ChannelFactoryType` is one of: A, B".

---

### 11. AC-30 does not say which doubles compose its arrangement (Score: 45)

**Evidence**: AC-30 — "Given two mismatched subscriptions `sub-a` and `sub-b` … evaluated against a `CombinedChannelFactory` with **three inner factories**." C-9 declares the double set closed, and only five factory doubles exist; making both subscriptions mismatch requires the three inner factories to exclude `FakeTransportChannelFactory` and `FakeOtherChannelFactory` exactly.

**Recommendation**: Name the arrangement: "`sub-a` a `FakeTransportSubscription`, `sub-b` a `FakeOtherSubscription`, and inner factories `[FakeDerivedChannelFactory, AlphaBus.ChannelFactory, BetaBus.ChannelFactory]`".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 6 |
| 0-49 (Low) | 2 |

**Total findings**: 11
**Findings at or above threshold (60)**: 7
