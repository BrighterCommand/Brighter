# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4334

## Problem Statement

As a developer configuring a Brighter consumer (ServiceActivator) against a messaging gateway, I would like `ValidatePipelines()` to tell me at startup when a `Subscription` is incompatible with the channel factory it will actually be handed, so that I find out from a named, actionable validation finding rather than from a `ConfigurationException` thrown deep inside the Dispatcher when it builds its channels — or, worse, from a consumer that appears to start but silently consumes from the wrong transport.

### The defect this catches

Every transport's `Subscription` subclass carries transport-specific configuration, and most transport `ChannelFactory` implementations downcast the `Subscription` they are handed. For example, `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs` does this in all three creation methods (lines 53, 75 and 99):

```csharp
MsSqlSubscription? msSqlSubscription = subscription as MsSqlSubscription;
if (msSqlSubscription == null)
    throw new ConfigurationException("MS SQL ChannelFactory We expect an MsSqlSubscription or MsSqlSubscription<T> as a parameter");
```

Writing `new Subscription<T>(...)` where `MsSqlSubscription<T>` was required therefore **compiles cleanly and fails only when the Dispatcher builds its channels**. Two sample applications in this repository carried exactly this defect and had never been able to start (#4331). CI did not notice, because CI compiles the samples and compiling is precisely what this defect survives.

### The second, currently-invisible half of the defect

`Subscription.ChannelFactoryType` (`src/Paramore.Brighter/Subscription.cs:172`) is a `virtual` property, defaulting to `typeof(InMemoryChannelFactory)`, that each transport's subscription overrides. `CombinedChannelFactory` — the multi-bus factory — routes on it by **exact type equality** (`src/Paramore.Brighter/CombinedChannelFactory.cs:34`, `:46`, `:59`):

```csharp
var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
if (factory == null)
    throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
```

Twelve messaging-gateway subscription families ship in `src/`. **Nine declare an `override Type ChannelFactoryType`; three declare none and therefore silently inherit the `typeof(InMemoryChannelFactory)` default.** Of the nine overrides, **two name a type that is not an `IAmAChannelFactory` at all**. Table 1:

| Subscription | Declares `ChannelFactoryType` | Correct? |
|---|---|---|
| `RocketSubscription` (`RocketMqSubscription.cs:50`) | `RocketMqChannelFactory` | yes |
| `RedisSubscription` | `ChannelFactory` (Redis) | yes |
| `AzureServiceBusSubscription` | `AzureServiceBusChannelFactory` | yes |
| `RmqSubscription` (RMQ.Sync) | `ChannelFactory` (RMQ.Sync) | yes |
| `RmqSubscription` (RMQ.Async) | `ChannelFactory` (RMQ.Async) | yes |
| `KafkaSubscription` | `ChannelFactory` (Kafka) | yes |
| `MsSqlSubscription` | `ChannelFactory` (MsSql) | yes |
| **`GcpPubSubSubscription`** (`GcpPubSubSubscription.cs:108`) | **`GcpPubSubConsumerFactory`** | **NO** — an `IAmAMessageConsumerFactory` (`GcpPubSubConsumerFactory.cs:15`). Should be `GcpPubSubChannelFactory` (`GcpPubSubChannelFactory.cs:14`). |
| **`MqttSubscription`** (`MqttSubscription.cs:35`) | **`MqttMessageConsumerFactory`** | **NO** — an `IAmAMessageConsumerFactory` (`MqttMessageConsumerFactory.cs:30`). Should be `Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory` (`ChannelFactory.cs:33`). |
| **`SqsSubscription`** (AWSSQS, `SqsSubscription.cs:37`) | **— no override —** | **NO** — inherits `InMemoryChannelFactory`. Should be `Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory` (`ChannelFactory.cs:44`). |
| **`SqsSubscription`** (AWSSQS.V4, `SqsSubscription.cs:37`) | **— no override —** | **NO** — inherits `InMemoryChannelFactory`. Should be `Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory` (`ChannelFactory.cs:44`). |
| **`PostgresSubscription`** (`PostgresSubscription.cs:10`) | **— no override —** | **NO** — inherits `InMemoryChannelFactory`. Should be `PostgresChannelFactory` (`PostgresChannelFactory.cs:11`). |

Because `CombinedChannelFactory` matches on exact type equality, **a subscription from any of those five transports can never match a registered factory** and always throws `ConfigurationException("No channel factory found for subscription …")`. Multi-transport configurations that include GCP Pub/Sub, MQTT, AWS SQS (either version) or Postgres are broken today.

Correcting all five is part of this feature, not a follow-up (decision D2). That is what makes `Error` severity (D1) safe: were the three missing overrides left in place, the new rule would report an `Error` for a *correct, working* AWS SQS or Postgres consumer — `D` would be `InMemoryChannelFactory` while the effective factory is the real transport factory — and under the default `throwOnError: true` would refuse to start a host that runs fine today.

### Why nothing catches this today

`RegisterConsumerValidationSpecs` (`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:199-229`) registers four `ISpecification<Subscription>` rules — `PumpHandlerMatch`, `HandlerRegistered`, `RequestTypeSubtype` and `UnwrapTransformResolvable`, all defined in `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`. **None of them checks that a subscription is compatible with the channel factory it will be handed.** The data needed to check it is already present on every subscription.

### Terminology

Used consistently throughout this document.

- **Declared channel factory type** — the value of `Subscription.ChannelFactoryType` for a given subscription instance (`src/Paramore.Brighter/Subscription.cs:172`).
- **Effective channel factory** — the `IAmAChannelFactory` instance a subscription will actually be handed at channel-creation time (defined precisely in FR-2).
- **Candidate factory set** — the set of factory instances the effective channel factory can dispatch a subscription to: for a `CombinedChannelFactory`, its inner factories; for any other factory, the single-element set containing the effective channel factory itself (FR-3).
- **Compatible / mismatch** — the compatibility predicate defined in FR-3. A subscription that is not compatible is in *mismatch*.
- **The rule** — the new `ISpecification<Subscription>` introduced by this feature.
- **ValidatePipelines** — the opt-in entry point `ValidatePipelines(bool enabled = true, bool throwOnError = true)` (`src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs:58`). Findings are `ValidationError` records carrying `Severity`, `Source` and `Message`. `ValidationSeverity.Error` findings block host startup when `throwOnError: true`; `ValidationSeverity.Warning` findings never block.

## Proposed Solution

From the developer's point of view, three things change.

1. **A fifth consumer validation rule.** When `ValidatePipelines()` is enabled, every configured `Subscription` is checked against the channel factory it will actually be handed. A mismatch is reported as a `ValidationSeverity.Error` finding, in the same shape and style as the existing `PumpHandlerMatch` rule, so with `ValidatePipelines(throwOnError: true)` the host refuses to start and says exactly which subscription is wrong, what type it declares it needs, what it will actually get, and what to change. The developer sees this at startup in development, instead of a `ConfigurationException` from the Dispatcher (or nothing at all until the wrong transport is consumed).

2. **Multi-bus configurations are respected, not flagged.** When the effective factory is a `CombinedChannelFactory`, the rule checks the subscription against the factories *inside* it — exactly as `CombinedChannelFactory` itself does at runtime — so a correctly-configured multi-transport application produces no findings, and an incorrectly-configured one is told which inner factories were available.

3. **Five long-standing transport defects are fixed.** `GcpPubSubSubscription` and `MqttSubscription` are corrected to declare their real channel factories rather than a consumer factory, and `SqsSubscription` (AWSSQS), `SqsSubscription` (AWSSQS.V4) and `PostgresSubscription` gain the override they never had. Subscriptions from all five transports can then be routed by `CombinedChannelFactory` for the first time — and the new Error-severity rule does not fire on correct configurations of any of them.

## Requirements

### Functional Requirements

**FR-1 — A channel-factory compatibility rule exists and is evaluated for every configured subscription.**
A new `ISpecification<Subscription>` MUST be added to `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`, alongside the existing four rules, and registered in `RegisterConsumerValidationSpecs` (`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:199-229`) so that it is evaluated once per configured subscription when `ValidatePipelines()` runs. For a subscription in mismatch (FR-3) the rule MUST produce exactly one `ValidationError` with `Severity = ValidationSeverity.Error` and `Source = $"Subscription '{subscription.Name}'"` — the same `Source` format the existing four rules use. For a compatible subscription the rule MUST produce no finding.
- Example: with `options.DefaultChannelFactory = new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(msSqlMessageConsumerFactory)` and a single configured `Subscription<TestRequest>` named `greeting-sub`, the rule produces exactly one finding, of severity `Error`, whose `Source` is `Subscription 'greeting-sub'`.
- Example: replacing that subscription with `MsSqlSubscription<TestRequest>` named `greeting-sub` produces zero findings.

**FR-2 — The effective channel factory is resolved as `subscription.ChannelFactory ?? options.DefaultChannelFactory ?? InMemoryChannelFactory`.**
The rule MUST determine the effective channel factory for a subscription using this precedence, which mirrors the runtime wiring (`DispatchBuilder` assigns the default only to subscriptions whose `ChannelFactory` is `null` — `src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs:146-148`; the DI path substitutes an in-memory factory for a null default — `ServiceCollectionExtensions.cs:159`):
1. If `subscription.ChannelFactory` is non-null, that instance is the effective channel factory. The per-subscription override takes precedence over the default and the default MUST NOT be consulted.
2. Otherwise, if `options.DefaultChannelFactory` (`src/Paramore.Brighter/IAmConsumerOptions.cs:12`) is non-null, that instance is the effective channel factory.
3. Otherwise, the effective channel factory is treated as an `InMemoryChannelFactory`, matching `ServiceCollectionExtensions.cs:159`.
- Example: `options.DefaultChannelFactory` is a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`, and subscription `rmq-sub` is an `RmqSubscription<TestRequest>` (RMQ.Async) with `ChannelFactory` set to a `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory`. The effective factory for `rmq-sub` is the RMQ.Async one, it is compatible, and no finding is produced for `rmq-sub`.
- Example: the same configuration with `rmq-sub`'s `ChannelFactory` left `null` resolves to the MsSql default, which is a mismatch, and produces one `Error`.

**FR-2a — The verdict MUST be invariant to `DispatchBuilder`'s back-fill.** `DispatchBuilder.Subscriptions()` **writes** the default into the subscription (`src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs:146-148`):
```csharp
foreach (var connection in _subscriptions.Where(c => c.ChannelFactory == null))
{
    connection.ChannelFactory = _defaultChannelFactory;
}
```
so whether the rule observes step 1 or steps 2/3 depends on whether `IDispatcher` has been resolved before the validation hosted service runs. The rule MUST produce identical findings either way. (The instance assigned is the same object the rule would have resolved, and step 3's in-memory substitute has the same type as the one `ServiceCollectionExtensions.cs:159` constructs, so the invariance holds — but it MUST be asserted, not assumed.)

**FR-3 — Compatibility is defined against the candidate factory set, with `CombinedChannelFactory` unwrapped one level.**
Given the effective channel factory `F` (FR-2) and the subscription's declared channel factory type `D`:
- **Combined arm.** If `F` is a `CombinedChannelFactory`, the candidate factory set is `F`'s inner factories, in the order supplied to its constructor. The subscription is **compatible** if and only if at least one inner factory `f` satisfies `f.GetType() == D` — exact type equality, mirroring `CombinedChannelFactory.cs:34/46/59` precisely. The rule MUST NOT compare `D` against `typeof(CombinedChannelFactory)`; doing so would flag every subscription in every multi-transport application. The rule MUST NOT recurse into a nested `CombinedChannelFactory` among the inner factories, because `CombinedChannelFactory` does not recurse at runtime either — a nested combined factory therefore matches only a subscription whose `D` is literally `typeof(CombinedChannelFactory)`.
- **Direct arm.** If `F` is not a `CombinedChannelFactory`, the candidate factory set is `{ F }`, and the subscription is **compatible** if and only if `D.IsAssignableFrom(F.GetType())` — that is, `F` is of the declared type or a subtype of it. Assignability (rather than exact equality) is used here so that an application supplying its own subclass of a transport's `ChannelFactory` is not falsely flagged; at runtime that subclass satisfies the gateway's downcast of the subscription and works. The two arms differ deliberately because the runtime behaviours they predict differ.
- **A null `D`.** `ChannelFactoryType` is `public virtual`, so an out-of-repo override may return `null`. In the **combined** arm the *iff* above already settles it: `object.GetType()` never returns null, so no inner factory satisfies the predicate, the subscription is in mismatch, and FR-1 requires exactly one `Error`. This matches runtime, where `_factories.FirstOrDefault(f => f.GetType() == null)` is `null` for every non-empty factory set and `CombinedChannelFactory.cs:37` throws on every start. The **direct** arm is stated explicitly to match: a null `D` is **incompatible**, decided by an explicit `D is null` test rather than by letting `D.IsAssignableFrom(...)` throw and be converted into a `"Rule evaluation failed"` finding with the wrong `Source`. The direct arm's verdict is new breakage and is accepted under **C-13**.
- Example (combined, compatible): `F = new CombinedChannelFactory([new Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory(...), new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(...)])` and the subscription is an `MsSqlSubscription<TestRequest>` (`D = Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`). No finding.
- Example (combined, mismatch): the same `F` with a plain `Subscription<TestRequest>` (`D = typeof(InMemoryChannelFactory)`). One `Error`, because no inner factory is an `InMemoryChannelFactory`.
- Example (direct, subclass accepted): `F = new MyAuditingMsSqlChannelFactory(...) : Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` with an `MsSqlSubscription<TestRequest>`. No finding.
- Example (combined, subclass rejected): the same subclass instance as the sole inner factory of a `CombinedChannelFactory`, with an `MsSqlSubscription<TestRequest>`. One `Error` — correctly, because `CombinedChannelFactory` would throw `ConfigurationException("No channel factory found for subscription …")` at runtime.

**FR-4 — The default in-memory configuration produces no finding.**
A plain `Subscription<T>` or `Subscription` (whose `ChannelFactoryType` is `typeof(InMemoryChannelFactory)`) with `subscription.ChannelFactory == null` and `options.DefaultChannelFactory == null` MUST produce no finding, because it resolves to `InMemoryChannelFactory` under FR-2 step 3 and is compatible under the FR-3 direct arm. The same holds when `options.DefaultChannelFactory` is explicitly an `InMemoryChannelFactory`.
- Example: `AddConsumers(options => options.Subscriptions = [new Subscription<TestRequest>(new SubscriptionName("greeting-sub"), …)])` with no `DefaultChannelFactory`, plus `ValidatePipelines(throwOnError: true)`, starts the host with zero findings from this rule.

**FR-5 — The finding message uses fully-qualified type names and names a concrete remedy.**
Eight transports name their channel factory class `ChannelFactory` in eight different assemblies (AWSSQS, AWSSQS.V4, Kafka, MQTT, MsSql, Redis, RMQ.Sync, RMQ.Async). A message built from `Type.Name` would therefore read "expected `ChannelFactory`, got `ChannelFactory`". The `Message` of every finding produced by this rule MUST therefore contain:
1. the subscription's own runtime type, as `Type.FullName`;
2. the declared channel factory type `D`, as `Type.FullName` — or, when `D` is `null` (FR-3's null-`D` case), the literal phrase `no ChannelFactoryType` in its place, there being no type to name;
3. the type(s) it will actually be handed, as `Type.FullName` — in the direct arm the effective factory's type; in the combined arm the `Type.FullName` of every inner factory in the candidate factory set, in constructor order, comma-separated;
4. a remedy clause instructing the developer either to use the subscription type that the effective factory requires, or to configure a channel factory of type `D`.

**Type display format (normative).** `Type.FullName` is NOT usable directly: for a closed generic such as `Subscription<TestRequest>` it returns
`Paramore.Brighter.Subscription`1[[Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest, SomeAssembly, Version=…, Culture=neutral, PublicKeyToken=null]]`,
whose assembly identity payload defeats NFR-2. The rule MUST therefore render every type in items 1-3 with a **display name** defined as:
- a non-generic type: its `Type.FullName` (namespace-qualified, no assembly identity);
- a closed generic type: the namespace-qualified name with the ``​`n`` arity suffix removed, followed by its type arguments' display names in angle brackets, comma-separated — e.g. `Paramore.Brighter.Subscription<Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest>`.

A display name MUST NOT contain `Version=`, `Culture=` or `PublicKeyToken=`, and MUST NOT be a bare `Type.Name`.

**Remedy clause (normative templates).** Item 4 MUST be rendered as one of exactly **five** literals, selected by the rules below. `{F}` is the effective factory's display name; `{F-list}` is the inner factories' display names in constructor order, comma-separated. The conditions are evaluated in the order listed and are total over the input space: T4 first, then the null-`D`/in-memory pair, then the general pair.

| # | Condition | Literal |
|---|---|---|
| T4 | Combined arm, and the candidate set is **empty** | `— add a channel factory to the combined channel factory` |
| T3a | Direct arm, and (`D == typeof(InMemoryChannelFactory)` or `D is null`) | `— use a subscription type whose ChannelFactoryType is {F}` |
| T3b | Combined arm, non-empty candidate set, and (`D == typeof(InMemoryChannelFactory)` or `D is null`) | `— use a subscription type whose ChannelFactoryType is one of: {F-list}` |
| T1 | Direct arm, `D` is not null, and `D != typeof(InMemoryChannelFactory)` | `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is {F}` |
| T2 | Combined arm, non-empty candidate set, `D` is not null, and `D != typeof(InMemoryChannelFactory)` | `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is one of: {F-list}` |

**A null `D` selects T3a/T3b, not T1/T2.** T1 and T2 name `{D}` on their "change the configuration" side, which is unrenderable when there is no declared type; T3a/T3b already suppress that half, so they are the correct templates for the null case as well as the in-memory one. The reason differs — in-memory suppresses a remedy that is legitimate but harmful (C-2), null suppresses one that cannot be written — but the literal required is identical, so no further template is needed.

**T4 exists because an empty candidate set has no `{F-list}` to offer.** A `CombinedChannelFactory` constructed with no inner factories is legal and routes nothing, so the verdict is an `Error`; but rendering T2/T3b with an empty list would produce a message ending "is one of:" with nothing after it, which states no remedy and so fails NFR-2. T4 names the actual fault instead. T4 is **combined-arm only** — the direct arm's candidate set is `{ F }` or `{ typeof(InMemoryChannelFactory) }` and can never be empty.

**T3a and T3b suppress the `{D}` half, and that suppression is normative.** When `D` is `InMemoryChannelFactory` the subscription is a plain `Subscription`/`Subscription<T>`, and "configure a channel factory of type `Paramore.Brighter.InMemoryChannelFactory`" is not a legitimate remedy — following it produces a consumer that silently reads from an in-memory bus instead of the intended transport, which is exactly the outcome C-2 exists to prevent. Offering it as one of two options is not neutral: it is the option that requires less work, in the case this feature most often fires. T3a and T3b therefore offer only the direction that fixes the defect.

T2's "is one of" rather than "is" is deliberate: a subscription declares exactly one channel factory type, while a combined factory offers several.

The remedy MUST be **asymmetric**: it names `{D}` on the "change the configuration" side and `{F}` on the "change the subscription" side. A template referring only to `{D}` would, in this feature's headline case — a plain `Subscription<T>` (`D = InMemoryChannelFactory`) handed a transport factory — advise the developer to configure an in-memory channel factory, which is precisely the silent-wrong-bus outcome C-2 exists to prevent. The remedy is pinned by AC-13 and AC-13a rather than left to the implementer's prose.
- Example (direct arm): a `Subscription<TestRequest>` handed a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` yields a message containing the literals `Paramore.Brighter.Subscription`, `Paramore.Brighter.InMemoryChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`, and a remedy clause.
- Example (combined arm): the same subscription handed `CombinedChannelFactory([RMQ.Async.ChannelFactory, MsSql.ChannelFactory])` yields a message containing `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` in that order.

**FR-6 — Severity is `Error`, and the rule participates in existing reporting and blocking semantics unchanged.**
Every finding from this rule MUST carry `ValidationSeverity.Error`. Under `ValidatePipelines(throwOnError: true)` (the default) a mismatch MUST prevent the host from starting; under `ValidatePipelines(throwOnError: false)` it MUST be reported without blocking, exactly as for the existing `Error`-severity rules. The rule MUST be evaluated only when `ValidatePipelines()` is enabled and MUST add no startup work when it is not. The addition MUST be purely additive: it MUST NOT change the severity, `Source`, `Message` or blocking behaviour of `PumpHandlerMatch`, `HandlerRegistered`, `RequestTypeSubtype` or `UnwrapTransformResolvable`, nor the defaults of `ValidatePipelines(enabled = true, throwOnError = true)`.
- Example: a configuration with one mismatched subscription and `ValidatePipelines(enabled: false)` starts with no findings and no rule evaluation.

**FR-7 — `GcpPubSubSubscription.ChannelFactoryType` is corrected to `GcpPubSubChannelFactory`.**
`src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubSubscription.cs:108` MUST return `typeof(GcpPubSubChannelFactory)` instead of `typeof(GcpPubSubConsumerFactory)`. `GcpPubSubConsumerFactory` implements `IAmAMessageConsumerFactory`, not `IAmAChannelFactory`, so the current value can never match any registered channel factory.
- Example: `new GcpPubSubSubscription<TestRequest>(…).ChannelFactoryType == typeof(GcpPubSubChannelFactory)`.

**FR-8 — `MqttSubscription.ChannelFactoryType` is corrected to the MQTT `ChannelFactory`.**
`src/Paramore.Brighter.MessagingGateway.MQTT/MqttSubscription.cs:35` MUST return `typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)` instead of `typeof(MqttMessageConsumerFactory)`. `MqttMessageConsumerFactory` implements `IAmAMessageConsumerFactory`, not `IAmAChannelFactory`.
- Example: `new MqttSubscription<TestRequest>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)`.

**FR-9 — `SqsSubscription` (AWSSQS) declares its channel factory.**
`src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsSubscription.cs` MUST add `public override Type ChannelFactoryType => typeof(ChannelFactory);` (the AWSSQS `ChannelFactory`, `ChannelFactory.cs:44`). It currently declares no override and so inherits `typeof(InMemoryChannelFactory)`, which no AWS SQS configuration can satisfy. That factory downcasts (`ChannelFactory.cs:98`, `:208`: `subscription as SqsSubscription`), so the mismatch is a genuine runtime failure, not merely a routing gap.
- Example: `new SqsSubscription<TestRequest>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory)`.

**FR-10 — `SqsSubscription` (AWSSQS.V4) declares its channel factory.**
`src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsSubscription.cs` MUST add the equivalent override naming the V4 `ChannelFactory` (`ChannelFactory.cs:44`), for the same reason and with the same evidence (`ChannelFactory.cs:98`, `:208`). The two AWS packages MUST be corrected together; correcting only one would leave the V3/V4 pair inconsistent.
- Example: `new SqsSubscription<TestRequest>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory)`.

**FR-11 — `PostgresSubscription` declares its channel factory.**
`src/Paramore.Brighter.MessagingGateway.Postgres/PostgresSubscription.cs` MUST add `public override Type ChannelFactoryType => typeof(PostgresChannelFactory);` (`PostgresChannelFactory.cs:11`). It currently declares no override. That factory downcasts in all three creation methods (`PostgresChannelFactory.cs:18`, `:39`, `:60`: `subscription is not PostgresSubscription`).
- Example: `new PostgresSubscription<TestRequest>(…).ChannelFactoryType == typeof(PostgresChannelFactory)`.

**FR-12 — Every shipped gateway subscription declares a real, non-default channel factory type (regression guard).**
An automated **reflection sweep** MUST assert, for every non-abstract type assignable to `Subscription` in each shipped messaging-gateway assembly, that its `ChannelFactoryType`:
1. implements `IAmAChannelFactory` (`typeof(IAmAChannelFactory).IsAssignableFrom(type)`), **and**
2. is **not** `typeof(InMemoryChannelFactory)` — i.e. the type does not merely inherit the base default.

Condition 2 is what catches the AWSSQS/Postgres class of defect; a guard that only checked types *declaring* an override would pass over them vacuously.

**Mechanism (normative).** The sweep MUST obtain `ChannelFactoryType` **without invoking a subscription constructor**. `ChannelFactoryType` is an instance virtual property (`Subscription.cs:172`), so an instance is needed. `Activator.CreateInstance(Type)` cannot supply one: **no shipped subscription type declares a parameterless constructor** — a constructor whose parameters are all optional does not produce one — so it throws `MissingMethodException`. Separately, several transports' constructors default `messagePumpType` to `MessagePumpType.Unknown` (GCP Pub/Sub, Postgres, RocketMQ, Kafka among them), which `Subscription.cs:213` rejects with `ConfigurationException`; others default to `Reactor` or `Proactor` (`RMQ.Sync/RmqSubscription.cs:107`, `AWSSQS/SqsSubscription.cs:201`, `Redis/RedisSubscription.cs:125`, `AzureServiceBus/AzureServiceBusSubscription.cs:125`, `RMQ.Async/RmqSubscription.cs:177`, `MQTT/MqttSubscription.cs:132`). The missing parameterless constructor is the universal reason; the pump-type guard is an additional obstacle for some. The sweep MUST therefore use an uninitialised instance (`System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject`) or an equivalent construction-free mechanism, and MUST NOT require broker or network access (NFR-3).

**The check MUST be exposed as a pure predicate** over `(Type subscriptionType, Type declaredFactoryType)` returning pass/fail plus a reason, so that the negative cases (AC-28) can be exercised over synthetic types declared in a test assembly. A guard whose failure path can only be demonstrated by shipping a defective gateway is not testable.

**Where the predicate lives is a design decision for the ADR**, but one constraint is fixed here: AC-27 requires a caller in each of the twelve gateway test projects, and no shared test-support assembly reaches all twelve (`Paramore.Brighter.Base.Test` is referenced by three, `Paramore.Test.Helpers` by one, `Paramore.Brighter.ServiceActivator` by six — AWS, AWS.V4, MQTT, RMQ.Async, RMQ.Sync, RocketMQ). Only `Paramore.Brighter` itself is referenced by all of them. Introducing **one new public type** to host the predicate is therefore expressly permitted notwithstanding NFR-5, which constrains changes to *existing* abstractions. Copying the predicate into twelve test projects is not acceptable — twelve copies of a guard drift.

**Scope.** Generic subscription types MUST be closed against a representative `IRequest` before the property is read where the mechanism requires it, honouring each type's constraints (`GcpPubSubSubscription<T> where T : class, IRequest` differs from `MqttSubscription<T> where T : IRequest`). A base/derived pair in the same assembly (e.g. `RocketSubscription` and `RocketMqSubscription<T>`) MUST be reported at most once **when the derived type declares no `ChannelFactoryType` of its own** — its value is then its base's by construction, so the two cannot disagree and a second report says nothing new. A derived type that **does** declare its own override MUST be reported in its own right, because it can disagree with its base and a guard that hid it would be blind to exactly the declaration the author wrote. Stated conditionally on purpose: an unconditional "at most once" would require dropping a derived override that differs from its base, which inverts the guard's purpose.
- Example: a hypothetical `FooSubscription` added to a Foo gateway assembly with no override fails condition 2; one declaring `typeof(FooMessageConsumerFactory)` fails condition 1.

**FR-13 — Findings are one-per-subscription and deterministic.**
The rule MUST emit at most one finding per configured subscription per validation run, regardless of how many candidate factories were inspected. Repeated runs over the same configuration MUST produce the same findings, with the same message text, in the same order — subscriptions in `options.Subscriptions` order, and inner factories in `CombinedChannelFactory` constructor order.
- Example: two mismatched subscriptions, `sub-a` then `sub-b`, produce exactly two findings, in that order, on every run.

### Non-functional Requirements

- **NFR-1 — Consistency with the existing rule architecture.** The rule MUST be expressed with the `ISpecification<Subscription>` abstraction (`src/Paramore.Brighter/Specification.cs:35`) used by the existing rules — of which three use the `Specification<Subscription>` predicate/error-factory form, while `UnwrapTransformResolvable` (`ConsumerValidationRules.cs:142-167`) uses `DisposingSpecification<Subscription>` with a `subscription => IReadOnlyList<ValidationResult>` function. The new rule fits the `Specification<Subscription>` form. It MUST be unit-testable in isolation without a host, a container, or a broker.
- **NFR-2 — Actionable, unambiguous messages.** Each message MUST identify the offending subscription by name (via `Source`) and by type, state what it declares it needs and what it will get (in fully-qualified form, FR-5), and state a remedy. No message may be satisfiable by a generic phrase such as "channel factory mismatch".
- **NFR-3 — No broker or network access.** Neither the rule nor any test introduced by this feature may open a connection to a broker, a database, or any external service. The rule inspects types and configured instances only. The FR-12 sweep is reflection-only.
- **NFR-4 — No new startup cost when validation is off.** Consistent with FR-6; the rule performs work only inside a `ValidatePipelines()`-enabled run, and its per-subscription cost is bounded by the size of the candidate factory set.
- **NFR-5 — No public API change to existing abstractions beyond what FR-7 to FR-11 require.** In particular `IAmAChannelFactory`, `Subscription` and `IAmConsumerOptions` keep their current members. If the rule requires read access to a `CombinedChannelFactory`'s inner factories, any new member MUST be added to `CombinedChannelFactory` itself and MUST be additive (see C-4).
- **NFR-6 — Behaviour preserved for working configurations.** No configuration that starts successfully today may be made to fail by this feature, **except** in the cases documented as deliberate exceptions in **C-2**, **C-10**, **C-11**, **C-12** and **C-13**. Deliberately stated without a count: the exception set is enumerated in those constraints, and a number in this sentence has twice been wrong as further cases surfaced. Any newly discovered case MUST be added as its own constraint and to C-8's release-note obligations, not absorbed silently here. Stated without the "genuinely correct" qualifier deliberately — that qualifier made the requirement unfalsifiable, because the specification would then decide for itself what counts as correct. A naive reading of D1 would have had the rule reject *every* correct AWS SQS, Postgres, GCP and MQTT configuration; FR-7 to FR-11 correct their declarations so those pass, which is the whole reason D2 is in scope. That is separate from C-11 and C-12, which are the residue those corrections cannot remove: MQTT's factory accepts any subscription (C-11), and correcting AWS SQS / Postgres withdraws an accidental in-memory route (C-12).
- **NFR-7 — British spelling** in all new documentation and XML comments, consistent with the repository.

### Constraints and Assumptions

- **C-1 — Severity is `Error` (settled, D1).** A mismatch is reported at `ValidationSeverity.Error` and therefore blocks startup under `ValidatePipelines(throwOnError: true)`. This is not revisited by this specification. It is safe to do so precisely because D2 (FR-7 to FR-11) removes every class of in-repo false positive known at the time of writing: with `GcpPubSubSubscription` and `MqttSubscription` still declaring consumer-factory types, and `SqsSubscription` (both AWS packages) and `PostgresSubscription` declaring no override at all, a correct configuration of any of those five transports would otherwise be flagged as an `Error` and refused startup. The AWS SQS and Postgres cases are the more serious of the two classes, because they affect working consumers and more in-repo samples (`samples/TaskQueue/AWSTaskQueue/…`, `samples/TaskQueue/PostgresTaskQueue/…`, `samples/Scheduler/AwsTaskQueue/…`, `samples/Transforms/AWS*/…`). FR-12's guard exists so this class cannot silently return.
- **C-2 — The rule is symmetric, including the "transport subscription, in-memory factory" direction (accepted risk).** A transport-specific subscription (for example `MsSqlSubscription<T>`) resolving to an `InMemoryChannelFactory` is a mismatch and is reported as an `Error`, even though `InMemoryChannelFactory` does not downcast the subscription and so would not throw. This is deliberate: such a configuration silently consumes from an in-memory bus instead of the intended transport, which is a worse failure than an exception. Developers who want an in-memory consumer should use a plain `Subscription<T>`, which is the intended pattern and passes under FR-4; `ValidatePipelines` is in any case opt-in and can be disabled.
- **C-3 — Eleven of the twelve transport channel factories downcast the subscription and throw `ConfigurationException` when the cast fails; only MQTT's `ChannelFactory` does not.** Verified throw sites: MsSql (`ChannelFactory.cs:53`, `:75`, `:99`), Kafka, Redis, RMQ.Sync, RMQ.Async (`subscription as XSubscription`), Azure Service Bus (`AzureServiceBusChannelFactory.cs:103`), RocketMQ (`RocketMqChannelFactory.cs:15`, `:30`), GCP Pub/Sub (`GcpPubSubChannelFactory.cs:28-31`), AWSSQS and AWSSQS.V4 (`ChannelFactory.cs:98`, `:208`), Postgres (`PostgresChannelFactory.cs:18`, `:39`, `:60`). MQTT's `ChannelFactory` (`MQTT/ChannelFactory.cs:32-73`) passes the subscription through untouched. Consequence: for eleven transports a mismatch is already a hard runtime failure that this rule merely surfaces earlier and more clearly; for MQTT alone the mismatch is currently silent, which is why FR-8's correction matters independently of the rule.
  - *Note*: an earlier draft claimed Azure Service Bus and RocketMQ do not downcast. That was wrong — it came from a search for the `subscription as XSubscription` idiom that missed the `subscription is not XSubscription` form those gateways use.

- **C-4 — `CombinedChannelFactory`'s inner factories are currently private.** `_factories` is a `private readonly IReadOnlyList<IAmAChannelFactory>` (`CombinedChannelFactory.cs:14`). FR-3's combined arm requires read access to it. The exact mechanism (an additive read-only property, a membership query method, or an internals-visible arrangement) is a design decision for the ADR; the constraint here is only that it be additive and confined to `CombinedChannelFactory` (NFR-5).
- **C-5 — The rule needs the consumer options.** FR-2 requires `options.DefaultChannelFactory`, which lives on `IAmConsumerOptions` and is available where the four existing consumer specs are registered (`ServiceCollectionExtensions.cs:199-229`). Threading it into the rule's factory function is expected wiring, not new API. Where the options or the default factory cannot be resolved, FR-2 step 3 (in-memory) applies.
- **C-6 — Validation sees the configuration as snapshotted by `ValidatePipelines()`.** Consistent with the existing documented behaviour of `ValidatePipelines` ("call this last in the Brighter builder chain"), a channel factory assigned after that call is not seen by the rule.
- **C-7 — Subscriptions with a null `RequestType` are still checked.** Unlike `PumpHandlerMatch`, `HandlerRegistered` and `UnwrapTransformResolvable`, this rule does not depend on `RequestType` and therefore MUST NOT skip datatype-channel subscriptions whose `RequestType` is null.
- **C-8 — Versioning.** Targets Brighter V10.X. FR-7 to FR-11 change the value returned by a public virtual property (or add the override where none existed); this is a behaviour change (a fix), not a binary-breaking API change, and must be noted in the release notes. FR-9 to FR-11 in particular make AWS SQS, AWS SQS V4 and Postgres subscriptions routable by `CombinedChannelFactory` for the first time. The release notes MUST additionally carry the breaking-change notes for **C-10** (out-of-repo subscription types with no override), **C-11** (a plain `Subscription<T>` used with MQTT, which works today), **C-12** (AWS SQS / Postgres routed through an in-memory inner factory) and **C-13** (an out-of-repo override returning `null` in a single-factory configuration), each naming its symptom and its remedy. C-12 MUST be flagged as the one case `ValidatePipelines(throwOnError: false)` does not avoid, being a routing change rather than a validation verdict.
- **C-9 — Test placement, test doubles, and their request types.** `tests/Paramore.Brighter.Core.Tests` references only `Paramore.Brighter`, `Paramore.Brighter.BoxProvisioning`, `Paramore.Brighter.Extensions.DependencyInjection`, `Paramore.Brighter.Mediator`, `Paramore.Brighter.Outbox.Hosting`, `Paramore.Brighter.ServiceActivator` and `Paramore.Brighter.Testing` — **no** `MessagingGateway.*` assembly and **not** `ServiceActivator.Extensions.DependencyInjection`. The rule's behavioural tests (AC-1 to AC-19) MUST therefore be written against purpose-built doubles, not real gateway types, and live in `tests/Paramore.Brighter.Core.Tests/Validation/` with the existing `When_…` naming.

  **The doubles (a closed set — no AC may use one not listed here).** One class per file under `Validation/TestDoubles/`, per `.agent_instructions/testing.md`:

  | Double | Role in the comparison |
  |---|---|
  | `DeclaredChannelFactory : IAmAChannelFactory` | The identity a subscription **declares**. The compatible case in the direct arm. |
  | `DerivedChannelFactory : DeclaredChannelFactory` | A subclass of the declared identity — the direct arm's assignability case (AC-8, AC-9). |
  | `NonMatchingChannelFactory : IAmAChannelFactory` | A **different** identity that nothing declares. The mismatch case. |
  | `DeclaringSubscription : Subscription` | Overrides `ChannelFactoryType` to `typeof(DeclaredChannelFactory)`. |
  | `NonMatchingSubscription : Subscription` | Overrides `ChannelFactoryType` to `typeof(NonMatchingChannelFactory)`. Required by AC-6. |
  | `NullDeclaringSubscription : Subscription` | Overrides `ChannelFactoryType` to return `null`, standing in for an out-of-repo override that declares nothing. Required by AC-10a and AC-10b. Identity-only in the same sense as the rest: it overrides `ChannelFactoryType` and nothing else. |
  | `AlphaBus.ChannelFactory` / `BetaBus.ChannelFactory` | Two factories **both named `ChannelFactory`** in different namespaces under `…TestDoubles.AlphaBus` / `…TestDoubles.BetaBus`, plus `AlphaBus.AlphaSubscription` declaring the former. Required by AC-15, the same-simple-name case. |

  **These doubles are identity-only and MUST stay that way.** The rule never invokes a channel factory — FR-3 compares `Type` objects (`D.IsAssignableFrom(F.GetType())` in the direct arm, `f.GetType() == D` in the combined arm), so a double's entire contribution is *being a distinct type*. Every `IAmAChannelFactory` member on these doubles MUST therefore throw (`CreateSyncChannel`, `CreateAsyncChannel`, `CreateAsyncChannelAsync`), which both documents the intent and turns any test that strays into channel creation into a loud failure rather than a silent pass (NFR-3). They are named for their role in the comparison rather than after a transport precisely so that no one is tempted to give them transport behaviour: there is no behaviour to give.

  **Request types.** Each subscription double takes its own distinct request type — `FakeChannelFactoryRequest`, `FakeOtherRequest`, `AlphaRequest` — declared in `…Validation.TestDoubles`, one per file, so assembly scans cannot collide (`.agent_instructions/testing.md`). **No acceptance criterion may use `GreetingMade`**: that type exists only under `samples/WebAPI/*` (`WebAPI_Dynamo`, `WebAPI_Dapper`, `WebAPI_EFCore`) in namespaces `GreetingsApp.Requests` / `SalutationApp.Requests`, and is not visible to any test project. The namespace `Greetings.Ports.Events` (a different sample) contains `GreetingEvent`, not `GreetingMade`.

  **`TestRequest` is a placeholder, not a type.** In the transport-correction criteria (AC-20 to AC-26f) and in the FR-1 to FR-11 examples, `TestRequest` stands for a request type declared **locally in that gateway test project**, one per file, per `.agent_instructions/testing.md`. No type named `TestRequest` exists in the repository today and none need be created centrally; each gateway test project supplies its own.

  **Host-start behaviour (AC-16, AC-17)** needs `ServiceActivator.Extensions.DependencyInjection` and belongs in `tests/Paramore.Brighter.Extensions.Tests`, which already references it. That project does **not** reference `Paramore.Brighter.Core.Tests` — test projects here do not reference one another — so it MUST declare **its own copies** of the doubles it needs, in its own namespace, with their own distinct request types. This duplication is deliberate and stated so an implementer does not discover it mid-task.

  **Real-type tests.** The transport-correction criteria (AC-20 to AC-26f) and the FR-12 sweep (AC-27) name real gateway types and live in the gateway test project for that assembly, requiring no infrastructure (NFR-3). All are reflection-only **except AC-26f**, which constructs a channel factory and evaluates the rule — construction only, so it still touches no broker, database or network. The twelve shipped gateway assemblies map to twelve test projects:

  | Gateway assembly | Test project |
  |---|---|
  | `MessagingGateway.AWSSQS` | `Paramore.Brighter.AWS.Tests` |
  | `MessagingGateway.AWSSQS.V4` | `Paramore.Brighter.AWS.V4.Tests` |
  | `MessagingGateway.AzureServiceBus` | `Paramore.Brighter.AzureServiceBus.Tests` |
  | `MessagingGateway.GcpPubSub` | `Paramore.Brighter.Gcp.Tests` |
  | `MessagingGateway.Kafka` | `Paramore.Brighter.Kafka.Tests` |
  | `MessagingGateway.MQTT` | `Paramore.Brighter.MQTT.Tests` |
  | `MessagingGateway.MsSql` | `Paramore.Brighter.MSSQL.Tests` |
  | `MessagingGateway.Postgres` | `Paramore.Brighter.PostgresSQL.Tests` |
  | `MessagingGateway.RMQ.Async` | `Paramore.Brighter.RMQ.Async.Tests` |
  | `MessagingGateway.RMQ.Sync` | `Paramore.Brighter.RMQ.Sync.Tests` |
  | `MessagingGateway.RocketMQ` | `Paramore.Brighter.RocketMQ.Tests` |
  | `MessagingGateway.Redis` | `Paramore.Brighter.Redis.Tests` |

  A test project can only sweep the assemblies it references, and none references more than one gateway — so FR-12's guard needs **one sweep test in each of the twelve**, not one sweep somewhere. A newly added thirteenth gateway acquires its own.

  **`Paramore.Brighter.ServiceActivator` references.** `Paramore.Brighter.AWS.Tests`, `Paramore.Brighter.AWS.V4.Tests` and `Paramore.Brighter.MQTT.Tests` already reference it; `Paramore.Brighter.Gcp.Tests` and `Paramore.Brighter.PostgresSQL.Tests` do **not**, so they cannot evaluate the rule. Those two cover the routing-decision clause only (which needs just `Paramore.Brighter`); the rule-produces-no-findings clause for GCP and Postgres is covered in `Core.Tests` against the doubles.

- **C-10 — Subscriptions defined outside this repository are an accepted breaking change (settled, D3).** `Subscription.ChannelFactoryType` is `public virtual` with a `typeof(InMemoryChannelFactory)` default (`Subscription.cs:172`), so *any* subclass that does not override it inherits the in-memory default — including subscription types defined by applications, custom transports, or community gateways. D2 (FR-7 to FR-11) corrects the five in-repo cases and FR-12's sweep guards the twelve shipped gateway assemblies, but neither reaches out-of-repo types. Under D1 such a subscription is an `Error` and blocks startup when `ValidatePipelines(throwOnError: true)`.

  **This is accepted, not mitigated away.** The maintainer's decision (D3) is that custom transports are rare and the fix is a single line — adding the override — so the check keeps its strength rather than being softened for a population it cannot verify.

  Scope of the exposure, stated honestly:
  - A subclass of a *transport* subscription (e.g. extending `RmqSubscription` to carry extra configuration) inherits that transport's correct override and is **unaffected**.
  - A subclass of `Subscription`/`Subscription<T>` itself, used with a **downcasting** factory, already fails today — the rule only moves the failure earlier and names it.
  - A subclass of `Subscription`/`Subscription<T>` used with a **non-downcasting** custom factory, in a single-factory (non-combined) configuration, is the genuinely new breakage: it starts today and will not after this change.
  - Any such subscription used with a `CombinedChannelFactory` already throws `ConfigurationException("No channel factory found for subscription …")` today — **unless** one of the inner factories is exactly an `InMemoryChannelFactory`, in which case `CombinedChannelFactory.cs:34`'s exact-type match routes it successfully and the rule's combined arm likewise finds a match and reports nothing. Either way it is not a new failure.

  **Requirements this places on the change:**
  1. `release_notes.md` MUST record this as a breaking startup change for V10.X, naming the symptom (an `Error` from `ValidatePipelines` citing `InMemoryChannelFactory` as the declared type) and the remedy (`public override Type ChannelFactoryType => typeof(MyChannelFactory);`).
  2. The finding's message MUST make the remedy self-evident without consulting the release notes — which FR-5's asymmetric remedy clause already achieves, since `{F}` names the factory the subscription must declare.
  3. `ValidatePipelines` is opt-in, so an affected user can also unblock immediately with `ValidatePipelines(throwOnError: false)` while they add the override. This MUST be stated in the release note as the interim workaround.

- **C-11 — MQTT is a third deliberate exception: a plain `Subscription<T>` consumes MQTT correctly today and will become an `Error`.** MQTT is the one shipped transport whose channel factory accepts *any* `Subscription`. `MQTT/ChannelFactory.cs:61-99` builds its channel from `subscription.ChannelName`, `subscription.RoutingKey` and `subscription.BufferSize` only, and `MqttMessageConsumerFactory.Create` (`MqttMessageConsumerFactory.cs:63-68`) probes with null-tolerant `as IUseBrighterDeadLetterSupport` / `as IUseBrighterInvalidMessageSupport` casts, taking all broker configuration from `MqttMessagingGatewayConsumerConfiguration`. So `new Subscription<T>(…)` handed the MQTT `ChannelFactory` consumes MQTT correctly **today** — it is not a latent failure like the eleven downcasting transports (C-3).

  Under FR-3's direct arm that configuration has `D = typeof(InMemoryChannelFactory)` and `F = MQTT.ChannelFactory`, so it becomes an `Error` and, under the default `throwOnError: true`, blocks a host that currently runs. Unlike C-10 this involves **only shipped Brighter types**, so it is in-repo and cannot be dismissed as a custom-transport edge case.

  **Accepted on the same grounds as D3**, and with the same one-line remedy: use `MqttSubscription<T>`, which is the documented way to configure an MQTT consumer and which FR-8 corrects in this same change. The alternative — exempting MQTT from the rule — would mean the one transport where a mismatch is currently *silent* is also the one the rule stays silent about, which inverts the feature's purpose.

  **Obligations**: the release note required by C-8 MUST name this case explicitly alongside C-10's, with the symptom (an `Error` citing `InMemoryChannelFactory` as the declared type against the MQTT channel factory) and the remedy (`MqttSubscription<T>`). FR-5's templates T3a/T3b already render exactly that remedy direction, naming `Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory` as the type the subscription must declare.

- **C-12 — AWS SQS, AWS SQS V4 and Postgres subscriptions routed through an in-memory inner factory break at runtime (accepted).** This exception is caused by the **corrections** (FR-9 to FR-11), not by the rule, and is therefore the one case `ValidatePipelines(throwOnError: false)` does **not** rescue.

  Today `SqsSubscription<T>` (both AWS packages) and `PostgresSubscription<T>` declare no override, so `ChannelFactoryType` is `typeof(InMemoryChannelFactory)`. A host configured as `options.DefaultChannelFactory = new CombinedChannelFactory([new InMemoryChannelFactory(bus, TimeProvider.System), …])` therefore matches exactly at `CombinedChannelFactory.cs:34` and **starts successfully**, routing those subscriptions to the in-memory bus. After FR-9 to FR-11 the declared type becomes the real transport factory, no inner factory matches, and `CombinedChannelFactory.cs:37` throws `ConfigurationException("No channel factory found for subscription …")` when the Dispatcher builds its channels.

  **Accepted.** What breaks is a host that was silently consuming from an in-memory bus while believing it was consuming from SQS or Postgres — the precise silent-wrong-bus failure C-2 exists to name. Preserving it would mean preserving the defect. Note the asymmetry with C-2, C-10 and C-11: those are rule verdicts, suppressible by disabling validation; this is a routing change in `CombinedChannelFactory`, so the only remedy is to configure the real channel factory for that transport.

  **Obligations**: the C-8 release note MUST carry this case separately from C-10 and C-11, naming the symptom (`ConfigurationException` at Dispatcher start, not a validation finding), the remedy (add the transport's real channel factory to the `CombinedChannelFactory`, or stop relying on the in-memory route), and stating explicitly that `throwOnError: false` does not avoid it.

- **C-13 — A direct-arm subscription whose `ChannelFactoryType` override returns `null` starts today and will become an `Error`.**

  `CombinedChannelFactory` (`CombinedChannelFactory.cs:34/46/59`) is the **only** reader of `ChannelFactoryType` anywhere in `src/`. In a configuration whose effective channel factory is *not* a `CombinedChannelFactory`, a `Subscription` subclass that overrides `ChannelFactoryType` to return `null` is therefore never consulted at runtime: the host starts, the channel is built by the factory directly, and the consumer works. Under FR-3's null-`D` clause such a subscription is a mismatch, and under the default `throwOnError: true` it now blocks startup.

  Note the asymmetry with the combined arm, which is **not** new breakage: there, a null `D` already fails at `CombinedChannelFactory.cs:37` on every start, so the rule converts a certain runtime failure into a named startup finding. Only the direct arm turns a working host into a blocked one.

  **Accepted**, on D3's grounds. The population is vanishingly small — it requires an override written deliberately to return `null`, which no shipped subscription does and which ADR 0073's sweep would reject in-repo — and the remedy is the same one line as C-10's. The alternative, passing a null declaration in the direct arm, would make the rule silent about the one subscription that cannot state what it needs, which inverts the feature's purpose in the same way exempting MQTT would (C-11).

  **Obligations**: the C-8 release note MUST name this case, its symptom (a startup `Error` reading `declares no ChannelFactoryType`), its remedy (return a real channel factory type from the override), and the fact that unlike C-12 it *is* suppressible by `ValidatePipelines(throwOnError: false)`.

### Out of Scope

- **OOS-1 — Producer-side / publication validation.** No equivalent check is added for `Publication`s or the producer registry.
- **OOS-2 — Changing the downcasting behaviour of any gateway `ChannelFactory`.** The eleven factories that downcast the subscription (C-3) keep doing so, with the same exception type and message. This feature detects the condition earlier; it does not change what happens if detection is skipped.
- **OOS-3 — New API surface on `IAmAChannelFactory`.** No member is added to `IAmAChannelFactory`. The check works from `Subscription.ChannelFactoryType`, which already exists.
- **OOS-4 — Re-implementing what the existing four consumer rules cover.** Handler registration, pump/handler sync-async matching, `ICommand`/`IEvent` subtype checks and unwrap-transform resolvability are untouched.
- **OOS-5 — Changing `CombinedChannelFactory`'s runtime matching semantics.** It keeps matching on exact type equality, non-recursively; the rule mirrors that behaviour rather than improving it. Making it assignability-based or recursive is a separate proposal.
- **OOS-6 — A reverse map from channel factory type to required subscription type.** The finding message names the types involved and a generic remedy (FR-5 item 4); it is not required to compute and print "use `MsSqlSubscription<T>`" by scanning loaded assemblies for subscription subclasses.
- **OOS-7 — Fixing the sample applications from #4331.** That is tracked separately; this feature provides the detection that would have caught them.
- **OOS-8 — Runtime (post-startup) validation.** The check runs only at startup via `ValidatePipelines()`; no check is added to `DispatchBuilder`, `ConsumerFactory`, or the channel-creation path.
- **OOS-9 — Non-`ValidatePipelines` configuration surfaces**, including `DescribePipelines()` output changes and any Darker equivalent.

## Acceptance Criteria

Unless a criterion names a real gateway type, it is written against the test doubles defined in C-9 — the closed set `DeclaringSubscription`, `NonMatchingSubscription`, `DeclaredChannelFactory`, `DerivedChannelFactory`, `NonMatchingChannelFactory`, `AlphaBus.ChannelFactory`, `BetaBus.ChannelFactory`, `AlphaBus.AlphaSubscription` — with their own request types (`FakeChannelFactoryRequest`, `FakeOtherRequest`, `AlphaRequest`), and lives in `tests/Paramore.Brighter.Core.Tests/Validation/`. `TestRequest` in the transport-correction criteria means a request type local to that gateway test project. No criterion uses `GreetingMade`, which is not visible to any test project (C-9).

### The rule's behaviour

**AC-1** (FR-1, FR-5, FR-6) — *A mismatch is one Error, correctly sourced.*
Given a `Subscription<FakeChannelFactoryRequest>` named `greeting-sub` with `ChannelFactory` null, and `options.DefaultChannelFactory` set to a `DeclaredChannelFactory`,
When the rule is evaluated,
Then exactly one `ValidationError` is produced, with `Severity == ValidationSeverity.Error` and `Source == "Subscription 'greeting-sub'"`.

**AC-2** (FR-1, FR-3 direct arm) — *A matching subscription passes.*
Given a `DeclaringSubscription` named `greeting-sub` with `ChannelFactory` null, and `options.DefaultChannelFactory` set to a `DeclaredChannelFactory`,
When the rule is evaluated,
Then no findings are produced.

**AC-3** (FR-2) — *The per-subscription factory overrides the default.*
Given `options.DefaultChannelFactory` set to a `NonMatchingChannelFactory`, and a `DeclaringSubscription` named `sub-a` whose `ChannelFactory` is set to a `DeclaredChannelFactory`,
When the rule is evaluated,
Then no findings are produced for `sub-a`.

**AC-4** (FR-2) — *Falling back to the default is detected.*
Given the configuration of AC-3 with `sub-a.ChannelFactory` left null,
When the rule is evaluated,
Then exactly one `Error` is produced for `sub-a`.

**AC-5** (FR-2a) — *The verdict is invariant to `DispatchBuilder`'s back-fill.*
Given any configuration from AC-2, AC-3 or AC-4,
When the rule is evaluated once before `subscription.ChannelFactory` has been back-filled with the default, and again after it has,
Then both evaluations produce identical findings — same count, same `Source`, byte-identical `Message`.

**AC-6** (FR-3 combined arm, FR-13) — *A correct multi-bus configuration produces no false positives.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()])` and two subscriptions — a `DeclaringSubscription` named `sub-a` and a `NonMatchingSubscription` named `sub-b`, both with `ChannelFactory` null,
When the rule is evaluated,
Then no findings are produced for either subscription.

**AC-7** (FR-3 combined arm, FR-5) — *A subscription no inner factory can serve is an Error naming the inner factories.*
Given the `CombinedChannelFactory` of AC-6 and a plain `Subscription<FakeChannelFactoryRequest>` named `greeting-sub`,
When the rule is evaluated,
Then exactly one `Error` is produced for `greeting-sub`, whose `Message` contains the display names of both inner factories in constructor order, and does not name `Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed.

