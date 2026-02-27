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
                                 AggregateException


Integration Points:
─────────────────────────────────────────────────────

  With ServiceActivator          Without ServiceActivator
  (AddConsumers used)            (pure CQRS or producers only)
  ┌─────────────────────┐        ┌─────────────────────────┐
  │ ServiceActivator     │        │ BrighterValidation      │
  │ HostedService        │        │ HostedService            │
  │ .StartAsync():       │        │ .StartAsync():          │
  │   describe()         │        │   describe()            │
  │   validate()         │        │   validate()            │
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

**Composite Implementation**: The validator is composed of `ValidationRule<T>` instances, each wrapping a `Specification<T>` predicate with severity and message metadata. Rules are grouped into rule sets by configuration path:

```csharp
// In Paramore.Brighter (core handler rules)
internal static class HandlerPipelineValidationRules
{
    // Returns ValidationRule<HandlerPipelineDescription> instances for:
    // FR-1: Backstop attribute ordering
    // FR-2: Sync/async attribute consistency
    public static IEnumerable<ValidationRule<HandlerPipelineDescription>> Rules();
}

// In Paramore.Brighter (producer rules, same project as AddProducers)
internal static class ProducerValidationRules
{
    // Returns ValidationRule<Publication> instances for:
    // FR-4: Publication.RequestType validation
    public static IEnumerable<ValidationRule<Publication>> Rules();
}

// In Paramore.Brighter.ServiceActivator (consumer rules)
internal static class ConsumerValidationRules
{
    // Returns ValidationRule<Subscription> instances for:
    // FR-6: Pump ↔ handler type match
    // FR-7: Handler registered for subscription
    // FR-8: MessageType ↔ IRequest subtype
    public static IEnumerable<ValidationRule<Subscription>> Rules(
        IAmASubscriberRegistry subscriberRegistry);
}
```

Each rule set is registered only when its corresponding configuration path is used. The top-level `PipelineValidator` evaluates all registered rules and aggregates the results.

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
    public IReadOnlyList<PipelineValidationError> Errors { get; }
    public IReadOnlyList<PipelineValidationError> Warnings { get; }
    public bool IsValid => Errors.Count == 0;

    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new AggregateException(
                "Brighter pipeline validation failed. See inner exceptions for details.",
                Errors.Select(e => new ConfigurationException(e.Message)));
    }

    // Compose results from multiple rule sets
    public static PipelineValidationResult Combine(
        params PipelineValidationResult[] results);
}
```

#### 4. `PipelineValidationError` — Information Holder

**Responsibility**: Knowing the details of one validation finding.

```csharp
// In Paramore.Brighter
public class PipelineValidationError
{
    public PipelineValidationSeverity Severity { get; }
    public string Source { get; }   // e.g. "Subscription 'OrderCreated'" or "Handler 'OrderCreatedHandler'"
    public string Message { get; }  // actionable description
}

public enum PipelineValidationSeverity { Error, Warning }
```

#### 5. `Specification<T>` — Moved to Paramore.Brighter (Tidy First)

`ISpecification<T>` and `Specification<T>` currently live in `Paramore.Brighter.Mediator`. They are a general-purpose implementation of the Specification pattern (predicate composition via `And()`, `Or()`, `Not()`) and have no inherent dependency on the Mediator workflow — they just happened to be introduced there for `ExclusiveChoice<TData>` branching.

We move both types to `Paramore.Brighter` (namespace `Paramore.Brighter`) so they are available to the core library. `Paramore.Brighter.Mediator` already depends on `Paramore.Brighter`, so existing consumers (e.g. `ExclusiveChoice<TData>`) continue to work with a `using` update. This is a structural-only tidy committed separately.

#### 6. `ValidationRule<T>` — Coordinator

**Responsibility**: Pairing a predicate (what to check) with validation metadata (what to report when it fails).

```csharp
// In Paramore.Brighter
public class ValidationRule<T>
{
    public ISpecification<T> Specification { get; }
    public PipelineValidationSeverity Severity { get; }
    public Func<T, string> Source { get; }    // e.g. entity => $"Handler '{entity.HandlerType.Name}'"
    public Func<T, string> Message { get; }   // e.g. entity => "backstop attribute should come before resilience"

