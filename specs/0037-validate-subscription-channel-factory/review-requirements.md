# Review: requirements — 0037-validate-subscription-channel-factory

**Date**: 2026-09-13
**Threshold**: 60
**Verdict**: NEEDS WORK

10 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Three in-repo transports do not override `ChannelFactoryType` at all — the rule turns their *correct* configurations into startup-blocking Errors (Score: 95)

The document's model is stated as "`Subscription.ChannelFactoryType` … is a `virtual` property, defaulting to `typeof(InMemoryChannelFactory)`, that **each transport's subscription overrides**", and Table 1 enumerates "nine `override Type ChannelFactoryType` declarations in `src/`". The count of overrides is right, but the model is wrong: there are **twelve** gateway subscription families in `src/`, and three of them declare **no override**, so they inherit `typeof(InMemoryChannelFactory)`:

- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsSubscription.cs`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsSubscription.cs`
- `src/Paramore.Brighter.MessagingGateway.Postgres/PostgresSubscription.cs`

Consequence under FR-3's direct arm: a correct, working AWS SQS consumer (`options.DefaultChannelFactory = new Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory(...)`, subscriptions of type `SqsSubscription<T>`) has `D = typeof(InMemoryChannelFactory)` and `F = AWSSQS.ChannelFactory`. `typeof(InMemoryChannelFactory).IsAssignableFrom(typeof(AWSSQS.ChannelFactory))` is `false`, so the rule emits `ValidationSeverity.Error` and, under the default `throwOnError: true`, **prevents a working host from starting**. Same for Postgres. These are exactly the shape of AC-1 — the spec's headline positive case — applied to a configuration that runs fine today.

This is not D1 or D2 being re-argued; it is a consequence of D1 the document fails to state, and it directly contradicts two of its own clauses:

**Evidence**:
- NFR-6: "No configuration that starts successfully today and is genuinely correct may be made to fail by this feature. The known exception is discussed in C-2." C-2 covers only the *opposite* direction (transport subscription → in-memory factory). It does not cover "subscription that silently declares in-memory because its transport forgot the override → real transport factory".
- C-1: "It is safe to do so precisely because D2 (FR-7, FR-8) removes **the only known class of in-repo false positive**." That claim is false — AWS SQS (both versions) and Postgres are three further classes, affecting more in-repo samples than GCP/MQTT (`samples/TaskQueue/AWSTaskQueue/…`, `samples/TaskQueue/PostgresTaskQueue/…`, `samples/Scheduler/AwsTaskQueue/…`, and four `samples/Transforms/AWS*/…`).
- FR-9/AC-20 ("each of the nine subscription types in Table 1") and AC-21's sweep ("enumerates every non-abstract type … **that declares (or inherits an override of) `ChannelFactoryType`**") both pass vacuously for AWSSQS and Postgres, so the regression guard as specified does not catch the gap either.
- The same three transports are also unroutable by `CombinedChannelFactory` today for the same reason the document identifies for GCP/MQTT — so the Proposed Solution's claim that "Two long-standing transport defects are fixed" undercounts by three.

**Recommendation**: Add an FR (a sibling to FR-7/FR-8) that adds `ChannelFactoryType` overrides to `SqsSubscription` (AWSSQS), `SqsSubscription` (AWSSQS.V4) and `PostgresSubscription`; or, if the maintainer will not widen D2's scope, add an explicit FR requiring the rule to *not* report an Error when `D == typeof(InMemoryChannelFactory)` and the subscription's runtime type is not `Subscription`/`Subscription<T>`. Correct the Table 1 preamble to say "nine of twelve transports override…", replace AC-20's fixed list, and rewrite AC-21's sweep predicate to enumerate every non-abstract `Subscription` subclass in a gateway assembly (not only those that override), asserting the declared type implements `IAmAChannelFactory` **and** is not the inherited `InMemoryChannelFactory` default. Correct C-1's "only known class of in-repo false positive" claim.

---

### 2. AC-17 and AC-19 cannot be satisfied without a broker, contradicting NFR-3 and AC-23 (Score: 85)

Both ACs require, as a second clause, that the `CombinedChannelFactory` actually *select* and use the transport factory. Selecting it means calling `CreateSyncChannel`/`CreateAsyncChannel`, which in both transports opens a real connection during construction.