This configuration is flat, so the prohibition binds without exception. **D5** concerns only the *nested* case of AC-10 and does not relax this criterion: any wording of the message must continue to satisfy it.

**AC-8** (FR-3 direct arm) — *A user subclass of a channel factory is accepted.*
Given a `DerivedChannelFactory` (deriving from `DeclaredChannelFactory`) set as `options.DefaultChannelFactory`, and a `DeclaringSubscription` with `ChannelFactory` null,
When the rule is evaluated,
Then no findings are produced.

**AC-9** (FR-3 combined arm) — *The same subclass inside a `CombinedChannelFactory` is flagged, mirroring runtime.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new DerivedChannelFactory()])` and a `DeclaringSubscription` with `ChannelFactory` null,
When the rule is evaluated,
Then exactly one `Error` is produced — and, as a companion assertion, calling `CreateSyncChannel` on that `CombinedChannelFactory` with the same subscription throws `ConfigurationException` (it throws at `CombinedChannelFactory.cs:35-38`, before dispatching to any inner factory, so no channel is created).

**AC-10** (FR-3, no recursion) — *Nested combined factories are not unwrapped.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new CombinedChannelFactory([new DeclaredChannelFactory()])])` and a `DeclaringSubscription`,
When the rule is evaluated,
Then exactly one `Error` is produced, matching the runtime behaviour of `CombinedChannelFactory`, which also fails to route this subscription.

