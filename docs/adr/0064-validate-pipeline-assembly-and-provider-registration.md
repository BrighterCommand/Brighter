# 64. Validate Pipeline Assembly Scanning and Validation-Provider Registration

Date: 2026-06-16

## Status

Accepted

## Context

**Parent Requirement**: [specs/0035-validate-pipeline-assembly-and-provider-registration/requirements.md](../../specs/0035-validate-pipeline-assembly-and-provider-registration/requirements.md) (FR-1..12, NFR-1..6, C-1..12, OOS-1..8, AC-1..12).

**Scope**: This is the single ADR for the whole feature. It covers **both** new checks — (A) missing-transform detection and (B) missing validation-provider detection — as one focused architectural decision, mirroring [ADR 0053](0053-pipeline-validation-at-startup.md), which covered the entire `ValidatePipelines()` feature in one record. It builds directly on two prior ADRs and assumes their vocabulary:

- **ADR 0053** established the startup-validation architecture: `ISpecification<T>` rule families (`HandlerPipelineValidationRules` over `HandlerPipelineDescription`, `ProducerValidationRules` over `Publication`, `ConsumerValidationRules` over `Subscription`), evaluated by `PipelineValidator` and collected via the `ValidationResultCollector<T>` visitor; findings as `ValidationError(ValidationSeverity, Source, Message)` surfaced through `PipelineValidationResult.Errors`/`.Warnings`; logging and `throwOnError` semantics owned by `BrighterValidationHostedService`; the reflection-only `PipelineBuilder.Describe()` dry-run (no handler instantiation); and the §7 `RequestHandlerAttribute.GetHandlerType()` / marker-interface pattern for classifying pipeline steps "rather than hardcoding a list of known Brighter attribute types".
- **ADR 0063** established the request-validation feature: `[ValidateRequest]` / `[ValidateRequestAsync]` whose `GetHandlerType()` targets the abstract open generics `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>`; provider packages whose `UseFluentValidation()` / `UseDataAnnotations()` / `UseSpecification()` map those open generics to concrete handlers in the `IServiceCollection`.

### The Problem

A developer using assembly scanning (`AutoFromAssemblies()`) has two silent failure modes that surface only when a message is actually produced/consumed in production:

1. A `Publication`/`Subscription` declares a message transform (e.g. `[Compress]` / `[Decompress]`) whose transformer type was never registered — a strong signal that the transformer's assembly was not scanned (the issue's `JustSayingCompressionTransform` example). At runtime this throws `ConfigurationException` from `TransformPipelineBuilder.BuildWrapPipeline`/`BuildUnwrapPipeline`, but only when a message of that type is sent/received.
2. A handler is annotated with `[ValidateRequest]`/`[ValidateRequestAsync]` but no validation provider was registered. At runtime the abstract `ValidateRequestHandler<>` cannot be resolved, again deferred until that request flows through the pipeline.

`ValidatePipelines()` already detects the consumer "no handler registered" symptom (`ConsumerValidationRules.HandlerRegistered`, **Error**) and cannot reliably detect "missing mapper" (the DI path always registers a default `JsonMessageMapper<>`, C-9 / OOS-7). The remaining reliably-detectable signals are **(A)** the unresolvable *transformer* and **(B)** the missing validation *provider*.

### What Needs a Decision

The detection mechanisms are largely a matter of reuse, but three HOWs are genuinely open and are the subject of this ADR:

- **(A) async-aware describe (C-5, FR-5).** The existing `TransformPipelineBuilder.DescribeTransforms(MessageMapperRegistry, Type)` consults only the sync `ResolveMapperInfo` and `MapperMethodDiscovery.FindMapToMessage`/`FindMapToRequest`. FR-5 requires the **union** of the sync- and async-resolved mappers, so a transform declared only on the async mapper is evaluated when only the async mapper resolves. The describe path must be extended.
- **(A)/(B) resolvability without instantiation (C-7, C-11).** "Transformer resolvable?" and "provider registered?" must be observed **without building the pipeline** and **without instantiating** the transformer/handler — because the build path *throws* exactly when the thing is missing.
- **Plumbing (C-12).** `ValidatePipelines()` today constructs `PipelineValidator` with only the pipeline builder, publications, subscriptions, and consumer specs — it threads neither a `MessageMapperRegistry` nor a transformer-resolvability source. `ProducerValidationRules` methods are static/parameterless and the producer spec list in `PipelineValidator.ValidateProducers` is hard-coded. New dependencies must reach the producer rule (core) and consumer rule (ServiceActivator).

