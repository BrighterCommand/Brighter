---
id: 0073-gateway-channel-factory-type-regression-guard
title: "Gateway ChannelFactoryType Regression Guard"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-09-21
summary: "Implements FR-12's regression guard as one new public static type in Paramore.Brighter, SubscriptionChannelFactoryDeclaration, exposing a pure (subscriptionType, declaredFactoryType) predicate that returns the reason a declaration is unsound (null when sound) and an assembly sweep that reads ChannelFactoryType from uninitialised instances via GetUninitializedObject, so no constructor runs, de-duplicating a base/derived pair only where the derived type declares no override of its own; the twelve per-gateway tests are generated from one new Liquid template through a new GatewayConformance configuration section, assert the reported subject set exactly plus all reasons null, carry no reflection logic of their own, and run in CI's infrastructure-free build job."
tags:
  - "testing"
  - "test-generation"
  - "transports"
  - "configuration"
---

# 73. Gateway ChannelFactoryType Regression Guard

Date: 2026-09-21

## Status

Accepted

## Context

**Parent Requirement**: [specs/0037-validate-subscription-channel-factory/requirements.md](../../specs/0037-validate-subscription-channel-factory/requirements.md)

**Scope**: This ADR focuses specifically on **FR-12's regression sweep** — the construction-free
mechanism for reading `Subscription.ChannelFactoryType`, the pure predicate the sweep is built from,
where that predicate lives, and how the twelve per-gateway sweep tests are produced. It satisfies
AC-27, AC-28 and AC-29. Its sibling,
[ADR 0072](0072-subscription-channel-factory-compatibility.md), owns the startup validation rule
(FR-1 to FR-6, FR-13) and the five `ChannelFactoryType` corrections (FR-7 to FR-11); 0072 defers
FR-12 here and nothing it settled is re-decided.

### The problem

`Subscription.ChannelFactoryType` (`src/Paramore.Brighter/Subscription.cs:172`) is a
`public virtual Type` defaulting to `typeof(InMemoryChannelFactory)`. A gateway subscription that
never overrides it inherits that default silently. It compiles, it ships, and
`CombinedChannelFactory` can then never route it. Five of the twelve shipped gateway subscription
families were wrong on exactly this point — two naming an `IAmAMessageConsumerFactory`, three
declaring no override at all — and nothing in the build noticed for years. ADR 0072 corrects those
five. Without a guard, the same class of defect returns the next time a gateway is added, and
returns invisibly.

### The forces

**The property cannot be read by constructing a subscription.** It is an instance property, so an
instance is needed. None of the twenty-four shipped gateway subscription types declares a
parameterless constructor — a constructor whose parameters are all optional does not produce one —
so `Activator.CreateInstance(Type)` throws `MissingMethodException` on every one of them.
Construction is additionally blocked for GCP Pub/Sub, Postgres, RocketMQ and Kafka, whose
constructors default `messagePumpType` to `MessagePumpType.Unknown`, which the base constructor
rejects with `ConfigurationException` (`src/Paramore.Brighter/Subscription.cs:213`). The guard must
therefore obtain the value without running a constructor, and without touching a broker (NFR-3).

**The guard must reach twelve test projects that share no test-support assembly.** A test project
can sweep only assemblies it references, and none of the twelve references more than one gateway
(C-9). Verified against the twelve `.csproj` files: `Paramore.Brighter.Base.Test` is referenced by
three (Gcp, MSSQL, PostgresSQL), `Paramore.Test.Helpers` by one (MQTT), and
`Paramore.Brighter.ServiceActivator` by six (AWS, AWS.V4, MQTT, RMQ.Async, RMQ.Sync, RocketMQ). Only
`Paramore.Brighter` itself is referenced — directly or transitively through the gateway — by all
twelve.

**The requirements already settled whether a new public type may be added; what remains is which
design to spend it on.** FR-12 fixes this expressly: "Introducing **one new public type** to host the
predicate is therefore expressly permitted notwithstanding NFR-5, **which constrains changes to
*existing* abstractions**" (`specs/0037-validate-subscription-channel-factory/requirements.md`, FR-12),
on the stated ground that AC-27 needs a caller in each of the twelve gateway test projects and only
`Paramore.Brighter` is referenced by all twelve. The trailing clause matters: NFR-5 is not being
overridden by exception, it is **not engaged** — it protects the members of `IAmAChannelFactory`,
`Subscription` and `IAmConsumerOptions`, and a new type adds nothing to any of them. **This ADR does not re-derive that permission, and does not need to.** It
takes the permission as given and decides where the predicate lives and how the twelve tests are
produced.

**It is worth being plain about what the permission is.** `.agent_instructions/testing.md`
§ *Narrow and deep — and when widening the surface is legitimate* asks, before any widening, "**is
there a path through the module's existing exports to the behaviour under test?** … If there is,
widening is unjustified — test through that path." Here a path does exist:
`Subscription.ChannelFactoryType`, `IAmAChannelFactory` and `InMemoryChannelFactory` are all already
public, so the comparison can be written entirely from outside the module — and the generator-emitted
predicate under Alternatives Considered does exactly that, adding nothing to the shipped package. So
this widening is **not forced by impossibility**. It is authorised by the requirements, on
reachability grounds, and chosen over an alternative that would also have worked. Read honestly, that
makes it an **authorised testing concession** rather than a gap in the module's contract that the
design merely noticed — which is what FR-12's own rationale says it is.

**One project principle is genuinely in tension, and the requirement is treated as the higher
authority.** `.agent_instructions/design_principles.md` says "do not add new types without necessity",
and on the reading just given this type is not *necessary* — a generator-emitted predicate would have
worked. The principle is not discharged by argument here; it is **overridden by FR-12**, which is a
requirement for this feature and decided the question expressly after the alternatives were weighed.
Recording that plainly is better than stretching "necessity" to cover a design that was chosen rather
than forced.

**What § *Narrow and deep* supplies is therefore the obligation, not the permission**: "widen
**honestly**: make it public, and record what was widened and why (in the ADR, or the PR)", and the
after-the-fact check, "does anything other than a test ever call it? If nothing ever does, it was a
testing concession after all, and should be revisited." Both are discharged here: the widening is
recorded under Negative with its cost stated, and the check is answered there rather than deferred.

**Two things then distinguish the chosen design from the generator-emitted predicate, neither of them
the bare permission.** First, a rule whose logic ships in the package is one the package can be held
to; a rule that exists only in this repository's generated test files is a private convention, and
`CombinedChannelFactory` already depends on that rule at runtime —
`_factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType)`, at
`CombinedChannelFactory.cs:34`, `:46` and `:59`, once in each creation method, throwing
`ConfigurationException` when a subscription that inherited the default finds no match. Second,
**C-10**: out-of-repo gateway authors face the identical defect, and a predicate in the core package
is something they can point at their own assembly, which a template-emitted copy can never be. C-10
is a **benefit that separates the two designs**, not a requirement either has to meet — the
requirements accept that neither the corrections nor the sweep reaches out-of-repo types. Both
reasons are about where the rule lives, which is exactly the question FR-12 left to this ADR.

