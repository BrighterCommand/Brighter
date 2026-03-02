# 54. Roslyn Analyzer Extensions for Pipeline Validation

Date: 2026-03-01

## Status

Proposed

## Context

**Parent Requirement**: [specs/0023-Pipeline-Validation-At-Startup/requirements.md](../../specs/0023-Pipeline-Validation-At-Startup/requirements.md)

**Scope**: This ADR covers Roslyn analyzer extensions (Layer 3 from requirements — FR-14, FR-15, and the static counterpart of FR-2). ADR 0053 covers the runtime startup validation and diagnostic report (Layers 1 and 2).

**Related ADR**: [0053 — Pipeline Validation and Diagnostic Report at Startup](0053-pipeline-validation-at-startup.md)

### The Problem

Brighter has an existing Roslyn analyzer package (`Paramore.Brighter.Analyzer`) with five diagnostics (BRT001–BRT005) covering publication configuration, subscription pump type, and transform attribute placement. However, several common pipeline misconfiguration mistakes that developers make are not caught until runtime:

1. **Backstop attribute ordered after resilience pipeline** — A `[RejectMessageOnError(step: 2)]` after `[UseResiliencePipeline(step: 1)]` means the resilience pipeline catches exceptions before the backstop handler ever sees them, rendering the backstop ineffective. The developer gets no IDE feedback.

2. **Sync attributes on async handlers (or vice versa)** — Decorating a `RequestHandlerAsync<T>` with `[RejectMessageOnError]` (the sync variant) instead of `[RejectMessageOnErrorAsync]` means the attribute is silently ignored at runtime. The pipeline builds without the intended decorator.

3. **Subscription pump type doesn't match handler** — A `Subscription` configured with `MessagePumpType.Reactor` but whose `RequestType` has only an async handler (`RequestHandlerAsync<T>`) will fail at runtime with "no handlers found". When both the pump type and handler are statically visible, the IDE should flag this.

ADR 0053 catches all three at startup via runtime validation. This ADR adds compile-time detection for the cases that are statically determinable, giving developers immediate IDE feedback as they type.

### Existing Analyzer Architecture

The analyzer project follows consistent patterns:

- **Three analyzers** producing five diagnostics (BRT001–BRT005)
- **Two analysis strategies**: Operation-based (`OperationWalker` for object creation) and symbol-based (`SymbolVisitor` for method/type inspection)
- **Shared infrastructure**: `ChildOfVisitor` for type hierarchy checking, `BrighterAnalyzerGlobals` for string constants, `DiagnosticsIds` for diagnostic IDs
- **Conventions**: All diagnostics use category "Design", severity Warning, enabled by default
- **Test infrastructure**: `BaseAnalyzerTest<T>` with `CSharpAnalyzerTest`, inline test code with `{|#0:markup|}` for diagnostic locations

### Forces

- The analyzers must work with what is statically visible in the compilation — constructor arguments, type hierarchies, attribute applications.
- The analyzers cannot call methods at compile time (e.g. `GetHandlerType()`), so they must resolve handler types by inspecting the attribute class's method body via the semantic model.
- ADR 0053 introduces `IAmABackstopHandler` and `IAmAResilienceHandler` marker interfaces on handler types. The analyzer can use these interfaces for classification, making it extensible to third-party attributes.
- New diagnostics must be Warning severity (NFR-4) so they do not break existing builds.
- The analyzer package is a `DevelopmentDependency` — it ships as a NuGet analyzer, not a runtime reference.

## Decision

We add three new diagnostics to the existing `Paramore.Brighter.Analyzer` package, implemented as two new analyzer classes following the established patterns.

### New Diagnostics

| ID | Title | Severity | Requirement |
|----|-------|----------|-------------|
| BRT006 | Backstop attribute after resilience pipeline | Warning | FR-14 |
| BRT007 | Sync/async attribute mismatch on handler | Warning | FR-2 (static) |
| BRT008 | Subscription pump type doesn't match handler | Warning | FR-15 |

### Architecture Overview

