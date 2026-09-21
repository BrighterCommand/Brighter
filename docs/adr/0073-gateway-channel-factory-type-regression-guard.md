---
id: 0073-gateway-channel-factory-type-regression-guard
title: "Gateway ChannelFactoryType Regression Guard"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-09-21
summary: "Implements FR-12's regression guard as one new public static type in Paramore.Brighter, SubscriptionChannelFactoryDeclaration, exposing a pure (subscriptionType, declaredFactoryType) predicate that returns the reason a declaration is unsound (null when sound) and an assembly sweep that reads ChannelFactoryType from uninitialised instances via GetUninitializedObject, so no constructor runs, de-duplicating base/derived pairs by subsumption; the twelve per-gateway sweep tests are generated from a single new Liquid template through a new GatewayConformance configuration section and run in CI's infrastructure-free build job."
tags:
  - "testing"
  - "test-generation"
  - "transports"
  - "configuration"
---

# 73. Gateway ChannelFactoryType Regression Guard

Date: 2026-09-21

## Status

Proposed

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

**Two project rules pull against each other here.**
`.agent_instructions/testing.md` *Test Scope and Isolation* carries three bullets, and the third is
the one that bites:

> - Do not expose more than is necessary from an assembly
>   - An assembly is a module, it's surface area should be as narrow as possible.
>   - Do not make export classes or methods from a module to test them; we only test exports from
>     modules, not implementation details.

**This decision contravenes that third bullet, and the ADR says so plainly rather than calling it a
tension.** Publishing a predicate from `Paramore.Brighter` so that twelve test projects can call it
is exactly "export … methods from a module to test them". FR-12 grants the exception expressly —
**one new public type** notwithstanding NFR-5 — after reasoning that only the core assembly is
referenced by all twelve. An exception granted by an approved requirement is the right way to break
a project rule; doing it silently is not.

The alternative that would have kept the guard invisible is closed by a second rule. *No
InternalsVisibleTo* is categorical: "**NEVER** use `InternalsVisibleTo` to expose internal classes
for testing." So the choice is between a declared public widening and twelve copies of the guard.
(That section's own prescription — "make the interface **public** so it can be injected through the
public API" — is about injecting a dependency for testing and is **not** a licence for this; it is
cited here only for the prohibition, not as endorsement.)

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
  │        -> IReadOnlyList<string>  the reasons, ordered        │
  │                                                              │
  │   candidates -> subsume -> close generics -> read -> Check   │
  │                                     │                        │
  │                            GetUninitializedObject            │  no constructor runs
  └──────────────────────────────────────────────────────────────┘
        ▲                                        ▲
        │ Sweep                                  │ Check
        │                                        │
  ┌─────┴──────────────────────────┐    ┌────────┴─────────────────────────┐
  │ 12 GENERATED sweep tests       │    │ Core.Tests, hand-written         │
  │ one per gateway test project   │    │  - AC-28 synthetic negatives     │
  │ from ONE Liquid template       │    │  - AC-29 constructs-nothing      │
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

**`SubscriptionChannelFactoryDeclaration`** — a new `public static class` in namespace
`Paramore.Brighter`, in `src/Paramore.Brighter`. In Responsibility-Driven Design terms it is a
**service provider**: it answers a question on request and holds no state. It has exactly two public
members. It is the one new public type FR-12 permits; the trade-off it carries is recorded under
Negative, and the options it was chosen over are in Alternatives Considered.

**`public static string? Check(Type subscriptionType, Type? declaredFactoryType)`** — the pure
predicate FR-12 requires. It returns `null` when the declaration is sound, and otherwise the single
reason it is not. It evaluates three named branches in order and returns the first that fails. They
are **named, not numbered**, because FR-12 numbers its own two conditions differently and the two
schemes would otherwise be read for each other:

- **null** — `declaredFactoryType is null` → *"… declares no channel factory type
  (`ChannelFactoryType` returned null)."*
