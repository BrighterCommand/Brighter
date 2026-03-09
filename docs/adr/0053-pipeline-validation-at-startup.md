# 53. Pipeline Validation and Diagnostic Report at Startup

Date: 2026-02-25

## Status

Accepted

## Context

**Parent Requirement**: [specs/0023-Pipeline-Validation-At-Startup/requirements.md](../../specs/0023-Pipeline-Validation-At-Startup/requirements.md)

**Scope**: This ADR covers the runtime startup validation and diagnostic report (Layers 1 and 2 from requirements). Roslyn analyzer extensions (Layer 3) are a separate concern that can be addressed in a follow-up ADR.

### The Problem

Setting up Brighter pipelines correctly requires getting several things right simultaneously. Today, most configuration mistakes are only discovered at runtime — sometimes minutes or hours after deployment. Developers need fast feedback.

### Three Configuration Paths

Brighter is not only a service activator for consuming messages. It has three distinct configuration paths, each progressively building on the previous:

1. **`AddBrighter()`** — Command Dispatcher (CQRS). Configures handler pipelines, subscriber registry, mapper registry, and command processor via `IBrighterBuilder`. Used by all Brighter applications.

2. **`AddProducers()`** — Outgoing Messages. Adds `Publication` definitions, a `ProducerRegistry`, and the outbox-producer mediator. Called on `IBrighterBuilder` when the application sends messages to a broker.

3. **`AddConsumers()`** — Incoming Messages (Service Activator). Adds `Subscription` definitions, a `Dispatcher`, and message pumps (Reactor/Proactor). This is the only path that involves the `ServiceActivator` project.

A developer may use only path 1 (pure CQRS in-process), paths 1+2 (sends messages), or all three (full messaging). Validation must work correctly for any combination.

### What Can Go Wrong at Each Level

**AddBrighter**: Handler attributes ordered such that a backstop (Reject/Defer/DontAck) comes after a resilience pipeline, rendering it ineffective. Sync attributes on async handlers (or vice versa) are silently ignored. Only discovered when the pipeline runs.

**AddProducers**: `Publication.RequestType` not set (causes `ConfigurationException` at `Post()`/`Deposit()` time). No mapper registered for a published type (causes `ArgumentOutOfRangeException` at send time). Transform pipeline misconfigured. Developer has no visibility into which mapper (custom vs default) resolves for outgoing messages.

**AddConsumers**: Subscription uses `MessagePumpType.Reactor` but handler implements `IHandleRequestsAsync<T>` — the sync factory can't find it, leading to "no handlers found". `RequestType` implements `ICommand` but message arrives as `MT_EVENT` (or vice versa) — today only a warning is logged. No handler registered for a subscription's `RequestType`. No visibility into incoming mapper resolution.

### Prior Art

- **ASP.NET Core**: `ValidateOnBuild` runs at `IServiceProvider` build time — not tied to any specific hosted service.
- **Wolverine**: `describe` covers all messaging configuration (listeners, routing, sending endpoints) in one report.
- **Rebus**: `LogPipeline` shows both incoming and outgoing pipeline chains.
- **MassTransit**: `GetProbeResult()` covers the entire bus configuration via a visitor pattern.

### Forces

- Validation must cover all three configuration paths, not just consumers.
- The `IBrighterBuilder` interface is the common extension point across all paths.
- Only `AddConsumers` involves the ServiceActivator project — handler and producer validation must not depend on it.
- The solution must integrate naturally with .NET hosting (`IHostedService`, `ILogger`).
- Validation must be opt-in (NFR-1) and non-breaking (NFR-3).

## Decision

We introduce validation and diagnostic reporting as a layered architecture that mirrors Brighter's own three configuration paths. Each layer validates and reports on its own concerns. The layers compose — enabling validation for consumers automatically includes the handler and producer checks too.

### Architecture Overview

```
                     IBrighterBuilder
                    ┌─────────────────────────────────────┐
                    │  .ValidatePipelines()                │
                    │  .DescribePipelines()                │
                    │                                     │
                    │  Registers IAmAPipelineValidator     │
                    │  Registers IAmAPipelineDiagnosticWriter│
                    └─────────────────────────────────────┘
                           │
            ┌──────────────┼──────────────┐
            ▼              ▼              ▼
     AddBrighter()   AddProducers()  AddConsumers()
     ┌──────────┐    ┌───────────┐   ┌────────────┐
     │ Handler  │    │ Producer  │   │ Consumer   │
     │ Pipeline │    │ Pipeline  │   │ Pipeline   │
     │ Checks   │    │ Checks    │   │ Checks     │
     └──────────┘    └───────────┘   └────────────┘
            │              │              │
            └──────────────┼──────────────┘
                           ▼
              PipelineValidationResult
                           │
                ┌──────────┴──────────┐
                │                     │
           IsValid → continue    !IsValid → throw
                                 PipelineValidationException
                                 (: ConfigurationException)


Integration Points:
─────────────────────────────────────────────────────

  With ServiceActivator          Without ServiceActivator
  (AddConsumers used)            (pure CQRS or producers only)
  ┌─────────────────────┐        ┌─────────────────────────┐
  │ ServiceActivator     │        │ BrighterValidation      │
  │ HostedService        │        │ HostedService            │
  │ .StartAsync():       │        │ .StartAsync():          │
  │   Describe()         │        │   Describe()            │
  │   Validate()         │        │   Validate()            │
  │   dispatcher.Receive │        │   (no dispatcher)       │
  └─────────────────────┘        └─────────────────────────┘
```

### Key Roles and Responsibilities

Following Responsibility-Driven Design:

#### 1. `IAmAPipelineValidator` — Service Provider

**Responsibility**: Deciding whether the pipeline configuration is valid across all configured paths.

```csharp
// In Paramore.Brighter
public interface IAmAPipelineValidator
{
    PipelineValidationResult Validate();
}
```

The validator is parameterless — it has all its dependencies via constructor injection (subscriber registry, mapper registries, optionally subscriptions and publications). It validates everything that has been configured.

**Composite Implementation**: The validator is composed of `ISpecification<T>` instances — each specification encapsulates both the predicate (what to check) and the validation metadata (what to report when it fails). Specifications are grouped into rule sets by configuration path:

```csharp
// In Paramore.Brighter (core handler rules)
internal static class HandlerPipelineValidationRules
{
    // Returns ISpecification<HandlerPipelineDescription> instances for:
    // FR-1: Backstop attribute ordering
    // FR-2: Sync/async attribute consistency
    public static IEnumerable<ISpecification<HandlerPipelineDescription>> Rules();
}

// In Paramore.Brighter (producer rules, same project as AddProducers)
internal static class ProducerValidationRules
{
    // Returns ISpecification<Publication> instances for:
    // FR-4: Publication.RequestType validation
    public static IEnumerable<ISpecification<Publication>> Rules();
}

// In Paramore.Brighter.ServiceActivator (consumer rules)
internal static class ConsumerValidationRules
{
    // Returns ISpecification<Subscription> instances for:
    // FR-6: Pump ↔ handler type match
    // FR-7: Handler registered for subscription
    // FR-8: MessageType ↔ IRequest subtype
    public static IEnumerable<ISpecification<Subscription>> Rules(
        IAmASubscriberRegistry subscriberRegistry);
}
```

Each rule set is registered only when its corresponding configuration path is used. The top-level `PipelineValidator` evaluates all registered specifications and uses the visitor pattern to collect detailed findings from failed specifications.

#### 2. `IAmAPipelineDiagnosticWriter` — Interfacer

**Responsibility**: Producing a human-readable diagnostic report of all configured pipelines.

```csharp
// In Paramore.Brighter
public interface IAmAPipelineDiagnosticWriter
{
    void Describe();
}
```

Like the validator, the writer is composed of sections that correspond to configuration paths:

At **Information** level — a single summary line:
```
Brighter: 3 handler pipelines, 2 publications, 5 subscriptions configured
```

At **Debug** level — full detail organized by section:

```
=== Handler Pipelines ===
  OrderCreatedHandler (async)
    Pipeline: [DeferMessageOnErrorAsync(0)] → [UseResiliencePipelineAsync(1, "OrderRetry")] → OrderCreatedHandler
  PaymentReceivedHandler (async)
    Pipeline: [RejectMessageOnErrorAsync(0)] → PaymentReceivedHandler

=== Publications (Outgoing) ===
  OrderCreated → order-created (topic)
    Mapper:     OrderCreatedMessageMapper (custom)
    Transforms: [CompressPayload(0)]
  PaymentReceived → payment-received (topic)
    Mapper:     JsonMessageMapper<PaymentReceived> (default)
    Transforms: (none)

=== Subscriptions (Incoming) ===
  OrderCreated ← order-created-queue [Proactor]
    Handler:  OrderCreatedHandler (async)
    Pipeline: [DeferMessageOnErrorAsync(0)] → [UseResiliencePipelineAsync(1)] → OrderCreatedHandler
    Mapper:   OrderCreatedMessageMapper (custom)
  PaymentReceived ← payment-received-queue [Proactor]
    Handler:  PaymentReceivedHandler (async)
    Pipeline: [RejectMessageOnErrorAsync(0)] → PaymentReceivedHandler
    Mapper:   JsonMessageMapper<PaymentReceived> (default)
```