```
Paramore.Brighter.Analyzer/
├── Analyzers/
│   ├── PublicationRequestTypeAssignmentAnalyzer.cs   (existing — BRT001, BRT002)
│   ├── SubscriptionConstructorAnalyzer.cs            (existing — BRT003)
│   ├── WrapAttributeAnalyzer.cs                      (existing — BRT004, BRT005)
│   ├── HandlerAttributeAnalyzer.cs                   (NEW — BRT006, BRT007)
│   └── PumpHandlerMismatchAnalyzer.cs                (NEW — BRT008)
├── Visitors/
│   ├── Operation/
│   │   ├── RequestTypeAssignmentVisitor.cs           (existing)
│   │   ├── SubscriptionConstructorVisitor.cs         (existing)
│   │   └── PumpHandlerMismatchVisitor.cs             (NEW)
│   └── Symbol/
│       ├── ChildOfVisitor.cs                          (existing)
│       ├── WrapAttributeSymbolVisitor.cs              (existing)
│       └── HandlerAttributeVisitor.cs                 (NEW)
├── docs/
│   ├── BRT006.md                                      (NEW)
│   ├── BRT007.md                                      (NEW)
│   └── BRT008.md                                      (NEW)
├── BrighterAnalyzerGlobals.cs                         (extended)
└── DiagnosticsIds.cs                                  (extended)
```

### Diagnostic Descriptors

```csharp
// BRT006 — Backstop attribute ordering
public static DiagnosticDescriptor BackstopAfterResilience = new(
    id: DiagnosticsIds.BackstopAfterResilience,
    title: "Backstop attribute after resilience pipeline",
    messageFormat: "'{0}' at step {1} will not catch failures from '{2}' at step {3} — " +
                   "the resilience pipeline handles exceptions before the backstop sees them",
    category: "Design",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true
);

// BRT007 — Sync/async attribute mismatch
public static DiagnosticDescriptor AttributeAsyncMismatch = new(
    id: DiagnosticsIds.AttributeAsyncMismatch,
    title: "Sync/async attribute mismatch on handler",
    messageFormat: "{0} handler '{1}' uses {2} attribute '{3}' — it will be silently ignored at runtime",
    category: "Design",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true
);

// BRT008 — Pump/handler type mismatch
public static DiagnosticDescriptor PumpHandlerMismatch = new(
    id: DiagnosticsIds.PumpHandlerMismatch,
    title: "Subscription pump type doesn't match handler",
    messageFormat: "Subscription for '{0}' uses MessagePumpType.{1} but handler '{2}' is {3} — " +
                   "use MessagePumpType.{4} or change the handler to {5}",
    category: "Design",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true
);
```

### Key Roles and Responsibilities

#### 1. `HandlerAttributeAnalyzer` — Service Provider (BRT006 + BRT007)

**Responsibility**: Detecting attribute misconfiguration on handler methods at compile time.

**Detection strategy**: Symbol-based analysis on `SymbolKind.Method`.

```
Developer writes:
    public class OrderHandler : RequestHandlerAsync<OrderCreated>
    {
        [RejectMessageOnError(step: 2)]              ← sync on async handler (BRT007)
        [UseResiliencePipelineAsync("retry", step: 1)] ← resilience at lower step (BRT006)
        public override async Task<OrderCreated> HandleAsync(...)
    }

Analyzer detects:
    1. Method is on a class inheriting RequestHandlerAsync<T> → handler is async
    2. Collects all RequestHandlerAttribute subclasses on the method
    3. Classifies each via marker interface on its handler type:
       - RejectMessageOnError → GetHandlerType() → RejectMessageOnErrorHandler<> → IAmABackstopHandler
       - UseResiliencePipelineAsync → GetHandlerType() → ResilienceExceptionPolicyHandlerAsync<> → IAmAResilienceHandler
    4. BRT006: backstop step (2) > resilience step (1) → warning
    5. BRT007: RejectMessageOnError is sync but handler is async → warning
```

**Analysis flow**:

1. Register `SymbolAction` for `SymbolKind.Method`
2. `HandlerAttributeVisitor` checks if the method's containing type inherits from `RequestHandler<T>` or `RequestHandlerAsync<T>` (using `ChildOfVisitor`)
3. Collects all attributes deriving from `RequestHandlerAttribute` on the method
4. For each attribute, resolves the handler type via `GetHandlerType()` return type analysis (see "Handler Type Classification" below)
5. **BRT006 check**: For every backstop attribute, verify no resilience attribute has a lower step number
6. **BRT007 check**: For every attribute, verify its handler type's async nature matches the containing class's async nature

#### 2. `PumpHandlerMismatchAnalyzer` — Service Provider (BRT008)

**Responsibility**: Detecting subscription/handler sync-async mismatches at compile time.

**Detection strategy**: Operation-based analysis on `OperationKind.ObjectCreation`, combined with compilation-wide type search.