Per **D5**, the message for this configuration may name `Paramore.Brighter.CombinedChannelFactory` among the candidate types even though it does not route. That is accepted; this criterion asserts the verdict, not the wording.

**AC-10a** (FR-3 null `D`, direct arm, C-13) — *A null declared type is a mismatch in the direct arm.*
Given a `NullDeclaringSubscription` named `null-sub` with `ChannelFactory` null and `options.DefaultChannelFactory = new DeclaredChannelFactory()`,
When the rule is evaluated,
Then exactly one `Error` is produced for `null-sub`, whose `Message` contains the literal `no ChannelFactoryType` in place of a declared type name, ends with the T3a literal naming `DeclaredChannelFactory`, and contains no occurrence of the substring `configure a channel factory of type`.

**AC-10b** (FR-3 null `D`, combined arm, FR-1) — *A null declared type is a mismatch in the combined arm.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()])` and a `NullDeclaringSubscription` with `ChannelFactory` null,
When the rule is evaluated,
Then exactly one `Error` is produced, whose `Message` contains the literal `no ChannelFactoryType` and ends with the T3b literal listing both inner factories' display names in constructor order — and, as a companion assertion, calling `CreateSyncChannel` on that `CombinedChannelFactory` with the same subscription throws `ConfigurationException`, confirming the rule's verdict matches runtime.

**AC-10c** (FR-3 combined arm, FR-5 template T4) — *An empty combined factory yields an actionable remedy.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([])` and a `DeclaringSubscription` named `empty-sub` with `ChannelFactory` null,
When the rule is evaluated,
Then exactly one `Error` is produced for `empty-sub`, whose `Message` ends with the literal `— add a channel factory to the combined channel factory` and contains no occurrence of the substring `is one of:`.

