---
id: 0074-lifetime-validation-evaluation-site
title: "Where the scope-configuration rules are evaluated"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-08-02
summary: "FR-22's three lifetime rules, FR-24.3's duplicate-provider rule and FR-17's repeated-opt-in rule are evaluated in Paramore.Brighter.Extensions.DependencyInjection by a ScopeConfigurationValidator that implements the core IAmAPipelineValidator, decorates the core PipelineValidator and combines results — so both validation hosts fire it unchanged — reading the three configured lifetimes and DefaultScopeAffinity from the object IBrighterOptions resolves to at host start, and the ServiceDescriptors from a snapshot taken when ValidatePipelines() is called, with no container type added to core."
tags:
  - "di"
  - "lifetime"
  - "configuration"
  - "specification-pattern"
---

# 74. Where the scope-configuration rules are evaluated

Date: 2026-08-02

## Status

Proposed

## Context

Five configurations are now expressible that an application almost certainly did not intend: an opt-in that can never take effect; a set of lifetimes that shares dependencies across half a pipeline and not the other half; a process-lifetime artefact that requires a per-request one; two different ambient sources registered, of which only one is used; and two opt-in calls disagreeing about what was opted into. Each is silent at run time — the software works, adopts nothing, and says nothing — and FR-22, FR-24.3 and FR-17 require each to be reported at startup instead.

What no prior record decides is **which component evaluates those rules, and how it reaches its inputs.** That is a real question rather than a placement detail, because every one of those inputs is a container concept — two of the rules read service registrations, three read lifetime settings that only exist once the container is built — while the component that runs Brighter's startup validation today lives in core, where no container concept may go.

**Parent Requirement**: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md)

**Scope**: This ADR decides one thing — **the evaluation site for FR-22's three rules, FR-24.3's duplicate-provider rule and FR-17's repeated-opt-in rule, and the plumbing that gets their inputs to it**. It discharges FR-22 and the evaluation-site half of FR-24.3 and of FR-17.

It does **not** decide the rules or their severities. Those are fixed by the requirements and by D5, D8, D9 and D15, and are restated below as inputs rather than re-argued. It does not decide the registration model for `IAmAScopeProvider` — ADR 0072's — nor the three runtime `Warning` latches in `AmbientScopeDiagnostics`. **The boundary with ADR 0072 is exact: 0072 owns the run-time diagnostics a pipeline emits while asking for an ambient; this ADR owns the start-time findings `ValidatePipelines()` produces about the configuration.** They share no code and no state, and the message sets do not overlap.

**`docs/guides/lifetimes-and-scoping.md` is an implementation-plan deliverable, and this is where that is recorded.** FR-25 requires a new page at `docs/guides/lifetimes-and-scoping.md` and enumerates eleven things it must contain; NFR-10 makes that page — not the messages — the acceptance bar for the whole opt-in, and every message these five rules produce is required to name it (AC-43). The page is therefore stated here rather than nowhere, because this is the ADR whose errors are unactionable without it.

**It is not an ADR-level decision.** Writing a guidance page decides nothing that six ADRs have not already decided; what it needs is an owner in the implementation plan and a map saying where each clause's substance comes from, so that the page can be written without re-deciding anything and reviewed against the record. That map is complete — every clause has a source — and it is this:

| FR-25 clause | Where its substance is decided |
| --- | --- |
| 1 — the get/release cycle for `Transient`, `Scoped`, `Singleton` | ADR 0070 step 7 (transform pipelines) and ADR 0071 step 5 (handler pipelines), each a per-lifetime table; `0067-per-resolution-di-scope-for-transient-factory-instances` for `Transient`'s per-resolution scope |
| 2 — affinity applies to `Scoped` only (FR-21), and an inert opt-in is reported | ADR 0072's `ScopeAffinityPolicy` and adoption ladder for the first half; **this ADR's FR-22.1 rule** for the second |
| 3 — NFR-9's truth table | ADR 0072's adoption ladder supplies the *source* column for every outcome, and its *Artefact identity, restated for both affinities* supplies the identity rule; ADR 0075 supplies the `Publish`-subscriber and nested-pipeline rows. The table is the cross product of those rows with the three lifetimes and the two affinities — **NFR-9 is discharged by writing it, and this is the only place NFR-9 lands** |
| 4 — `IAmAScope` versus `IAmALifetime` (NFR-8) | ADR 0070's `IAmAScope` component entry, and ADR 0071's paragraph on `IAmALifetime` carrying two responsibilities |
| 5 — `Publish` subscribers, and pipelines nested inside them, cannot join the caller's transaction (C-4) | **ADR 0075**, which owns suppression and its two brackets |
| 6 — the `MapperLifetime.Scoped` break and its migration (FR-20) | ADR 0070 step 7a, which also fixes that this is one `release_notes.md` entry rather than four |
| 7 — no mixing `Transient` with `Scoped`, `Singleton` excluded, enforced only under `ValidatePipelines()` | **this ADR's FR-22.2 rule**, and C-18's compatibility note in step 7 |
| 8 — the captive-dependency hazard, and `ValidateScopes` as the complete check | **this ADR's** *Captive-dependency detection: what it reads, and what it cannot see* |
| 9 — the decision guide | **this ADR's FR-22.2 rule**, from which the passing set is derived — see below — with ADR 0072 for what adopting buys and ADR 0070 for what a per-pipeline scope is |
| 10 — validation only reaches you if you call it *and* a host runs, plus troubleshooting for the five messages | **this ADR's** *Both host shapes, enumerated* (D14's gap), and its five rule rows |
| 11 — the extension's affinity argument is the value (D18) | ADR 0076 step 4, with the three gestures themselves in ADR 0073 step 5 |

**Clause 9's table of passing configurations is derived, not authored.** FR-22.2's rule is *discard any of the three lifetimes that is `Singleton`; the remainder must be uniform* — so the configurations that pass are exactly `{Transient, Transient, Transient}`, `{Scoped, Scoped, Scoped}`, and either with any subset of members replaced by `Singleton`, less those FR-22.1 then rejects under `JoinAmbient` because nothing remains `Scoped`. The guide states that set with the cost of each; it does not restate the rule, and if the rule ever changes the table follows from it rather than drifting against it.

This ADR **supersedes no prior ADR.** It extends the `ValidatePipelines()` machinery of `0053-pipeline-validation-at-startup` and `0064-validate-pipeline-assembly-and-provider-registration`. Both are cited by slug throughout, and the bare numbers are avoided deliberately: `docs/adr` holds three files numbered 0053, two numbered 0054 and two numbered 0064, and C-16 assigns the bare "ADR 0064" to the *other* one — `0064-pipeline-cache-type-key`. Where the shortened forms **0053** and **0064** appear below they always mean the two slugs named here.

### Where this ADR sits

Seven ADRs deliver the parent requirement, one decision each. This is the fifth, and it exists because the decisions around it made wrong configurations expressible.

| ADR | Decides |
| --- | --- |
| 0070 | a transform pipeline takes one DI scope, carried **as a parameter** |
| 0071 | handler pipelines converge onto the **same handle**, carried on the object they already pass |
| 0072 | how a pipeline discovers an **ambient** DI scope the host owns |
| 0073 | the **ASP.NET Core package**, and the one line an application writes to opt in |
| **0074** *(this one)* | **where** the six scope-configuration rules are evaluated |
| 0075 | how a `Publish` subscriber **suppresses** adoption, for itself and everything nested beneath it |
| 0076 | the **affinity option**, and how one setting reaches all four registration paths in any order |

The rule the first two state is **the per-pipeline object carries the DI scope**. This ADR neither creates nor holds one: it reports, before any pipeline is built, on whether the configuration those objects will read is coherent.

What the siblings around it settled is what this one reads. ADR 0072 fixed the registration model for `IAmAScopeProvider` — a plain `AddSingleton` on every path, never `TryAddSingleton`, so every duplicate descriptor stays in the collection while Microsoft's container resolves the last. ADR 0076 fixed the opt-in as an affinity property on `IBrighterOptions`, defaulting to today's behaviour, together with the override that carries a registration extension's argument onto whichever options object the four registration paths produce, and ADR 0073 ships the extension that supplies it. Validation is therefore written last of the six that shape a configuration, because it cannot be written earlier: three of its five rules read values that only exist once 0076 has put them there, and the other two read the registration model 0072 and 0073 fixed. Nothing here changes a lifetime, a scope or a pipeline.