    public ValidationRule(
        ISpecification<T> specification,
        PipelineValidationSeverity severity,
        Func<T, string> source,
        Func<T, string> message)
    {
        Specification = specification;
        Severity = severity;
        Source = source;
        Message = message;
    }

    /// <summary>
    /// Evaluates the rule against an entity. Returns null if the specification is satisfied,
    /// or a PipelineValidationError if it is not.
    /// </summary>
    public PipelineValidationError? Evaluate(T entity)
    {
        if (Specification.IsSatisfiedBy(entity))
            return null;

        return new PipelineValidationError(Severity, Source(entity), Message(entity));
    }
}
```

**How rules are defined** — each rule is a `Specification<T>` expressing the *valid* condition, paired with the error to report when it fails:

```csharp
// Example: backstop attributes should come before resilience pipelines
internal static class HandlerPipelineValidationRules
{
    public static IEnumerable<ValidationRule<HandlerPipelineDescription>> Rules()
    {
        yield return new ValidationRule<HandlerPipelineDescription>(
            specification: new Specification<HandlerPipelineDescription>(d =>
            {
                var backstops = d.BeforeSteps.Where(s => IsBackstopAttribute(s.AttributeType));
                var resilience = d.BeforeSteps.Where(s => IsResilienceAttribute(s.AttributeType));
                return !backstops.Any(b => resilience.Any(r => b.Step > r.Step));
            }),
            severity: PipelineValidationSeverity.Warning,
            source: d => $"Handler '{d.HandlerType.Name}'",
            message: d => "Backstop attribute (Reject/Defer/DontAck) has a higher step number than " +
                          "a resilience pipeline attribute, so it will never execute on failure"
        );

        yield return new ValidationRule<HandlerPipelineDescription>(
            specification: new Specification<HandlerPipelineDescription>(d =>
                d.BeforeSteps.All(step =>
                {
                    var stepIsAsync = typeof(IHandleRequestsAsync)
                        .IsAssignableFrom(step.HandlerType);
                    return d.IsAsync == stepIsAsync;
                })),
            severity: PipelineValidationSeverity.Error,
            source: d => $"Handler '{d.HandlerType.Name}'",
            message: d => d.IsAsync
                ? "Async handler has sync pipeline attributes — they will be silently ignored"
                : "Sync handler has async pipeline attributes — they will be silently ignored"
        );
    }
}
```

**How the validator evaluates rules**:

```csharp
// PipelineValidator evaluates all rules across all configured paths
public PipelineValidationResult Validate()
{
    var findings = new List<PipelineValidationError>();

    // Handler pipeline rules
    foreach (var description in _pipelineBuilder.Describe())
    {
        foreach (var rule in HandlerPipelineValidationRules.Rules())
        {
            var error = rule.Evaluate(description);
            if (error != null) findings.Add(error);
        }
    }

    // Producer rules (if configured)
    if (_publications != null)
    {
        foreach (var publication in _publications)
        {
            foreach (var rule in ProducerValidationRules.Rules())
            {
                var error = rule.Evaluate(publication);
                if (error != null) findings.Add(error);
            }
        }
    }

    // Consumer rules (if configured)
    // ...same pattern with ConsumerValidationRules.Rules()

    return new PipelineValidationResult(findings);
}
```

The `Specification<T>` handles the predicate logic and supports composition (`And()`, `Or()`, `Not()`) when rules need to be combined. The `ValidationRule<T>` adds the "why it failed" metadata that a bare boolean can't express.

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

`PipelineBuilder` gains a `Describe()` method alongside the existing `Build()`:

```csharp
public class PipelineBuilder<TRequest> where TRequest : class, IRequest
{
    // Existing — instantiates handlers, chains them
    public Pipelines<TRequest> Build(TRequest request, IRequestContext requestContext);
    public AsyncPipelines<TRequest> BuildAsync(TRequest request, IRequestContext ctx, bool continueOnCaptured);