**AC-11** (FR-4) — *The default in-memory configuration is silent.*
Given a plain `Subscription<FakeChannelFactoryRequest>` with `ChannelFactory` null and `options.DefaultChannelFactory` null,
When the rule is evaluated,
Then no findings are produced. The same holds when `options.DefaultChannelFactory` is explicitly `new InMemoryChannelFactory(new InternalBus(), TimeProvider.System)`.

### The finding message

**AC-12** (FR-5 item 1) — *The message names the offending subscription's own type.*
Given the configuration of AC-1,
When the rule is evaluated,
Then the `Message` contains the display name of the subscription's runtime type — `Paramore.Brighter.Subscription<Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest>` — rendered per FR-5's display-name format.

**AC-13** (FR-5 item 4, template T1) — *The two-way remedy, where both directions are legitimate.*
Given the configuration of AC-4 — a `DeclaringSubscription` (so `D == typeof(DeclaredChannelFactory)`, **not** the in-memory default) resolving to a `NonMatchingChannelFactory`,
When the rule is evaluated,
Then the `Message` ends with the literal `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is {F}`, with `{D}` the display name of `DeclaredChannelFactory` and `{F}` that of `NonMatchingChannelFactory`.

**AC-13a** (FR-5 item 4, template T3a) — *The in-memory case offers only the direction that fixes the defect.*
Given the configuration of AC-1 — a plain `Subscription<FakeChannelFactoryRequest>` (so `D == typeof(InMemoryChannelFactory)`) handed a `DeclaredChannelFactory`,
When the rule is evaluated,
Then the `Message` ends with the literal `— use a subscription type whose ChannelFactoryType is {F}`, naming `DeclaredChannelFactory`; and the message contains **no** occurrence of the substring `configure a channel factory of type`. The `{D}` half is suppressed entirely, per FR-5 template T3a — advising the developer to configure an `InMemoryChannelFactory` would be advising the C-2 failure.

