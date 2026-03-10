# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: [#2176 - Enable pipeline validation upon startup](https://github.com/BrighterCommand/Brighter/issues/2176)

## Problem Statement

As a developer new to Brighter, I would like Brighter to validate my pipeline configuration at startup, so that I can find and fix misconfiguration immediately instead of discovering errors only when a message is processed at runtime.

Today, many configuration mistakes — mixing sync and async handlers, incorrect attribute ordering, missing handler or mapper registrations — are only surfaced when a message arrives on the broker and the pipeline fails. This leads to a slow and frustrating "build → run → send message → see error → fix → repeat" cycle. The developer should be able to know their configuration is wrong before any message is consumed.

## Brighter's Three Configuration Paths

Brighter is used in three distinct deployment scenarios, each adding a layer of configuration:

1. **`AddBrighter()`** — Command Dispatcher (CQRS). Registers handler pipelines, subscriber registry, mapper registry, and command processor. Used by all Brighter applications. Configured via `IBrighterBuilder`.

2. **`AddProducers()`** — Outgoing Messages. Adds `Publication` definitions, a `ProducerRegistry`, and the outbox-producer mediator. Used when the application sends or publishes messages to a broker.

3. **`AddConsumers()`** — Incoming Messages (Service Activator). Adds `Subscription` definitions, a `Dispatcher`, and message pumps (Reactor/Proactor). Used when the application consumes messages from a broker.