    // New — describes the pipeline without instantiation
    public IEnumerable<HandlerPipelineDescription> Describe(Type requestType);

    // New — describe-only constructor (no handler factory needed)
    internal PipelineBuilder(
        IAmASubscriberRegistry subscriberRegistry,
        InboxConfiguration? inboxConfiguration = null);
}
```

**Describe-only construction**: `Describe()` only does Phase 1 (pure reflection) — it never calls the handler factory. A new `internal` constructor accepts just the subscriber registry and inbox configuration, omitting the factory. The validator constructs a single describe-only instance and calls `Describe(type)` for each registered request type. The same approach applies to `TransformPipelineBuilder` and `TransformPipelineBuilderAsync`.

**Why `Describe(Type)` stays on `PipelineBuilder<TRequest>`**: `Describe(Type requestType)` is a non-generic method that accepts `Type` and ignores the class's `TRequest` parameter. This is a deliberate trade-off — the two reasons for keeping it co-located rather than extracting to a separate class:

1. **Drift prevention** — the describe path and the build path share the same code (`HandlerMethodDiscovery`, `GetOtherHandlersInPipeline()`, static attribute caches), guaranteeing they stay in sync as the pipeline evolves. Co-location on the same class makes this relationship explicit to future maintainers.
2. **No runtime instance** — the validator operates on `Type` objects at registration time, not on request instances, so a generic constraint buys nothing here. The `Type requestType` parameter is the natural API for startup-time inspection.

`Describe()` executes phase 1 only:

1. Queries `IAmASubscriberRegistry` for handler types (via the new `GetHandlerTypes(Type)` method — see below).
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

`IAmASubscriberRegistry.Get<T>()` requires an instance of `T` and a request context, which we don't have at startup. We add a type-based lookup:

```csharp
// Addition to IAmASubscriberRegistry
IEnumerable<Type> GetHandlerTypes(Type requestType);
```

**Implementation detail**: Today `SubscriberRegistry._observers` stores routing functions (`Func<IRequest?, IRequestContext?, List<Type>>`), not handler types directly. For simple registrations via `Register<TRequest, TImplementation>()`, the handler type is captured in a lambda. For routing registrations via `Register<TRequest>(Func<...> router, IEnumerable<Type> handlerTypes)`, the `handlerTypes` parameter represents the set of all possible types the routing function may return — these are passed through to the DI container (`ServiceCollectionSubscriberRegistry`) for registration, but `SubscriberRegistry` itself does not currently store them.

To support `GetHandlerTypes(Type)`, `SubscriberRegistry` gains a parallel `Dictionary<Type, HashSet<Type>> _allHandlerTypes` that both `Add` overloads populate. This is a plain `Dictionary`, not a `ConcurrentDictionary` — matching the existing `_observers` field. Both are written during the single-threaded DI registration phase and only read after the service provider is built. The static caches in `PipelineBuilder` (`s_preAttributesMemento`, `s_postAttributesMemento`) use `ConcurrentDictionary` because they are lazily populated during concurrent message dispatch; `_allHandlerTypes` does not have this concern.

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
```

This is a structural-only tidy (no behavioral change to existing callers) — `Get<T>()` continues to evaluate routing functions at runtime. `GetHandlerTypes(Type)` returns the superset of all possible handler types without needing an instance of the request or evaluation of routing predicates.

### Mapper Resolution Inspection

`MessageMapperRegistry` maintains **separate** sync and async mapper dictionaries (`_messageMappers` and `_asyncMessageMappers`) with **separate** default mappers (`_defaultMessageMapper` and `_defaultMessageMapperAsync`). The existing `Get<T>()` and `GetAsync<T>()` methods each search their own registry and instantiate the mapper. For the dry run, we need to know which mapper type resolves without instantiation — and we need both sync and async variants:

