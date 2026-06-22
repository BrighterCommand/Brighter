# Tasks — Validate Pipeline Assembly Scanning & Validation-Provider Registration

**Spec**: [requirements.md](requirements.md) · **Design**: [ADR 0064](../../docs/adr/0064-validate-pipeline-assembly-and-provider-registration.md) · **Issue**: #4159

> TDD is mandatory (CLAUDE.md). Every behavioral task uses `/test-first` and STOPS for IDE approval before implementing. Structural (tidy-first) tasks land first and separately.

## Overview

Adds two non-blocking (Warning) checks to `ValidatePipelines()`: (A) missing wrap/unwrap transforms (producer in core, consumer in ServiceActivator) and (B) a validation pipeline step present with no provider registered (core). Per ADR 0064, structural changes (new record/interface/widened ctor) land first and separately from behavioral (TDD) changes, with all new findings emitted at `ValidationSeverity.Warning`.

> **Implementation alignment (respect the solution pattern).** Two items from the original plan were dropped to match the codebase's established patterns: (1) **no §3a inline `try`/`catch` guard** — validation rules do not catch; the `Specification<T>` framework reports any evaluation exception as `Error` like every other rule (intended findings stay Warning). (2) **No standalone "async `DescribeTransforms` overload" structural task** — that would be untested speculative code; the async (sync∪async union) describe is added **test-first inside the (A)-consumer async-only cycle (FR-5)**. Both the producer and consumer (A) rules consume that async-aware overload (`includeAsync: true`) per FR-5 — the producer is conformed to it by its own producer async-only FR-5 test (see the (A)-producer section).

## Structural tasks (tidy-first, do first)

These are pure structural additions — new types, an overload, and an additive ctor widening. No behavior is added here; behavior arrives in the Behavioral section. Each is verified by "compiles + all existing tests still green". Keep each as its own commit.

- [ ] **STRUCTURAL: Add the `ValidationProviderRegistrations` record (core).**
  - Add `public record ValidationProviderRegistrations(bool Sync, bool Async);` in `Paramore.Brighter` core (namespace `Paramore.Brighter.Validation`), as specified in ADR §5. (Record class, not `record struct` — the solution uses `record` value holders throughout and has no `record struct`.)
  - No behavior; not referenced by any rule yet.
  - Verified by: solution compiles; existing `tests/Paramore.Brighter.Core.Tests/Validation/` tests remain green.
  - Traces to: ADR Key Component #5; FR-7/FR-8/FR-9 (consumed later).

- [ ] **STRUCTURAL: Add the `IAmATransformerResolvabilityProbe` interface (core).**
  - Add `public interface IAmATransformerResolvabilityProbe { bool Resolves(Type transformerType); }` in `Paramore.Brighter` core, namespace `Paramore.Brighter.Validation` (same namespace as `ValidationProviderRegistrations` and the rule infrastructure), per ADR Key Component #1. Interface only — no implementation in core (mirrors `IAmASubscriberRegistryInspector`).
  - Verified by: compiles; existing tests green.
  - Traces to: ADR Key Component #1 (RDD rationale); C-11.

> The async-aware `DescribeTransforms` overload (sync∪async union, ADR C-5 / FR-5) is **not** a structural task — it is added **test-first** in the (A)-consumer async-only cycle below, driven by its FR-5 test. Once it exists, both the producer and consumer (A) rules call it with `includeAsync: true` (FR-5 applies to publications and subscriptions alike).

- [ ] **STRUCTURAL: Widen the `PipelineValidator` constructor with trailing optional params (core).**
  - Append three optional, defaulted parameters to `PipelineValidator`'s primary ctor (`src/Paramore.Brighter/Validation/PipelineValidator.cs`): `MessageMapperRegistry? mapperRegistry = null`, `IAmATransformerResolvabilityProbe? transformerProbe = null`, `ValidationProviderRegistrations? providerRegistrations = null`. Because `ValidationProviderRegistrations` is a `record` class, `null` is the only valid default and is treated as "no provider" `(false,false)` — inert (do NOT write `= default` expecting `(false,false)`; `default` of a class is `null`). Store them; do NOT yet append any rule to `ValidateProducers`/`ValidateHandlerPipelines` (that is behavioral). Appended + optional so existing positional callers (incl. core tests constructing `PipelineValidator` directly) compile unchanged.
  - Verified by: compiles; all existing validation tests green (behavior identical because no rule is appended yet).
  - Traces to: ADR "Plumbing/threading (C-12)" / Negative consequence "Widened PipelineValidator constructor".