**AC-13b** (FR-5 item 4, template T2) — *The combined arm lists the alternatives.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new NonMatchingChannelFactory(), new AlphaBus.ChannelFactory()])` and a `DeclaringSubscription` named `sub-a` with `ChannelFactory` null — so `D == typeof(DeclaredChannelFactory)`, which is neither inner factory, and `D != typeof(InMemoryChannelFactory)` —

> This Given is stated in full rather than inherited from AC-7. AC-7's inner set is fixed by AC-6's no-false-positive case as `[DeclaredChannelFactory, NonMatchingChannelFactory]`, in which a `DeclaringSubscription` **matches** the first inner factory and produces no finding at all — the opposite of what this criterion needs to assert.

When the rule is evaluated,
Then the `Message` ends with the literal `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is one of: {F-list}`, with `{F-list}` the inner factories' display names in constructor order.

**AC-13c** (FR-5 item 4, template T3b) — *The combined arm also suppresses the in-memory half.*
Given the configuration of AC-7 — the `CombinedChannelFactory` of AC-6 with a plain `Subscription<FakeChannelFactoryRequest>`, so `D == typeof(InMemoryChannelFactory)` and no inner factory matches,
When the rule is evaluated,
Then the `Message` ends with the literal `— use a subscription type whose ChannelFactoryType is one of: {F-list}`, listing both inner factories' display names in constructor order; and the message contains **no** occurrence of the substring `configure a channel factory of type`.

**AC-14** (FR-5 type display format) — *Display names carry no assembly identity.*
Given any finding produced by the rule for a closed generic subscription,
When its `Message` is inspected,
Then the message contains no occurrence of `Version=`, `Culture=` or `PublicKeyToken=`, and no occurrence of a backtick-arity suffix such as `` `1 ``.

