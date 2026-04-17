# 57. Replay Outbox Messages on Inbox Duplicate Detection

Date: 2026-04-16

## Status

Accepted

## Context

**Parent Requirement**: [specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/requirements.md](../../specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/requirements.md)

When a handler processes a command, it may produce downstream outbox messages that trigger subsequent workflow steps. If a workflow fails partway through — the handler completed and its outbox messages were dispatched, but a downstream consumer never processed its message — there is no mechanism to replay the workflow. Re-sending the original command to the first handler results in the inbox detecting a duplicate, and either throwing or warning. The downstream messages are never resent.

We need to allow the inbox to trigger a replay of the outbox messages that were originally produced when handling a command, without re-executing the handler logic itself. The outbox sweeper already handles dispatching outstanding messages; we simply need to mark the relevant outbox messages as undispatched.

### Forces

- The inbox handler (`UseInboxHandler`) currently has no knowledge of or access to the outbox. These are separate concerns in the current architecture.
- The outbox is generic over a transaction type (`IAmAnOutboxSync<TMessage, TTransaction>`), making it difficult to inject directly into the inbox handler without knowing the transaction type.
- Not all inbox/outbox implementations will support this feature (e.g., third-party or older implementations). The change must be non-breaking and opt-in.
- Some handlers have an inbox but no outbox (terminal steps). The design must handle this gracefully.
- All Brighter-maintained persistent store schemas (inbox and outbox) will add the `CausationId` column. However, users upgrading Brighter are not required to migrate their store schema unless they intend to use `OnceOnlyAction.Replay`. A separate PR provides inbox/outbox schema migration tooling and will be merged into master before this feature, then merged into this branch. `SupportsCausationTracking()` performs a runtime schema check so that users running new Brighter code against an old (un-migrated) schema get a clear validation error at startup rather than a runtime failure.

## Decision

### New Concept: Causation Id

A **Causation Id** links an inbox entry to the outbox messages produced during the same handler invocation. It is distinct from:

- **CorrelationId** — used for request-reply patterns
- **JobId / WorkflowId** — reserved for future workflow orchestration across multiple steps

The Causation Id is scoped to a single handler invocation: "when I handled command X, I produced messages M1, M2, M3 — they all share Causation Id C."

#### Propagation

The Causation Id is propagated via `RequestContext.Bag` using a well-known key defined in `RequestContextBagNames`:

```
RequestContextBagNames.CausationId = "Brighter-CausationId"  // new well-known key
```

The `UseInboxHandler` is responsible for generating the Causation Id and placing it in the pipeline's `RequestContext.Bag`. On first handling (no duplicate), `UseInboxHandler.Handle()` generates the Causation Id (defaulting to the command's `Id`) and stores it in the Bag before calling `base.Handle()`. This ensures it is available to all downstream pipeline steps, including the outbox `Add` operation in `CommandProcessor.DepositPost`.

**Prerequisite**: `UseInboxHandler` currently creates a private `RequestContext` via `InitRequestContext()` instead of using the pipeline's `this.Context`. This must be changed — the handler must use the pipeline's `IRequestContext` (available via the `Context` property inherited from `RequestHandler<T>`) so that Bag data is shared across the pipeline. This is a structural refactor that affects the existing `Throw` and `Warn` paths as well, and should be done as a tidy-first change before the behavioral changes.

Both the inbox `Add` and outbox `Add` operations read the Causation Id from `RequestContext.Bag` and store it.

### New OnceOnlyAction: Replay

A new enum value is added to `OnceOnlyAction`:

```csharp
public enum OnceOnlyAction
{
    Throw,
    Warn,
    Replay   // Clear dispatched state on matching outbox messages
}
```

When `UseInboxHandler` detects a duplicate and the action is `Replay`, it:

1. Retrieves the Causation Id from the inbox entry
2. Replays the causation's outbox messages by clearing their `DispatchedAt`
3. Returns the request without executing the handler

### New Role Interfaces

Two new interfaces define the **causation tracking** capability as an optional role that inbox/outbox implementations can provide:

```csharp
/// <summary>
/// Role: An inbox that can track and retrieve Causation Ids.
/// Responsibility: Knowing which causation an inbox entry belongs to.
/// </summary>
public interface IAmACausationTrackingInbox
{
    bool SupportsCausationTracking();
    
    Task<bool> SupportsCausationTrackingAsync(
        CancellationToken cancellationToken = default);
    
    string? GetCausationId(string id, string contextKey, 
        RequestContext? requestContext, int timeoutInMilliseconds = -1);
    
    Task<string?> GetCausationIdAsync(string id, string contextKey, 
        RequestContext? requestContext, int timeoutInMilliseconds = -1, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Role: An outbox that can replay messages for a causation.
/// Responsibility: Knowing if causation tracking is supported, and
/// doing the reset of dispatch state for a causation's outbox messages.
/// </summary>
public interface IAmACausationTrackingOutbox
{
    bool SupportsCausationTracking();
    
    Task<bool> SupportsCausationTrackingAsync(
        CancellationToken cancellationToken = default);
    
    void ReplayCausation(string causationId, RequestContext? requestContext,
        Dictionary<string, object>? args = null);
    
    Task ReplayCausationAsync(string causationId, RequestContext? requestContext,
        Dictionary<string, object>? args = null, 
        CancellationToken cancellationToken = default);
}
```

These follow the existing naming convention (`IAmA*`) and are separate from the core inbox/outbox interfaces, so implementations that don't support causation tracking continue to work.

### Architecture Overview

```
Command arrives (duplicate)
        │
        ▼
┌──────────────────────┐
│   UseInboxHandler    │
│                      │
│  inbox.Exists() ─►true
│  action == Replay?   │
│        │yes          │
│        ▼             │
│  inbox.GetCausationId()
│        │             │
│        ▼             │
│  outbox.ReplayCausation(causationId)
│        │             │
│        ▼             │
│  return (skip handler)
└──────────────────────┘
        │
        ▼  (later)
┌──────────────────────┐
│   Outbox Sweeper     │
│                      │
│  OutstandingMessages()│
│  ── finds messages   │
│     with cleared     │
│     DispatchedAt     │
│  ── re-dispatches    │
└──────────────────────┘
```

### Modified UseInboxHandler

The `UseInboxHandler` gains an optional outbox dependency. Since the outbox is generic over `TTransaction`, we inject the causation-tracking role interface directly, avoiding the generic type parameter:

```csharp
public class UseInboxHandler<T> : RequestHandler<T> where T : class, IRequest
{
    private readonly IAmAnInboxSync _inbox;
    private readonly IAmACausationTrackingOutbox? _outbox;  // optional
    
    public UseInboxHandler(IAmAnInboxSync inbox, 
        IAmACausationTrackingOutbox? outbox = null)
    {
        _inbox = inbox;
        _outbox = outbox;
    }
}
```

The `Handle` method is extended:

```csharp
if (exists && _onceOnlyAction is OnceOnlyAction.Replay)
{
    Log.CommandHasAlreadyBeenSeenReplayingOutbox(s_logger, request.Id);
    
    if (_inbox is IAmACausationTrackingInbox trackingInbox 
        && _outbox is not null)
    {
        var causationId = trackingInbox.GetCausationId(
            request.Id, _contextKey, requestContext);
        
        if (causationId is not null)
        {
            _outbox.ReplayCausation(causationId, requestContext);
        }
    }
    
    return request;
}
```

The same pattern applies to `UseInboxHandlerAsync`.

### Causation Id Storage

#### Inbox Storage

The inbox `Add` method reads `CausationId` from `RequestContext.Bag` and stores it alongside the request. For `InMemoryInbox`, the `InboxItem` record gains a `CausationId` property:

```csharp
public class InboxItem(Type requestType, string requestBody, DateTimeOffset writeTime, string contextKey)
{
    // ... existing properties ...
    public string? CausationId { get; set; }  // new
}
```

#### Outbox Storage

