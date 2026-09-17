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
- **The cure creates its own breakage.** Five documented exceptions to NFR-6 — C-2, C-10, C-11, C-12
  and C-13 — are accepted, and C-12 is *not* suppressible by `ValidatePipelines(throwOnError: false)`,
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
                                                         CombinedChannelFactory;   │
                                                         false when D is null      │
                : D is not null                        ← explicit guard, not a     │
                  && D.IsAssignableFrom(candidates[0])    catch (see below)        │
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
`InMemoryChannelFactory` or an `InternalBus` — the in-memory fallback is a `typeof`, not a `new`.
(The rule still allocates its candidate list; see Performance.)

### Key Components

#### 1. `CombinedChannelFactory.FactoryTypes` — the composite's "knowing" responsibility (NEW, additive)

```csharp
private IReadOnlyList<Type>? _factoryTypes;

/// <summary>
/// The concrete types of the inner factories, in the order supplied to the constructor.
/// These are the identities this factory routes on: it can serve a subscription exactly when
/// that subscription's <see cref="Subscription.ChannelFactoryType"/> is one of them.
/// </summary>
/// <remarks>
/// The list is built on first read and cached. This property is <b>not</b> thread-safe: concurrent
/// first reads may each build a list, so every read yields an <i>equal</i> list but not necessarily
/// the same instance. Callers needing concurrent access must synchronise.
/// </remarks>
public IReadOnlyList<Type> FactoryTypes =>
    _factoryTypes ??= _factories.Select(f => f.GetType()).ToList();
```

**Contract.** Input: none. Output: a non-null, possibly empty, ordered list of concrete factory types,
one per inner factory, in constructor order, including duplicates if the same factory type was
supplied twice. Stable in the sense that every read yields an **equal** list; not guaranteed to yield
the same instance (see thread-safety below). Error conditions: none — it never throws and never
creates a channel.

**Implementation note (a real trap, and a compiler rule that hides it).** The property MUST be
derived from the existing `_factories` field, **not** from the primary-constructor `factories`
parameter: `IEnumerable<IAmAChannelFactory>` need not be re-enumerable, and
`_factories = factories.ToList()` has already consumed it. Reading `factories` a second time can
yield an empty list.

The trap is that the obvious spelling of "derive it from `_factories`" does not compile.
`CombinedChannelFactory` is a primary-constructor class with **no constructor body**
(`CombinedChannelFactory.cs:12-14`), so a get-only auto-property is assignable only from an
initialiser — and an instance field or auto-property initialiser may not reference *any* instance
member, in any declaration order:

```
public IReadOnlyList<Type> FactoryTypes { get; } = _factories.Select(f => f.GetType()).ToList();
// error CS0236: A field initializer cannot reference the non-static field, method, or property
```

This matters because the compile error points the wrong way. An implementer who reads it as an
ordering problem will "fix" it by switching to `factories.Select(...)` — the initialiser *may*
reference a primary-constructor parameter — and that is precisely the defect recorded under Risks:
an already-consumed `IEnumerable` yields an empty list, and the rule then flags every subscription
in every multi-bus application.

**The shape above is therefore normative**: a nullable backing field plus an expression-bodied
property that caches on first read. It keeps the primary constructor (so the change stays a pure
addition) and derives from `_factories`. It also needs `using System;` added to the file —
`CombinedChannelFactory.cs` currently imports only the four `System.Collections.Generic`,
`System.Linq`, `System.Threading` and `System.Threading.Tasks` namespaces, uses no `System` type
today, and the repository does not enable `ImplicitUsings`.

Two alternatives were weighed and rejected: a plain expression-bodied
`=> _factories.Select(...).ToList()` re-allocates on every read; converting the class to an explicit
constructor would make this a non-trivial edit to an existing core type rather than an addition.

