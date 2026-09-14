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
2. the declared channel factory type `D`, as `Type.FullName`;
3. the type(s) it will actually be handed, as `Type.FullName` — in the direct arm the effective factory's type; in the combined arm the `Type.FullName` of every inner factory in the candidate factory set, in constructor order, comma-separated;
4. a remedy clause instructing the developer either to use the subscription type that the effective factory requires, or to configure a channel factory of type `D`.

**Type display format (normative).** `Type.FullName` is NOT usable directly: for a closed generic such as `Subscription<TestRequest>` it returns
`Paramore.Brighter.Subscription`1[[Greetings.Ports.Events.TestRequest, SomeAssembly, Version=…, Culture=neutral, PublicKeyToken=null]]`,
whose assembly identity payload defeats NFR-2. The rule MUST therefore render every type in items 1-3 with a **display name** defined as:
- a non-generic type: its `Type.FullName` (namespace-qualified, no assembly identity);
- a closed generic type: the namespace-qualified name with the ``​`n`` arity suffix removed, followed by its type arguments' display names in angle brackets, comma-separated — e.g. `Paramore.Brighter.Subscription<Greetings.Ports.Events.TestRequest>`.

A display name MUST NOT contain `Version=`, `Culture=` or `PublicKeyToken=`, and MUST NOT be a bare `Type.Name`.

**Remedy clause (normative template).** Item 4 MUST be rendered as, literally:
`— either configure a channel factory of type {D-display-name}, or use a subscription type whose ChannelFactoryType is {F-display-names}`
where `{F-display-names}` is the effective factory's display name in the direct arm, or the comma-separated display names of the inner factories in constructor order in the combined arm.

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

**Mechanism (normative).** The sweep MUST obtain `ChannelFactoryType` **without invoking a subscription constructor**. `ChannelFactoryType` is an instance virtual property (`Subscription.cs:172`), while the base `Subscription` constructor throws `ConfigurationException` unless `messagePumpType` is `Reactor`/`Proactor` and a `requestType` or `getRequestType` is supplied — and every transport subscription defaults `messagePumpType` to `Unknown` with `requestType` null, so `Activator.CreateInstance` cannot be used. The sweep MUST therefore use an uninitialised instance (`System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject`) or an equivalent construction-free mechanism, and MUST NOT require broker or network access (NFR-3).

**The check MUST be exposed as a pure predicate** over `(Type subscriptionType, Type declaredFactoryType)` returning pass/fail plus a reason, so that the negative cases (AC-28) can be exercised over synthetic types declared in a test assembly. A guard whose failure path can only be demonstrated by shipping a defective gateway is not testable.