**`InternalsVisibleTo` is rejected for the reason behind the rule, not merely by the rule.** It is
forbidden outright — "**NEVER** use `InternalsVisibleTo` to expose internal classes for testing" —
because it makes `internal` a lie. The comfort of `internal` is that a member may be refactored
freely, since every dependency on it lives inside the module; once tests in another assembly bind to
it that is false, and refactoring breaks them. The member has been made public in effect, just to a
narrower audience, while the keyword claims otherwise. Honest widening is preferable precisely
because it forces the question this section has just answered in the open — *why does this belong on
the module, and at what cost?* — instead of settling it silently in an assembly attribute. (The section's own prescription, "make the
interface **public** so it can be injected through the public API", is about injecting a dependency
for testing; it is cited here for its prohibition, not as endorsement of this shape.)

**Twelve near-identical tests are a drift hazard.** FR-12 states it directly: "Copying the predicate
into twelve test projects is not acceptable — twelve copies of a guard drift." The repository
already has the machinery for this problem: the Liquid test generator of
[ADR 0035](0035-generated-test.md) and [ADR 0037](0037-add-messaging-gateway-generated-test.md).

## Decision

The guard is **one new public static type in `Paramore.Brighter`**,
`SubscriptionChannelFactoryDeclaration`, exposing a pure predicate over
`(subscriptionType, declaredFactoryType)` and an assembly sweep that reads `ChannelFactoryType`
from **uninitialised instances**. The twelve per-gateway sweep tests are **generated** from a single
new Liquid template owned by the test generator, and are run in CI's infrastructure-free `build`
job.

### Architecture Overview

```
  src/Paramore.Brighter  (shipped core package)
  ┌──────────────────────────────────────────────────────────────┐
  │ public static class SubscriptionChannelFactoryDeclaration    │
  │                                                              │
  │   Check(Type subscriptionType, Type? declaredFactoryType)    │  pure; no reflection
  │        -> string?   null = sound, else the reason            │  beyond IsAssignableFrom
  │                                                              │
  │   Sweep(Assembly gatewayAssembly)                            │
  │     -> IReadOnlyList<(Type Subject, string? Reason)>         │
  │        one entry per subject; Reason null = sound            │
  │                                                              │
  │   candidates -> subsume -> close generics -> read -> Check   │
  │                                     │                        │
  │                            GetUninitializedObject            │  no constructor runs
  └──────────────────────────────────────────────────────────────┘
        ▲                                        ▲
        │ Sweep                                  │ Check + Sweep
        │                                        │
  ┌─────┴──────────────────────────┐    ┌────────┴─────────────────────────┐
  │ 12 GENERATED sweep tests       │    │ Core.Tests, hand-written         │
  │ one per gateway test project   │    │  - AC-28 synthetic negatives     │
  │ from ONE Liquid template       │    │  - AC-28 doubles SWEPT: the      │
  │                                │    │    reading path's only negative  │
  │                                │    │  - AC-29 constructs-nothing      │
  │                                │    │  - synthetic generic: closing    │
  │                                │    │  - base/derived pairs: subsump-  │
  │                                │    │    tion, both directions         │
  │                                │    │  - throwing getter, bad con-     │
  │                                │    │    straints: reason, not skip    │
  └────────────────────────────────┘    └──────────────────────────────────┘
        ▲
        │ renders
  ┌─────┴────────────────────────────────────────────────────────┐
  │ tools/Paramore.Brighter.Test.Generator                       │
  │   Templates/GatewayConformance/*.liquid                      │
  │   Generators/GatewayConformanceGenerator.cs (Suites/Plan)    │
  │   Configuration: "GatewayConformance" section                │
  └──────────────────────────────────────────────────────────────┘
```

### Key Components

This section specifies the components. The reasoning for choices made against live alternatives is in
*Alternatives Considered*; the things that could go wrong with them are in *Risks and Mitigations*. Each
decision is stated here once.

#### `SubscriptionChannelFactoryDeclaration`

A new `public static class` in namespace `Paramore.Brighter`, in `src/Paramore.Brighter`. In
Responsibility-Driven Design terms a **service provider**: it answers a question on request and holds no
state. It has exactly **two public members**, and is the one new public type FR-12 permits.

#### `public static string? Check(Type subscriptionType, Type? declaredFactoryType)`

The pure predicate FR-12 requires. Returns `null` when the declaration is sound, otherwise the single
reason it is not. Three branches, evaluated in order, first failure wins. They are **named, not
numbered**, because FR-12 numbers its own two conditions differently:

| Branch | Condition | Reason |
|---|---|---|
| **null** | `declaredFactoryType is null` | *"… declares no channel factory type (`ChannelFactoryType` returned null)."* |
| **not-a-channel-factory** (FR-12 condition 1) | `!typeof(IAmAChannelFactory).IsAssignableFrom(declaredFactoryType)` | *"… declares `{full name}`, which does not implement `Paramore.Brighter.IAmAChannelFactory`."* |
| **inherited-default** (FR-12 condition 2) | `declaredFactoryType == typeof(InMemoryChannelFactory)` | *"… declares `Paramore.Brighter.InMemoryChannelFactory`. A shipped gateway subscription must declare its own transport's channel factory; a type that does not override `ChannelFactoryType` inherits this default."* |

The **null** branch is this ADR's addition — FR-12 states two conditions, and a `ChannelFactoryType`
override may return `null` (C-13). `subscriptionType` names the subject only; a null argument throws
`ArgumentNullException`. Every type in a reason is rendered with `Type.FullName`, because eight transports
name their channel factory class `ChannelFactory` and `Type.Name` would produce "expected
`ChannelFactory`, got `ChannelFactory`" — the same reasoning FR-5 applies to the rule's messages.

#### `public static IReadOnlyList<(Type Subject, string? Reason)> Sweep(Assembly gatewayAssembly)`

Two terms, used precisely throughout: **candidates** are the types step 1 finds; **subjects** are the
candidates that survive step 2. `Sweep` returns **one entry per subject**, carrying that type and its
`Reason` — `null` when the declaration is sound.

