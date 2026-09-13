# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4334

## Problem Statement

As a developer configuring a Brighter consumer (ServiceActivator) against a messaging gateway, I would like `ValidatePipelines()` to tell me at startup when a `Subscription` is incompatible with the channel factory it will actually be handed, so that I find out from a named, actionable validation finding rather than from a `ConfigurationException` thrown deep inside the Dispatcher when it builds its channels — or, worse, from a consumer that appears to start but silently consumes from the wrong transport.

### The defect this catches

Every transport's `Subscription` subclass carries transport-specific configuration, and most transport `ChannelFactory` implementations downcast the `Subscription` they are handed. For example, `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs` does this in all three creation methods (lines 46, 66 and 88):

```csharp
MsSqlSubscription? rmqSubscription = subscription as MsSqlSubscription;
if (rmqSubscription == null)
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

There are nine `override Type ChannelFactoryType` declarations in `src/`. **Two of them name a type that is not an `IAmAChannelFactory` at all** (Table 1):

| Subscription | Declares `ChannelFactoryType` | Is that an `IAmAChannelFactory`? |
|---|---|---|
| `RocketMqSubscription` | `RocketMqChannelFactory` | yes |
| `RedisSubscription` | `ChannelFactory` (Redis) | yes |
| `AzureServiceBusSubscription` | `AzureServiceBusChannelFactory` | yes |
| `RmqSubscription` (RMQ.Sync) | `ChannelFactory` (RMQ.Sync) | yes |
| `RmqSubscription` (RMQ.Async) | `ChannelFactory` (RMQ.Async) | yes |
| `KafkaSubscription` | `ChannelFactory` (Kafka) | yes |
| `MsSqlSubscription` | `ChannelFactory` (MsSql) | yes |
| **`GcpPubSubSubscription`** (`GcpPubSubSubscription.cs:108`) | **`GcpPubSubConsumerFactory`** | **NO** — it is an `IAmAMessageConsumerFactory` (`GcpPubSubConsumerFactory.cs:15`). The channel factory is `GcpPubSubChannelFactory` (`GcpPubSubChannelFactory.cs:14-15`). |
| **`MqttSubscription`** (`MqttSubscription.cs:35`) | **`MqttMessageConsumerFactory`** | **NO** — it is an `IAmAMessageConsumerFactory` (`MqttMessageConsumerFactory.cs:30`). The channel factory is `Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory` (`ChannelFactory.cs:33`). |

Because `CombinedChannelFactory` matches on exact type equality, **a GCP Pub/Sub or MQTT subscription used with a `CombinedChannelFactory` can never match a registered factory** and always throws `ConfigurationException("No channel factory found for subscription …")`. Multi-transport configurations that include either of those two transports are broken today. Correcting the two overrides is part of this feature, not a follow-up (decision D2).

### Why nothing catches this today

`RegisterConsumerValidationSpecs` (`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:205-226`) registers four `ISpecification<Subscription>` rules — `PumpHandlerMatch`, `HandlerRegistered`, `RequestTypeSubtype` and `UnwrapTransformResolvable`, all defined in `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`. **None of them checks that a subscription is compatible with the channel factory it will be handed.** The data needed to check it is already present on every subscription.

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

3. **Two long-standing transport defects are fixed.** `GcpPubSubSubscription` and `MqttSubscription` are corrected to declare their real channel factories, so GCP Pub/Sub and MQTT subscriptions can be routed by `CombinedChannelFactory` for the first time — and so the new Error-severity rule does not fire on correct configurations of those two transports.

## Requirements

### Functional Requirements

**FR-1 — A channel-factory compatibility rule exists and is evaluated for every configured subscription.**
A new `ISpecification<Subscription>` MUST be added to `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`, alongside the existing four rules, and registered in `RegisterConsumerValidationSpecs` (`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:205-226`) so that it is evaluated once per configured subscription when `ValidatePipelines()` runs. For a subscription in mismatch (FR-3) the rule MUST produce exactly one `ValidationError` with `Severity = ValidationSeverity.Error` and `Source = $"Subscription '{subscription.Name}'"` — the same `Source` format the existing four rules use. For a compatible subscription the rule MUST produce no finding.
- Example: with `options.DefaultChannelFactory = new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(connection)` and a single configured `Subscription<GreetingMade>` named `greeting-sub`, the rule produces exactly one finding, of severity `Error`, whose `Source` is `Subscription 'greeting-sub'`.
- Example: replacing that subscription with `MsSqlSubscription<GreetingMade>` named `greeting-sub` produces zero findings.

**FR-2 — The effective channel factory is resolved as `subscription.ChannelFactory ?? options.DefaultChannelFactory ?? InMemoryChannelFactory`.**
The rule MUST determine the effective channel factory for a subscription using this precedence, which mirrors the runtime wiring (`DispatchBuilder` assigns the default only to subscriptions whose `ChannelFactory` is `null` — `src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs:146-148`; the DI path substitutes an in-memory factory for a null default — `ServiceCollectionExtensions.cs:159`):
1. If `subscription.ChannelFactory` is non-null, that instance is the effective channel factory. The per-subscription override takes precedence over the default and the default MUST NOT be consulted.
2. Otherwise, if `options.DefaultChannelFactory` (`src/Paramore.Brighter/IAmConsumerOptions.cs:12`) is non-null, that instance is the effective channel factory.
3. Otherwise, the effective channel factory is treated as an `InMemoryChannelFactory`, matching `ServiceCollectionExtensions.cs:159`.
- Example: `options.DefaultChannelFactory` is a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`, and subscription `rmq-sub` is an `RmqSubscription<GreetingMade>` (RMQ.Async) with `ChannelFactory` set to a `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory`. The effective factory for `rmq-sub` is the RMQ.Async one, it is compatible, and no finding is produced for `rmq-sub`.
- Example: the same configuration with `rmq-sub`'s `ChannelFactory` left `null` resolves to the MsSql default, which is a mismatch, and produces one `Error`.

