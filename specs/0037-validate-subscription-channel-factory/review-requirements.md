# Review: requirements — 0037-validate-subscription-channel-factory (round 2)

**Date**: 2026-09-13
**Threshold**: 60
**Verdict**: NEEDS WORK

13 findings at or above threshold 60. Address these before approving.

## Round 1 disposition

| # | Prior finding | Status | Justification |
|---|---|---|---|
| 1 | Three transports with no override (AWSSQS, AWSSQS.V4, Postgres) | **FIXED** | D2 widened; FR-9/FR-10/FR-11 added; Table 1 now says "nine of twelve … three declare none"; FR-12 condition 2 catches the inherited default; C-1's "only known class" claim corrected. Verified: exactly twelve gateway subscription families, nine overrides, those three with none. |
| 2 | AC-17/AC-19 needed a broker | **FIXED** | Replaced by AC-26, which asserts the routing *decision* (`f.GetType() == subscription.ChannelFactoryType`) with an explicit note that channel creation is forbidden. |
| 3 | C-9 places unit tests where the types are unreachable | **PARTIALLY FIXED** | Core.Tests' seven references verified exactly as C-9 states, and the doubles approach fixes AC-1..AC-15. But new placement defects appear — see findings 5 and 8. |
| 4 | C-3 downcast claim wrong | **FIXED** | Verified all twelve factories with both idioms plus `Cast`/explicit-cast forms: eleven throw `ConfigurationException`, only `MQTT/ChannelFactory.cs` passes the subscription through. Cited throw sites all correct. |
| 5 | Sweep mechanism under-specified / would throw | **FIXED** | FR-12 now pins `RuntimeHelpers.GetUninitializedObject`; AC-29 pins "constructs nothing". Verified viable: every one of the nine overrides is an expression-bodied `=> typeof(X)` constant touching no instance state, and tests target net9.0/net10.0 where the API exists. |
| 6 | No AC for FR-5 items 1 and 4 | **FIXED** | AC-12 (item 1) and AC-13 (item 4, pinned literal) added. (Their content has separate problems — findings 1 and 2.) |
| 7 | MsSql citations stale | **FIXED** | Verified `:53`, `:75`, `:99` and the `msSqlSubscription` variable name. |
| 8 | Constructor examples do not compile | **PARTIALLY FIXED** | The MQTT one is gone; FR-1's `MsSql.ChannelFactory(connection)` remains wrong. Re-opened as finding 7. |
| 9 | `Type.FullName` unworkable for generics; AC-11 untestable clause | **FIXED** | Normative display-name format added; AC-14 makes it assertable. (AC-15's replacement clause creates a new contradiction — finding 3.) |
| 10 | Null-`RequestType` AC's Given incomplete | **FIXED** | AC-19 now supplies `getRequestType:` and `messagePumpType: MessagePumpType.Proactor`, matching the base ctor's two guards (verified at `Subscription.cs:212-216`). |
| 11 | `RocketMqSubscription` non-generic does not exist | **FIXED** | Table 1 row is now `RocketSubscription` (`RocketMqSubscription.cs:50`); FR-12 scope names the base/derived pair. |
| 12 | `DispatchBuilder` mutation not addressed | **FIXED** | FR-2a added with the verified `DispatchBuilder.cs:146-148` quote; AC-5 asserts the invariance. |
| 13 | Grounding line refs off | **FIXED** | Verified `ServiceCollectionExtensions.cs:199-229` and `CombinedChannelFactory.cs:14`. Also spot-verified `:159`, `Subscription.cs:48/:172`, `IAmConsumerOptions.cs:12`, `Specification.cs:35`, `ValidationError.cs:33`, `BrighterPipelineValidationExtensions.cs:58`, `GcpPubSubChannelFactory.cs:14`, `GcpPubSubConsumerFactory.cs:15`, `MqttMessageConsumerFactory.cs:30`, MQTT `ChannelFactory.cs:33`, AWSSQS/V4 `ChannelFactory.cs:44`, `PostgresChannelFactory.cs:11`, MultiBus `Program.cs:83` — all correct. |
| 14 | NFR-1 mis-describes existing rules | **FIXED** | Now says three use the predicate/error-factory form and `UnwrapTransformResolvable` (`:142-167`, verified) uses `DisposingSpecification`. |

Twelve of fourteen are genuinely fixed; #3 and #8 are re-opened below.

## Findings

### 1. The pinned remedy template gives actively wrong advice in the feature's headline case (Score: 78)

FR-5 item 4 is now a *normative literal*, and AC-13 pins it. But the template's only variable is `{D}` — the *declared* type — so in the direct arm the remedy always tells the developer to make the world match the subscription, never the other way round. In the flagship scenario (issue #4331, FR-1's own first example, AC-1, AC-12, AC-13) `D` is `InMemoryChannelFactory`, so the message a developer sees is advice to configure an in-memory channel factory — which is precisely the silent-wrong-bus outcome C-2 declares "a worse failure than an exception".