The report uses `ILogger` so it integrates with whatever logging infrastructure the developer has.

#### 3. `PipelineValidationResult` — Information Holder

**Responsibility**: Knowing the outcome of validation.

```csharp
// In Paramore.Brighter
public class PipelineValidationResult
{
    public IReadOnlyList<ValidationError> Errors { get; }
    public IReadOnlyList<ValidationError> Warnings { get; }
    public bool IsValid => Errors.Count == 0;

    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new PipelineValidationException(this);
    }

    // Compose results from multiple rule sets
    public static PipelineValidationResult Combine(
        params PipelineValidationResult[] results);
}

/// <summary>
/// Thrown when pipeline validation finds one or more errors.
/// Extends ConfigurationException so existing catch blocks that handle Brighter
/// configuration errors (the established Brighter convention) will also catch
/// validation failures. The PipelineValidationResult is available for programmatic
/// inspection, and the exception message includes all errors with their source context.
/// </summary>
public class PipelineValidationException : ConfigurationException
{
    public PipelineValidationResult ValidationResult { get; }

    public PipelineValidationException(PipelineValidationResult result)
        : base(FormatMessage(result))
    {
        ValidationResult = result;
    }

    private static string FormatMessage(PipelineValidationResult result)
    {
        var errorLines = result.Errors
            .Select(e => $"  [{e.Source}] {e.Message}");
        return $"Brighter pipeline validation failed with {result.Errors.Count} error(s):\n"
            + string.Join("\n", errorLines);
    }
}
```

#### 4. `ValidationError` — Information Holder

**Responsibility**: Knowing the details of one validation finding. This type is generic (not pipeline-specific) so that `Specification<T>` can carry validation metadata without coupling to the pipeline domain.

```csharp
// In Paramore.Brighter
public class ValidationError
{
    public ValidationSeverity Severity { get; }
    public string Source { get; }   // e.g. "Subscription 'OrderCreated'" or "Handler 'OrderCreatedHandler'"
    public string Message { get; }  // actionable description
}

public enum ValidationSeverity { Error, Warning }
```

#### 5. `Specification<T>` — Enhanced with Validation Support (Tidy First + Behavioral)

`ISpecification<T>` and `Specification<T>` currently live in `Paramore.Brighter.Mediator`. They are a general-purpose implementation of the Specification pattern (predicate composition via `And()`, `Or()`, `Not()`) and have no inherent dependency on the Mediator workflow — they just happened to be introduced there for `ExclusiveChoice<TData>` branching.

We move both types to `Paramore.Brighter` (namespace `Paramore.Brighter`) so they are available to the core library. `Paramore.Brighter.Mediator` already depends on `Paramore.Brighter`, so existing consumers (e.g. `ExclusiveChoice<TData>`) continue to work with a `using` update. The move is a structural-only tidy committed separately.

Beyond the move, we enhance `Specification<T>` so that each specification carries its own validation metadata. This eliminates the need for a separate `ValidationRule<T>` coordinator — the specification itself knows what to report when it fails. We use the **visitor pattern** to allow calling code to interrogate the specification graph for detailed validation results after `IsSatisfiedBy` returns `false`.

**Updated interface:**

```csharp
// In Paramore.Brighter
public interface ISpecification<TData>
{
    bool IsSatisfiedBy(TData entity);

    /// <summary>
    /// Accepts a visitor that can traverse the specification graph. Calling code
    /// invokes this after IsSatisfiedBy returns false to collect detailed results.
    /// </summary>
    TResult Accept<TResult>(ISpecificationVisitor<TData, TResult> visitor);

    ISpecification<TData> And(ISpecification<TData> other);
    ISpecification<TData> Or(ISpecification<TData> other);
    ISpecification<TData> Not();
    ISpecification<TData> Not(Func<TData, ValidationError> errorFactory);
    ISpecification<TData> AndNot(ISpecification<TData> other);
    ISpecification<TData> OrNot(ISpecification<TData> other);
}
```

**`Specification<T>` — three constructors:**

```csharp
// In Paramore.Brighter
public class Specification<T>(Func<T, bool> expression) : ISpecification<T>
{
    private readonly Func<T, bool> _expression = expression
        ?? throw new ArgumentNullException(nameof(expression));
    private readonly Func<T, ValidationError>? _errorFactory;
    private readonly Func<T, IEnumerable<ValidationResult>>? _resultEvaluator;
    private IReadOnlyList<ValidationResult> _lastResults = [];

    /// <summary>
    /// Pure predicate — no validation metadata. Used by non-validation consumers
    /// (e.g. ExclusiveChoice workflow branching). The visitor returns empty results.
    /// This is the existing constructor, unchanged.
    /// </summary>
    public Specification(Func<T, bool> expression) : this(expression) { }

    /// <summary>
    /// Simple rule: a predicate paired with an error factory. IsSatisfiedBy evaluates
    /// the predicate; on failure, stores a single ValidationResult with the error.
    /// Yields zero or one findings.
    /// </summary>
    public Specification(Func<T, bool> expression, Func<T, ValidationError> errorFactory)
        : this(expression)
    {
        _errorFactory = errorFactory;
    }

    /// <summary>
    /// Collapsed rule: a single function that evaluates the entity and returns zero
    /// or more ValidationResults. IsSatisfiedBy derives its bool from the results —
    /// returns true only when all results indicate success (or the enumerable is empty).
    /// Used for per-element rules where a single entity (e.g. a handler pipeline)
    /// contains multiple elements that each need individual error reporting.
    /// </summary>
    public Specification(Func<T, IEnumerable<ValidationResult>> resultEvaluator)
        : this(_ => true) // predicate is derived, not used directly
    {
        _resultEvaluator = resultEvaluator;
    }

    public bool IsSatisfiedBy(T entity)
    {
        if (_resultEvaluator != null)
        {
            // Collapsed mode: evaluate and store all results
            try
            {
                _lastResults = _resultEvaluator(entity).ToList();
            }
            catch (Exception ex)
            {
                _lastResults = [ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Error,
                    entity?.ToString() ?? "(unknown)",
                    $"Rule evaluation failed: {ex.Message}"))];
            }
            return _lastResults.All(r => r.Success);
        }

        // Simple or pure-predicate mode
        bool satisfied;
        try
        {
            satisfied = _expression(entity);
        }
        catch (Exception ex)
        {
            _lastResults = [ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Error,
                entity?.ToString() ?? "(unknown)",
                $"Rule evaluation failed: {ex.Message}"))];
            return false;
        }

        if (!satisfied && _errorFactory != null)
            _lastResults = [ValidationResult.Fail(_errorFactory(entity))];
        else
            _lastResults = [];

        return satisfied;
    }

    /// <summary>
    /// Returns the stored validation results from the most recent IsSatisfiedBy call.
    /// The visitor uses this to collect results from leaf nodes.
    /// </summary>
    internal IReadOnlyList<ValidationResult> LastResults => _lastResults;

    public TResult Accept<TResult>(ISpecificationVisitor<T, TResult> visitor)
        => visitor.Visit(this);

    // And(), Or(), Not(), AndNot(), OrNot() — composition methods
    // return AndSpecification, OrSpecification, NotSpecification etc.
    // that implement ISpecification<T> and delegate to children via the visitor.
}
```

The exception-catching wrapper in `IsSatisfiedBy` ensures that even programming errors in handler types — such as a type that implements neither `IHandleRequests` nor `IHandleRequestsAsync` — produce a structured Error-severity finding rather than crashing startup with an unhandled exception.

**Statefulness note**: `_lastResults` is set during `IsSatisfiedBy` and read by the visitor. This makes the specification stateful, but startup validation is single-threaded so this is safe. Non-validation consumers (e.g. `ExclusiveChoice`) never call the visitor and are unaffected.

#### 6. `ValidationResult` and the Visitor Pattern

**`ValidationResult`** — pairs a success/failure bool with an optional `ValidationError`:

```csharp
// In Paramore.Brighter
public class ValidationResult
{
    public bool Success { get; }
    public ValidationError? Error { get; }

    private ValidationResult(bool success, ValidationError? error)
    {
        Success = success;
        Error = error;
    }

    public static ValidationResult Ok() => new(true, null);
    public static ValidationResult Fail(ValidationError error) => new(false, error);
}
```