The outbox `Add` method reads `CausationId` from `RequestContext.Bag` and stores it alongside the message. For `InMemoryOutbox`, the `OutboxEntry` class gains a `CausationId` property:

```csharp
public class OutboxEntry(Message message) : IHaveABoxWriteTime
{
    // ... existing properties (Message, WriteTime as DateTimeOffset, TimeFlushed) ...
    public string? CausationId { get; set; }  // new
}
```

The `ReplayCausation(causationId)` method finds all entries with a matching `CausationId` and resets `TimeFlushed` to `DateTimeOffset.MinValue`, causing the sweeper to re-dispatch them.

#### Persistent Store Implementations

All Brighter-maintained persistent inbox and outbox implementations add `CausationId` support:

**Inbox stores** (9): DynamoDB, DynamoDB.V4, Firestore, MongoDb, MsSql, MySql, Postgres, Spanner, Sqlite
**Outbox stores** (9): DynamoDB, DynamoDB.V4, Firestore, MongoDb, MsSql, MySql, PostgreSql, Spanner, Sqlite

Each persistent store:
1. Adds a nullable `CausationId` column/attribute to its schema (existing rows have null — no data migration needed)
2. Reads `CausationId` from `RequestContext.Bag` in its `Add` method and stores it
3. Implements `IAmACausationTrackingInbox` or `IAmACausationTrackingOutbox` as appropriate
4. Indexes `CausationId` in the outbox for efficient replay queries
5. Returns `true` from `SupportsCausationTracking()` once the schema supports it

#### Test Strategy

Implementation follows test-first, starting with in-memory stores and propagating to persistent stores via the base test pattern:

1. **Base tests** in `tests/Paramore.Brighter.Base.Test` define causation tracking test cases against the `IAmACausationTrackingInbox` and `IAmACausationTrackingOutbox` interfaces
2. **In-memory stores** (`InMemoryInbox`, `InMemoryOutbox`) are implemented and validated first
3. **Outbox persistent store tests** are generated from base tests via the Liquid template generator in `tools/Paramore.Brighter.Test.Generator/` — new templates are added for causation tracking scenarios
4. **Inbox persistent store tests** are derived manually from the base test classes (the generator does not yet cover inbox tests)
5. Each persistent store test project inherits the causation tracking tests and provides its store-specific setup/teardown

### Pipeline Validation

#### Prerequisite: Enrich PipelineStepDescription (structural change)

`PipelineBuilder.Describe()` already has access to the full attribute instances — `GetOtherHandlersInPipeline()` returns `IEnumerable<RequestHandlerAttribute>` via reflection. However, `PipelineStepDescription` currently discards the attribute's properties, keeping only `AttributeType`, `HandlerType`, `Step`, and `Timing`. The `OnceOnlyAction` value on a `UseInboxAttribute` is lost.

To enable attribute-aware validation rules, `PipelineStepDescription` is enriched with a non-positional `Attribute` property. The existing positional parameters are preserved to avoid breaking the record's constructor contract — tests and production code construct `PipelineStepDescription` using positional syntax (`new PipelineStepDescription(typeof(SomeAttribute), typeof(SomeHandler<>), Step: 0, HandlerTiming.Before)`), and this must continue to compile.

```csharp
public record PipelineStepDescription(
    Type AttributeType,             // unchanged — positional contract preserved
    Type HandlerType,
    int Step,
    HandlerTiming Timing)
{
    /// <summary>
    /// The full attribute instance, when available. Set by PipelineBuilder.Describe()
    /// which has access to the reflected attribute objects. May be null in test code
    /// that constructs descriptions directly.
    /// </summary>
    public RequestHandlerAttribute? Attribute { get; init; }
}
```

The corresponding projection in `PipelineBuilder.Describe()` changes from:

```csharp
.Select(a => new PipelineStepDescription(a.GetType(), a.GetHandlerType(), a.Step, a.Timing))
```

to:

```csharp
.Select(a => new PipelineStepDescription(a.GetType(), a.GetHandlerType(), a.Step, a.Timing)
    { Attribute = a })
```