**Evidence**: FR-5, "Remedy clause (normative template)": `— use a subscription whose ChannelFactoryType is {D-display-name}, or configure a channel factory of type {D-display-name}`. AC-1's configuration is a plain `Subscription<GreetingMade>` (`D = typeof(InMemoryChannelFactory)`) handed a transport factory. AC-13: "the `Message` **ends with** the literal … where `{D}` is the display name of the declared channel factory type." Rendered: `— use a subscription whose ChannelFactoryType is Paramore.Brighter.InMemoryChannelFactory, or configure a channel factory of type Paramore.Brighter.InMemoryChannelFactory`. NFR-2 requires each message "state a remedy"; the issue's own illustrative message was the opposite direction ("use `MsSqlSubscription<T>`"), and OOS-6 excludes computing that — but OOS-6 excludes *naming the subscription type*, not naming the *effective factory* the developer must satisfy.

**Recommendation**: Make the remedy asymmetric and factory-aware, e.g. `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is {F}` (where `{F}` is the effective factory's display name, or the inner-factory list in the combined arm). Pin the new literal in FR-5 and AC-13, and add an AC asserting the remedy names the *effective* factory in the `D == InMemoryChannelFactory` case.

---

### 2. AC-12 asserts a literal that cannot be produced anywhere in the repository (Score: 75)

AC-12's expected string embeds a namespace/type pair that does not exist, and cannot exist in the project C-9 assigns the test to.

**Evidence**: AC-12 — "Then the `Message` contains the display name of the subscription's runtime type, `Paramore.Brighter.Subscription<Greetings.Ports.Events.GreetingMade>`." Verified: `class GreetingMade` exists only under `samples/WebAPI/WebAPI_Dynamo/*` in namespaces `GreetingsApp.Requests` and `SalutationApp.Requests`; the namespace `Greetings.Ports.Events` exists only in `samples/TaskQueue/ASBTaskQueue` and `samples/TaskQueue/RedisTaskQueue`, and contains `GreetingEvent`/`GreetingAsyncEvent`, not `GreetingMade`. The combination appears nowhere. Worse, `GreetingMade` does not exist in `tests/Paramore.Brighter.Core.Tests` at all, and the AC preamble plus C-9 place AC-12 in `tests/Paramore.Brighter.Core.Tests/Validation/`, whose seven project references include no samples. The same phantom type is used as the request type in AC-1, AC-7, AC-11 and AC-20 to AC-24, and C-9 additionally requires "each with its own distinct request type … to avoid assembly-scan collisions", which `GreetingMade` shared across every AC would violate.

**Recommendation**: Replace `GreetingMade` throughout with a double the spec defines (e.g. `FakeChannelFactoryRequest` in `Paramore.Brighter.Core.Tests.Validation.TestDoubles`), and restate AC-12's expected literal in terms of that type's actual namespace — or state it as a *shape* (`Paramore.Brighter.Subscription<{namespace}.{TRequest}>`) rather than a fixed string. Add the request-type doubles to C-9's list.

---

### 3. AC-15's negative assertion contradicts FR-5's own mandated remedy literal (Score: 72)

AC-15 adds a blanket assertion about every occurrence of the token `ChannelFactory` in the message. FR-5's remedy template — which AC-13 requires the message to *end with* — contains `ChannelFactoryType`, in which `ChannelFactory` is preceded by a space. The two ACs cannot both pass under a straightforward substring implementation.

**Evidence**: AC-15 — "every occurrence of the token `ChannelFactory` in the message is immediately preceded by a `.` (i.e. no bare, unqualified `ChannelFactory` appears)." FR-5 — `— use a subscription whose ChannelFactoryType is {D-display-name}, …`. `"… whose ChannelFactoryType is …".IndexOf("ChannelFactory")` lands on a character preceded by `' '`. Two developers will resolve this differently (one strips the remedy clause before asserting; one uses a word-boundary regex; one treats it as a genuine failure), which is exactly the divergence the review criteria call out.

**Recommendation**: Restate AC-15 with a word boundary and an explicit carve-out, e.g. "no occurrence of `ChannelFactory` that is neither preceded by `.` nor part of the token `ChannelFactoryType`", or scope the assertion to the portion of the message before the remedy clause.

---

### 4. The Error verdict for user-defined `Subscription` subclasses with no override is never addressed, and NFR-6 asserts the opposite (Score: 70)

D2 removes the *in-repo* false positives, and C-1 is careful to hedge ("every class of in-repo false positive known at the time of writing"). NFR-6 is not hedged, and neither is FR-12's guard, which sweeps only shipped gateway assemblies. Any application- or third-party-defined `Subscription` subclass that does not override `ChannelFactoryType` — a custom transport, a community gateway, or a subclass added purely to carry extra configuration and used with a non-downcasting factory — has `D = typeof(InMemoryChannelFactory)` and will be an `Error` that blocks a host running correctly today. That is the identical shape to round 1's Critical finding, in the one population D2 cannot reach, and the document nowhere states it.

**Evidence**: NFR-6 — "No configuration that starts successfully today and is genuinely correct may be made to fail by this feature. **There is exactly one deliberate exception, C-2**". C-2 is the opposite direction only. Verified: `Subscription.ChannelFactoryType` is `public virtual` (`Subscription.cs:172`) with a `typeof(InMemoryChannelFactory)` default, so the default is inherited silently by any subclass anywhere; FR-12's scope is "each shipped messaging-gateway assembly", so no guard exists for out-of-repo subscriptions. FR-3's direct arm then evaluates `typeof(InMemoryChannelFactory).IsAssignableFrom(F.GetType())` → false → Error under the default `throwOnError: true`.

**Recommendation**: Add a constraint (a sibling to C-2) that names this population, states the consequence of D1 for it, and says how it is mitigated — for example that migration notes instruct custom-subscription authors to add the override, and/or that release notes flag it as a breaking startup change for V10.X. Then qualify NFR-6 to "no *in-repo* configuration", or list this as a second acknowledged exception.

---

### 5. AC-25 and AC-26 cannot be hosted in any existing test project (Score: 70)

The AC preamble states "Each of AC-20 to AC-26 lives in the corresponding gateway test project". AC-25 and AC-26 are written across *all five* corrected transports, and AC-26 additionally requires evaluating the rule — which lives in `Paramore.Brighter.ServiceActivator`.

**Evidence**:
- AC-25 — "Given **each of the five corrected subscription types**"; AC-26 — "Given, **for each corrected transport**, a `CombinedChannelFactory` …". Verified: no `tests/*/*.csproj` references more than one of `MessagingGateway.GcpPubSub`, `MessagingGateway.MQTT`, `MessagingGateway.AWSSQS`, `MessagingGateway.AWSSQS.V4`, `MessagingGateway.Postgres`. There is therefore no project in which "each of the five" can be a single test.
- AC-26 — "and **the rule produces no findings** for that subscription." Verified: `tests/Paramore.Brighter.Gcp.Tests` and `tests/Paramore.Brighter.PostgresSQL.Tests` have **no** `Paramore.Brighter.ServiceActivator` project reference (AWS.Tests, AWS.V4.Tests and MQTT.Tests do). Two of the five cannot see `ConsumerValidationRules` at all.

**Recommendation**: Split AC-25 and AC-26 into five per-transport criteria, each stating its host project. For AC-26's second clause, either state that `Paramore.Brighter.ServiceActivator` must be added as a project reference to `Gcp.Tests` and `PostgresSQL.Tests`, or drop the clause there and cover it once in `Core.Tests` with the doubles (the routing-decision clause needs only `Paramore.Brighter`).

---

### 6. OOS-2 still says "six factories that downcast", contradicting C-3's corrected count of eleven (Score: 68)

C-3 was rewritten in this revision from "six do" to "eleven of the twelve"; OOS-2 was not updated with it.

**Evidence**: OOS-2 — "**The six factories that downcast** the subscription keep doing so, with the same exception type and message." C-3 — "**Eleven of the twelve** transport channel factories downcast … only MQTT's `ChannelFactory` does not." Verified C-3 is the correct one: AWSSQS `:98/:208`, AWSSQS.V4 `:98/:208`, ASB `:103`, GCP `:28/:68`, Kafka `:63/:82/:102`, MsSql `:53/:75/:99`, Postgres `:18/:39/:60`, Redis `:63/:82/:96`, RMQ.Async `:64/:86/:109`, RMQ.Sync `:63/:97`, RocketMQ `:15/:30` — eleven; MQTT's `ChannelFactory` has no downcast at all.

**Recommendation**: Change OOS-2 to "the eleven factories that downcast".

---

### 7. FR-1's first example still uses a constructor signature that does not exist (re-opening round 1 finding 8) (Score: 68)

The MQTT half of the prior finding was resolved; the MsSql half was carried through unchanged into the revision, and it now appears in the example that anchors the whole feature.

**Evidence**: FR-1 — "with `options.DefaultChannelFactory = new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(**connection**)`". Verified `MsSql/ChannelFactory.cs:32`: `public ChannelFactory(MsSqlMessageConsumerFactory msSqlMessageConsumerFactory)`. There is no connection-taking overload. FR-3's examples hide the same problem behind `(...)`, which is fine, but FR-1's does not.

**Recommendation**: Change to `new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(msSqlMessageConsumerFactory)` or elide the argument as FR-3 does.

---

### 8. The test doubles are defined in a project `Paramore.Brighter.Extensions.Tests` cannot reference (Score: 66)

C-9 places the doubles in `Core.Tests` and then tells AC-16/AC-17 to reuse them from a different test project. Test projects do not reference one another here.

**Evidence**: C-9 — "Host-start behaviour (AC-16, AC-17) … belongs in `tests/Paramore.Brighter.Extensions.Tests`, which already references that package, **still using the doubles above** rather than a real transport." Verified `tests/Paramore.Brighter.Extensions.Tests/*.csproj` references exactly `Paramore.Brighter.Extensions.DependencyInjection`, `Paramore.Brighter.Outbox.Sqlite`, `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection`, `Paramore.Brighter.ServiceActivator.Extensions.Hosting`, `Paramore.Brighter.Sqlite.EntityFrameworkCore` — no reference to `Paramore.Brighter.Core.Tests`, where C-9 puts the doubles. As written, AC-16/AC-17 cannot compile.

**Recommendation**: State explicitly that `Extensions.Tests` gets its own copies of the doubles (with their own distinct request types, per `.agent_instructions/testing.md`), or that a shared location such as `Paramore.Test.Helpers` hosts them. Duplicating them silently is the outcome an implementer will reach anyway; the spec should say which.

---

### 9. NFR-3 still refers to "the FR-9 sweep" after the renumber (Score: 65)

FR-9 is now the AWSSQS correction; the sweep is FR-12. A reader following the cross-reference lands on a requirement that is not reflection-based at all.

**Evidence**: NFR-3 — "The rule inspects types and configured instances only. **The FR-9 sweep is reflection-only.**" FR-9 — "`SqsSubscription` (AWSSQS) declares its channel factory." FR-12 — "An automated **reflection sweep** MUST assert …". Every other cross-reference to the sweep (C-1, C-9, AC-27/28/29) correctly says FR-12; NFR-3 is the sole survivor.

**Recommendation**: Change to "The FR-12 sweep is reflection-only."

---

### 10. Two acceptance criteria use test doubles the document never defines (Score: 65)

C-9 enumerates exactly four doubles, and the AC preamble repeats that list as the closed set. Two ACs reach outside it.

**Evidence**:
- AC-6 — "two subscriptions — a `FakeTransportSubscription` named `sub-a` and a **`FakeOtherSubscription`** named `sub-b`". C-9's list is `FakeTransportSubscription`, `FakeTransportChannelFactory`, `FakeDerivedChannelFactory`, `FakeOtherChannelFactory`. There is no `FakeOtherSubscription` anywhere in the document; without it AC-6 (the "no false positives in multi-bus" criterion, and FR-13's only combined-arm positive case) cannot be arranged.
- AC-15 — "a subscription declaring a channel factory type **named `ChannelFactory` in one namespace**, handed a factory whose type is also **named `ChannelFactory` in a different namespace**". Since AC-15 names no real gateway type, the preamble puts it in `Core.Tests` against the doubles — but two doubles both named `ChannelFactory` in different namespaces are not in C-9's list either.