**Thread-safety, stated plainly rather than waved through.** The backing field is a non-volatile
reference and the property is not thread-safe. If two threads first read `FactoryTypes` concurrently
they may each build a list and one write wins, so a caller can observe two equal-but-distinct
instances. This is acceptable **only because of who calls it**: the sole consumer is the startup
validation rule, reached through `PipelineValidator.ValidateConsumers` → `EvaluateSpecs`, which is a
sequential loop on one thread. `FactoryTypes` is therefore documented as *not* thread-safe, and a
caller needing concurrent access must synchronise or the property must be promoted to
`Lazy<IReadOnlyList<Type>>` at that point. Note the honest consequence for the contract: "stable"
below means *equal on every read*, not *the same instance on every read*, and "materialised once"
means once per composite in practice rather than by guarantee.

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
wider public API than the requirement, permanently, for no caller — against
[`.agent_instructions/testing.md`](../../.agent_instructions/testing.md)'s *Test Scope and Isolation*
rule that "an assembly is a module, it's surface area should be as narrow as possible" and that one
should "not expose more than is necessary from an assembly". `FactoryTypes` is the narrowest surface
that satisfies the need: identities, not instances.

**Why not tell-don't-ask — e.g. `bool CanRoute(Subscription subscription)`?** This is the strongest
alternative and deserves a straight answer rather than a dismissal. Its merit is real: it would put
the exact-equality predicate in exactly one place, so the rule could not drift from
`CombinedChannelFactory.cs`'s three call sites. It is rejected on the constraint the requirements
flag: **FR-5's T2 and T3b templates need the display names of every inner factory in constructor
order**, so a boolean cannot render the finding. Resolving that would mean either (a) shipping
`CanRoute` *and* `FactoryTypes`, in which case `CanRoute` is a one-line derivation of `FactoryTypes`
and we have two public members where one suffices — against "there should be one, and preferably only
one, obvious way to do it"; or (b) returning a routing-decision value object carrying the selection
and the candidates, which buys a permanent public abstraction in core to carry two fields between a
single producer and a single consumer. YAGNI decides it: `FactoryTypes` can be wrapped in such a
type later, without breaking anyone, if a second caller ever appears — whereas a published value
object cannot be withdrawn. There is a secondary cost too: a public
`CanRoute` on a channel factory reads as a runtime capability check and invites callers to pre-flight
before every `CreateSyncChannel`, which is not a pattern we want to seed.

The duplication that rejecting `CanRoute` leaves behind is genuine and we mitigate it directly rather
than deny it: **AC-9's companion assertion** requires that, for the same configuration, the rule
reports an `Error` *and* `CombinedChannelFactory.CreateSyncChannel` throws `ConfigurationException`.
That test catches the most plausible drift — someone "unifying" the two arms on assignability — and
it does so in the direction that matters most, since a false negative restores the silent failure the
feature exists to remove. But it is materially weaker than sharing the code, in a way worth stating
precisely: the pinning is **negative-only**. C-9 requires every `IAmAChannelFactory` member on the
test doubles to throw, so the mirror-image assertion — a correct multi-bus configuration where the
rule is silent *and* the composite successfully routes — cannot be written with the approved double
set at all. AC-6 is therefore rule-only, with no runtime counterpart, and AC-10's nested case carries
no companion assertion either. We record that under Risks rather than imply AC-9 pins both
directions.

**Why not `InternalsVisibleTo`?** **It is prohibited by a standing project rule**, so this is not a
trade-off we get to weigh. [`.agent_instructions/testing.md`](../../.agent_instructions/testing.md)
carries a dedicated section, *No InternalsVisibleTo*, whose first line is categorical:

> **NEVER use `InternalsVisibleTo` to expose internal classes for testing.**

and which prescribes the alternative directly:

> If you need to inject a dependency for testing (e.g., randomness, I/O), make the interface
> **public** so it can be injected through the public API.

`FactoryTypes` is precisely that prescription applied: where access is needed, widen the **public**
surface deliberately and by a known amount, rather than punching an invisible hole in the assembly
boundary. The rule's bullets are framed around testing, and our caller is production code in another
assembly rather than a test project — but the section heading and the `NEVER` are unqualified, and
the reasoning that motivates them (tests and callers couple to behaviour, not to internals;
refactoring internals must not break anyone) applies with more force to a production consumer, not
less.

