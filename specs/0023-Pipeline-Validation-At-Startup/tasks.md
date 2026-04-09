# Tasks — Spec 0023: Pipeline Validation at Startup

**Branch**: `pipeline_validation`
**ADR**: [0053-pipeline-validation-at-startup.md](../../docs/adr/0053-pipeline-validation-at-startup.md)
**Issue**: [#2176](https://github.com/BrighterCommand/Brighter/issues/2176)

## Overview

Tasks are organized into 8 phases. Phases 1-2 are structural tidies (no behavioral change). Phases 3-8 add new behavior via TDD. Each phase builds on the previous.

---

## Phase 1: Tidy First — Move and Extract (Structural Only)

These tasks use `/tidy-first` — they change code structure without changing behavior. Existing tests must continue to pass.

- [x] **TIDY: Move ISpecification<T> and Specification<T> from Mediator to Brighter**
  - **USE COMMAND**: `/tidy-first move Specification types from Paramore.Brighter.Mediator to Paramore.Brighter`
  - Move `ISpecification<TData>`, `Specification<T>`, `AndSpecification<T>`, `OrSpecification<T>`, `NotSpecification<T>` from `src/Paramore.Brighter.Mediator/Specification.cs` to `src/Paramore.Brighter/`
  - Change namespace from `Paramore.Brighter.Mediator` to `Paramore.Brighter`
  - Update `using` statements in `ExclusiveChoice<TData>` and any other Mediator consumers
  - Existing Mediator tests must pass unchanged (except `using` updates)

- [x] **TIDY: Extract HandlerMethodDiscovery static utility**
  - **USE COMMAND**: `/tidy-first extract HandlerMethodDiscovery from RequestHandler and RequestHandlerAsync`
  - Create `src/Paramore.Brighter/HandlerMethodDiscovery.cs` (internal static class)
  - Extract `FindHandlerMethod(Type handlerType, Type requestType)` — single source of truth
  - Refactor `RequestHandler<T>.FindHandlerMethod()` and `RequestHandlerAsync<T>.FindHandlerMethod()` to delegate to it
  - Existing pipeline tests must pass unchanged

- [x] **TIDY: Extract MapperMethodDiscovery static utility**
  - **USE COMMAND**: `/tidy-first extract MapperMethodDiscovery from TransformPipelineBuilder`
  - Create `src/Paramore.Brighter/MapperMethodDiscovery.cs` (internal static class)
  - Extract `FindMapToMessage`, `FindMapToMessageAsync`, `FindMapToRequest`, `FindMapToRequestAsync`
  - Refactor `TransformPipelineBuilder` and `TransformPipelineBuilderAsync` to delegate to them
  - Existing transform pipeline tests must pass unchanged

- [x] **TIDY: Fix PipelineBuilder double AddGlobalInboxAttributesAsync bug**
  - **USE COMMAND**: `/tidy-first fix double AddGlobalInboxAttributesAsync call in PipelineBuilder.BuildAsyncPipeline`
  - `BuildAsyncPipeline()` calls `AddGlobalInboxAttributesAsync()` twice — once inside the cache-miss block (correct) and once unconditionally outside it (bug)
  - Remove the duplicate call outside the `if` block
  - Existing pipeline tests must pass unchanged

---

## Phase 2: Tidy First — New Interfaces Without Consumers (Structural Only)

These tidies add new interfaces and fields but no new consumers yet. No behavioral change to existing code.

- [x] **TIDY: Add IAmASubscriberRegistryInspector and implement on SubscriberRegistry**
  - **USE COMMAND**: `/tidy-first add IAmASubscriberRegistryInspector to SubscriberRegistry`
  - Create `src/Paramore.Brighter/IAmASubscriberRegistryInspector.cs` with `GetHandlerTypes(Type)` and `GetRegisteredRequestTypes()`
  - Add `Dictionary<Type, HashSet<Type>> _allHandlerTypes` to `SubscriberRegistry`
  - Both `Add` overloads populate `_allHandlerTypes`
  - `GetHandlerTypes()` returns `.ToArray()`, `GetRegisteredRequestTypes()` returns `.Keys.ToArray()`
  - `SubscriberRegistry` implements `IAmASubscriberRegistryInspector`
  - Existing tests pass unchanged — new methods are additive

- [x] **TIDY: Add ResolveMapperInfo and ResolveAsyncMapperInfo to MessageMapperRegistry**
  - **USE COMMAND**: `/tidy-first add ResolveMapperInfo to MessageMapperRegistry`
  - Add `ResolveMapperInfo(Type requestType)` returning `(Type? mapperType, bool isDefault)`
  - Add `ResolveAsyncMapperInfo(Type requestType)` with same signature
  - Four return states: explicit, default resolved, no mapper, misconfigured default
  - Existing tests pass unchanged — new methods are additive

- [x] **TIDY: Add IAmABackstopHandler and IAmAResilienceHandler marker interfaces**
  - **USE COMMAND**: `/tidy-first add marker interfaces for handler classification`
  - Create `src/Paramore.Brighter/IAmABackstopHandler.cs` and `src/Paramore.Brighter/IAmAResilienceHandler.cs`
  - Apply `IAmABackstopHandler` to: `RejectMessageOnErrorHandler<T>`, `RejectMessageOnErrorHandlerAsync<T>`, `DeferMessageOnErrorHandler<T>`, `DeferMessageOnErrorHandlerAsync<T>`, `DontAckOnErrorHandler<T>`, `DontAckOnErrorHandlerAsync<T>`
  - Apply `IAmAResilienceHandler` to: `UseResiliencePipelineHandler<T>` and its async counterpart
  - Existing tests pass unchanged — marker interfaces add no behavior

---

## Phase 3: Foundation Types — Validation Infrastructure

These tasks add new types that the specifications and validator depend on.

- [x] **TEST + IMPLEMENT: Specification<T> enhanced with validation support and visitor pattern**
  - **USE COMMAND**: `/test-first when Specification is created with error factory and entity fails predicate then visitor collects the validation error`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_specification_with_error_factory_fails_should_collect_error.cs`
  - Test should verify:
    - Pure predicate constructor: `IsSatisfiedBy` returns bool, visitor returns empty results
    - Simple rule constructor (predicate + error factory): visitor returns the `ValidationError` on failure
    - Collapsed rule constructor (result evaluator): visitor returns multiple `ValidationResult` items
    - Exception in predicate produces Error-severity `ValidationResult` instead of throwing
    - `_lastResults` cache: visitor reads cached results without re-evaluating
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Enhance `Specification<T>` in `src/Paramore.Brighter/` with three constructors (pure, simple, collapsed)
    - Add `_errorFactory`, `_resultEvaluator`, `_lastResults` fields
    - Add `EvaluateSimple` and `EvaluateCollapsed` methods with exception catching
    - Add `LastResults` internal property
    - Add `Accept<TResult>(ISpecificationVisitor<TData, TResult>)` to `ISpecification<T>` and all composition types

- [x] **TEST + IMPLEMENT: AndSpecification evaluates both children unconditionally**
  - **USE COMMAND**: `/test-first when AndSpecification left side fails then right side is still evaluated and visitor collects both errors`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_and_specification_left_fails_should_evaluate_both_sides.cs`
  - Test should verify:
    - Left fails, right fails: visitor collects errors from both
    - Left fails, right passes: visitor collects only left error, right has empty results
    - Both pass: `IsSatisfiedBy` returns true
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `AndSpecification.IsSatisfiedBy`: `var l = Left.IsSatisfiedBy(entity); var r = Right.IsSatisfiedBy(entity); return l && r;`

- [x] **TEST + IMPLEMENT: ValidationResult, ValidationError, and ValidationResultCollector**
  - **USE COMMAND**: `/test-first when ValidationResultCollector visits a specification graph then it collects all failed results`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_collector_visits_specification_graph_should_collect_all_failures.cs`
  - Test should verify:
    - `ValidationResult.Ok()` has `Success = true`, `Error = null`
    - `ValidationResult.Fail(error)` has `Success = false`, carries the error
    - `ValidationError` holds `Severity`, `Source`, `Message`
    - `ValidationResultCollector` traverses And/Or/Not nodes and collects leaf results
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/ValidationResult.cs`
    - Create `src/Paramore.Brighter/Validation/ValidationError.cs` with `ValidationSeverity` enum
    - Create `src/Paramore.Brighter/Validation/ISpecificationVisitor.cs`
    - Create `src/Paramore.Brighter/Validation/ValidationResultCollector.cs`

- [x] **TEST + IMPLEMENT: PipelineValidationResult and PipelineValidationException**
  - **USE COMMAND**: `/test-first when PipelineValidationResult has errors then ThrowIfInvalid throws PipelineValidationException`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_validation_result_has_errors_should_throw_pipeline_validation_exception.cs`
  - Test should verify:
    - `IsValid` is true when no errors (warnings only is still valid)
    - `IsValid` is false when errors present
    - `ThrowIfInvalid()` throws `PipelineValidationException` (extends `ConfigurationException`)
    - Exception message lists all errors with source context
    - `PipelineValidationResult.Combine()` merges multiple results
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/PipelineValidationResult.cs`
    - Create `src/Paramore.Brighter/Validation/PipelineValidationException.cs` extending `ConfigurationException`

---

## Phase 4: Pipeline Description Model (Dry Run)

- [x] **TEST + IMPLEMENT: PipelineBuilder.Describe(Type) produces HandlerPipelineDescription**
  - **USE COMMAND**: `/test-first when PipelineBuilder describes a handler type then it returns pipeline description with attribute chain`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_pipeline_builder_describes_handler_should_return_pipeline_description.cs`
  - Test should verify:
    - Description includes `RequestType`, `HandlerType`, `IsAsync`
    - `BeforeSteps` lists attributes in step order with correct `AttributeType`, `HandlerType`, `Step`
    - `AfterSteps` lists post-handler attributes
    - Describe-only constructor (no handler factory) works without error
    - Multiple handler types per request type produce multiple descriptions
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/HandlerPipelineDescription.cs`
    - Create `src/Paramore.Brighter/Validation/PipelineStepDescription.cs`
    - Add describe-only constructor to `PipelineBuilder<TRequest>` (subscriber registry + inbox config only)
    - Add `Describe(Type requestType)` method — phase 1 only (reflection, no instantiation)
    - Add parameterless `Describe()` that iterates `GetRegisteredRequestTypes()`

- [x] **TEST + IMPLEMENT: TransformPipelineBuilder.DescribeTransforms produces TransformPipelineDescription**
  - **USE COMMAND**: `/test-first when TransformPipelineBuilder describes transforms then it returns mapper type and transform chain`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_transform_builder_describes_should_return_mapper_and_transforms.cs`
  - Test should verify:
    - Description includes `MapperType`, `IsDefaultMapper`
    - `WrapTransforms` lists outgoing transforms in step order
    - `UnwrapTransforms` lists incoming transforms in step order
    - Custom mapper identified as `isDefault = false`
    - Default mapper identified as `isDefault = true`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/TransformPipelineDescription.cs`
    - Create `src/Paramore.Brighter/Validation/TransformStepDescription.cs`
    - Add `DescribeTransforms<TRequest>()` to `TransformPipelineBuilder`
    - Uses `ResolveMapperInfo` + `MapperMethodDiscovery` — no factory calls

---

## Phase 5: Validation Specifications (Rules)

- [x] **TEST + IMPLEMENT: HandlerPipelineValidationRules — handler visibility, backstop ordering, sync/async consistency**
  - **USE COMMAND**: `/test-first when handler pipeline has misconfigured attributes then validation rules report correct errors`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_handler_pipeline_misconfigured_should_report_errors.cs`
  - Test should verify:
    - **HandlerTypeVisibility**: Non-public handler type → Error
    - **BackstopAttributeOrdering**: Backstop at step 5, resilience at step 3 → Warning with attribute names
    - **BackstopAttributeOrdering**: Backstop at step 0, resilience at step 1 → passes (no warning)
    - **AttributeAsyncConsistency**: Async handler with sync attribute → Error identifying the attribute
    - **AttributeAsyncConsistency**: Sync handler with async attribute → Error identifying the attribute
    - **AttributeAsyncConsistency**: Step implementing neither interface → Error
    - All rules return empty findings for correctly configured handlers
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/HandlerPipelineValidationRules.cs` (internal static)
    - Three specifications: HandlerTypeVisibility (simple), BackstopAttributeOrdering (collapsed), AttributeAsyncConsistency (collapsed)
    - Uses `IAmABackstopHandler` and `IAmAResilienceHandler` marker interfaces

- [x] **TEST + IMPLEMENT: ProducerValidationRules — publication RequestType validation**
  - **USE COMMAND**: `/test-first when publication has no RequestType then validation reports error`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_publication_missing_request_type_should_report_error.cs`
  - Test should verify:
    - Publication with `RequestType = null` → Error
    - Publication with `RequestType` not implementing `IRequest` → Error
    - Publication with valid `RequestType` → passes
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/ProducerValidationRules.cs` (internal static)
    - Two simple specifications: PublicationRequestTypeSet, PublicationRequestTypeImplementsIRequest

- [x] **TEST + IMPLEMENT: ConsumerValidationRules — pump/handler match, handler registered, request type subtype**
  - **USE COMMAND**: `/test-first when subscription has pump handler mismatch then validation reports directional error`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_subscription_misconfigured_should_report_errors.cs`
  - Test should verify:
    - **PumpHandlerMatch**: Reactor + async handler → Error naming Reactor and suggesting Proactor
    - **PumpHandlerMatch**: Proactor + sync handler → Error naming Proactor and suggesting Reactor
    - **PumpHandlerMatch**: No handlers registered → vacuously passes (HandlerRegistered catches this)
    - **HandlerRegistered**: No handler for subscription's DataType → Error
    - **RequestTypeSubtype**: Type implementing neither ICommand nor IEvent → Warning
    - **RequestTypeSubtype**: Type implementing ICommand → passes
    - All rules pass for correctly configured subscriptions
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter.ServiceActivator/Validation/ConsumerValidationRules.cs` (internal static)
    - Three specifications: PumpHandlerMatch (simple), HandlerRegistered (simple), RequestTypeSubtype (simple)
    - Takes `IAmASubscriberRegistryInspector` as parameter

---

## Phase 6: Validator and Diagnostic Writer

- [x] **TEST + IMPLEMENT: PipelineValidator evaluates all rule sets and aggregates errors**
  - **USE COMMAND**: `/test-first when PipelineValidator validates configuration with errors across all paths then all errors are collected`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_validator_finds_errors_across_paths_should_aggregate_all.cs`
  - Test should verify:
    - Handler pipeline errors, producer errors, and consumer errors all appear in result
    - Warnings are collected separately from errors
    - `IsValid` is false only when errors (not just warnings) are present
    - Validation scales to path: AddBrighter-only → only handler checks run
    - AddBrighter + AddProducers → handler + producer checks run
    - All three paths → all checks run
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/IAmAPipelineValidator.cs`
    - Create `src/Paramore.Brighter/Validation/PipelineValidator.cs`
    - Constructor injection: `PipelineBuilder`, optional publications, optional consumer rules
    - `Validate()` calls `ValidateHandlerPipelines`, `ValidateProducers`, `ValidateConsumers`
    - Uses `EvaluateSpecs<T>` shared helper with `ValidationResultCollector`

- [x] **TEST + IMPLEMENT: PipelineDiagnosticWriter logs handler pipelines, publications, and subscriptions**
  - **USE COMMAND**: `/test-first when diagnostic writer describes pipelines then it logs summary at Information and detail at Debug`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_diagnostic_writer_describes_should_log_summary_and_detail.cs`
  - Test should verify:
    - Information-level log: summary line with counts (e.g. "3 handler pipelines, 2 publications")
    - Debug-level log: per-handler pipeline chain in step order
    - Debug-level log: per-publication mapper type (custom vs default) and transforms
    - Debug-level log: per-subscription pump type, handler chain, mapper, channel, routing key
    - No output when no items are configured for a section
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Validation/IAmAPipelineDiagnosticWriter.cs`
    - Create `src/Paramore.Brighter/Validation/PipelineDiagnosticWriter.cs`
    - Constructor injection: `ILogger`, `PipelineBuilder`, `TransformPipelineBuilder`, optional subscriptions/publications
    - `Describe()` logs sections: Handler Pipelines, Publications, Subscriptions

---

## Phase 7: DI and Hosting Integration

- [x] **TEST + IMPLEMENT: ValidatePipelines() and DescribePipelines() extension methods on IBrighterBuilder**
  - **USE COMMAND**: `/test-first when ValidatePipelines is called then IAmAPipelineValidator is registered in DI`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_validate_pipelines_called_should_register_validator_in_di.cs`
  - Test should verify:
    - `ValidatePipelines()` registers `IAmAPipelineValidator` in DI
    - `ValidatePipelines()` registers `BrighterValidationHostedService` as `IHostedService`
    - `ValidatePipelines()` registers `BrighterPipelineValidationOptions` with `ConsumerOwnsValidation = false`
    - `DescribePipelines()` registers `IAmAPipelineDiagnosticWriter` in DI
    - Both return `IBrighterBuilder` for fluent chaining
    - Requires `IAmASubscriberRegistryInspector` — throws clear error if missing
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `ValidatePipelines()` and `DescribePipelines()` extension methods in `src/Paramore.Brighter.Extensions.DependencyInjection/`
    - Create `BrighterPipelineValidationOptions.cs` in same project
    - Register hosted service, validator, and options

- [x] **TEST + IMPLEMENT: BrighterValidationHostedService runs validation at startup for non-consumer apps**
  - **USE COMMAND**: `/test-first when BrighterValidationHostedService starts without consumers then it runs validation`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_validation_hosted_service_starts_without_consumers_should_validate.cs`
  - Test should verify:
    - `ConsumerOwnsValidation = false` → runs validation and diagnostics
    - `ConsumerOwnsValidation = true` → no-op (ServiceActivatorHostedService handles it)
    - Validation errors throw `PipelineValidationException` preventing startup
    - Warnings are logged but do not prevent startup
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterValidationHostedService.cs`
    - Reads `BrighterPipelineValidationOptions` to decide whether to act
    - Calls `IAmAPipelineValidator.Validate()` and `IAmAPipelineDiagnosticWriter.Describe()`

- [x] **TEST + IMPLEMENT: ServiceActivatorHostedService runs validation before Receive when opted in**
  - **USE COMMAND**: `/test-first when ServiceActivatorHostedService starts with validator registered then it validates before receiving`
  - Test location: `tests/Paramore.Brighter.ServiceActivator.Tests/`
  - Test file: `When_service_activator_starts_with_validator_should_validate_before_receive.cs`
  - Test should verify:
    - `IAmAPipelineValidator` resolved and present → calls `Validate()` then `Receive()`
    - `IAmAPipelineValidator` not registered → goes straight to `Receive()` (backward compatible)
    - `IAmAPipelineDiagnosticWriter` resolved and present → calls `Describe()` before `Receive()`
    - Validation error stops the host (does not call `Receive()`)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `src/Paramore.Brighter.ServiceActivator.Extensions.Hosting/ServiceActivatorHostedService.cs`
    - Add optional constructor parameters: `IAmAPipelineValidator?`, `IAmAPipelineDiagnosticWriter?`
    - In `StartAsync`: if present, call `Describe()` then `Validate().ThrowIfInvalid()` before `_dispatcher.Receive()`

- [x] **TEST + IMPLEMENT: AddConsumers sets ConsumerOwnsValidation flag when validation is opted in**
  - **USE COMMAND**: `/test-first when AddConsumers is called after ValidatePipelines then ConsumerOwnsValidation is true`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_add_consumers_with_validation_should_set_consumer_owns_flag.cs`
  - Test should verify:
    - `ValidatePipelines()` then `AddConsumers()` → `ConsumerOwnsValidation = true`
    - `AddConsumers()` then `ValidatePipelines()` → `ConsumerOwnsValidation = true` (order independent)
    - `AddConsumers()` without `ValidatePipelines()` → no `BrighterPipelineValidationOptions` registered
    - Consumer validation rules are registered when `AddConsumers()` is called with validation
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `AddConsumers()` in `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/`
    - Call `Configure<BrighterPipelineValidationOptions>(o => o.ConsumerOwnsValidation = true)` if options are registered
    - Register `ConsumerValidationRules` with the validator

---

## Phase 8: Configuration Control Flags (FR-14, FR-15)

These tasks add `enabled` and `throwOnError` parameters to the opt-in extension methods, giving developers control over whether validation/diagnostics run and whether errors terminate the host.

- [x] **TEST + IMPLEMENT: ValidatePipelines and DescribePipelines accept enabled flag that skips registration when false**
  - **USE COMMAND**: `/test-first when ValidatePipelines is called with enabled false then no validator or hosted service is registered`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_validate_pipelines_disabled_should_not_register.cs`
  - Test should verify:
    - `ValidatePipelines(enabled: false)` → `IAmAPipelineValidator` is NOT in DI, `BrighterValidationHostedService` is NOT registered
    - `ValidatePipelines(enabled: true)` → registers as before (backward compatible, `true` is the default)
    - `ValidatePipelines()` without argument → registers as before (default is `true`)
    - `DescribePipelines(enabled: false)` → `IAmAPipelineDiagnosticWriter` is NOT in DI, `BrighterDiagnosticHostedService` is NOT registered
    - `DescribePipelines(enabled: true)` → registers as before
    - `DescribePipelines()` without argument → registers as before (default is `true`)
    - Both return `IBrighterBuilder` for fluent chaining regardless of `enabled` value
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `ValidatePipelines()` signature to `ValidatePipelines(bool enabled = true, bool throwOnError = true)` in `src/Paramore.Brighter.Extensions.DependencyInjection/`
    - Update `DescribePipelines()` signature to `DescribePipelines(bool enabled = true)` in same project
    - When `enabled` is `false`, return `builder` immediately without registering any services
    - Store `throwOnError` in `BrighterPipelineValidationOptions.ThrowOnError` via `Configure<>`

- [x] **TEST + IMPLEMENT: ValidatePipelines stores throwOnError flag and hosted services respect it**
  - **USE COMMAND**: `/test-first when ValidatePipelines is called with throwOnError false then validation errors are logged not thrown`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Validation/`
  - Test file: `When_throw_on_error_false_should_log_errors_not_throw.cs`
  - Test should verify:
    - `ValidatePipelines(throwOnError: false)` → `BrighterPipelineValidationOptions.ThrowOnError` is `false`
    - `ValidatePipelines(throwOnError: true)` → `ThrowOnError` is `true` (default)
    - `ValidatePipelines()` without argument → `ThrowOnError` is `true` (default)
    - `BrighterValidationHostedService` with `ThrowOnError = false`: validation errors are logged at `LogLevel.Error`, no exception thrown, startup continues
    - `BrighterValidationHostedService` with `ThrowOnError = true`: validation errors throw `PipelineValidationException` (existing behavior)
    - Warnings are logged at `LogLevel.Warning` regardless of `ThrowOnError` value
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `ThrowOnError` property (default `true`) to `BrighterPipelineValidationOptions`
    - Update `ValidatePipelines()` to store `throwOnError` via `Configure<BrighterPipelineValidationOptions>(o => o.ThrowOnError = throwOnError)`
    - Update `BrighterValidationHostedService.StartAsync()`: read `ThrowOnError` from options; if `true`, call `ThrowIfInvalid()`; if `false`, log each error at `LogLevel.Error`

- [x] **TEST + IMPLEMENT: ServiceActivatorHostedService respects throwOnError flag**
  - **USE COMMAND**: `/test-first when ServiceActivatorHostedService starts with throwOnError false then validation errors are logged not thrown`
  - Test location: `tests/Paramore.Brighter.ServiceActivator.Tests/`
  - Test file: `When_service_activator_throw_on_error_false_should_log_not_throw.cs`
  - Test should verify:
    - `ThrowOnError = false` + validation errors → errors logged at `LogLevel.Error`, `Receive()` is still called
    - `ThrowOnError = true` + validation errors → `PipelineValidationException` thrown, `Receive()` is NOT called
    - `ThrowOnError = false` + no errors → normal startup, `Receive()` called
    - Warnings logged at `LogLevel.Warning` regardless of `ThrowOnError` value
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `ServiceActivatorHostedService.StartAsync()`: read `ThrowOnError` from `BrighterPipelineValidationOptions`; if `true`, call `ThrowIfInvalid()`; if `false`, log each error at `LogLevel.Error` and continue to `_dispatcher.Receive()`

---

## Summary

| Phase | Tasks | Type |
|-------|-------|------|
| 1. Tidy — Move & Extract | 4 | `/tidy-first` |
| 2. Tidy — New Interfaces | 3 | `/tidy-first` |
| 3. Foundation Types | 4 | `/test-first` |
| 4. Pipeline Description Model | 2 | `/test-first` |
| 5. Validation Specifications | 3 | `/test-first` |
| 6. Validator & Writer | 2 | `/test-first` |
| 7. DI & Hosting Integration | 4 | `/test-first` |
| 8. Configuration Control Flags | 3 | `/test-first` |
| **Total** | **25** | |

## Dependencies

```
Phase 1 (tidy: move, extract)
  └── Phase 2 (tidy: interfaces, markers)
        └── Phase 3 (foundation: Specification<T>, ValidationResult, PipelineValidationResult)
              └── Phase 4 (description model: Describe(), HandlerPipelineDescription)
              │     └── Phase 6 (validator + writer use description model)
              └── Phase 5 (specifications: rules using marker interfaces + description model)
                    └── Phase 6 (validator evaluates specifications)
                          └── Phase 7 (DI + hosting: wire everything together)
                                └── Phase 8 (configuration control: enabled/throwOnError flags)
```