ADR 0067's `Terms` block defines the two axes this ADR turns on — Brighter's **configured lifetime**, which governs the artefact, and the container's **registration lifetime**, which governs that artefact's dependencies — and keeps `IServiceScope`, `ServiceProviderLifetimeScope` and `IAmALifetime` distinct. This ADR does not restate it. The rules below read *both* axes and never conflate them: FR-22.1 and FR-22.2 read only configured lifetimes; FR-22.3 reads a configured lifetime on one side and a registration lifetime on the other.

### The rules, fixed

| Rule | Condition | Severity |
| --- | --- | --- |
| **FR-22.1** | `DefaultScopeAffinity` is `JoinAmbient` and **none** of `HandlerLifetime`, `MapperLifetime`, `TransformerLifetime` is `Scoped` — the opt-in is inert (D5) | **Error** |
| **FR-22.2** | discarding any of the three that is `Singleton`, the remainder is not uniform — `Transient` and `Scoped` are mixed. Under either affinity (D8) | **Error** |
| **FR-22.3** | an artefact whose **configured** lifetime is `Singleton` takes a direct constructor parameter whose **registration** lifetime is `Scoped` — a captive dependency (D9) | **Warning** |
| **FR-24.3** | the service collection holds `IAmAScopeProvider` descriptors for more than one distinct implementation type | **Warning** |
| **FR-17** | the service collection holds affinity-override descriptors carrying more than one distinct `ScopeAffinity` value — the registration extension was called twice with different affinities | **Warning** |

Every message names `docs/guides/lifetimes-and-scoping.md` (AC-43), and FR-25.10 requires a troubleshooting entry keyed to each of the five.

Two properties of that set are load-bearing and follow from enumerating it rather than from reading any one row:

- **FR-22.1 and FR-22.2 are mutually exclusive by construction.** FR-22.2 fires only when the remainder contains `Scoped`; FR-22.1 fires only when nothing is `Scoped`. No host can receive both errors, so the two messages never contradict each other about what to do next.
- **FR-22.1 is "none is `Scoped`", not "all are `Transient`".** `{Singleton, Singleton, Singleton}` with `JoinAmbient` is an inert opt-in and is an error. AC-27 exercises the all-`Transient` case; the rule is wider than the case.

The three lifetimes are a **joint** choice. `{Scoped, Scoped, Transient}` is not a destination, so FR-22.2's message must list all three values and reach FR-25.9's decision guide (NFR-10) — a message that says only "this is wrong" fails the requirement.

### The forces

- **Core must gain no container types.** NFR-1's load-bearing clause is *source-level*: no file under `src/Paramore.Brighter/` may reference `ServiceLifetime`, `IServiceCollection`, `IServiceProvider` or `ServiceDescriptor`. Because `Microsoft.Extensions.DependencyInjection` is already on core's compile closure transitively through `Microsoft.Extensions.Logging`, those types **compile** in core today — a project-file check is vacuous and AC-22.3's source scan is the only real guard. Verified: that scan returns **zero** matches under `src/Paramore.Brighter/` today. The durable reason is ADR 0014's — Brighter offers per-family factory interfaces rather than abstracting an IoC container.
- **The rules are all container concepts.** Three of the five read `ServiceLifetime` directly; the other two read `ServiceDescriptor`s. There is no framing in which they belong in core.
- **The evaluator today is in core.** `PipelineValidator` is `src/Paramore.Brighter/Validation/PipelineValidator.cs:54`, and `IAmAPipelineValidator` is a core interface with a single `PipelineValidationResult Validate()`.
- **`PipelineValidator` has no access to `IBrighterOptions`.** `BrighterPipelineValidationExtensions.cs:91-93` constructs it with a pipeline builder, publications, subscriptions, consumer specs, inbox, outbox, provider registrations, a mapper-registry factory and a transformer probe — and nothing that carries a lifetime. Supplying the missing inputs is new work, and is why this is an ADR.
- **The inputs must be read as the factories read them.** The five container-backed factories read the object `IBrighterOptions` resolves to (`ServiceProviderMapperFactory.cs:44`), **not** `IOptions<BrighterOptions>.Value` — which is a different object on three of the four registration paths, because only one of them runs an `IOptions` pipeline at all (C-12a; the four sites are enumerated under *How the inputs reach the rules*). Validation must not pass a configuration the factories will ignore, nor fail one they would have honoured.
- **The only precedent for reading the container without resolving it does no constructor inspection at all.** `ServiceCollectionTransformerResolvabilityProbe` (`:40-56`) is a `HashSet<Type>` and a `Contains`. It is a precedent for *snapshot-and-query-without-resolving* and for nothing else; the constructor and lifetime walk FR-22.3 needs is new.
- **`ValidatePipelines()` is opt-in and snapshots at call time** (`BrighterPipelineValidationExtensions.cs:58`; step 2 gives the existing capture points it joins). C-15 makes the residual gaps explicit and accepted.
- **Both host shapes must fire, and the consumer one is not Brighter's to register.** `AddConsumers` sets `ConsumerOwnsValidation`, which makes `BrighterValidationHostedService.StartAsync` return immediately (`BrighterValidationHostedService.cs:73`), so the consumer path runs through `ServiceActivatorHostedService` — which nothing in `src` registers (D14). Any change to that host would be exercised only where a test registers the hosting package itself. *Both host shapes, enumerated* below walks all five combinations with their citations.
- **Errors and warnings must stay distinguishable.** Three of the five messages are warnings, and a warning must never block startup whatever `ThrowOnError` says.

## Decision

**The five rules are evaluated by a validator in the container package that decorates the core one, so the existing validation hosts fire it unchanged and neither of them knows.**

The shape that takes is a decorator rather than a second registration, because both hosts consume exactly one validator: the decorating validator implements the core validation interface, is what the existing registration inside `ValidatePipelines()` hands back, runs the core validator first and returns the two results combined. Its lifetime and affinity inputs are read from the object `IBrighterOptions` resolves to, at host start; its registration inputs are snapshotted from the service collection at `ValidatePipelines()` call time. No type in `Paramore.Brighter` gains a container concept, and no core rule family is extended. The types and signatures are under *Key Components*.

### The mechanism, end to end

The rules must run in the DI package, because every input they read is a container concept. The component that runs Brighter's startup validation lives in core, and both hosted services consume exactly **one** validator. So the DI package does not add a validator — it **wraps** the one that already exists, at the registration point it already owns, and returns the combined result. Neither hosted service changes, and neither knows.

The inputs arrive at two deliberately different instants, and a validation run is a function of exactly those two:

```mermaid
sequenceDiagram
    participant VP as ValidatePipelines(), at call time
    participant SC as builder.Services
    participant Host as the validation hosted service, at host start
    participant Dec as ScopeConfigurationValidator
    participant Inner as PipelineValidator, core

    Note over VP,SC: CAPTURE 1 — ValidatePipelines() call time
    VP->>SC: snapshot every ServiceDescriptor
    VP->>VP: the existing TryAddSingleton for IAmAPipelineValidator<br/>now returns the decorator, wrapping the core validator

    Note over Host,Inner: CAPTURE 2, then evaluation — host start
    Host->>Dec: resolve IAmAPipelineValidator
    Dec->>Dec: read the affinity and the three lifetimes<br/>from the resolved IBrighterOptions
    Host->>Dec: Validate()
    Dec->>Inner: Validate()
    Inner-->>Dec: handler, producer and consumer findings
    Dec->>Dec: evaluate FR-22.1, FR-22.2, FR-22.3 and FR-24.3
    Dec-->>Host: Combine(inner, own)
    Host->>Host: throw on errors under ThrowOnError, and log warnings always

    Note over Dec,Inner: at shutdown the container disposes the decorator,<br/>which MUST cascade into the inner validator
```

Why those two instants and not one. The descriptors are snapshotted at call time because that is what `ValidatePipelines()` already does with its provider registrations and its transformer probe, and because C-15's "call it last" guidance depends on it. The lifetimes and the affinity are read at host start because on three of the four registration paths the value does not exist until `IBrighterOptions` is resolved, and because ADR 0076's override lands inside that resolution. Reading `IOptions<BrighterOptions>.Value` instead would read a *different object* on three of the four paths (C-12a) — validation would then pass configurations the factories will ignore, and fail ones they would have honoured.