```csharp
// Additions to MessageMapperRegistry — non-generic, accept Type directly
public (Type? mapperType, bool isDefault) ResolveMapperInfo(Type requestType)
{
    if (_messageMappers.TryGetValue(requestType, out var mapperType))
        return (mapperType, false);  // explicit registration
    if (_defaultMessageMapper != null)
        return (_defaultMessageMapper.MakeGenericType(requestType), true);
    return (null, false);
}

public (Type? mapperType, bool isDefault) ResolveAsyncMapperInfo(Type requestType)
{
    if (_asyncMessageMappers.TryGetValue(requestType, out var mapperType))
        return (mapperType, false);  // explicit registration
    if (_defaultMessageMapperAsync != null)
        return (_defaultMessageMapperAsync.MakeGenericType(requestType), true);
    return (null, false);
}
```

These methods accept `Type` directly rather than using a generic `<TRequest>` constraint. The underlying dictionaries (`_messageMappers`, `_asyncMessageMappers`) are already keyed by `Type`, so the generic parameter would add nothing — and would force the validator to use `MakeGenericMethod()` reflection to call them from runtime `Type` objects. The existing generic `Get<T>()` / `GetAsync<T>()` methods need generics only to cast the return value to `IAmAMessageMapper<TRequest>`, which `ResolveMapperInfo` does not need to do.

The diagnostic report uses the appropriate variant based on context: `ResolveMapperInfo` for publications (outgoing, sync mappers) and `ResolveAsyncMapperInfo` for subscriptions using Proactor pumps.

### How Validator and Writer Use the Model

The validator and writer both consume the same `HandlerPipelineDescription` model from `Describe()`, but for different purposes.

**Validator** — evaluates `ValidationRule<T>` instances against each description:

```csharp
// Each rule is a Specification<T> + severity + message
foreach (var description in pipelineBuilder.Describe())
{
    foreach (var rule in HandlerPipelineValidationRules.Rules())
    {
        var error = rule.Evaluate(description);
        if (error != null) findings.Add(error);
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
| `HandlerMethodDiscovery` | `Paramore.Brighter` | Static utility for finding handler methods; existing instance methods delegate here (internal) |
| `MapperMethodDiscovery` | `Paramore.Brighter` | Static utility for finding mapper methods (sync and async variants); existing instance methods on TransformPipelineBuilder and TransformPipelineBuilderAsync delegate here (internal) |
| **Builder Extensions** | | |
| `PipelineBuilder.Describe()` | `Paramore.Brighter` | Dry-run mode on existing builder |
| `TransformPipelineBuilder.DescribeTransforms()` | `Paramore.Brighter` | Dry-run mode on existing builder |
| `MessageMapperRegistry.ResolveMapperInfo()` | `Paramore.Brighter` | Sync mapper introspection without instantiation |
| `MessageMapperRegistry.ResolveAsyncMapperInfo()` | `Paramore.Brighter` | Async mapper introspection without instantiation |
| `IAmASubscriberRegistry.GetHandlerTypes()` | `Paramore.Brighter` | Type-based handler lookup |
| **Specification & Validation Rules** | | |
| `ISpecification<T>` | `Paramore.Brighter` | Moved from Mediator; general-purpose specification pattern |
| `Specification<T>` | `Paramore.Brighter` | Moved from Mediator; predicate composition (And/Or/Not) |
| `ValidationRule<T>` | `Paramore.Brighter` | Wraps specification with severity + message metadata |
| **Validation & Diagnostics** | | |
| `IAmAPipelineValidator` | `Paramore.Brighter` | Core interface |
| `IAmAPipelineDiagnosticWriter` | `Paramore.Brighter` | Core interface |
| `PipelineValidationResult` | `Paramore.Brighter` | Information holder |
| `PipelineValidationError` | `Paramore.Brighter` | Information holder |
| `HandlerPipelineValidationRules` | `Paramore.Brighter` | Attribute checks (internal) |
| `ProducerValidationRules` | `Paramore.Brighter` | Publication checks (internal) |
| `ConsumerValidationRules` | `Paramore.Brighter.ServiceActivator` | Pump/handler/MessageType checks (internal) |
| **DI & Hosting Integration** | | |
| `ValidatePipelines()` extension | `Paramore.Brighter.Extensions.DependencyInjection` | Extension on `IBrighterBuilder` |
| `DescribePipelines()` extension | `Paramore.Brighter.Extensions.DependencyInjection` | Extension on `IBrighterBuilder` |
| `BrighterValidationHostedService` | `Paramore.Brighter.Extensions.DependencyInjection` | Runs at startup for non-consumer apps |
| Consumer rule registration | `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` | Adds consumer rules to validator |
| `ServiceActivatorHostedService` changes | `Paramore.Brighter.ServiceActivator.Extensions.Hosting` | Invokes validator/writer before Receive() |

### Validation Rule Details

Each rule is a `ValidationRule<T>` wrapping a `Specification<T>` that expresses the *valid* condition. The specification returns `true` when the entity is correctly configured, `false` when it isn't. The `ValidationRule<T>` adds severity and message for the failing case.

#### AddBrighter Rules — `ValidationRule<HandlerPipelineDescription>`

**Rule: BackstopAttributeOrdering** (Warning)

```csharp
new Specification<HandlerPipelineDescription>(d =>
{
    var backstops = d.BeforeSteps.Where(s => IsBackstopAttribute(s.AttributeType));
    var resilience = d.BeforeSteps.Where(s => IsResilienceAttribute(s.AttributeType));
    return !backstops.Any(b => resilience.Any(r => b.Step > r.Step));
})
// Severity: Warning
// Message: "Backstop attribute has a higher step number than resilience pipeline — it will never execute on failure"
```

**Rule: AttributeAsyncConsistency** (Error)

```csharp
new Specification<HandlerPipelineDescription>(d =>
    d.BeforeSteps.Concat(d.AfterSteps).All(step =>
    {
        var stepIsAsync = typeof(IHandleRequestsAsync).IsAssignableFrom(step.HandlerType);
        return d.IsAsync == stepIsAsync;
    }))
// Severity: Error
// Message: "Async handler has sync pipeline attributes (or vice versa) — they will be silently ignored"
```

#### AddProducers Rules — `ValidationRule<Publication>`

**Rule: PublicationRequestTypeSet** (Error)

```csharp
new Specification<Publication>(p => p.RequestType != null)
// Severity: Error
// Message: "Publication.RequestType is null — Post()/Deposit() will throw ConfigurationException"
```

**Rule: PublicationRequestTypeImplementsIRequest** (Error)

```csharp
new Specification<Publication>(p =>
    p.RequestType == null || typeof(IRequest).IsAssignableFrom(p.RequestType))
// Severity: Error
// Message: "Publication.RequestType does not implement IRequest"
```

Note: these two could be composed via `And()` if evaluated together, but are kept separate for distinct error messages.

#### AddConsumers Rules — `ValidationRule<Subscription>`

**Rule: PumpHandlerMatch** (Error)

```csharp
new Specification<Subscription>(s =>
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
})
// Severity: Error
// Message: "Reactor subscription has only async handlers (or Proactor has only sync) — handler will not be found at runtime"
```

**Rule: HandlerRegistered** (Error)

```csharp
new Specification<Subscription>(s =>
    subscriberRegistry.GetHandlerTypes(s.DataType).Any())
// Severity: Error
// Message: "No handler registered for subscription's RequestType"
```

**Rule: RequestTypeSubtype** (Warning)

```csharp
new Specification<Subscription>(s =>
    s.DataType == null
    || typeof(ICommand).IsAssignableFrom(s.DataType)
    || typeof(IEvent).IsAssignableFrom(s.DataType))