**Recommendation**: Add `FakeOtherSubscription` (declaring `typeof(FakeOtherChannelFactory)`) and the AC-15 pair (e.g. `…TestDoubles.AlphaBus.ChannelFactory` / `…TestDoubles.BetaBus.ChannelFactory`, plus a subscription declaring the former) to C-9's list and to the AC preamble.

---

### 11. AC-28 requires a shipped gateway assembly to contain the defects AC-27 forbids (Score: 65)

The guard's negative test is specified against real gateway assemblies, which makes it self-contradictory with the positive test.

**Evidence**: AC-28 — "Given **a gateway assembly containing** (a) a non-abstract `Subscription` subclass with no `ChannelFactoryType` override, and (b) one overriding it with a type that does not implement `IAmAChannelFactory`, When the sweep runs, Then it fails for both". AC-27 — "Given every non-abstract type assignable to `Subscription` in **each shipped messaging-gateway assembly** … Then the type implements `IAmAChannelFactory` and is not `typeof(InMemoryChannelFactory)`, **for every type found**." If any shipped gateway assembly contained (a) or (b), AC-27 would be red. FR-12's own example has the same framing. Neither says the sweep's predicate must be extracted and exercised over synthetic types in a test assembly, which is the only way both ACs can be green.

**Recommendation**: Require FR-12 to expose the check as a pure predicate over `(Type subscriptionType, Type declaredFactoryType)`, and restate AC-28 as "Given two synthetic `Subscription` subclasses in the test assembly — one with no override, one overriding with a non-`IAmAChannelFactory` type — When the predicate is applied …".