The last note on the diagram is easy to miss and expensive to get wrong: the container tracks only the instance a factory **returns**, so an inner validator created inside the delegate and not returned would never be disposed, and the `MessageMapperRegistry` it may have built lazily — with the mapper factory and any DI scope it holds — would live to process exit.

### Where the pieces live

```mermaid
flowchart TB
    subgraph core["Paramore.Brighter — core: no container types, and none added"]
        iface["IAmAPipelineValidator — PipelineValidationResult Validate()"]
        pv["PipelineValidator — handler, producer and consumer rule families"]
        result["PipelineValidationResult — Errors, Warnings, IsValid, ThrowIfInvalid()<br/>Combine, which exists today and is unused in src"]
        specs["ISpecification, Specification, ValidationResultCollector<br/>ValidationError with ValidationSeverity, Source, Message"]
        eval["SpecificationEvaluator — NEW<br/>the harvest loop, lifted out of PipelineValidator. Structural only"]
        pv --> eval
    end

    subgraph di["Paramore.Brighter.Extensions.DependencyInjection"]
        dec["ScopeConfigurationValidator — NEW, public<br/>IAmAPipelineValidator and IDisposable"]
        rules["ScopeConfigurationRules — NEW, internal<br/>FR-22.1, FR-22.2, FR-22.3, FR-24.3, FR-17"]
        ents["the entities, all NEW and internal<br/>ScopeConfiguration, one per host<br/>ScopeProviderRegistration, one per registered provider<br/>ArtefactRegistration, one per candidate artefact<br/>ArtefactKind, which lifetime governs an artefact<br/>ContainerRegistrationSnapshot, descriptors taken at call time<br/>ArtefactConstructorSelector, D15's rule in one place<br/>ArtefactExclusionSet, the attribute half of FR-22.3's conjunction"]
        dec --> rules
        rules --> ents
    end

    subgraph hosts["the two validation hosts — unchanged"]
        h1["BrighterValidationHostedService, producer"]
        h2["ServiceActivatorHostedService, consumer"]
    end

    dec -- "implements" --> iface
    dec -- "wraps, and disposes" --> pv
    dec -. "Combine(inner, own)" .-> result
    rules -- "instantiated over DI-package entities" --> specs
    h1 -. "resolves the one IAmAPipelineValidator" .-> dec
    h2 -. "resolves the one IAmAPipelineValidator" .-> dec
```

**Reading the edges**, on the convention ADRs 0070 and 0071 use: a solid arrow is a compile-time reference or an ownership, a dotted arrow is a runtime call or resolution. Every solid arrow crossing into core runs from the DI package inward, which is the real reference direction — core names nothing here. The two host edges are dotted because the hosts do not reference the decorator at all: they resolve `IAmAPipelineValidator` and get it, which is the whole reason neither of them changes.

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Stereotype | Responsibility |
| --- | --- | --- | --- |
| Validation coordinator | `ScopeConfigurationValidator` (DI package) | **doing** | Runs the inner validator, evaluates the container rule set, combines the two results, and owns the inner validator's disposal |
| Host configuration | `ScopeConfiguration` (DI package) | **knowing** (information holder) | The affinity and the three configured lifetimes as the factories see them, plus the ambient-source and affinity-override registrations in registration order — each of the former a `ScopeProviderRegistration`, which pairs an implementation type (or, where none is statically known, a position) with that position |
| Artefact under test | `ArtefactRegistration` (DI package) | **knowing** | One candidate artefact: its type, its `ArtefactKind` — handler, mapper or transform, which is what selects the governing configured lifetime — and that lifetime's value |
| Registration snapshot | `ContainerRegistrationSnapshot` (DI package) | **knowing** | The descriptors as they stood when `ValidatePipelines()` was called; answers "what lifetime is this service type registered with" and "what artefacts are registered" without resolving anything |
| Constructor choice | `ArtefactConstructorSelector` (DI package) | **deciding** | D15's rule, and only D15's rule: the public constructor with the most parameters; on a tie, none |
| Brighter's own artefacts | `ArtefactExclusionSet` (DI package) | **knowing** (information holder) | The set of artefact types returned by a `RequestHandlerAttribute` or `TransformAttribute` `GetHandlerType()`. It answers one question — is this type one Brighter put in the pipeline itself — and holds the attribute half of FR-22.3's conjunction |
| The rules | `ScopeConfigurationRules` (DI package) | **deciding** ×5 | Each rule decides whether one entity satisfies it, and what the finding says when it does not |
| Finding | `ValidationError` (core) | **knowing** | Severity, source, message — unchanged |
| Reporting | the two hosted services | **doing** | Throw on errors under `ThrowOnError`, log warnings always — unchanged |

`ScopeConfigurationValidator` is the only one of these that is public; the rest are `internal` to the DI package. Only the validator is something an application can meaningfully name, and only because it is what `IAmAPipelineValidator` now resolves to.

#### The evaluation site: a decorating validator, not a second registration

`IAmAPipelineValidator` is registered `TryAddSingleton` (`BrighterPipelineValidationExtensions.cs:71`), and **both** hosts consume exactly one instance: `BrighterValidationHostedService` takes it as a constructor parameter (`:60`) and `ServiceActivatorHostedService` resolves it with `GetService<IAmAPipelineValidator>()` (`:50`). Registering a second implementation with a plain `AddSingleton` therefore does not compose — Microsoft's container would resolve the last descriptor and the core validator's findings would silently disappear.

So the DI package composes at the point it already owns. The factory delegate at `:71-94` continues to build the core `PipelineValidator` exactly as it does today, and returns it wrapped:

```csharp
builder.Services.TryAddSingleton<IAmAPipelineValidator>(sp =>
{
    //ONE registry for the whole validation run, owned here. The inner validator is given a
    //factory that hands back this same instance, so its Lazy can never build a second.
    var registry = new Lazy<MessageMapperRegistry>(registryFactory);

    var inner = new PipelineValidator(/* exactly as today */, mapperRegistryFactory: () => registry.Value);

    return new ScopeConfigurationValidator(
        inner,
        sp.GetRequiredService<IBrighterOptions>(),
        snapshot,                                     // captured from builder.Services, above the delegate
        ArtefactExclusionSet.Build(registry.Value),   // Brighter's own attribute-returned artefacts
        registry);                                    // owned: disposed with the decorator
});
```

Three consequences of that shape, each of which is why it was chosen:

- **Neither hosted service changes.** Both host shapes fire, and the change is invisible to `ServiceActivatorHostedService` — which matters, because D14 records that nothing in `src` registers it, so a change there would be exercised only by tests that register it explicitly (AC-40 does).
- **`TryAddSingleton` keeps its meaning.** An application that registers its own `IAmAPipelineValidator` before calling `ValidatePipelines()` replaces Brighter's validation wholesale, exactly as today. That escape hatch now costs five more rules; it is unchanged in kind.
- **The core validator stays untouched.** No new constructor parameter, no new rule family, no new entity type in core. AC-22.3's scan finds nothing new because there is nothing new in core to find.

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `ScopeConfigurationValidator.Validate()` | none (all inputs held from construction) | a `PipelineValidationResult` combining the inner validator's findings with this ADR's five rules | Propagates whatever the inner validator propagates. A rule-body exception is converted by `Specification<T>` into a `ValidationSeverity.Error` finding, as it is for every existing rule (ADR 0064 (`0064-validate-pipeline-assembly-and-provider-registration`)) — the rules add no bespoke `try`/`catch` |
| `ScopeConfigurationValidator.Dispose()` | none | — | Disposes the inner `PipelineValidator`. Idempotent |

That last row is not incidental. `PipelineValidator` implements `IDisposable` and its `Dispose` (`:85`) drains the `MessageMapperRegistry` it may have built lazily, on the stated understanding that the container disposes the validator at shutdown. The container tracks only the instance a factory **returns**, so an inner validator created inside the delegate and not returned would never be disposed and the registry — with the mapper factory and any DI scope it holds — would live to process exit. The decorator must cascade, and that obligation is stated here rather than left to be discovered.