**FR-3 — Compatibility is defined against the candidate factory set, with `CombinedChannelFactory` unwrapped one level.**
Given the effective channel factory `F` (FR-2) and the subscription's declared channel factory type `D`:
- **Combined arm.** If `F` is a `CombinedChannelFactory`, the candidate factory set is `F`'s inner factories, in the order supplied to its constructor. The subscription is **compatible** if and only if at least one inner factory `f` satisfies `f.GetType() == D` — exact type equality, mirroring `CombinedChannelFactory.cs:34/46/59` precisely. The rule MUST NOT compare `D` against `typeof(CombinedChannelFactory)`; doing so would flag every subscription in every multi-transport application. The rule MUST NOT recurse into a nested `CombinedChannelFactory` among the inner factories, because `CombinedChannelFactory` does not recurse at runtime either — a nested combined factory therefore matches only a subscription whose `D` is literally `typeof(CombinedChannelFactory)`.
- **Direct arm.** If `F` is not a `CombinedChannelFactory`, the candidate factory set is `{ F }`, and the subscription is **compatible** if and only if `D.IsAssignableFrom(F.GetType())` — that is, `F` is of the declared type or a subtype of it. Assignability (rather than exact equality) is used here so that an application supplying its own subclass of a transport's `ChannelFactory` is not falsely flagged; at runtime that subclass satisfies the gateway's downcast of the subscription and works. The two arms differ deliberately because the runtime behaviours they predict differ.
- Example (combined, compatible): `F = new CombinedChannelFactory([new Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory(...), new Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory(...)])` and the subscription is an `MsSqlSubscription<GreetingMade>` (`D = Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`). No finding.
- Example (combined, mismatch): the same `F` with a plain `Subscription<GreetingMade>` (`D = typeof(InMemoryChannelFactory)`). One `Error`, because no inner factory is an `InMemoryChannelFactory`.
- Example (direct, subclass accepted): `F = new MyAuditingMsSqlChannelFactory(...) : Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` with an `MsSqlSubscription<GreetingMade>`. No finding.
- Example (combined, subclass rejected): the same subclass instance as the sole inner factory of a `CombinedChannelFactory`, with an `MsSqlSubscription<GreetingMade>`. One `Error` — correctly, because `CombinedChannelFactory` would throw `ConfigurationException("No channel factory found for subscription …")` at runtime.

