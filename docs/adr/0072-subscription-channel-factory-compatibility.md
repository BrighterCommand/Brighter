---
id: 0072-subscription-channel-factory-compatibility
title: "Validate Subscription and Channel Factory Compatibility at Startup"
status: Accepted
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

Accepted

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
  the five corrections ship with it. It is the direct inverse of ADR 0064's non-blocking `Warning`;
  the contrast is argued under Alternatives Considered, and turns on a mismatch failing on every
  start for eleven of twelve transports, or silently consuming the wrong bus. MQTT is the twelfth
  and works today — that is C-11, and it is why the exception set exists.
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
one per inner factory, in constructor order, duplicates included. Every read yields an **equal** list,
not necessarily the same instance. It never throws and never creates a channel.

**The shape above is normative**: a nullable backing field plus an expression-bodied property that
caches on first read, derived from the `_factories` field and **not** from the primary-constructor
`factories` parameter. The file also needs `using System;` added.

That shape decides four things:

- **Derive from `_factories`.** `IEnumerable<IAmAChannelFactory>` need not be re-enumerable, and
  `_factories = factories.ToList()` has already consumed it.
- **Not a get-only auto-property.** `CombinedChannelFactory` has a primary constructor and no
  constructor body, and an auto-property initialiser may not reference an instance member — CS0236.
  The compiler accepts the `factories` parameter and rejects `_factories`, so the error nudges an
  implementer toward the defect. Recorded under Risks.
- **Not a plain `=> _factories.Select(…).ToList()`**, which re-allocates on every read.
- **Not an explicit constructor.** Converting the class would make this a non-trivial edit to an
  existing core type rather than a pure addition.

**Thread-safety.** The property is not thread-safe, as its `<remarks>` states. This is acceptable
because of who calls it: the only consumer is the startup validation rule, reached through
`PipelineValidator.ValidateConsumers` → `EvaluateSpecs`, a sequential loop on one thread. A caller
needing concurrent access must synchronise, or promote the field to `Lazy<IReadOnlyList<Type>>` then.

##### Where the responsibility sits (the C-4 decision)

The question C-4 leaves open is not how to get the list out, but **where knowing which factory serves
a subscription belongs**. It is already split three ways, and the split is correct:

| Role | Stereotype | Responsibility |
|---|---|---|
| `Subscription` | information holder | *knowing* which factory identity it requires (`ChannelFactoryType`) — exists today, unchanged |
| `CombinedChannelFactory` | structurer / coordinator | *knowing* which identities it can serve, and *deciding* at runtime which one to dispatch to |
| The rule | service provider | *deciding* whether those two are compatible **for the purpose of validation** |

The third responsibility is the rule's, not the composite's, because the rule's decision is **strictly
larger**: it also covers the direct arm, applies assignability there, enforces C-2's asymmetric
verdict, and renders a remedy. A composite that knew about validation severities and remedy wording
would be a worse object.

So what the rule needs from the composite is knowledge in the composite's own routing vocabulary, and
`f.GetType()` **is** that vocabulary — it is what `CombinedChannelFactory` matches on. `FactoryTypes`
hands over exactly that: identities, not instances.

Three narrower or wider alternatives were weighed and rejected — exposing the factory *instances*,
tell-don't-ask via `CanRoute(Subscription)`, and `InternalsVisibleTo`. Each is recorded under
Alternatives Considered with its reason.

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

**Contract.** Input: one `Subscription`, plus the captured default. Output: satisfied, or unsatisfied
with **exactly one** `ValidationError` at `ValidationSeverity.Error` whose `Source` is
`$"Subscription '{s.Name}'"`. It reads only `Subscription.ChannelFactory`,
`Subscription.ChannelFactoryType` and `Subscription.Name`. It never touches `RequestType` (C-7 — this
rule deliberately does **not** vacuously pass for datatype-channel subscriptions, unlike the other
consumer rules), never creates a channel, never contacts a broker (NFR-3), and never mutates the
subscription.