This is a non-breaking structural change:
- The four positional parameters are unchanged — all existing constructors, tests, and deconstruction patterns compile as before.
- The `Attribute` property is additive — existing code that doesn't need it simply ignores it.
- Validation rules that need attribute details (like the replay rule) access `step.Attribute` and pattern-match: `step.Attribute is UseInboxAttribute { OnceOnlyAction: OnceOnlyAction.Replay }`.
- Rules must handle `Attribute` being `null` (test-constructed descriptions), which the replay rule does naturally — if `Attribute` is null, the pattern match fails and the step is skipped.

#### Prerequisite: Pass InboxConfiguration into the validation path (structural change)

`BrighterPipelineValidationExtensions.ValidatePipelines()` currently creates the `PipelineBuilder` without `InboxConfiguration`:

```csharp
var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);  // no InboxConfiguration
```

The describe-only constructor already accepts `InboxConfiguration?` as an optional parameter, but `ValidatePipelines()` does not resolve and pass it. This means `Describe()` does not include global inbox attributes — only per-handler `[UseInbox]` attributes are visible.

The fix: resolve `InboxConfiguration` from the service provider and pass it through:

```csharp
var inboxConfiguration = sp.GetService<InboxConfiguration>();
var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry, inboxConfiguration);
```

Additionally, `Describe()` should include global inbox attributes in its output, mirroring what `AddGlobalInboxAttributes()` does at build time. This ensures `Describe()` and `Build()` agree on what the pipeline looks like, which is already the stated design intent of `Describe()`.

This change touches shared infrastructure — `Describe()` is the foundation of all pipeline validation — and should be treated as a tidy-first structural change with its own focused test coverage to prevent regression. `AddGlobalInboxAttributes()` (used during `Build()`) performs two guard checks before injecting the global inbox attribute:

1. `handlerMethod.HasNoInboxAttributesInPipeline()` — calls `MethodInfo.IsDefined(typeof(NoGlobalInboxAttribute), true)`
2. `handlerMethod.HasExistingUseInboxAttributesInPipeline()` — calls `MethodInfo.IsDefined(typeof(UseInboxAttribute/UseInboxAsyncAttribute), true)`

These are `MethodInfo` extension methods (in `ReflectionExtensions`). They do not require a handler instance — they only need the `MethodInfo`. `Describe()` already has the `MethodInfo` from `HandlerMethodDiscovery.FindHandlerMethod(handlerType, requestType)` at line 116. So the same guard checks can be called directly on `handlerMethod` inside `Describe()`:

```csharp
public IEnumerable<HandlerPipelineDescription> Describe(Type requestType)
{
    // ... existing code ...
    foreach (var handlerType in handlerTypes)
    {
        var handlerMethod = HandlerMethodDiscovery.FindHandlerMethod(handlerType, requestType);
        var attributes = handlerMethod.GetOtherHandlersInPipeline();

        // Inject global inbox attribute if applicable (same guards as Build path)
        if (_inboxConfiguration != null
            && !handlerMethod.HasNoInboxAttributesInPipeline()
            && !handlerMethod.HasExistingUseInboxAttributesInPipeline())
        {
            var isAsync = HandlerMethodDiscovery.IsAsyncHandler(handlerType);
            RequestHandlerAttribute globalInbox = isAsync
                ? new UseInboxAsyncAttribute(
                    step: 0,
                    contextKey: _inboxConfiguration.Context!(handlerType),
                    onceOnly: _inboxConfiguration.OnceOnly,
                    timing: HandlerTiming.Before,
                    onceOnlyAction: _inboxConfiguration.ActionOnExists)
                : new UseInboxAttribute(
                    step: 0,
                    contextKey: _inboxConfiguration.Context!(handlerType),
                    onceOnly: _inboxConfiguration.OnceOnly,
                    timing: HandlerTiming.Before,
                    onceOnlyAction: _inboxConfiguration.ActionOnExists);
            attributes = attributes.Append(globalInbox);
        }

        // ... rest of Describe (filter before/after, build description) ...
    }
}
```