// Severity: Warning
// Message: "RequestType implements neither ICommand nor IEvent — message dispatch uses Send vs Publish based on this distinction"
```

## Consequences

### Positive

- Developers get immediate feedback on misconfiguration at startup, cutting the debug cycle from minutes to seconds.
- The diagnostic report provides visibility into pipeline wiring that did not exist before — useful for both debugging and onboarding.
- Works for all Brighter deployment scenarios — pure CQRS, producers only, or full messaging — not just the service activator case.
- The dry-run model (`HandlerPipelineDescription`, `TransformPipelineDescription`) is a single source of truth — both validator and writer consume the same model, ensuring consistency.
- The model is produced by the same `PipelineBuilder` and `TransformPipelineBuilder` that build the real pipelines, so it accurately reflects what will be instantiated at runtime. If the builder logic changes, the description stays in sync.
- The dry-run reuses the existing static attribute caches, so subsequent `Build()` calls benefit from the reflection already done during `Describe()`.
- Validation rules are expressed as `Specification<T>` predicates — a well-known pattern already used in Brighter's workflow engine. Each rule is testable in isolation and composable via `And()`, `Or()`, `Not()`.
- `ValidationRule<T>` cleanly separates the predicate ("is this valid?") from the reporting metadata ("what to tell the developer when it isn't"), avoiding the loss of diagnostic detail that bare boolean specifications would cause.
- Moving `Specification<T>` from `Paramore.Brighter.Mediator` to `Paramore.Brighter` makes it available as a general-purpose building block, which better reflects its nature.
- Validation scales to what's configured: only relevant rules run for each combination of paths.
- Opt-in design means zero impact on existing users and zero production overhead when not enabled.
- Core interfaces and model types live in `Paramore.Brighter`, making them available to all Brighter users regardless of transport.
- Role interfaces with the `IAmA*` naming convention allow users to override with custom implementations if needed.

### Negative

- Adds new types across multiple projects — though each type is focused and cohesive.
- `PipelineBuilder` gains a new public method (`Describe`) which increases its surface area. However, this is a natural companion to `Build` — same inputs, different output.
- `MessageMapperRegistry` and `IAmASubscriberRegistry` need minor extensions for inspection without instantiation.
- Moving `ISpecification<T>` / `Specification<T>` from `Paramore.Brighter.Mediator` to `Paramore.Brighter` is a breaking namespace change for Mediator consumers — they must update `using` directives. However, the Mediator project depends on Brighter, so the types remain accessible.
- Two potential integration points (dedicated `BrighterValidationHostedService` for non-consumer apps, `ServiceActivatorHostedService` for consumer apps) need to coordinate to avoid double-running. This is handled via optional DI resolution rather than shared mutable state.

### Risks and Mitigations

- **Risk**: Reflection-based pipeline inspection could be slow for large configurations.
  **Mitigation**: `PipelineBuilder` already caches attribute lists in static concurrent dictionaries. `Describe()` populates the same caches, so subsequent `Build()` calls are faster too. Inspection runs once at startup, not per-message.

- **Risk**: The description model drifts from what `Build()` actually constructs.
  **Mitigation**: Both `Describe()` and `Build()` share the same method-discovery utilities (`HandlerMethodDiscovery`, `MapperMethodDiscovery`) and attribute caches. Because the existing instance methods are refactored to delegate to the same static utilities, divergence between `Describe()` and `Build()` is structurally impossible — they run the same code.

- **Risk**: Validation rules could produce false positives.
  **Mitigation**: Only definite errors are reported as errors. Attribute ordering and ambiguous RequestType are warnings. Opt-in means developers can disable validation.

- **Risk**: Adding `GetHandlerTypes(Type)` to `IAmASubscriberRegistry` is a breaking interface change.
  **Mitigation**: Add it as a default interface method that returns empty, or add it to a new `IAmASubscriberRegistryInspector` interface that `SubscriberRegistry` also implements.

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