**`ISpecificationVisitor<TData, TResult>`** — the visitor interface that traverses the specification graph. For validation, `TResult` is `IEnumerable<ValidationResult>`:

```csharp
// In Paramore.Brighter
public interface ISpecificationVisitor<TData, TResult>
{
    TResult Visit(Specification<TData> specification);
    TResult Visit(AndSpecification<TData> specification);
    TResult Visit(OrSpecification<TData> specification);
    TResult Visit(NotSpecification<TData> specification);
}
```

**`ValidationResultCollector<TData>`** — a concrete visitor that collects all stored `ValidationResult` instances from the specification graph:

```csharp
// In Paramore.Brighter
public class ValidationResultCollector<TData> : ISpecificationVisitor<TData, IEnumerable<ValidationResult>>
{
    public IEnumerable<ValidationResult> Visit(Specification<TData> spec)
        => spec.LastResults;

    public IEnumerable<ValidationResult> Visit(AndSpecification<TData> spec)
        => spec.Left.Accept(this).Concat(spec.Right.Accept(this));

    public IEnumerable<ValidationResult> Visit(OrSpecification<TData> spec)
        => spec.Left.Accept(this).Concat(spec.Right.Accept(this));

    public IEnumerable<ValidationResult> Visit(NotSpecification<TData> spec)
        => spec.LastResults; // Not stores its own ValidationResult (see below)
}
```

**Composition and the visitor** — `And`, `Or`, and `Not` are thin wrappers that delegate `IsSatisfiedBy` to their children and expose them for the visitor:

- **`AndSpecification<T>`**: `IsSatisfiedBy` returns `left.IsSatisfiedBy(entity) && right.IsSatisfiedBy(entity)`. On failure, the visitor walks both children and collects all failed results.
- **`OrSpecification<T>`**: `IsSatisfiedBy` returns `left.IsSatisfiedBy(entity) || right.IsSatisfiedBy(entity)`. On failure (both children failed), the visitor collects from both.
- **`NotSpecification<T>`**: `IsSatisfiedBy` returns `!inner.IsSatisfiedBy(entity)`. When negation fails (the inner spec *succeeded*), the inner spec has no stored errors. Therefore `Not` needs its own error — provided via `Not(Func<T, ValidationError> errorFactory)`. The parameterless `Not()` remains for non-validation uses (e.g. `ExclusiveChoice`) and produces no validation metadata.

**Flow** — calling code uses the visitor only when it wants detailed information:

```csharp
if (!specification.IsSatisfiedBy(entity))
{
    // Calling code decides it wants detail
    var collector = new ValidationResultCollector<HandlerPipelineDescription>();
    var results = specification.Accept(collector);
    findings.AddRange(results.Where(r => !r.Success).Select(r => r.Error!));
}
```

#### 7. Marker Interfaces for Handler Classification

Validation specifications need to distinguish backstop handlers from resilience handlers. Rather than hardcoding a list of known Brighter attribute types or using naming conventions, we follow the existing `IAmA*` pattern with marker interfaces on the handler types produced by `RequestHandlerAttribute.GetHandlerType()`:

```csharp
// In Paramore.Brighter — marker interfaces for handler classification
public interface IAmABackstopHandler { }
public interface IAmAResilienceHandler { }
```

Brighter's built-in handlers implement the appropriate interface:
- `IAmABackstopHandler`: `RejectMessageOnErrorHandler<T>`, `DeferMessageOnErrorHandler<T>`, `DontAckOnErrorHandler<T>` (and their async counterparts)
- `IAmAResilienceHandler`: `UseResiliencePipelineHandler<T>` (and async counterpart)

Third-party handlers (e.g. custom Polly wrappers) can implement these interfaces to participate in validation. This is the standard Brighter extensibility pattern — role interfaces express what a type does, not what it is.

**How specifications are defined** — simple specifications use the two-argument constructor (predicate + error factory) expressing the *valid* condition and what to report when it fails. Per-element specifications use the collapsed constructor (`Func<T, IEnumerable<ValidationResult>>`) that can store zero or more findings, so the developer can identify exactly which attribute is misconfigured:

```csharp
// Example: backstop and async consistency specifications with per-attribute error messages
internal static class HandlerPipelineValidationRules
{
    public static IEnumerable<ISpecification<HandlerPipelineDescription>> Rules()
    {
        // Per-pair analysis — yields one Warning per misordered backstop/resilience pair.
        // Uses the collapsed constructor: the function returns ValidationResult per bad pair,
        // and IsSatisfiedBy derives its bool from the results.
        yield return new Specification<HandlerPipelineDescription>(d =>
        {
            var backstops = d.BeforeSteps.Where(s =>
                typeof(IAmABackstopHandler).IsAssignableFrom(s.HandlerType));
            var resilience = d.BeforeSteps.Where(s =>
                typeof(IAmAResilienceHandler).IsAssignableFrom(s.HandlerType));

            return backstops.SelectMany(b => resilience
                .Where(r => b.Step > r.Step)
                .Select(r => ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    $"Handler '{d.HandlerType.Name}'",
                    $"'{b.AttributeType.Name}' at step {b.Step} is after " +
                    $"'{r.AttributeType.Name}' at step {r.Step} — " +
                    "in Brighter, lower step values are outer wrappers, so the backstop " +
                    "will never execute on failure"))));
        });

        // Per-attribute analysis — yields one Error per mismatched sync/async attribute.
        // Also catches handler types that implement neither interface and reports them
        // as Error-severity findings rather than throwing (see Specification<T>.IsSatisfiedBy
        // exception handling).
        yield return new Specification<HandlerPipelineDescription>(d =>
        {
            return d.BeforeSteps.Concat(d.AfterSteps).SelectMany(step =>
            {
                var isSync = typeof(IHandleRequests).IsAssignableFrom(step.HandlerType);
                var isAsync = typeof(IHandleRequestsAsync).IsAssignableFrom(step.HandlerType);

                if (!isSync && !isAsync)
                {
                    return [ValidationResult.Fail(new ValidationError(
                        ValidationSeverity.Error,
                        $"Handler '{d.HandlerType.Name}'",
                        $"Pipeline step '{step.HandlerType.FullName}' at step {step.Step} " +
                        "implements neither IHandleRequests nor IHandleRequestsAsync"))];
                }

                if (d.IsAsync != isAsync)
                {
                    return [ValidationResult.Fail(new ValidationError(
                        ValidationSeverity.Error,
                        $"Handler '{d.HandlerType.Name}'",
                        d.IsAsync
                            ? $"Async handler uses sync attribute '{step.AttributeType.Name}' " +
                              $"at step {step.Step} — it will be silently ignored"
                            : $"Sync handler uses async attribute '{step.AttributeType.Name}' " +
                              $"at step {step.Step} — it will be silently ignored"))];
                }

                return [];
            });
        });
    }
}
```

**How the validator evaluates specifications**:

```csharp
// PipelineValidator evaluates all specifications across all configured paths
public PipelineValidationResult Validate()
{
    var findings = new List<ValidationError>();
    var collector = new ValidationResultCollector<HandlerPipelineDescription>();

    // Handler pipeline specifications
    foreach (var description in _pipelineBuilder.Describe())
    {
        foreach (var spec in HandlerPipelineValidationRules.Rules())
        {
            if (!spec.IsSatisfiedBy(description))
            {
                findings.AddRange(
                    spec.Accept(collector)
                        .Where(r => !r.Success)
                        .Select(r => r.Error!));
            }
        }
    }

    // Producer specifications (if configured)
    if (_publications != null)
    {
        var pubCollector = new ValidationResultCollector<Publication>();
        foreach (var publication in _publications)
        {
            foreach (var spec in ProducerValidationRules.Rules())
            {
                if (!spec.IsSatisfiedBy(publication))
                {
                    findings.AddRange(
                        spec.Accept(pubCollector)
                            .Where(r => !r.Success)
                            .Select(r => r.Error!));
                }
            }
        }
    }

    // Consumer specifications (if configured)
    // ...same pattern with ConsumerValidationRules.Rules()

    return new PipelineValidationResult(findings);
}
```

Each specification is one rule. **Simple specifications** use the two-argument constructor (predicate + error factory) and produce zero or one findings. **Collapsed specifications** use the `Func<T, IEnumerable<ValidationResult>>` constructor and can produce zero or more findings — enabling per-attribute detail (e.g. identifying which specific attribute is misconfigured). Both modes catch exceptions during evaluation and surface them as Error-severity findings. Calling code evaluates `IsSatisfiedBy` first; only on failure does it invoke the visitor to collect the detailed results.

### Opt-In Configuration via IBrighterBuilder

Developers opt in through the existing `IBrighterBuilder` interface, which is common to all three paths:

```csharp
// Extension methods on IBrighterBuilder
builder.Services.AddBrighter(options => { /* ... */ })
    .AutoFromAssemblies()
    .ValidatePipelines()    // enable startup validation
    .DescribePipelines();   // enable diagnostic report

// If using producers
builder.Services.AddBrighter(options => { /* ... */ })
    .AutoFromAssemblies()
    .AddProducers(config => { /* ... */ })
    .ValidatePipelines()
    .DescribePipelines();

// If using consumers (full messaging)
builder.Services.AddBrighter(options => { /* ... */ })
    .AutoFromAssemblies()
    .AddConsumers(config => { /* ... */ })
    .ValidatePipelines()
    .DescribePipelines();
```

The extension methods register:
- `IAmAPipelineValidator` and/or `IAmAPipelineDiagnosticWriter` in DI.
- A dedicated `BrighterValidationHostedService` (an `IHostedService`) that runs validation at startup.

### Integration Points and Hosted Service Coordination

When both `BrighterValidationHostedService` and `ServiceActivatorHostedService` are registered, validation must run exactly once. Rather than shared mutable state (flags, singletons), coordination is based on **DI registration presence** — which is deterministic and immutable after the service provider is built:

- `ValidatePipelines()` always registers `IAmAPipelineValidator` in DI and always registers `BrighterValidationHostedService`.
- `AddConsumers()` always registers `IDispatcher` in DI (via `ServiceCollectionExtensions`).

The two hosted services use optional DI resolution to decide who acts:

**`BrighterValidationHostedService.StartAsync()`**: Resolves `IDispatcher?` (optional) from DI. If `IDispatcher` is present, `ServiceActivatorHostedService` will handle validation — this service becomes a no-op. If absent (pure CQRS or producers only), it runs validation and diagnostics.

**`ServiceActivatorHostedService.StartAsync()`**: Resolves `IAmAPipelineValidator?` and `IAmAPipelineDiagnosticWriter?` (both optional) from DI. If present, calls them before `_dispatcher.Receive()`. If absent (validation not opted in), proceeds directly to `Receive()`.

```
Scenario 1: Pure CQRS / Producers Only
  BrighterValidationHostedService:
    IDispatcher? → null → RUN validation
  (no ServiceActivatorHostedService)

Scenario 2: Full Messaging, Validation Opted In
  BrighterValidationHostedService:
    IDispatcher? → present → NO-OP
  ServiceActivatorHostedService:
    IAmAPipelineValidator? → present → RUN validation
    _dispatcher.Receive()

Scenario 3: Full Messaging, Validation Not Opted In
  (no BrighterValidationHostedService)
  ServiceActivatorHostedService:
    IAmAPipelineValidator? → null → SKIP
    _dispatcher.Receive()
```

This requires no shared mutable state — each service makes a local decision based on what DI contains. `IHostedService` startup order is deterministic (registration order), but the design does not depend on ordering.

### Core Design: Pipeline Description Model (Dry Run)

The `PipelineBuilder` today already separates two internal phases:

1. **Attribute discovery** — Pure reflection. `FindHandlerMethod()` locates the `Handle`/`HandleAsync` method, `GetOtherHandlersInPipeline()` extracts `RequestHandlerAttribute` custom attributes, and the results are sorted by `Step` and cached in static `ConcurrentDictionary`s (`s_preAttributesMemento`, `s_postAttributesMemento`).

2. **Handler instantiation** — Creates handler instances via factory, sets `Context`, calls `InitializeFromAttributeParams()`, and chains via `SetSuccessor()`.

The key insight is that phase 1 contains all the information needed for both validation and diagnostic reporting. We add a **dry-run mode** to the pipeline builders that executes phase 1 only and emits a descriptive model. Both the validator and writer consume this model — one to check for errors, the other to format the report.

#### The Pipeline Description Model

```csharp
// In Paramore.Brighter — the descriptive model
public class HandlerPipelineDescription
{
    public Type RequestType { get; }
    public Type HandlerType { get; }
    public bool IsAsync { get; }
    public IReadOnlyList<PipelineStepDescription> BeforeSteps { get; }
    public IReadOnlyList<PipelineStepDescription> AfterSteps { get; }
    public bool HasGlobalInbox { get; }
}

public class PipelineStepDescription
{
    public Type AttributeType { get; }         // e.g. typeof(RejectMessageOnErrorAsyncAttribute)
    public Type HandlerType { get; }           // from attribute.GetHandlerType()
    public int Step { get; }
    public HandlerTiming Timing { get; }
    public object?[] InitializerParams { get; } // e.g. policy name for UseResiliencePipeline
}

public class TransformPipelineDescription
{
    public Type RequestType { get; }
    public Type MapperType { get; }
    public bool IsDefaultMapper { get; }
    public IReadOnlyList<TransformStepDescription> WrapTransforms { get; }    // outgoing
    public IReadOnlyList<TransformStepDescription> UnwrapTransforms { get; }  // incoming
}

public class TransformStepDescription
{
    public Type AttributeType { get; }
    public Type TransformType { get; }
    public int Step { get; }
}
```

#### Dry Run on PipelineBuilder

`PipelineBuilder` gains `Describe()` methods alongside the existing `Build()`:

```csharp
public class PipelineBuilder<TRequest> where TRequest : class, IRequest
{
    // Existing — instantiates handlers, chains them
    public Pipelines<TRequest> Build(TRequest request, IRequestContext requestContext);
    public AsyncPipelines<TRequest> BuildAsync(TRequest request, IRequestContext ctx, bool continueOnCaptured);

    // New — describes a single request type's pipeline without instantiation
    public IEnumerable<HandlerPipelineDescription> Describe(Type requestType);

    // New — describes all registered pipelines without instantiation
    public IEnumerable<HandlerPipelineDescription> Describe();

    // New — describe-only constructor (no handler factory needed)
    public PipelineBuilder(
        IAmASubscriberRegistry subscriberRegistry,
        InboxConfiguration? inboxConfiguration = null);
}
```

**Two `Describe` overloads**: `Describe(Type requestType)` produces descriptions for a single request type. The parameterless `Describe()` enumerates **all** registered request types from the subscriber registry and yields descriptions for each. This is the method the validator and writer call — they do not need to know how to iterate the registry themselves:

```csharp
// Parameterless Describe() — iterates all registered request types
public IEnumerable<HandlerPipelineDescription> Describe()
{
    foreach (var requestType in _subscriberRegistry.GetRegisteredRequestTypes())
    {
        foreach (var description in Describe(requestType))
            yield return description;
    }
}
```

The subscriber registry exposes `GetRegisteredRequestTypes()` for this purpose (see the Subscriber Registry Inspection section below for details).

**Describe-only construction**: `Describe()` only does Phase 1 (pure reflection) — it never calls the handler factory. A new **public** constructor accepts just the subscriber registry and inbox configuration, omitting the factory. This follows the existing pattern — `PipelineBuilder` already has two public constructors (one taking `IAmAHandlerFactorySync`, the other `IAmAHandlerFactoryAsync`), and the factory fields (`_syncHandlerFactory`, `_asyncHandlerFactory`) are already nullable. The describe-only constructor is a natural third overload that leaves both factories null. Making it public also ensures it is testable without `[InternalsVisibleTo]`. The validator constructs a single describe-only instance and calls the parameterless `Describe()`. The same approach applies to `TransformPipelineBuilder` and `TransformPipelineBuilderAsync`.

**Why `Describe(Type)` stays on `PipelineBuilder<TRequest>`**: `Describe(Type requestType)` is a non-generic method that accepts `Type` and ignores the class's `TRequest` parameter. This is a deliberate trade-off — the two reasons for keeping it co-located rather than extracting to a separate class:

1. **Drift prevention** — the describe path and the build path share the same code (`HandlerMethodDiscovery`, `GetOtherHandlersInPipeline()`, static attribute caches), guaranteeing they stay in sync as the pipeline evolves. Co-location on the same class makes this relationship explicit to future maintainers.
2. **No runtime instance** — the validator operates on `Type` objects at registration time, not on request instances, so a generic constraint buys nothing here. The `Type requestType` parameter is the natural API for startup-time inspection.

`Describe(Type requestType)` executes phase 1 only:

1. Queries the subscriber registry inspector for handler types (via `IAmASubscriberRegistryInspector.GetHandlerTypes(Type)` — see below).
2. For each handler type, finds the handler method **statically** — the same `GetMethods().Where(m => m.Name == "Handle"/"HandleAsync")` logic that `FindHandlerMethod()` uses, but operating on the `Type` rather than an instance.
3. Calls `GetCustomAttributes<RequestHandlerAttribute>(true)` on the method.
4. Separates into Before/After by `Timing`, sorts by `Step`.
5. Checks for global inbox attributes.
6. Returns the model — **no factory calls, no handler instantiation, no `SetSuccessor` chaining**.