### Forces and Constraints

- **C-2 — Severity is `Warning`, non-blocking even under `throwOnError: true`.** This is a *deliberate* deviation from the existing precedent, where a guaranteed build-time `ConfigurationException` is an **Error** (`HandlerPipelineValidationRules.AttributeAsyncConsistency`, `ConsumerValidationRules.HandlerRegistered`). It is recorded here as a revisitable decision, justified in the Decision section.
- **C-1 — `ValidatePipelines()` only runs where it is enabled** (typically dev/test). Running it forces the very assemblies it checks to load. Accepted; the warning is valuable where it runs (OOS-6).
- **C-8 — Placement.** (A)-producer rule in core `Paramore.Brighter/Validation/ProducerValidationRules.cs`; (A)-consumer rule in `Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` (consumer concerns must not leak into core). (B) must not depend on ServiceActivator.
- **C-10 — (A) is honestly narrowed.** (A) reflects transforms off the *resolved* mapper; if a custom mapper's own assembly is unscanned, resolution silently falls back to the default mapper and the custom transforms are never seen — so (A) does not warn in that case. (A) catches only "mapper IS scanned, but the transformer it declares is NOT resolvable".
- **NFR-1/-2, OOS-4 — additive only.** New rules must reuse the `ISpecification<T>` + `ValidationResultCollector` pattern and must not change the severity, message, or blocking behaviour of any existing rule, nor the `ValidatePipelines(enabled = true, throwOnError = true)` defaults.

The central tension is the **deferred-throw vs. severity** mismatch: the underlying condition is deterministic (a missing registration), which by the 0053 precedent would be an Error, yet C-2 fixes it as a non-blocking Warning. This ADR adopts and justifies that, rather than re-opening it.

## Decision

We add **two new `ISpecification<T>` rule families** that plug into the existing `PipelineValidator` exactly like the 0053 rules. All findings are `ValidationError` records at `ValidationSeverity.Warning`, surfaced through `PipelineValidationResult.Warnings` and logged by `BrighterValidationHostedService` (which already logs warnings unconditionally and never blocks on them). No new hosted service, no change to `throwOnError`, no new exception type.

### Architecture Overview

```
                         IBrighterBuilder.ValidatePipelines(enabled, throwOnError)
                         (BrighterPipelineValidationExtensions)
                              │
        builds PipelineValidator, now also threading:
        ┌──────────────────────────────────────────────────────────┐
        │  • MessageMapperRegistry  (reuse DescribePipelines path)   │
        │  • IAmATransformerResolvabilityProbe  (NEW role, see below)│
        │  • ValidationProviderRegistrations (IServiceCollection scan)│
        └──────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴───────────────────────────────┐
                    ▼                                          ▼
            PipelineValidator.Validate()
   ┌───────────────────┬───────────────────────┬──────────────────────┐
   ▼                   ▼                       ▼                      ▼
 Handler            Producer                Consumer              Handler
 pipelines          (Publication)           (Subscription)        pipelines
 (existing)         ┌────────────────┐      ┌────────────────┐    (existing Describe())
                    │ existing rules │      │ existing rules │           │
                    │ + (A) NEW:     │      │ + (A) NEW:     │     + (B) NEW:
                    │  WrapTransform │      │  UnwrapTransform│     ValidationProvider
                    │  Resolvable    │      │  Resolvable    │     Registered
                    └───────┬────────┘      └───────┬────────┘     └──────┬──────┘
                            │ DescribeTransforms     │ DescribeTransforms  │ Describe() steps
                            │  (sync∪async union)    │  (sync∪async union) │  HandlerType ==
                            ▼                        ▼                     │  ValidateRequestHandler<>
                    IAmATransformerResolvabilityProbe.Resolves(type)      │  AND !providerRegistered
                            │  (non-instantiating)   │                    ▼
                            ▼                        ▼              Warning naming request +
                    Warning (FR-1)            Warning (FR-2)        UseFluentValidation()/…
                            └────────────────────────┴─────────────────────┘
                                              ▼
                              PipelineValidationResult.Warnings
                                              ▼
                          BrighterValidationHostedService
                          logs at LogLevel.Warning — NEVER blocks (C-2, FR-10)
```