## Behavioral tasks (TDD)

Ordered so dependencies come first: default probe → producer (A) rule → consumer (A) rule (adds async describe test-first) → (B) → plumbing/regression. Each combines TEST+IMPLEMENT with the approval gate. Test style must match the existing Core.Tests validation tests (read one first): xUnit `[Fact]`, Given/When/Then comments, construct the spec, call `IsSatisfiedBy` + `Accept(new ValidationResultCollector<T>())`, assert on `results[0].Error!`.

> Some tasks below note "no new production code expected" — they pin a behavior already implemented by a prior task (legitimate TDD characterization tests, each targeting a distinct behavior). If such a test passes on first run, confirm the red→green moment by temporarily reverting the enabling change so you've seen the test fail.

### Default transformer-resolvability probe (DependencyInjection)

- [ ] **TEST + IMPLEMENT: The default probe resolves a registered transformer type and not an unregistered one.**
  - **USE COMMAND**: `/test-first the default transformer-resolvability probe reports a registered transformer type as resolving and an unregistered transformer type as not resolving`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests/"
  - Test file: `When_probing_transformer_resolvability_should_match_registered_service_types.cs`
  - Test should verify:
    - Given an `IServiceCollection` into which `CompressPayloadTransformer` has been registered (as `ServiceCollectionTransformerRegistry.Add` does — `ServiceDescriptor(transform, transform, Transient)`), the default `IAmATransformerResolvabilityProbe.Resolves(typeof(CompressPayloadTransformer))` returns `true`.
    - For a transformer type NOT registered, `Resolves(...)` returns `false`.
    - The probe does NOT instantiate the transformer (it answers from registration membership, not by creating it) — C-11.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add a default `IAmATransformerResolvabilityProbe` implementation in `Paramore.Brighter.Extensions.DependencyInjection` that captures the `IServiceCollection` (or a frozen snapshot of registered transformer `ServiceType`s) and answers `Resolves(type)` as a membership test — "a descriptor exists whose `ServiceType == transformerType`" (ADR §1). Not yet wired into `ValidatePipelines()` (wiring happens in the plumbing task).
  - Depends on: the `IAmATransformerResolvabilityProbe` interface task.
  - Traces to: ADR Key Component #1; C-11; C-12.

### (A) producer — `ProducerValidationRules.WrapTransformResolvable`