**FR-4 — The default in-memory configuration produces no finding.**
A plain `Subscription<T>` or `Subscription` (whose `ChannelFactoryType` is `typeof(InMemoryChannelFactory)`) with `subscription.ChannelFactory == null` and `options.DefaultChannelFactory == null` MUST produce no finding, because it resolves to `InMemoryChannelFactory` under FR-2 step 3 and is compatible under the FR-3 direct arm. The same holds when `options.DefaultChannelFactory` is explicitly an `InMemoryChannelFactory`.
- Example: `AddConsumers(options => options.Subscriptions = [new Subscription<GreetingMade>(new SubscriptionName("greeting-sub"), …)])` with no `DefaultChannelFactory`, plus `ValidatePipelines(throwOnError: true)`, starts the host with zero findings from this rule.

**FR-5 — The finding message uses fully-qualified type names and names a concrete remedy.**
Five transports name their channel factory class `ChannelFactory` in five different assemblies (Redis, RMQ.Sync, RMQ.Async, Kafka, MsSql). A message built from `Type.Name` would therefore read "expected `ChannelFactory`, got `ChannelFactory`". The `Message` of every finding produced by this rule MUST therefore contain:
1. the subscription's own runtime type, as `Type.FullName`;
2. the declared channel factory type `D`, as `Type.FullName`;
3. the type(s) it will actually be handed, as `Type.FullName` — in the direct arm the effective factory's type; in the combined arm the `Type.FullName` of every inner factory in the candidate factory set, in constructor order, comma-separated;
4. a remedy clause instructing the developer either to use the subscription type that the effective factory requires, or to configure a channel factory of type `D`.