- **`Subject` is the candidate as discovered, not as read.** For a generic subject it is the **open
  definition** (`Foo<>`), not the closed construction step 3 builds. The closed type exists only to be
  instantiated and read. This is the reported identity, so it is what an expected-subject configuration
  names (`typeof(Foo<>)` is valid C#) and what a reason message renders — a closed construction's
  `FullName` is ``Foo`1[[Paramore.Brighter.Command, …, Version=…]]``, which is unusable in a message and
  unrenderable from a configuration string.
- A subsumed candidate gets **no** entry: it declares no `ChannelFactoryType`, so its value *is* its
  base's, and its base is a subject.
- The result is **empty only when the assembly has no candidates**. Base chains are finite and acyclic,
  so a non-empty candidate set has a root, and a root has no candidate ancestor and is never subsumed.
- Entries are ordered by `Subject.FullName`, ordinal. `Assembly.GetTypes()` order is unspecified, so the
  sort is what makes repeated runs byte-identical.

Four steps:

1. **Candidates.** Non-abstract classes that **are** `Paramore.Brighter.Subscription` or whose base chain
   reaches it. The inclusive reading is FR-12's — "every non-abstract type **assignable to**
   `Subscription`" — and a type is assignable to itself. It matters only in `Paramore.Brighter` itself,
   where `Subscription` is the root candidate that subsumes `Subscription<T>`, `InMemorySubscription` and
   `InMemorySubscription<T>`, none of which declares an override; excluding it would leave
   `Subscription<>` and `InMemorySubscription` as two unsubsumed roots. No gateway assembly contains
   `Subscription` itself, so the twelve are unaffected. The chain is walked explicitly rather than tested
   with `IsAssignableFrom`, which behaves surprisingly for open generic definitions — and every gateway
   assembly contains one.
2. **Subsumption.** A candidate is dropped when it does **not** itself declare `ChannelFactoryType`
   (`BindingFlags.DeclaredOnly`) **and** an ancestor in its base chain is also a candidate in the same
   assembly. This is FR-12's "a base/derived pair … MUST be reported at most once **when the derived type
   declares no `ChannelFactoryType` of its own**": such a derived type cannot disagree with its base. A
   derived type that **does** declare its own override is never dropped, because it can. **Ancestry is
   matched on the generic type definition**: subsumption runs before closing, so candidates are open
   definitions (`Foo<>`) while a base chain yields closed constructions (`Foo<Bar>`), and an ancestor that
   is a constructed generic is reduced with `GetGenericTypeDefinition()` before comparison. Without that
   reduction a `FooBar : Foo<Bar>` declaring no override would never match its own base, and would be
   reported twice — or, read the other way, silently dropped. **No shipped assembly exercises the
   reduction**: all twelve pairs are `XSubscription<T> : XSubscription` with a *non-generic* base
   (`RocketMqSubscription<T>` to `RocketSubscription`, `…/RocketMQ/RocketMqSubscription.cs:117` and `:10`;
   `SqsSubscription<T>` to `SqsSubscription`; and so for all twelve), so no reduction happens in any of
   them. The **no-override** pair among the two `Core.Tests` subsumption pairs below therefore has a
   **constructed generic base** (`Derived : Base<Command>`, `Derived` declaring nothing), which covers the
   branch at no extra cost. It has to be that pair: the ancestry comparison — the only place the reduction
   is applied — is load-bearing only when the derived type declares no override of its own. On the
   *declares-its-own* pair the drop condition's first conjunct is already false, so both types are reported
   whether the reduction works or not, and an assertion there could not fail on a broken reduction.
3. **Generic closing.** A subject that is an open generic definition is closed with `MakeGenericType`
   before it can be read, using one representative argument, `typeof(Paramore.Brighter.Command)` — a
   public concrete class implementing `IRequest`, satisfying both constraint forms the gateways use:
   `where T : IRequest` (AWSSQS, AWSSQS.V4, AzureServiceBus, Kafka, MQTT, MsSql, Redis, RMQ.Async,
   RMQ.Sync) and `where T : class, IRequest` (GcpPubSub, Postgres, RocketMQ).
4. **Read and check.** `GetUninitializedObject` produces an instance, cast to `Subscription`, and
   `ChannelFactoryType` is read through the virtual property. The value and the subject are passed to
   `Check`. No member-level reflection is needed for the read.

**Three reasons belong to `Sweep`, not `Check`**: a generic definition whose arity or constraints the
representative argument does not satisfy; a type for which an uninitialised instance cannot be produced;
and a read that throws. Each **yields a reason, never a silent skip** — a skipped type would vanish from
the reported subject set, which is the vacuous pass this design exists to prevent. A throw becomes a
reason naming the type, the exception type and its message, so one run reports every offending type
instead of stopping at the first; see *Alternatives Considered* for why this does not contradict ADR
0064. A `ReflectionTypeLoadException` from `GetTypes()` propagates: the test project's references are
broken, which is not a declaration defect.

#### The twelve sweep tests are generator-owned

Following ADR 0037's precedent for messaging-gateway tests and ADR 0070's decision that conformance
tests are generator-owned by default. Three new files in `tools/Paramore.Brighter.Test.Generator` — a
template, a configuration class and a generator — plus edits to `TestConfiguration`, `Program.cs` and
`GeneratedTreeAudit`.

**The template** —
`Templates/GatewayConformance/When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs.liquid`
— renders one `[Fact]` in a class named `GatewayChannelFactoryDeclarationTests`. File named for the test
method, class for the behaviour, per `.agent_instructions/testing.md`. It asserts two things and
**contains no reflection logic**: a set comparison against rendered `typeof(...)` literals, and a null
check.

1. `Sweep`'s reported subject set is **exactly** the expected set — one entry in all twelve today.
2. Every `Reason` is `null`.

**What the exact set catches, and what it does not.** Subsumption over-reporting yields an extra subject;
subsumption inverted — base dropped, derived kept — yields the wrong subject, which a non-emptiness check
would miss because the derived type inherits its base's value and reports a `null` reason; discovery
finding nothing yields an empty set. All three fail. A `SubscriptionType` aimed at the **wrong assembly**
is *not* caught here, because one value both locates the sweep and supplies the expectation, so a misaim
moves both together. Three other mechanisms cover that: a cross-gateway misaim usually does not
**compile** (no gateway test project references a second gateway, C-9); a misaim to `Paramore.Brighter`
itself reports `{Subscription}`, matching the configuration, and fails the **reason** check because
`Subscription.ChannelFactoryType` is `typeof(InMemoryChannelFactory)`; and a gateway left unconfigured or
configured twice fails the **thirteenth-gateway audit**. What the exact set adds over all three is the
*wrong subject within the right assembly* — a configuration naming `MqttSubscription<T>` rather than
`MqttSubscription` — which the audit's namespace comparison cannot see.

**AC-27's at-most-once clause is discharged in two places, deliberately.** Over the twelve real
assemblies, by the exact set: exactly-one-subject is strictly stronger than at-most-once. Over the
*mechanism*, by the `Core.Tests` pairs below. The two are complementary rather than redundant: the
**no-override** shape is in all twelve shipped assemblies — it is precisely why each resolves to one
subject — so the generated sweeps test its *outcome* on real input, while the `Core.Tests` pair tests the
step that produces it. The **declares-its-own** shape has no shipped instance at all, so `Core.Tests` is
the only place it can be tested.

**The configuration** — `Configuration/GatewayConformanceConfiguration.cs`, and a `GatewayConformance`
section on `TestConfiguration`:

| Property | Type | Meaning |
|---|---|---|
| `SubscriptionType` | `string`, required | Fully-qualified name of the subscription type expected to be reported. Rendered **both** as `typeof(X).Assembly`, to locate the sweep, and as an expected subject. **It MUST name a type that survives subsumption** — one declaring its own `ChannelFactoryType`, or a root candidate. Naming the generic derived type (`MqttSubscription<T>`) was a valid locator before and is now a red test. |
| `AdditionalExpectedSubjects` | `List<string>`, optional, default empty | Further expected subjects, for an assembly declaring an override on more than one type. Absent in all twelve today. A **generic** entry is written in JSON with its arity backtick, exactly as `Type.FullName` reports it (`Ns.Foo\`1`), and the template renders it by replacing the `` `n `` suffix with angle brackets carrying *n*−1 commas — `typeof(Ns.Foo<>)`, `typeof(Ns.Foo<,>)` for arity 2 — matching `Subject`'s open-definition identity. Writing `Ns.Foo<>` in the JSON is invalid: the value must stay a name `Type.FullName` could have produced, so the audit's namespace comparison keeps working. |
| `Category` | `string`, optional | Unused by the guard; present for symmetry with the other sections. |

The expected set is `SubscriptionType` ∪ `AdditionalExpectedSubjects`. An assembly that grows a second
declaring subscription fails its sweep until that list is updated — which is the point: a new gateway
subscription type is exactly the event this guard exists to notice.

**`Generators/GatewayConformanceGenerator.cs`** mirrors `MessagingGatewayGenerator`'s
`Suites` / `SuitesFor` / `Plan` shape. Per `.agent_instructions/generated_tests.md` the suite is described
in `SuitesFor(...)`, so the generate path and the plan path walk one description; a suite only the
generate path knows about is written and then reported as an orphan by the audit.

**Output** at `MessagingGateway/Generated/Conformance/`, namespace
`{{ Namespace }}.MessagingGateway.Generated.Conformance`. The `Generated` segment is what brings the file
inside the generated-tree audit's scope, and the path cannot collide with a per-variant folder
(`MessagingGateway/Classic/Generated/…`). **One sweep per project, not per gateway variant** — AC-27 asks
for one per assembly, and `Paramore.Brighter.AWS.Tests` has four variants over one assembly.

#### The twelve configurations

**Three gateway test projects need a `test-configuration.json` that does not exist today**:
`Paramore.Brighter.AzureServiceBus.Tests`, `Paramore.Brighter.MQTT.Tests` and
`Paramore.Brighter.RMQ.Sync.Tests`. Theirs carry `Namespace` and a `GatewayConformance` section, nothing
else. `Namespace` is required, not incidental: it is a top-level property defaulting to `string.Empty`
(`Configuration/TestConfiguration.cs:38`), and the template renders
`{{ Namespace }}.MessagingGateway.Generated.Conformance`, so omitting it yields
`namespace .MessagingGateway.Generated.Conformance` — a failure *after* generation rather than at
configuration load. All fourteen existing configurations carry it. The other nine gain the section in the
file they already have.

`AdditionalExpectedSubjects` is absent from all twelve rows below, not omitted from the table:

| Test project | `GatewayConformance.SubscriptionType` |
|---|---|
| `Paramore.Brighter.AWS.Tests` | `Paramore.Brighter.MessagingGateway.AWSSQS.SqsSubscription` |
| `Paramore.Brighter.AWS.V4.Tests` | `Paramore.Brighter.MessagingGateway.AWSSQS.V4.SqsSubscription` |
| `Paramore.Brighter.AzureServiceBus.Tests` | `Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusSubscription` |
| `Paramore.Brighter.Gcp.Tests` | `Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubSubscription` |
| `Paramore.Brighter.Kafka.Tests` | `Paramore.Brighter.MessagingGateway.Kafka.KafkaSubscription` |
| `Paramore.Brighter.MQTT.Tests` | `Paramore.Brighter.MessagingGateway.MQTT.MqttSubscription` |
| `Paramore.Brighter.MSSQL.Tests` | `Paramore.Brighter.MessagingGateway.MsSql.MsSqlSubscription` |
| `Paramore.Brighter.PostgresSQL.Tests` | `Paramore.Brighter.MessagingGateway.Postgres.PostgresSubscription` |
| `Paramore.Brighter.RMQ.Async.Tests` | `Paramore.Brighter.MessagingGateway.RMQ.Async.RmqSubscription` |
| `Paramore.Brighter.RMQ.Sync.Tests` | `Paramore.Brighter.MessagingGateway.RMQ.Sync.RmqSubscription` |
| `Paramore.Brighter.Redis.Tests` | `Paramore.Brighter.MessagingGateway.Redis.RedisSubscription` |
| `Paramore.Brighter.RocketMQ.Tests` | `Paramore.Brighter.MessagingGateway.RocketMQ.RocketSubscription` |

#### CI placement

**The guard runs in the `build` job, not the transport jobs.** The generated test carries no `Category`
and no `Collection` attribute, and the `build` job gains one step running the twelve projects with
`--configuration Release --filter "FullyQualifiedName~GatewayChannelFactoryDeclarationTests" --no-build`.
The configuration is named explicitly because `--no-build` otherwise looks for a Debug build the `build`
job never produced. That job already compiles the whole solution, so this costs seconds and needs no
broker.

Three facts make the placement load-bearing rather than tidiness: `.github/workflows/ci.yml:228` runs
MQTT with `--filter "Category=MQTT&…"` and `:361` runs Kafka with `--filter "Category=Kafka&…"`, so an
untagged test in those projects would never be selected; and the RocketMQ job (`:708-769`) is entirely
commented out, so a sweep living only there would never run at all.

**The step's non-vacuity depends on a different step.** A `--filter` that matches nothing does not fail on
most runners, so a missing generated file would leave this step green while guarding eleven assemblies.
What catches that is the generated-tree audit in the same job, which fails when any of the twelve files
is missing.

#### A thirteenth gateway cannot be forgotten

A new test in `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/` asserts that every
`src/Paramore.Brighter.MessagingGateway.*` project directory is named by exactly one
`GatewayConformance.**SubscriptionType**` across `tests/*/test-configuration.json`, comparing the
directory name with the namespace containing the configured type. All twelve shipped gateway assemblies
use their assembly name as their root namespace, which is what makes the comparison exact. Adding a
gateway to `src/` fails this audit until a configuration names it.

**The audit counts `SubscriptionType` only, deliberately.** If `AdditionalExpectedSubjects` also counted
as naming a directory, the very case that key exists for — a second declaring type in an existing gateway
— would make that directory "named by two" and fail.

#### Hand-written cases in `Core.Tests`

In `tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration/`, beside the existing
`CombinedChannelFactory` and channel tests, one class per file, with doubles under a `TestDoubles/` folder
in that namespace. They are deliberately **not** in `Validation/TestDoubles/`, whose double set C-9
declares closed for the rule's own criteria.

**Three subscription doubles.** C-9's "every `IAmAChannelFactory` member throws" rule does **not** apply
to these: a `Subscription` subclass has no such members. They are identity-only in the sense that each
exists to be a distinct type with one declaration.

- one with **no** `ChannelFactoryType` override, inheriting the default — AC-28's **inherited-default**
  shape;
- one overriding it with a type that is **not** an `IAmAChannelFactory` — AC-28's
  **not-a-channel-factory** shape;
- one whose **constructor cannot succeed** — it calls `base(...)` with `MessagePumpType.Unknown`, so
  `Subscription.cs:213` throws — but which declares a sound factory type. It appears among its own
  assembly's subjects with a `null` `Reason`, and can only do so because no constructor ran. That is
  AC-29.

**Two helper types they declare**, named here because the design depends on both: a non-factory marker
type, implementing nothing, for the second double to declare; and one sound `IAmAChannelFactory` double
for the third to declare. C-9's throw-rule **does** apply to the latter — every `CreateSyncChannel`,
`CreateAsyncChannel` and `CreateAsyncChannelAsync` on it throws.

**Two subsumption pairs**, for the mechanism AC-27's clause describes: a base/derived pair whose derived
type declares **no** override, asserting the derived is subsumed — this is the pair with the **constructed
generic base** (`Derived : Base<Command>`), for the reason given under subsumption above, with `Base<T>`
declaring a sound override — reusing the sound `IAmAChannelFactory` double — so its own entry carries a null
reason and the assertion is about subsumption alone; and one whose
derived type declares **its own**, asserting both are reported. Plus **one generic subscription declaring
its own override**, for the closing path, asserted against the open-definition literal `typeof(X<>)` per
`Subject`'s identity above. The no-override shape itself is universal among the twelve — what no shipped
assembly has is its constructed-generic-base variant, the declares-its-own pair, or a generic type
declaring its own override.

**One double whose getter throws** on an uninitialised instance, asserting its entry carries a non-null
reason naming the exception type. This is what makes the read-fault behaviour a tested property rather
than an argument, and no other double exercises it: `MockSubscription` reads `null` rather than throwing.

**One generic subscription whose constraints the representative argument cannot satisfy** — `where T :
IEvent` is enough, since `Paramore.Brighter.Command` implements `ICommand` — asserted to carry a non-null
reason. This is what makes "never a silent skip" a tested property on the one path where a skip would be
**invisible**, for the reason set out next.

**Reason-path coverage, stated so the gaps are visible.** Six reason paths exist. `Check`'s three branches
are each asserted with literal arguments, the **null** branch included — it is this ADR's addition and
load-bearing three times over, so a `Check` whose null branch returned "sound" must not pass. Of
`Sweep`'s three, the read-throws path is asserted by the throwing double, and the unsatisfiable
arity-or-constraints path by the double above.

**Why that last one needs its own test, when the exact set appears to cover it.** A silent skip is caught
by the exact subject set only for a subject the configuration **already expects** — dropping such a
subject shrinks the reported set and fails the comparison. It is *not* caught for an **unconfigured**
subject, and that is exactly the case this guard exists to notice: a gateway that grows a second declaring
subscription which happens to be generic with constraints `Command` cannot satisfy. Nobody has added it to
`AdditionalExpectedSubjects` — its absence is the event being guarded — so if the sweep skips it silently
the reported set still equals the expectation, every `Reason` is null, and the test is green. No `Core.Tests`
case can reproduce that blind spot — the blind spot belongs to the *generated* sweeps, whose expectation
comes from configuration — so the path is closed the other way: make the sweep's behaviour on it a tested
property, with a synthetic asserted to carry a reason.

The one remaining path — an instance that cannot be produced — is **not** asserted. It is unreachable for
any type this design declares, and it carries the same blind spot just described, so it is a known and
accepted gap rather than one the exact set closes.

**The `Core.Tests` cases sweep the whole assembly and assert subject-scoped.** `Sweep` takes an `Assembly`
and nothing narrower, so each case is a `Sweep(typeof(<double>).Assembly)` over all of
`Paramore.Brighter.Core.Tests`, and each assertion selects the entry it is about —
`result.Single(e => e.Subject == typeof(X))` — except the subsumption pairs, whose assertion is that a
subsumed type has **no** entry, and which therefore read the filtered sequence rather than `Single(...)`. It
must be subject-scoped either way, because that assembly holds `Subscription`
subclasses these cases do not own and which are not sound:

- `MockSubscription` (`MessagingGateway/When_constructing_a_channel_with_combined_factory.cs:85`)
  overrides `ChannelFactoryType` as an **auto-property assigned in its constructor**, so an uninitialised
  instance reads `null` and it is reported under the **null** branch.
- ADR 0072 adds **four** more in `Validation/TestDoubles/` — C-9's set has `DeclaringSubscription`,
  `NonMatchingSubscription`, `NullDeclaringSubscription` and `AlphaBus.AlphaSubscription`.
  `NullDeclaringSubscription` is reported under the **null** branch by design.

**A constraint this design places on 0072's doubles**, not an assumption about them: the other three must
declare `ChannelFactoryType` as an **expression-bodied `typeof(...)`**. C-9 pins them only as
"identity-only … it overrides `ChannelFactoryType` and nothing else", which is exactly what
`MockSubscription` is — and `MockSubscription` reads `null` uninitialised. Written `=> typeof(X)` they are
sound; written `{ get; }` assigned in a constructor they are reported under the null branch.

No type-scoped `Sweep` overload is introduced for any of this: FR-12 authorises one new type, not a member
added to it so a test can avoid a `Single(...)`.

**The reading path's negative case.** AC-28 calls `Check` with both arguments written literally, which is
the Evident Data those assertions are about. That leaves the reading path — discovery, subsumption,
closing, the uninitialised read, and the hand-off into `Check` — exercised only over sound declarations,
since the twelve sweeps assert every `Reason` is `null`. A `Sweep` that read the wrong property, passed
`null` to `Check`, or discarded the read value would leave all of those green. A **sweep over the two
AC-28 doubles**, asserting their entries carry the expected non-null `Reason`, closes that seam and reuses
doubles already declared. AC-29's double is a *positive* case — it shows that a type whose constructor
cannot succeed is nonetheless examined — not a negative exercise of the reading path.

### Technology Choices

- **`GetUninitializedObject`, not `Activator`.** `System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject`
  is the FR-12 normative mechanism and exists from .NET 5. `Paramore.Brighter` targets
  `netstandard2.0;net8.0;net9.0;net10.0`, so the netstandard2.0 build uses
  `System.Runtime.Serialization.FormatterServices.GetUninitializedObject` behind `#if NETSTANDARD2_0`.
  The modern API must be used on `net8.0` and above: `FormatterServices.GetUninitializedObject` is
  obsoleted there as SYSLIB0050, and `src/Directory.Build.props` sets
  `TreatWarningsAsErrors`, so using it would fail the build.
- **A `string?` reason.** Null means sound. This keeps the addition to the shipped package at one
  type, and follows ADR 0072's judgement that a message which never crosses a boundary and protects
  no invariant "is honest" as a string.
- **Liquid via Fluid**, through the existing generator, because that is where generated tests live.
  Per `.agent_instructions/generated_tests.md`, the generated files are **never** edited directly;
  a change to the guard's test is a change to the template followed by `./generate-test.sh`.

### Implementation Approach

Structural changes precede behavioural ones, and each step is independently testable.

1. **`SubscriptionChannelFactoryDeclaration.Check`** in `src/Paramore.Brighter`, with AC-28's two
   synthetic subscriptions and the two helper types they declare, in `Core.Tests`. All **three** branches
   are asserted with literal arguments here, the **null** branch included — it is this ADR's addition
   rather than an AC-28 shape, so it is scheduled explicitly or it is not built. Pure; no reflection
   machinery yet.
2. **`SubscriptionChannelFactoryDeclaration.Sweep`** — candidates, subsumption, closing, the
   uninitialised read — with AC-29's constructor-cannot-succeed double in `Core.Tests`, and the
   sweep-over-the-AC-28-doubles case that gives the reading path its only negative assertion. **Five**
   further synthetics are added here. Four of the shapes have no shipped instance at all; the fifth, the
   *no-override* subsumption shape, is universal among the twelve, but its constructed-generic-base variant
   is not, and the generated sweeps test only its outcome. They are: a generic
   subscription that declares its own override, for the closing path (see Risks); a base/derived pair
   whose derived type declares **no** override, asserting the derived is subsumed; a pair whose derived
   type declares **its own**, asserting both are reported. **The no-override pair** carries a
   **constructed generic base** (`Derived : Base<Command>`), so subsumption's `GetGenericTypeDefinition()`
   reduction is exercised: a broken reduction leaves `Derived` unsubsumed, so the sweep reports an entry for
   it where a correct reduction reports none. The assertion is therefore that **no entry has
   `Subject == typeof(Derived)`**, with `typeof(Base<>)` present. On the declares-its-own pair the reduction
   is not load-bearing, so it could not be covered there. Then: a double whose `ChannelFactoryType` getter
   **throws** on an uninitialised instance; and a generic
   subscription whose constraints `Command` cannot satisfy (`where T : IEvent`). The two pairs are where
   AC-27's at-most-once clause is tested as a mechanism — the generated sweeps test its outcome on real
   assemblies, which contain the no-override shape but never the declares-its-own one. The last two make
   "a reason, never a silent skip" a tested property on the two paths where a skip would otherwise be
   invisible.
3. **Generator additions** — configuration section, `GatewayConformanceGenerator` with its
   `Suites` / `SuitesFor` / `Plan` trio, and the template. `Program.cs` invokes the new generator;
   `GeneratedTreeAudit.ExpectedFilesUnder` adds its `Plan` alongside `OutboxGenerator.Plan` and
   `MessagingGatewayGenerator.Plan`, or the twelve new files are all reported as orphans.
4. **The twelve configurations** — nine edits, three new files — then `./generate-test.sh`, and the
   twelve generated files are committed.

   ⚠️ **This step and step 6 sequence after ADR 0072's FR-7 to FR-11 corrections.** The generated test
   asserts every `Reason` is `null`, and that is false today in **five** of the twelve assemblies:
   GcpPubSub (FR-7) and MQTT (FR-8) declare an `IAmAMessageConsumerFactory`, so their subject reports
   **not-a-channel-factory**; AWSSQS (FR-9), AWSSQS.V4 (FR-10) and Postgres (FR-11) declare no override at
   all, so their non-generic base is a root candidate reporting **inherited-default**. If 0072's
   corrections slip, the generated files may still be committed — the generated-tree audit wants them —
   but the CI step of step 6 must not be enabled, or the `build` job is red on every pull request.

   `SharedGenerator` is left alone: it will render its four helper files into the three new
   conformance-only projects as well. Those files reference only
   `Paramore.Brighter` (which contains the `Observability` namespace — it is not a separate package)
   and xunit, the latter through fully-qualified `Xunit.Assert` calls rather than a `using`, so a
   usings-only check misses it. Both are already present in every gateway test project, so they
   compile; they land in the project root, which is outside the
   generated-tree audit's `Generated/` scope, so no CI job depends on them. Twelve unused files is
   the accepted cost of not making a behavioural change to a shared generator that FR-12 does not
   ask for.
5. **The thirteenth-gateway audit** in `Paramore.Brighter.Test.Generator.Tests`.
6. **CI** — the `build` job step running the twelve sweeps. Not enabled until 0072's FR-7 to FR-11 have
   merged; see step 4.
7. **Documentation** — `.agent_instructions/generated_tests.md` gains the `GatewayConformance`
   section and the new template folder in its architecture listing.

## Consequences

### Positive

- The defect class ADR 0072 corrects cannot silently return. A gateway subscription that inherits
  `InMemoryChannelFactory`, or names a consumer factory, fails a test in the `build` job — before
  any broker is involved, and on every pull request.
- One definition of "sound declaration", in one place, used by twelve tests. A change to what makes a
  declaration sound is a change to `Check` in `Paramore.Brighter` — not twelve edits, and not a template
  edit either, since the template decides nothing. A change to what the twelve tests *assert* is one
  template edit and a regeneration.
- The guard reaches gateway authors outside this repository. C-10 accepts that out-of-repo
  subscription types are exposed to the same defect and that nothing in the repository can reach
  them; a public predicate in the core package is something a community gateway author can point at
  their own assembly. That is real value from the surface this decision spends.
- Nothing is constructed, so no test in the sweep can contact a broker, a database or a network
  (NFR-3, AC-31), and the `ConfigurationException` from `Subscription.cs:213` cannot arise.
- The failure message names the offending type, what it declared, and what to do — the same standard
  NFR-2 sets for the rule's findings.

### Negative

- **A permanent widening of the shipped core package's surface, whose only in-repo consumer is a
  test guard.** `SubscriptionChannelFactoryDeclaration` is part of `Paramore.Brighter`'s API and
  carries the versioning commitment that implies. Context states the position plainly: a path through
  existing exports does exist, so this is an **authorised testing concession** — FR-12 permits it on
  reachability grounds — and not a gap in the contract the design merely noticed. A generator-emitted
  predicate is narrower on public surface and is rejected under Alternatives Considered on where the
  rule lives, not on width. Narrowed as far as it can be: one static type, two methods, no new result
  type.
- **§ *Narrow and deep*'s after-the-fact check already answers "no" inside this repository**, and
  that is stated rather than deferred: nothing but the guard calls `Check` or `Sweep` here. The open
  question is only whether an out-of-repo gateway author ever does — C-10 is the reason to expect it
  — and if none ever does, the rule prescribes revisiting the widening. Two in-repo consumers were
  considered and not taken: `CombinedChannelFactory`, which makes the same judgement inline at three
  call sites but needs a factory *instance* rather than a declaration verdict, and ADR 0072's startup
  rule, addressed below.
- **The inherited-default judgement is expressed twice in the same package, and that is accepted.**
  `Check`'s **inherited-default** branch and ADR 0072's T3a/T3b case both turn on
  `D == typeof(InMemoryChannelFactory)` (0072:457 — "When `D` is `InMemoryChannelFactory` the
  subscription is a plain `Subscription`/`Subscription<T>`"), so the same rule ships in two places —
  at a much smaller scale, the drift hazard FR-12 invokes against twelve copies. **0072's rule is not
  expected to consume `Check`.** 0072 is Accepted; its rule is an `ISpecification<Subscription>`
  evaluating a subscription against a configured factory, with message-template and severity
  obligations `Check` has neither of, and it answers a different question — *can this subscription be
  routed by the factory it will be handed?* rather than *is this declaration sound at all?* Coupling
  them would re-open settled work to remove a two-site duplication of one `typeof` comparison. The
  cost is that a change to what "inherited default" means must be made in both.
- **Twelve generated files and twelve configuration entries** are added to the repository, and the
  generated-tree audit will then require them to stay in step. A flag or path change that is not
  regenerated becomes a CI failure in the `build` job.
- **A CI project list to maintain.** The `build` job step names twelve projects. A thirteenth
  gateway must be added to it by hand; the configuration audit catches a missing configuration, not
  a missing CI entry.
- **Twelve unused generated helper files.** `SharedGenerator` renders its four helpers into each of
  the three new conformance-only projects. They compile and nothing depends on them, and leaving
  them is deliberate: making `SharedGenerator` conditional would be a behavioural change to a shared
  generator that FR-12 does not ask for, to remove clutter no CI job fails on.
- **The netstandard2.0 branch of the read is compiled but never executed here.** Test projects
  target `net9.0;net10.0` (`tests/Directory.Build.props`), so they bind the modern asset. Only a
  netstandard2.0 consumer would run the `FormatterServices` path.
- **A pre-existing CI gap is made visible rather than fixed.** The RocketMQ job is commented out.
  This design routes around it by running the sweep in the `build` job, but every *other* RocketMQ
  test remains unrun, and this ADR does not change that.

### Risks and Mitigations

- **A `ChannelFactoryType` getter that reads instance state would throw on an uninitialised
  instance**, producing a reason that looks like a declaration defect. No *shipped gateway* override
  does: all nine today, and all twelve once 0072 adds its three, are constant `typeof(...)`
  expressions on non-generic base classes. `Core.Tests` is where the exceptions live, deliberately:
  `MockSubscription`'s auto-property reads `null` rather than throwing, and this design adds one double
  whose getter **does** throw — which is why the cases there assert subject-scoped, and why the
  throw-to-reason behaviour is a tested property rather than a promise. *Mitigation*: the
  reason names the type and the exception, so it is actionable rather than mysterious; and a
  `ChannelFactoryType` computed from constructor state would itself be a defect, since
  `CombinedChannelFactory` treats the value as a fixed identity.
- **The generic-closing path has no shipped type to exercise it.** After subsumption, all twelve
  assemblies resolve to a non-generic base. *Mitigation*: step 2 adds a synthetic generic
  subscription in `Core.Tests` that declares its own override, so the path is covered by a test
  rather than by inspection.
- **The thirteenth-gateway audit relies on a naming convention** — that a gateway assembly's root
  namespace equals its project directory name. *Mitigation*: it holds for all twelve today, and a
  future gateway that breaks it fails the audit loudly; the fix is to name the assembly explicitly
  in that configuration.
- **Subsumption could hide a derived type that genuinely differs.** It drops a derived candidate
  only when that type declares no `ChannelFactoryType` of its own, so its value is its base's value
  by construction. A derived type that *does* declare an override is never dropped.
- **A hand-edited generated file is lost on the next regeneration.**
  `.agent_instructions/generated_tests.md` states the rule; the generated-tree audit detects missing
  and orphaned files but, as it documents, not stale contents. *Mitigation*: every judgement lives in
  `Paramore.Brighter`, none in the template. The template arranges the call, compares the reported
  subject set against rendered `typeof(...)` literals, and checks the reasons are null — no reflection,
  no base-chain walk, no rule about what makes a declaration sound. That is deliberate: an earlier
  draft put the subsumption rule in the template, which would have expressed one rule in two places and
  made a regeneration something to review rather than to trust.

## Alternatives Considered

- **`Activator.CreateInstance(Type)`.** Rejected because it cannot work: not one of the
  twenty-four shipped gateway subscription types declares a parameterless constructor, and a
  constructor with all-optional parameters does not provide one, so every call throws
  `MissingMethodException`. Even if one existed, four transports default `messagePumpType` to
  `MessagePumpType.Unknown`, which `Subscription.cs:213` rejects.
- **Construct with fabricated arguments** — pick a constructor and pass plausible values. Rejected:
  it needs per-transport knowledge of what is valid, it breaks on any constructor signature change,
  and constructors that validate would fail the guard for reasons unrelated to the declaration. It
  also re-opens the door to construction side effects that NFR-3 closes.
- **Widen a shared test-support assembly to all twelve** — for instance promoting
  `Paramore.Test.Helpers` or `Paramore.Brighter.Base.Test`. Rejected on three grounds. It still
  creates a new public surface, just in a test assembly, so it does not actually honour the
  narrow-surface rule any better. It requires eleven new project references, eleven places for a
  thirteenth gateway to be forgotten, against one configuration line. And it puts the rule outside the
  module that depends on it — the same objection as the generator-emitted predicate, with the same
  corollary that C-10's out-of-repo authors cannot reach a test assembly either.
- **`InternalsVisibleTo` on `Paramore.Brighter`.** Rejected; the reasoning is in Context and is not
  repeated here. In short: it is forbidden categorically, and the reason is that it makes `internal`
  a lie — the member becomes public to a narrower audience while the keyword still promises it can
  be refactored freely. It would in any case need twelve public-key-qualified entries, the assembly
  being strong-named.
- **A generator-emitted predicate** — one Liquid template rendering the predicate itself into each
  of the twelve test projects, exactly as `SharedGenerator` already renders four helper files into
  every configured project root. This is strictly narrower than the chosen design on public surface:
  it reaches all twelve, it cannot drift (one template), and it adds **nothing** to the shipped
  package. **On surface width alone it wins**, and Context says so. It is rejected on **where the rule
  lives**, which width does not capture: it leaves a rule `CombinedChannelFactory` already depends on
  at runtime sitting outside the module, in twelve generated test files, where no consumer of the
  package can see it or rely on it. A guard whose logic ships in the package is one the package can be
  held to; one that exists only in this repository's test tree is a private convention. And it cannot
  reach C-10's out-of-repo gateway authors, who face the identical defect — a benefit that separates
  the two designs, though not one the requirements demand of either.
- **A failures-only `Sweep` contract** — return just the reasons, with an empty result meaning sound.
  Rejected, and this was decided in review rather than at first draft. An empty result cannot
  distinguish a sound assembly from one never looked at: it would pass identically over a sound gateway
  and over zero candidates, so a refactor that moved the subscription types out of the swept assembly
  would leave the guard green while guarding nothing. It also makes AC-29 unassertable, since AC-29 is a
  claim about a type having been *examined*. Reporting every subject costs no new public type — a
  `ValueTuple` carries the pair — and is what the exact-set assertion is built on.
- **Two weaker forms of the at-most-once assertion**, both tried and both withdrawn. Recorded because the
  reasoning is easy to lose and the weaker forms look reasonable. Asserting the `Subject` values are
  **distinct** cannot fail at all: candidates come from `Assembly.GetTypes()` and are therefore distinct
  types, and subsumption only removes — so distinctness holds by construction, and the base/derived pair
  it appeared to catch is two distinct types. Asserting **no `Subject` has another in its base chain** is worse — it
  fails a *correct* result, because subsumption drops a derived candidate only when that candidate
  declares no override of its own, so a sound result may legitimately hold both a base and a derived type.
  Fixing the second would also have put a `DeclaredOnly` lookup and a base-chain walk into the generated
  test, expressing the subsumption rule in two places. The exact-subject-set assertion needs no reflection
  and fails in all the right places.
- **Letting a read fault terminate the sweep**, rather than converting it to a reason. Rejected, and it
  does not contradict ADR 0064's "rules must not catch" once that rule's reason is stated correctly. 0064
  forbids a *rule* catching because the `Specification<T>` framework **already** wraps rule evaluation in
  a `try`/`catch` and turns any rule-body exception into a `ValidationSeverity.Error` finding (ADR
  0064:165) — a rule that caught would duplicate a service it is given. A static sweep has no such
  surrounding framework, so catching per type is how it obtains the **equivalent** behaviour: one fault
  becomes one reported reason instead of terminating the run, and one run reports every offending type.
  Nothing is swallowed; the sweep still fails, with more information. The throwing double in `Core.Tests`
  makes that a tested property rather than an assertion.
- **Hand-write the sweep in each of the twelve projects.** Rejected for the reason FR-12 gives:
  twelve copies drift, and the thirteenth gateway then depends on someone remembering. ADR 0070
  already settled that conformance tests are generator-owned by default.
- **A single sweep in one project.** Rejected by AC-27, and impossible: no test project references
  more than one gateway, so eleven assemblies would be unguarded.
- **A `ChannelFactoryDeclarationResult` record instead of a `string?`.** Rejected: it is a second
  public type in the shipped package, and it would carry a boolean plus a message with no behaviour
  and no invariant. FR-12 permits one new public type; spending the allowance on a result carrier
  buys nothing the null-means-sound convention does not.
- **A Roslyn analyzer** in `src/Paramore.Brighter.Analyzer`, flagging a `Subscription` subclass with
  no override at compile time. Not rejected as wrong — it is an attractive complement, and it would
  reach out-of-repo gateways at the point of authorship. It is not this decision: FR-12 specifies a
  reflection sweep, an analyzer cannot inspect an already-shipped binary, and a new diagnostic with
  its own identifier and severity configuration needs its own specification.

## References

- Requirements: [specs/0037-validate-subscription-channel-factory/requirements.md](../../specs/0037-validate-subscription-channel-factory/requirements.md) — FR-12, AC-27, AC-28, AC-29; NFR-3, NFR-5; C-9's twelve-project table and closed double set; C-10's out-of-repo exposure.
- Related ADRs (this ADR **supersedes none** of them; all remain in force):
  - [ADR 0072 — Validate Subscription and Channel Factory Compatibility at Startup](0072-subscription-channel-factory-compatibility.md) (Accepted) — the sibling for the same specification. It owns the startup rule, `CombinedChannelFactory.FactoryTypes`, and the five gateway corrections, and defers FR-12 here. Not superseded.
  - [ADR 0053 — Pipeline Validation and Diagnostic Report at Startup](0053-pipeline-validation-at-startup.md) (Accepted) — the framework the sibling rule extends: `IAmAPipelineValidator`, `ISpecification<T>`, `ValidationError`. Context only; this guard adds no rule. Not superseded.
  - [ADR 0064 — Validate Pipeline Assembly Scanning and Validation-Provider Registration](0064-validate-pipeline-assembly-and-provider-registration.md) (Accepted) — source of "rules must not catch", from which this ADR's sweep deliberately differs, for stated reasons. Its placement rule (C-8) concerns which rule class a rule is declared in, and does not apply here: this guard adds no rule. Not superseded.
  - [ADR 0037 — Add Messaging Gateway Generated Tests](0037-add-messaging-gateway-generated-test.md) (Accepted) — established Liquid-template gateway tests and `MessagingGatewayConfiguration`; the pattern `GatewayConformanceGenerator` follows. Not superseded.
  - ADR 0070 — Generator-Owned Rejection and Delay Conformance Tests — removed from `master` after this ADR was accepted (merged by accident alongside an unrelated PR, then reverted in `47d2e8a89`); it argued that conformance tests are generator-owned by default for every gateway configuration, a precedent this guard follows independently.
  - [ADR 0035 — Test Generation Tool](0035-generated-test.md) (Accepted) — the generator's original rationale. Not superseded.
- Project rules: [.agent_instructions/testing.md](../../.agent_instructions/testing.md) — *Narrow and deep — and when widening the surface is legitimate* (cited for the obligation it creates — widen honestly, record it, apply the after-the-fact check — not for permission, which FR-12 gives), *No InternalsVisibleTo*, *Test Scope and Isolation*, test and file naming, one class per file; [.agent_instructions/generated_tests.md](../../.agent_instructions/generated_tests.md) — never edit generated files, `SuitesFor(...)` and the generated-tree audit; [.agent_instructions/design_principles.md](../../.agent_instructions/design_principles.md) — Responsibility-Driven Design; and "do not add new types without necessity", which this decision does **not** satisfy on its own terms and which FR-12's express permission overrides (see Context).
- External references: GitHub issue [#4334](https://github.com/BrighterCommand/Brighter/issues/4334), prompted by [#4331](https://github.com/BrighterCommand/Brighter/issues/4331).
- Grounded code references (verified against the working tree):
  - `src/Paramore.Brighter/Subscription.cs:172` — `public virtual Type ChannelFactoryType => typeof(InMemoryChannelFactory)`; `:213` — the `MessagePumpType.Unknown` `ConfigurationException`; `Subscription` is non-abstract (`:35`) and `Subscription<T>` is at `:258`.
  - `src/Paramore.Brighter/CombinedChannelFactory.cs:34`, `:46`, `:59` — `_factories.FirstOrDefault(f => f.GetType() == subscription.ChannelFactoryType)` in each creation method — the same comparison the guard makes, implemented inline, with the `ConfigurationException` on no match. This is the runtime dependency that makes *where the rule lives* the question this ADR decides.
  - `tests/Paramore.Brighter.Core.Tests/MessagingGateway/When_constructing_a_channel_with_combined_factory.cs:85-87` — `MockSubscription`, whose `ChannelFactoryType` is an auto-property and so reads `null` on an uninitialised instance; the only `Subscription` subclass in `Core.Tests` today.
  - `src/Paramore.Brighter/IAmAChannelFactory.cs`, `src/Paramore.Brighter/InMemoryChannelFactory.cs` — both in namespace `Paramore.Brighter`; `src/Paramore.Brighter/Command.cs:42` — `public class Command : ICommand`, the representative generic argument.
  - The twenty-four shipped gateway subscription types — twelve base/derived pairs, one per assembly, none of them abstract, with `where T : class, IRequest` on `GcpPubSubSubscription<T>` (`GcpPubSubSubscription.cs:158-159`), `PostgresSubscription<T>` (`PostgresSubscription.cs:118-119`) and `RocketMqSubscription<T>` (`RocketMqSubscription.cs:117-118`), and `where T : IRequest` on the other nine.
  - The nine `override Type ChannelFactoryType` declarations, all on non-generic base classes: RocketMQ `:50`, GcpPubSub `:108`, Redis `:32`, AzureServiceBus `:39`, RMQ.Sync `:67`, RMQ.Async `:73`, Kafka `:162`, MsSql `:32`, MQTT `:35`.
  - `tools/Paramore.Brighter.Test.Generator/` — `Program.cs`, `Generators/{BaseGenerator,SharedGenerator,OutboxGenerator,MessagingGatewayGenerator,GenerationSuite,PlannedFile}.cs`, `Configuration/TestConfiguration.cs`, and `Templates/{MessagingGateway/{Reactor,Proactor},Outbox/{Sync,Async,Causation}}`.
  - `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/GeneratedTreeAudit.cs` — the expected set built from `OutboxGenerator.Plan` and `MessagingGatewayGenerator.Plan`, and its scope note that only the `Generated/` tree is audited.
  - `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Generated/{Reactor,Proactor}` and `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/{Classic,Quorum}/Generated/…` — the two existing output shapes the conformance path must not collide with.
  - `.github/workflows/ci.yml` — the `build` job's `dotnet build --configuration Release` and generator-audit step; `:228` MQTT's `Category=MQTT` filter; `:361` Kafka's `Category=Kafka` filter; `:708-769` the commented-out RocketMQ job, declared at `:708`.
  - `src/Directory.Build.props` — `BrighterTargetFrameworks` (`netstandard2.0;net8.0;net9.0;net10.0`) and `TreatWarningsAsErrors`; `tests/Directory.Build.props` — `BrighterTestTargetFrameworks` (`net9.0;net10.0`).