```
Developer writes:
    new KafkaSubscription<OrderCreated>(
        messagePumpType: MessagePumpType.Reactor, ...);

    public class OrderHandler : RequestHandlerAsync<OrderCreated> { ... }

Analyzer detects:
    1. KafkaSubscription<OrderCreated> creation with Reactor pump
    2. Searches compilation for types inheriting RequestHandler<OrderCreated>
       or RequestHandlerAsync<OrderCreated>
    3. Finds OrderHandler : RequestHandlerAsync<OrderCreated> → async
    4. Reactor requires sync → mismatch → BRT008 warning
```

**Analysis flow**:

1. Register `OperationAction` for `OperationKind.ObjectCreation`
2. `PumpHandlerMismatchVisitor` checks if the created type inherits from `Subscription` (using existing `ChildOfVisitor`)
3. Extracts the generic type argument (`TRequest`) from the constructed type — this is the request type
4. Extracts the `MessagePumpType` value from constructor arguments (extends the pattern from `SubscriptionConstructorVisitor`)
5. Searches the compilation's types for classes inheriting from `RequestHandler<TRequest>` or `RequestHandlerAsync<TRequest>`
6. If found and the pump type doesn't match, reports BRT008

**Compilation-wide type search**: The visitor iterates the compilation's `GlobalNamespace` recursively to find handler types matching the request type. This is bounded — it only runs when a subscription creation is found and the pump type is statically determinable.

#### 3. Handler Type Classification — Deciding Backstop vs Resilience

The analyzer needs to classify pipeline attributes as "backstop" or "resilience" to check ordering (BRT006) and to determine sync/async nature (BRT007). Rather than hardcoding attribute names, the analyzer uses the marker interfaces from ADR 0053.

**Classification strategy** — from attribute type to marker interface:

1. Get the attribute's `INamedTypeSymbol`
2. Find the `GetHandlerType()` method override on the attribute type
3. Inspect the method body's syntax tree for a `TypeOfExpressionSyntax` (i.e. `typeof(SomeHandler<>)`)
4. Resolve the type symbol from the `typeof` expression
5. Check if the resolved type implements `IAmABackstopHandler` or `IAmAResilienceHandler` via `AllInterfaces`

This works because all Brighter pipeline attributes have single-expression `GetHandlerType()` overrides returning `typeof(...)`. For attributes where the method body cannot be resolved (e.g. third-party attributes without source), the analyzer silently skips classification — no false positives.

**Sync/async determination**: Once the handler type is resolved, check whether it implements `IHandleRequests` (sync) or `IHandleRequestsAsync` (async). Both are already well-known types in the `Paramore.Brighter` assembly.

**Dependency on ADR 0053**: The marker interfaces (`IAmABackstopHandler`, `IAmAResilienceHandler`) must be present in `Paramore.Brighter` before the analyzer can use them. Since the analyzer references `Paramore.Brighter` as a metadata reference (for type resolution), these interfaces will be available once ADR 0053's infrastructure is implemented. If the interfaces are not found (e.g. older Brighter version), the analyzer falls back to skipping classification — degrading gracefully rather than failing.

### Extended Constants

```csharp
// Additions to BrighterAnalyzerGlobals
public const string RequestHandlerBaseClass = "RequestHandler";
public const string RequestHandlerAsyncBaseClass = "RequestHandlerAsync";
public const string RequestHandlerAttributeClass = "RequestHandlerAttribute";
public const string BackstopHandlerInterface = "IAmABackstopHandler";
public const string ResilienceHandlerInterface = "IAmAResilienceHandler";
public const string HandleMethodName = "Handle";
public const string HandleAsyncMethodName = "HandleAsync";
public const string GetHandlerTypeMethodName = "GetHandlerType";
```

### What the Analyzers Cannot Detect

These limitations are inherent to static analysis and are covered by ADR 0053's runtime validation:

- **Dynamic step values**: When `step` is a variable or computed expression, not a literal
- **Dynamic pump types**: When `MessagePumpType` comes from configuration or a variable
- **Programmatic handler registration**: When handlers are registered via `SubscriberRegistry.Register()` rather than generic subscription types
- **Cross-assembly handlers**: When the handler is in a different assembly that isn't referenced (though this is unusual)
- **Non-generic subscriptions**: When using `new Subscription(requestType: typeof(Order), ...)` — the type argument is available but requires `typeof` expression analysis

### Test Strategy

Following the existing test infrastructure:

**`HandlerAttributeAnalyzerTest`** — tests for BRT006 and BRT007:
- Backstop attribute at lower step than resilience → no diagnostic
- Backstop attribute at higher step than resilience → BRT006
- Multiple backstop attributes, one misordered → BRT006 on the misordered one
- Sync attribute on sync handler → no diagnostic
- Async attribute on async handler → no diagnostic
- Sync attribute on async handler → BRT007
- Async attribute on sync handler → BRT007
- Mixed: both ordering and mismatch on same method → both BRT006 and BRT007
- Handler with no pipeline attributes → no diagnostic
- Non-handler class with same attribute → no diagnostic (ignored)