The only difference from `Build()` is that `Build()` calls `_inboxConfiguration.Context(implicitHandler.GetType())` on the handler instance, while `Describe()` calls `_inboxConfiguration.Context(handlerType)` on the `Type` directly. Since `InboxConfiguration.Context` is `Func<Type, string>`, both produce the same result — the handler instance's `GetType()` returns the same `Type` that `Describe()` already has.

#### Validation rule: ReplayRequiresCausationTracking

A new collapsed specification is added to `HandlerPipelineValidationRules`, following the same pattern as `BackstopAttributeOrdering()` and `AttributeAsyncConsistency()`. Both the inbox and outbox instances are captured via closure in the factory method:

```csharp
public static ISpecification<HandlerPipelineDescription> ReplayRequiresCausationTracking(
    IAmAnInbox? inbox, IAmAnOutbox? outbox)
    => new Specification<HandlerPipelineDescription>(d =>
    {
        var hasReplay = d.BeforeSteps.Any(s =>
            s.Attribute is UseInboxAttribute { OnceOnlyAction: OnceOnlyAction.Replay }
            || s.Attribute is UseInboxAsyncAttribute { OnceOnlyAction: OnceOnlyAction.Replay });

        if (!hasReplay) return [];

        var findings = new List<ValidationResult>();

        // Check 1: inbox must implement IAmACausationTrackingInbox
        if (inbox is not IAmACausationTrackingInbox trackingInbox)
        {
            findings.Add(ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Error,
                $"Handler '{d.HandlerType.Name}'",
                "Inbox is configured with OnceOnlyAction.Replay but the inbox does not implement " +
                "IAmACausationTrackingInbox. Upgrade the inbox implementation.")));
        }
        else if (!trackingInbox.SupportsCausationTracking())
        {
            // Check 2: inbox schema must support CausationId
            findings.Add(ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Warning,
                $"Handler '{d.HandlerType.Name}'",
                "Inbox implements IAmACausationTrackingInbox but SupportsCausationTracking() " +
                "returned false. The inbox schema may need upgrading to add the CausationId column.")));
        }

        // Check 3: outbox must be configured
        if (outbox is null)
        {
            findings.Add(ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Warning,
                $"Handler '{d.HandlerType.Name}'",
                "Inbox is configured with OnceOnlyAction.Replay but no outbox is configured. " +
                "Replay will be a no-op (terminal step).")));
            return findings;
        }

        // Check 4: outbox must implement IAmACausationTrackingOutbox
        if (outbox is not IAmACausationTrackingOutbox trackingOutbox)
        {
            findings.Add(ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Error,
                $"Handler '{d.HandlerType.Name}'",
                "Inbox is configured with OnceOnlyAction.Replay but the outbox does not implement " +
                "IAmACausationTrackingOutbox. Upgrade the outbox implementation.")));
            return findings;
        }

        // Check 5: outbox schema must support CausationId
        if (!trackingOutbox.SupportsCausationTracking())
        {
            findings.Add(ValidationResult.Fail(new ValidationError(
                ValidationSeverity.Warning,
                $"Handler '{d.HandlerType.Name}'",
                "Outbox implements IAmACausationTrackingOutbox but SupportsCausationTracking() " +
                "returned false. The outbox schema may need upgrading to add the CausationId column.")));
        }

        return findings;
    });
```

This mirrors the collapsed specification form used by `BackstopAttributeOrdering()` and `AttributeAsyncConsistency()` — a result evaluator that returns zero or more `ValidationResult`s per pipeline description.

#### Wiring: PipelineValidator and ValidatePipelines()

The specification is added to the existing handler pipeline specs array in `PipelineValidator.ValidateHandlerPipelines()`. The outbox is resolved from DI and passed through:

In `ValidatePipelines()`:

```csharp
var inboxConfiguration = sp.GetService<InboxConfiguration>();
var inbox = inboxConfiguration?.Inbox;  // IAmAnInbox — already available

var mediator = sp.GetService<IAmAnOutboxProducerMediator>();
var outbox = mediator?.Outbox;  // IAmAnOutbox? — new read-only property (see below)

var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry, inboxConfiguration);
return new PipelineValidator(pipelineBuilder, publications, subscriptions, consumerSpecList, inbox, outbox);
```