The message MUST NOT rely on `Type.Name` for any of items 1-3. Namespace-qualified names (`Type.FullName`) are sufficient; assembly-qualified names are not required.
- Example (direct arm): a `Subscription<GreetingMade>` handed a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` yields a message containing the literals `Paramore.Brighter.Subscription`, `Paramore.Brighter.InMemoryChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`, and a remedy clause.
- Example (combined arm): the same subscription handed `CombinedChannelFactory([RMQ.Async.ChannelFactory, MsSql.ChannelFactory])` yields a message containing `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` in that order.

**FR-6 — Severity is `Error`, and the rule participates in existing reporting and blocking semantics unchanged.**
Every finding from this rule MUST carry `ValidationSeverity.Error`. Under `ValidatePipelines(throwOnError: true)` (the default) a mismatch MUST prevent the host from starting; under `ValidatePipelines(throwOnError: false)` it MUST be reported without blocking, exactly as for the existing `Error`-severity rules. The rule MUST be evaluated only when `ValidatePipelines()` is enabled and MUST add no startup work when it is not. The addition MUST be purely additive: it MUST NOT change the severity, `Source`, `Message` or blocking behaviour of `PumpHandlerMatch`, `HandlerRegistered`, `RequestTypeSubtype` or `UnwrapTransformResolvable`, nor the defaults of `ValidatePipelines(enabled = true, throwOnError = true)`.
- Example: a configuration with one mismatched subscription and `ValidatePipelines(enabled: false)` starts with no findings and no rule evaluation.

**FR-7 — `GcpPubSubSubscription.ChannelFactoryType` is corrected to `GcpPubSubChannelFactory`.**
`src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubSubscription.cs:108` MUST return `typeof(GcpPubSubChannelFactory)` instead of `typeof(GcpPubSubConsumerFactory)`. `GcpPubSubConsumerFactory` implements `IAmAMessageConsumerFactory`, not `IAmAChannelFactory`, so the current value can never match any registered channel factory.
- Example: `new GcpPubSubSubscription<GreetingMade>(…).ChannelFactoryType == typeof(GcpPubSubChannelFactory)`.

**FR-8 — `MqttSubscription.ChannelFactoryType` is corrected to the MQTT `ChannelFactory`.**
`src/Paramore.Brighter.MessagingGateway.MQTT/MqttSubscription.cs:35` MUST return `typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)` instead of `typeof(MqttMessageConsumerFactory)`. `MqttMessageConsumerFactory` implements `IAmAMessageConsumerFactory`, not `IAmAChannelFactory`.
- Example: `new MqttSubscription<GreetingMade>(…).ChannelFactoryType == typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)`.

**FR-9 — Every `ChannelFactoryType` override names a type implementing `IAmAChannelFactory` (regression guard).**
For each of the nine subscription types listed in Table 1, `ChannelFactoryType` MUST return a `Type` for which `typeof(IAmAChannelFactory).IsAssignableFrom(type)` is true. This MUST be enforced by an automated reflection sweep rather than nine hand-written assertions alone: for each messaging-gateway assembly, the sweep enumerates every non-abstract type assignable to `Subscription` that declares (or inherits an override of) `ChannelFactoryType`, obtains its `ChannelFactoryType`, and asserts the result implements `IAmAChannelFactory`. A future transport that repeats the GCP/MQTT defect MUST fail this sweep.
- Example: adding a hypothetical `FooSubscription` whose `ChannelFactoryType` returns `typeof(FooMessageConsumerFactory)` (an `IAmAMessageConsumerFactory`) to the Foo gateway assembly fails the sweep for that assembly.

**FR-10 — Findings are one-per-subscription and deterministic.**
The rule MUST emit at most one finding per configured subscription per validation run, regardless of how many candidate factories were inspected. Repeated runs over the same configuration MUST produce the same findings, with the same message text, in the same order — subscriptions in `options.Subscriptions` order, and inner factories in `CombinedChannelFactory` constructor order.
- Example: two mismatched subscriptions, `sub-a` then `sub-b`, produce exactly two findings, in that order, on every run.

### Non-functional Requirements

- **NFR-1 — Consistency with the existing rule architecture.** The rule MUST be expressed with the established `ISpecification<Subscription>` / `Specification<Subscription>` pattern used by the existing four rules in `ConsumerValidationRules.cs`, so that it is unit-testable in isolation without a host, a container, or a broker.
- **NFR-2 — Actionable, unambiguous messages.** Each message MUST identify the offending subscription by name (via `Source`) and by type, state what it declares it needs and what it will get (in fully-qualified form, FR-5), and state a remedy. No message may be satisfiable by a generic phrase such as "channel factory mismatch".
- **NFR-3 — No broker or network access.** Neither the rule nor any test introduced by this feature may open a connection to a broker, a database, or any external service. The rule inspects types and configured instances only. The FR-9 sweep is reflection-only.
- **NFR-4 — No new startup cost when validation is off.** Consistent with FR-6; the rule performs work only inside a `ValidatePipelines()`-enabled run, and its per-subscription cost is bounded by the size of the candidate factory set.
- **NFR-5 — No public API change to existing abstractions beyond what FR-7/FR-8 require.** In particular `IAmAChannelFactory`, `Subscription` and `IAmConsumerOptions` keep their current members. If the rule requires read access to a `CombinedChannelFactory`'s inner factories, any new member MUST be added to `CombinedChannelFactory` itself and MUST be additive (see C-4).
- **NFR-6 — Behaviour preserved for correct configurations.** No configuration that starts successfully today and is genuinely correct may be made to fail by this feature. The known exception is discussed in C-2 and is a deliberate consequence of D1.
- **NFR-7 — British spelling** in all new documentation and XML comments, consistent with the repository.

### Constraints and Assumptions

- **C-1 — Severity is `Error` (settled, D1).** A mismatch is reported at `ValidationSeverity.Error` and therefore blocks startup under `ValidatePipelines(throwOnError: true)`. This is not revisited by this specification. It is safe to do so precisely because D2 (FR-7, FR-8) removes the only known class of in-repo false positive: with `GcpPubSubSubscription` and `MqttSubscription` still declaring consumer-factory types, a correct GCP/MQTT configuration would otherwise be flagged as an Error by the new rule.
- **C-2 — The rule is symmetric, including the "transport subscription, in-memory factory" direction (accepted risk).** A transport-specific subscription (for example `MsSqlSubscription<T>`) resolving to an `InMemoryChannelFactory` is a mismatch and is reported as an `Error`, even though `InMemoryChannelFactory` does not downcast the subscription and so would not throw. This is deliberate: such a configuration silently consumes from an in-memory bus instead of the intended transport, which is a worse failure than an exception. Developers who want an in-memory consumer should use a plain `Subscription<T>`, which is the intended pattern and passes under FR-4; `ValidatePipelines` is in any case opt-in and can be disabled.
- **C-3 — Three of the nine channel factories do not downcast the subscription** (`AzureServiceBusChannelFactory`, MQTT's `ChannelFactory`, `RocketMqChannelFactory`); the other six do (MsSql, Kafka, Redis, RMQ.Sync, RMQ.Async, plus the combined/in-memory factories, which are not transport factories). This is why a mismatch is not *always* an immediate runtime failure today, and hence why D2 (correcting the overrides) is what makes D1 (Error severity) safe: without the corrections, MQTT in particular would be flagged despite "working" via a non-downcasting factory.
- **C-4 — `CombinedChannelFactory`'s inner factories are currently private.** `_factories` is a `private readonly IReadOnlyList<IAmAChannelFactory>` (`CombinedChannelFactory.cs:13`). FR-3's combined arm requires read access to it. The exact mechanism (an additive read-only property, a membership query method, or an internals-visible arrangement) is a design decision for the ADR; the constraint here is only that it be additive and confined to `CombinedChannelFactory` (NFR-5).
- **C-5 — The rule needs the consumer options.** FR-2 requires `options.DefaultChannelFactory`, which lives on `IAmConsumerOptions` and is available where the four existing consumer specs are registered (`ServiceCollectionExtensions.cs:205-226`). Threading it into the rule's factory function is expected wiring, not new API. Where the options or the default factory cannot be resolved, FR-2 step 3 (in-memory) applies.
- **C-6 — Validation sees the configuration as snapshotted by `ValidatePipelines()`.** Consistent with the existing documented behaviour of `ValidatePipelines` ("call this last in the Brighter builder chain"), a channel factory assigned after that call is not seen by the rule.
- **C-7 — Subscriptions with a null `RequestType` are still checked.** Unlike `PumpHandlerMatch`, `HandlerRegistered` and `UnwrapTransformResolvable`, this rule does not depend on `RequestType` and therefore MUST NOT skip datatype-channel subscriptions whose `RequestType` is null.
- **C-8 — Versioning.** Targets Brighter V10.X. FR-7 and FR-8 change the value returned by a public virtual property; this is a behaviour change (a fix), not a binary-breaking API change, and must be noted in the release notes.
- **C-9 — Test placement.** The rule's unit tests belong with the existing consumer validation tests (`tests/Paramore.Brighter.Core.Tests/Validation/`, following the `When_…` naming convention already used there). The FR-9 sweep and the FR-7/FR-8 assertions belong in the corresponding per-gateway test projects, because no single test project references all nine gateway assemblies. Those tests must be reflection-only and must not require broker infrastructure (NFR-3).

### Out of Scope

- **OOS-1 — Producer-side / publication validation.** No equivalent check is added for `Publication`s or the producer registry.
- **OOS-2 — Changing the downcasting behaviour of any gateway `ChannelFactory`.** The six factories that downcast the subscription keep doing so, with the same exception type and message. This feature detects the condition earlier; it does not change what happens if detection is skipped.
- **OOS-3 — New API surface on `IAmAChannelFactory`.** No member is added to `IAmAChannelFactory`. The check works from `Subscription.ChannelFactoryType`, which already exists.
- **OOS-4 — Re-implementing what the existing four consumer rules cover.** Handler registration, pump/handler sync-async matching, `ICommand`/`IEvent` subtype checks and unwrap-transform resolvability are untouched.
- **OOS-5 — Changing `CombinedChannelFactory`'s runtime matching semantics.** It keeps matching on exact type equality, non-recursively; the rule mirrors that behaviour rather than improving it. Making it assignability-based or recursive is a separate proposal.
- **OOS-6 — A reverse map from channel factory type to required subscription type.** The finding message names the types involved and a generic remedy (FR-5 item 4); it is not required to compute and print "use `MsSqlSubscription<T>`" by scanning loaded assemblies for subscription subclasses.
- **OOS-7 — Fixing the sample applications from #4331.** That is tracked separately; this feature provides the detection that would have caught them.
- **OOS-8 — Runtime (post-startup) validation.** The check runs only at startup via `ValidatePipelines()`; no check is added to `DispatchBuilder`, `ConsumerFactory`, or the channel-creation path.
- **OOS-9 — Non-`ValidatePipelines` configuration surfaces**, including `DescribePipelines()` output changes and any Darker equivalent.

## Acceptance Criteria

**AC-1** (FR-1, FR-5, FR-6) — *Plain subscription with a transport factory is an Error.*
Given a `Subscription<GreetingMade>` named `greeting-sub` with `ChannelFactory` null, and `options.DefaultChannelFactory` set to a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`,
When the rule is evaluated,
Then exactly one `ValidationError` is produced with `Severity == ValidationSeverity.Error`, `Source == "Subscription 'greeting-sub'"`, and a `Message` containing the literals `Paramore.Brighter.InMemoryChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`.