**AC-15** (FR-5 items 2-3) — *Same-named factory types in different namespaces are distinguishable.*
Given an `AlphaBus.AlphaSubscription` (declaring `…TestDoubles.AlphaBus.ChannelFactory`) handed a `…TestDoubles.BetaBus.ChannelFactory` — two distinct types both simply named `ChannelFactory`,
When the rule is evaluated,
Then the `Message` contains both namespace-qualified display names in full, and no occurrence of the token `ChannelFactory` appears that is neither immediately preceded by a `.` nor part of the token `ChannelFactoryType`.

The `ChannelFactoryType` carve-out is required: FR-5's remedy clause, which AC-13 makes the message end with, contains that token preceded by a space. Without the carve-out AC-13 and AC-15 could not both pass.

**"Token" means a match at a word boundary, and the assertion is normative as a regex.** A composite identifier that merely *ends* in `ChannelFactory` — `InMemoryChannelFactory`, `CombinedChannelFactory`, `DeclaredChannelFactory` — is a different token and is **not** an occurrence. Without this, the criterion would be unimplementable: FR-5's own body renders `Paramore.Brighter.InMemoryChannelFactory` in this feature's headline case, so a naive `Message.Contains("ChannelFactory")` reading would fail AC-13a and AC-15 simultaneously. The assertion MUST therefore be written as