Each path has its own validation needs. A developer may use only path 1 (pure CQRS), paths 1+2 (sends messages but doesn't consume), or all three (full messaging).

## Prior Art

### ASP.NET Core
- **`ValidateOnBuild`**: Traverses every `ServiceDescriptor` at startup and verifies the entire dependency tree can be satisfied. Throws `AggregateException` if any service is unresolvable. Enabled by default in Development since .NET 9.
- **`ValidateOnStart()` (Options pattern)**: Registers an `IHostedService` that eagerly resolves `IOptions<T>` at startup, triggering all `IValidateOptions<T>` validators. Fails the host before any request is served.
- **Pattern**: Runtime validation at startup, not compile-time.

### MassTransit
- **Startup topology validation**: `bus.Start()` creates and validates broker topology for all registered consumers. Unresolvable consumer dependencies fail the bus start.
- **Roslyn analyzers (`MassTransit.Analyzers`)**: Compile-time checks for message contract correctness (MCA0001–MCA0003).
- **`GetProbeResult()`**: Returns a JSON diagnostic tree via a visitor pattern.
- **Pattern**: Two layers — Roslyn analyzers for static structure, runtime validation for wiring.

### Wolverine
- **`dotnet run -- describe`**: CLI command prints tabular diagnostic report of all listeners, message routing, sending endpoints, error handling, and HTTP endpoints. Also supports `--json` for file export.
- **`DescribeHandlerMatch()`**: Explains why a type is or isn't recognized as a handler.
- **Pattern**: Rich diagnostic output as a developer tool, not just pass/fail validation.

### Rebus
- **`LogPipeline(verbose: true)`**: Logs the full incoming/outgoing pipeline chain via `ILogger` at startup.
- **Pattern**: Simplest approach — pipeline visualization via standard logging.

### Common patterns across all frameworks
1. A **layered approach**: Static analyzers catch what they can at compile time, and runtime startup validation catches everything else.
2. A **diagnostic report** in addition to pass/fail validation: frameworks give developers visibility into "here's what I configured" — not just "something is wrong".

## Proposed Solution

A two-layer strategy aligned with .NET ecosystem conventions, applied across all three configuration paths (Roslyn analyzer extensions are out of scope — see ADR 0054):

### Layer 1: Diagnostic Report

A startup diagnostic report that shows the developer exactly how Brighter has been wired. The report covers whichever paths the application uses:
- **AddBrighter**: Registered handler pipelines with their attribute chains.
- **AddProducers**: Publications with their outgoing message mappers (custom vs default) and transforms.
- **AddConsumers**: Subscriptions with pump type, handler chain, and incoming message mappers.

**Developer experience**: The developer opts in via configuration. Brighter logs a structured report to `ILogger`. Summary at Information level, full detail at Debug level.

### Layer 2: Startup Validation

An opt-in validation that checks the fully-wired configuration at startup, before any messages are sent or consumed. Separate validation rules apply to each configuration path.

**Developer experience**: If validation fails, Brighter throws an `AggregateException` containing all errors, preventing the host from starting.

### Layer 3: Roslyn Analyzers — OUT OF SCOPE

Roslyn analyzer extensions are out of scope for this specification. They are documented separately in ADR 0054 (`0054-roslyn-analyzer-extensions-for-pipeline-validation.md`) for future implementation.

## Requirements

### Functional Requirements — AddBrighter (Command Processor)

#### FR-1: Handler Pipeline Attribute Ordering Validation
Backstop error-handling attributes (`RejectMessageOnErrorAttribute`, `DeferMessageOnErrorAttribute`, `DontAckOnErrorAttribute`) should be at the outermost position (lowest step number, typically step 0) in the pipeline. If a backstop attribute has a step number higher than a `UseResiliencePipelineAttribute`, report a warning — the resilience pipeline will catch exceptions before the backstop handler sees them, making the backstop ineffective.

#### FR-2: Handler Sync/Async Attribute Consistency
If a handler implements the sync interface (`IHandleRequests<T>`), it should use sync attributes (e.g. `RejectMessageOnErrorAttribute`, not `RejectMessageOnErrorAsyncAttribute`). If async, it should use async attributes. Mismatches must be reported.

#### FR-3: Handler Pipeline Diagnostic Report
The diagnostic report must show, for each registered handler:
- The **handler pipeline chain** — the full sequence of decorators in step order (backstop attributes, resilience pipeline, inbox, target handler) so the developer can see the decoration order.
- Whether the handler is sync (`IHandleRequests<T>`) or async (`IHandleRequestsAsync<T>`).
- Any resilience pipeline policies referenced by name.

This applies to all Brighter applications, not just those consuming messages.

### Functional Requirements — AddProducers (Outgoing Messages)

#### FR-4: Publication RequestType Validation
Every `Publication` should have a `RequestType` set, and that type must implement `IRequest`. (Note: this is already covered by Roslyn analyzers BRT001/BRT002 — the startup validator should also check it for cases the analyzer cannot catch, e.g. programmatic registration.)

#### FR-5: Outgoing Mapper and Transform Diagnostic Report
The diagnostic report must show, for each `Publication`:
- The **outgoing message mapper** that will be used — identifying whether it is a **custom mapper** (explicitly registered) or the **default mapper** (e.g. `JsonMessageMapper<T>`). Since Brighter has a default mapper fallback, a missing custom mapper is not an error — but the developer should know which mapper resolves so they can confirm it's what they intend.
- Any **outgoing transforms** (`WrapWithAttribute`) configured on the mapper's `MapToMessage` method, in pipeline order.
- The **topic** (`RoutingKey`) the publication targets.

### Functional Requirements — AddConsumers (Incoming Messages / Service Activator)

#### FR-6: Reactor/Proactor ↔ Handler Type Validation
When a `Subscription` uses `MessagePumpType.Reactor`, the registered handler for its `RequestType` **must** implement `IHandleRequests<T>` (sync). When it uses `MessagePumpType.Proactor`, the handler **must** implement `IHandleRequestsAsync<T>` (async). A mismatch must be reported as an error.

#### FR-7: Handler Registration Completeness for Subscriptions
Every `Subscription` must have at least one handler registered for its `RequestType`. A subscription with no handler must be reported as an error.

#### FR-8: MessageType ↔ IRequest Subtype Consistency
When a `Subscription`'s `RequestType` implements `ICommand`, the message should be dispatched via `Send()` (expecting `MT_COMMAND`). When it implements `IEvent`, it should be dispatched via `Publish()` (expecting `MT_EVENT`). Today `ValidateMessageType()` in the message pump only logs a warning — the startup validator should report when a subscription's `RequestType` is ambiguous (implements neither `ICommand` nor `IEvent`) or when the type hierarchy doesn't match the expected dispatch pattern.

#### FR-9: Incoming Mapper and Transform Diagnostic Report
The diagnostic report must show, for each `Subscription`:
- The **pump type** (Reactor/Proactor) and handler interface (sync/async).
- The **incoming message mapper** that resolves (custom or default).
- Any **incoming transforms** (`UnwrapWithAttribute`) configured on the mapper's `MapToRequest` method.
- The **handler pipeline chain** for the subscription's `RequestType`.
- The **channel name** and **routing key** (topic).

### Functional Requirements — Cross-Cutting

#### FR-10: Aggregate Error Reporting
All validation errors from all configuration paths must be collected and reported together as an `AggregateException` (or similar aggregating mechanism), not one at a time. The developer should see every problem in a single startup failure, not have to fix-and-restart repeatedly.

#### FR-11: Clear, Actionable Error Messages
Each validation error message must identify:
- The subscription/publication/handler that has the problem.
- What the problem is.
- How to fix it (brief guidance).

Example: `"Subscription 'OrderCreated' uses MessagePumpType.Reactor but handler 'OrderCreatedHandler' implements IHandleRequestsAsync<OrderCreated>. Use MessagePumpType.Proactor or change the handler to implement IHandleRequests<OrderCreated>."`

#### FR-12: Diagnostic Report Output Channel
The diagnostic report should be written via `ILogger` so it integrates with whatever logging infrastructure the developer has configured (console, Serilog, etc.). The report should be logged at **Information** level for a brief summary line (e.g. "Brighter: 3 handler pipelines, 2 publications, 5 subscriptions configured") and at **Debug** level for the full per-item detail. This follows ASP.NET Core's convention where detailed information is available at Debug level.

#### FR-13: Validation Scales to Configuration Path
Validation must work correctly regardless of which combination of paths the developer uses:
- **AddBrighter only**: Validates handler pipelines only.
- **AddBrighter + AddProducers**: Validates handler pipelines and publications.
- **AddBrighter + AddConsumers**: Validates handler pipelines and subscriptions.
- **All three**: Validates everything.

### Non-Functional Requirements

#### NFR-1: Opt-In by Default
Startup validation and diagnostic reporting must be opt-in. Developers who don't enable them see no change in behavior. This avoids impacting startup time in production for existing users.

#### NFR-2: Minimal Startup Overhead
Validation should complete in well under 1 second for typical configurations (≤50 subscriptions). It should use reflection on types rather than instantiating handlers or mappers.

#### NFR-3: No Breaking Changes
The feature must not change any existing API signatures or default behavior. Existing applications that do not opt in must work identically.

#### NFR-4: Works with IBrighterBuilder Interface
Validation must be configured through the `IBrighterBuilder` interface, not tied to a specific implementation. Although `ServiceCollectionBrighterBuilder` is the only implementation today, the design should allow other implementations to participate.

### Constraints and Assumptions

- **Constraint**: Startup validation requires a built `IServiceProvider` and thus runs after DI registration, not at compile time.
- **Constraint**: Handler pipeline validation (FR-1, FR-2, FR-3) applies even without producers or consumers — a pure CQRS application benefits from these checks.

### Out of Scope

- **Roslyn analyzer extensions**: New compile-time diagnostics for the `Paramore.Brighter.Analyzer` package (e.g. attribute ordering warnings, pump/handler mismatch detection in the IDE). Documented in ADR 0054 for future implementation.
- **Broker connectivity validation**: Checking whether the broker is reachable or topics/queues exist. This is a separate concern (health checks).
- **Message schema validation**: Validating that message payloads match expected schemas.
- **Comprehensive pipeline correctness**: Validating that the business logic in handlers is correct (that's what tests are for).
- **Automatic fix-up**: The validator reports problems; it does not attempt to auto-correct them.
- **Performance profiling**: Analyzing pipeline performance characteristics at startup.

## Acceptance Criteria

### AC-1: Handler Pipeline Validation (AddBrighter)
Given a handler with `[UseResiliencePipeline(step: 0)]` and `[RejectMessageOnError(step: 1)]`, when startup validation runs, then a warning is reported that the backstop attribute is ineffective.

### AC-2: Sync/Async Attribute Consistency (AddBrighter)
Given an async handler (`IHandleRequestsAsync<T>`) decorated with a sync attribute (`[RejectMessageOnError]`), when startup validation runs, then an error is reported identifying the mismatch.

### AC-3: Handler Pipeline Diagnostic Report (AddBrighter)
Given registered handler pipelines, when the diagnostic report is generated, then for each handler the report shows the full attribute chain in step order, the handler's sync/async nature, and referenced resilience policies.

### AC-4: Publication Validation (AddProducers)
Given a `Publication` with no `RequestType` set (programmatic registration), when startup validation runs, then an error is reported.

### AC-5: Outgoing Mapper Diagnostic Report (AddProducers)
Given a Publication for `OrderCreated` with a custom mapper and one for `PaymentReceived` with no custom mapper, when the diagnostic report is generated, then it shows the custom mapper name for `OrderCreated` and `JsonMessageMapper<PaymentReceived> (default)` for `PaymentReceived`, along with any transforms.

### AC-6: Pump/Handler Mismatch (AddConsumers)
Given a subscription with `MessagePumpType.Reactor` and an async-only handler, when startup validation runs, then an error is reported identifying the mismatch.

### AC-7: Missing Handler for Subscription (AddConsumers)
Given a subscription for `OrderCreated` with no handler registered, when startup validation runs, then an error is reported identifying the missing handler.

### AC-8: MessageType/IRequest Consistency (AddConsumers)
Given a subscription whose `RequestType` implements `ICommand`, and a message arrives with `MT_EVENT`, this is a known mismatch. The startup validator should report when a subscription's `RequestType` implements neither `ICommand` nor `IEvent`.

### AC-9: Incoming Subscription Diagnostic Report (AddConsumers)
Given subscriptions for incoming messages, when the diagnostic report is generated, then for each subscription the report shows: pump type, handler chain, incoming mapper (custom vs default), channel name, and routing key.

### AC-10: All Errors Reported Together
Given a configuration with errors across all three paths (handler, producer, consumer), when startup validation runs, then all errors appear in the exception, not just the first one.

### AC-11: Opt-In Does Not Affect Non-Adopters
Given an existing application that does not enable validation or diagnostics, when the application starts, then behavior is identical to before this feature was added.

### AC-12: Pure CQRS Application Works
Given an application that only calls `AddBrighter()` (no producers, no consumers), when validation is enabled, then only handler pipeline checks run — no errors about missing subscriptions or publications.

## Additional Context

### Existing Validation in Brighter Today

| What | Where | When |
|------|-------|------|
| `MessagePumpType != Unknown` | `Subscription` constructor | Object creation (runtime) |
| `RequestType` or `MapRequestType` set | `Subscription` constructor | Object creation (runtime) |
| Pipeline handlers consistent type | `PipelineBuilder` | First message dispatch (runtime) |
| MessageType ↔ IRequest subtype | `MessagePump.ValidateMessageType()` | Per-message (WARNING only, not blocking) |
| `Publication.RequestType` set | Roslyn analyzer BRT001 | Compile time |
| `Publication.RequestType` implements `IRequest` | Roslyn analyzer BRT002 | Compile time |
| `MessagePumpType` assigned | Roslyn analyzer BRT003 | Compile time |
| `WrapWith`/`UnwrapWith` on correct methods | Roslyn analyzers BRT004/BRT005 | Compile time |
| `ProducerRegistry` not null | `AddProducers()` | DI registration time |
| Producer has `Publication` with `Topic` | `ProducerRegistry` constructor | Object creation (runtime) |

### Gaps This Spec Addresses

| Gap | Configuration Path | Layer |
|-----|-------------------|-------|
| No visibility into handler pipeline wiring | AddBrighter | Diagnostic Report |
| Backstop attribute after resilience pipeline attribute | AddBrighter | Validation |
| Sync attribute on async handler (or vice versa) | AddBrighter | Validation |
| No visibility into outgoing mappers (custom vs default) | AddProducers | Diagnostic Report |
| No visibility into outgoing transforms | AddProducers | Diagnostic Report |
| Programmatic Publication missing RequestType | AddProducers | Validation |
| No visibility into incoming mappers and subscription wiring | AddConsumers | Diagnostic Report |
| Reactor subscription with async handler (or vice versa) | AddConsumers | Validation |
| Subscription with no handler registered | AddConsumers | Validation |
| Ambiguous RequestType (neither ICommand nor IEvent) | AddConsumers | Validation |