- **not-a-channel-factory** (FR-12's condition 1) —
  `!typeof(IAmAChannelFactory).IsAssignableFrom(declaredFactoryType)` → *"… declares `{full name}`,
  which does not implement `Paramore.Brighter.IAmAChannelFactory`."*
- **inherited-default** (FR-12's condition 2) — `declaredFactoryType == typeof(InMemoryChannelFactory)` → *"… declares
   `Paramore.Brighter.InMemoryChannelFactory`. A shipped gateway subscription must declare its own
  transport's channel factory; a type that does not override `ChannelFactoryType` inherits this
  default."*

`subscriptionType` is used only to name the subject; a null argument throws
`ArgumentNullException`. Every type in a reason is rendered with `Type.FullName`, because eight
transports name their channel factory class `ChannelFactory` and a message built from `Type.Name`
would read "expected `ChannelFactory`, got `ChannelFactory`" — the same reasoning FR-5 applies to
the rule's messages. The reason is a `string?` rather than a result record; see Alternatives
Considered.

**`public static IReadOnlyList<(Type Subject, string? Reason)> Sweep(Assembly gatewayAssembly)`** —
the sweep. It returns **one entry per candidate it examined**, carrying the subject type and its
`Reason` — `null` when that declaration is sound. It is empty only when the assembly contains no
candidates at all. Entries are ordered by `Subject.FullName` using ordinal comparison;
`Assembly.GetTypes()` order is not specified, so the sort is what makes repeated runs
byte-identical.

**It reports what it examined, not only what failed, and that is deliberate.** A sweep returning
failures alone cannot distinguish a sound assembly from one it never looked at: an empty result
would pass identically over twelve sound subscriptions and over zero candidates, so a refactor that
moved the subscription types or a `SubscriptionType` pointed at the wrong assembly would leave the
guard green while guarding nothing. That is the vacuous pass this design exists to prevent, and a
contract that cannot express "I examined these and they were sound" cannot rule it out. It is also
what makes AC-29 assertable — see the synthetic types below. A `ValueTuple` carries the pair, so
this costs no new public type. Four steps:

- **Candidates.** Non-abstract classes whose base chain reaches `Paramore.Brighter.Subscription`.
  The base chain is walked explicitly rather than tested with `IsAssignableFrom`, because
  `IsAssignableFrom` behaves surprisingly for open generic type definitions and each gateway
  assembly contains one.
- **Subsumption.** A candidate is dropped when it does not itself declare `ChannelFactoryType`
  (looked up with `BindingFlags.DeclaredOnly`) and an ancestor in its base chain is also a candidate
  in the same assembly. **Ancestry is matched on the generic type *definition*.** Subsumption runs
  before closing, so the candidate set holds open definitions (`Foo<>`) while a base chain yields
  closed constructions (`Foo<Bar>`); an ancestor that is a constructed generic is therefore reduced
  with `GetGenericTypeDefinition()` before the comparison. Without that reduction a
  `FooBar : Foo<Bar>` declaring no override would never match its own base and would be reported
  twice — or, read the other way, silently dropped. No shipped assembly has that shape today, so
  step 2's synthetic types cover it. This is FR-12's "a base/derived pair … MUST be reported at most once": a
  derived type that adds no override cannot disagree with its base. In every shipped assembly this
  reduces the pair to its non-generic base — `RocketMqSubscription<T>` to `RocketSubscription`
  (`src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMqSubscription.cs:117` and `:10`),
  `SqsSubscription<T>` to `SqsSubscription`, and so on for all twelve.
- **Generic closing.** A surviving candidate that is an open generic definition is closed with
  `MakeGenericType` before it can be read. One representative argument is used,
  `typeof(Paramore.Brighter.Command)`, a public concrete class implementing `IRequest`, which
  satisfies both constraint forms the gateways use: `where T : IRequest` (AWSSQS, AWSSQS.V4,
  AzureServiceBus, Kafka, MQTT, MsSql, Redis, RMQ.Async, RMQ.Sync) and `where T : class, IRequest`
  (GcpPubSub, Postgres, RocketMQ). A definition of any other arity, or one whose constraints the
  representative does not satisfy, yields a reason — never a silent skip, because a silently skipped
  type is a vacuous pass, which is the very failure mode condition 2 exists to prevent.
- **Read and check.** `GetUninitializedObject` produces an instance, which is cast to `Subscription`
  and its `ChannelFactoryType` read through the virtual property. The value and the type are passed
  to `Check`. No member-level reflection is needed for the read.

Two further reasons belong to `Sweep` rather than `Check`: a type for which an uninitialised
instance cannot be produced, and a read that throws. A throw is converted into a reason naming the
type, the exception type and its message, so that one run reports every offending type rather than
stopping at the first. This does not contradict ADR 0064's "rules must not catch", once that rule's
reason is stated correctly. 0064 forbids a rule catching because the `Specification<T>` framework
**already** wraps rule evaluation in a `try`/`catch` and converts any rule-body exception into a
`ValidationSeverity.Error` finding (ADR 0064:165) — a rule that caught would be duplicating a
service it is already given. A static sweep has no such surrounding framework, so catching per type
is how it obtains the **equivalent** behaviour: one fault becomes one reported reason instead of
terminating the run. Nothing is swallowed — the sweep still fails, with more information. A `ReflectionTypeLoadException` from `GetTypes()` is allowed to propagate: it means the
test project's references are broken, not that a declaration is wrong.

**The twelve sweep tests are generator-owned**, following ADR 0037's precedent for
messaging-gateway tests and ADR 0070's decision that conformance tests are generator-owned by
default. Three new files in `tools/Paramore.Brighter.Test.Generator` — a template, a configuration
class and a generator — plus edits to `TestConfiguration`, `Program.cs` and `GeneratedTreeAudit`:

- `Templates/GatewayConformance/When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs.liquid`
  — one template, rendering one `[Fact]` in a class named `GatewayChannelFactoryDeclarationTests`.
  The test asserts **both** halves of `Sweep`'s contract: that the result is non-empty, so the sweep
  is known to have examined something, and that every `Reason` is `null`. Asserting only the second
  would be the vacuous pass described above.
  The naming follows `.agent_instructions/testing.md`: the file is named for the test method, the
  class for the behaviour.
- `Configuration/GatewayConformanceConfiguration.cs` and a `GatewayConformance` section on
  `TestConfiguration`, with `SubscriptionType` (the fully-qualified name of a subscription type in
  the gateway assembly, which the template renders as `typeof(X).Assembly`) and an optional
  `Category`.
- `Generators/GatewayConformanceGenerator.cs`, mirroring `MessagingGatewayGenerator`'s
  `Suites` / `SuitesFor` / `Plan` shape. Per `.agent_instructions/generated_tests.md`, the suite is
  described in `SuitesFor(...)` so that the generate path and the plan path walk one description; a
  suite only the generate path knows about is written and then reported as an orphan by the audit.
- Output at `MessagingGateway/Generated/Conformance/`, namespace
  `{{ Namespace }}.MessagingGateway.Generated.Conformance`. The path carries a `Generated` segment,
  which is what brings the file inside the generated-tree audit's scope, and it cannot collide with
  a per-variant folder (`MessagingGateway/Classic/Generated/…`).

One sweep is emitted per **project**, not per gateway variant — AC-27 asks for one per assembly, and
`Paramore.Brighter.AWS.Tests` has four variants over one assembly.

**Three gateway test projects need a `test-configuration.json` that does not exist today**:
`Paramore.Brighter.AzureServiceBus.Tests`, `Paramore.Brighter.MQTT.Tests` and
`Paramore.Brighter.RMQ.Sync.Tests`. Theirs carry `Namespace` and a `GatewayConformance` section, and
nothing else. `Namespace` is required, not incidental: it is a top-level property defaulting to
`string.Empty` (`Configuration/TestConfiguration.cs:38`), and the template renders
`{{ Namespace }}.MessagingGateway.Generated.Conformance`, so omitting it yields
`namespace .MessagingGateway.Generated.Conformance`, which fails to compile *after* generation
rather than at configuration load. All fourteen existing configurations carry it.
The other nine gain the section in the file they already have. The twelve values:

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

**The guard runs in CI's `build` job, not in the transport jobs.** The generated test carries no
`Category` and no `Collection` attribute, and the `build` job gains one step running the twelve
projects with `--filter "FullyQualifiedName~GatewayChannelFactoryDeclarationTests" --no-build`. The
`build` job already compiles the whole solution, so this costs seconds and needs no broker. It is
placed there for the reason the generator audit is placed there — a guard that needs no
infrastructure should not be gated on infrastructure. Three concrete facts make this load-bearing
rather than tidiness: `.github/workflows/ci.yml:228` runs the MQTT project with
`--filter "Category=MQTT&…"` and `:361` runs Kafka with `--filter "Category=Kafka&…"`, so an
untagged test in those projects would never be selected; and the RocketMQ job (`:769`) is entirely
commented out, so a sweep living only there would never run at all.

**A thirteenth gateway cannot be forgotten.** A new test in
`tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/` asserts that every
`src/Paramore.Brighter.MessagingGateway.*` project directory is named by exactly one
`GatewayConformance.SubscriptionType` across `tests/*/test-configuration.json`, comparing the
directory name with the namespace containing the configured type. All twelve shipped gateway
assemblies use their assembly name as their root namespace, which is what makes the comparison
exact. Adding a gateway to `src/` then fails this audit until a configuration names it.

**AC-28's and AC-29's synthetic types are hand-written, in
`tests/Paramore.Brighter.Core.Tests/MessagingGateway/ChannelFactoryDeclaration/`** — beside the
existing `CombinedChannelFactory` and channel tests, one class per file, with the doubles under a
`TestDoubles/` folder in that namespace. They are deliberately **not** placed in
`Validation/TestDoubles/`, whose double set C-9 declares closed for the rule's own criteria. Three
doubles, all identity-only in C-9's sense — every `IAmAChannelFactory` member throws:

- a subscription with no `ChannelFactoryType` override, so it inherits the default — AC-28's
  **inherited-default** shape (FR-12's condition 2);
- a subscription overriding it with a type that is not an `IAmAChannelFactory` — AC-28's
  **not-a-channel-factory** shape (FR-12's condition 1);
- a subscription whose constructor cannot succeed — it calls `base(...)` with
  `MessagePumpType.Unknown`, so `Subscription.cs:213` throws — but which declares a sound factory
  type. It appears among `Sweep(itsOwnAssembly)`'s subjects with a `null` `Reason`, and can only do
  so because no constructor ran — a constructed instance would have thrown. That is AC-29, and it is
  assertable only because `Sweep` reports what it examined.

AC-28 calls `Check` with both arguments written literally, which is the Evident Data the assertion
is about and is exactly the `(subscriptionType, declaredFactoryType)` shape FR-12 specifies. The
reading path it does not exercise is covered positively by the twelve sweeps and negatively by
AC-29.

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
   synthetic subscriptions and their doubles in `Core.Tests`. Pure; no reflection machinery yet.
2. **`SubscriptionChannelFactoryDeclaration.Sweep`** — candidates, subsumption, closing, the
   uninitialised read — with AC-29's constructor-cannot-succeed double in `Core.Tests`. A synthetic
   generic subscription that declares its own override is added here, because no shipped type
   exercises the closing path (see Risks).
3. **Generator additions** — configuration section, `GatewayConformanceGenerator` with its
   `Suites` / `SuitesFor` / `Plan` trio, and the template. `Program.cs` invokes the new generator;
   `GeneratedTreeAudit.ExpectedFilesUnder` adds its `Plan` alongside `OutboxGenerator.Plan` and
   `MessagingGatewayGenerator.Plan`, or the twelve new files are all reported as orphans.
4. **The twelve configurations** — nine edits, three new files — then `./generate-test.sh`, and the
   twelve generated files are committed. `SharedGenerator` is left alone: it will render its four
   helper files into the three new conformance-only projects as well. Those files reference only
   `Paramore.Brighter` and `Paramore.Brighter.Observability`, both already available in every
   gateway test project, so they compile; they land in the project root, which is outside the
   generated-tree audit's `Generated/` scope, so no CI job depends on them. Twelve unused files is
   the accepted cost of not making a behavioural change to a shared generator that FR-12 does not
   ask for.
5. **The thirteenth-gateway audit** in `Paramore.Brighter.Test.Generator.Tests`.
6. **CI** — the `build` job step running the twelve sweeps.
7. **Documentation** — `.agent_instructions/generated_tests.md` gains the `GatewayConformance`
   section and the new template folder in its architecture listing.

## Consequences

### Positive

- The defect class ADR 0072 corrects cannot silently return. A gateway subscription that inherits
  `InMemoryChannelFactory`, or names a consumer factory, fails a test in the `build` job — before
  any broker is involved, and on every pull request.
- One definition of "sound declaration", in one place, used by twelve tests. A change to the guard
  is one template edit and a regeneration, not twelve edits.
- The guard reaches gateway authors outside this repository. C-10 accepts that out-of-repo
  subscription types are exposed to the same defect and that nothing in the repository can reach
  them; a public predicate in the core package is something a community gateway author can point at
  their own assembly. That is real value from the surface this decision spends.
- Nothing is constructed, so no test in the sweep can contact a broker, a database or a network
  (NFR-3, AC-31), and the `ConfigurationException` from `Subscription.cs:213` cannot arise.
- The failure message names the offending type, what it declared, and what to do — the same standard
  NFR-2 sets for the rule's findings.

### Negative

- **A permanently public type in the shipped core package whose only in-repo consumer is a test
  guard.** `SubscriptionChannelFactoryDeclaration` is part of `Paramore.Brighter`'s API surface and
  carries the versioning commitment that implies. This sits in tension with
  `.agent_instructions/testing.md`'s *Test Scope and Isolation* rule that an assembly's "surface
  area should be as narrow as possible", and it **contravenes** that section's third bullet — "Do
  not make export classes or methods from a module to test them" — under the exception FR-12 grants
  expressly. `InternalsVisibleTo`, which would have avoided the exposure, is forbidden outright by
  the same document. It is the narrowest option that reaches all twelve test projects *and* the
  out-of-repo gateway authors of C-10; a generator-emitted predicate would be narrower still on
  public surface alone, and is rejected under Alternatives Considered for the C-10 reason. Narrowed
  as far as it can be: one static type, two methods, no new result type.
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
  instance**, producing a reason that looks like a declaration defect. Today none does — all nine
  overrides are constant `typeof(...)` expressions on non-generic base classes. *Mitigation*: the
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
  and orphaned files but, as it documents, not stale contents. *Mitigation*: the guard's logic lives
  in `Paramore.Brighter`, not in the template, so the template is three lines of arrangement and
  there is very little in it to be tempted to edit.

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
  thirteenth gateway to be forgotten, against one configuration line. And a test assembly cannot
  serve the out-of-repo gateway authors of C-10, who face the identical defect.
- **`InternalsVisibleTo` on `Paramore.Brighter`.** Rejected because
  `.agent_instructions/testing.md` forbids it categorically — "**NEVER** use `InternalsVisibleTo` to
  expose internal classes for testing" — and names the deliberate public widening chosen here as
  the alternative. It would in any case need twelve public-key-qualified entries, the assembly
  being strong-named.
- **A generator-emitted predicate** — one Liquid template rendering the predicate itself into each
  of the twelve test projects, exactly as `SharedGenerator` already renders four helper files into
  every configured project root. This is strictly narrower than the chosen design on public surface:
  it reaches all twelve, it cannot drift (one template), and it adds **nothing** to the shipped
  package. It is rejected on two grounds that public-surface width does not capture. First, C-10: a
  template-emitted copy lives only in this repository, so it cannot be pointed at a community
  gateway author's own assembly, and out-of-repo authors face the identical defect with no remedy.
  Second, a guard whose logic ships in the package is one the package can be held to; a guard that
  exists only in twelve generated test files is a private convention. That is what the public
  surface buys, and it is the honest reason to spend it.
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
  - [ADR 0064 — Validate Pipeline Assembly Scanning and Validation-Provider Registration](0064-validate-pipeline-assembly-and-provider-registration.md) (Accepted) — source of the placement rule and of "rules must not catch", from which this ADR's sweep deliberately differs, for stated reasons. Not superseded.
  - [ADR 0037 — Add Messaging Gateway Generated Tests](0037-add-messaging-gateway-generated-test.md) (Accepted) — established Liquid-template gateway tests and `MessagingGatewayConfiguration`; the pattern `GatewayConformanceGenerator` follows. Not superseded.
  - [ADR 0070 — Generator-Owned Rejection and Delay Conformance Tests](0070-generator-owned-rejection-and-delay-conformance.md) (Proposed) — conformance tests are generator-owned by default for every gateway configuration; this guard is another. Not superseded.
  - [ADR 0035 — Test Generation Tool](0035-generated-test.md) (Accepted) — the generator's original rationale. Not superseded.
- Project rules: [.agent_instructions/testing.md](../../.agent_instructions/testing.md) — *No InternalsVisibleTo*, *Test Scope and Isolation*, test and file naming, one class per file; [.agent_instructions/generated_tests.md](../../.agent_instructions/generated_tests.md) — never edit generated files, `SuitesFor(...)` and the generated-tree audit; [.agent_instructions/design_principles.md](../../.agent_instructions/design_principles.md) — Responsibility-Driven Design and "do not add new types without necessity".
- External references: GitHub issue [#4334](https://github.com/BrighterCommand/Brighter/issues/4334), prompted by [#4331](https://github.com/BrighterCommand/Brighter/issues/4331).
- Grounded code references (verified against the working tree):
  - `src/Paramore.Brighter/Subscription.cs:172` — `public virtual Type ChannelFactoryType => typeof(InMemoryChannelFactory)`; `:213` — the `MessagePumpType.Unknown` `ConfigurationException`; `Subscription` is non-abstract (`:35`) and `Subscription<T>` is at `:258`.
  - `src/Paramore.Brighter/IAmAChannelFactory.cs`, `src/Paramore.Brighter/InMemoryChannelFactory.cs` — both in namespace `Paramore.Brighter`; `src/Paramore.Brighter/Command.cs:42` — `public class Command : ICommand`, the representative generic argument.
  - The twenty-four shipped gateway subscription types — twelve base/derived pairs, one per assembly, none of them abstract, with `where T : class, IRequest` on `GcpPubSubSubscription<T>` (`GcpPubSubSubscription.cs:158-159`), `PostgresSubscription<T>` (`PostgresSubscription.cs:118-119`) and `RocketMqSubscription<T>` (`RocketMqSubscription.cs:117-118`), and `where T : IRequest` on the other nine.
  - The nine `override Type ChannelFactoryType` declarations, all on non-generic base classes: RocketMQ `:50`, GcpPubSub `:108`, Redis `:32`, AzureServiceBus `:39`, RMQ.Sync `:67`, RMQ.Async `:73`, Kafka `:162`, MsSql `:32`, MQTT `:35`.
  - `tools/Paramore.Brighter.Test.Generator/` — `Program.cs`, `Generators/{BaseGenerator,SharedGenerator,OutboxGenerator,MessagingGatewayGenerator,GenerationSuite,PlannedFile}.cs`, `Configuration/TestConfiguration.cs`, and `Templates/{MessagingGateway/{Reactor,Proactor},Outbox/{Sync,Async,Causation}}`.
  - `tests/Paramore.Brighter.Test.Generator.Tests/GeneratedFileAudit/GeneratedTreeAudit.cs` — the expected set built from `OutboxGenerator.Plan` and `MessagingGatewayGenerator.Plan`, and its scope note that only the `Generated/` tree is audited.
  - `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Generated/{Reactor,Proactor}` and `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/{Classic,Quorum}/Generated/…` — the two existing output shapes the conformance path must not collide with.
  - `.github/workflows/ci.yml` — the `build` job's `dotnet build --configuration Release` and generator-audit step; `:228` MQTT's `Category=MQTT` filter; `:361` Kafka's `Category=Kafka` filter; `:769` the commented-out RocketMQ job.
  - `src/Directory.Build.props` — `BrighterTargetFrameworks` (`netstandard2.0;net8.0;net9.0;net10.0`) and `TreatWarningsAsErrors`; `tests/Directory.Build.props` — `BrighterTestTargetFrameworks` (`net9.0;net10.0`).