### Key Components and Roles (Responsibility-Driven Design)

#### 1. `IAmATransformerResolvabilityProbe` — service provider role (knowing) — NEW interface

**Responsibility**: *knowing* whether a transformer type can be resolved, without instantiating it and without building a pipeline.

```csharp
// In Paramore.Brighter (core)
public interface IAmATransformerResolvabilityProbe
{
    /// <summary>True when the given transformer type is registered/resolvable. Never instantiates it.</summary>
    bool Resolves(Type transformerType);
}
```

Contract: input a transformer `Type` (the `GetHandlerType()` result, e.g. `CompressPayloadTransformer`); output `bool`; never throws for an unregistered type; never constructs the transformer.

**Why a new interface (RDD: "do not add new types without necessity" — and why it is necessary here).** The grounded options were weighed:

- Reusing `IAmAMessageTransformerFactory.Create(Type)` is rejected: `ServiceProviderTransformerFactory.Create` delegates to `ServiceProviderLifetimeScope.GetOrCreate`, which calls `IServiceProvider.GetService(objectType)` and **instantiates** (and caches via `Lazy` for singletons). That violates C-11's non-instantiating intent and FR-6's "rendering the message does not require the transformer to be registered or instantiated".
- A bare `IServiceProvider.GetService(transformerType)` probe (the example C-11 itself offers) is acceptable for *unregistered* types (it returns `null` cleanly), but for *registered* transient transforms it instantiates one. The truly non-instantiating, deterministic source is the `IServiceCollection` registration: transforms register as `_services.TryAdd(new ServiceDescriptor(transform, transform, ServiceLifetime.Transient))` (verified in `ServiceCollectionTransformerRegistry.Add`), so resolvability is exactly "a descriptor exists whose `ServiceType == transformerType`".

A small role interface lets the **DI layer** own the registration-scan implementation (it has the `IServiceCollection`/`IServiceProvider`) while the **core producer rule** depends only on the abstract role — keeping core free of any DI dependency (mirrors how `IAmASubscriberRegistryInspector` decouples the core describe path from the DI subscriber registry). The default implementation lives in `Paramore.Brighter.Extensions.DependencyInjection`, captures the `IServiceCollection` (or a frozen snapshot of registered transformer types) at `ValidatePipelines()` registration time, and answers `Resolves` by membership test. The probe carries the **identity** to report as the `TransformType` (= `GetHandlerType()`), per FR-5.

#### 2. (A)-producer spec — `ProducerValidationRules.WrapTransformResolvable(...)` — NEW (core, C-8)

**Responsibility**: *deciding* whether each declared wrap transform on a publication's resolved mapper(s) is resolvable.

It is an `ISpecification<Publication>` built with the existing **collapsed** `Specification<T>(Func<T, IEnumerable<ValidationResult>>)` constructor (one finding per missing transform, FR-3/FR-12). For each `Publication` it asks the mapper registry to describe the union of sync- and async-resolved mappers' `WrapTransforms`. Publications whose request type resolves to the **default mapper** (`TransformPipelineDescription.IsDefaultMapper`) are **skipped** — the default mapper (`JsonMessageMapper<>`) and its `[CloudEvents]` transform are Brighter built-ins, out of scope for (A) (FR-4/NFR-5); checking them would false-positive in configurations that use the default mapper without auto-scanning Brighter's own transforms. For each remaining `TransformStepDescription` whose `TransformType` the probe reports as **not** resolving, it yields:

```
ValidationError(
  ValidationSeverity.Warning,
  $"Publication '{p.Topic}'",
  $"Request '{p.RequestType.Name}' declares wrap transform '{step.TransformType.Name}' on topic " +
  $"'{p.Topic}' — that transformer is not registered. Is its assembly included in AutoFromAssemblies()?")
```

This satisfies FR-1 (contains request type, transformer type, `Topic`, `AutoFromAssemblies` prompt) and AC-1.

Because `ProducerValidationRules` methods are static today and need new dependencies, this method takes them as parameters (mirroring `ConsumerValidationRules.HandlerRegistered(inspector)`):

```csharp
public static ISpecification<Publication> WrapTransformResolvable(
    MessageMapperRegistry mapperRegistry,
    IAmATransformerResolvabilityProbe probe);
```

#### 3. (A)-consumer spec — `ConsumerValidationRules.UnwrapTransformResolvable(...)` — NEW (ServiceActivator, C-8)

Symmetric `ISpecification<Subscription>` over `UnwrapTransforms`, keyed by `Subscription.RequestType`. Verified: `Subscription.RequestType` is `Type?` — when `null` (a datatype-channel subscription using `MapRequestType` per message) the spec **skips** the entity (vacuous pass), matching the existing convention in `PumpHandlerMatch`/`HandlerRegistered` and FR-2. The message names `Subscription.Name` instead of `Topic` (FR-2, AC-2). Signature mirrors the existing consumer rules:

```csharp
public static ISpecification<Subscription> UnwrapTransformResolvable(
    MessageMapperRegistry mapperRegistry,
    IAmATransformerResolvabilityProbe probe);
```

#### 3a. Error handling — the rules follow the framework pattern (no inline guard)

Consistent with **every** existing validation rule, the (A) rules do **not** catch exceptions themselves. The `Specification<T>` framework already wraps rule evaluation in a `try`/`catch` (`EvaluateCollapsed`/`EvaluateSimple`, message "Rule evaluation failed: …") and reports any rule-body exception as a `ValidationSeverity.Error`. `HandlerPipelineValidationRules` and the existing producer/consumer rules rely on this; the new rules do the same — no bespoke try/catch.

The severity split is therefore by *category*, exactly as for all rules:
- A detected misconfiguration the rule is designed to find (a declared transform whose transformer is not resolvable) is reported as the intended **Warning** (non-blocking — C-2/FR-10/AC-9).
- A failure of the inspection *itself* (a genuinely exceptional reflection/probe error) surfaces as the framework's **Error**, exactly as it does for every other rule. In practice this does not arise for valid configurations: the default probe is a membership test that never throws, and `DescribeTransforms` is pure reflection over the resolved mapper.

> An earlier draft of this ADR added a per-rule `try`/`catch` to downgrade such exceptions to Warning. It was **removed**: it deviated from the solution's established rule pattern (rules must not catch; the framework reports evaluation errors). Respecting the existing pattern takes precedence over the bespoke guard.

#### 4. (B) spec — `HandlerPipelineValidationRules.ValidationProviderRegistered(ValidationProviderRegistrations)` — NEW (core)

**Responsibility**: *deciding* whether a handler that declares a validation pipeline step has a provider behind it.

This is the key simplification: (B) reuses the **already reflection-only** `PipelineBuilder.Describe()` (verified: it never calls the handler factory; it builds `PipelineStepDescription` from `attribute.GetHandlerType()`). For `[ValidateRequest]`/`[ValidateRequestAsync]`, `GetHandlerType()` returns the **open generics** `typeof(ValidateRequestHandler<>)` / `typeof(ValidateRequestHandlerAsync<>)` (verified), so `PipelineStepDescription.HandlerType` is directly comparable to those open generics — no instantiation, no pipeline build, satisfying C-7. (B) is therefore an `ISpecification<HandlerPipelineDescription>` (collapsed constructor), parameterised by a `ValidationProviderRegistrations` value (§5), that, for each step whose `HandlerType` equals `ValidateRequestHandler<>` (or the async open generic) while the corresponding flag (`.Sync`/`.Async`) is `false`, yields:

```
ValidationError(
  ValidationSeverity.Warning,
  $"Handler '{d.HandlerType.Name}'",
  $"Request '{d.RequestType.Name}' declares a validation step but no validation provider is " +
  "registered. Call UseFluentValidation(), UseDataAnnotations(), or UseSpecification().")
```

This satisfies FR-7, AC-6, and is provider-agnostic and detected via `GetHandlerType()` (C-4) rather than the concrete attribute type — so any future attribute targeting the validation handler participates automatically (OOS-2 scopes it to those two open generics only).

#### 5. `ValidationProviderRegistrations` — information holder (knowing)

**Responsibility**: *knowing* whether each validation open generic is mapped in the container.

Verified: every provider registers via `new ServiceDescriptor(typeof(ValidateRequestHandler<>), typeof(ConcreteHandler<>), ServiceLifetime.Transient)` (FluentValidation, DataAnnotations, Specification builder extensions). So "provider registered" is computed once, at `ValidatePipelines()` registration time, from the `IServiceCollection`.

Rather than thread two bare positional `bool`s — which `design_principles.md` flags as primitive obsession and which invite transposition since both are `bool` — we carry the pair as a tiny self-describing value record (a `record` class, matching the codebase's convention — the solution uses `record` value holders throughout and has no `record struct`):

```csharp
// In Paramore.Brighter (core)
public record ValidationProviderRegistrations(bool Sync, bool Async);
```

It is computed once and passed as a single argument:

```csharp
var providers = new ValidationProviderRegistrations(
    Sync:  builder.Services.Any(d => d.ServiceType == typeof(ValidateRequestHandler<>)),
    Async: builder.Services.Any(d => d.ServiceType == typeof(ValidateRequestHandlerAsync<>)));
```

(FR-8/AC-7: when the relevant flag is `true` no warning fires; FR-9/AC-8: a handler with no validation step yields nothing regardless.) The record is a deliberate, minimal new type (justified against "do not add new types without necessity" by the transposition hazard of two same-typed positional bools and by revealing intent at the call site and in the `PipelineValidator` constructor).

### Implementation Approach

#### A-producer (core)

1. Extend the describe path for the **sync∪async union** (FR-5, C-5). Add an async-aware overload to the static `TransformPipelineBuilder.DescribeTransforms(MessageMapperRegistry, Type)` that also consults `ResolveAsyncMapperInfo` + `MapperMethodDiscovery.FindMapToMessageAsync`/`FindMapToRequestAsync` (both already exist, verified). This is the correct home: `DescribeTransforms` is already a static method on `TransformPipelineBuilder` taking the concrete `MessageMapperRegistry`, which implements both the sync and async registry interfaces and exposes both resolve methods — so the async describe needs nothing from the separate `TransformPipelineBuilderAsync` class (which has no `DescribeTransforms`). The overload itself unions the `WrapTransforms`/`UnwrapTransforms` from the sync- and async-resolved mapper descriptions, de-duplicated by `(TransformType, Step)` so a transform declared on both is reported once per entity (FR-12); the (A) producer and consumer rules both consume this overload (passing `includeAsync: true`) and iterate the resulting transforms.
2. Add `WrapTransformResolvable(mapperRegistry, probe)` to `ProducerValidationRules`. Both the producer rule and its consumer counterpart short-circuit (yield nothing) when the description is null or `IsDefaultMapper` is true, so the default mapper's built-in transforms are never checked (FR-4/NFR-5). The guard is applied to both rules symmetrically — for consistency and robustness — even though `JsonMessageMapper` (the default) declares only a wrap transform (`[CloudEvents]`) and no unwrap.
3. In `PipelineValidator.ValidateProducers`, the hard-coded producer spec array gains the new rule. Because the rule needs dependencies the current static list cannot supply, thread `MessageMapperRegistry?` and `IAmATransformerResolvabilityProbe?` into `PipelineValidator` (new optional ctor params) and append the (A) rule only when both are non-null — keeping the rule absent (and thus inert) for pure-CQRS/no-producer configurations and preserving existing behaviour when the new dependencies are not wired (NFR-2/-4).

#### A-consumer (ServiceActivator)

`UnwrapTransformResolvable(mapperRegistry, probe)` is added to `ConsumerValidationRules` and registered like the other consumer specs — appended to `RegisterConsumerValidationSpecs` in `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` as another `services.AddSingleton<ISpecification<Subscription>>(sp => …)` resolving `MessageMapperRegistry` and `IAmATransformerResolvabilityProbe` from `sp`. `ValidatePipelines()` already collects these via `sp.GetServices<ISpecification<Subscription>>()` (verified), so the consumer side needs **no** change to `PipelineValidator` plumbing — it rides the existing `consumerSpecs` channel.

#### B (core)

`ValidationProviderRegistered(ValidationProviderRegistrations)` is added to `HandlerPipelineValidationRules` and appended to the spec array in `PipelineValidator.ValidateHandlerPipelines`. The `ValidationProviderRegistrations` value is computed in `ValidatePipelines()` from `builder.Services` and threaded into `PipelineValidator` (a new optional ctor param `ValidationProviderRegistrations? providerRegistrations = null` — because the type is a `record` class, `null` cannot be expressed as a non-null constant default, so `null` is treated as "no provider" `(false, false)`, and a configuration that never wires this stays inert). It consumes the existing `Describe()` output — zero new reflection.

#### Plumbing / threading (C-12)

`BrighterPipelineValidationExtensions.ValidatePipelines` is the single wiring point. It already shows the pattern to copy: `DescribePipelines` builds a `MessageMapperRegistry` via `ServiceCollectionExtensions.MessageMapperRegistry(sp)` when a `ServiceCollectionMessageMapperRegistryBuilder` is present (verified). `ValidatePipelines` adopts the same construction, registers the default `IAmATransformerResolvabilityProbe` (backed by an `IServiceCollection` snapshot of registered transformer types), computes the `ValidationProviderRegistrations` value from `builder.Services`, and passes mapper registry + probe + registrations into the `PipelineValidator` constructor.

**Why constructor-threading for producer/(B) but DI-injection for consumer (A).** This mirrors the *existing* split in `PipelineValidator`: handler and producer specs are already hard-coded arrays inside the validator, while only consumer specs arrive via injected `consumerSpecs` (`IEnumerable<ISpecification<Subscription>>`). The reason is assembly boundaries — consumer rules need `IAmASubscriberRegistryInspector`, which lives behind the ServiceActivator DI layer, so they must be composed there and injected; producer and handler-pipeline rules need only types core can reach (`MessageMapperRegistry`, the probe abstraction, the registrations value), so they are composed in core. The new dependencies are **appended** to the `PipelineValidator` constructor as trailing optional parameters, so existing positional callers (including the core tests that construct `PipelineValidator` directly) continue to compile unchanged. Net new wiring is confined to this one extension method plus the appended optional `PipelineValidator` parameters; the consumer (A) rule reaches the validator through the unchanged `ISpecification<Subscription>` DI channel.

### Per-constraint coverage

| Constraint | How honoured |
|---|---|
| C-2 (Warning, non-blocking) | All new findings are `ValidationSeverity.Warning`; `PipelineValidationResult.IsValid` ignores warnings; `BrighterValidationHostedService` logs them and never throws (FR-10, AC-9). Consistent with all rules, the (A) rules do not catch exceptions themselves; a genuine inspection exception surfaces as the framework's Error (rare — the default probe and reflection-only describe do not throw for valid configs) — see §3a. Recorded as revisitable below. |
| C-4 (B provider-agnostic, `GetHandlerType()`) | (B) compares `PipelineStepDescription.HandlerType` to the open generics, not to `[ValidateRequest]` concrete types. |
| C-5 (async describe extension) | New async-aware `DescribeTransforms` overload reusing existing `ResolveAsyncMapperInfo` + `FindMapTo*Async`. |
| C-7 (B without pipeline build) | Reuses reflection-only `Describe()`; provider presence read from `IServiceCollection`. |
| C-8 (placement) | (A)-producer in core `ProducerValidationRules`; (A)-consumer in ServiceActivator `ConsumerValidationRules`; (B) in core `HandlerPipelineValidationRules`. |
| C-11 (probe not build) | `IAmATransformerResolvabilityProbe` answers from registration membership; never builds wrap/unwrap pipeline, never instantiates the transformer. |
| C-12 (plumbing) | Mapper registry + probe + provider flags threaded through `ValidatePipelines()` into a widened `PipelineValidator` ctor; consumer rule rides existing DI spec channel. |

## Consequences

### Positive

- **Consistent and minimal.** Three new specs + one small role interface, all evaluated by the unchanged `PipelineValidator`/`ValidationResultCollector` machinery. Unit-testable in isolation (NFR-1).
- **(B) is nearly free.** It reuses the existing reflection-only `Describe()` and a one-line `IServiceCollection` scan — no new reflection, no pipeline build, no risk of throwing where a provider is missing (C-7).
- **No new failure surface.** Warnings are purely additive; `ConsumerValidationRules.HandlerRegistered` stays an Error, `throwOnError` semantics and defaults are untouched (FR-11, NFR-2, AC-11).
- **Actionable diagnostics where they matter most** — development/test — naming the request, entity (`Topic`/`Name`), transformer type, and the exact remedy (NFR-3).
- **Core stays DI-free.** The `IAmATransformerResolvabilityProbe` abstraction keeps the registration-scan implementation in the DI assembly, exactly like `IAmASubscriberRegistryInspector`.

### Negative

- **A new role type.** `IAmATransformerResolvabilityProbe` is one more interface to learn. We judged it necessary (see RDD rationale) over overloading `IAmAMessageTransformerFactory`, but it is real surface area.
- **Widened `PipelineValidator` constructor.** New optional parameters (mapper registry, probe, and a `ValidationProviderRegistrations` value). Existing callers/tests that construct `PipelineValidator` directly continue to compile (parameters are appended and optional, defaulting to inert), but the constructor is less terse.
- **One small new value type.** `ValidationProviderRegistrations` is a new (if tiny) public record introduced to avoid two positional bools — a deliberate trade of "fewer types" for "reveal intent / no transposition".
- **(A) describe-path cost.** Extending `DescribeTransforms` to the sync∪async union adds reflection over async map methods and a second `ResolveAsyncMapperInfo` lookup per entity. Bounded by entity count and run only at startup when validation is enabled (NFR-4), so negligible.
- **(A) is genuinely narrow (C-10).** It cannot warn when the custom *mapper's* own assembly is unscanned (resolution silently falls back to the default mapper). The honest framing must be communicated so developers do not over-trust a clean (A) result.

### Risks and Mitigations

- **Risk: the deferred-throw / severity mismatch (C-2).** The condition is deterministic, so the 0053 precedent argues for Error; we ship Warning. If a developer ignores the warning, the failure still bites in production where `ValidatePipelines()` is off (C-1/OOS-6). *Mitigation*: the warning is loud and names the precise remedy; the decision is explicitly recorded as **revisitable** with the maintainer — promoting (A)/(B) to Error (or a configurable severity) is a localized change (swap `ValidationSeverity.Warning` and let the existing Error/`throwOnError` path handle it) requiring no architectural rework.
- **Risk: an (A) rule-body exception surfaces as the framework's Error (blocking under `throwOnError`).** `Specification<T>` converts any uncaught rule-body exception to a `ValidationSeverity.Error`. This is the **same behaviour as every existing rule**, so it is accepted rather than worked around. *Mitigation*: keep the rule bodies exception-free for valid configurations — the default probe is a non-throwing membership test and `DescribeTransforms` is pure reflection over the resolved mapper. We deliberately do **not** add a bespoke per-rule guard, to respect the solution's rule pattern (rules don't catch) — see §3a.
- **Risk: false-positive transform warnings.** A transformer registered through a path other than `ServiceCollectionTransformerRegistry` (custom factory) could read as "not resolvable" by a registration scan. *Mitigation*: the probe is an interface — a custom resolvability implementation can be registered; and the default scans the same `IServiceCollection` Brighter itself registers transforms into. Severity is Warning, bounding the blast radius.
- **Risk: provider-flag staleness.** The flags are computed from `builder.Services` at `ValidatePipelines()` registration time; a provider registered *after* `ValidatePipelines()` in the fluent chain would be missed. *Mitigation*: same model the providers already assume (registration-time composition); document that `ValidatePipelines()` is called last in the chain — already the idiomatic position shown in ADR 0053's examples.

## Alternatives Considered

- **Severity = Error instead of Warning.** Rejected per C-2: the maintainer framed #4159 as a "warning"; `ValidatePipelines()` is opt-in/dev-test; and the runtime failure is deferred and conditional (only when a message of that type flows). A brand-new check should not escalate to a startup-blocker without maintainer sign-off. Recorded as revisitable rather than re-litigated here.
- **(B) by hardcoding `[ValidateRequest]`/`[ValidateRequestAsync]` concrete types.** Rejected per C-4 / ADR 0053 §7: detect by the `GetHandlerType()` target (`ValidateRequestHandler<>` / async) so any future attribute aimed at the validation handler participates automatically and the "step present" and "provider registered" vocabularies share one open generic.
- **Resolvability by building the transform pipeline (or calling the factory `Create`).** Rejected per C-11: `BuildWrapPipeline`/`BuildUnwrapPipeline` *throw* `ConfigurationException` precisely when the transformer is missing, and `ServiceProviderTransformerFactory.Create` instantiates. A non-instantiating registration lookup is required and is what the probe does.
- **Reuse `IAmAMessageTransformerFactory` rather than a new probe role.** Considered for type economy (RDD: avoid new types). Rejected because its `Create(Type)` instantiates and caches; a separate read-only "does it resolve?" responsibility is a distinct role and keeps core DI-free.
- **Mapper-missing / "default-mapper heads-up" detection.** Rejected per OOS-7 / C-9 / NFR-5: the DI path always registers a default `JsonMessageMapper<>`, so "no mapper" never occurs and a default-mapper notice would be noisy for apps that intentionally use the default. `DescribePipelines()` already surfaces which mapper is default. This is exactly why (A) targets *transforms* (C-10).
- **A separate ADR per check (A and B).** Rejected: both extend the same `ValidatePipelines()` machinery with the same `ISpecification<T>` + `ValidationResultCollector` pattern and the same Warning/non-blocking decision. ADR 0053 set the precedent of one ADR for the whole feature; splitting would fragment a single coherent decision.

## References

- **Requirements**: [specs/0035-validate-pipeline-assembly-and-provider-registration/requirements.md](../../specs/0035-validate-pipeline-assembly-and-provider-registration/requirements.md)
- **Related ADRs**: [ADR 0053 — Pipeline Validation and Diagnostic Report at Startup](0053-pipeline-validation-at-startup.md); [ADR 0063 — Request Validation Handler](0063-request-validation-handler.md)
- **External**: GitHub issue [#4159](https://github.com/BrighterCommand/Brighter/issues/4159) (the original "verify assemblies scanned" ask) and PR #4183 (the deferred "Ian #4" validation-provider item).
- **Grounded code references** (verified): `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs`; `src/Paramore.Brighter/Validation/{PipelineValidator,ProducerValidationRules,HandlerPipelineValidationRules,PipelineStepDescription,HandlerPipelineDescription,TransformPipelineDescription,TransformStepDescription,PipelineValidationResult}.cs`; `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`; `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` (`RegisterConsumerValidationSpecs`, line 197); `src/Paramore.Brighter/{TransformPipelineBuilder,MessageMapperRegistry,MapperMethodDiscovery,PipelineBuilder,Subscription}.cs`; `src/Paramore.Brighter/Transforms/Attributes/{CompressAttribute,DecompressAttribute}.cs`; `src/Paramore.Brighter/RequestValidation/Attributes/{ValidateRequestAttribute,ValidateRequestAsyncAttribute}.cs`; `src/Paramore.Brighter.Extensions.DependencyInjection/{ServiceProviderTransformerFactory,ServiceProviderLifetimeScope,ServiceCollectionTransformerRegistry}.cs`; `src/Paramore.Brighter.Validation.{FluentValidation,DataAnnotations,Specification}/*BuilderExtensions.cs`.