- [ ] **TEST + IMPLEMENT: Missing wrap-transform on a Publication produces a Warning.**
  - **USE COMMAND**: `/test-first a Publication whose resolved mapper declares a wrap transform whose transformer type is not resolvable produces a Warning naming the request type, transformer type, topic, and an AutoFromAssemblies prompt`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_publication_wrap_transform_unresolvable_should_report_warning.cs`
  - Test should verify:
    - For a `Publication` with `RequestType = GreetingMade`, `Topic = greeting`, whose resolved mapper declares `[Compress]` and a probe that reports `CompressPayloadTransformer` as not resolving, `IsSatisfiedBy` is false and exactly one finding is produced.
    - The finding has `Severity == ValidationSeverity.Warning`, `Source` like `Publication 'greeting'`, and `Message` containing `GreetingMade`, `CompressPayloadTransformer`, `greeting`, and the phrase `AutoFromAssemblies`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `public static ISpecification<Publication> WrapTransformResolvable(MessageMapperRegistry mapperRegistry, IAmATransformerResolvabilityProbe probe)` to `src/Paramore.Brighter/Validation/ProducerValidationRules.cs` using the collapsed `Specification<Publication>(Func<…,IEnumerable<ValidationResult>>)` ctor (one finding per missing transform).
    - For each `Publication`, call `DescribeTransforms(mapperRegistry, publication.RequestType, includeAsync: true)` (the async-aware overload, FR-5 — initially the 2-arg sync overload, switched once the FR-5 overload exists), iterate `WrapTransforms`, and for each `TransformStepDescription` whose `TransformType` the probe reports as not resolving, yield a `ValidationError(Warning, $"Publication '{p.Topic}'", …)` matching the ADR §2 message template. The rule does NOT catch exceptions — it follows the framework pattern (the `Specification<T>` evaluator reports evaluation errors as `Error`, like every rule; §3a).
    - Append `WrapTransformResolvable(mapperRegistry, probe)` to the producer spec array in `PipelineValidator.ValidateProducers`, guarded so it is added only when the threaded `mapperRegistry` and `transformerProbe` fields are both non-null (inert for pure-CQRS/no-producer configs, NFR-2/-4). This mirrors how the (B) rule self-appends to `ValidateHandlerPipelines`, leaving the plumbing task as pure dependency-threading.
  - Depends on: the `IAmATransformerResolvabilityProbe` interface task; the widened `PipelineValidator` ctor task (for the threaded fields).
  - Traces to: FR-1, FR-6, AC-1, C-8.

- [ ] **TEST + IMPLEMENT: A resolvable wrap transform does not suppress an unresolvable one (one Warning per missing transform).**
  - **USE COMMAND**: `/test-first a Publication whose resolved mapper declares two wrap transforms, one resolvable and one not, produces exactly one Warning for the unresolvable transform`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_publication_has_resolvable_and_unresolvable_wrap_transforms_should_report_one_warning.cs`
  - Test should verify:
    - With a probe that resolves one transformer type but not `CompressPayloadTransformer`, exactly one finding is produced, for the unresolvable transform.
    - The resolvable transform produces no finding.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Confirmed by the per-step yield in `WrapTransformResolvable` (no suppression / no early-out across steps). No new production code expected beyond the prior task; if the prior implementation short-circuited, fix it to yield per missing step.
  - Depends on: the `WrapTransformResolvable` task above.
  - Traces to: FR-3, AC-3.

- [ ] **TEST + IMPLEMENT: A publication resolving to the default mapper yields no (A) warning (default mapper out of scope).**
  - **USE COMMAND**: `/test-first a Publication that resolves to the default mapper produces no producer wrap-transform warning even when that default mapper's declared transformer is not resolvable`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test fact: `When_publication_resolves_to_default_mapper_should_report_no_warning` (added to the existing `WrapTransformResolvableTests` class)
  - Test should verify:
    - For a `Publication` resolving to the default `JsonMessageMapper<>` (registry built with `JsonMessageMapper<>` as the default, no custom mapper for the request type) and a probe that resolves **nothing**, `IsSatisfiedBy` is true and zero findings are produced — the default mapper's `[CloudEvents]` wrap transform is skipped.
    - A publication whose mapper declares no transforms also produces zero findings (existing facts).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add a short-circuit to `WrapTransformResolvable`: `if (description is null || description.IsDefaultMapper) return [];` so the default mapper's built-in transforms are never checked (FR-4/NFR-5). The consumer rule needs no guard (the default mapper declares no unwrap transform).
  - Depends on: the `WrapTransformResolvable` task.
  - Traces to: FR-4, AC-4, NFR-5.