**AC-2** (FR-1, FR-3 direct arm) — *Matching transport subscription passes.*
Given an `MsSqlSubscription<GreetingMade>` named `greeting-sub` with `ChannelFactory` null, and `options.DefaultChannelFactory` set to a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`,
When the rule is evaluated,
Then no findings are produced.

**AC-3** (FR-2) — *The per-subscription factory overrides the default.*
Given `options.DefaultChannelFactory` set to a `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`, and an `RmqSubscription<GreetingMade>` (RMQ.Async) named `rmq-sub` whose `ChannelFactory` is set to a `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory`,
When the rule is evaluated,
Then no findings are produced for `rmq-sub`.

**AC-4** (FR-2) — *Falling back to the default is detected.*
Given the configuration of AC-3 with `rmq-sub.ChannelFactory` left null,
When the rule is evaluated,
Then exactly one `Error` is produced for `rmq-sub`, whose `Message` contains `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory`.

**AC-5** (FR-3 combined arm, FR-10) — *A correct multi-bus configuration produces no false positives.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([rmqAsyncChannelFactory, msSqlChannelFactory])` and two subscriptions — an `RmqSubscription<GreetingMade>` named `rmq-sub` and an `MsSqlSubscription<FarewellMade>` named `mssql-sub`, both with `ChannelFactory` null,
When the rule is evaluated,
Then no findings are produced for either subscription.