**`PumpHandlerMismatchAnalyzerTest`** — tests for BRT008:
- Reactor subscription with sync handler → no diagnostic
- Proactor subscription with async handler → no diagnostic
- Reactor subscription with async-only handler → BRT008
- Proactor subscription with sync-only handler → BRT008
- Subscription with dynamic pump type → no diagnostic (skipped)
- Handler implementing both sync and async interfaces → no diagnostic (both match)
- No handler found for request type → no diagnostic (covered by runtime validation FR-7)

### Documentation

Each new diagnostic gets a `docs/BRTnnn.md` file following the existing pattern:
- Description of what the rule checks
- Why it matters (consequence of ignoring)
- How to fix (with before/after code examples)

`AnalyzerReleases.Unshipped.md` is updated with all three new diagnostics.

## Consequences

### Positive

- Developers get immediate IDE feedback for the three most common pipeline mistakes — no need to run the application
- Backstop ordering mistakes are caught as they're typed, preventing silent resilience pipeline bypass
- Sync/async attribute mismatches are caught before deployment, preventing silently-ignored pipeline decorators
- Pump/handler mismatches are caught in the editor, not after deployment when the first message fails
- Classification via marker interfaces means third-party attributes that adopt `IAmABackstopHandler`/`IAmAResilienceHandler` automatically participate in BRT006 checks
- All diagnostics are Warning severity — they inform without breaking builds, consistent with BRT001–BRT005

### Negative

- Compilation-wide type search for BRT008 adds analysis time proportional to the number of types in the compilation — bounded but not zero cost
- The `GetHandlerType()` return type resolution requires syntax tree inspection of the attribute class, which is more complex than the existing analyzers' pure semantic analysis
- The analyzer depends on ADR 0053's marker interfaces — if those interfaces are not implemented first, the handler classification degrades to skipping (safe but less useful)

### Risks and Mitigations

- **Risk**: `GetHandlerType()` body inspection is fragile if attribute implementations become more complex. **Mitigation**: The analyzer only handles `typeof(...)` returns — the universal pattern in Brighter. Non-matching patterns are silently skipped.
- **Risk**: BRT008's compilation-wide search could be slow on very large projects. **Mitigation**: The search only runs when a subscription creation is detected (rare per compilation), and is bounded by the compilation's type count.
- **Risk**: False positives if a handler intentionally implements both sync and async interfaces. **Mitigation**: BRT008 only reports when pump type doesn't match *any* handler interface — if both are implemented, no diagnostic fires.

## Alternatives Considered

### Hardcoded attribute name matching instead of marker interface resolution

Simpler to implement — just check if the attribute type name contains "RejectMessageOnError", "DeferMessageOnError", "DontAckOnError" (backstop) or "UseResiliencePipeline" (resilience). However, this doesn't extend to third-party attributes and creates a maintenance burden when new backstop or resilience attributes are added. The marker interface approach aligns with the runtime validation design (ADR 0053) and Brighter's `IAmA*` convention.

### Single combined analyzer for all three diagnostics

Since BRT006 and BRT007 both analyze handler methods, they share an analyzer. BRT008 could be folded in too, but it uses a fundamentally different analysis strategy (operation-based vs symbol-based) and targets different syntax (subscription creation vs handler methods). Keeping BRT008 in a separate analyzer follows the existing pattern where `SubscriptionConstructorAnalyzer` is separate from `WrapAttributeAnalyzer`.

### Extending the existing SubscriptionConstructorAnalyzer for BRT008

BRT008 could be added to the existing `SubscriptionConstructorAnalyzer` since both analyze subscription creation. However, BRT008 requires compilation-wide type search (a new capability), and mixing it with the simpler BRT003 check would complicate the existing visitor. A separate analyzer keeps concerns focused.

## References

- Requirements: [specs/0023-Pipeline-Validation-At-Startup/requirements.md](../../specs/0023-Pipeline-Validation-At-Startup/requirements.md) — FR-2, FR-14, FR-15, NFR-4
- Related ADR: [0053 — Pipeline Validation and Diagnostic Report at Startup](0053-pipeline-validation-at-startup.md) — runtime counterpart; introduces marker interfaces
- Existing analyzers: `src/Paramore.Brighter.Analyzer/` — BRT001–BRT005
- Roslyn documentation: [Tutorial: Write a Roslyn Analyzer](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