The inbox is obtained from `InboxConfiguration.Inbox` which is already `IAmAnInbox`. This is the same `InboxConfiguration` that is now passed into `PipelineBuilder` for `Describe()` to inject global inbox attributes.

For the outbox, `OutboxProducerMediator` stores the outbox instances as private fields (`_outBox`, `_asyncOutbox`). A new read-only property is added to `IAmAnOutboxProducerMediator` to expose the outbox for validation:

```csharp
// On IAmAnOutboxProducerMediator:
IAmAnOutbox? Outbox { get; }
```

Implemented in `OutboxProducerMediator<TMessage, TTransaction>`:

```csharp
public IAmAnOutbox? Outbox => (IAmAnOutbox?)_outBox ?? _asyncOutbox;
```

This is safe because both `IAmAnOutboxSync<TMessage, TTransaction>` and `IAmAnOutboxAsync<TMessage, TTransaction>` inherit from `IAmAnOutbox`. The property returns whichever is available (sync preferred), or null if neither is configured.

`PipelineValidator` gains optional `IAmAnInbox?` and `IAmAnOutbox?` constructor parameters and includes the new rule in its specs array:

```csharp
private void ValidateHandlerPipelines(List<ValidationError> findings)
{
    var descriptions = _pipelineBuilder.Describe();
    var specs = new ISpecification<HandlerPipelineDescription>[]
    {
        HandlerPipelineValidationRules.HandlerTypeVisibility(),
        HandlerPipelineValidationRules.BackstopAttributeOrdering(),
        HandlerPipelineValidationRules.AttributeAsyncConsistency(),
        HandlerPipelineValidationRules.ReplayRequiresCausationTracking(_inbox, _outbox)  // new
    };

    EvaluateSpecs(descriptions, specs, findings);
}
```

No new validation method or separate code path is needed — the rule flows through the existing `EvaluateSpecs` infrastructure alongside all other handler pipeline rules.

This approach works because:

- The enriched `PipelineStepDescription` gives us access to `UseInboxAttribute.OnceOnlyAction` on each step
- Global inbox configuration is included in `Describe()` output, so both per-handler and global configurations are validated
- The outbox instance is captured by the specification's closure, keeping `PipelineValidator` generic — it does not need to know about replay semantics
- The rule is composable and independently testable, consistent with the Specification pattern established in ADR 0053

The `SupportsCausationTracking()` method is a permanent runtime schema check. It allows users to upgrade Brighter without being forced to migrate their store schema — the feature is only available once the schema supports it. A separate PR provides inbox/outbox migration tooling and will be merged before this feature.

### Observability

`UseInboxHandler` currently has no telemetry — it writes structured log messages but does not add events to the pipeline's Activity span. This is a gap for all inbox paths, not just Replay.