**AC-6** (FR-3 combined arm, FR-5) — *A subscription no inner factory can serve is an Error naming the inner factories.*
Given the `CombinedChannelFactory` of AC-5 and a plain `Subscription<GreetingMade>` named `greeting-sub`,
When the rule is evaluated,
Then exactly one `Error` is produced for `greeting-sub`, whose `Message` contains `Paramore.Brighter.MessagingGateway.RMQ.Async.ChannelFactory` and `Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` in constructor order, and does **not** name `Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed.

**AC-7** (FR-3 direct arm) — *A user subclass of a transport channel factory is accepted.*
Given a `MyAuditingMsSqlChannelFactory : Paramore.Brighter.MessagingGateway.MsSql.ChannelFactory` set as `options.DefaultChannelFactory`, and an `MsSqlSubscription<GreetingMade>` with `ChannelFactory` null,
When the rule is evaluated,
Then no findings are produced.

**AC-8** (FR-3 combined arm) — *The same subclass inside a `CombinedChannelFactory` is flagged, mirroring runtime.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new MyAuditingMsSqlChannelFactory(...)])` and an `MsSqlSubscription<GreetingMade>` with `ChannelFactory` null,
When the rule is evaluated,
Then exactly one `Error` is produced — and, as a companion assertion, calling `CreateSyncChannel` on that `CombinedChannelFactory` with the same subscription throws `ConfigurationException`.