#### How the inputs reach the rules — two capture points, deliberately different

| Input | Read from | When | Why then |
| --- | --- | --- | --- |
| `DefaultScopeAffinity`, `HandlerLifetime`, `MapperLifetime`, `TransformerLifetime` | the object `IBrighterOptions` resolves to | at host start, from the built container | Per D18 the ASP.NET extension's affinity argument is written into Brighter's own `IBrighterOptions` registration and therefore lands after every application options delegate; and on three of four paths there is no `IOptions` pipeline at all, so the value only exists once `IBrighterOptions` is resolved. Reading `IOptions<BrighterOptions>.Value` instead would read a different object on three of the four paths (C-12a) |
| every `ServiceDescriptor` | `builder.Services` | at `ValidatePipelines()` call time | C-15's snapshot semantics, and the same point at which `ValidationProviderRegistrations` (`:64-66`) and `ServiceCollectionTransformerResolvabilityProbe` (`:68-69`) are already captured. AC-32 requires `ValidatePipelines()` to be called after both provider registrations for the duplicate to be seen |

`GetRequiredService<IBrighterOptions>()` is safe on all four entry points: each `TryAddSingleton`s it — `AddBrighter(Action)` at `ServiceCollectionExtensions.cs:74`, `AddBrighter(Func)` at `:97`, `AddConsumers(Action)` at `ServiceActivator…/ServiceCollectionExtensions.cs:38`, `AddConsumers(Func)` at `:88` — and all four route through `BrighterHandlerBuilder`, so each alone is a complete host.