**Shape: the `Specification<Subscription>` predicate + error-factory constructor**, as
`PumpHandlerMatch`, `HandlerRegistered` and `RequestTypeSubtype` use, per NFR-1 and with
`PumpHandlerMatch` as the named style model. Two nearby shapes are not used:
`DisposingSpecification<Subscription>` (see Alternatives Considered) and the collapsed
`Specification<T>(Func<T, IEnumerable<ValidationResult>>)` constructor, which exists for rules
yielding many findings per entity where FR-13 fixes this one at zero or one.

That shape resolves the effective factory **twice** on the failure path — the predicate runs, then the
error factory — exactly as `PumpHandlerMatch` re-fetches `handlerTypes` today. It is acceptable
because resolution is a pure function of `(subscription, defaultChannelFactory)`, the purity FR-2a and
AC-5 assert. The knowledge is stated once, in the private statics both lambdas call; only the
evaluation repeats, on a path about to throw.

**Internal structure — three private statics, each with one responsibility:**

| Helper | Responsibility |
|---|---|
| `ResolveCandidates(Subscription, IAmAChannelFactory?)` | *knowing* — FR-2's precedence, returning the arm and the ordered candidate `Type` list |
| `IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)` | *deciding* — FR-3's two arms and the null-`declared` case, and nothing else |
| `DisplayName(Type)` | *doing* — FR-5's display-name format |

`Arm` is a private nested `enum { Direct, Combined }` on `ConsumerValidationRules`, and
`ResolveCandidates` returns `(Arm, IReadOnlyList<Type>)`. Neither is public surface.

`ResolveCandidates` returns both together because they are one fact about one subscription; splitting
them would let a caller pair a combined list with a direct comparison. `IsCompatible` takes the arm
explicitly rather than re-deriving it by testing `F is CombinedChannelFactory`, which is what lets the
rest of the rule work in types only.

**A null declared type is a mismatch in both arms.** `ChannelFactoryType` is `public virtual Type`, so
an out-of-repo override may return `null`. The direct arm guards it with an explicit `D is null` test
rather than letting `D.IsAssignableFrom(…)` throw into the framework's `"Rule evaluation failed"`
path, which would block startup with a useless message and the wrong `Source`. This is not a
`try`/`catch` — ADR 0064's "rules must not catch" stands — it is a defined input handled in the
predicate.

The two arms reach that verdict differently, and the difference matters for breakage, not for the
rule: the combined arm follows from FR-3's *iff* and mirrors a runtime that already throws, while the
direct arm is new breakage. Recorded as **C-13** under Risks. Skipping the case was considered and
rejected; see Alternatives Considered.

**This changed the requirements, and the requirements were amended rather than deviated from.** An
earlier draft claimed the rendering "costs no new normative surface". That was wrong twice: FR-5 item
2 required `D` as a `Type.FullName`, unsatisfiable when there is no type; and a null `D` satisfies
T1's stated condition, so preferring T3a/T3b changes a **normative selection rule**, not merely
wording. Both are settled in `requirements.md`:

- FR-5 item 2 admits the literal `no ChannelFactoryType` when `D` is null.
- FR-5's selection table is five ordered, total conditions, with T3a/T3b taking `D is null` alongside
  the in-memory case — a different reason, the identical literal.
- FR-3 carries the verdict for both arms; C-13 records the direct arm's breakage under C-8.

So the rule implements FR-3 and FR-5 as approved; nothing here is a deviation.

#### 3. Message rendering — `DisplayName` and the five remedy templates

`Type.FullName` is unusable directly: for `Subscription<FakeChannelFactoryRequest>` it embeds
`Version=`, `Culture=` and `PublicKeyToken=`, which AC-14 forbids. `Type.Name` is unusable in the
other direction: eight transports name the class `ChannelFactory`, so AC-15 requires namespace
qualification. No display-name formatter exists in `Paramore.Brighter` today — `TypeExtensions` and
`ReflectionExtensions` are both `internal` — so the rule provides its own, as a **private static on
`ConsumerValidationRules`**. A formatter with one caller does not earn permanent public surface, and
[`.agent_instructions/testing.md`](../../.agent_instructions/testing.md)'s *Test Scope and Isolation*
rule — "an assembly is a module, it's surface area should be as narrow as possible" — points the same
way. The latent duplicate that leaves behind is recorded under Negative.