```
Regex.Matches(message, @"(?<![.\w])ChannelFactory(?!Type)")
```

and MUST find no match. This rule is asserted in `Core.Tests`, the only project whose acceptance criteria produce a `Message` for it to run against — the AC-25/AC-26 gateway criteria assert assignability, inner-factory selection, or the absence of findings, none of which renders a message. Writing the assertion any other way is a defect in the test, not in the message.

### Severity and blocking

**AC-16** (FR-6) — *An Error blocks startup under `throwOnError: true`.*
Given a host configured with `AddConsumers` containing one mismatched subscription (as in AC-1, using the doubles) and `ValidatePipelines(throwOnError: true)`,
When the host starts,
Then startup fails and the reported findings include the mismatch `Error`. (Lives in `tests/Paramore.Brighter.Extensions.Tests` — see C-9.)

**AC-17** (FR-6) — *The same configuration does not block under `throwOnError: false`.*
Given the configuration of AC-16 with `ValidatePipelines(throwOnError: false)`,
When the host starts,
Then the host starts successfully and the mismatch `Error` is present in the validation results.

**AC-17a** (FR-6, NFR-4) — *A disabled validation run evaluates nothing.*
Given the mismatched configuration of AC-16 with `ValidatePipelines(enabled: false)`,
When the host starts,
Then the host starts successfully and no validation results are produced by any rule. (Verifiable: `BrighterPipelineValidationExtensions.cs:58-60` returns the builder untouched when `enabled` is false.)

**AC-18** (FR-6) — *Existing rules are unaffected.*
Given a subscription whose `RequestType` has no registered handler and whose channel factory is correctly matched,
When `ValidatePipelines(throwOnError: true)` runs,
Then the `HandlerRegistered` rule still produces exactly one `Error` with its existing message, and this feature contributes no additional finding.

**AC-19** (FR-1, C-7) — *A subscription with a null `RequestType` is still checked.*
Given a `DeclaringSubscription` constructed with `getRequestType:` a mapping function (so `requestType` may be null) and `messagePumpType: MessagePumpType.Proactor` — both required, or the base `Subscription` constructor throws `ConfigurationException` — such that `RequestType` is null, handed a `NonMatchingChannelFactory`,
When the rule is evaluated,
Then exactly one `Error` is produced (the rule does not skip null-`RequestType` subscriptions).

### The five transport corrections

Each of AC-20 to AC-26f lives in the corresponding gateway test project and requires no infrastructure. All are reflection-only except AC-26f, which constructs a channel factory and evaluates the rule (construction only — no channel is created).

**AC-20** (FR-7) — Given `new GcpPubSubSubscription<TestRequest>(new SubscriptionName("t"), new ChannelName("t"), new RoutingKey("t"), messagePumpType: MessagePumpType.Proactor)` — the three positional arguments and the explicit pump type are both required, since `GcpPubSubSubscription<T>` defaults `messagePumpType` to `Unknown` (`GcpPubSubSubscription.cs:164-180`) and `Subscription.cs:213` rejects that — When `ChannelFactoryType` is read, Then it equals `typeof(GcpPubSubChannelFactory)`.

**AC-21** (FR-8) — Given an `MqttSubscription<TestRequest>`, When `ChannelFactoryType` is read, Then it equals `typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)`.

**AC-22** (FR-9) — Given a `SqsSubscription<TestRequest>` (AWSSQS), When `ChannelFactoryType` is read, Then it equals `typeof(Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory)`.

**AC-23** (FR-10) — Given a `SqsSubscription<TestRequest>` (AWSSQS.V4), When `ChannelFactoryType` is read, Then it equals `typeof(Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory)`.

**AC-24** (FR-11) — Given `new PostgresSubscription<TestRequest>(new SubscriptionName("t"), new ChannelName("t"), new RoutingKey("t"), messagePumpType: MessagePumpType.Proactor)` — `PostgresSubscription<T>` likewise defaults `messagePumpType` to `Unknown` (`PostgresSubscription.cs:146-158`) — When `ChannelFactoryType` is read, Then it equals `typeof(PostgresChannelFactory)`.

*Note on the Givens*: the required constructor arguments differ per transport. AC-21 (MQTT) and AC-22/AC-23 (AWS) need no explicit `messagePumpType` — those generic constructors already default to `Proactor` (`MqttSubscription.cs:132`, `SqsSubscription.cs:201`).

**AC-25** (FR-7 to FR-11) — *All five declared types are real channel factories.*
Given the corrected subscription type for a transport,
When `ChannelFactoryType` is read,
Then `typeof(IAmAChannelFactory).IsAssignableFrom(type)` is true and the type is not `typeof(InMemoryChannelFactory)`.

This is asserted **once per transport, in that transport's own test project** — AC-25a `Gcp.Tests`, AC-25b `MQTT.Tests`, AC-25c `AWS.Tests`, AC-25d `AWS.V4.Tests`, AC-25e `PostgresSQL.Tests`. No test project references more than one gateway assembly (C-9), so it cannot be a single test over "all five".

**AC-26** (FR-7 to FR-11, FR-3) — *`CombinedChannelFactory` can now route each corrected subscription.*
Given, for one corrected transport, a `CombinedChannelFactory` constructed with an instance of that transport's real channel factory (construction only — **no** channel is created, so no broker, database or network is touched),
When the inner-factory selection predicate `f.GetType() == subscription.ChannelFactoryType` is evaluated for that transport's subscription,
Then exactly one inner factory is selected, where before the correction none was.

Asserted once per transport — AC-26a `Gcp.Tests`, AC-26b `MQTT.Tests`, AC-26c `AWS.Tests`, AC-26d `AWS.V4.Tests`, AC-26e `PostgresSQL.Tests`. This clause needs only `Paramore.Brighter`, so every one of the five can host it.

**AC-26f** (FR-7 to FR-11, FR-1) — *The rule reports no findings for a corrected transport.*
Given a corrected subscription and a `CombinedChannelFactory` containing its real channel factory,
When the rule is evaluated,
Then no findings are produced.

This needs `Paramore.Brighter.ServiceActivator`, which `AWS.Tests`, `AWS.V4.Tests` and `MQTT.Tests` reference but `Gcp.Tests` and `PostgresSQL.Tests` do **not** (C-9). It is therefore asserted in those three projects for their transports; GCP and Postgres are covered equivalently in `Core.Tests` against the doubles (AC-2, AC-6), since the rule's logic is transport-agnostic — what is transport-specific is the declared type, which AC-25 already pins.