**Scope.** Generic subscription types MUST be closed against a representative `IRequest` before the property is read where the mechanism requires it, honouring each type's constraints (`GcpPubSubSubscription<T> where T : class, IRequest` differs from `MqttSubscription<T> where T : IRequest`). A base/derived pair in the same assembly (e.g. `RocketSubscription` and `RocketMqSubscription<T>`) MUST be reported at most once.
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
- **NFR-6 — Behaviour preserved for working configurations.** No configuration that starts successfully today may be made to fail by this feature, with exactly one deliberate exception: **C-2** (a transport-specific subscription resolving to an in-memory factory). Stated without the "genuinely correct" qualifier deliberately — that qualifier made the requirement unfalsifiable, because the specification would then decide for itself what counts as correct. The AWS SQS / Postgres / GCP / MQTT configurations that a naive reading of D1 would have broken are **not** exceptions: FR-7 to FR-11 correct their declarations so they pass, which is the whole reason D2 is in scope.
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
- **C-8 — Versioning.** Targets Brighter V10.X. FR-7 to FR-11 change the value returned by a public virtual property (or add the override where none existed); this is a behaviour change (a fix), not a binary-breaking API change, and must be noted in the release notes. FR-9 to FR-11 in particular make AWS SQS, AWS SQS V4 and Postgres subscriptions routable by `CombinedChannelFactory` for the first time.
- **C-9 — Test placement, test doubles, and their request types.** `tests/Paramore.Brighter.Core.Tests` references only `Paramore.Brighter`, `Paramore.Brighter.BoxProvisioning`, `Paramore.Brighter.Extensions.DependencyInjection`, `Paramore.Brighter.Mediator`, `Paramore.Brighter.Outbox.Hosting`, `Paramore.Brighter.ServiceActivator` and `Paramore.Brighter.Testing` — **no** `MessagingGateway.*` assembly and **not** `ServiceActivator.Extensions.DependencyInjection`. The rule's behavioural tests (AC-1 to AC-19) MUST therefore be written against purpose-built doubles, not real gateway types, and live in `tests/Paramore.Brighter.Core.Tests/Validation/` with the existing `When_…` naming.

  **The doubles (a closed set — no AC may use one not listed here).** One class per file under `Validation/TestDoubles/`, per `.agent_instructions/testing.md`:

  | Double | Purpose |
  |---|---|
  | `FakeTransportChannelFactory : IAmAChannelFactory` | The "transport" factory. Throws if any channel is actually created, so a test straying into channel creation fails loudly. |
  | `FakeDerivedChannelFactory : FakeTransportChannelFactory` | The direct-arm subclass case (AC-8, AC-9). |
  | `FakeOtherChannelFactory : IAmAChannelFactory` | An unrelated factory, for mismatch and multi-bus cases. |
  | `FakeTransportSubscription : Subscription` | Overrides `ChannelFactoryType` to `typeof(FakeTransportChannelFactory)`. |
  | `FakeOtherSubscription : Subscription` | Overrides `ChannelFactoryType` to `typeof(FakeOtherChannelFactory)`. Required by AC-6. |
  | `AlphaBus.ChannelFactory` / `BetaBus.ChannelFactory` | Two factories **both named `ChannelFactory`** in different namespaces under `…TestDoubles.AlphaBus` / `…TestDoubles.BetaBus`, plus `AlphaBus.AlphaSubscription` declaring the former. Required by AC-15, which is the same-simple-name case. |

  **Request types.** Each subscription double takes its own distinct request type — `FakeChannelFactoryRequest`, `FakeOtherRequest`, `AlphaRequest` — declared in `…Validation.TestDoubles`, one per file, so assembly scans cannot collide (`.agent_instructions/testing.md`). **No acceptance criterion may use `TestRequest`**: that type exists only under `samples/WebAPI/WebAPI_Dynamo` in namespaces `GreetingsApp.Requests` / `SalutationApp.Requests`, is not visible to any test project, and the namespace `Greetings.Ports.Events` contains `GreetingEvent`, not `TestRequest`.

  **Host-start behaviour (AC-16, AC-17)** needs `ServiceActivator.Extensions.DependencyInjection` and belongs in `tests/Paramore.Brighter.Extensions.Tests`, which already references it. That project does **not** reference `Paramore.Brighter.Core.Tests` — test projects here do not reference one another — so it MUST declare **its own copies** of the doubles it needs, in its own namespace, with their own distinct request types. This duplication is deliberate and stated so an implementer does not discover it mid-task.

  **Real-type tests.** The five correction criteria (AC-20 to AC-26e) and the FR-12 sweep (AC-27) name real gateway types and live in the gateway test project for that assembly, reflection-only, requiring no infrastructure (NFR-3). The twelve shipped gateway assemblies map to twelve test projects:

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

Unless a criterion names a real gateway type, it is written against the test doubles defined in C-9 — the closed set `FakeTransportSubscription`, `FakeOtherSubscription`, `FakeTransportChannelFactory`, `FakeDerivedChannelFactory`, `FakeOtherChannelFactory`, `AlphaBus.ChannelFactory`, `BetaBus.ChannelFactory`, `AlphaBus.AlphaSubscription` — with their own request types (`FakeChannelFactoryRequest`, `FakeOtherRequest`, `AlphaRequest`), and lives in `tests/Paramore.Brighter.Core.Tests/Validation/`. `TestRequest` in the transport-correction criteria means a request type local to that gateway test project. No criterion uses `GreetingMade` (C-9).

### The rule's behaviour

**AC-1** (FR-1, FR-5, FR-6) — *A mismatch is one Error, correctly sourced.*
Given a `Subscription<FakeChannelFactoryRequest>` named `greeting-sub` with `ChannelFactory` null, and `options.DefaultChannelFactory` set to a `FakeTransportChannelFactory`,
When the rule is evaluated,
Then exactly one `ValidationError` is produced, with `Severity == ValidationSeverity.Error` and `Source == "Subscription 'greeting-sub'"`.

**AC-2** (FR-1, FR-3 direct arm) — *A matching subscription passes.*
Given a `FakeTransportSubscription` named `greeting-sub` with `ChannelFactory` null, and `options.DefaultChannelFactory` set to a `FakeTransportChannelFactory`,
When the rule is evaluated,
Then no findings are produced.