**AC-9** (FR-3, no recursion) — *Nested combined factories are not unwrapped.*
Given `options.DefaultChannelFactory = new CombinedChannelFactory([new CombinedChannelFactory([msSqlChannelFactory])])` and an `MsSqlSubscription<GreetingMade>`,
When the rule is evaluated,
Then exactly one `Error` is produced, matching the runtime behaviour of `CombinedChannelFactory`, which also fails to route this subscription.

**AC-10** (FR-4) — *The default in-memory configuration is silent.*
Given a plain `Subscription<GreetingMade>` with `ChannelFactory` null and `options.DefaultChannelFactory` null,
When the rule is evaluated,
Then no findings are produced. The same holds when `options.DefaultChannelFactory` is explicitly `new InMemoryChannelFactory(new InternalBus(), TimeProvider.System)`.

**AC-11** (FR-5) — *Same-named factory types in different assemblies are distinguishable.*
Given a `RedisSubscription<GreetingMade>` (declaring `Paramore.Brighter.MessagingGateway.Redis.ChannelFactory`) handed a `Paramore.Brighter.MessagingGateway.Kafka.ChannelFactory`,
When the rule is evaluated,
Then the finding's `Message` contains both `Paramore.Brighter.MessagingGateway.Redis.ChannelFactory` and `Paramore.Brighter.MessagingGateway.Kafka.ChannelFactory` as distinct, namespace-qualified literals, and the message is not satisfied by the bare token `ChannelFactory` appearing twice.

**AC-12** (FR-6) — *An Error blocks startup under `throwOnError: true`.*
Given a host configured with `AddConsumers` containing one mismatched subscription (as in AC-1) and `ValidatePipelines(throwOnError: true)`,
When the host starts,
Then startup fails and the reported findings include the mismatch `Error`.

**AC-13** (FR-6) — *The same configuration does not block under `throwOnError: false`.*
Given the configuration of AC-12 with `ValidatePipelines(throwOnError: false)`,
When the host starts,
Then the host starts successfully and the mismatch `Error` is present in the validation results.

**AC-14** (FR-6) — *Existing rules are unaffected.*
Given a subscription whose `RequestType` has no registered handler and whose channel factory is correctly matched,
When `ValidatePipelines(throwOnError: true)` runs,
Then the `HandlerRegistered` rule still produces exactly one `Error` with its existing message, and this feature contributes no additional finding.

**AC-15** (FR-1, C-7) — *A subscription with a null `RequestType` is still checked.*
Given a datatype-channel subscription with `RequestType == null` declaring `MsSqlSubscription`'s channel factory type, handed an `InMemoryChannelFactory`,
When the rule is evaluated,
Then exactly one `Error` is produced (the rule does not skip null-`RequestType` subscriptions).

**AC-16** (FR-7) — *GCP Pub/Sub declares its channel factory.*
Given a `GcpPubSubSubscription<GreetingMade>`,
When `ChannelFactoryType` is read,
Then it equals `typeof(GcpPubSubChannelFactory)` and `typeof(IAmAChannelFactory).IsAssignableFrom(...)` is true.

**AC-17** (FR-7, FR-3) — *`CombinedChannelFactory` can now route a GCP Pub/Sub subscription.*
Given `new CombinedChannelFactory([new GcpPubSubChannelFactory(connection)])` and a `GcpPubSubSubscription<GreetingMade>`,
When the rule is evaluated,
Then no findings are produced; and the combined factory selects the `GcpPubSubChannelFactory` for that subscription rather than throwing `ConfigurationException("No channel factory found for subscription …")`.