---

### 12. FR-5's premise undercounts the same-named factories by three (Score: 62)

FR-5's rationale for fully-qualified names names five assemblies; there are eight, including two of the transports this very specification is correcting.

**Evidence**: FR-5 — "**Five transports** name their channel factory class `ChannelFactory` in five different assemblies (Redis, RMQ.Sync, RMQ.Async, Kafka, MsSql)." Verified `public [partial] class ChannelFactory` in **eight** assemblies: `MessagingGateway.AWSSQS` (`:44`), `MessagingGateway.AWSSQS.V4` (`:44`), `MessagingGateway.Kafka` (`:32`), `MessagingGateway.MQTT` (`:33`), `MessagingGateway.MsSql` (`:12`), `MessagingGateway.Redis` (`:33`), `MessagingGateway.RMQ.Async` (`:33`), `MessagingGateway.RMQ.Sync` (`:32`). The document elsewhere depends on the AWS and MQTT ones being called `ChannelFactory` (Table 1, FR-8, FR-9, FR-10), so this is an internal inconsistency, not only a miscount — and it understates the case FR-5 is making.

**Recommendation**: "Eight transports name their channel factory class `ChannelFactory` in eight different assemblies (AWSSQS, AWSSQS.V4, Kafka, MQTT, MsSql, Redis, RMQ.Sync, RMQ.Async)."