```
DisplayName(t):
  if !t.IsGenericType          → t.FullName ?? t.Name
  else                         → strip at '`' from t.GetGenericTypeDefinition().FullName
                                 + "<" + join(", ", t.GetGenericArguments().Select(DisplayName)) + ">"
```

Known simplification: nested types render with CLR's `+` separator. No acceptance criterion exercises
one — C-9's `AlphaBus`/`BetaBus` doubles are *namespaces*, not nested types.

**The message is a body plus one of FR-5's five remedy literals**, appended last so the "ends with"
assertions of AC-10a, AC-10b, AC-10c, AC-13, AC-13a, AC-13b and AC-13c all hold. Only the five
literals are normative; the body below is this ADR's.

**The body's constraint set — stated once here, referred to everywhere else.** The body may be
revised as long as **AC-5, AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14, AC-15 and
AC-30** continue to hold. None of the eleven is satisfied by the fixed remedy literals alone, so each
is something a revision of the body can break:

- **AC-5 and AC-30 require the message to be a pure function of `({S}, D, arm, candidate types)`.**
  A body revision must not interpolate an instance, an identity hash, or anything that distinguishes
  a back-filled `subscription.ChannelFactory` from a resolved default — AC-5 evaluates the same
  configuration either side of `DispatchBuilder`'s back-fill and demands a byte-identical `Message`,
  and AC-30 demands the same across two runs. This is the same reduction-to-types discipline the
  Architecture Overview relies on, applied to the message rather than to the verdict.
- **AC-7** forbids naming `Paramore.Brighter.CombinedChannelFactory` as the type the subscription
  will be handed, which only the handed clause can breach.
- **AC-10a and AC-10b** require the literal `no ChannelFactoryType`, which no remedy template
  contains; **AC-10c** forbids `is one of:`.
- **AC-13a and AC-13c** forbid the substring `configure a channel factory of type` anywhere in the
  message.
- **AC-12 and AC-14** govern the rendered type names; **AC-15** governs the `ChannelFactory` token.

The body is **two independently varying clauses**, not a pair of fixed templates. The amended FR-5
admits a null `D` (item 2) and an empty candidate set (T4), and those vary *different* clauses, so a
pair of templates cannot cover the space:

```
message         : {body} {remedy}
body            : Subscription type '{S}' {declared-clause} but {handed-clause}

{declared-clause}, on D:
  D is non-null                → declares ChannelFactoryType '{D}'
  D is null                    → declares no ChannelFactoryType

{handed-clause}, on arm and candidate set:
  direct                       → will be handed '{F}'
  combined, non-empty          → will be handed one of '{F-list}'
  combined, empty              → will be handed no channel factory at all