The CommandProcessor creates a span via `BrighterTracer.CreateSpan()` and stores it in `RequestContext.Span`. The base `RequestHandler.Handle()` writes handler entry events to this span via `BrighterTracer.WriteHandlerEvent()`. `UseInboxHandler` should follow the same pattern: write events to `Context.Span` (available after the tidy-first prerequisite that switches from `InitRequestContext()` to the pipeline's `this.Context`).

#### Events added to the pipeline span

All events are guarded by `Context?.Span != null` and gated on `InstrumentationOptions.Brighter`:

| Path | Event Name | Tags |
|------|-----------|------|
| First handling (no duplicate) | `"UseInboxHandler Add"` | `request.id`, `context_key` |
| Duplicate + Throw | `"UseInboxHandler Duplicate Throw"` | `request.id`, `context_key` |
| Duplicate + Warn | `"UseInboxHandler Duplicate Warn"` | `request.id`, `context_key` |
| Duplicate + Replay | `"UseInboxHandler Duplicate Replay"` | `request.id`, `context_key`, `causation_id` |

The Replay event includes the `CausationId` using a new `BrighterSemanticConventions.CausationId` constant (`"paramore.brighter.causation_id"`) — distinct from the existing `ConversationId` constant which carries the `CorrelationId` for request-reply patterns.

#### No new spans

`UseInboxHandler` does not create child spans. It adds events to the existing pipeline span, consistent with how all other built-in handler decorators work. The `OutboxSweeper` already creates its own Activity when it runs `SweepAsync`, so the re-dispatched messages get their own independent trace — there is no parent-child link between the replay trigger and the sweep, which is correct because the sweep is asynchronous and may pick up messages from multiple replays.

#### Implementation sketch

```csharp
// Inside Handle(), after the Replay branch executes:
if (Context?.Span != null)
{
    var tags = new ActivityTagsCollection
    {
        { BrighterSemanticConventions.RequestId, request.Id },
        { BrighterSemanticConventions.CausationId /* new constant: "paramore.brighter.causation_id" */, causationId }
    };
    Context.Span.AddEvent(new ActivityEvent(
        "UseInboxHandler Duplicate Replay", DateTimeOffset.UtcNow, tags));
}
```

The same pattern applies to the Throw, Warn, and Add paths (without the `CausationId` tag).

### Attribute Changes

`UseInboxAttribute` and `UseInboxAsyncAttribute` already accept `OnceOnlyAction` as a parameter. Adding `Replay` to the enum is sufficient — no attribute changes needed beyond the enum value.

### Configuration Changes

`InboxConfiguration.ActionOnExists` already stores a `OnceOnlyAction`. No changes needed.

### DI Registration

`UseInboxHandler` gains an optional `IAmACausationTrackingOutbox?` constructor parameter. For DI resolution to work, the outbox must be registered under this interface in addition to its primary registration.

Outbox implementations that support causation tracking (starting with `InMemoryOutbox`) implement `IAmACausationTrackingOutbox` directly. The DI registration in `ServiceCollectionExtensions` (or equivalent setup code) must register the same outbox instance as both its primary interface and `IAmACausationTrackingOutbox`:

```csharp
// Existing registration (unchanged):
services.AddSingleton<IAmAnOutbox>(outbox);

// Additional registration for causation tracking:
if (outbox is IAmACausationTrackingOutbox)
    services.AddSingleton<IAmACausationTrackingOutbox>((IAmACausationTrackingOutbox)outbox);
```

When the outbox does not implement `IAmACausationTrackingOutbox`, no registration is made and `UseInboxHandler` receives `null` for its optional parameter — the handler degrades gracefully (pipeline validation catches the mismatch at startup if `Replay` is configured).

**DI resolution path**: `UseInboxHandler<T>` is not explicitly registered in the container — it is resolved by type via `ServiceProviderHandlerFactory`, which calls `IServiceProvider.GetService(handlerType)`. The container uses `ActivatorUtilities` to construct the handler, which supports optional constructor parameters: it resolves `IAmAnInboxSync` from the container and passes `null` for the optional `IAmACausationTrackingOutbox?` parameter when that service is not registered. This is standard `Microsoft.Extensions.DependencyInjection` behavior and requires no special handling.

### Key Components and Responsibilities

| Component | Role | Responsibility |
|-----------|------|----------------|
| `UseInboxHandler` | Coordinator | Deciding whether to replay; generating the CausationId; delegating to inbox and outbox |
| `IAmACausationTrackingInbox` | Information Holder | Knowing the Causation Id for an inbox entry |
| `IAmACausationTrackingOutbox` | Service Provider | Knowing if causation tracking is supported by the schema; doing the replay of a causation's outbox messages |
| `RequestContext.Bag` | Structurer | Carrying the Causation Id through the pipeline |
| `PipelineStepDescription` | Information Holder | Knowing the full attribute instance (including `OnceOnlyAction`) for validation |
| `PipelineValidator` | Controller | Deciding if the pipeline is correctly configured for replay |
| Outbox Sweeper | Service Provider | Doing the re-dispatch (existing, unchanged) |

## Consequences

### Positive

- Enables workflow replay without re-executing handler logic — "skip what's done, resend what follows"
- Non-breaking: existing implementations work unchanged; new behavior is opt-in via `OnceOnlyAction.Replay`
- Uses the existing outbox sweeper for re-dispatch — no new dispatch mechanism needed
- Pipeline validation catches misconfiguration at startup, not at runtime
- The structural prerequisites (enriched `PipelineStepDescription`, `InboxConfiguration` in validation path) improve the validation infrastructure generally — future rules can inspect any attribute property
- Fixing `UseInboxHandler` to use the pipeline's `RequestContext` instead of creating its own is a correctness improvement that benefits all inbox paths
- The role interfaces (`IAmACausationTrackingInbox`, `IAmACausationTrackingOutbox`) follow existing patterns and can be adopted incrementally by store implementations

### Negative

- `UseInboxHandler` gains an optional outbox dependency, adding complexity to its constructor and DI registration
- All 18 Brighter-maintained store implementations (9 inbox, 9 outbox) need schema and code changes — significant breadth of change, though each individual change is mechanical
- Migration of existing data is not provided — new columns are nullable, so existing rows have null `CausationId` and replay is unavailable for historical entries
- `SupportsCausationTracking()` is a permanent runtime schema check on both inbox and outbox — it protects users who upgrade Brighter but have not yet migrated their store schema. Pipeline validation uses it at startup so that misconfiguration (Replay enabled on an un-migrated schema) produces a clear error, not a silent runtime failure
- The `Replay` action silently does nothing if the inbox/outbox don't support causation tracking at runtime (though pipeline validation should catch this at startup)

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Outbox has many messages for a causation; clearing all is slow | CausationId should be indexed in persistent stores. For in-memory, the scan is bounded by outbox size and expiry. |
| Handler produces different messages on re-execution vs original | Not applicable — the handler is not re-executed. The *same* outbox messages from the original execution are replayed. |
| Race condition: sweeper dispatches while we're clearing | Acceptable: worst case, a message is dispatched twice. Downstream inbox deduplication handles this. |
| Replay configured but no outbox (terminal step) | Pipeline validation warns. At runtime, `_outbox` is null, so the handler simply returns without replay — safe no-op. |
| Inbox `Add` fails after `base.Handle()` succeeds (including outbox writes) | The outbox messages will have CausationIds but the inbox entry won't exist, making replay impossible for that invocation. This is acceptable: since the inbox entry was never written, the message is not marked as "seen" — the transport will redeliver it, the handler will re-execute (inbox `Exists()` returns false), and both inbox and outbox entries will be written correctly on the retry. |

## Alternatives Considered

### 1. Use JobId for Correlation

The existing `JobId` field on `MessageHeader` could correlate inbox and outbox entries. Rejected because `JobId` represents an entire workflow instance — replaying by `JobId` would resend *all* messages for that job across *all* steps, not just the messages from the specific step being replayed.

### 2. Use CorrelationId for Correlation

`CorrelationId` is used for request-reply patterns and has different semantics. Overloading it for replay correlation would conflate two distinct concepts.

### 3. Signal Replay via RequestContext, Handle in Middleware

Instead of giving the inbox handler an outbox reference, set a flag in `RequestContext.Bag` and have a separate middleware component perform the outbox clearing. This adds a new handler type to the pipeline and complicates configuration for a single-purpose operation. The inbox handler is the natural place for this decision since it already owns the "what to do on duplicate" responsibility.

### 4. Re-execute the Handler on Duplicate

Instead of replaying stored outbox messages, re-execute the handler to produce new messages. Rejected because this defeats the purpose of the inbox (preventing non-idempotent re-execution) and could produce *different* messages depending on current state.

## References

- Requirements: [specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/requirements.md](../../specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/requirements.md)
- GitHub Issue: #2541
- Existing ADRs:
  - [0054 - Roslyn Analyzer Extensions for Pipeline Validation](0054-roslyn-analyzer-extensions-for-pipeline-validation.md)
  - [0056 - Timed Outbox Archiver Sync Fallback](0056-timed-outbox-archiver-sync-fallback.md)