---

### 13. FR-12 requires a sweep over twelve assemblies but nothing says where it runs or that all twelve are covered (Score: 60)

FR-12 and AC-27 are written as a single sweep over "each shipped messaging-gateway assembly", while C-9 disperses the real-type tests into gateway test projects with an unenumerated "and the rest".

**Evidence**: FR-12 — "for every non-abstract type assignable to `Subscription` in **each shipped messaging-gateway assembly**". C-9 — "live in the corresponding gateway test projects (`Paramore.Brighter.Gcp.Tests`, `Paramore.Brighter.MQTT.Tests`, `Paramore.Brighter.AWS.Tests`, `Paramore.Brighter.AWS.V4.Tests`, `Paramore.Brighter.PostgresSQL.Tests` **and the rest**)". Verified: a per-project test can only sweep the assemblies its project references, and no project references more than one of the corrected five; there are twelve gateway assemblies and twelve corresponding test projects (`AzureServiceBus.Tests`, `Kafka.Tests`, `MSSQL.Tests`, `Redis.Tests`, `RMQ.Async.Tests`, `RMQ.Sync.Tests`, `RocketMQ.Tests` in addition to the five named). Nothing requires a sweep in each of the twelve, so a compliant implementation could add it to one project and leave eleven assemblies unguarded — defeating FR-12's stated purpose.