```

Selecting the clauses independently renders all nine reachable cells, including the three the
round-2 amendment created criteria for:

- **Null `D`, direct arm (AC-10a)** — the declared clause carries the literal `no ChannelFactoryType`
  that AC-10a requires "in place of a declared type name". A fixed template would have rendered
  `declares ChannelFactoryType ''`, which does not contain it. It is also the wording C-13's
  release-note obligation names, so the two agree by construction.
- **Null `D`, combined arm (AC-10b)** — the same declared clause with the `{F-list}` handed clause.
- **Empty candidate set (AC-10c)** — `{F-list}` is never interpolated, so the body cannot render
  `one of ''`, and the message contains no `is one of:`.

Both clause sets satisfy AC-15: `ChannelFactoryType` is excluded by the criterion's own `(?!Type)`
lookahead, and `no channel factory at all` is lower-case prose, not the token.

**Why the empty-set clause is not a second undeclared deviation.** FR-5 item 3 quantifies over the
candidate set — "the `Type.FullName` of *every inner factory in the candidate factory set*" — so an
empty set satisfies it **vacuously**, with zero types to name. That is what distinguishes it from
item 2, whose *single* required type was unsatisfiable when `D` is null and therefore did force the
round-2 amendment. `will be handed no channel factory at all` is accordingly body wording this ADR
owns under the constraint set above, not a change to a normative rule. No requirements amendment is
needed, and this paragraph records why rather than leaving the reasoning outside the document.

**These are the easiest of them to breach accidentally:**

- **AC-15** — "token" is a word-boundary match pinned to a normative regex, so a composite identifier
  merely *ending* in the word — `InMemoryChannelFactory`, `CombinedChannelFactory` — is a different
  token, and the body may render any of them freely.
- **AC-13a/AC-13c** — the prohibition is on the whole message, so the body must not paraphrase the
  suppressed half in other words.
- **AC-10a/AC-10b** — the literal must appear in the body, which is why the declared clause varies
  rather than interpolating an empty `{D}`.
- **AC-7** — the prohibition is on the handed clause. The *outer* composite's type never enters
  `{F-list}`, which is built from `FactoryTypes` and reports the inner factories, so AC-7's own flat
  configuration satisfies it. **D5 does not relax it**: D5 accepts only that the *nested* case renders
  an inner `CombinedChannelFactory` in `{F-list}`, a configuration AC-7 does not cover. Any revision
  of the handed clause must still satisfy AC-7 — in particular it must not name the composite being
  handed, however that is phrased.

**The nested case names a type that does not route, and we accept that.** This is **D5**, recorded
in `requirements.md` under *Maintainer decisions already taken*: the message may name
`Paramore.Brighter.CombinedChannelFactory` among the types the subscription will be handed even
though that type does not route, because filtering it out would empty the candidate set and select
T4, whose wording says less about what is configured. It bounds the *wording* of the nested case and
nothing else — AC-10's verdict and AC-7's prohibition both stand unchanged. `FactoryTypes`
reports an inner `CombinedChannelFactory` by its own concrete type, so for
`CombinedChannelFactory([CombinedChannelFactory([DeclaredChannelFactory])])` `{F-list}` is literally
`Paramore.Brighter.CombinedChannelFactory`. FR-3's word is "matches", not "routes", and the
distinction bites exactly here. The consequence, and why it is tolerable, is under Negative.

`{F-list}` joins display names with `", "` in constructor order. The separator is defined **once** and
shared between body and remedy, so the two cannot disagree. An empty candidate set reaches `{F-list}`
on neither side: T4 is selected first for the remedy, and the handed clause's empty form keeps it out
of the body. T4 names the actual fault, which is what satisfies **NFR-2**'s demand that every finding
carry a remedy — an empty list interpolated into T2/T3b would have ended the message at "is one of:"
with nothing after it, stating no remedy at all. T4 is combined-arm only — the direct arm's candidate
set is always exactly one type.

**T3a/T3b's suppression is architectural, not cosmetic.** When `D` is `InMemoryChannelFactory` the
subscription is a plain `Subscription`/`Subscription<T>`, and "configure a channel factory of type
`Paramore.Brighter.InMemoryChannelFactory`" is the *cheaper* of the two remedies in the case this
feature fires most often — and following it produces a consumer that silently reads an in-memory bus
while believing it reads SQS. That is what C-2 names and C-12 withdraws. Offering it as an equal
option would make the rule an accessory to the defect it detects.

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
whole role to a rule that needs one member widens its dependency and invites it to iterate
`Subscriptions`, which is `PipelineValidator.ValidateConsumers`'s job. The narrower dependency also
keeps the rule unit-testable with a bare `new DeclaredChannelFactory()` and no options object, which
is what C-9's doubles require. `GetService` rather than `GetRequiredService` is deliberate: an absent
registration degrades to FR-2 step 3, the same fallback a null `DefaultChannelFactory` takes.

Two properties follow from where this lambda sits, both verified:

- `IAmConsumerOptions` is registered by `AddConsumers` **before** `RegisterConsumerValidationSpecs` is
  called, and the lambda runs at *resolution* time, so the options are fully configured by then. C-6's
  snapshot semantics are unchanged: a default factory assigned after `ValidatePipelines()` is not seen.
- The lambda is invoked only from `sp.GetServices<ISpecification<Subscription>>()` inside the
  `IAmAPipelineValidator` factory, and `ValidatePipelines(enabled: false)` returns the builder before
  registering that factory. With validation disabled the rule is never constructed — FR-6 and NFR-4
  are satisfied by the existing wiring, not by a guard inside the rule.

#### 5. The five corrections (FR-7 to FR-11) — a consequence, not an architecture

Each is a one-line `public override Type ChannelFactoryType => typeof(…);` in its own gateway
assembly. Verified targets: `GcpPubSubChannelFactory` (which does implement `IAmAChannelFactory`),
`Paramore.Brighter.MessagingGateway.MQTT.ChannelFactory`,
`Paramore.Brighter.MessagingGateway.AWSSQS.ChannelFactory`,
`Paramore.Brighter.MessagingGateway.AWSSQS.V4.ChannelFactory` and `PostgresChannelFactory`.

They carry no design decision, but they are **load-bearing for D1** — they are what makes `Error`
severity defensible, which is why D2 puts them in this change rather than a follow-up. The argument
is made once under Alternatives Considered ("Ship the rule now and correct the five gateways
later"). Their cost is C-12, recorded below.

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
   AC-1 to AC-13c, AC-19 and AC-30 all exercise this against C-9's doubles with no container and no
   host.
4. **Registration** in `RegisterConsumerValidationSpecs`. AC-16, AC-17, AC-17a and AC-18 then exercise the
   host-start behaviour in `tests/Paramore.Brighter.Extensions.Tests`, which must declare its **own**
   copies of the doubles — test projects here do not reference one another (C-9).
5. **The five corrections**, one per gateway assembly, each with AC-20 to AC-26 in that gateway's own
   test project. AC-26f additionally runs the rule, and so lands only in `AWS.Tests`, `AWS.V4.Tests`
   and `MQTT.Tests`, the three of the five that reference `Paramore.Brighter.ServiceActivator`.
6. **Release notes** for C-8's obligations — C-10, C-11, C-12 and C-13 as separate entries, with
   C-12 flagged as the one `throwOnError: false` does not avoid, and C-13 flagged as one it does.

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
- **The finding message is pinned by literal assertions.** All five remedy literals
  (T1/T2/T3a/T3b/T4) and AC-15's token rule mean
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
- **Release-note burden.** Four distinct breaking-change notes (C-10, C-11, C-12, C-13) for one
  feature is a lot to ask a reader of V10.X notes to absorb.

### Risks and Mitigations

- **Risk: the rule and `CombinedChannelFactory` drift apart.** If the composite's routing ever changes
  (OOS-5 says it will not here), the rule silently becomes wrong — and a false *negative* is the
  dangerous direction, since it restores the silent failure the feature exists to remove.
  *Mitigation*: AC-9's companion assertion ties verdict to runtime behaviour; `FactoryTypes`'s XML
  documentation states the routing contract at the point a future editor would change it; and
  promoting to a shared predicate later is a localised change, since the rule already asks the
  composite rather than reaching into it. **The residual risk is real and asymmetric**: the
  companion assertion can only ever fire negatively. C-9's doubles throw on every member, so the
  mirror-image assertion — a correct multi-bus configuration where the rule is silent *and* the
  composite successfully routes — cannot be written as a *successful-routing* assertion. The most the
  approved double set can assert is that `CreateSyncChannel` throws the selected inner double's own
  exception rather than `ConfigurationException`, which pins selection but not dispatch. AC-6 is
  therefore rule-only, with no runtime counterpart for the dispatch half, and AC-10's nested agreement point is likewise
  unpinned at runtime. If ADR 0073 or a later change introduces a non-throwing double, the positive
  direction should be pinned then.
- **Risk: `Error` severity blocks a host we did not anticipate.** NFR-6 is deliberately stated without
  a count because the exception set has grown twice under review. *Mitigation*: any newly discovered
  case gets its own constraint and its own release note rather than being absorbed silently;
  `ValidatePipelines` is opt-in and `throwOnError: false` unblocks every case except C-12; and ADR
  0073's sweep exists so the in-repo class cannot silently return.
- **Risk: the message assertions become a maintenance tax.** *Mitigation*: only the five remedy
  literals are normative; the body wording is this ADR's and may be revised as long as **the body's
  constraint set** holds — defined once in Key Components §3 rather than restated here, so the two
  cannot drift apart. Defining `{F-list}`'s separator once removes the most likely inconsistency.
- **Risk: a null `ChannelFactoryType` from an out-of-repo override.** *Mitigation*: handled as a
  defined input — a mismatch in both arms, guarded explicitly — rather than left to the framework's
  `"Rule evaluation failed"` path, which would block startup with the wrong `Source` and an unhelpful
  message. **The two arms are not the same kind of change and must not be described as if they were.**
  In the *combined* arm the configuration already throws at `CombinedChannelFactory.cs:37` on every
  start, so the rule converts a certain runtime failure into a named startup finding — no new
  breakage. In the *direct* arm there is no such consolation: the host starts and the consumer works
  today, so blocking it **is** new breakage. Recorded as **C-13** under Negative, which carries the
  evidence and the D3 grounds, with its own release-note obligation under C-8 as NFR-6 requires.
- **Risk: `FactoryTypes` is initialised from the constructor parameter rather than `_factories`.** An
  `IEnumerable` already consumed by `_factories = factories.ToList()` would yield an empty list, and
  the rule would then flag **every** subscription in **every** multi-bus application — a very loud,
  very wrong failure. This risk is *raised*, not lowered, by the CS0236 rule described above: the
  compiler rejects the correct-looking auto-property and accepts the wrong one, so the error message
  itself nudges an implementer toward the defect. *Mitigation*: the lazy-backing-field shape is
  normative and spelled out in full; and a dedicated test builds a `CombinedChannelFactory` from a
  single-pass sequence and asserts `FactoryTypes`. AC-6 (no false positives in a correct multi-bus
  configuration) does **not** catch this regression: its configuration passes an array-backed
  collection, which re-enumerates successfully, so the defect only shows on a single-pass source.
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
  inner factory, bypassing the composite) where the rule needs only knowledge. No caller in this
  feature needs an instance — FR-3's combined arm is `f.GetType() == D`, **FR-5 item 3** needs the
  display names of *types*, and AC-9's companion assertion calls `CreateSyncChannel` on the
  **composite**, not on an inner factory. A wider public API than the requirement, permanently, for
  no caller also runs against
  [`.agent_instructions/testing.md`](../../.agent_instructions/testing.md)'s *Test Scope and
  Isolation* rule: "an assembly is a module, it's surface area should be as narrow as possible" and
  "do not expose more than is necessary from an assembly".
- **Tell-don't-ask: `CombinedChannelFactory.CanRoute(Subscription)`.** Genuinely attractive — it would
  make drift impossible. Rejected because FR-5's T2/T3b need the ordered display names of *all* inner
  factories for the remedy clause, so a boolean cannot render the finding; supplying both `CanRoute`
  and `FactoryTypes` gives two public members where one derives trivially from the other, and a
  routing-decision value object buys a permanent public abstraction in core to carry two fields
  between one producer and one consumer, which YAGNI rejects while `FactoryTypes` can still be wrapped
  in one later without breaking anyone. There is a secondary cost too: a public `CanRoute` on a
  channel factory reads as a runtime capability check and invites callers to pre-flight before every
  `CreateSyncChannel`, which is not a pattern we want to seed.
- **`InternalsVisibleTo` from `Paramore.Brighter` to `Paramore.Brighter.ServiceActivator`.** Rejected
  because a standing project rule forbids the mechanism outright —
  [`.agent_instructions/testing.md`](../../.agent_instructions/testing.md) § *No InternalsVisibleTo*:
  "**NEVER use `InternalsVisibleTo` to expose internal classes for testing.**" That same rule
  prescribes the remedy we took ("make the interface **public** so it can be injected through the
  public API"). The rule's bullets are framed around testing and our caller is production code in
  another assembly, but the section heading and the `NEVER` are unqualified, and the reasoning behind
  them — callers couple to behaviour, not internals — applies with more force to a production
  consumer, not less. Secondarily it would open all of core's internals to ServiceActivator
  permanently in order to read one list, and would do nothing for out-of-repo callers wanting to
  inspect a composite's routing. Consistent with the rule, the mechanism appears nowhere in `src/`
  today — the only occurrence of the string is a comment in `SpannerBoxMigrationRunner.cs`.
- **Skip validation when `ChannelFactoryType` is null**, rather than treating it as a mismatch.
  Rejected: it would leave the rule silent about a configuration certain to throw at Dispatcher start
  in the combined arm — the same inversion this ADR refuses for MQTT under C-11. The case where
  failure is currently *silent* is the last case the rule should be silent about. Silence is the one
  outcome worse than an imperfect message. The direct arm's cost of not skipping is C-13.
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