**AC-3** (FR-2) — *The per-subscription factory overrides the default.*
Given `options.DefaultChannelFactory` set to a `FakeOtherChannelFactory`, and a `FakeTransportSubscription` named `sub-a` whose `ChannelFactory` is set to a `FakeTransportChannelFactory`,
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
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new FakeTransportChannelFactory(), new FakeOtherChannelFactory()])` and two subscriptions — a `FakeTransportSubscription` named `sub-a` and a `FakeOtherSubscription` named `sub-b`, both with `ChannelFactory` null,
When the rule is evaluated,
Then no findings are produced for either subscription.

**AC-7** (FR-3 combined arm, FR-5) — *A subscription no inner factory can serve is an Error naming the inner factories.*
Given the `CombinedChannelFactory` of AC-6 and a plain `Subscription<FakeChannelFactoryRequest>` named `greeting-sub`,
When the rule is evaluated,
Then exactly one `Error` is produced for `greeting-sub`, whose `Message` contains the display names of both inner factories in constructor order, and does not name `Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed.

**AC-8** (FR-3 direct arm) — *A user subclass of a channel factory is accepted.*
Given a `FakeDerivedChannelFactory` (deriving from `FakeTransportChannelFactory`) set as `options.DefaultChannelFactory`, and a `FakeTransportSubscription` with `ChannelFactory` null,
When the rule is evaluated,
Then no findings are produced.

**AC-9** (FR-3 combined arm) — *The same subclass inside a `CombinedChannelFactory` is flagged, mirroring runtime.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new FakeDerivedChannelFactory()])` and a `FakeTransportSubscription` with `ChannelFactory` null,
When the rule is evaluated,
Then exactly one `Error` is produced — and, as a companion assertion, calling `CreateSyncChannel` on that `CombinedChannelFactory` with the same subscription throws `ConfigurationException` (it throws at `CombinedChannelFactory.cs:35-38`, before dispatching to any inner factory, so no channel is created).

**AC-10** (FR-3, no recursion) — *Nested combined factories are not unwrapped.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new CombinedChannelFactory([new FakeTransportChannelFactory()])])` and a `FakeTransportSubscription`,
When the rule is evaluated,
Then exactly one `Error` is produced, matching the runtime behaviour of `CombinedChannelFactory`, which also fails to route this subscription.

**AC-11** (FR-4) — *The default in-memory configuration is silent.*
Given a plain `Subscription<FakeChannelFactoryRequest>` with `ChannelFactory` null and `options.DefaultChannelFactory` null,
When the rule is evaluated,
Then no findings are produced. The same holds when `options.DefaultChannelFactory` is explicitly `new InMemoryChannelFactory(new InternalBus(), TimeProvider.System)`.

### The finding message

**AC-12** (FR-5 item 1) — *The message names the offending subscription's own type.*
Given the configuration of AC-1,
When the rule is evaluated,
Then the `Message` contains the display name of the subscription's runtime type — `Paramore.Brighter.Subscription<Paramore.Brighter.Core.Tests.Validation.TestDoubles.FakeChannelFactoryRequest>` — rendered per FR-5's display-name format.

**AC-13** (FR-5 item 4) — *The message carries the pinned remedy clause.*
Given the configuration of AC-1,
When the rule is evaluated,
Then the `Message` ends with the literal `— either configure a channel factory of type {D}, or use a subscription type whose ChannelFactoryType is {F}`, where `{D}` is the display name of the declared channel factory type and `{F}` the display name of the effective factory.

**AC-13a** (FR-5 item 4) — *The remedy names the effective factory, not the in-memory default, in the headline case.*
Given the configuration of AC-1 — a plain `Subscription<FakeChannelFactoryRequest>` (so `D == typeof(InMemoryChannelFactory)`) handed a `FakeTransportChannelFactory`,
When the rule is evaluated,
Then the `{F}` half of the remedy names `FakeTransportChannelFactory`, and the message does **not** advise the developer to configure an `InMemoryChannelFactory` as the way to make the subscription work. (This is the case that made the earlier `{D}`-only template give backwards advice.)