**Recommendation**: Enumerate the twelve host projects in C-9, or make AC-27 explicit: "one sweep test per gateway test project, covering all twelve shipped gateway assemblies", and state how a newly added thirteenth gateway is forced to acquire one.

---

### 14. NFR-5 still scopes the API-change allowance to FR-7/FR-8 only (Score: 55)

Dangling pre-renumber reference. FR-9, FR-10 and FR-11 also change the public observable value of a virtual property on shipped public classes.

**Evidence**: NFR-5 — "No public API change to existing abstractions **beyond what FR-7/FR-8 require**." Compare C-8, updated correctly: "**FR-7 to FR-11** change the value returned by a public virtual property (or add the override where none existed)", and NFR-6, likewise "FR-7 to FR-11".

**Recommendation**: "beyond what FR-7 to FR-11 require."

---

### 15. NFR-6 names an exception and then denies it is one (Score: 50)

The reworded NFR-6 is circular: it protects only configurations that are "genuinely correct", then declares its single exception to be not genuinely correct — so the exception is not an exception, and the requirement becomes unfalsifiable because the specification itself decides what counts as correct.

**Evidence**: NFR-6 — "No configuration that starts successfully today and **is genuinely correct** may be made to fail by this feature. There is exactly one deliberate exception, C-2 …, which this specification **classifies as *not* genuinely correct** — it silently consumes from the wrong bus."

**Recommendation**: Drop the "genuinely correct" qualifier and make the exception real: "No configuration that starts successfully today may be made to fail, with one deliberate exception: C-2." That is a testable statement; the current wording is not.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 5 |
| 50-69 (Medium) | 10 |
| 0-49 (Low) | 0 |

**Total findings**: 15
**Findings at or above threshold (60)**: 13