**Evidence**:
- AC-19: "*and the combined factory selects the MQTT `ChannelFactory` for that subscription rather than throwing `ConfigurationException`*". `MQTT/ChannelFactory.cs:63-73` calls `_consumerFactory.Create(subscription)`, and `MQTT/MqttMessageConsumer.cs:117-120` ends its constructor with `Task connectTask = Connect(configuration.ConnectionAttempts); connectTask.GetAwaiter().GetResult();` — a blocking TCP connect to the broker.
- AC-17: "*and the combined factory selects the `GcpPubSubChannelFactory` for that subscription*". `GcpPubSub/GcpPubSubChannelFactory.cs:34-40` calls `await EnsureSubscriptionExistsAsync(pullSubscription)` — a Pub/Sub admin API call.
- NFR-3: "Neither the rule **nor any test introduced by this feature** may open a connection to a broker, a database, or any external service." AC-23: "*Given the full set of tests introduced by this feature, When they run in an environment with no broker, database, or network access, Then they all pass.*"

(AC-8's companion assertion is fine by contrast — `CombinedChannelFactory` throws at `CombinedChannelFactory.cs:35-38` before it dispatches to any inner factory.)

**Recommendation**: Reduce the second clause of AC-17 and AC-19 to the routing *decision* rather than channel creation — e.g. assert that the inner factory whose `GetType() == subscription.ChannelFactoryType` is found, or assert `CreateSyncChannel` does not throw `ConfigurationException("No channel factory found for subscription …")` using a stub inner factory of the same runtime type. Alternatively delete the second clause; AC-16/AC-18 plus the rule's own behaviour already cover the fix.

---

### 3. C-9 places the rule's unit tests in a project that cannot reference the types AC-1 to AC-15 are written in (Score: 85)

C-9: "The rule's unit tests belong with the existing consumer validation tests (`tests/Paramore.Brighter.Core.Tests/Validation/` …)." That directory exists and uses the `When_…` convention as claimed. But almost every AC is written in terms of concrete gateway types that `Paramore.Brighter.Core.Tests` cannot see, and AC-12/AC-13 are written in terms of `AddConsumers`, which it also cannot see.

**Evidence**: `tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj` lines 7-13 declare exactly seven project references — `Paramore.Brighter`, `Paramore.Brighter.BoxProvisioning`, `Paramore.Brighter.Extensions.DependencyInjection`, `Paramore.Brighter.Mediator`, `Paramore.Brighter.Outbox.Hosting`, `Paramore.Brighter.ServiceActivator`, `Paramore.Brighter.Testing`. There is **no** `MessagingGateway.*` reference and **no** `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` reference. Yet:
- AC-1, AC-2, AC-4, AC-6, AC-7, AC-8, AC-9, AC-15 name `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` and/or `MsSqlSubscription<T>`.
- AC-3, AC-4, AC-5 name `RmqSubscription` (RMQ.Async) and `RMQ.Async.ChannelFactory`.
- AC-11 names `RedisSubscription<T>` and `Kafka.ChannelFactory`.
- AC-12/AC-13 say "*Given a host configured with `AddConsumers` … When the host starts*"; `AddConsumers` is defined at `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:29` and `:78`.

`tests/Paramore.Brighter.Extensions.Tests` does reference `ServiceActivator.Extensions.DependencyInjection` but likewise references no gateway assembly. So no existing project can host the ACs as written.

**Recommendation**: Either (a) rewrite AC-1 to AC-15 against purpose-built test doubles (`FakeTransportSubscription` overriding `ChannelFactoryType`, `FakeTransportChannelFactory : IAmAChannelFactory`, `FakeDerivedChannelFactory : FakeTransportChannelFactory`) so they are genuinely unit-level and live in `Core.Tests`, keeping the real-type versions only where a gateway test project can host them; or (b) state in C-9 which project references must be added, and reconcile that with the repo convention that `Core.Tests` takes no gateway dependency. Also state where AC-12/AC-13 (host-start behaviour) live.

---

### 4. C-3's claim about which channel factories downcast is factually wrong for two of the three it names, and omits a fourth (Score: 82)

C-3 is load-bearing: it is the stated reason "why a mismatch is not *always* an immediate runtime failure today, and hence why D2 … is what makes D1 … safe". Two of its three examples do downcast, and the arithmetic does not add up.

**Evidence**: C-3 states "*Three of the nine channel factories do not downcast the subscription (`AzureServiceBusChannelFactory`, MQTT's `ChannelFactory`, `RocketMqChannelFactory`); the other six do …*"
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusChannelFactory.cs:103-105`: `if (subscription is not AzureServiceBusSubscription azureServiceBusSubscription) { throw new ConfigurationException(...` — it **does** downcast.
- `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMqChannelFactory.cs:15-17` and `:30-32`: `if (subscription is not RocketSubscription) throw new ConfigurationException(...)` — it **does** downcast.
- Only MQTT's `ChannelFactory` genuinely does not (`MQTT/ChannelFactory.cs:32-73` passes the subscription straight through).
- `GcpPubSubChannelFactory` — one of the nine — appears in neither list, and it **does** downcast (`GcpPubSubChannelFactory.cs:28-31`). So the count is "one of nine does not, eight do", not "three do not, six do".

**Root cause of the error**: the claim originated from a grep using the pattern `as [A-Za-z]+Subscription`, which matches the older `subscription as XSubscription` idiom but misses the modern `subscription is not XSubscription` pattern that ASB, RocketMQ and GCP use.

**Recommendation**: Restate C-3 as "eight of the nine transport channel factories downcast the subscription and throw `ConfigurationException`; only MQTT's `ChannelFactory` does not", cite the throw sites, and rewrite the derived rationale accordingly (the "MQTT in particular would be flagged despite 'working'" argument survives; the Azure/RocketMQ part of it does not).

---

### 5. FR-9/AC-21's reflection sweep is under-specified, and the obvious implementation throws before it can read `ChannelFactoryType` (Score: 75)

FR-9 requires the sweep to "obtain" each type's `ChannelFactoryType`. That property is an **instance** virtual property (`Subscription.cs:172`), so an instance is required — and the base `Subscription` constructor actively rejects the arguments a naive sweep would supply.

**Evidence**: `src/Paramore.Brighter/Subscription.cs` base constructor body:
```csharp
if (messagePumpType == MessagePumpType.Unknown)
    throw new ConfigurationException("You must set a message pump type: use Reactor for sync pipelines; use Proactor for async pipelines");

if (requestType is null && getRequestType is null)
    throw new ConfigurationException("You must set a request type or a function to map a message to a request type");
```
Every transport subscription defaults `messagePumpType` to `MessagePumpType.Unknown` and `requestType` to `null`, and requires three positional arguments (`SubscriptionName`, `ChannelName`, `RoutingKey`) with no defaults. So `Activator.CreateInstance(type)` fails, and `Activator.CreateInstance(type, name, channel, key)` throws `ConfigurationException` for every non-generic transport subscription. The sweep must therefore either pick the right optional-parameter positions per type (signatures differ — compare `MqttSubscription`'s 19 parameters with `GcpPubSubSubscription`'s 29), close each generic type against a suitable `IRequest` (noting `GcpPubSubSubscription<T> where T : class, IRequest` vs `MqttSubscription<T> where T : IRequest`), or bypass construction entirely (`RuntimeHelpers.GetUninitializedObject`). FR-9's phrase "instantiates **or otherwise obtains**" leaves all of that to the implementer, and AC-21 adds no constraint.

**Recommendation**: Pin the mechanism in FR-9 (or explicitly delegate it to the ADR and say so), and add to AC-21 a Given that names the construction strategy — e.g. "the sweep obtains `ChannelFactoryType` without invoking a subscription constructor". Add a boundary AC for generic subscription types and for abstract/base pairs (`RocketSubscription` + `RocketMqSubscription<T>` must not double-report).

---

### 6. No acceptance criterion pins FR-5 item 4 (the remedy clause) or FR-5 item 1 (the subscription's own type) (Score: 70)

FR-5 makes four numbered demands on the message. Items 2 and 3 are pinned by AC-1, AC-4, AC-6 and AC-11. Items 1 and 4 are pinned by nothing testable — they appear only in FR-5's own bulleted examples, not in any AC.

**Evidence**: AC-1's Then reads "*… and a `Message` containing the literals `Paramore.Brighter.InMemoryChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`*" — no mention of the subscription type, no mention of a remedy. Meanwhile NFR-2 makes the remedy mandatory ("state a remedy"), and FR-5 item 4 describes it only as prose. Two developers will produce different remedy wording, and neither can fail a test. This is a missing AC for a non-obvious requirement — the remedy is the whole point of the finding message (see the issue's illustrative text, "— use `MsSqlSubscription<T>`", which OOS-6 then explicitly excludes).

**Recommendation**: Add an AC that asserts the message contains the subscription's own `Type.FullName`, and an AC that asserts a specified remedy substring (give the literal template in FR-5), so the message shape is pinned rather than described.

---

### 7. The MsSql `ChannelFactory` line citations are stale and the quoted snippet does not match the code (Score: 70)

The Problem Statement and the Grounding references both cite `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs` "lines 46, 66 and 88" and quote a snippet using the variable name `rmqSubscription`.

**Evidence**: In the current file the downcasts are at lines **53, 75 and 99**, with the throws at 55, 77 and 101, and the variable is named `msSqlSubscription`:
```csharp
MsSqlSubscription? msSqlSubscription = subscription as MsSqlSubscription;
```
Lines 46/66/88 contain XML-doc text. The document has copied the stale citation and the pre-rename snippet verbatim from issue #4334; the code has moved since the issue was filed.

**Recommendation**: Update the three line numbers to 53/75/99 and re-copy the snippet with the current variable name. Consider citing method names rather than line numbers throughout, since the document leans on file:line heavily.

---

### 8. Two of the three constructor examples in the requirements do not compile against the real types (Score: 68)

**Evidence**:
- FR-1's first example: "`options.DefaultChannelFactory = new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(connection)`". The actual signature is `MsSql/ChannelFactory.cs:32`: `public ChannelFactory(MsSqlMessageConsumerFactory msSqlMessageConsumerFactory)`. There is no connection-taking overload.
- AC-19: "`new Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory(configuration)`". The actual signature is `MQTT/ChannelFactory.cs:51`: `public ChannelFactory(MqttMessageConsumerFactory consumerFactory)`.
- AC-17's `new GcpPubSubChannelFactory(connection)` **is** correct (`GcpPubSubChannelFactory.cs:14`), which makes the other two look verified when they are not.

`new SubscriptionName("greeting-sub")` as the first constructor argument is correct (`Subscription.cs:280`).

**Recommendation**: Correct the two constructor examples to take the consumer factories (and show how those are built without a broker, given NFR-3). If the ACs are rewritten against test doubles per finding 3, this resolves itself.

---

### 9. FR-5's `Type.FullName` mandate is unworkable for generic subscriptions, and AC-11's negative assertion is not testable as written (Score: 65)

FR-5 item 1 requires "the subscription's own runtime type, as `Type.FullName`". Every example in the document uses a *generic* subscription, and `Type.FullName` for a closed generic embeds the assembly-qualified type argument.

**Evidence**: `typeof(Subscription<GreetingMade>).FullName` returns something of the shape
```
Paramore.Brighter.Subscription`1[[…GreetingMade, Paramore.Brighter.Core.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]
```
FR-5's own example asserts the message "contain[s] the literal `Paramore.Brighter.Subscription`" — technically satisfied as a substring, but the rendered message will carry a Version/Culture/PublicKeyToken payload that contradicts NFR-2's "Actionable, unambiguous messages" and FR-5's closing sentence "*assembly-qualified names are not required*". FR-5 gives no rule for formatting generic type arguments.

Separately, AC-11's Then ends "*and the message is not satisfied by the bare token `ChannelFactory` appearing twice*". That is a statement about the AC, not an assertion a test can make.

**Recommendation**: Either specify a formatting helper (e.g. "namespace-qualified name with generic arguments rendered as their own namespace-qualified names, no assembly identity") and pin it with an AC over a generic subscription, or narrow FR-5 item 1 to the *generic type definition*'s `FullName`. Rewrite AC-11's last clause as a positive assertion.

---

### 10. AC-15's subscription cannot be constructed the way the AC implies (Score: 60)

AC-15: "*Given a datatype-channel subscription with `RequestType == null` declaring `MsSqlSubscription`'s channel factory type, handed an `InMemoryChannelFactory`*".

**Evidence**: The base `Subscription` constructor rejects that shape unless a `getRequestType` delegate is supplied, and separately throws unless `messagePumpType` is set to `Reactor` or `Proactor`, while `MsSqlSubscription`'s constructor defaults it to `MessagePumpType.Unknown`. Neither precondition is stated in the Given, so the AC as literally written produces a `ConfigurationException` in arrange, not a `ValidationError` in assert. (C-7's underlying requirement is sound; it is the AC's Given that is incomplete.)

**Recommendation**: Add the missing Given conditions — "constructed with `getRequestType:` a mapping function and `messagePumpType: MessagePumpType.Proactor`, so `RequestType` is null" — so the AC arranges successfully.

---

### 11. Table 1 and AC-20 name a type that does not exist in non-generic form (Score: 58)

Table 1's first row and AC-20's list both name `RocketMqSubscription` as one of "the nine subscription types" whose `ChannelFactoryType` is read.

**Evidence**: `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMqSubscription.cs:10` declares `public class RocketSubscription : Subscription, …` and the override at `:50` lives on `RocketSubscription`. `RocketMqSubscription` exists only as the generic `public class RocketMqSubscription<T> : RocketSubscription` (`:117`). `RocketMqChannelFactory.cs:15` and `:30` likewise downcast to `RocketSubscription`.

**Recommendation**: Rename the Table 1 row and the AC-20 entry to `RocketSubscription` (noting `RocketMqSubscription<T>` derives from it).

---

### 12. FR-2 does not address `DispatchBuilder` mutating `subscription.ChannelFactory`, so the rule's inputs depend on hosted-service ordering (Score: 55)

FR-2 cites `DispatchBuilder.cs:146-148` correctly, but describes it as "assigns the default only to subscriptions whose `ChannelFactory` is `null`" without noting that this is a **write** to the shared subscription object.

**Evidence**:
```csharp
foreach (var connection in _subscriptions.Where(c => c.ChannelFactory == null))
{
    connection.ChannelFactory = _defaultChannelFactory;
}
```
So whether the rule sees FR-2 step 1 or steps 2/3 depends on whether `IDispatcher` has been resolved before the validation hosted service runs — i.e. on `IHostedService` registration order. The outcome happens to be invariant (the assigned instance is the same object, and step 3's synthesised `InMemoryChannelFactory` has the same type as the one line 159 news up), but the document neither states that invariance nor makes it a requirement, and C-6 only covers the *snapshot* concern, not the mutation.

**Recommendation**: Add a sentence to FR-2 (or a new C-) stating that the rule's verdict must be invariant to whether `DispatchBuilder.Subscriptions()` has already back-filled `subscription.ChannelFactory`, and add an AC that evaluates the rule both before and after that back-fill and asserts identical findings.

---

### 13. Two grounding line references are off (Score: 50)

**Evidence**:
- `ServiceCollectionExtensions.cs:205-226` is cited three times as `RegisterConsumerValidationSpecs`. The method actually spans lines **199-229**.
- C-4 cites `CombinedChannelFactory.cs:13` for the `_factories` field. Line 13 is `{`; the field is on line **14**.

(`:34`, `:46`, `:59`, `Subscription.cs:48`, `Subscription.cs:172`, `IAmConsumerOptions.cs:12`, `GcpPubSubSubscription.cs:108`, `MqttSubscription.cs:35`, `GcpPubSubChannelFactory.cs:14-15`, `GcpPubSubConsumerFactory.cs:15`, MQTT `ChannelFactory.cs:33`, `BrighterPipelineValidationExtensions.cs:58` and `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs:83` all check out exactly.)

**Recommendation**: Correct to `:199-229` and `:14`.

---

### 14. NFR-1 mis-describes the pattern used by the existing four rules (Score: 45)

NFR-1 requires the new rule be "expressed with the established `ISpecification<Subscription>` / `Specification<Subscription>` pattern used by the existing four rules".

**Evidence**: Only three of the four use `Specification<Subscription>` with a `(predicate, errorFactory)` pair. `UnwrapTransformResolvable` (`ConsumerValidationRules.cs:142-167`) returns a `DisposingSpecification<Subscription>` built from a single `subscription => IReadOnlyList<ValidationResult>` function. `ValidationError` is confirmed as `public record ValidationError(ValidationSeverity Severity, string Source, string Message)` (`src/Paramore.Brighter/ValidationError.cs:33`) and `ISpecification<TData>` at `src/Paramore.Brighter/Specification.cs:35`.

**Recommendation**: Say "the `ISpecification<Subscription>` abstraction used by the existing rules, of which three use the `Specification<Subscription>` predicate/error-factory form".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 6 |
| 50-69 (Medium) | 6 |
| 0-49 (Low) | 1 |

**Total findings**: 14
**Findings at or above threshold (60)**: 10