The method reuses the existing static caches (`s_preAttributesMemento`, `s_postAttributesMemento`), so if `Build()` later runs for the same handler types the attribute reflection is already cached.

#### Dry Run on TransformPipelineBuilder

Similarly, `TransformPipelineBuilder` gains describe methods:

```csharp
public class TransformPipelineBuilder
{
    // Existing — instantiates mappers and transforms
    public WrapPipeline<TRequest> BuildWrapPipeline<TRequest>();
    public UnwrapPipeline<TRequest> BuildUnwrapPipeline<TRequest>();

    // New — describes mapper resolution and transforms without instantiation
    public TransformPipelineDescription DescribeTransforms<TRequest>();
}
```

`DescribeTransforms()` checks the mapper registry for which mapper type resolves (and whether it's the default), then reflects on the mapper type's `MapToMessage`/`MapToRequest` methods for `WrapWithAttribute`/`UnwrapWithAttribute` — all without calling the transformer factory.

#### Relationship to Existing Build

```
                 PipelineBuilder
                 ┌─────────────────────────────────┐
                 │                                 │
  Describe()     │   Phase 1: Attribute Discovery  │     Build()
  ────────────►  │   (reflection, cached)          │  ────────────►
                 │              │                   │
                 │              ▼                   │
  Returns model  │   ┌─────────────────────┐       │   Phase 2: Instantiation
  ◄────────────  │   │ HandlerPipeline     │       │   (factory, chaining)
                 │   │ Description         │       │   ────────────►
                 │   └─────────────────────┘       │
                 │                                 │   Returns Pipelines<T>
                 └─────────────────────────────────┘   ◄────────────
```

The describe model is the single source of truth for both validation and reporting. Neither the validator nor the writer needs to duplicate any reflection logic — they consume the model.

#### Static Method Discovery (Tidy First)

Today, handler method discovery is duplicated as instance methods: `RequestHandler<T>.FindHandlerMethod()` calls `GetType().GetMethods()` and filters for `Handle(TRequest)`, while `RequestHandlerAsync<T>.FindHandlerMethod()` does the same for `HandleAsync(TRequest, CancellationToken)`. Similarly, `TransformPipelineBuilder` has instance methods `FindMapToMessage()` and `FindMapToRequest()` (and their async counterparts) that locate mapper methods on a mapper instance via `GetType()`.

For the dry run, we need the same logic operating on a `Type` rather than an instance. Rather than duplicating these methods and leaving "tidy later" debt, we introduce static utilities with the canonical logic and **refactor the existing instance methods to delegate to them** in the same change. This gives us a single source of truth from day one.

```csharp
// In Paramore.Brighter (internal static utility)
internal static class HandlerMethodDiscovery
{
    /// <summary>
    /// Finds the Handle or HandleAsync method on a handler type for a given request type.
    /// This is the single source of truth — both the dry-run Describe() and the existing
    /// instance FindHandlerMethod() methods delegate here.
    /// </summary>
    internal static MethodInfo? FindHandlerMethod(Type handlerType, Type requestType)
    {
        if (typeof(IHandleRequestsAsync).IsAssignableFrom(handlerType))
        {
            return handlerType.GetMethods()
                .Where(m => m.Name == "HandleAsync")
                .SingleOrDefault(m => m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType == requestType
                    && m.GetParameters()[1].ParameterType == typeof(CancellationToken));
        }
        else
        {
            return handlerType.GetMethods()
                .Where(m => m.Name == "Handle")
                .SingleOrDefault(m => m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == requestType);
        }
    }
}
```

The existing instance methods become thin delegates:

```csharp
// In RequestHandler<TRequest>
internal MethodInfo FindHandlerMethod()
    => HandlerMethodDiscovery.FindHandlerMethod(GetType(), typeof(TRequest))
        ?? throw new InvalidOperationException($"Could not find Handle method on {GetType().Name}");

// In RequestHandlerAsync<TRequest>
internal MethodInfo FindHandlerMethod()
    => HandlerMethodDiscovery.FindHandlerMethod(GetType(), typeof(TRequest))
        ?? throw new InvalidOperationException($"Could not find HandleAsync method on {GetType().Name}");
```

The same approach applies to mapper method discovery. Today, `TransformPipelineBuilder` (sync) and `TransformPipelineBuilderAsync` are separate classes, each with their own `FindMapToMessage` / `FindMapToRequest` methods looking for distinct method names. The static utility maintains this sync/async distinction with separate methods:

```csharp
// In Paramore.Brighter (internal static utility)
internal static class MapperMethodDiscovery
{
    // Sync — finds MapToMessage(TRequest, Publication)
    internal static MethodInfo? FindMapToMessage(Type mapperType, Type requestType)
    {
        return mapperType.GetMethods()
            .Where(m => m.Name == "MapToMessage")
            .SingleOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 2
                    && p[0].ParameterType == requestType
                    && p[1].ParameterType == typeof(Publication);
            });
    }

    // Async — finds MapToMessageAsync(TRequest, Publication, CancellationToken)
    internal static MethodInfo? FindMapToMessageAsync(Type mapperType, Type requestType)
    {
        return mapperType.GetMethod(
            "MapToMessageAsync",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [requestType, typeof(Publication), typeof(CancellationToken)],
            null);
    }

    // Sync — finds MapToRequest(Message)
    internal static MethodInfo? FindMapToRequest(Type mapperType)
    {
        return mapperType.GetMethods()
            .Where(m => m.Name == "MapToRequest")
            .SingleOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 1
                    && p[0].ParameterType == typeof(Message);
            });
    }

    // Async — finds MapToRequestAsync(Message, CancellationToken)
    internal static MethodInfo? FindMapToRequestAsync(Type mapperType)
    {
        return mapperType.GetMethod(
            "MapToRequestAsync",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [typeof(Message), typeof(CancellationToken)],
            null);
    }
}
```

Note: the async methods use `GetMethod()` with explicit `BindingFlags` and parameter type arrays, matching the existing `TransformPipelineBuilderAsync` implementation. The sync methods use LINQ filtering, matching `TransformPipelineBuilder`.

The existing `TransformPipelineBuilder.FindMapToMessage()`, `FindMapToRequest()`, and the async counterparts on `TransformPipelineBuilderAsync` are refactored to delegate to `MapperMethodDiscovery`. The private `FindMethods()` helper in `TransformPipelineBuilder` is removed.

This is a structural-only tidy (no behavioral change) — all existing callers get the same `MethodInfo` back. It should be committed separately from behavioral changes, following the Tidy First principle.

#### Pre-existing Bug Fix (Tidy First)

`PipelineBuilder.BuildAsyncPipeline()` calls `AddGlobalInboxAttributesAsync()` twice — once inside the cache-miss block (correct) and once unconditionally outside it (bug). This means the inbox attribute is re-added on every pipeline build, not just the first time. This should be fixed as a separate tidy commit during implementation, removing the duplicate call outside the `if` block.

### Subscriber Registry Inspection

`IAmASubscriberRegistry.Get<T>()` requires an instance of `T` and a request context, which we don't have at startup. We need type-based introspection methods for the validator.

Rather than adding these methods to `IAmASubscriberRegistry` (which would be a breaking change to a public interface), we introduce a new **`IAmASubscriberRegistryInspector`** interface. This follows the Interface Segregation Principle — runtime dispatch (`Get<T>()`) and startup inspection are separate concerns with separate consumers:

```csharp
// In Paramore.Brighter — new interface for startup-time introspection
public interface IAmASubscriberRegistryInspector
{
    IEnumerable<Type> GetHandlerTypes(Type requestType);
    IEnumerable<Type> GetRegisteredRequestTypes();
}
```

`SubscriberRegistry` implements both `IAmASubscriberRegistry` and `IAmASubscriberRegistryInspector`. Custom registry implementations only need to implement `IAmASubscriberRegistryInspector` if they want to participate in pipeline validation — there is no silent degradation. If the validator cannot resolve `IAmASubscriberRegistryInspector` from DI, it reports a clear error explaining that the custom registry must implement the inspector interface to support validation.

**`GetHandlerTypes(Type requestType)`** returns all handler types registered for a given request type — the superset of types that routing functions may return, without evaluating any routing predicate.

**`GetRegisteredRequestTypes()`** returns all request types that have at least one handler registration. This is the method `PipelineBuilder.Describe()` (parameterless) uses to enumerate all pipelines.

**How the pieces connect end-to-end**: At startup, the validator constructs a describe-only `PipelineBuilder` (no handler factory) and calls its parameterless `Describe()`. Internally, `Describe()` calls `_subscriberRegistry.GetRegisteredRequestTypes()` to get every request type that has handlers, then for each type calls `Describe(Type requestType)`, which in turn calls `_subscriberRegistry.GetHandlerTypes(requestType)` to get the handler types for that specific request type. The validator never iterates the registry directly — `PipelineBuilder` owns that coordination:

```
Validator                PipelineBuilder              SubscriberRegistry
   │                          │                       (IAmASubscriberRegistryInspector)
   │── Describe() ──────────► │                              │
   │                          │── GetRegisteredRequestTypes() ──►│
   │                          │◄── [OrderCreated, Payment...]──│
   │                          │                              │
   │                          │ for each request type:       │
   │                          │── GetHandlerTypes(type) ────►│
   │                          │◄── [HandlerA, HandlerB] ────│
   │                          │                              │
   │                          │  reflect on each handler:    │
   │                          │  find Handle/HandleAsync     │
   │                          │  extract attributes           │
   │                          │  build description model      │
   │                          │                              │
   │◄── IEnumerable<HandlerPipelineDescription> ────────────│
```

**Implementation detail**: Today `SubscriberRegistry._observers` stores routing functions (`Func<IRequest?, IRequestContext?, List<Type>>`), not handler types directly. For simple registrations via `Register<TRequest, TImplementation>()`, the handler type is captured in a lambda. For routing registrations via `Register<TRequest>(Func<...> router, IEnumerable<Type> handlerTypes)`, the `handlerTypes` parameter represents the set of all possible types the routing function may return — these are passed through to the DI container (`ServiceCollectionSubscriberRegistry`) for registration, but `SubscriberRegistry` itself does not currently store them.

To support both methods, `SubscriberRegistry` gains a parallel `Dictionary<Type, HashSet<Type>> _allHandlerTypes` that both `Add` overloads populate. This is a plain `Dictionary`, not a `ConcurrentDictionary` — matching the existing `_observers` field. Both are written during the single-threaded DI registration phase and only read after the service provider is built. The static caches in `PipelineBuilder` (`s_preAttributesMemento`, `s_postAttributesMemento`) use `ConcurrentDictionary` because they are lazily populated during concurrent message dispatch; `_allHandlerTypes` does not have this concern.

```csharp
// In SubscriberRegistry
private readonly Dictionary<Type, HashSet<Type>> _allHandlerTypes = new();

public void Add(Type requestType, Type handlerType)
{
    // ... existing routing function storage ...
    if (!_allHandlerTypes.TryGetValue(requestType, out var types))
        _allHandlerTypes[requestType] = types = [];
    types.Add(handlerType);
}

public void Add(Type requestType, Func<IRequest?, IRequestContext?, List<Type>> router,
    IEnumerable<Type> handlerTypes)
{
    // ... existing routing function storage ...
    if (!_allHandlerTypes.TryGetValue(requestType, out var types))
        _allHandlerTypes[requestType] = types = [];
    foreach (var handlerType in handlerTypes)
        types.Add(handlerType);
}

public IEnumerable<Type> GetHandlerTypes(Type requestType)
    => _allHandlerTypes.TryGetValue(requestType, out var types) ? types : [];

public IEnumerable<Type> GetRegisteredRequestTypes()
    => _allHandlerTypes.Keys;
```

This is a structural-only tidy (no behavioral change to existing callers) — `Get<T>()` continues to evaluate routing functions at runtime. `GetHandlerTypes(Type)` returns the superset of all possible handler types without needing an instance of the request or evaluation of routing predicates. `GetRegisteredRequestTypes()` returns the keys of the same dictionary — the set of all request types that have been registered.

### Mapper Resolution Inspection

`MessageMapperRegistry` maintains **separate** sync and async mapper dictionaries (`_messageMappers` and `_asyncMessageMappers`) with **separate** default mappers (`_defaultMessageMapper` and `_defaultMessageMapperAsync`). The existing `Get<T>()` and `GetAsync<T>()` methods each search their own registry and instantiate the mapper. For the dry run, we need to know which mapper type resolves without instantiation — and we need both sync and async variants:

```csharp
// Additions to MessageMapperRegistry — non-generic, accept Type directly
public (Type? mapperType, bool isDefault) ResolveMapperInfo(Type requestType)
{
    if (_messageMappers.TryGetValue(requestType, out var mapperType))
        return (mapperType, false);  // explicit registration
    if (_defaultMessageMapper != null && _defaultMessageMapper.IsGenericTypeDefinition)
        return (_defaultMessageMapper.MakeGenericType(requestType), true);
    return (null, false);
}

public (Type? mapperType, bool isDefault) ResolveAsyncMapperInfo(Type requestType)
{
    if (_asyncMessageMappers.TryGetValue(requestType, out var mapperType))
        return (mapperType, false);  // explicit registration
    if (_defaultMessageMapperAsync != null && _defaultMessageMapperAsync.IsGenericTypeDefinition)
        return (_defaultMessageMapperAsync.MakeGenericType(requestType), true);
    return (null, false);
}
```

The `IsGenericTypeDefinition` guard prevents `MakeGenericType` from throwing `InvalidOperationException` if the default mapper is not an open generic type. Without this guard, a misconfigured default mapper (e.g. a closed generic or non-generic type registered by mistake) would crash startup with an unhandled exception rather than returning `(null, false)` and allowing the validator to report the issue.

These methods accept `Type` directly rather than using a generic `<TRequest>` constraint. The underlying dictionaries (`_messageMappers`, `_asyncMessageMappers`) are already keyed by `Type`, so the generic parameter would add nothing — and would force the validator to use `MakeGenericMethod()` reflection to call them from runtime `Type` objects. The existing generic `Get<T>()` / `GetAsync<T>()` methods need generics only to cast the return value to `IAmAMessageMapper<TRequest>`, which `ResolveMapperInfo` does not need to do.

The diagnostic report uses the appropriate variant based on context: `ResolveMapperInfo` for publications (outgoing, sync mappers) and `ResolveAsyncMapperInfo` for subscriptions using Proactor pumps.

### How Validator and Writer Use the Model

The validator and writer both consume the same `HandlerPipelineDescription` model from `Describe()`, but for different purposes.

**Validator** — evaluates `ISpecification<T>` instances against each description, using the visitor to collect detailed findings on failure:

```csharp
// Each specification is one rule; on failure the visitor collects the details
var collector = new ValidationResultCollector<HandlerPipelineDescription>();
foreach (var description in pipelineBuilder.Describe())
{
    foreach (var spec in HandlerPipelineValidationRules.Rules())
    {
        if (!spec.IsSatisfiedBy(description))
        {
            findings.AddRange(
                spec.Accept(collector)
                    .Where(r => !r.Success)
                    .Select(r => r.Error!));
        }
    }
}
```

**Writer** — formats the same description model for human consumption:

```csharp
foreach (var description in pipelineBuilder.Describe())
{
    var steps = description.BeforeSteps
        .OrderBy(s => s.Step)
        .Select(s => $"[{s.AttributeType.Name}({s.Step})]");
    var chain = string.Join(" → ", steps) + " → " + description.HandlerType.Name;
    logger.LogDebug("  Pipeline: {Chain}", chain);
}
```

### Project Layout

| Component | Project | Why |
|-----------|---------|-----|
| **Description Model** | | |
| `HandlerPipelineDescription` | `Paramore.Brighter` | Dry-run output from PipelineBuilder |
| `PipelineStepDescription` | `Paramore.Brighter` | One step in the handler chain |
| `TransformPipelineDescription` | `Paramore.Brighter` | Dry-run output from TransformPipelineBuilder |
| `TransformStepDescription` | `Paramore.Brighter` | One step in the transform chain |
| `IAmABackstopHandler` | `Paramore.Brighter` | Marker interface for backstop handlers (Reject/Defer/DontAck) |
| `IAmAResilienceHandler` | `Paramore.Brighter` | Marker interface for resilience pipeline handlers |
| `HandlerMethodDiscovery` | `Paramore.Brighter` | Static utility for finding handler methods; existing instance methods delegate here (internal) |
| `MapperMethodDiscovery` | `Paramore.Brighter` | Static utility for finding mapper methods (sync and async variants); existing instance methods on TransformPipelineBuilder and TransformPipelineBuilderAsync delegate here (internal) |
| **Builder Extensions** | | |
| `PipelineBuilder.Describe()` | `Paramore.Brighter` | Dry-run mode on existing builder |
| `TransformPipelineBuilder.DescribeTransforms()` | `Paramore.Brighter` | Dry-run mode on existing builder |
| `MessageMapperRegistry.ResolveMapperInfo()` | `Paramore.Brighter` | Sync mapper introspection without instantiation |
| `MessageMapperRegistry.ResolveAsyncMapperInfo()` | `Paramore.Brighter` | Async mapper introspection without instantiation |
| `IAmASubscriberRegistryInspector` | `Paramore.Brighter` | New interface for startup-time introspection; `SubscriberRegistry` implements it |
| `IAmASubscriberRegistryInspector.GetHandlerTypes()` | `Paramore.Brighter` | Type-based handler lookup for a single request type |
| `IAmASubscriberRegistryInspector.GetRegisteredRequestTypes()` | `Paramore.Brighter` | Enumerate all registered request types |
| **Specification & Validation** | | |
| `ISpecification<T>` | `Paramore.Brighter` | Moved from Mediator; enhanced with `Accept` visitor method |
| `Specification<T>` | `Paramore.Brighter` | Moved from Mediator; three constructors (pure predicate, simple rule, collapsed rule) |
| `AndSpecification<T>`, `OrSpecification<T>`, `NotSpecification<T>` | `Paramore.Brighter` | Composition nodes; delegate to children via visitor |
| `ValidationResult` | `Paramore.Brighter` | Success/failure bool paired with optional `ValidationError` |
| `ValidationError` | `Paramore.Brighter` | Severity + Source + Message — generic validation finding |
| `ValidationSeverity` | `Paramore.Brighter` | Enum: Error, Warning |
| `ISpecificationVisitor<TData, TResult>` | `Paramore.Brighter` | Visitor interface for traversing specification graphs |
| `ValidationResultCollector<TData>` | `Paramore.Brighter` | Concrete visitor that collects `ValidationResult` from the graph |
| **Pipeline Validation & Diagnostics** | | |
| `IAmAPipelineValidator` | `Paramore.Brighter` | Core interface |
| `IAmAPipelineDiagnosticWriter` | `Paramore.Brighter` | Core interface |
| `PipelineValidationResult` | `Paramore.Brighter` | Aggregates `ValidationError` instances; pipeline-specific information holder |
| `PipelineValidationException` | `Paramore.Brighter` | Extends `ConfigurationException`; holds `PipelineValidationResult` for programmatic access |
| `HandlerPipelineValidationRules` | `Paramore.Brighter` | Attribute checks (internal) |
| `ProducerValidationRules` | `Paramore.Brighter` | Publication checks (internal) |
| `ConsumerValidationRules` | `Paramore.Brighter.ServiceActivator` | Pump/handler/MessageType checks (internal) |
| **DI & Hosting Integration** | | |
| `ValidatePipelines()` extension | `Paramore.Brighter.Extensions.DependencyInjection` | Extension on `IBrighterBuilder` |
| `DescribePipelines()` extension | `Paramore.Brighter.Extensions.DependencyInjection` | Extension on `IBrighterBuilder` |
| `BrighterValidationHostedService` | `Paramore.Brighter.Extensions.DependencyInjection` | Runs at startup for non-consumer apps |
| Consumer rule registration | `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` | Adds consumer rules to validator |
| `ServiceActivatorHostedService` changes | `Paramore.Brighter.ServiceActivator.Extensions.Hosting` | Invokes validator/writer before Receive() |

### Validation Specification Details

Each specification encapsulates both the predicate and its validation metadata. **Simple specifications** use the two-argument constructor (`Func<T, bool>` + `Func<T, ValidationError>`) — the predicate expresses the *valid* condition, and the error factory provides what to report when it fails. **Collapsed specifications** use the single-argument constructor (`Func<T, IEnumerable<ValidationResult>>`) for per-element rules that need to report on multiple sub-elements individually.

#### AddBrighter Specifications — `ISpecification<HandlerPipelineDescription>`

**Specification: BackstopAttributeOrdering** (Warning, per-pair, collapsed)

```csharp
// Warning, not Error: a resilience pipeline placed before a backstop could
// intentionally catch and act on the exception before passing it on. This is
// unusual and probably not what the developer intended, but it is not provably
// wrong — so we warn rather than block startup.
//
// Collapsed constructor: the function returns one ValidationResult per misordered
// backstop/resilience pair, identifying the specific attribute names and step numbers.
// IsSatisfiedBy derives its bool from the results. Example message:
//   "Handler 'OrderCreatedHandler' — 'RejectMessageOnErrorAsync' at step 5 is
//    after 'UseResiliencePipelineAsync' at step 3 — in Brighter, lower step values
//    are outer wrappers, so the backstop will never execute on failure"
new Specification<HandlerPipelineDescription>(d =>
{
    var backstops = d.BeforeSteps.Where(s =>
        typeof(IAmABackstopHandler).IsAssignableFrom(s.HandlerType));
    var resilience = d.BeforeSteps.Where(s =>
        typeof(IAmAResilienceHandler).IsAssignableFrom(s.HandlerType));

    return backstops.SelectMany(b => resilience
        .Where(r => b.Step > r.Step)
        .Select(r => ValidationResult.Fail(new ValidationError(
            ValidationSeverity.Warning,
            $"Handler '{d.HandlerType.Name}'",
            $"'{b.AttributeType.Name}' at step {b.Step} is after " +
            $"'{r.AttributeType.Name}' at step {r.Step} — " +
            "in Brighter, lower step values are outer wrappers, so the backstop " +
            "will never execute on failure"))));
})
```

**Specification: AttributeAsyncConsistency** (Error, per-attribute, collapsed)

```csharp
// Collapsed constructor: yields one ValidationResult per mismatched attribute,
// identifying the specific attribute type name and step number. Example message:
//   "Handler 'OrderCreatedHandler' — Async handler uses sync attribute
//    'RejectMessageOnError' at step 2 — it will be silently ignored"
//
// Handler types that implement neither IHandleRequests nor IHandleRequestsAsync
// are reported as Error-severity findings rather than throwing an exception. This
// ensures corrupted or unexpected types in the registry produce clean validation
// output instead of crashing startup. (Additionally, Specification<T>.IsSatisfiedBy
// catches any unexpected exceptions as a safety net.)
new Specification<HandlerPipelineDescription>(d =>
{
    return d.BeforeSteps.Concat(d.AfterSteps).SelectMany(step =>
    {
        var isSync = typeof(IHandleRequests).IsAssignableFrom(step.HandlerType);
        var isAsync = typeof(IHandleRequestsAsync).IsAssignableFrom(step.HandlerType);

        if (!isSync && !isAsync)
        {
            return [ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Error,
                $"Handler '{d.HandlerType.Name}'",
                $"Pipeline step '{step.HandlerType.FullName}' at step {step.Step} " +
                "implements neither IHandleRequests nor IHandleRequestsAsync"))];
        }

        if (d.IsAsync != isAsync)
        {
            return [ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Error,
                $"Handler '{d.HandlerType.Name}'",
                d.IsAsync
                    ? $"Async handler uses sync attribute '{step.AttributeType.Name}' " +
                      $"at step {step.Step} — it will be silently ignored"
                    : $"Sync handler uses async attribute '{step.AttributeType.Name}' " +
                      $"at step {step.Step} — it will be silently ignored"))];
        }

        return [];
    });
})
```

#### AddProducers Specifications — `ISpecification<Publication>`

**Specification: PublicationRequestTypeSet** (Error, simple)

```csharp
// Simple constructor: predicate + error factory. Yields zero or one findings.
new Specification<Publication>(
    p => p.RequestType != null,
    p => new ValidationError(
        ValidationSeverity.Error,
        $"Publication '{p.Topic}'",
        "Publication.RequestType is null — Post()/Deposit() will throw ConfigurationException"))
```

**Specification: PublicationRequestTypeImplementsIRequest** (Error, simple)

```csharp
new Specification<Publication>(
    p => p.RequestType == null || typeof(IRequest).IsAssignableFrom(p.RequestType),
    p => new ValidationError(
        ValidationSeverity.Error,
        $"Publication '{p.Topic}'",
        $"Publication.RequestType '{p.RequestType?.Name}' does not implement IRequest"))
```

Note: these two could be composed via `And()` if evaluated together, but are kept separate for distinct error messages.

#### AddConsumers Specifications — `ISpecification<Subscription>`

**Specification: PumpHandlerMatch** (Error, simple)

```csharp
// Note: All() returns true on an empty collection, so this specification vacuously
// passes when no handlers are registered. This is intentional — the HandlerRegistered
// specification (below) catches the missing-handler case with a more specific error.
new Specification<Subscription>(
    s =>
    {
        var handlerTypes = subscriberRegistry.GetHandlerTypes(s.DataType);
        return s.MessagePumpType switch
        {
            MessagePumpType.Reactor => handlerTypes.All(t =>
                typeof(IHandleRequests).IsAssignableFrom(t)),
            MessagePumpType.Proactor => handlerTypes.All(t =>
                typeof(IHandleRequestsAsync).IsAssignableFrom(t)),
            _ => true
        };
    },
    s => new ValidationError(
        ValidationSeverity.Error,
        $"Subscription '{s.Name}'",
        "Reactor subscription has only async handlers (or Proactor has only sync) — handler will not be found at runtime"))
```

**Specification: HandlerRegistered** (Error, simple)

```csharp
new Specification<Subscription>(
    s => subscriberRegistry.GetHandlerTypes(s.DataType).Any(),
    s => new ValidationError(
        ValidationSeverity.Error,
        $"Subscription '{s.Name}'",
        "No handler registered for subscription's RequestType"))
```

**Specification: RequestTypeSubtype** (Warning, simple)

```csharp
new Specification<Subscription>(
    s => s.DataType == null
        || typeof(ICommand).IsAssignableFrom(s.DataType)
        || typeof(IEvent).IsAssignableFrom(s.DataType),
    s => new ValidationError(
        ValidationSeverity.Warning,
        $"Subscription '{s.Name}'",
        "RequestType implements neither ICommand nor IEvent — message dispatch uses Send vs Publish based on this distinction"))
```

**Note: Mapper coverage is diagnostic, not a validation error.** Brighter has a default mapper fallback (`JsonMessageMapper<T>`) — a missing custom mapper is not an error, it just means the default resolves. The diagnostic report (FR-5, FR-9) shows which mapper resolves for each publication and subscription (custom vs default), giving the developer visibility to confirm it matches their intent. There is no mapper validation specification because the default mapper makes this a valid configuration. The `RequestTypeSubtype` null guard (`s.DataType == null ||`) is defensive — `Subscription` constructors validate `DataType`, but validation specifications should not assume preconditions of other code.

## Consequences

### Positive

- Developers get immediate feedback on misconfiguration at startup, cutting the debug cycle from minutes to seconds.
- The diagnostic report provides visibility into pipeline wiring that did not exist before — useful for both debugging and onboarding.
- Works for all Brighter deployment scenarios — pure CQRS, producers only, or full messaging — not just the service activator case.
- The dry-run model (`HandlerPipelineDescription`, `TransformPipelineDescription`) is a single source of truth — both validator and writer consume the same model, ensuring consistency.
- The model is produced by the same `PipelineBuilder` and `TransformPipelineBuilder` that build the real pipelines, so it accurately reflects what will be instantiated at runtime. If the builder logic changes, the description stays in sync.
- The dry-run reuses the existing static attribute caches, so subsequent `Build()` calls benefit from the reflection already done during `Describe()`.
- Validation specifications carry their own error metadata, eliminating the need for a separate `ValidationRule<T>` coordinator. Each `Specification<T>` encapsulates both the predicate and the validation findings, keeping the rule definition self-contained and testable in isolation.
- Two construction modes cover all cases: simple specifications (predicate + error factory) for single-finding rules, and collapsed specifications (`Func<T, IEnumerable<ValidationResult>>`) for per-element rules that report on multiple sub-elements individually.
- The visitor pattern (`ISpecificationVisitor<TData, TResult>`) allows calling code to traverse the specification graph and collect detailed validation results only when needed — `IsSatisfiedBy` gives the fast boolean answer, and the visitor provides the detail on failure.
- The exception-catching wrapper in `IsSatisfiedBy` ensures corrupted types produce clean validation output rather than crashing startup.
- Moving `Specification<T>` from `Paramore.Brighter.Mediator` to `Paramore.Brighter` makes it available as a general-purpose building block, which better reflects its nature.
- Validation scales to what's configured: only relevant rules run for each combination of paths.
- Opt-in design means zero impact on existing users and zero production overhead when not enabled.
- Core interfaces and model types live in `Paramore.Brighter`, making them available to all Brighter users regardless of transport.
- Role interfaces with the `IAmA*` naming convention allow users to override with custom implementations if needed.

### Negative

- Adds new types across multiple projects — though each type is focused and cohesive.
- `PipelineBuilder` gains a new public method (`Describe`) which increases its surface area. However, this is a natural companion to `Build` — same inputs, different output.
- `MessageMapperRegistry` needs minor extensions for inspection without instantiation. `SubscriberRegistry` gains a second interface (`IAmASubscriberRegistryInspector`) — though this is non-breaking since `IAmASubscriberRegistry` itself is unchanged.
- Moving `ISpecification<T>` / `Specification<T>` from `Paramore.Brighter.Mediator` to `Paramore.Brighter` is a minor breaking change. The types move from the `Paramore.Brighter.Mediator` namespace to `Paramore.Brighter`, so any user code with `using Paramore.Brighter.Mediator` that references these types directly will need to add `using Paramore.Brighter` (or the compiler will find them via the Brighter dependency). This is acknowledged as an acceptable breaking change for V10.x — type-forwarding shims are not provided because the types are moving to a more fundamental layer and the fix is a single `using` update. The `Paramore.Brighter.Mediator` project continues to depend on `Paramore.Brighter`, so the types remain transitively available.
- Two potential integration points (dedicated `BrighterValidationHostedService` for non-consumer apps, `ServiceActivatorHostedService` for consumer apps) need to coordinate to avoid double-running. This is handled via optional DI resolution rather than shared mutable state.

### Risks and Mitigations

- **Risk**: Reflection-based pipeline inspection could be slow for large configurations.
  **Mitigation**: `PipelineBuilder` already caches attribute lists in static concurrent dictionaries. `Describe()` populates the same caches, so subsequent `Build()` calls are faster too. Inspection runs once at startup, not per-message.

- **Risk**: The description model drifts from what `Build()` actually constructs.
  **Mitigation**: Both `Describe()` and `Build()` share the same method-discovery utilities (`HandlerMethodDiscovery`, `MapperMethodDiscovery`) and attribute caches. Because the existing instance methods are refactored to delegate to the same static utilities, divergence between `Describe()` and `Build()` is structurally impossible — they run the same code.

- **Risk**: Validation rules could produce false positives.
  **Mitigation**: Only definite errors are reported as errors. Attribute ordering and ambiguous RequestType are warnings. Opt-in means developers can disable validation.

- **Risk**: Startup introspection requires methods not present on `IAmASubscriberRegistry`.
  **Mitigation**: A new `IAmASubscriberRegistryInspector` interface carries the introspection methods (`GetHandlerTypes(Type)`, `GetRegisteredRequestTypes()`). `SubscriberRegistry` implements both interfaces. `IAmASubscriberRegistry` is untouched — no breaking change. Custom registry implementations that want to participate in validation implement the inspector interface; those that do not are unaffected.

- **Risk**: Coordination between `BrighterValidationHostedService` and `ServiceActivatorHostedService`.
  **Mitigation**: Each service resolves an optional DI dependency to decide whether to act: `BrighterValidationHostedService` checks for `IDispatcher?` (no-op if present), `ServiceActivatorHostedService` checks for `IAmAPipelineValidator?` (runs validation if present). No shared mutable state — decisions are based on immutable DI registrations. See "Hosted Service Coordination" section above for the full scenario matrix.

## Alternatives Considered

### 1. Validation only in ServiceActivatorHostedService

This was the initial design. However, it means applications that use Brighter purely for CQRS or as a producer cannot benefit from validation at all. Handler attribute ordering and publication validation are valuable even without consumers.

### 2. Always-on validation (not opt-in)

Would catch errors for everyone but changes existing behavior (NFR-3), potentially slows startup in production, and could break applications with latent misconfigurations that don't cause runtime issues.

### 3. Separate validator per path (no composition)

Three independent validators (one per path) called separately. This is simpler but means the developer must know which validator to call, and error aggregation across paths requires manual composition. A single `ValidatePipelines()` call is better UX.

### 4. Validation via IOptions<T> ValidateOnStart pattern

Register validation rules as `IValidateOptions<BrighterOptions>` and use `.ValidateOnStart()`. This integrates well with ASP.NET Core but only works with the Options pattern and can't easily access registries and subscriptions that are registered as separate services.

## References

- Requirements: [specs/0023-Pipeline-Validation-At-Startup/requirements.md](../../specs/0023-Pipeline-Validation-At-Startup/requirements.md)
- GitHub Issue: [#2176 - Enable pipeline validation upon startup](https://github.com/BrighterCommand/Brighter/issues/2176)
- ASP.NET Core `ValidateOnBuild`: [.NET 9 breaking change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/9.0/hostbuilder-validation)
- Wolverine CLI diagnostics: [wolverinefx.net/guide/diagnostics](https://wolverinefx.net/guide/diagnostics)
- Rebus pipeline logging: [Rebus Wiki - Log message pipelines](https://github.com/rebus-org/Rebus/wiki/Log-message-pipelines)
- MassTransit probe API: [masstransit.io - Show Configuration](https://masstransit.io/support/show-configuration)
