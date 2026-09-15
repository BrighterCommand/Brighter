---
id: 0072-subscription-channel-factory-compatibility
title: "Validate Subscription and Channel Factory Compatibility at Startup"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-09-15
summary: "Adds a fifth ISpecification<Subscription> rule to ValidatePipelines() that reports an Error when a subscription's declared ChannelFactoryType is incompatible with the channel factory it will actually be handed, resolving the effective factory to a Type and reading a CombinedChannelFactory's inner identities through a new additive FactoryTypes property; the five wrong or missing gateway ChannelFactoryType declarations are corrected as a consequence."
tags:
  - "pipeline"
  - "configuration"
  - "specification-pattern"
  - "transports"
---

# 72. Validate Subscription and Channel Factory Compatibility at Startup

Date: 2026-09-15

## Status

Proposed

## Context

**Parent Requirement**: [specs/0037-validate-subscription-channel-factory/requirements.md](../../specs/0037-validate-subscription-channel-factory/requirements.md)

**Scope**: This ADR decides **the runtime validation rule** — FR-1 to FR-6 and FR-13, together with the
two mechanism questions the requirements deliberately leave open (C-4, how the rule reads a
`CombinedChannelFactory`'s inner factories; C-5, how it obtains `options.DefaultChannelFactory`). The
five `ChannelFactoryType` corrections (FR-7 to FR-11) are recorded here as a **consequence** of the
`Error` severity, not as an architecture of their own: they are one-line overrides in five gateway
assemblies, and they exist so that D1's `Error` does not refuse startup to correct AWS SQS, AWS SQS
V4, Postgres, GCP Pub/Sub and MQTT hosts.

**Deferred to ADR 0073**: FR-12's regression sweep — the construction-free reflection guard over the
twelve gateway assemblies, the pure `(subscriptionType, declaredFactoryType)` predicate it is built
from, and where that predicate lives. That is a distinct decision with a distinct force (it must
reach twelve test projects that share no test-support assembly) and is not pre-empted here.

This ADR **supersedes neither** [ADR 0053](0053-pipeline-validation-at-startup.md) nor
[ADR 0064](0064-validate-pipeline-assembly-and-provider-registration.md). Both remain in force; this
is the **third** extension of the framework 0053 introduced, and it follows 0064's pattern of adding
rules to the existing `ISpecification<T>` channel rather than building new machinery.

### The problem

Every transport's `Subscription` subclass carries transport-specific configuration, and eleven of the
twelve shipped gateway channel factories downcast the `Subscription` they are handed. Writing
`new Subscription<T>(...)` where `MsSqlSubscription<T>` was required therefore **compiles cleanly**
and fails only when the `Dispatcher` builds its channels — verified in
`src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs`, which throws
`ConfigurationException` from all three creation methods when the cast fails. Two sample applications
shipped with exactly this defect and had never been able to start (#4331). CI compiled them, and
compiling is precisely what this defect survives.

There is a second, currently invisible half. `Subscription.ChannelFactoryType`
(`src/Paramore.Brighter/Subscription.cs`) is a `public virtual Type` defaulting to
`typeof(InMemoryChannelFactory)`. `CombinedChannelFactory`
(`src/Paramore.Brighter/CombinedChannelFactory.cs`) routes on it by **exact type equality** in all
three creation methods:

```csharp
var factory = _factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType);
if (factory == null)
    throw new ConfigurationException($"No channel factory found for subscription {subscription.Name}");
```

Five shipped subscription families cannot satisfy that predicate today. Verified by inspection:
`GcpPubSubSubscription` declares `typeof(GcpPubSubConsumerFactory)` and `MqttSubscription` declares
`typeof(MqttMessageConsumerFactory)` — both `IAmAMessageConsumerFactory` implementations, not
channel factories — while `SqsSubscription` (AWSSQS), `SqsSubscription` (AWSSQS.V4) and
`PostgresSubscription` declare **no override at all** and so inherit the in-memory default. Every
multi-bus configuration involving those transports is broken.

`RegisterConsumerValidationSpecs` in
`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`
registers four `ISpecification<Subscription>` rules — `PumpHandlerMatch`, `HandlerRegistered`,
`RequestTypeSubtype` and `UnwrapTransformResolvable`, all in
`src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`. None of them looks at
the channel factory, although every subscription already carries the data needed to check it.

### Forces

- **The effective factory is resolved at two different times.** `DispatchBuilder.Subscriptions()`
  **writes** the default into every subscription whose `ChannelFactory` is `null`. Whether the rule
  observes a subscription before or after that back-fill depends on whether `IDispatcher` has been
  resolved before the validation hosted service runs. FR-2a requires the verdict to be identical
  either way — asserted, not assumed.
- **The two arms of the compatibility test must differ.** The combined arm has to mirror
  `CombinedChannelFactory`'s exact-type equality, or it will disagree with runtime. The direct arm
  has to use assignability, or an application supplying its own subclass of a transport's
  `ChannelFactory` — which works at runtime, because the gateway's downcast of the *subscription*
  still succeeds — would be falsely flagged. OOS-5 forbids "fixing" the asymmetry by making
  `CombinedChannelFactory` assignability-based.
- **`_factories` is private** (C-4). The combined arm needs read access, and NFR-5 confines any new
  member to `CombinedChannelFactory` itself.
- **Eight transports name their channel factory class `ChannelFactory`** in eight assemblies, so a
  message built from `Type.Name` would read "expected `ChannelFactory`, got `ChannelFactory`". But
  `Type.FullName` of a closed generic carries `Version=`/`PublicKeyToken=` payload that AC-14
  forbids. A display-name format is therefore forced.
- **Severity is fixed at `Error` (D1/C-1) and is not revisitable here.** That is only safe because
  the five corrections ship with it. This is the direct inverse of ADR 0064, which chose a
  non-blocking `Warning` for its two rule families; the difference is that 0064's conditions are
  deferred and conditional, whereas a channel-factory mismatch is a certain failure (or, worse, a
  silently wrong bus) on every start.
- **The cure creates its own breakage.** Four documented exceptions to NFR-6 — C-2, C-10, C-11 and
  C-12 — are accepted, and C-12 is *not* suppressible by `ValidatePipelines(throwOnError: false)`,
  because it is a routing change in `CombinedChannelFactory`, not a validation verdict.

## Decision

We add **one** `ISpecification<Subscription>` — `ConsumerValidationRules.ChannelFactoryCompatible` —
to the existing consumer rule family, registered alongside the other four in
`RegisterConsumerValidationSpecs`. It reduces the effective channel factory to a **set of candidate
factory `Type`s plus an arm discriminator**, compares those types against the subscription's declared
`ChannelFactoryType`, and on mismatch yields exactly one
`ValidationError(ValidationSeverity.Error, $"Subscription '{name}'", message)`. It reaches a
`CombinedChannelFactory`'s inner identities through a **new, additive, read-only
`CombinedChannelFactory.FactoryTypes` property**, and it obtains the default factory as a single
constructor argument resolved from `IAmConsumerOptions` at specification-construction time.

No new hosted service, no new validator, no change to `PipelineValidator`, no change to
`ValidatePipelines`'s signature or defaults. The rule rides the existing
`IEnumerable<ISpecification<Subscription>>` DI channel that `PipelineValidator.ValidateConsumers`
already consumes — exactly as ADR 0064's `UnwrapTransformResolvable` does.

### Architecture Overview

```
  AddConsumers(options => { options.Subscriptions = …; options.DefaultChannelFactory = …; })
        │
        ├── RegisterConsumerValidationSpecs(services)     [ServiceActivator.Extensions.DI]
        │      services.AddSingleton<ISpecification<Subscription>>(sp =>
        │          ConsumerValidationRules.ChannelFactoryCompatible(
        │              sp.GetService<IAmConsumerOptions>()?.DefaultChannelFactory))   ← C-5
        │
  ValidatePipelines(enabled, throwOnError)                [Extensions.DI]
        │      registers IAmAPipelineValidator; its factory calls
        │      sp.GetServices<ISpecification<Subscription>>()  ← the ONLY point the
        │      spec factory lambda above ever runs  (⇒ zero cost when disabled, NFR-4)
        ▼
  BrighterValidationHostedService → PipelineValidator.Validate() → ValidateConsumers()
        │      EvaluateSpecs(subscriptions, consumerSpecs, findings)
        │      — outer loop over subscriptions, inner over specs ⇒ FR-13 ordering is
        │        already guaranteed by the framework, not by the rule
        ▼
  ChannelFactoryCompatible ── per subscription ──────────────────────────────────┐
                                                                                 │
   D = subscription.ChannelFactoryType            (Subscription — "knows what     │
                                                   identity I require")           │
   F = subscription.ChannelFactory                                                │
       ?? defaultChannelFactory                   (FR-2 steps 1 and 2)            │
                                                                                 │
   ┌── F is null ─────────────► arm = Direct,   candidates = [ InMemoryChannelFactory ]
   │                                            (FR-2 step 3 — a TYPE, not an instance)
   ├── F is CombinedChannelFactory c ─► arm = Combined, candidates = c.FactoryTypes
   │                                            (CombinedChannelFactory — "knows which
   │                                             identities I route on", in ctor order)
   └── otherwise ────────────► arm = Direct,   candidates = [ F.GetType() ]
                                                                                 │
   compatible = arm == Combined                                                   │
                ? candidates.Any(t => t == D)          ← exact equality, mirrors   │
                                                         CombinedChannelFactory    │
                : D.IsAssignableFrom(candidates[0])    ← assignability             │
                                                                                 │
   compatible ? (no finding) : ValidationError(Error, "Subscription 'name'", msg) ┘
                                                          │
                                                          ▼
                                     PipelineValidationResult.Errors
                                                          ▼
                     BrighterValidationHostedService — throws when throwOnError (FR-6)
```

The single most consequential detail in that diagram is that **the effective factory is reduced to a
`Type` (or an ordered list of `Type`s) before any comparison happens.** FR-2a's invariance then holds
*by construction* rather than by coincidence:

- With a non-null `options.DefaultChannelFactory`, `DispatchBuilder`'s back-fill writes **the same
  instance** the rule would have resolved, so the same type.
- With a null default, the DI path substitutes `new InMemoryChannelFactory(new InternalBus(),
  TimeProvider.System)` and the back-fill writes **a different instance of the same type** — which
  FR-2 step 3 also yields as a type.

Had the rule compared instances (`ReferenceEquals`) or carried the instance into the message, the
second case would have been fragile. Reducing to types also means FR-2 step 3 **never constructs** an
`InMemoryChannelFactory` or an `InternalBus`: the rule allocates nothing on the passing path.

### Key Components

#### 1. `CombinedChannelFactory.FactoryTypes` — the composite's "knowing" responsibility (NEW, additive)

```csharp
/// <summary>
/// The concrete types of the inner factories, in the order supplied to the constructor.
/// These are the identities this factory routes on: it can serve a subscription exactly when
/// that subscription's <see cref="Subscription.ChannelFactoryType"/> is one of them.
/// </summary>
public IReadOnlyList<Type> FactoryTypes { get; }
```

**Contract.** Input: none. Output: a non-null, possibly empty, stable, ordered list of concrete
factory types, one per inner factory, in constructor order, including duplicates if the same factory
type was supplied twice. Error conditions: none — it never throws and never creates a channel.

**Implementation note (a real trap).** It MUST be derived from the existing `_factories` field, not
from the primary-constructor `factories` parameter: `IEnumerable<IAmAChannelFactory>` need not be
re-enumerable, and `_factories = factories.ToList()` has already consumed it. Because C# runs field
and auto-property initialisers in declaration order, the property must be **declared after**
`_factories`.

##### Why this shape, and not the alternatives (the C-4 decision)

The question the requirements leave open is not "how do we get the list out" but **where the
responsibility for knowing which factory serves a subscription belongs**. The answer is that it is
already split three ways, and the split is correct:

| Role | Stereotype | Responsibility |
|---|---|---|
| `Subscription` | information holder | *knowing* which factory identity it requires (`ChannelFactoryType`) — exists today, unchanged |
| `CombinedChannelFactory` | structurer / coordinator | *knowing* which identities it can serve, and *deciding* at runtime which one to dispatch to |
| The rule | decider | *deciding* whether those two are compatible **for the purpose of validation** |

The third responsibility is genuinely the rule's and not the composite's, because the rule's decision
is **strictly larger** than the composite's. It also covers the direct arm (where no composite is
involved at all), it applies assignability there, it enforces C-2's deliberately asymmetric verdict
that a transport subscription handed an in-memory factory is an `Error` even though
`InMemoryChannelFactory` would happily serve it, and it renders a remedy. None of that is a channel
factory's business. A composite that knew about validation severities and remedy wording would be a
worse object.

So what the rule needs from the composite is **knowledge, in the composite's own routing vocabulary**
— and `f.GetType()` *is* that vocabulary, because it is literally what `CombinedChannelFactory`
matches on. `FactoryTypes` hands over exactly that, and nothing else.

**Why not a read-only property exposing the factory *instances* (`IReadOnlyList<IAmAChannelFactory>
Factories`)?** This was the obvious reading of C-4 and is rejected. It gives away **capability**
where only **knowledge** is needed: any holder of that list can call `CreateSyncChannel` on an inner
factory directly, bypassing the routing the composite exists to perform — the precise encapsulation
the Composite pattern buys. The rule demonstrably never needs an instance: FR-3's combined arm is
`f.GetType() == D`, FR-5 item 3 needs display names of types, and AC-9's companion assertion calls
`CreateSyncChannel` on the **composite**, not on an inner factory. Exposing instances would be a
wider public API than the requirement, permanently, for no caller.

**Why not tell-don't-ask — e.g. `bool CanRoute(Subscription subscription)`?** This is the strongest
alternative and deserves a straight answer rather than a dismissal. Its merit is real: it would put
the exact-equality predicate in exactly one place, so the rule could not drift from
`CombinedChannelFactory.cs`'s three call sites. It is rejected on the constraint the requirements
flag: **FR-5's T2 and T3b templates need the display names of every inner factory in constructor
order**, so a boolean cannot render the finding. Resolving that would mean either (a) shipping
`CanRoute` *and* `FactoryTypes`, in which case `CanRoute` is a one-line derivation of `FactoryTypes`
and we have two public members where one suffices — against "there should be one, and preferably only
one, obvious way to do it"; or (b) returning a routing-decision value object carrying the selection
and the candidates, which is a new public type in core, and NFR-5 spends this feature's one
permitted new public type on ADR 0073's predicate. There is a secondary cost too: a public
`CanRoute` on a channel factory reads as a runtime capability check and invites callers to pre-flight
before every `CreateSyncChannel`, which is not a pattern we want to seed.

The duplication that rejecting `CanRoute` leaves behind is genuine and we mitigate it directly rather
than deny it: **AC-9's companion assertion** requires that, for the same configuration, the rule
reports an `Error` *and* `CombinedChannelFactory.CreateSyncChannel` throws `ConfigurationException`.
That test pins the rule's combined arm to the composite's routing in the one direction where drift
would matter (a false negative is worse than a false positive here). It is a weaker guarantee than
sharing the code, and we record that honestly under Risks.

**Why not `InternalsVisibleTo`?** Rejected on two independent grounds. First, it does not fit the
assembly topology: the rule lives in `Paramore.Brighter.ServiceActivator`, a different assembly from
`Paramore.Brighter`, so this would mean opening **all** of core's internals to ServiceActivator
permanently in order to read one list — an invisible, unbounded widening of a boundary that a
one-line public property widens by a known amount. Second, `InternalsVisibleTo` is **not used
anywhere in `src/`** today (verified: the only occurrence is a comment in
`SpannerBoxMigrationRunner.cs`); introducing it for this would add a mechanism the codebase has
never needed, against "do not add new types without necessity" and its spirit for mechanisms. It also
does not solve the problem for out-of-repo callers who write their own validation, whereas a public
property does.

#### 2. `ConsumerValidationRules.ChannelFactoryCompatible` — the rule (NEW)

```csharp
/// <summary>
/// Validates that a subscription's declared <see cref="Subscription.ChannelFactoryType"/> is
/// compatible with the channel factory it will actually be handed …
/// </summary>
/// <param name="defaultChannelFactory">The consumer options' default channel factory, or null.
/// Used only when the subscription carries no factory of its own.</param>
public static ISpecification<Subscription> ChannelFactoryCompatible(
    IAmAChannelFactory? defaultChannelFactory)
```

**Contract.** Input: one `Subscription`, plus the captured default. Output: satisfied (no finding), or
unsatisfied with **exactly one** `ValidationError` at `ValidationSeverity.Error` whose `Source` is
`$"Subscription '{s.Name}'"`. It reads only `Subscription.ChannelFactory`,
`Subscription.ChannelFactoryType` and `Subscription.Name`; it never touches `RequestType` (C-7 — this
rule deliberately does **not** vacuously pass for datatype-channel subscriptions, unlike the other
consumer rules), never creates a channel, never contacts a broker (NFR-3), and never mutates the
subscription.

**Shape: the `Specification<Subscription>` predicate + error-factory constructor** — the same form as
`PumpHandlerMatch`, `HandlerRegistered` and `RequestTypeSubtype`, per NFR-1 and with `PumpHandlerMatch`
as the named style model. `DisposingSpecification<Subscription>` is explicitly **not** used: it exists
because `UnwrapTransformResolvable` takes ownership of a `MessageMapperRegistry` it must drain at
container teardown. This rule owns no disposable resource, so `DisposingSpecification` would add a
lifetime contract with nothing to manage. The collapsed
`Specification<T>(Func<T, IEnumerable<ValidationResult>>)` constructor is also not used: it exists for
rules that yield *many* findings per entity, and FR-13 fixes this rule at zero or one.

**One honest wrinkle in that shape.** The two-argument form evaluates the predicate and then, on
failure, invokes the error factory, so the effective factory is resolved **twice** on the failure path
— exactly as `PumpHandlerMatch` re-fetches `handlerTypes` today. This is acceptable because
resolution is a pure function of `(subscription, defaultChannelFactory)` and both passes must agree —
the same purity FR-2a/AC-5 assert. The *knowledge* is stated once, in private static helpers both
lambdas call; only the *evaluation* repeats, on a path that is about to throw anyway.

**Internal structure — three small private statics, each with one responsibility:**

| Helper | Responsibility |
|---|---|
| `ResolveCandidates(Subscription, IAmAChannelFactory?)` | *knowing* — FR-2's precedence, returning the arm discriminator and the ordered candidate `Type` list |
| `IsCompatible(Type declared, candidates)` | *deciding* — FR-3's two arms, and nothing else |
| `DisplayName(Type)` | *doing* — FR-5's display-name format |

`ResolveCandidates` returns the arm and the candidate list together, because they are a single fact
about one subscription and separating them would let a caller pair a combined list with a direct
comparison. Keeping the arm as an explicit discriminator (rather than re-testing `F is
CombinedChannelFactory` at each use site) is what lets the rest of the rule work in types only.

**A case the requirements do not pin.** `ChannelFactoryType` is `public virtual Type`, so an
out-of-repo override may return `null`. `D.IsAssignableFrom(...)` would then throw, and the
`Specification<T>` framework would convert it to an `Error` reading `"Rule evaluation failed: Object
reference not set…"` with `Source` set to the subscription's `ToString()` — blocking startup with a
useless diagnostic and the wrong `Source`. We therefore treat a null declared type as **satisfied
(skipped)**: there is nothing to compare and no honest message to render, and silence is a better
outcome than that finding. This is not a `try`/`catch` — ADR 0064's "rules must not catch" stands —
it is a defined input case handled in the predicate. Flagged here because it is a decision this ADR
takes that the requirements do not.

#### 3. Message rendering — `DisplayName` and the four remedy templates

`Type.FullName` is unusable directly: for `Subscription<FakeChannelFactoryRequest>` it embeds
`Version=`, `Culture=` and `PublicKeyToken=`, which AC-14 forbids. `Type.Name` is unusable in the
other direction: eight transports name the class `ChannelFactory`, so AC-15 requires namespace
qualification. No display-name formatter exists in `Paramore.Brighter` today — `Extensions/TypeExtensions.cs`
is an `internal` netstandard2.0 polyfill for `IsAssignableTo`, and `Extensions/ReflectionExtensions.cs`
is `internal` to core — so the rule provides its own.

It is a **private static method on `ConsumerValidationRules`**, not a new public helper type. NFR-5
allows one new public type and ADR 0073 needs it; a formatter used by one rule does not earn public
surface. The algorithm avoids parsing `FullName`'s assembly payload entirely:

```
DisplayName(t):
  if !t.IsGenericType          → t.FullName ?? t.Name
  else                         → strip at '`' from t.GetGenericTypeDefinition().FullName
                                 + "<" + join(", ", t.GetGenericArguments().Select(DisplayName)) + ">"
```

Known simplification: nested types render with CLR's `+` separator. No acceptance criterion exercises
one — C-9's `AlphaBus`/`BetaBus` doubles are *namespaces*, not nested types — and handling `+`
would add branching for a case the feature does not have.

The message is assembled as a body plus one of FR-5's four remedy literals, appended last so that
AC-13/13a/13b/13c's "ends with" assertions hold. Only the four remedy literals are normative; the
body wording below is this ADR's proposal, constrained by AC-12, AC-14 and AC-15:

```
direct   : Subscription type '{S}' declares ChannelFactoryType '{D}' but will be handed '{F}' {remedy}
combined : Subscription type '{S}' declares ChannelFactoryType '{D}' but will be handed one of '{F-list}' {remedy}
```

Two lexical constraints shaped that wording and are easy to breach accidentally:

- AC-15 forbids any occurrence of the token `ChannelFactory` that is neither preceded by `.` nor part
  of `ChannelFactoryType`. The body therefore says "channel factory" in lower-case prose where it
  refers to the concept, and never writes `CombinedChannelFactory` as a bare word — which AC-7
  independently forbids, since naming it would tell the developer to declare
  `typeof(CombinedChannelFactory)`, the one thing the combined arm must never suggest.
- AC-13a and AC-13c forbid the substring `configure a channel factory of type` anywhere in a T3a/T3b
  message, so the body must not paraphrase the suppressed half.

`{F-list}` joins display names with `", "` in constructor order. The separator is defined **once** and
shared between body and remedy, so the two cannot disagree.

**Why T3a/T3b's suppression is architectural, not cosmetic.** When `D` is `InMemoryChannelFactory` the
subscription is a plain `Subscription`/`Subscription<T>`, and "configure a channel factory of type
`Paramore.Brighter.InMemoryChannelFactory`" is the *cheaper* of the two remedies in the case this
feature fires most often — and following it produces a consumer that silently reads an in-memory bus
while believing it reads SQS. That is the exact failure C-2 exists to name and C-12 exists to
withdraw. Offering it as one of two equal options would make the rule an accessory to the defect it
detects, so the in-memory templates offer only the direction that fixes it.

#### 4. Registration and the C-5 decision

The rule is appended to `RegisterConsumerValidationSpecs` as a fifth
`services.AddSingleton<ISpecification<Subscription>>(sp => …)`:

```csharp
services.AddSingleton<ISpecification<Subscription>>(sp =>
    ConsumerValidationRules.ChannelFactoryCompatible(
        sp.GetService<IAmConsumerOptions>()?.DefaultChannelFactory));
```

**We pass the default factory instance, not the options object.** `IAmConsumerOptions` also carries
`Subscriptions`, `InboxConfiguration`, `InstrumentationOptions` and `ShutdownTimeout`; handing the
whole role to a rule that needs one member both widens its dependency and invites it to iterate
`Subscriptions` itself, which is `PipelineValidator.ValidateConsumers`'s job. Depending on the
narrowest thing that satisfies the need keeps the rule unit-testable with a bare
`new DeclaredChannelFactory()` and no options object at all, which is what C-9's doubles require.
`GetService` rather than `GetRequiredService` is deliberate: an absent registration degrades to FR-2
step 3, the same fallback a null `DefaultChannelFactory` takes.

Two properties follow from where this lambda sits, both verified:

- `IAmConsumerOptions` is registered by `AddConsumers` (`services.TryAddSingleton<IAmConsumerOptions>(options)`)
  **before** `RegisterConsumerValidationSpecs(services)` is called, and the lambda runs at *resolution*
  time, so the options are fully configured by then. C-6's snapshot semantics apply unchanged: a
  default factory assigned after `ValidatePipelines()` is not seen.
- The lambda is only ever invoked from `sp.GetServices<ISpecification<Subscription>>()` inside the
  `IAmAPipelineValidator` factory, and `ValidatePipelines(enabled: false)` returns the builder before
  registering that factory. So with validation disabled the rule is never constructed, never
  evaluated, and costs nothing — FR-6 and NFR-4 are satisfied by the existing wiring rather than by a
  guard inside the rule.

#### 5. The five corrections (FR-7 to FR-11) — a consequence, not an architecture

Each is a one-line `public override Type ChannelFactoryType => typeof(…);` in its own gateway
assembly. Verified targets: `GcpPubSubChannelFactory` (which does implement `IAmAChannelFactory`),
`Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory`,
`Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory`,
`Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory` and `PostgresChannelFactory`.

They carry no design decision, but they are **load-bearing for D1**: without them the new rule would
report an `Error` for correct, working AWS SQS and Postgres consumers — `D` would be
`InMemoryChannelFactory` while the effective factory is the real transport factory — and under the
default `throwOnError: true` would refuse to start hosts that run today, including four families of
in-repo sample. They are what makes `Error` severity defensible, which is why D2 puts them in this
change rather than a follow-up. Their cost is C-12, recorded below.

### Technology Choices

- **`ISpecification<Subscription>` + `Specification<T>(predicate, errorFactory)`**, not a new
  abstraction. The framework already gives us evaluation order (FR-13 falls out of
  `EvaluateSpecs`'s subscription-outer/spec-inner loop), result collection
  (`ValidationResultCollector<T>`), severity routing (`PipelineValidationResult.Errors`), and
  blocking (`BrighterValidationHostedService`).
- **Reflection-free.** The rule uses `Type` identity, `Type.IsAssignableFrom` and
  `Type.GetGenericArguments` only — no `Activator`, no assembly scanning, no member probing. That is
  what keeps it in the rule layer and out of ADR 0073's territory.
- **`Type.IsAssignableFrom`, not the `IsAssignableTo` polyfill.** `IsAssignableFrom` exists on
  netstandard2.0 natively; core's `IsAssignableTo` extension is `internal` and in the `System`
  namespace, unavailable across the assembly boundary.
- **No new value type for the display name.** "Avoid primitive obsession" argues for one; NFR-5's
  single-new-public-type budget, claimed by ADR 0073, argues against. The name is produced and
  consumed within a few lines of the same private method, so a `string` is honest here.

### Implementation Approach

Sequenced so each step is independently testable, and structural changes precede behavioural ones.

1. **`CombinedChannelFactory.FactoryTypes`** (core, additive). Declared after `_factories`, derived
   from it. Pure structural addition; no existing behaviour changes.
2. **The display-name formatter** as a private static on `ConsumerValidationRules`, covered by
   AC-12/AC-14/AC-15's message assertions.
3. **`ChannelFactoryCompatible`**, with `ResolveCandidates` / `IsCompatible` / the four templates.
   AC-1 to AC-13c and AC-19 all exercise this against C-9's doubles with no container and no host.
4. **Registration** in `RegisterConsumerValidationSpecs`. AC-16, AC-17 and AC-17a then exercise the
   host-start behaviour in `tests/Paramore.Brighter.Extensions.Tests`, which must declare its **own**
   copies of the doubles — test projects here do not reference one another (C-9).
5. **The five corrections**, one per gateway assembly, each with AC-20 to AC-26 in that gateway's own
   test project. AC-26f additionally runs the rule, and so lands only in `AWS.Tests`, `AWS.V4.Tests`
   and `MQTT.Tests`, the three of the five that reference `Paramore.Brighter.ServiceActivator`.
6. **Release notes** for C-8's obligations — C-10, C-11 and C-12 as separate entries, with C-12
   flagged as the one `throwOnError: false` does not avoid.

FR-12's sweep follows in ADR 0073 and is not part of this sequence.

### Testing Strategy

The rule is a pure function of `(Subscription, IAmAChannelFactory?)`, which is what makes C-9's
approach work: the doubles are **identity-only**. `DeclaredChannelFactory`,
`DerivedChannelFactory : DeclaredChannelFactory`, `NonMatchingChannelFactory`, and the
same-simple-name pair `AlphaBus.ChannelFactory` / `BetaBus.ChannelFactory` contribute nothing but
*being distinct types*; every `IAmAChannelFactory` member on them throws, so a test that strays into
channel creation fails loudly rather than passing silently (NFR-3). `DeclaringSubscription` and
`NonMatchingSubscription` override `ChannelFactoryType` and nothing else.

That the rule can be fully specified against types that have no behaviour is itself evidence the
design is right: if the rule needed a functioning factory, the responsibility split would be wrong.

Three tests carry more weight than the rest:

- **AC-5 (FR-2a invariance)** — evaluate the same configuration before and after
  `DispatchBuilder`'s back-fill and require byte-identical messages. This is the assertion that the
  type-reduction argument above is true and not merely plausible.
- **AC-9's companion assertion** — the rule reports an `Error` for a `DerivedChannelFactory` inside a
  `CombinedChannelFactory`, *and* `CreateSyncChannel` on that composite throws
  `ConfigurationException`. This is the only guard against the rule's combined arm drifting from the
  composite's routing, and it is the price of rejecting `CanRoute`.
- **AC-30 (determinism)** — two runs, two subscriptions, byte-identical messages in
  `options.Subscriptions` order.

### Performance

Per subscription, per validation run, at startup, only when `ValidatePipelines()` is enabled: one
null check, at most one `is` test, and either one `IsAssignableFrom` or a linear scan of
`FactoryTypes` (in practice two or three entries). On the passing path the rule allocates only the
candidate list; `FactoryTypes` is materialised once per `CombinedChannelFactory`. FR-2 step 3
allocates nothing at all, because the in-memory fallback is a `typeof`, not a `new`. String work
happens only on the failing path, which is about to block startup. This is immeasurable against the
container build it sits inside.

## Consequences

### Positive

- **The #4331 class of defect becomes a named startup finding.** A `Subscription<T>` where
  `MsSqlSubscription<T>` was required is reported by name, with the type it declares, the type it will
  get, and what to change — instead of a `ConfigurationException` from deep inside the Dispatcher, or
  nothing at all until the wrong bus is consumed.
- **Five long-broken transports become routable.** AWS SQS, AWS SQS V4, Postgres, GCP Pub/Sub and MQTT
  subscriptions can be routed by `CombinedChannelFactory` for the first time. Multi-transport
  configurations involving them are fixed, not merely diagnosed.
- **Silent wrong-bus consumption is now an error, not a surprise.** C-2's symmetry means a transport
  subscription resolving to an in-memory factory is reported even though it would not throw — the
  failure mode that is worse than an exception because nothing tells you.
- **Minimal, conventional surface.** One rule, one registration line, one additive property. No change
  to `PipelineValidator`, `ValidatePipelines`, `IAmAChannelFactory`, `Subscription` or
  `IAmConsumerOptions` (NFR-5, OOS-3), and no change to the four existing rules' severity, `Source`,
  `Message` or blocking behaviour (FR-6).
- **Zero cost when disabled**, by construction rather than by a guard — the spec factory lambda never
  runs unless `ValidatePipelines` registered the validator.
- **Testable without a broker, a container or a host** — the rule's entire input is two objects and
  its entire output is a record.
- **A useful capability for others.** `FactoryTypes` makes a composite's routing set inspectable to
  anyone writing their own diagnostics, without giving away the ability to bypass the routing.

### Negative

These are real, and four of them are accepted breakage.

- **C-2 — the symmetric in-memory verdict produces findings for configurations that would not
  throw.** A `MsSqlSubscription<T>` resolving to an `InMemoryChannelFactory` is an `Error` even though
  `InMemoryChannelFactory` does not downcast. Accepted: developers wanting an in-memory consumer
  should use a plain `Subscription<T>`, which passes under FR-4.
- **C-10 — out-of-repo subscription types that declare no override now block startup.** `Subscription`
  subclasses defined by applications, custom transports or community gateways inherit
  `typeof(InMemoryChannelFactory)`, and neither D2 nor ADR 0073's sweep reaches them. The genuinely
  new breakage is narrow — a subclass of `Subscription`/`Subscription<T>` used with a
  **non-downcasting** custom factory in a single-factory configuration; the downcasting case already
  fails today and the combined case already throws (unless an inner factory is exactly an
  `InMemoryChannelFactory`, in which case both runtime and the rule agree it matches). Accepted per
  D3: custom transports are rare, the fix is one line, and softening the rule for a population we
  cannot verify would weaken it exactly where the AWS SQS and Postgres defects lived.
- **C-11 — a plain `Subscription<T>` used with MQTT works today and will become an `Error`.** MQTT is
  the one shipped transport whose `ChannelFactory` accepts any `Subscription`: it builds its channel
  from `ChannelName`, `RoutingKey` and `BufferSize` only, and its consumer factory probes with
  null-tolerant `as` casts. This involves **only shipped Brighter types**, so it cannot be dismissed
  as an edge case. Accepted on D3's grounds with the same one-line remedy (`MqttSubscription<T>`,
  which FR-8 corrects in this same change). The alternative — exempting MQTT — would mean the one
  transport where a mismatch is currently *silent* is the one transport the rule stays silent about,
  inverting the feature's purpose.
- **C-12 — and this one `throwOnError: false` does not rescue.** A host configured with
  `CombinedChannelFactory([new InMemoryChannelFactory(…), …])` and AWS SQS or Postgres subscriptions
  starts successfully today, because those subscriptions' inherited `typeof(InMemoryChannelFactory)`
  matches the in-memory inner factory exactly. After FR-9 to FR-11 nothing matches and
  `CombinedChannelFactory` throws `ConfigurationException` when the Dispatcher builds its channels.
  This is caused by the **corrections**, not the rule, so disabling validation does not avoid it.
  Accepted, because what breaks is a host that was consuming from an in-memory bus while believing it
  was consuming from SQS — preserving it would mean preserving the defect. It must be a separate
  release note naming the symptom (a `ConfigurationException` at Dispatcher start, not a validation
  finding), the remedy, and the fact that `throwOnError: false` does not help.
- **The combined arm restates `CombinedChannelFactory`'s routing predicate.** Two places now encode
  "exact type equality, non-recursive". The cost of the `FactoryTypes` choice over `CanRoute`, pinned
  by AC-9's companion assertion rather than eliminated.
- **The finding message is pinned by literal assertions.** T1/T2/T3a/T3b and AC-15's token rule mean
  the message text is effectively public API. Improving the wording later breaks tests, and AC-15's
  `ChannelFactoryType` carve-out is a constraint future editors will not guess.
- **A private display-name formatter that others will want.** Type rendering is a general need —
  `PipelineDiagnosticWriter` has the same problem — and we are solving it privately inside one rule
  because the public-type budget is spent elsewhere. When a second caller appears this should be
  promoted, and until then there is a latent duplicate waiting to be written.
- **A new public member on a core type.** `FactoryTypes` is permanent surface on
  `CombinedChannelFactory`, additive and small, but it exists because a rule in another assembly
  needed to see inside.
- **Release-note burden.** Three distinct breaking-change notes (C-10, C-11, C-12) for one feature is
  a lot to ask a reader of V10.X notes to absorb.

### Risks and Mitigations

- **Risk: the rule and `CombinedChannelFactory` drift apart.** If the composite's routing ever changes
  (OOS-5 says it will not here), the rule silently becomes wrong — and a false *negative* is the
  dangerous direction, since it restores the silent failure the feature exists to remove.
  *Mitigation*: AC-9's companion assertion ties verdict to runtime behaviour; `FactoryTypes`'s XML
  documentation states the routing contract at the point a future editor would change it; and
  promoting to a shared predicate later is a localized change, since the rule already asks the
  composite rather than reaching into it.
- **Risk: `Error` severity blocks a host we did not anticipate.** NFR-6 is deliberately stated without
  a count because the exception set has grown twice under review. *Mitigation*: any newly discovered
  case gets its own constraint and its own release note rather than being absorbed silently;
  `ValidatePipelines` is opt-in and `throwOnError: false` unblocks every case except C-12; and ADR
  0073's sweep exists so the in-repo class cannot silently return.
- **Risk: the message assertions become a maintenance tax.** *Mitigation*: only the four remedy
  literals are normative; the body wording is this ADR's and may be revised as long as AC-12, AC-14
  and AC-15 hold. Defining `{F-list}`'s separator once removes the most likely inconsistency.
- **Risk: a null `ChannelFactoryType` from an out-of-repo override.** *Mitigation*: handled as a
  defined input (skip) rather than left to the framework's `"Rule evaluation failed"` path, which
  would block startup with the wrong `Source` and an unhelpful message. Recorded above as a decision
  the requirements do not pin, and reversible if maintainers prefer strictness.
- **Risk: `FactoryTypes` is initialised from the constructor parameter rather than `_factories`.** An
  `IEnumerable` already consumed by `_factories = factories.ToList()` would yield an empty list, and
  the rule would then flag **every** subscription in **every** multi-bus application — a very loud,
  very wrong failure. *Mitigation*: derive from `_factories`, declare the property after it, and let
  AC-6 (no false positives in a correct multi-bus configuration) catch a regression.
- **Risk: C-12 surprises an operator at deployment rather than at build.** It is the only case with no
  validation-time warning. *Mitigation*: its own release note, named symptom, and the explicit
  statement that `throwOnError: false` does not avoid it.

## Alternatives Considered

- **`Warning` severity instead of `Error`** — as ADR 0064 chose for its two rule families. Rejected
  per D1/C-1, and the contrast with 0064 is the argument: 0064's conditions are deferred and
  conditional (they bite only when a message of a given type flows), whereas a channel-factory
  mismatch fails on **every** start for eleven of twelve transports, or silently consumes the wrong
  bus. A `Warning` would have been logged and ignored by the #4331 samples exactly as the absence of
  any check was. The in-repo false positives that would have made `Error` unsafe are removed by D2,
  which is why the two ADRs can reach opposite conclusions from the same framework.
- **Ship the rule now and correct the five gateways later.** Rejected per D2. Under `Error` severity
  with `throwOnError: true` the rule would refuse startup to correct AWS SQS, AWS SQS V4, Postgres,
  GCP Pub/Sub and MQTT hosts — including several in-repo samples. The corrections are not a follow-up;
  they are the precondition for the severity.
- **Expose `IReadOnlyList<IAmAChannelFactory> Factories` on `CombinedChannelFactory`.** The obvious
  reading of C-4. Rejected: it hands out capability (any holder can call `CreateSyncChannel` on an
  inner factory, bypassing the composite) where the rule demonstrably needs only knowledge. No caller
  in this feature needs an instance.
- **Tell-don't-ask: `CombinedChannelFactory.CanRoute(Subscription)`.** Genuinely attractive — it would
  make drift impossible. Rejected because FR-5's T2/T3b need the ordered display names of *all* inner
  factories for the remedy clause, so a boolean cannot render the finding; supplying both `CanRoute`
  and `FactoryTypes` gives two public members where one derives trivially from the other, and a
  routing-decision value object would spend NFR-5's one new public type that ADR 0073 needs.
- **`InternalsVisibleTo` from `Paramore.Brighter` to `Paramore.Brighter.ServiceActivator`.** Rejected:
  it opens all of core's internals permanently to read one list, it is used nowhere in `src/` today,
  and it does nothing for out-of-repo callers who want to inspect a composite's routing.
- **Make `CombinedChannelFactory` match on assignability, or recurse into nested composites, so both
  arms unify.** Rejected per OOS-5. It would change runtime routing semantics for every existing
  multi-bus application as a side effect of adding a validation rule — the arms differ precisely
  because the runtime behaviours they predict differ, and the rule's job is to predict runtime
  faithfully, not to improve it. A worthwhile separate proposal.
- **Check compatibility in `DispatchBuilder` or the channel-creation path instead of a specification.**
  Rejected per OOS-8. It would run unconditionally rather than under opt-in validation, it would fire
  after the host had already committed to starting, and it would duplicate what the eleven downcasting
  factories already do — later and with a worse message. `ValidatePipelines` is the established place
  for "tell me at startup what is wrong with my configuration".
- **Put the rule in core `Paramore.Brighter` alongside `ProducerValidationRules`.** Superficially
  appealing since `Subscription`, `CombinedChannelFactory` and `InMemoryChannelFactory` are all core
  types. Rejected per ADR 0064's placement rule: consumer concerns do not leak into core, and
  `ConsumerValidationRules` already lives in `Paramore.Brighter.ServiceActivator`. Placing it in core
  would also orphan it from the consumer-spec DI channel it rides.
- **`DisposingSpecification<Subscription>`**, as `UnwrapTransformResolvable` uses. Rejected: that shape
  exists to transfer ownership of a `MessageMapperRegistry` the rule must drain at teardown. This rule
  owns nothing disposable; using it would add a lifetime contract with nothing to manage.
- **Compute the remedy by scanning loaded assemblies for the subscription type that matches `F`** —
  "use `MsSqlSubscription<T>`". Rejected per OOS-6: it would make the rule reflective and
  assembly-load-order dependent, for a message improvement FR-5's asymmetric `{F}` clause already
  delivers — naming the type the subscription must declare is enough to find the subscription class.
- **Fold FR-12's regression sweep into this ADR**, as ADR 0064 folded its two rule families into one
  record. Rejected: the sweep's forces are entirely different — construction-free reflection over
  uninitialised objects, closing open generics against per-transport constraints, and a predicate that
  must reach twelve test projects sharing no test-support assembly. Combining them would bury a
  distinct decision inside a rule design. It is ADR 0073.

## References

- **Requirements**: [specs/0037-validate-subscription-channel-factory/requirements.md](../../specs/0037-validate-subscription-channel-factory/requirements.md)
- **Related ADRs**:
  - [ADR 0053 — Pipeline Validation and Diagnostic Report at Startup](0053-pipeline-validation-at-startup.md) (Accepted) — the parent decision: `IAmAPipelineValidator`, `IAmAPipelineDiagnosticWriter`, the `ISpecification<T>` rule families, `ValidationError`, and `throwOnError` semantics. Not superseded.
  - [ADR 0064 — Validate Pipeline Assembly Scanning and Validation-Provider Registration](0064-validate-pipeline-assembly-and-provider-registration.md) (Accepted) — the direct sibling and closest precedent: the second extension of that framework, and the source of the placement rule (consumer rules in ServiceActivator), the "rules must not catch" pattern, and the consumer-spec DI channel this rule rides. Not superseded; this ADR is the third extension.
  - **ADR 0073 — Gateway `ChannelFactoryType` Regression Guard** *(forthcoming)* — FR-12's reflection sweep, its construction-free mechanism, and the pure predicate it exposes. Deliberately not designed here.
- **External references**: GitHub issue [#4334](https://github.com/BrighterCommand/Brighter/issues/4334) (this feature), prompted by [#4331](https://github.com/BrighterCommand/Brighter/issues/4331) (two sample applications that shipped with `Subscription<T>` where `MsSqlSubscription<T>` was required and had never been able to start).
- **Grounded code references** (all verified against the working tree):
  - `src/Paramore.Brighter/CombinedChannelFactory.cs` — `_factories`, and exact-type-equality routing in `CreateSyncChannel`, `CreateAsyncChannel` and `CreateAsyncChannelAsync`.
  - `src/Paramore.Brighter/Subscription.cs` — `ChannelFactory` and the `virtual ChannelFactoryType` defaulting to `typeof(InMemoryChannelFactory)`.
  - `src/Paramore.Brighter/IAmConsumerOptions.cs` — `DefaultChannelFactory`.
  - `src/Paramore.Brighter/Specification.cs` — `ISpecification<TData>` and the three `Specification<T>` constructors; `src/Paramore.Brighter/ValidationError.cs`; `src/Paramore.Brighter/ValidationResult.cs`.
  - `src/Paramore.Brighter/Validation/PipelineValidator.cs` — `ValidateConsumers` and `EvaluateSpecs` (the source of FR-13's ordering).
  - `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` — the four existing rules; `PumpHandlerMatch` is the style model. `src/Paramore.Brighter.ServiceActivator/Validation/DisposingSpecification.cs` (`internal sealed`).
  - `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` — `RegisterConsumerValidationSpecs`, and the `options.DefaultChannelFactory ?? new InMemoryChannelFactory(new InternalBus(), TimeProvider.System)` fallback.
  - `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs` — `ValidatePipelines(enabled, throwOnError)` and the `GetServices<ISpecification<Subscription>>()` collection point.
  - `src/Paramore.Brighter.ServiceActivator/DispatchBuilder.cs` — `Subscriptions()`'s back-fill of the default channel factory.
  - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/{GcpPubSubSubscription,GcpPubSubChannelFactory}.cs`; `src/Paramore.Brighter.MessagingGateway.MQTT/{MqttSubscription,ChannelFactory}.cs`; `src/Paramore.Brighter.MessagingGateway.AWSSQS/{SqsSubscription,ChannelFactory}.cs`; `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/{SqsSubscription,ChannelFactory}.cs`; `src/Paramore.Brighter.MessagingGateway.Postgres/{PostgresSubscription,PostgresChannelFactory}.cs` — the five corrections and their targets.
  - `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs` — the downcast-and-throw pattern the rule pre-empts.
  - `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs` — an in-repo multi-bus configuration that must produce no findings.