- [ ] **TEST + IMPLEMENT: Wrap transform on an async-only-resolved mapper is evaluated (producer FR-5).**
  - **USE COMMAND**: `/test-first a Publication for which only an async mapper resolves, declaring a wrap transform whose transformer type is not resolvable, still produces a Warning naming the request type and the transformer type`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_publication_async_only_mapper_wrap_transform_unresolvable_should_report_warning.cs`
  - Test should verify:
    - With only an async mapper resolved for `MyDescribableCommand` (registered via `RegisterAsync`) that declares a wrap transform (`MyDescribableTransform`) and a probe reporting it as not resolving, exactly one Warning is produced whose `Message` contains the request type and the `GetHandlerType()` transformer type.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Switch `WrapTransformResolvable` to `DescribeTransforms(mapperRegistry, publication.RequestType, includeAsync: true)` so the producer evaluates the sync∪async union exactly like the consumer. FR-5 is general — it applies to publications as well as subscriptions; AC-5 illustrates only the subscription side.
  - Depends on: the `WrapTransformResolvable` task; the async-aware `DescribeTransforms` overload (added in the (A)-consumer FR-5 cycle).
  - Traces to: FR-5.

### (A) consumer — `ConsumerValidationRules.UnwrapTransformResolvable` (ServiceActivator)

- [ ] **TEST + IMPLEMENT: Missing unwrap-transform on a Subscription produces a Warning (and null-RequestType subscription is skipped).**
  - **USE COMMAND**: `/test-first a Subscription whose resolved mapper declares an unwrap transform whose transformer type is not resolvable produces a Warning naming the request type, transformer type, and subscription Name; a subscription with null RequestType is skipped`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"  (Core.Tests references ServiceActivator and already tests `ConsumerValidationRules` here — `using Paramore.Brighter.ServiceActivator.Validation;`)
  - Test file: `When_subscription_unwrap_transform_unresolvable_should_report_warning.cs`
  - Test should verify:
    - For a `Subscription` with `RequestType = GreetingMade`, `Name = greeting-subscription`, whose resolved mapper declares `[Decompress]` and a probe reporting `CompressPayloadTransformer` as not resolving, exactly one finding with `Severity == Warning`, `Source` like `Subscription 'greeting-subscription'`, `Message` containing `GreetingMade`, `CompressPayloadTransformer`, `greeting-subscription`, and an `AutoFromAssemblies` prompt.
    - A `Subscription` whose `RequestType` is null is skipped (`IsSatisfiedBy` true, zero findings).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This cycle includes the **async-only mapper** fact (FR-5), so first add (test-first, driven by that fact) the async-aware `DescribeTransforms` overload to `TransformPipelineBuilder`: it unions the sync- and async-resolved mappers' transforms (`ResolveAsyncMapperInfo` + `FindMapToMessageAsync`/`FindMapToRequestAsync`), de-duplicated by `(TransformType, Step)`. The producer rule can then switch to this overload too if async coverage is wanted there.
    - Add `public static ISpecification<Subscription> UnwrapTransformResolvable(MessageMapperRegistry mapperRegistry, IAmATransformerResolvabilityProbe probe)` to `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`, collapsed-ctor, keyed on `Subscription.RequestType` (skip when null, mirroring `HandlerRegistered`), iterating `UnwrapTransforms` from the async-aware `DescribeTransforms`, naming `Subscription.Name`. The rule does NOT catch exceptions (framework pattern, §3a).
    - Register it in `RegisterConsumerValidationSpecs` (`src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`, ~line 197) as another `services.AddSingleton<ISpecification<Subscription>>(sp => …)` resolving `MessageMapperRegistry` and `IAmATransformerResolvabilityProbe` from `sp`. It rides the existing `consumerSpecs` DI channel — no `PipelineValidator` change.
    - **Sequencing note (as built):** this DI registration is performed in the **plumbing/wiring cycle**, not in this rule cycle — together with registering the default probe and making `MessageMapperRegistry` resolvable on the consumer path — and the consumer-spec count assertion `Assert.Equal(3, …)` in `When_add_consumers_should_register_consumer_validation_specs` becomes `4`. Registering it before those dependencies exist throws at validation time (`GetRequiredService<IAmATransformerResolvabilityProbe>()`/`<MessageMapperRegistry>()`) and breaks existing consumer-validation tests. The rule **method** is delivered in this cycle; its wiring rides the plumbing cycle.
  - Depends on: the `IAmATransformerResolvabilityProbe` interface + default impl tasks. (This task itself adds the async-aware `DescribeTransforms` overload test-first, driven by the async-only fact.)
  - Traces to: FR-2, AC-2.

- [ ] **TEST + IMPLEMENT: Transform on an async-only-resolved mapper is evaluated; identity is the `GetHandlerType()` transformer type.**
  - **USE COMMAND**: `/test-first a Subscription for which only an async mapper resolves, declaring [Decompress], with CompressPayloadTransformer unresolvable, still produces a Warning naming CompressPayloadTransformer`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_subscription_async_only_mapper_unwrap_transform_unresolvable_should_report_warning.cs`
  - Test should verify:
    - With only an async mapper resolved for `GreetingMade` that declares `[Decompress]` and a probe reporting `CompressPayloadTransformer` as not resolving, exactly one Warning is produced whose `Message` contains `GreetingMade` and the literal `CompressPayloadTransformer` (the `GetHandlerType()` transformer type, not the attribute name).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Be satisfied by the async∪sync union in the `DescribeTransforms` overload and `UnwrapTransformResolvable`. No new production code expected beyond confirming the union reads async-resolved mappers; add a TestDouble async mapper if one is not already present in `TestDoubles/`.
  - Depends on: the `UnwrapTransformResolvable` task (this fact drives adding the async-aware `DescribeTransforms` overload test-first).
  - Traces to: FR-5, AC-5.