Two supporting reasons, now secondary to the rule. It does not fit the assembly topology: the rule
lives in `Paramore.Brighter.ServiceActivator`, a different assembly from `Paramore.Brighter`, so this
would open **all** of core's internals to ServiceActivator permanently in order to read one list. And
it does nothing for out-of-repo callers writing their own diagnostics, whereas a public property
does. Consistent with the rule, the mechanism appears nowhere in `src/` today — the only occurrence
of the string is a comment in `SpannerBoxMigrationRunner.cs`.

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
| `IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)` | *deciding* — FR-3's two arms, the null-`declared` case, and nothing else. Takes the arm explicitly: it is the discriminator `ResolveCandidates` returns, never re-derived by testing `F is CombinedChannelFactory` again |
| `DisplayName(Type)` | *doing* — FR-5's display-name format |

`Arm` is a private nested `enum { Direct, Combined }` on `ConsumerValidationRules`, and
`ResolveCandidates` returns `(Arm, IReadOnlyList<Type>)`. Both are private to the rule class; nothing
here is new public surface.

`ResolveCandidates` returns the arm and the candidate list together, because they are a single fact
about one subscription and separating them would let a caller pair a combined list with a direct
comparison. Keeping the arm as an explicit discriminator (rather than re-testing `F is
CombinedChannelFactory` at each use site) is what lets the rest of the rule work in types only.

**A case the requirements pin only for one arm.** `ChannelFactoryType` is `public virtual Type`, so
an out-of-repo override may return `null`. The two arms are not symmetric here:

- **Combined arm — already decided by FR-3.** Its compatibility test is an *if and only if*: the
  subscription is compatible iff at least one inner factory satisfies `f.GetType() == D`.
  `object.GetType()` never returns null, so a null `D` satisfies nothing, the subscription **is** in
  mismatch, and FR-1 requires exactly one `ValidationError`. That is not ours to decide — and it
  mirrors runtime exactly, since `_factories.FirstOrDefault(f => f.GetType() == null)` is `null` for
  every non-empty factory set, so the composite throws `ConfigurationException` on **every** start.
- **Direct arm — genuinely undefined.** `D.IsAssignableFrom(...)` would throw, and the
  `Specification<T>` framework would convert that to an `Error` reading `"Rule evaluation failed:
  Object reference not set…"` with `Source` set to the subscription's `ToString()` — blocking startup
  with a useless diagnostic and the wrong `Source`.

**We therefore treat a null declared type as a mismatch in both arms**, guarding the direct arm with
an explicit `D is null` test rather than letting the exception path fire. This is not a `try`/`catch`
— ADR 0064's "rules must not catch" stands — it is a defined input case handled in the predicate.

Skipping was considered and rejected. It would leave the rule silent about a configuration that is
certain to throw at Dispatcher start, which is the same inversion this ADR refuses to accept for MQTT
under C-11: the case where failure is currently *silent* is the last case the rule should be silent
about. Silence is the one outcome worse than an imperfect message.

**This changed the requirements, and the requirements were amended rather than deviated from.** An
earlier draft of this ADR claimed the rendering "costs no new normative surface". That was wrong
twice: FR-5 item 2 required the message to carry `D` as a `Type.FullName`, which is unsatisfiable when
there is no type; and a null `D` satisfies T1's stated condition (`D != typeof(InMemoryChannelFactory)`
is true of `null`), so preferring T3a/T3b is a change to a **normative selection rule**, not merely a
change of wording. Both are now settled in `requirements.md` rather than asserted here:

- FR-5 item 2 admits the literal phrase `no ChannelFactoryType` when `D` is null.
- FR-5's selection table is restated as five ordered, total conditions, with T3a/T3b taking
  `D is null` alongside the in-memory case — for a different reason (the in-memory half is suppressed
  because it is harmful, the null half because it is unwritable) but with the identical literal.
