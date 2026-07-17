# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4159

## Problem Statement

As a developer configuring Brighter with assembly scanning (`AutoFromAssemblies`), I would like `ValidatePipelines()` to warn me (1) when a publication or subscription declares a message transform whose implementing type is not registered — a strong signal its assembly was not scanned — and (2) when a request-validation attribute is present in a pipeline but no validation provider is registered, so that I can detect these misconfigurations during development or testing instead of discovering them as silent runtime failures in production.

### Scope note — what the existing machinery already covers (informed by ADR 0053)

The original #4159 ask was broad ("verify all required assemblies are scanned"). A discovery pass against the codebase narrowed it to what is both *new* and *reliably detectable*:

- **Missing request handler for a subscription is already detected.** `ConsumerValidationRules.HandlerRegistered` (in `Paramore.Brighter.ServiceActivator`) already emits an **Error** when a `Subscription`'s `RequestType` has no registered handler — the exact "assembly not scanned → handler missing" symptom for consumers. This feature does **not** re-implement it (see OOS-8).
- **Missing message mapper cannot be reliably detected** because Brighter's DI path always registers a default mapper (`JsonMessageMapper<>`); `MessageMapperRegistry.ResolveMapperInfo` falls back to it, so "no mapper registered" never occurs in practice. The real failure mode — a silent fallback to the default mapper when a custom mapper's assembly is unscanned — is indistinguishable from an intentional use of the default mapper, so warning on it would be noisy and is excluded (see OOS-7).
- **Transforms are the remaining detectable collaborator.** A transform declared on a mapper resolves to a concrete transformer type; if that type is not registered, it is a reliable, low-false-positive signal of an unscanned assembly (the issue's own `JustSayingCompressionTransform` example). This is net-new (no existing rule covers it).
- **No producer-side "no handler" check.** The producer (`Post`/publication) path has no analogue to the consumer handler-missing check, because the producing process need not host the request's handler at all — so "published type has no handler" is not a reliable misconfiguration signal and is not attempted.

### Background and terminology

- **AutoFromAssemblies**: A Brighter configuration call that performs reflection-based assembly scanning to discover and register handlers, message mappers, and transforms. It scans only assemblies already loaded into the AppDomain plus any explicitly passed. Dynamically/lazily-loaded assemblies are therefore not guaranteed to be scanned.
- **Resolved mapper**: For a given request type, the message-mapper type that Brighter resolves via `MessageMapperRegistry.ResolveMapperInfo` / `ResolveAsyncMapperInfo` — either a custom registered mapper or, failing that, the configured default mapper. The resolution returns the mapper `Type` and an `IsDefault` flag without instantiating it.
- **Transform**: A message transformation declared as an attribute on a mapper's map method — a `WrapWithAttribute` (e.g. `[Compress]`) for outgoing/**wrap** transforms on publications, or an `UnwrapWithAttribute` (e.g. `[Decompress]`) for incoming/**unwrap** transforms on subscriptions. Each transform attribute returns its implementing **transformer type** from `GetHandlerType()` (e.g. `[Compress]` and `[Decompress]` both return `CompressPayloadTransformer`).
- **Transform registered (resolvable)**: A transform is considered *registered* if the transformer type returned by its attribute's `GetHandlerType()` is resolvable from the transformer factory. If it is not resolvable, the transform is *missing*.
- **Validation provider** (provider): A registered concrete implementation backing the abstract request-validation handler. Each provider call (`UseFluentValidation()` / `UseDataAnnotations()` / `UseSpecification()`) maps the core open-generic abstract handlers `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>` (the targets of `[ValidateRequest]` / `[ValidateRequestAsync]`) to its concrete implementation in the service collection. A provider is considered *registered* if and only if the service collection contains a registration for the open-generic `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>`. Detection is provider-agnostic — it does not enumerate the provider assemblies by name.
- **Validation pipeline step**: A pipeline step on a registered handler whose `RequestHandlerAttribute.GetHandlerType()` resolves to the abstract `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>`. `[ValidateRequest]` / `[ValidateRequestAsync]` are the attributes that do this today. Detection keys off the `GetHandlerType()` target — consistent with ADR 0053 §7's marker / `GetHandlerType()` pattern (which was introduced "rather than hardcoding a list of known Brighter attribute types") — **not** off the concrete attribute type, so any future attribute that targets the validation handler participates automatically.
- **ValidatePipelines**: The opt-in feature (`ValidatePipelines(bool enabled = true, bool throwOnError = true)`) that runs at host startup and reports `ValidationError` findings, each with a `ValidationSeverity` (`Error` or `Warning`), a `Source`, and a `Message`. `Error` findings block startup when `throwOnError: true`; `Warning` findings never block startup and are always logged at `LogLevel.Warning`.

## Proposed Solution

Extend `ValidatePipelines()` with two additional, non-blocking checks reported at `Warning` severity:

- **Check (A) — Missing-transform detection.** For each `Publication`, inspect the resolved mapper's declared **wrap** transforms; for each `Subscription`, inspect the resolved mapper's declared **unwrap** transforms. When a declared transform's transformer type is not resolvable from the transformer factory, emit a warning naming the request type, the transformer type, and prompting an `AutoFromAssemblies()` check.
- **Check (B) — Missing validation-provider detection.** When a registered handler declares a validation pipeline step (an attribute whose `GetHandlerType()` targets `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>`, e.g. `[ValidateRequest]` / `[ValidateRequestAsync]`) but no validation provider is registered, emit a warning naming the request/handler and the three provider registration calls as the remedy.

## Requirements

### Functional Requirements

**FR-1 — Warn on a missing wrap-transform for a Publication.**
For each configured `Publication`, if the resolved mapper for its `RequestType` declares a wrap transform whose transformer type (the attribute's `GetHandlerType()` result) is not resolvable from the transformer factory, `ValidatePipelines()` MUST emit a `Warning` finding containing the request type name, the transformer type name, the publication `Topic`, and an `AutoFromAssemblies` prompt.
- Example: A `Publication` for `RequestType = GreetingMade` on topic `greeting`, whose resolved `GreetingMadeMessageMapper` declares `[Compress]` but `CompressPayloadTransformer` is not registered with the transformer factory, produces a warning whose message contains `GreetingMade`, `CompressPayloadTransformer`, `greeting`, and a phrase such as `Is its assembly included in AutoFromAssemblies()?`.

**FR-2 — Warn on a missing unwrap-transform for a Subscription.**
For each configured `Subscription`, if the resolved mapper for its `RequestType` declares an unwrap transform whose transformer type is not resolvable from the transformer factory, `ValidatePipelines()` MUST emit a `Warning` finding containing the request type name, the transformer type name, the subscription `Name`, and an `AutoFromAssemblies` prompt. A `Subscription` whose `RequestType` is null (e.g. a datatype-channel subscription that maps the type per-message via `MapRequestType`) is skipped by this check.
- Example: A `Subscription` with `RequestType = GreetingMade` and `Name = greeting-subscription`, whose resolved mapper declares `[Decompress]` but `CompressPayloadTransformer` is not registered, produces a warning whose message contains `GreetingMade`, `CompressPayloadTransformer`, `greeting-subscription`, and an `AutoFromAssemblies` prompt.

**FR-3 — Independent per-transform warnings.**
Each missing transform MUST be reported as its own warning; a resolvable transform MUST NOT suppress a warning about a different, missing transform on the same mapper.
- Example: A mapper declaring `[Compress]` (resolvable) and a second custom wrap transform (not resolvable) produces exactly one warning — for the unresolvable transform.

**FR-4 — No warning when all declared transforms resolve, none are declared, or the default mapper is used.**
For a `Publication`/`Subscription` whose resolved mapper declares no transforms, or declares only transforms whose transformer types all resolve, check (A) MUST NOT emit any warning for that entity. In particular, a publication/subscription whose request type resolves to the **default mapper** (the built-in `JsonMessageMapper<>`, which declares a `[CloudEvents]` wrap transform) MUST NOT produce a transform warning: the default mapper is a Brighter built-in and is **out of scope** for check (A), so its transforms are not checked — regardless of whether the transformer happens to be registered. (This keeps a configuration that relies on the default mapper from being warned about Brighter's own built-in transform — NFR-5.)
- Example: A `Publication` for `RequestType = FarewellMade` resolving to the default `JsonMessageMapper` (which declares a `[CloudEvents]` transform) produces zero (A) warnings — the default mapper's transforms are out of scope.

**FR-5 — Transforms reflected from each registered mapper type; identity is the transformer type.**
Transform attributes MUST be reflected from each mapper type that `ResolveMapperInfo` / `ResolveAsyncMapperInfo` resolves for the request (sync and/or async — the union, so a transform declared only on the async mapper is still evaluated when only the async mapper is resolved). The reported transform identity MUST be the transformer type name returned by the attribute's `GetHandlerType()` (e.g. `CompressPayloadTransformer`), not the attribute name.
- Example: With only an async mapper resolved for `GreetingMade` that declares `[Decompress]`, the transform is read from the async mapper type and reported as `CompressPayloadTransformer`.

**FR-6 — Message naming is derived from configured/declared information only.**
The (A) warning messages MUST be constructed from already-available information: the `Publication.RequestType` / `Subscription.RequestType`, the `Topic` / subscription `Name`, and the transformer type from `GetHandlerType()`. Because the transform attribute lives on the resolved (loaded) mapper and `GetHandlerType()` returns a compile-time type reference from that mapper's assembly, rendering the message does NOT require the transformer to be registered or its assembly to be otherwise loaded.

**FR-7 — Warn on a validation pipeline step present but no provider registered.**
If a registered handler declares a validation pipeline step (a `RequestHandlerAttribute` whose `GetHandlerType()` resolves to `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>` — as `[ValidateRequest]` / `[ValidateRequestAsync]` do) and no validation provider is registered, `ValidatePipelines()` MUST emit a `Warning` finding that identifies the affected request/handler and names `UseFluentValidation()`, `UseDataAnnotations()`, and `UseSpecification()` as the remedy.
- Example: A registered handler `GreetingMadeHandlerAsync` annotated with `[ValidateRequestAsync]` (whose `GetHandlerType()` is `ValidateRequestHandlerAsync<>`), with no provider registered, produces a warning naming the handler/request and the three provider calls.

**FR-8 — No (B) warning when a validation provider is registered.**
If a validation provider is registered (the open-generic abstract handler is mapped in the service collection), check (B) MUST NOT emit a warning for handlers declaring a validation pipeline step.
- Example: With `UseFluentValidation()` registered, a handler annotated with `[ValidateRequestAsync]` produces zero (B) warnings.

**FR-9 — No (B) warning when no validation pipeline step is present.**
Check (B) MUST NOT emit a warning for any handler that does not declare a validation pipeline step (no `RequestHandlerAttribute` whose `GetHandlerType()` targets `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>`), regardless of provider registration.
- Example: A handler with no validation pipeline step and no provider registered produces zero (B) warnings.

**FR-10 — Both checks are non-blocking regardless of `throwOnError`.**
All findings from checks (A) and (B) MUST be emitted at `ValidationSeverity.Warning` and MUST NOT cause `ValidatePipelines(throwOnError: true)` to throw or block host startup. Only pre-existing `Error`-severity rules may block under `throwOnError`.
- Example: A configuration with a missing transform (FR-1) AND a missing provider (FR-7), run with `ValidatePipelines(throwOnError: true)`, starts the host successfully while logging both warnings.

**FR-11 — Findings integrate with existing reporting, additively.**
Warnings from checks (A) and (B) MUST be produced as `ValidationError` records (severity `Warning`, populated `Source` and `Message`) and surface through the existing `PipelineValidationResult` warnings collection and existing logging path. They MUST be purely additive — they MUST NOT alter the severity, message, or blocking behavior of any existing rule (including `ConsumerValidationRules.HandlerRegistered`, which remains an `Error`), nor change `throwOnError` semantics.

**FR-12 — Deterministic, per-entity warnings.**
Check (A) MUST emit one warning per (configured entity, missing transform): a missing transform is reported against each `Publication`/`Subscription` that declares it, so each warning can name that entity's `Topic` / `Name`. The same transformer type missing on two different entities yields two warnings, each scoped to its entity (not duplicates — each names a different `Topic`/`Name`). The set of emitted warnings MUST be deterministic in content and ordering across runs of the same configuration: entities are processed publications-then-subscriptions in configuration order, and multiple missing transforms on a single mapper are ordered by their transform step. No cross-entity de-duplication is performed.

### Non-functional Requirements

- **NFR-1 — Consistency with existing rule architecture.** New checks MUST follow the established `ISpecification<T>` + `ValidationResultCollector` rule pattern used by `ProducerValidationRules` and `ConsumerValidationRules`, so the new rules are unit-testable in isolation.
- **NFR-2 — No behavioral change to existing rules.** Adding these checks MUST NOT alter the severity, messages, or blocking behavior of existing validation rules, nor change the default values of `ValidatePipelines(enabled = true, throwOnError = true)`.
- **NFR-3 — Actionable messages.** Each warning message MUST name the specific request type (and, for (A), the entity context and transformer type) and the concrete remedy (`AutoFromAssemblies(...)` for (A); the three provider calls for (B)).
- **NFR-4 — Opt-in and side-effect-light.** The checks run only when `ValidatePipelines()` is enabled; they MUST NOT introduce new startup work when it is disabled.
- **NFR-5 — Non-blocking and low-noise.** The new checks MUST be non-blocking (`Warning`) and MUST never prevent a host from starting, even though the underlying condition is deterministic (rationale in C-2). They MUST NOT fire on the normal, intentional use of the default mapper (mapper-missing is excluded — OOS-7). The detection itself is exact; the *message naming* is the only heuristic part (C-3).
- **NFR-6 — Determinism.** Repeated runs over the same configuration MUST produce the same warnings in the same order.

### Constraints and Assumptions

- **C-1 — Run-only-when-executed contradiction (explicit assumption, not a defect).** Running `ValidatePipelines()` forces loading of the assemblies it checks. These checks therefore only detect the problem in environments where `ValidatePipelines()` runs (typically development/test), not in production where it is commonly disabled. Accepted — the warning is valuable where it runs.
- **C-2 — Severity is `Warning` (deliberate, despite the deterministic condition).** Both new checks emit `Warning` and never block startup, even under `throwOnError: true`. This is a *deliberate* deviation from the existing severity precedent, under which a guaranteed build-time `ConfigurationException` is an `Error` (e.g. `HandlerPipelineValidationRules.AttributeAsyncConsistency`, `ConsumerValidationRules.HandlerRegistered`). We choose `Warning` because: (1) the issue author explicitly framed #4159 as a "warning"; (2) `ValidatePipelines()` is opt-in and commonly dev/test-only (C-1), so these are guidance diagnostics and a brand-new check should not escalate to a startup-blocker without maintainer sign-off; (3) the runtime failure is *deferred and conditional* — a missing transform/provider only throws when a message of that request type is actually produced/consumed, so a declared-but-unexercised pipeline never fails at startup. The *detection* is deterministic (not heuristic); only the message *naming* is heuristic (C-3). This decision is recorded as revisitable in the ADR / with the maintainer.
- **C-3 — (A) naming is heuristic from configured/declared information** (request type, `Topic`/`Name`, transformer type from `GetHandlerType()`), not a precise scanned-assembly-set comparison.
- **C-4 — (B) is validation-specific, provider-agnostic, and detected via `GetHandlerType()` (not hardcoded attribute types).** (B) detects a validation pipeline step by the abstract handler its attribute targets — `GetHandlerType()` → `ValidateRequestHandler<>` / `ValidateRequestHandlerAsync<>` — consistent with ADR 0053 §7's `GetHandlerType()` / marker-interface pattern, rather than enumerating the concrete `[ValidateRequest]`/`[ValidateRequestAsync]` types. The same open generic is what providers map, so "validation step present" and "provider registered" share one vocabulary. The remedy is the three provider calls; detection checks the open-generic registration's presence, not provider assembly names.
- **C-5 — Existing surfaces are reused (with one extension).** `Publication.RequestType`/`Topic`, `Subscription.RequestType`/`Name`, `MessageMapperRegistry.ResolveMapperInfo`/`ResolveAsyncMapperInfo` (mapper type + `IsDefault`, no instantiation), the transform-pipeline describe model (`TransformPipelineDescription.WrapTransforms`/`UnwrapTransforms`, each `TransformStepDescription` carrying the transformer `Type`), the transformer factory (transformer type → instance), and `IAmASubscriberRegistryInspector` (registered request/handler types) are the sources of truth. **Note:** the existing `TransformPipelineBuilder.DescribeTransforms` consults only the sync `ResolveMapperInfo`; evaluating an async-resolved mapper / the sync-async union required by FR-5 therefore requires extending the describe path (a HOW concern for the ADR, not pure reuse).
- **C-6 — Versioning.** Targets Brighter V10.X.
- **C-7 — (B) trigger condition is observable independent of pipeline resolution (feasibility assertion).** The presence of a validation pipeline step on a registered handler MUST be detectable by reflecting the handler's `RequestHandlerAttribute`s (on registered handler types via `IAmASubscriberRegistryInspector`) and inspecting each attribute's `GetHandlerType()` for the validation open generics, and "no provider registered" by inspecting the service collection for the open-generic `ValidateRequestHandler<>`/`ValidateRequestHandlerAsync<>` registration — NOT by building/describing the pipeline (which may be unbuildable precisely when no provider is registered). Detection covers handlers that appear in the subscriber registry; `AutoFromAssemblies`-discovered handlers (including command/event handlers invoked via `Send`/`Publish`, which `RegisterHandlersFromAssembly` adds to the subscriber registry) are in scope. Handlers registered through paths that bypass the subscriber registry are not enumerated.
- **C-8 — Architectural placement.** The publication (A) transform rule belongs in core `Paramore.Brighter/Validation/ProducerValidationRules.cs` (alongside the existing publication rules). The subscription (A) transform rule belongs in `Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` (alongside the existing subscription rules), because consumer concerns must not be added to core. (B)'s placement is an ADR concern but must not depend on ServiceActivator.
- **C-9 — Default-mapper rationale.** Brighter's DI path always configures a default mapper (`ServiceCollectionMessageMapperRegistryBuilder.DefaultMessageMapper = JsonMessageMapper<>`), so `ResolveMapperInfo` never returns "no mapper". This is why (A) targets transforms (declared on whichever mapper resolves) rather than mapper presence.
- **C-10 — (A) detects an unscanned *transformer*, not an unscanned *mapper* (honest narrowing).** (A) reflects transforms off the *resolved* mapper. If a custom mapper's own assembly is unscanned, resolution falls back to the default mapper and the custom mapper's transforms are never seen — so (A) does NOT warn in that case. (A) therefore catches only the narrower, reliably-detectable half of #4159: "the custom mapper IS scanned/registered, but the transformer type it declares is NOT resolvable (its assembly/registration is missing)" — e.g. `JustSayingMessageMapper` is scanned but `JustSayingCompressionTransform` is not. The unscanned-mapper-assembly half is undetectable for the same reason as OOS-7.
- **C-11 — (A) "resolvable" is a non-instantiating lookup, NOT a pipeline build (feasibility assertion, mirrors C-7).** A transformer type is determined to be *resolvable* by a non-instantiating lookup of that type against the transformer factory / service collection (e.g. `IServiceProvider.GetService(transformerType)` returns non-null), NOT by building the wrap/unwrap transform pipeline. This is required because the existing build path (`TransformPipelineBuilder.BuildWrapPipeline`/`BuildUnwrapPipeline`) *throws* `ConfigurationException` when the factory cannot create a declared transformer, whereas the lookup returns null cleanly; and because (A) must not instantiate the mapper or transformers (consistent with the reflection-only describe approach in C-5 and the naming guarantee in FR-6). The mechanism is the ADR's concern; this constraint fixes that resolvability is observed without instantiation.
- **C-12 — `ValidatePipelines` must thread new dependencies to the validator (plumbing acknowledgement).** Today `BrighterPipelineValidationExtensions.ValidatePipelines` constructs the `PipelineValidator` with only the pipeline builder, publications, subscriptions, and consumer specs — it does NOT pass the message-mapper registry or a transformer-resolvability source (unlike `DescribePipelines`, which does build a mapper registry). Implementing (A) therefore requires threading the mapper registry (`ResolveMapperInfo`/`ResolveAsyncMapperInfo`) and a transformer-resolvability source into the producer (core) and consumer (ServiceActivator) rule evaluation, and extending the hard-coded producer spec list. This is new wiring at the validation entrypoint, not pure reuse; the exact shape is an ADR concern.

### Out of Scope

- **OOS-1** — The source-generation alternative to reflection-based scanning.
- **OOS-2** — Generalizing check (B) beyond the validation pipeline step. (B) covers only attributes whose `GetHandlerType()` targets `ValidateRequestHandler<>`/`ValidateRequestHandlerAsync<>`; it does NOT check arbitrary other `RequestHandlerAttribute`s (logging, resilience, inbox, etc.) for missing registrations.
- **OOS-3** — A precise scanned-assembly-set vs required-assembly-set comparison; messages name the request type/transformer and prompt `AutoFromAssemblies()`, not the exact missing assembly.
- **OOS-4** — Changing the behavior, severity, messages, or blocking semantics of existing validation rules.
- **OOS-5** — Darker and any non-`ValidatePipelines` configuration validation surface.
- **OOS-6** — Guaranteeing detection in production / resolving the C-1 contradiction.
- **OOS-7** — **Missing-message-mapper detection.** Defeated by the always-present default mapper (C-9); a "publication resolves to the default mapper" heads-up is intentionally excluded as too noisy for apps that use the default JSON mapper intentionally (NFR-5). `DescribePipelines()` already surfaces which mapper is the default for developers who want that visibility.
- **OOS-8** — **Missing-request-handler-for-subscription detection.** Already provided by the existing `ConsumerValidationRules.HandlerRegistered` rule (severity `Error`); not re-implemented or re-specified here.

## Acceptance Criteria

**AC-1** (FR-1, FR-6) — *Missing wrap-transform for a publication produces a warning.*
Given a `Publication` with `RequestType = GreetingMade` on topic `greeting`, whose resolved mapper declares `[Compress]` and `CompressPayloadTransformer` is not registered with the transformer factory,
When `ValidatePipelines()` runs,
Then a `Warning` finding is produced whose message contains `GreetingMade`, `CompressPayloadTransformer`, `greeting`, and an `AutoFromAssemblies` prompt.

**AC-2** (FR-2) — *Missing unwrap-transform for a subscription produces a warning.*
Given a `Subscription` with `RequestType = GreetingMade` and `Name = greeting-subscription`, whose resolved mapper declares `[Decompress]` and `CompressPayloadTransformer` is not registered,
When `ValidatePipelines()` runs,
Then a `Warning` finding is produced whose message contains `GreetingMade`, `CompressPayloadTransformer`, `greeting-subscription`, and an `AutoFromAssemblies` prompt.

**AC-3** (FR-3) — *A resolvable transform does not suppress an unresolvable one.*
Given a `Publication` whose resolved mapper declares two wrap transforms, one whose transformer type resolves and one (`CompressPayloadTransformer`) that does not,
When `ValidatePipelines()` runs,
Then exactly one (A) warning is produced — for the unresolvable transform.

**AC-4** (FR-4) — *Default mapper / all-resolvable yields no (A) warning.*
Given a `Publication` for `RequestType = FarewellMade` resolving to the default `JsonMessageMapper` (which declares a `[CloudEvents]` transform),
When `ValidatePipelines()` runs,
Then no (A) warnings are produced for that publication (the default mapper's transforms are out of scope).

**AC-5** (FR-5) — *Transform on an async-only resolved mapper is evaluated and reported by transformer type.*
Given a `Subscription` with `RequestType = GreetingMade` for which only an async mapper resolves, that mapper declares `[Decompress]`, and `CompressPayloadTransformer` is not registered,
When `ValidatePipelines()` runs,
Then a `Warning` finding is produced whose message contains `GreetingMade` and the literal `CompressPayloadTransformer`.

**AC-6** (FR-7, C-7) — *Validation attribute with no provider produces a warning.*
Given a registered handler `GreetingMadeHandlerAsync` annotated with `[ValidateRequestAsync]` and no validation provider registered (no open-generic `ValidateRequestHandlerAsync<>` mapping in the service collection),
When `ValidatePipelines()` runs,
Then a `Warning` finding is produced whose message references the handler/request and names `UseFluentValidation()`, `UseDataAnnotations()`, and `UseSpecification()`.

**AC-7** (FR-8) — *Provider registered suppresses the (B) warning.*
Given a handler annotated with `[ValidateRequestAsync]` and `UseFluentValidation()` registered,
When `ValidatePipelines()` runs,
Then no (B) warning is produced for that handler.

**AC-8** (FR-9) — *No validation pipeline step yields no (B) warning.*
Given a handler with no validation pipeline step (no attribute whose `GetHandlerType()` targets `ValidateRequestHandler<>`/`ValidateRequestHandlerAsync<>`) and no provider registered,
When `ValidatePipelines()` runs,
Then no (B) warning is produced for that handler.

**AC-9** (FR-10) — *Warnings never block startup under `throwOnError: true`.*
Given a configuration that triggers an FR-1 missing-transform warning and an FR-7 missing-provider warning,
When `ValidatePipelines(throwOnError: true)` runs at host startup,
Then the host starts successfully (no throw) and both warnings are present in the results.

**AC-10** (FR-11, NFR-1) — *Findings appear in the warnings collection in the standard shape.*
Given any (A) or (B) trigger,
When `ValidatePipelines()` runs,
Then the corresponding finding appears in the `PipelineValidationResult` warnings collection as a `ValidationError` with severity `Warning` and populated `Source` and `Message` (the rule is implemented with the existing `ISpecification<T>` + `ValidationResultCollector` pattern per NFR-1).

**AC-11** (NFR-2, FR-11) — *Existing rules are unchanged.*
Given a `Subscription` whose `RequestType` has no registered handler,
When `ValidatePipelines(throwOnError: true)` runs,
Then the existing `ConsumerValidationRules.HandlerRegistered` rule still produces an `Error` that blocks startup exactly as before, and the default `ValidatePipelines(enabled = true, throwOnError = true)` behavior is unchanged.

**AC-12** (FR-12, NFR-6) — *Per-entity warnings are deterministic.*
Given two `Publication`s, one for `RequestType = GreetingMade` on topic `greeting` and one for `RequestType = GreetingMade` on topic `greeting-v2`, both whose resolved mappers declare `[Compress]` with `CompressPayloadTransformer` unregistered,
When `ValidatePipelines()` runs,
Then two FR-1 warnings for `GreetingMade` are produced — one naming `greeting` and one naming `greeting-v2` — and repeated runs produce the same warnings in the same order.

## Additional Context

- This feature combines the residual, reliably-detectable part of #4159 (check A — missing transforms) with the maintainer's add-on (check B — missing validation provider, the deferred "Ian #4" item from PR #4183). A discovery pass against ADR 0053 and the codebase established that subscription handler-missing is already detected and mapper-missing is undetectable on the DI path (see Scope note, C-9, OOS-7, OOS-8).
- Provider mechanism (PR #4183): `[ValidateRequest]`/`[ValidateRequestAsync]` target abstract handlers; each provider maps the abstract open-generic to a concrete type in the service collection (e.g. `DataAnnotationsBuilderExtensions.UseDataAnnotations` → `ValidateRequestHandler<>` → `DataAnnotationsRequestHandler<>`). No provider registered ⇒ the open-generic is absent ⇒ check (B) fires (observed per C-7).
- Grounding references (HOW belongs in the ADR):
  - `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs` — `ValidatePipelines()`.
  - `src/Paramore.Brighter/Validation/PipelineValidator.cs`, `ProducerValidationRules.cs`; `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`.
  - `src/Paramore.Brighter/MessageMapperRegistry.cs` (`ResolveMapperInfo`/`ResolveAsyncMapperInfo`, default-mapper fallback).
  - `src/Paramore.Brighter/Transforms/Attributes/CompressAttribute.cs` / `DecompressAttribute.cs` (`GetHandlerType()` → `CompressPayloadTransformer`).
- Labels: feature request, .NET, V10.X, Agent Friendly.