### (B) — `HandlerPipelineValidationRules.ValidationProviderRegistered`

- [ ] **TEST + IMPLEMENT: A validation pipeline step present with no provider registered produces a Warning naming the three Use*() calls.**
  - **USE COMMAND**: `/test-first a handler whose pipeline declares a step targeting ValidateRequestHandler<> while no provider is registered produces a Warning naming the request/handler and UseFluentValidation, UseDataAnnotations, UseSpecification`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_validation_step_present_and_no_provider_should_report_warning.cs`
  - Test should verify:
    - Building a `HandlerPipelineDescription` (via the reflection-only `Describe()`) for a handler annotated with `[ValidateRequestAsync]` (whose `GetHandlerType()` is `ValidateRequestHandlerAsync<>`), evaluated with `ValidationProviderRegistrations(Sync:false, Async:false)`, yields one finding: `Severity == Warning`, `Source` like `Handler '…'`, `Message` containing the request/handler name and `UseFluentValidation()`, `UseDataAnnotations()`, `UseSpecification()`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `public static ISpecification<HandlerPipelineDescription> ValidationProviderRegistered(ValidationProviderRegistrations registrations)` to `src/Paramore.Brighter/Validation/HandlerPipelineValidationRules.cs` (collapsed ctor). For each pipeline step whose `HandlerType == typeof(ValidateRequestHandler<>)` (or `typeof(ValidateRequestHandlerAsync<>)`) while the matching flag (`.Sync`/`.Async`) is `false`, yield the ADR §4 Warning.
    - Append the rule to the `specs` array in `PipelineValidator.ValidateHandlerPipelines`, parameterised by the threaded `providerRegistrations` field (null → treated as `(false,false)` → inert until wired).
  - Depends on: the `ValidationProviderRegistrations` record task; the widened `PipelineValidator` ctor task.
  - Traces to: FR-7, AC-6, C-4, C-7.

- [ ] **TEST + IMPLEMENT: Provider registered suppresses the (B) warning.**
  - **USE COMMAND**: `/test-first a handler with a validation step produces no (B) warning when the matching provider flag is true`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_validation_step_present_and_provider_registered_should_report_no_warning.cs`
  - Test should verify:
    - The same `[ValidateRequestAsync]` handler description, evaluated with `ValidationProviderRegistrations(Sync:false, Async:true)`, produces zero findings.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Be satisfied by the flag check in `ValidationProviderRegistered`. No new production code expected.
  - Depends on: the `ValidationProviderRegistered` task.
  - Traces to: FR-8, AC-7.