`IBrighterOptions` is `TryAddSingleton` on both sides, so in a mixed host **first registration wins** (C-12). The validator reads whichever object that is. With `AddBrighter` before `AddConsumers(Action<ConsumersOptions>)` it reads the producer's `BrighterOptions`, which is what AC-40 requires; in the reverse order it reads the `ConsumersOptions` instance, and the producer's affinity and lifetimes are never seen — by the factories either. **That is correct, not a defect**: the requirement is that validation reads the configuration the factories honour, including when that is the surprising object. (The pre-existing `InvalidCastException` from `AddBrighter` before the `Func` overload of `AddConsumers` (`:89-90`) is C-12's, is untouched here, and already bites `ResolveSubscriptions`.)

#### The five rules

Each is an `ISpecification<T>` built with the existing `Specification<T>` constructors, evaluated with `ValidationResultCollector<T>` — the same machinery ADR 0053 (`0053-pipeline-validation-at-startup`) established and ADR 0064 extended, instantiated in the DI package over DI-package entity types. There are two entity families.

**Family 1 — `ScopeConfiguration`, exactly one per host.** Carries the affinity, the three configured lifetimes, the `IAmAScopeProvider` registrations and the affinity-override registrations. Four rules evaluate it:

| Rule | Message must contain | `Source` |
| --- | --- | --- |
| FR-22.1 | the affinity setting; all three lifetimes **with their values**; that the opt-in has no effect; the guidance page | `"Brighter options"` |
| FR-22.2 | all three lifetimes with their values; that the mixed pair do not share pipeline-scoped dependencies; the guidance page | `"Brighter options"` |
| FR-24.3 | every registered implementation type; which one is effective (the **last** registered, matching Microsoft's resolution); the guidance page | `"Scope provider registration"` |
| FR-17 | every `ScopeAffinity` value registered; which is effective (the **last**, matching Microsoft's resolution); that the extension is called once and its argument is how an affinity is selected; the guidance page | `"Scope affinity registration"` |

FR-24.3's detail, stated over the family of descriptor shapes rather than the common one: a descriptor whose `ImplementationType` is statically known contributes that type; one registered by factory delegate or instance contributes its registration **position**, and its runtime type where `ImplementationInstance` supplies one. Distinctness is over implementation types, so the *same* implementation type registered twice is not a finding (AC-32's second branch) — it is idempotent in effect. Because Brighter registers no default provider (D11), the ASP.NET extension can never itself create a duplicate; two application registrations are the only way to reach this rule.

**FR-17 is the same rule shape over a different distinctness key, and the two are complementary rather than overlapping.** FR-24.3 asks whether two *different providers* were registered; FR-17 asks whether two *different affinities* were. A host that calls ADR 0073's extension twice reaches only the second: both calls register the same `HttpContextScopeProvider`, which FR-24.3 excludes in terms — the exclusion is exactly why a fifth rule is needed rather than a wider fourth one. Distinctness here is over the `ScopeAffinity` **value**, so a repeat carrying the same affinity is not a finding, mirroring FR-24.3's own exclusion and for the same reason (AC-49's third branch). The values are read from the descriptors' `ImplementationInstance` — ADR 0073's extension registers ADR 0076's override as an instance, with plain `AddSingleton` precisely so that every call's descriptor survives for this rule to see (FR-17); a descriptor supplying no instance contributes its registration position, as FR-24.3's does. This rule needs no new input: the descriptors are already in the `ValidatePipelines()`-time snapshot the other container rule reads.

**Family 2 — `ArtefactRegistration`, one per candidate artefact.** FR-22.3 evaluates it, and yields one `Warning` per captive parameter, naming the artefact type and the `Scoped` service it requires, plus the guidance page. `Source` is `$"{kind} '{artefactType.Name}'"`.

#### Captive-dependency detection: what it reads, and what it cannot see

**Candidates** come from the snapshot, not from the describe path. A descriptor contributes a candidate when its implementation type implements one of the core, container-free marker interfaces — `IHandleRequests`/`IHandleRequestsAsync` (Handler), `IAmAMessageMapper`/`IAmAMessageMapperAsync` (Mapper), `IAmAMessageTransform`/`IAmAMessageTransformAsync` (Transform). That is exactly FR-22.3's "discovered by assembly scanning or registered explicitly": all three registration builders register the artefact as its own service type at `ServiceLifetime.Transient` (`ServiceCollectionSubscriberRegistry.cs:63`, `:76`, `:90`, `:116`, `:130`, `:146`, `:160`; `ServiceCollectionMessageMapperRegistryBuilder.cs:80`, `:99`, `:116`, `:117`, `:127`, `:137`; `ServiceCollectionTransformerRegistry.cs:56`).

**The kind selects the configured lifetime.** Handlers are governed by `HandlerLifetime`, mappers by `MapperLifetime`, transforms by `TransformerLifetime`; only candidates whose governing lifetime is `Singleton` are inspected. This is why AC-42 moves the `Singleton` between kinds as it moves between cases: no single triple can serve a `Singleton` mapper and a `Singleton` transform at once. A type presenting two kinds is evaluated under each, and findings are de-duplicated by (artefact type, dependency service type) so it cannot be reported twice for one dependency.

**Exclusion is a conjunction, and the conjunction is the point.** A candidate is Brighter's own — and excluded — when it is **both** returned by a `RequestHandlerAttribute.GetHandlerType()` (`RequestHandlerAttribute.cs:91`, `public abstract`) or a `TransformAttribute.GetHandlerType()` (the type is `TransformAttribute`; the file is `TransformAttributeBase.cs`, class `:5`, member `:17`) **and** defined in an assembly whose simple name is `Paramore.Brighter` or begins with `Paramore.Brighter.` — the trailing dot is part of the rule.

The attribute-returned set is read from the reflection-only describe path that already exists and instantiates nothing: `PipelineBuilder<IRequest>.Describe()` (`PipelineBuilder.cs:151`) yields every `PipelineStepDescription.HandlerType`, and `TransformPipelineBuilder.DescribeTransforms(...)` (`:270`) yields every `TransformStepDescription.TransformType` for each request type reachable from the publications, the subscriptions, and the registered handlers. A mapper reachable by none of those three is unreachable at run time as well, so nothing is lost.

Both halves are load-bearing, and both are pinned by AC-42:

- Without the transform half, `ClaimCheckTransformer` (`src/Paramore.Brighter/Transforms/Transformers/ClaimCheckTransformer.cs:62`, taking `IAmAStorageProvider` and `IAmAStorageProviderAsync`) would be warned against in any host with `TransformerLifetime = Singleton` and an `AddScoped` storage provider — a Brighter type reported as the user's. Transforms never pass through `RequestHandlerAttribute`.
- Without the prefix half, a transform in an assembly named `Paramore.Brighter.Something` would not be excluded. No Brighter-shipped out-of-core transform can pin this: `JustSayingCompressionTransform` (`Paramore.Brighter.Transformers.JustSaying/JustSayingCompressionTransform.cs:34`) and `MassTransitTransform` (`:40`) are both parameterless, so a case built on either would raise no warning under an exact-name implementation and would prove nothing. AC-42 uses a transform in `Paramore.Brighter.Extensions.Tests` instead.

Note that Brighter's own attribute-driven handler decorators are excluded **by this mechanism**, not incidentally by being open generics. `ExceptionPolicyHandlerAsync<>` is registered by `ServiceCollectionSubscriberRegistry` and would also be skipped by the open-generic rule below; the exclusion is applied first so that AC-42's `[UsePolicyAsync]` clause pins the mechanism it is written to pin.

**Constructor selection is D15's rule and lives in one object.** `ArtefactConstructorSelector` returns the public constructor with the most parameters; where two public constructors have the same parameter count, it returns nothing and the type is not inspected. A type with no public constructor, or with only a parameterless one, yields nothing.

This is deliberately **not** Microsoft's selection, which additionally requires the winner's parameter set to be a superset of every other resolvable candidate's — throwing `InvalidOperationException` otherwise — and which treats `IServiceProvider`, `IServiceScopeFactory` and `IEnumerable<T>` as resolvable with no descriptor. The divergence is acceptable, and the reason is that the two are answering different questions. Microsoft's selector answers *which constructor will I activate*, and it can only answer it for a type it is willing to activate at all. Brighter's rule answers *what does this type appear to require*, for a type nobody is going to activate — the whole value of the check is that it runs before anything is built. AC-42's final clause makes the divergence explicit and necessary: a mapper with two same-count constructors is not activatable by Microsoft's container at all, and the AC asserts validation output while forbidding the mapper to be resolved. A rule that reproduced Microsoft's selection could not report on that type, because Microsoft's selection has no answer for it.

**Each parameter's lifetime** is the `ServiceLifetime` of its descriptor in the snapshot. Where the parameter type is a constructed generic with no descriptor of its own, the descriptor for its generic type definition is used — that is the descriptor Microsoft's container would resolve through, so it is a faithful reading of "the parameter's own descriptor" rather than a widening of the rule. Where more than one descriptor exists for a service type, the **last** is read, matching Microsoft's resolution and FR-24.3's last-wins.

**Failure modes, enumerated and accepted.** All but the last are cases where the rule reports nothing; the last is the one case where it can report wrongly, and it is called out as such.

| Case | Outcome | Standing |
| --- | --- | --- |
| A registration made **after** `ValidatePipelines()` was called | invisible, and on one branch **wrong**: a mapper registered later is not a candidate at all, and a later `AddScoped<IOrderDbContext>()` makes a real captive dependency invisible — while a later registration that *changes* a service type's effective lifetime makes the snapshot's last-descriptor reading disagree with what the container will resolve, so a warning can be raised about a dependency that is no longer `Scoped`, or withheld about one that now is | C-15, and the reason the guidance is "call `ValidatePipelines()` last" |
| Open generic artefact type | not inspected — a parameter typed by a type parameter has no descriptor to read | Necessary; no meaningful reading exists |
| Artefact registered by factory delegate or instance | not a candidate — no statically known implementation type | C-20(ii), explicitly |
| Parameter with no descriptor, including `IServiceProvider`, `IServiceScopeFactory` and `IEnumerable<T>` | not a finding, per FR-22.3's "what is read for each parameter" | C-20(i)'s divergence from Microsoft's always-resolvable services |
| Transitive captivity — a `Singleton` artefact taking a `Transient` that itself takes a `Scoped` | not reported | C-20(ii), pinned by AC-42 |
| Two public constructors of equal parameter count | not inspected | D15, pinned by AC-42 |
| An application type in a `Paramore.Brighter.*` assembly, returned by an attribute | excluded | C-20(iii), pinned by AC-42 |
| A Brighter-shipped **mapper** with constructor dependencies | **would be reported** — no mapper is returned by an attribute, so the exclusion cannot reach one | C-20(iv), pinned by AC-42's paired same-assembly mapper case. Latent today; Brighter ships no such mapper |

**Nothing is resolved and no provider is built.** The snapshot is the same technique `ServiceCollectionTransformerResolvabilityProbe` uses, extended from a membership test to a constructor and lifetime walk.

#### The result path is the existing one, and it already carries both severities

Verified rather than assumed, because three of the five messages are warnings:

- `ValidationSeverity` has exactly `Error = 0` and `Warning = 1`. No third severity is needed and none is added.
- `PipelineValidationResult.IsValid` is `Errors.Count == 0` (`:45`) — warnings alone never make a result invalid — and `ThrowIfInvalid()` (`:52`) throws `PipelineValidationException` only when errors are present.
- `PipelineValidationResult.Combine` (`:64`) merges errors and warnings from several results. It exists and is unused in `src`; this is the composition it was written for.
- `ThrowOnError` (`BrighterPipelineValidationOptions.cs:47`, default `true`) gates **errors only**, in both hosts: `BrighterValidationHostedService` throws at `:80` or logs errors at `:84`, and logs every warning at `:90-93` regardless; `ServiceActivatorHostedService` does the same at `:57`/`:61` and `:67-70`.

So FR-22.1 and FR-22.2 fail startup under `throwOnError: true` and log at `LogLevel.Error` under `throwOnError: false` (AC-27, AC-28), while FR-22.3 and FR-24.3 log at `LogLevel.Warning` and never block (AC-42, AC-32). A parallel result path would have had to reproduce all of that.

#### Both host shapes, enumerated

| Host | `ConsumerOwnsValidation` | Who validates | Fires? |
| --- | --- | --- | --- |
| Producer — `AddBrighter` only | false | `BrighterValidationHostedService` | Yes |
| Consumer — `AddConsumers`, hosting package registered | true (`:60` or `:127`) | `ServiceActivatorHostedService` (`:45-71`) | Yes |
| Consumer — `AddConsumers`, hosting package **not** registered | true | nobody — `BrighterValidationHostedService` returns at `:73` and nothing takes over | **No** (D14) |
| Mixed — `AddBrighter` then `AddConsumers(Action)` | true | `ServiceActivatorHostedService`; reads the producer's options object (C-12 first-wins) | Yes |
| Mixed — `AddConsumers(Action)` then `AddBrighter` | true | `ServiceActivatorHostedService`; reads the `ConsumersOptions` instance | Yes, against that object |

Rows 1, 2, 4 and 5 fire because both hosts resolve the same `IAmAPipelineValidator`, which is now the decorator. Row 3 is D14's accepted gap, unchanged by this ADR: FR-25.10's guidance must tell consumer applications to register `ServiceActivatorHostedService`, and AC-40 registers it explicitly.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| `Paramore.Brighter` | `SpecificationEvaluator` | **new** — the entity/spec harvest loop lifted verbatim out of `PipelineValidator.EvaluateSpecs` (`:152`). No container types |
| `Paramore.Brighter` | `PipelineValidator` | calls the extracted evaluator; no behaviour change, no signature change |
| `…DependencyInjection` | `ScopeConfigurationValidator` | **new**, public — `IAmAPipelineValidator`, `IDisposable` |
| `…DependencyInjection` | `ScopeConfiguration`, `ScopeProviderRegistration`, `ArtefactRegistration`, `ArtefactKind`, `ContainerRegistrationSnapshot`, `ArtefactConstructorSelector`, `ScopeConfigurationRules`, `ArtefactExclusionSet` | **new**, internal |
| `…DependencyInjection` | `BrighterPipelineValidationExtensions.ValidatePipelines` (`:58`) | captures the descriptor snapshot beside the existing probe and provider-registration captures; the factory at `:71` returns the decorator |

Unchanged, and named so the omission is not read as an oversight: `IAmAPipelineValidator`; `PipelineValidationResult` and `PipelineValidationException`; `ValidationError` and `ValidationSeverity`; `BrighterValidationHostedService`; `ServiceActivatorHostedService`; `BrighterPipelineValidationOptions` and the `ValidatePipelines(enabled, throwOnError = true)` signature and defaults; every existing rule's severity, message and blocking behaviour; `ServiceCollectionTransformerResolvabilityProbe`; and `AmbientScopeDiagnostics`, which is ADR 0072's and shares nothing with this.

### Technology Choices

**Why the rules are not `ISpecification<T>` families inside the core validator.** They could be, if their entity type lived in core — and their entity type carries `ServiceLifetime`. Putting it there fails AC-22.3's scan; mirroring `ServiceLifetime` as a core enum would *pass* the scan, because the scan names four identifiers and not a concept, and that is precisely why the mirror is worse than an honest violation. Core would then have to define what `Scoped` means with no container to define it against, and every future container package would have to map its own lifetimes onto Brighter's mirror rather than the other way round — the opposite of what NFR-7 and ADR 0014 ask for.

**Why the decorator holds its inputs rather than reaching for them.** The affinity and the three lifetimes are captured once, at construction, from the resolved `IBrighterOptions`; the descriptors once, at `ValidatePipelines()` call time. Nothing is read lazily during `Validate()`, so the result of a validation run is a function of two well-defined instants, both of which an Acceptance Criterion can name. That is what makes AC-32's ordering requirement testable at all. It is worth being exact about what AC-45 does and does not buy here: it pins the affinity on the **resolved `IBrighterOptions`** across all four registration paths, and this ADR reads that same object by the same route — but no Acceptance Criterion asserts what the *validator* reads, and AC-27, AC-28, AC-40, AC-41 and AC-42 each use a single registration path. The input-source choice is therefore argued, not pinned; the risk table records it.

**Why `ScopeConfiguration` and `ArtefactRegistration` are records rather than parameter lists.** Three same-typed `ServiceLifetime` values in positional order is exactly the transposition hazard ADR 0064 introduced `ValidationProviderRegistrations` to avoid, and here it is worse: transposing `MapperLifetime` and `TransformerLifetime` produces a rule that still passes AC-41 and still fails AC-28, and would be caught by nothing until AC-42's kind-varying cases.

**Why `ArtefactConstructorSelector` is its own object.** D15's rule is a *deciding* responsibility with three cases — widest, tie, none — and AC-42 tests each. Inlining it into the captive-dependency rule would make those cases reachable only through a built host.

**Why the harvest loop moves to core rather than being copied.** The alternative is a second copy of "evaluate a spec family and collect the failed results", including its result conventions. Duplicating knowledge is the worse of the two costs; the extraction names no container type and is a structural change made ahead of the behavioural one, per Tidy First.

**Naming.** `ScopeConfigurationValidator` is this ADR's name to choose — it is not one of C-11's working names. It is chosen over `LifetimeValidator` because the rule set is wider than lifetimes (FR-24.3 is about a registration) and narrower than validation (the core validator is the other half), and all five rules are about how a pipeline is scoped.

### Implementation Approach

1. **Structural, first and alone.** Extract `PipelineValidator.EvaluateSpecs` (`:152`) into `SpecificationEvaluator` in `Paramore.Brighter`; `PipelineValidator` calls it. No behaviour change; the existing validation tests are the guard.
2. **The snapshot.** Add `ContainerRegistrationSnapshot`, built from `builder.Services` inside `ValidatePipelines()` beside the existing `ValidationProviderRegistrations` computation (`:64-66`) and the transformer probe (`:68-69`). Two queries: the lifetime for a service type, and the artefact candidates with their kinds.
3. **The entities and the selector.** `ScopeConfiguration`, `ScopeProviderRegistration`, `ArtefactRegistration`, `ArtefactKind`, `ArtefactConstructorSelector`. The selector is testable with a `Type` alone.
4. **The five rules.** `ScopeConfigurationRules` returns `ISpecification<ScopeConfiguration>` for FR-22.1, FR-22.2, FR-24.3 and FR-17, and `ISpecification<ArtefactRegistration>` for FR-22.3, using the collapsed `Specification<T>` constructor where a rule yields more than one finding. Rules do not catch — ADR 0064's precedent.
5. **The decorator.** `ScopeConfigurationValidator` runs the inner validator, evaluates both entity families through `SpecificationEvaluator`, returns `PipelineValidationResult.Combine(...)`, and disposes the inner validator.

5a. **The exclusion set, and who owns the registry it needs.** FR-22.3's exclusion is a conjunction, and its attribute half cannot be read from the descriptor snapshot: it needs the reflection-only describe path, and `TransformPipelineBuilder.DescribeTransforms` (`:270`, `public static`) takes a `MessageMapperRegistry`. `ArtefactExclusionSet.Build(registry)` makes that pass once, over every request type reachable from the publications, the subscriptions and the registered handlers, and holds the resulting set.

   The registry it uses is the **only** one the validation run has, and the DI package owns it. `PipelineValidator` takes a `Func<MessageMapperRegistry>?` and wraps it in a `Lazy` that it builds at most once, only if the wrap-transform rule needs it (`:69-71`, `:139-140`). The delegate therefore constructs the `Lazy` itself and passes `() => registry.Value` inward, so the inner validator's `Lazy` can only ever hand back this instance. Both objects may call `Dispose` on it — the inner does if its rule ran (`:92-93`), the decorator does unconditionally — and that is safe by construction, not by luck: `MessageMapperRegistry.Dispose()` claims with a single `Interlocked.Exchange` and returns on a second call (`:360-362`), a guard whose own comment says it exists so that an owner and the container can both dispose it.

   The alternative — building the exclusion set from a registry of the decorator's own — was rejected for the reason this ADR already gives against letting the inner validator go undisposed: a second `MessageMapperRegistry` brings its own mapper factories and its own DI scope, and nothing in the container tracks it.
6. **The wiring.** One change in `ValidatePipelines()`: return the decorator from the existing `TryAddSingleton` factory. Nothing else in the extension method moves.
7. **The documentation this ADR owes.** `docs/guides/lifetimes-and-scoping.md` gains a troubleshooting entry for each of the five messages (FR-25.10), and `release_notes.md` gains C-18's compatibility note beside FR-20's break (AC-24).

## Consequences

### Positive

- **Core gains nothing.** Not a type, not a parameter, not a reference. AC-22.3's source scan returns zero matches before the change and zero after, and clause 1 of AC-22 is unaffected because no core interface changes at all.
- **Both host shapes fire without either being touched.** The decorator is what `IAmAPipelineValidator` resolves to, so `BrighterValidationHostedService` and `ServiceActivatorHostedService` both pick it up unchanged — including the consumer path, which is where `MapperLifetime` and `TransformerLifetime` matter most and where a change would have been exercised only by tests that register the hosting package themselves.
- **The reporting path is the one that already works.** Errors block under `ThrowOnError` and log at `Error` without it; warnings log and never block, in both hosts. No new exception type, no new hosted service, no new severity, no change to `ValidatePipelines(enabled, throwOnError = true)`.
- **The inputs are the ones the factories honour, on all four registration paths.** Reading the resolved `IBrighterOptions` rather than `IOptions<BrighterOptions>.Value` means validation cannot pass a configuration the factories will ignore or fail one they would have honoured — the failure mode C-12a describes and AC-45 asserts against.
- **Nothing is resolved and no application constructor runs.** The captive-dependency rule reads descriptors and reflects over constructors; a `Singleton` mapper with a `Scoped` dependency is *reported*, not *thrown*, which is the entire point of detecting it.
- **The rules are unit-testable without a host.** A `ServiceCollection`, an options object and a `Type` are enough for every clause of AC-42 except the two that assert host startup.
- **FR-22.1 and FR-22.2 cannot both fire**, so an application never receives two errors prescribing different remedies.

### Negative

- **FR-22.2 is a compatibility break, and validating applications pay it.** An application that today sets, say, `HandlerLifetime = Scoped` with `MapperLifetime = Transient` works — the two simply do not share pipeline-scoped dependencies — and if it calls `ValidatePipelines()` it will now fail to start. The cost falls entirely on applications that opted into validation, which is a smaller set than "all applications" but is exactly the set that did the right thing. The remedy is to pick a conformant triple, and per C-18 many of these applications have never had to reason about lifetime at all — which is why NFR-10 makes the guidance page, not the message, the acceptance bar. It belongs in `release_notes.md` beside FR-20's break (AC-24).
- **An application that never calls `ValidatePipelines()` gets nothing.** It can opt into adoption, leave all three lifetimes `Transient`, adopt nothing at all (ADR 0072's `TransformerLifetime` veto) and receive no signal of any kind. C-15 records this and it is accepted; the mitigation is documentation ("call `ValidatePipelines()` last"), which is weaker than a mechanism.
- **A consumer host that never registers `ServiceActivatorHostedService` has no validation host at all.** `AddConsumers` sets `ConsumerOwnsValidation` and does not register the hosted service (D14), so however wrong the configuration, no FR-22 message is surfaced. Unchanged by this work and deliberately not fixed here.
- **Every rule that reads the collection sees only what was registered before the call, and this is the one place the rules can be wrong rather than merely silent.** The duplicate-provider and repeated-opt-in rules miss a registration made after `ValidatePipelines()` — and a provider registered after it is the one Microsoft's container will actually resolve. The captive-dependency rule reads the same snapshot for *both* of its inputs, so a mapper registered later is not a candidate at all, and a later `AddScoped<IOrderDbContext>()` makes a real captive dependency invisible. Worse, because the rule reads the **last** descriptor for a service type to match Microsoft's resolution, a later registration that changes a type's effective lifetime can make the snapshot disagree with the built container in either direction: a warning raised about a dependency that is no longer `Scoped`, or withheld about one that now is. This is C-15's snapshot semantics, inherited rather than introduced, and the only mitigation is the same one: call `ValidatePipelines()` last.
- **The captive-dependency rule is bounded in four ways, all deliberate and all reportable as misses.** It uses Brighter's constructor selection rather than Microsoft's; it reads direct parameters only, so transitive captivity is not reported; the `Paramore.Brighter.` assembly **prefix** over-excludes, so an application type in an assembly the application named `Paramore.Brighter.Something` is excluded too; and no mapper can be excluded by the mechanism at all, because the mechanism keys off attributes and no mapper is returned by one. Each is asserted as intended by AC-42, so none can be quietly "improved". The container's own `ValidateScopes` remains the complete check, and FR-25.8 requires the guidance page to say so.
- **`IAmAPipelineValidator` no longer resolves to `PipelineValidator`.** A test or application that resolves it and casts to the concrete type breaks. Nothing in `src` does, but it is a behavioural change in what the container returns.
- **A reflection failure in a warning rule can block startup.** `Specification<T>` converts an uncaught rule-body exception into a `ValidationSeverity.Error`, so a `TypeLoadException` while reading a constructor's parameter types would fail a host under `throwOnError: true` on account of a rule whose own severity is Warning. This is the behaviour of every existing rule (ADR 0064) and the rules deliberately add no bespoke guard; the exposure is bounded by the artefact types already being materialised in a `ServiceDescriptor`, and therefore already loaded.
- **Nine new types in the DI package plus one in core.** They are internal apart from the validator, and each corresponds to a responsibility an Acceptance Criterion tests separately — but it is real surface area for five rules, and the alternative of two or three larger objects would have been defensible.
- **Startup cost grows with the artefact count, and every validating host pays the fixed part.** The `Describe()` pass for the exclusion set and the `MessageMapperRegistry` it needs are built in the factory delegate, so they happen in every host that calls `ValidatePipelines()` — including the common one that has no `Singleton`-governed artefact at all and where FR-22.3 will find nothing to exclude. Deferring the pass until a `Singleton` candidate is actually found would avoid that, at the cost of the single-instant property the decorator's inputs otherwise have; the fixed cost was taken instead. On top of it: one constructor walk per `Singleton`-governed artefact and one dictionary lookup per parameter, bounded by registrations, run once, only when validation is enabled — but it forces the load of every artefact's constructor parameter types.
- **The migration cost, and who pays it.** An application that does not validate pays nothing. An application that validates and has a uniform triple pays nothing. An application that validates and mixes `Transient` with `Scoped` pays a failed startup and a joint lifetime decision it has not had to make before, and `docs/guides/lifetimes-and-scoping.md` is the whole of what it gets to make it with.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| The inner `PipelineValidator` is never disposed, so its lazily built `MessageMapperRegistry` and the mapper factory's DI scope live to process exit | The decorator implements `IDisposable` and cascades. This is stated in its contract rather than left implicit, because the container tracks only the instance the factory returns |
| A second `MessageMapperRegistry` is built for the exclusion set and leaks, reproducing the row above one line down | There is one registry per validation run and the DI package owns it: the delegate builds the `Lazy` and passes `() => registry.Value` into `PipelineValidator`'s existing factory parameter, so the inner validator cannot construct another (step 5a). Double disposal is safe by `MessageMapperRegistry`'s own `Interlocked` guard (`:360-362`) |
| Validation reads a different configuration from the one the factories honour, passing a broken host or failing a working one | The one input is `IBrighterOptions`, resolved from the built container — the same object, by the same route, that `ServiceProviderMapperFactory.cs:44` reads. `IOptions<BrighterOptions>.Value` is never read. AC-45 pins that object's affinity on all four paths, which is the property this relies on; **no AC asserts the validator's input source directly**, and the ACs that exercise the rules each use one path, so this mitigation rests on the argument above rather than on a test |
| A future rule is added to core "just this once" because it is convenient, and MEDI's transitive presence lets it compile | AC-22.3's source scan is the guard and returns zero today. The csproj check (AC-22 clause 2) cannot catch it and must not be relied on |
| The captive-dependency rule warns against a Brighter type and is turned off wholesale | The exclusion is a mechanism, not a list, and covers both attribute families. AC-42 pins `ClaimCheckTransformer` (the in-core, dependency-taking case) and a `Paramore.Brighter.Extensions.Tests` transform (the prefix case) |
| The exclusion is implemented as "assembly prefix" alone, silently excluding application mappers in `Paramore.Brighter.*` assemblies | AC-42's paired same-assembly cases — transform excluded, mapper reported — are the only construction that distinguishes the conjunction from the prefix alone, and they fail an implementation that drops the attribute half |
| FR-22.2's message tells a user their triple is wrong but not what a right one looks like | The message lists all three values and names the guidance page; AC-43 asserts the literal path in all five messages and AC-44 walks each message to a concrete triple |
| A duplicate provider is reported but the effective one is not identified, so the remedy is guesswork | The message names the last-registered as effective, matching Microsoft's resolution of the service type, which ADR 0072's plain `AddSingleton` makes both true and observable |
| The consumer host silently validates nothing | D14 is stated as an accepted gap in this ADR, in FR-25.10's guidance, and in AC-40, which registers `ServiceActivatorHostedService` explicitly rather than assuming it |

## Alternatives Considered

**1. A second `IAmAPipelineValidator` contributed by the DI package.** Register the container rules as their own validator with a plain `AddSingleton`, alongside the core one. **Rejected on mechanism first and meaning second.** `IAmAPipelineValidator` is registered `TryAddSingleton` (`BrighterPipelineValidationExtensions.cs:71`) and both hosts consume exactly one — `BrighterValidationHostedService` by constructor parameter (`:60`), `ServiceActivatorHostedService` by `GetService` (`:50`) — so two registrations do not compose; Microsoft's container resolves the last and the core validator's findings vanish. Making it compose means changing both hosted services to `GetServices<IAmAPipelineValidator>()` and `PipelineValidationResult.Combine`, in two packages, one of which (`ServiceActivatorHostedService`) is registered nowhere in `src`, so the consumer half of the change would be exercised only where a test registers the hosting package. It also changes what the `TryAddSingleton` escape hatch means: an application that supplies its own validator today replaces Brighter's validation entirely, and under `GetServices` it would additively receive Brighter's as well. The decorator gets the same composition with one registration and no host changes.

**2. A validation spec the core validator consumes.** Widen `PipelineValidator`'s constructor with a new family, as ADR 0064 did for the producer rules. **Rejected.** The entity type carries `ServiceLifetime`, so it lives either in core — which AC-22.3 forbids — or in the DI package, which would put a DI-package type on a core constructor and require core to reference the package.

Two container-free shapes remain, and the first is the precedent this ADR leans on elsewhere, so it has to be met rather than skipped. **A named question interface, core-defined and implemented in the DI package** — the shape of ADR 0064's `IAmATransformerResolvabilityProbe` (`src/Paramore.Brighter/Validation/IAmATransformerResolvabilityProbe.cs`, implemented by `ServiceCollectionTransformerResolvabilityProbe`). A lifetime analogue would be neither opaque nor a delegate bag, and core could name and test its rules. It fails on what the messages have to say, not on shape: FR-22.1 and FR-22.2 require messages listing **all three lifetimes with their values**, so a `bool`-answering probe cannot supply the finding's own content, and an interface that returns the three values has to name a type for them — which is `ServiceLifetime`, or a core mirror of it, which is alternative 3 and is rejected there and for stronger reasons. The probe precedent holds exactly where ADR 0064 used it, for a yes/no question whose message needs nothing back.

The second is opaque: an `IEnumerable<Func<IEnumerable<ValidationError>>>` of pre-bound rules the validator merely invokes. That is genuinely tempting and it is the decorator with the collaboration hidden inside a closure: the same objects, the same order, one fewer type — but `PipelineValidator` stops being able to say what it evaluates, and its rules stop being ones core can name and test. The DI rules would then be constructed as delegates closing over `IBrighterOptions` and a snapshot inside the factory delegate at `:71`, which is where the decorator is constructed anyway, minus the ability to unit-test the rule set without building a `PipelineBuilder`. The decorator keeps each validator honest about what it evaluates.

**3. Put the rules in core and pass the lifetimes as core-typed values.** An `int` or a core enum mirroring `ServiceLifetime`, and the descriptors reduced to core-typed pairs. **Rejected by ADR 0014 and AC-22.3** — and rejected *more* firmly than a direct violation would be, which is the point worth recording. A mirror enum would pass the source scan, because the scan names four identifiers and not a concept. Core would then have to define what `Scoped` means with no container to define it against, and the seam would stop being implementable over Autofac or SimpleInjector on their own terms: NFR-7 asks that another container package express its own lifetimes, not that it translate them into Brighter's. Passing the scan is not the requirement; ADR 0014 is.

**4. A Roslyn analyzer instead of startup validation.** `Paramore.Brighter.Analyzer` exists and ADR 0054 (`roslyn-analyzer-extensions-for-pipeline-validation`, Proposed) already extends it with pipeline diagnostics. **Rejected on the concrete ground that three of the five inputs are not statically visible.** The three lifetimes and `DefaultScopeAffinity` are values assigned at run time, and per D18 the affinity may be written by an extension in a package the analyzer never sees; the `IAmAScopeProvider` descriptor list is the result of executing registration code; and FR-22.3 reads the registration lifetime of a parameter that may have been registered by any of a dozen `AddScoped` overloads inside a third-party library's own `AddX()` extension method. An analyzer could catch a literal `MapperLifetime = ServiceLifetime.Scoped` beside a literal `HandlerLifetime = ServiceLifetime.Transient` in one method body and nothing else — a fraction of FR-22.2 and none of FR-22.1, FR-22.3, FR-24.3 or FR-17. It is complementary, not alternative, and this ADR does not preclude it.

**5. Validate eagerly at `AddBrighter` time rather than in `ValidatePipelines()`.** **Rejected on three counts.** The affinity is not final at `AddBrighter` time — per D18 the extension's write lands after every application options delegate, and on three of the four paths there is no `IOptions` pipeline at all, so the value only exists once `IBrighterOptions` is resolved. The service collection is still being populated, so FR-24.3 would see a partial provider list and FR-22.3 a partial artefact set — precisely the staleness C-15 documents, made mandatory instead of opt-in. And it would fail startup for applications that never asked to be validated, which is a far larger break than C-18's and would contradict the `ValidatePipelines(enabled, throwOnError)` contract at `BrighterPipelineValidationExtensions.cs:58`.

**6. Resolve the artefacts and inspect the instances.** Ask the container for each `Singleton` artefact and look at what it got. **Rejected on four counts, any one sufficient.** It runs application constructors at startup, which is exactly what `ValidatePipelines()` exists to avoid — ADR 0064 rejected the same shortcut for the transformer probe and introduced `IAmATransformerResolvabilityProbe` instead. It throws in precisely the configuration the rule exists to warn about: resolving a `Singleton` from the root provider with a `Scoped` dependency raises `InvalidOperationException` under `ValidateScopes`, converting a warning into a startup failure. It cannot see a constructor Microsoft's container would not select, which D15's tie case requires. And AC-42's final clause forbids it outright — the two-equal-constructor mapper is not activatable at all, and the AC asserts validation output while stating that the mapper must not be resolved.

**7. A separate hosted service for the container rules.** Leave the core validator alone and add a `BrighterScopeValidationHostedService`. **Rejected.** It would have to reproduce the `ConsumerOwnsValidation` dance in both directions, because `BrighterValidationHostedService` is a no-op in consumer hosts and `ServiceActivatorHostedService` is registered by the application; it introduces an ordering question between two validation hosts that does not exist today; and it would report FR-22's errors through a second `PipelineValidationException`, so a host with findings from both would fail on whichever ran first. One validator, one result, one throw.

**8. Do nothing — document the five conditions instead of validating them.** **Rejected by FR-22 and D5**: an inert opt-in must be *validated*, never inferred and never silently ignored. It is the honest alternative, and it is weaker than it sounds — under ADR 0072 a single `Transient` participant vetoes adoption for the whole pipeline, and all three lifetimes default to `Transient`, so the most likely outcome of a partial opt-in is software that works, adopts nothing, and says nothing. Validation is the only place that silence is broken.

## References

- Requirements: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md) — FR-17, FR-20, FR-21, FR-22, FR-24.3, FR-25 (all eleven clauses; the clause-to-ADR map is in `Scope`); NFR-1, NFR-7, NFR-8, NFR-9, NFR-10; C-4, C-11, C-12, C-12a, C-15, C-16, C-18, C-20; D5, D8, D9, D11, D14, D15, D18, D19; AC-22, AC-24, AC-27, AC-28, AC-32, AC-40, AC-41, AC-42, AC-43, AC-44, AC-45, AC-49
- Related ADRs (cited by slug — ADR numbers are not unique in this repo, C-16):
  - `0053-pipeline-validation-at-startup` [Accepted] — the `ISpecification<T>` rule families, `ValidationResultCollector<T>`, `ValidationError`, and the `throwOnError` semantics this ADR reuses without change
  - `0064-validate-pipeline-assembly-and-provider-registration` [Accepted] — the precedent for a `Warning` rule family, for reading the `IServiceCollection` without resolving it (`IAmATransformerResolvabilityProbe`), for threading new inputs through `ValidatePipelines()`, and for rules that do not catch their own exceptions
  - `0072-ambient-scope-adoption-seam` [Proposed] — the plain `AddSingleton` registration model that makes FR-24.3's duplicate detectable and the effective provider predictable; and `AmbientScopeDiagnostics`, the run-time latches this ADR does **not** own
  - `0076-scope-affinity-option-and-write-through` [Proposed] — the opt-in property `DefaultScopeAffinity` on `IBrighterOptions`/`BrighterOptions`, and the override singleton by which an opt-in extension's argument reaches the resolved options object
  - `0073-aspnet-core-request-scope-package` [Proposed] — the ASP.NET package and the `AddBrighterRequestScope` extension whose repeated call this ADR's FR-17 rule reports on, and whose provider registration FR-24.3's rule reads
  - `0070-per-pipeline-di-scope-for-mapper-and-transform-factories` [Proposed] and `0071-pipeline-scope-handle-for-handler-pipelines` [Proposed] — the seam whose configuration these rules validate
  - `0067-per-resolution-di-scope-for-transient-factory-instances` [Accepted] — its `Terms` block defines the configured-lifetime and registration-lifetime axes FR-22.3 reads on opposite sides, and which this ADR references rather than restates
  - `0014-di-friendly-framework` [Accepted] — per-family factory interfaces rather than an IoC abstraction; the durable reason the rules cannot live in core
  - `0054-roslyn-analyzer-extensions-for-pipeline-validation` [Proposed] — the compile-time counterpart, and why it cannot substitute here
- External references:
  - [Dependency injection guidelines — scope validation and captive dependencies](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
  - Wirfs-Brock & McKean, *Object Design: Roles, Responsibilities, and Collaborations* — the knowing / doing / deciding vocabulary used in the roles table