- FR-3 carries the null-`D` verdict for both arms, and **C-13** records the direct arm's breakage with
  its own C-8 release-note obligation, as NFR-6 requires of any newly discovered case.

So the rule implements FR-3 and FR-5 as approved; nothing here is a deviation.

#### 3. Message rendering — `DisplayName` and the five remedy templates

`Type.FullName` is unusable directly: for `Subscription<FakeChannelFactoryRequest>` it embeds
`Version=`, `Culture=` and `PublicKeyToken=`, which AC-14 forbids. `Type.Name` is unusable in the
other direction: eight transports name the class `ChannelFactory`, so AC-15 requires namespace
qualification. No display-name formatter exists in `Paramore.Brighter` today — `Extensions/TypeExtensions.cs`
is an `internal` netstandard2.0 polyfill for `IsAssignableTo`, and `Extensions/ReflectionExtensions.cs`
is `internal` to core — so the rule provides its own.

It is a **private static method on `ConsumerValidationRules`**, not a new public helper type. A
formatter with exactly one caller does not earn permanent public surface, and
[`.agent_instructions/testing.md`](../../.agent_instructions/testing.md)'s *Test Scope and Isolation*
rule — "an assembly is a module, it's surface area should be as narrow as possible" — points the same
way. The latent duplicate this leaves behind is recorded honestly under Negative. The algorithm avoids parsing `FullName`'s assembly payload entirely:

```
DisplayName(t):
  if !t.IsGenericType          → t.FullName ?? t.Name
  else                         → strip at '`' from t.GetGenericTypeDefinition().FullName
                                 + "<" + join(", ", t.GetGenericArguments().Select(DisplayName)) + ">"
```

Known simplification: nested types render with CLR's `+` separator. No acceptance criterion exercises
one — C-9's `AlphaBus`/`BetaBus` doubles are *namespaces*, not nested types — and handling `+`
would add branching for a case the feature does not have.

The message is assembled as a body plus one of FR-5's five remedy literals, appended last so that
AC-13/13a/13b/13c's "ends with" assertions hold. Only the five remedy literals are normative; the
body wording below is this ADR's proposal, constrained by AC-10a, AC-10c, AC-12, AC-14 and AC-15.

The body is **two independently varying clauses**, not a pair of fixed templates. The amended FR-5
admits a null `D` (item 2) and an empty candidate set (T4), and those two vary *different* clauses,
so a pair of templates cannot cover the space — it leaves the null-`D` cells rendering
`declares ChannelFactoryType ''`, which fails AC-10a, and the empty-candidate cell rendering
`one of ''`:

```
body            : Subscription type '{S}' {declared-clause} but {handed-clause} {remedy}

{declared-clause}, on D:
  D is non-null                → declares ChannelFactoryType '{D}'
  D is null                    → declares no ChannelFactoryType

{handed-clause}, on arm and candidate set:
  direct                       → will be handed '{F}'
  combined, non-empty          → will be handed one of '{F-list}'
  combined, empty              → will be handed no channel factory at all