**AC-18** (FR-8) — *MQTT declares its channel factory.*
Given an `MqttSubscription<GreetingMade>`,
When `ChannelFactoryType` is read,
Then it equals `typeof(Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory)` and `typeof(IAmAChannelFactory).IsAssignableFrom(...)` is true.

**AC-19** (FR-8, FR-3) — *`CombinedChannelFactory` can now route an MQTT subscription.*
Given `new CombinedChannelFactory([new Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory(configuration)])` and an `MqttSubscription<GreetingMade>`,
When the rule is evaluated,
Then no findings are produced; and the combined factory selects the MQTT `ChannelFactory` for that subscription rather than throwing `ConfigurationException("No channel factory found for subscription …")`.

**AC-20** (FR-9) — *Every transport's `ChannelFactoryType` names an `IAmAChannelFactory`.*
Given each of the nine subscription types in Table 1 (`RocketMqSubscription`, `RedisSubscription`, `AzureServiceBusSubscription`, `RmqSubscription` (RMQ.Sync), `RmqSubscription` (RMQ.Async), `KafkaSubscription`, `MsSqlSubscription`, `GcpPubSubSubscription`, `MqttSubscription`),
When its `ChannelFactoryType` is read,
Then `typeof(IAmAChannelFactory).IsAssignableFrom(type)` is true for all nine.

**AC-21** (FR-9) — *The guard is a sweep, not a fixed list.*
Given a messaging-gateway assembly containing a non-abstract `Subscription` subclass that overrides `ChannelFactoryType` with a type that does not implement `IAmAChannelFactory`,
When the per-assembly reflection sweep runs,
Then the sweep fails and names the offending subscription type and the type it declared — so a future transport cannot reintroduce the GCP/MQTT defect unnoticed.

**AC-22** (FR-10) — *Findings are one-per-subscription, ordered and deterministic.*
Given two mismatched subscriptions `sub-a` and `sub-b`, in that order in `options.Subscriptions`, evaluated against a `CombinedChannelFactory` with three inner factories,
When the rule is evaluated twice,
Then each run produces exactly two findings — one per subscription, in the order `sub-a`, `sub-b` — with byte-identical messages across the two runs.

**AC-23** (NFR-3) — *No infrastructure is required.*
Given the full set of tests introduced by this feature,
When they run in an environment with no broker, database, or network access,
Then they all pass.

## Additional Context

- **Origin.** Issue #4334, prompted by #4331, in which two sample applications had shipped with `Subscription<T>` where an `MsSqlSubscription<T>` was required and had never been able to start. Compilation succeeded, CI compiled the samples, and nothing detected the defect until someone tried to run them.
- **Maintainer decisions already taken** (not to be re-opened by design or implementation):
  - **D1** — severity is `ValidationSeverity.Error` (see C-1).
  - **D2** — correcting the wrong `ChannelFactoryType` overrides on `GcpPubSubSubscription` and `MqttSubscription` ships with the rule, in this specification (FR-7, FR-8), rather than being deferred.
- **Grounding references** (HOW belongs in the ADR):
  - `src/Paramore.Brighter/Subscription.cs:48` (`ChannelFactory`), `:172` (`ChannelFactoryType`).
  - `src/Paramore.Brighter/CombinedChannelFactory.cs:13`, `:34`, `:46`, `:59` — exact-type-equality routing and the `ConfigurationException` it throws.
  - `src/Paramore.Brighter/IAmConsumerOptions.cs:12` (`DefaultChannelFactory`).
  - `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` — the four existing rules; `PumpHandlerMatch` is the style model for message and `Source` construction.
  - `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:159` (default-factory fallback), `:205-226` (`RegisterConsumerValidationSpecs`).
  - `src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs:146-148` — the default factory is assigned only where `subscription.ChannelFactory` is null, which is what FR-2's precedence mirrors.
  - `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs:46`, `:66`, `:88` — the downcast-and-throw pattern the rule pre-empts.
  - `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs:83` — an in-repo multi-bus configuration (Kafka + RMQ.Async) whose subscriptions must not be flagged.