**AC-14** (FR-5 type display format) — *Display names carry no assembly identity.*
Given any finding produced by the rule for a closed generic subscription,
When its `Message` is inspected,
Then the message contains no occurrence of `Version=`, `Culture=` or `PublicKeyToken=`, and no occurrence of a backtick-arity suffix such as `` `1 ``.

**AC-15** (FR-5 items 2-3) — *Same-named factory types in different namespaces are distinguishable.*
Given an `AlphaBus.AlphaSubscription` (declaring `…TestDoubles.AlphaBus.ChannelFactory`) handed a `…TestDoubles.BetaBus.ChannelFactory` — two distinct types both simply named `ChannelFactory`,
When the rule is evaluated,
Then the `Message` contains both namespace-qualified display names in full, and no occurrence of the token `ChannelFactory` appears that is neither immediately preceded by a `.` nor part of the token `ChannelFactoryType`.

The `ChannelFactoryType` carve-out is required: FR-5's remedy clause, which AC-13 makes the message end with, contains that token preceded by a space. Without the carve-out AC-13 and AC-15 could not both pass.

### Severity and blocking

**AC-16** (FR-6) — *An Error blocks startup under `throwOnError: true`.*
Given a host configured with `AddConsumers` containing one mismatched subscription (as in AC-1, using the doubles) and `ValidatePipelines(throwOnError: true)`,
When the host starts,
Then startup fails and the reported findings include the mismatch `Error`. (Lives in `tests/Paramore.Brighter.Extensions.Tests` — see C-9.)

**AC-17** (FR-6) — *The same configuration does not block under `throwOnError: false`.*
Given the configuration of AC-16 with `ValidatePipelines(throwOnError: false)`,
When the host starts,
Then the host starts successfully and the mismatch `Error` is present in the validation results.

**AC-18** (FR-6) — *Existing rules are unaffected.*
Given a subscription whose `RequestType` has no registered handler and whose channel factory is correctly matched,
When `ValidatePipelines(throwOnError: true)` runs,
Then the `HandlerRegistered` rule still produces exactly one `Error` with its existing message, and this feature contributes no additional finding.

**AC-19** (FR-1, C-7) — *A subscription with a null `RequestType` is still checked.*
Given a `FakeTransportSubscription` constructed with `getRequestType:` a mapping function (so `requestType` may be null) and `messagePumpType: MessagePumpType.Proactor` — both required, or the base `Subscription` constructor throws `ConfigurationException` — such that `RequestType` is null, handed a `FakeOtherChannelFactory`,
When the rule is evaluated,
Then exactly one `Error` is produced (the rule does not skip null-`RequestType` subscriptions).

### The five transport corrections

Each of AC-20 to AC-26 lives in the corresponding gateway test project, is reflection-only, and requires no infrastructure.

**AC-20** (FR-7) — Given a `GcpPubSubSubscription<TestRequest>`, When `ChannelFactoryType` is read, Then it equals `typeof(GcpPubSubChannelFactory)`.

**AC-21** (FR-8) — Given an `MqttSubscription<TestRequest>`, When `ChannelFactoryType` is read, Then it equals `typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)`.

**AC-22** (FR-9) — Given a `SqsSubscription<TestRequest>` (AWSSQS), When `ChannelFactoryType` is read, Then it equals `typeof(Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory)`.

**AC-23** (FR-10) — Given a `SqsSubscription<TestRequest>` (AWSSQS.V4), When `ChannelFactoryType` is read, Then it equals `typeof(Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory)`.

**AC-24** (FR-11) — Given a `PostgresSubscription<TestRequest>`, When `ChannelFactoryType` is read, Then it equals `typeof(PostgresChannelFactory)`.

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
Then the type implements `IAmAChannelFactory` and is not `typeof(InMemoryChannelFactory)`, for every type found, with a base/derived pair reported at most once.

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
Given two mismatched subscriptions `sub-a` and `sub-b`, in that order in `options.Subscriptions`, evaluated against a `CombinedChannelFactory` with three inner factories,
When the rule is evaluated twice,
Then each run produces exactly two findings — one per subscription, in the order `sub-a`, `sub-b` — with byte-identical messages across the two runs.

**AC-31** (NFR-3) — *No infrastructure is required.*
Given the full set of tests introduced by this feature,
When they run in an environment with no broker, database, or network access,
Then they all pass.

## Additional Context

- **Origin.** Issue #4334, prompted by #4331, in which two sample applications had shipped with `Subscription<T>` where an `MsSqlSubscription<T>` was required and had never been able to start. Compilation succeeded, CI compiled the samples, and nothing detected the defect until someone tried to run them.
- **Maintainer decisions already taken** (not to be re-opened by design or implementation):
  - **D1** — severity is `ValidationSeverity.Error` (see C-1).
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