- [ ] **TEST + IMPLEMENT: No validation pipeline step yields no (B) warning regardless of provider state.**
  - **USE COMMAND**: `/test-first a handler with no validation pipeline step produces no (B) warning even when no provider is registered`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_no_validation_step_present_should_report_no_provider_warning.cs`
  - Test should verify:
    - A `HandlerPipelineDescription` for a handler with no step whose `HandlerType` targets the validation open generics, evaluated with `ValidationProviderRegistrations(false,false)`, produces zero findings.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Be satisfied by the per-step filter in `ValidationProviderRegistered` (no matching step → nothing yielded). No new production code expected.
  - Depends on: the `ValidationProviderRegistered` task.
  - Traces to: FR-9, AC-8.

### §3a guard — REMOVED (rules follow the framework pattern)

> There is **no §3a guard task**. The rules do not catch exceptions; the `Specification<T>` framework reports any evaluation exception as `Error`, exactly as it does for every existing rule (intended findings stay Warning). This respects the solution pattern — see the Implementation-alignment note at the top and ADR 0064 §3a. The intended Warning findings are covered by the (A) producer/consumer tests; there is no throwing-probe test because no bespoke guard exists to pin.

### Plumbing / wiring and regression

- [ ] **TEST + IMPLEMENT: `ValidatePipelines()` threads mapper registry + probe + registrations so (A) and (B) run end-to-end.**
  - **USE COMMAND**: `/test-first ValidatePipelines wires the mapper registry, transformer-resolvability probe, and validation-provider registrations into PipelineValidator so a configured missing wrap transform and a missing-provider validation step both surface as Warnings`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests/"
  - Test file: `When_validate_pipelines_with_transform_and_provider_should_report_warnings.cs`
  - Test should verify:
    - A `ServiceCollection` configured with a publication declaring `[Compress]` (transformer unregistered) and a handler annotated with `[ValidateRequest]`/`[ValidateRequestAsync]` with no provider registered, after `.ValidatePipelines()` and `BuildServiceProvider()`, when resolving `IAmAPipelineValidator` and calling `Validate()`, yields `result.Warnings` containing both the (A) transform Warning and the (B) provider Warning, and `result.IsValid` is true (warnings only).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `BrighterPipelineValidationExtensions.ValidatePipelines` (`src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs`), copy the `DescribePipelines` pattern to build a `MessageMapperRegistry` via `ServiceCollectionExtensions.MessageMapperRegistry(sp)` when a `ServiceCollectionMessageMapperRegistryBuilder` is present; register/resolve the default `IAmATransformerResolvabilityProbe` (IServiceCollection snapshot); compute `ValidationProviderRegistrations(Sync: builder.Services.Any(d => d.ServiceType == typeof(ValidateRequestHandler<>)), Async: …Async<>…)`; and pass mapper registry + probe + registrations into the widened `PipelineValidator` ctor. (The producer (A) rule self-appends to `ValidateProducers` and the (B) rule to `ValidateHandlerPipelines` in their own tasks, guarded by the threaded fields — this task only threads the dependencies.)
  - Depends on: the widened `PipelineValidator` ctor task; `WrapTransformResolvable`; `ValidationProviderRegistered`; the default probe behavioral task.
  - Traces to: C-12; FR-11; AC-10.

- [ ] **TEST + IMPLEMENT: Both checks are non-blocking under `throwOnError: true` — host starts, both warnings present.**
  - **USE COMMAND**: `/test-first with a missing-transform trigger and a missing-provider trigger and ValidatePipelines(throwOnError true), validation does not throw and both warnings are present`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests/"
  - Test file: `When_throw_on_error_true_with_transform_and_provider_warnings_should_not_throw.cs`
  - Test should verify:
    - With both an (A) and a (B) trigger configured and `.ValidatePipelines(throwOnError: true)`, resolving the validator and validating does NOT throw, `result.IsValid` is true, and both warnings are present in `result.Warnings`.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Be satisfied by the Warning severity of both rules; no new production code expected beyond the wiring task. If `IsValid` incorrectly treats warnings as blocking, the defect is in rule severity (must be `Warning`).
  - Depends on: the plumbing/wiring task.
  - Traces to: FR-10, AC-9.

## Risk-mitigation tasks

> No §3a guard test (the guard was removed — rules follow the framework pattern). An inspection exception surfaces as the framework's `Error`, the same as every existing rule; the intended Warning findings are pinned by the (A) producer/consumer rule tests.

- [ ] **TEST + IMPLEMENT: AC-11 regression — existing `ConsumerValidationRules.HandlerRegistered` still produces a blocking Error.**
  - **USE COMMAND**: `/test-first after adding the new warning checks, a subscription with no registered handler still produces a blocking Error under ValidatePipelines(throwOnError true)`
  - Test location: "tests/Paramore.Brighter.Extensions.Tests/"
  - Test file: `When_subscription_has_no_handler_after_new_checks_should_still_block.cs`
  - Test should verify:
    - A subscription whose `RequestType` has no registered handler still yields a `ValidationSeverity.Error` containing "No handler registered", `result.IsValid` is false, and `ValidatePipelines(throwOnError: true)` blocks/throws as before.
    - The default `ValidatePipelines(enabled = true, throwOnError = true)` behavior is unchanged (existing tests stay green).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Require no production change — this is a guard that the additive rules did not alter existing severity/blocking. (The existing `When_validate_pipelines_with_consumers_should_receive_subscriptions.cs` already covers the spec-count; this asserts the Error still blocks alongside the new warnings.)
  - Depends on: the plumbing/wiring task.
  - Traces to: FR-11, NFR-2, AC-11, OOS-4.

- [ ] **TEST + IMPLEMENT: FR-12 determinism — two publications, same request type, different topics → two warnings in stable order.**
  - **USE COMMAND**: `/test-first two publications for the same request type on different topics, each with the same unresolvable wrap transform, produce two warnings each naming its topic, in stable publications-then-subscriptions configuration order across repeated runs`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Validation/"
  - Test file: `When_two_publications_same_request_different_topics_should_report_two_ordered_warnings.cs`
  - Test should verify:
    - Two `Publication`s for `RequestType = GreetingMade` on topics `greeting` and `greeting-v2`, both declaring `[Compress]` with `CompressPayloadTransformer` unresolvable, yield exactly two (A) warnings — one naming `greeting`, one naming `greeting-v2` — with no cross-entity de-duplication.
    - Running the validator twice over the same configuration yields the same warnings in the same order.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Be satisfied by per-entity evaluation in `PipelineValidator.EvaluateSpecs` (entities in configuration order; steps `OrderByDescending(a => a.Step)` in `DescribeTransforms`). No new production code expected; if ordering is nondeterministic, fix the describe/iteration order.
  - Depends on: the `WrapTransformResolvable` task.
  - Traces to: FR-12, NFR-6, AC-12.

## Coverage

| FR / ADR decision | Covered by task(s) |
|---|---|
| FR-1 (missing wrap transform → Warning) | "Missing wrap-transform on a Publication produces a Warning" |
| FR-2 (missing unwrap transform → Warning; null RequestType skipped) | "Missing unwrap-transform on a Subscription produces a Warning (and null-RequestType subscription is skipped)" |
| FR-3 (independent per-transform; resolvable doesn't suppress) | "A resolvable wrap transform does not suppress an unresolvable one" |
| FR-4 (all resolve / none declared / default mapper → no (A) warning) | "A publication resolving to the default mapper yields no (A) warning" + the custom-mapper all-resolvable / no-transforms facts |
| FR-5 (async-only-resolved mapper evaluated, publications **and** subscriptions; identity = `GetHandlerType()`) | Consumer "Transform on an async-only-resolved mapper is evaluated …" (adds the async-aware `DescribeTransforms` overload test-first) + producer "Wrap transform on an async-only-resolved mapper is evaluated (producer FR-5)" |
| FR-6 (message from configured/declared info only) | "Missing wrap-transform … produces a Warning" (message-content assertions) |
| FR-7 (validation step + no provider → Warning naming three Use*()) | "A validation pipeline step present with no provider registered produces a Warning …" |
| FR-8 (provider registered → no (B) warning) | "Provider registered suppresses the (B) warning" |
| FR-9 (no validation step → no (B) warning) | "No validation pipeline step yields no (B) warning …" |
| FR-10 (non-blocking under throwOnError) | "Both checks are non-blocking under `throwOnError: true` …" (all new findings are Warning) |
| FR-11 (additive; in `Warnings` as `ValidationError(Warning,…)`; existing rules unchanged) | Plumbing/wiring task (warnings surface) + AC-11 regression task |
| FR-12 (deterministic per-entity warnings) | "Two publications same request different topics → two ordered warnings" |
| NFR-1 (ISpecification + ValidationResultCollector pattern) | All (A)/(B) behavioral tasks (use collapsed `Specification<T>` + `ValidationResultCollector`) |
| NFR-2 / OOS-4 (no behavioral change to existing rules) | AC-11 regression task; widened-ctor structural task (inert defaults) |
| NFR-3 (actionable messages) | FR-1/FR-2/FR-7 behavioral tasks (message-content assertions) |
| NFR-4 (opt-in, side-effect-light; inert when not wired) | Widened-ctor structural task; plumbing task (rule appended only when deps non-null) |
| NFR-5 (non-blocking, low-noise; default mapper not flagged) | FR-4 task (default `[CloudEvents]` resolves → no warning) |
| NFR-6 (determinism) | FR-12 determinism task |
| ADR new type: `ValidationProviderRegistrations` record | Structural "Add the `ValidationProviderRegistrations` record" |
| ADR new type: `IAmATransformerResolvabilityProbe` interface (core) | Structural "Add the `IAmATransformerResolvabilityProbe` interface" |
| ADR new type: default probe impl (DependencyInjection) + its membership-test behavior | Behavioral "The default probe resolves a registered transformer type and not an unregistered one" |
| ADR: async-aware `DescribeTransforms` overload (C-5) | Added test-first in the (A)-consumer async-only cycle (FR-5) |
| ADR: widened `PipelineValidator` ctor (appended optional params) | Structural "Widen the `PipelineValidator` constructor" |
| ADR §2: (A)-producer rule placement = core `ProducerValidationRules` (C-8) | "Missing wrap-transform on a Publication …" (adds to `src/Paramore.Brighter/Validation/ProducerValidationRules.cs`) |
| ADR §3: (A)-consumer rule placement = ServiceActivator `ConsumerValidationRules` (C-8) | "Missing unwrap-transform on a Subscription …" (adds to ServiceActivator + `RegisterConsumerValidationSpecs`) |
| ADR §4: (B) rule placement = core `HandlerPipelineValidationRules` (no ServiceActivator dep) (C-8) | "A validation pipeline step present with no provider registered …" |
| ADR §3a: rules follow the framework pattern (no inline guard) | (A) producer/consumer rule tasks — rules do not catch; `Specification<T>` reports evaluation errors as Error like every rule |
| ADR Plumbing (C-12): thread mapper registry + probe + registrations via `ValidatePipelines` | Plumbing/wiring task |
| ADR: severity = Warning for all new findings (C-2) | FR-1/FR-2/FR-7 tasks (assert `Warning`); FR-10 task (never blocks) |
| ADR C-4 ((B) via `GetHandlerType()` open generics, provider-agnostic) | "A validation pipeline step present with no provider registered …" (compares `HandlerType` to `ValidateRequestHandler<>`/`Async<>`) |
| ADR C-7 / C-11 (resolvability/provider check without build or instantiation) | Default probe impl task (membership test); (B) task (reuses reflection-only `Describe()`) |

No FR (FR-1..FR-12), NFR (NFR-1..NFR-6), or ADR decision is left without a task. No task introduces scope outside an FR/ADR decision; OOS-1..OOS-8 are explicitly not implemented (no task adds mapper-missing detection, handler-missing re-implementation, assembly-set comparison, or generalization of (B)).

## Critical Files for Implementation
- `src/Paramore.Brighter/Validation/PipelineValidator.cs`
- `src/Paramore.Brighter/Validation/ProducerValidationRules.cs`
- `src/Paramore.Brighter/Validation/HandlerPipelineValidationRules.cs`
- `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs`
- `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterPipelineValidationExtensions.cs`