```

The clauses are selected independently, which renders the cells the round-2 amendment created
acceptance criteria for rather than patching around them:

- **Null `D`, direct arm (AC-10a)** — `Subscription type 'S' declares no ChannelFactoryType but will
  be handed 'F' — use a subscription type whose ChannelFactoryType is F`. The declared clause carries
  the literal `no ChannelFactoryType` that AC-10a asserts "in place of a declared type name", and it
  is the same wording C-13's release-note obligation names, so the two agree by construction.
- **Null `D`, combined arm (AC-10b)** — the same declared clause with the `{F-list}` handed clause.
- **Empty candidate set (AC-10c)** — `… but will be handed no channel factory at all — add a channel
  factory to the combined channel factory`. `{F-list}` is never interpolated, so the body cannot
  render `one of ''`, and the message still contains no `is one of:`.

Both clause sets satisfy AC-15: `ChannelFactoryType` is excluded by the criterion's own `(?!Type)`
lookahead, and `no channel factory at all` is lower-case prose, not the token.

Three lexical constraints shaped that wording and are easy to breach accidentally:

- AC-15 forbids any occurrence of the **token** `ChannelFactory` that is neither preceded by `.` nor
  part of `ChannelFactoryType`, where "token" is now defined in the requirements as a word-boundary
  match and pinned to a normative regex. A composite identifier that merely *ends* in the word —
  `InMemoryChannelFactory`, `CombinedChannelFactory` — is a different token and is not an occurrence,
  so the body may render any of them freely. The body still says "channel factory" in lower-case prose
  where it refers to the concept, because that reads better, not because AC-15 compels it.
- AC-7 forbids naming `Paramore.Brighter.CombinedChannelFactory` **as the type the subscription will
  be handed**. The *outer* composite's type never enters `{F-list}`, which is built from
  `FactoryTypes` and reports the inner factories. The rule does name it in one case — when an inner
  factory is itself a `CombinedChannelFactory` — and AC-7's configuration (AC-6's flat
  `CombinedChannelFactory([DeclaredChannelFactory, NonMatchingChannelFactory])`) has no nesting, so
  the criterion holds. That case is accepted immediately below as a known message-quality limitation;
  it is **not** excused by re-scoping AC-7 to the outer composite.
- AC-13a and AC-13c forbid the substring `configure a channel factory of type` anywhere in a T3a/T3b
  message, so the body must not paraphrase the suppressed half.

**The nested case renders an honest message that is not a working remedy, and we accept that.** AC-10
requires an `Error` for `CombinedChannelFactory([CombinedChannelFactory([DeclaredChannelFactory])])`
with a `DeclaringSubscription` — so `D == typeof(DeclaredChannelFactory)`, which is not the in-memory
default, and FR-5 selects **T2**. `FactoryTypes` reports the inner factories' concrete types, so
`{F-list}` is literally `Paramore.Brighter.CombinedChannelFactory` and the message offers "…or use a
subscription type whose ChannelFactoryType is one of: `Paramore.Brighter.CombinedChannelFactory`".

Following that second half does **not** produce a working configuration, and the ADR should not
pretend otherwise. A subscription declaring `typeof(CombinedChannelFactory)` is selected by the
*outer* composite at `CombinedChannelFactory.cs:34`, which then calls `CreateSyncChannel` on the inner
composite; the inner composite scans its own `[DeclaredChannelFactory]` for a factory whose type is
`typeof(CombinedChannelFactory)`, finds none, and throws at `CombinedChannelFactory.cs:35-38`. FR-3's
word is deliberately "matches", not "routes", and the distinction bites exactly here.

We accept it rather than special-case it. The message's *first* half — "either configure a channel
factory of type `Paramore.Brighter.Core.Tests.Validation.TestDoubles.DeclaredChannelFactory`" — is a
working remedy, and it is the half a developer should follow; nesting composites is a configuration that is broken
whatever the subscription declares, so no rendering of this message describes a route that works.
Filtering the nested type out of `{F-list}` would leave an empty list and select T4, whose "add a
channel factory to the combined channel factory" is *less* informative about what is actually there.
Recorded under Negative as a known message-quality limitation.

`{F-list}` joins display names with `", "` in constructor order. The separator is defined **once** and
shared between body and remedy, so the two cannot disagree. An **empty** candidate set never reaches
`{F-list}` on **either** side: FR-5's template **T4** is selected first for the remedy, and the
handed-clause's empty form keeps it out of the body. T4 renders "— add a channel factory to the
combined channel factory", which names the actual fault and satisfies NFR-2's demand for a remedy,
where an empty list interpolated into T2/T3b would have ended the message at "is one of:" with nothing
after it. T4 is combined-arm only — the direct arm's candidate set is always exactly one type.

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
- **No new value type for the display name.** "Avoid primitive obsession" argues for one. Against:
  the name is produced and consumed within a few lines of the same private method, never crosses a
  boundary, and has no invariant to protect — a `DisplayName` wrapper would be a type whose only
  behaviour is `ToString`. A `string` is honest here.

### Implementation Approach

Sequenced so each step is independently testable, and structural changes precede behavioural ones.

1. **`CombinedChannelFactory.FactoryTypes`** (core, additive). A nullable backing field plus an
   expression-bodied property caching from `_factories` — **not** a get-only auto-property initialised
   from `_factories`, which cannot compile (CS0236; an auto-property initialised from the
   primary-constructor `factories` parameter compiles fine, and is the wrong answer for the
   re-enumeration reason above). Add `using System;`. Pure structural addition; the primary
   constructor is untouched and no existing behaviour changes.
2. **The display-name formatter** as a private static on `ConsumerValidationRules`, covered by
   AC-12/AC-14/AC-15's message assertions.
3. **`ChannelFactoryCompatible`**, with `ResolveCandidates` / `IsCompatible` / the five templates.
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

**Three cases this ADR surfaced are now acceptance criteria**, not ADR-only tests. An earlier draft
listed them here as uncovered and proposed testing them from this document; that was the wrong route —
C-9's double set is closed and every non-gateway criterion is bound to it, so cases that need a sixth
double are a requirements change. `requirements.md` was amended instead, and the tests are written
from it like any other:

- **AC-10a** — null `ChannelFactoryType` on the **direct** arm: one `Error`, body carrying
  `no ChannelFactoryType`, remedy T3a.
- **AC-10b** — the same on the **combined** arm: one `Error`, remedy T3b, plus the AC-9-shaped
  companion assertion that `CreateSyncChannel` throws `ConfigurationException`.
- **AC-10c** — a `CombinedChannelFactory` with no inner factories, against a named
  `DeclaringSubscription`: one `Error` ending with T4.

C-9 gains one double, `NullDeclaringSubscription`, which overrides `ChannelFactoryType` to return
`null` and nothing else — identity-only in exactly the sense the rest of the set is.

### Performance

Per subscription, per validation run, at startup, only when `ValidatePipelines()` is enabled: one
null check, at most one `is` test, and either one `IsAssignableFrom` or a linear scan of
`FactoryTypes` (in practice two or three entries). On the passing path the rule allocates only the
candidate list; `FactoryTypes` is materialised on first read and cached, so once per
`CombinedChannelFactory` for the single-threaded startup path that is its only caller. FR-2 step 3
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

These are real, and five of them are accepted breakage.

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
- **C-13 — a direct-arm subscription whose `ChannelFactoryType` override returns `null` stops
  starting.** `CombinedChannelFactory` is the only reader of that property in `src/`, so in a
  single-factory configuration a null declaration is never consulted today and the host runs. Under
  FR-3's null-`D` clause it becomes an `Error` and, by default, blocks startup. Accepted on D3's
  grounds — the population requires an override written deliberately to return `null`, and the remedy
  is one line — but it is new breakage, not a converted failure, and it carries its own release note.
- **The nested-composite message names a type that does not route.** For
  `CombinedChannelFactory([CombinedChannelFactory([…])])` the T2 remedy's second half offers
  `typeof(CombinedChannelFactory)`, which the outer composite will select and the inner composite will
  then reject. The message's first half is still a working remedy, and the configuration is broken
  whatever the subscription declares, so we accept it rather than special-case the rendering — but it
  is a message that can mislead a reader who follows the wrong half.
- **The combined arm restates `CombinedChannelFactory`'s routing predicate.** Two places now encode
  "exact type equality, non-recursive". The cost of the `FactoryTypes` choice over `CanRoute`, pinned
  by AC-9's companion assertion rather than eliminated.
- **The finding message is pinned by literal assertions.** T1/T2/T3a/T3b and AC-15's token rule mean
  the message text is effectively public API. Improving the wording later breaks tests, and AC-15's
  `ChannelFactoryType` carve-out is a constraint future editors will not guess.
- **A private display-name formatter that others will want.** Type rendering is a general need —
  `PipelineDiagnosticWriter` has the same problem, rendering bare `Type.Name` throughout — and we are
  solving it privately inside one rule because one caller does not justify permanent public surface.
  When a second caller appears this should be promoted, and until then there is a latent duplicate
  waiting to be written.
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
  promoting to a shared predicate later is a localised change, since the rule already asks the
  composite rather than reaching into it. **The residual risk is real and asymmetric**: for the
  reason given in the Decision, the companion assertion can only ever fire negatively — C-9's doubles
  throw on every member, so no test in this feature asserts that a configuration the rule passes is
  one the composite actually routes. AC-10's nested agreement point is likewise unpinned at runtime.
  If ADR 0073 or a later change introduces a non-throwing double, the positive direction should be
  pinned then.
- **Risk: `Error` severity blocks a host we did not anticipate.** NFR-6 is deliberately stated without
  a count because the exception set has grown twice under review. *Mitigation*: any newly discovered
  case gets its own constraint and its own release note rather than being absorbed silently;
  `ValidatePipelines` is opt-in and `throwOnError: false` unblocks every case except C-12; and ADR
  0073's sweep exists so the in-repo class cannot silently return.
- **Risk: the message assertions become a maintenance tax.** *Mitigation*: only the five remedy
  literals are normative; the body wording is this ADR's and may be revised as long as AC-10a,
  AC-10c, AC-12, AC-14 and AC-15 hold. Defining `{F-list}`'s separator once removes the most likely inconsistency.
- **Risk: a null `ChannelFactoryType` from an out-of-repo override.** *Mitigation*: handled as a
  defined input — a mismatch in both arms, guarded explicitly — rather than left to the framework's
  `"Rule evaluation failed"` path, which would block startup with the wrong `Source` and an unhelpful
  message. **The two arms are not the same kind of change and must not be described as if they were.**
  In the *combined* arm the configuration already throws at `CombinedChannelFactory.cs:37` on every
  start, so the rule converts a certain runtime failure into a named startup finding — no new
  breakage. In the *direct* arm there is no such consolation: `CombinedChannelFactory` is the only
  reader of `ChannelFactoryType` in `src/`, so a null declaration is never consulted, the host starts
  and the consumer works today. Blocking it **is** new breakage, and it is recorded as **C-13** with
  its own release-note obligation under C-8, as NFR-6 requires.
- **Risk: `FactoryTypes` is initialised from the constructor parameter rather than `_factories`.** An
  `IEnumerable` already consumed by `_factories = factories.ToList()` would yield an empty list, and
  the rule would then flag **every** subscription in **every** multi-bus application — a very loud,
  very wrong failure. This risk is *raised*, not lowered, by the CS0236 rule described above: the
  compiler rejects the correct-looking auto-property and accepts the wrong one, so the error message
  itself nudges an implementer toward the defect. *Mitigation*: the lazy-backing-field shape is
  normative and spelled out in full; and AC-6 (no false positives in a correct multi-bus
  configuration) catches a regression.
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
  routing-decision value object buys a permanent public abstraction in core to carry two fields
  between one producer and one consumer, which YAGNI rejects while `FactoryTypes` can still be wrapped
  in one later without breaking anyone.
- **`InternalsVisibleTo` from `Paramore.Brighter` to `Paramore.Brighter.ServiceActivator`.** Rejected
  because a standing project rule forbids the mechanism outright —
  [`.agent_instructions/testing.md`](../../.agent_instructions/testing.md) § *No InternalsVisibleTo*:
  "**NEVER use `InternalsVisibleTo` to expose internal classes for testing.**" That same rule
  prescribes the remedy we took ("make the interface **public** so it can be injected through the
  public API"). Secondarily it would open all of core's internals permanently in order to read one
  list, and it would do nothing for out-of-repo callers wanting to inspect a composite's routing.
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