*Note*: AC-26 asserts the routing **decision**, not channel creation. Calling `CreateSyncChannel`/`CreateAsyncChannel` would open a real connection (MQTT connects in `MqttMessageConsumer`'s constructor; `GcpPubSubChannelFactory` calls `EnsureSubscriptionExistsAsync`), which NFR-3 forbids.

### The regression guard

**AC-27** (FR-12) — *Every shipped gateway subscription passes both conditions, in every gateway assembly.*
Given every non-abstract type assignable to `Subscription` in a shipped messaging-gateway assembly,
When the sweep reads its `ChannelFactoryType`,
Then the type implements `IAmAChannelFactory` and is not `typeof(InMemoryChannelFactory)`, for every type found, with a base/derived pair reported at most once **where the derived type declares no `ChannelFactoryType` of its own** (FR-12's Scope); a derived type declaring its own override is reported in its own right.

In each of the twelve shipped gateway assemblies this resolves to **exactly one** reported subject — the non-generic base that carries the override — because all nine existing overrides, and the three FR-9 to FR-11 adds, sit on that base while every generic derived type declares none. The sweep test MAY therefore assert the reported subject set exactly, which is a stronger guard than "at most once" and additionally catches a sweep aimed at the wrong assembly. An assembly that later declares an override on a second type acquires a second expected subject; that is a deliberate, visible change.

There MUST be **one such sweep test in each of the twelve gateway test projects** enumerated in C-9 — a test project can only sweep assemblies it references, and none references more than one gateway, so a single sweep in one project would leave eleven assemblies unguarded. A newly added thirteenth gateway acquires its own sweep.

**AC-28** (FR-12) — *The guard catches both failure shapes, exercised over synthetic types.*
Given two synthetic `Subscription` subclasses declared in a **test** assembly — one with no `ChannelFactoryType` override (so it inherits `InMemoryChannelFactory`), and one overriding it with a type that does not implement `IAmAChannelFactory`,
When FR-12's predicate is applied to each,
Then it fails both, naming the offending subscription type and the type it declared.

The predicate is exercised over synthetic types precisely because AC-27 requires no shipped gateway assembly to contain either shape; a guard whose failure path could only be demonstrated by shipping a defective gateway would be untestable.

**AC-29** (FR-12, NFR-3) — *The sweep constructs nothing.*
Given the sweep,
When it obtains each `ChannelFactoryType`,
Then no subscription constructor is invoked (so no `ConfigurationException` from the base `Subscription` constructor can occur) and no broker, database or network is contacted.

### Determinism and hygiene

**AC-30** (FR-13) — *Findings are one-per-subscription, ordered and deterministic.*
Given two mismatched subscriptions — `sub-a` a `DeclaringSubscription` and `sub-b` a `NonMatchingSubscription`, in that order in `options.Subscriptions` — evaluated against a `CombinedChannelFactory` whose three inner factories are `[DerivedChannelFactory, AlphaBus.ChannelFactory, BetaBus.ChannelFactory]` (chosen so neither subscription matches),
When the rule is evaluated twice,
Then each run produces exactly two findings — one per subscription, in the order `sub-a`, `sub-b` — with byte-identical messages across the two runs.

**AC-31** (NFR-3) — *No infrastructure is required.*
Given the full set of tests introduced by this feature,
When they run in an environment with no broker, database, or network access,
Then they all pass.

## Additional Context

- **Amendments after approval.** This document was approved, then re-opened **four times** and re-approved. The first re-opening followed the round-2 adversarial review of ADR 0072, which found that the ADR was deciding things the requirements owned. Rather than let the ADR deviate, the following were amended here and nothing else was touched:
  - **FR-3** gains the null-`D` clause for both arms (D4).
  - **FR-5** item 2 admits `no ChannelFactoryType` when `D` is null; the remedy table is restated as five ordered, total conditions, adding **T4** for an empty candidate set.
  - **NFR-6** and **C-8** gain **C-13**, the new direct-arm breakage.
  - **C-9** gains one double, `NullDeclaringSubscription`.
  - **AC-10a**, **AC-10b** and **AC-10c** are new; **AC-15** now defines "token" as a word-boundary match and pins the assertion to a regex.

  A second, narrower re-opening followed the round-5 review, which found ADR 0072 citing a **D5** that this document had never recorded — and using it to bound an approved acceptance criterion. Restricted to exactly that:
  - **D5** is added to *Maintainer decisions already taken*, stating the nested-composite rendering that is accepted and, explicitly, that it licenses nothing about AC-7.
  - **AC-7** and **AC-10** gain a cross-reference to D5. No Given/When/Then is altered: AC-7's assertion and AC-10's verdict are unchanged.

  A **fourth** re-opening followed the round-4 review of **ADR 0073**, which found that this document stated the base/derived dedup obligation **unconditionally** while the only mechanism that can satisfy it is necessarily conditional — subsumption may drop a derived type only when that type declares no override of its own, since a derived type that declares one may disagree with its base and must be reported. The ADR had been made to assert the unconditional reading, and would then have failed a correct sweep. The requirement was the imprecise half. Restricted to exactly that:
  - **FR-12's Scope** qualifies "reported at most once" with the condition, and says why an unconditional reading would invert the guard's purpose.
  - **AC-27** carries the same qualification in its Then clause, and records that the twelve shipped assemblies each resolve to exactly one reported subject — which licenses (without requiring) an exact-subject-set assertion in the sweep test. No other criterion is altered.

  A third, still narrower re-opening followed the round-7 review, which found **AC-15**'s test-site note naming a second site that cannot exist. Restricted to exactly that:
  - **AC-15**'s note no longer claims the gateway projects assert its regex "for AC-26f". AC-26f's Then is "no findings are produced", so it renders no `Message` for the regex to run against, and no criterion in the AC-25/AC-26 family asserts a `Message` at all. The note now names `Core.Tests` as the only site, and says why. AC-15's normative regex is unchanged.

- **Origin.** Issue #4334, prompted by #4331, in which two sample applications had shipped with `Subscription<T>` where an `MsSqlSubscription<T>` was required and had never been able to start. Compilation succeeded, CI compiled the samples, and nothing detected the defect until someone tried to run them.
- **Maintainer decisions already taken** (not to be re-opened by design or implementation):
  - **D1** — severity is `ValidationSeverity.Error` (see C-1).
  - **D3** — a subscription type defined *outside* this repository that declares no `ChannelFactoryType` override will be an `Error` that blocks startup, and that is **accepted** rather than softened (C-10). Rationale: custom transports are rare, the fix is one line, and softening the rule for an unverifiable population would weaken it exactly where the AWS SQS and Postgres defects lived until this specification. Raised by the round-2 adversarial review; decided by the maintainer.
  - **D4** — a `ChannelFactoryType` override that returns `null` is an `Error` in **both** arms, not skipped (FR-3's null-`D` clause). The combined arm's verdict already followed from FR-3's *iff*; the direct arm's is new breakage, accepted as C-13. Raised by the round-2 adversarial review of ADR 0072; decided by the maintainer.
  - **D5** — the nested-composite message may name `Paramore.Brighter.CombinedChannelFactory` among the types the subscription will be handed, even though that type does not in fact route. For `CombinedChannelFactory([CombinedChannelFactory([…])])` the inner composite's own type is what `FactoryTypes` reports, so it is what FR-5's `{F-list}` renders; a subscription declaring it is selected by the outer composite and then rejected by the inner one. Filtering it out would leave an empty candidate set and select **T4**, whose wording says *less* about what is actually configured, so the rendering is **accepted** rather than special-cased. This is a message-quality limitation, not a change to any verdict: AC-10's `Error` is unaffected, and AC-7 continues to hold in its own non-nested configuration — D5 licenses nothing about AC-7. Raised by the round-2 adversarial review of ADR 0072, re-examined in round 3; decided by the maintainer.
  - **D2** — correcting every wrong or missing `ChannelFactoryType` declaration ships with the rule, in this specification (FR-7 to FR-11), rather than being deferred. Originally scoped to the two *wrong* overrides (`GcpPubSubSubscription`, `MqttSubscription`); the adversarial review of these requirements found three transports with **no** override at all (`SqsSubscription` in both AWS packages, `PostgresSubscription`), which under D1 would have blocked working hosts. The maintainer widened D2 to cover all five.
- **Grounding references** (HOW belongs in the ADR):
  - `src/Paramore.Brighter/Subscription.cs:48` (`ChannelFactory`), `:172` (`ChannelFactoryType`).
  - `src/Paramore.Brighter/CombinedChannelFactory.cs:14`, `:34`, `:46`, `:59` — exact-type-equality routing and the `ConfigurationException` it throws.
  - `src/Paramore.Brighter/IAmConsumerOptions.cs:12` (`DefaultChannelFactory`).
  - `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` — the four existing rules; `PumpHandlerMatch` is the style model for message and `Source` construction.
  - `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:159` (default-factory fallback), `:199-229` (`RegisterConsumerValidationSpecs`).
  - `src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs:146-148` — the default factory is *written into* subscriptions whose `ChannelFactory` is null, which is what FR-2's precedence mirrors and FR-2a's invariance covers.
  - `src/Paramore.Brighter.MessagingGateway.AWSSQS/ChannelFactory.cs:44`, `:98`, `:208`; `…AWSSQS.V4/ChannelFactory.cs:44`, `:98`, `:208`; `…Postgres/PostgresChannelFactory.cs:11`, `:18`, `:39`, `:60` — the three transports with no override, and the downcasts that make their mismatches fatal.
  - `src/Paramore.Brighter/ValidationError.cs:33` (`record ValidationError(ValidationSeverity Severity, string Source, string Message)`) and `src/Paramore.Brighter/Specification.cs:35` (`ISpecification<TData>`).
  - `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs:53`, `:75`, `:99` — the downcast-and-throw pattern the rule pre-empts.
  - `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs:83` — an in-repo multi-bus configuration (Kafka + RMQ.Async) whose subscriptions must not be flagged.
