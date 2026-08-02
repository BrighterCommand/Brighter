---
id: 0071-pipeline-scope-handle-for-handler-pipelines
title: "Handler pipelines take their DI scope as a pipeline scope handle"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-08-02
summary: "Handler pipelines obtain their per-pipeline DI scope as the same IAmAScope handle that ADR 0070 gives transform pipelines, carried on the per-pipeline object the handler factories already receive. IAmAHandlerFactory gains CreatePipelineScope() and IAmALifetime gains a PipelineScope property; no Create or Release signature changes. ServiceProviderHandlerFactory stops keying a DI scope on IAmALifetime in a dictionary, and Release stops being the thing that disposes it. Behaviour-preserving: one DI scope per handler pipeline, released at the same point as today."
tags:
  - "lifetime"
  - "di"
  - "pipeline"
  - "handler"
---

# 71. Handler pipelines take their DI scope as a pipeline scope handle

Date: 2026-08-02

## Status

Proposed

## Context

ADR 0070 gave a transform pipeline one DI scope, carried as an `IAmAScope` handle and released when the pipeline is released. Handler pipelines already have a per-pipeline DI scope — that is the model 0070 copied — but they reach it by an entirely different route, and after 0070 the codebase would hold two mechanisms for one idea.

**Parent Requirement**: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md)

**Scope**: This ADR decides one thing — **a handler pipeline obtains and releases its DI scope through the same `IAmAScope` handle as a transform pipeline**. It is **behaviour-preserving**: it discharges no new requirement and changes nothing an application can observe. It exists to protect FR-7 while removing the divergence, and to give ADRs 0072 and 0073 one seam to build on instead of two.

It does **not** decide the *ambient* concept, `IAmAScopeProvider`, `ScopeAffinity`, adoption or borrowing, ASP.NET, the opt-in option on `IBrighterOptions`, `Publish`-subscriber ambient suppression, or the `ValidatePipelines()` rules of FR-22 — 0072 and 0073. It does not change **when** a handler pipeline has a DI scope, **which** lifetimes get one, or **when** it is released. `PipelineBuilder<TRequest>`'s eager per-subscriber resolution and its end-of-publish release stay exactly as they are (D10), and ADR 0039's DI scope per registered subscriber is preserved unchanged.

### How a handler pipeline reaches its DI scope today

1. `PipelineBuilder<TRequest>` creates one `HandlerLifetimeScope` per subscriber — `GetSyncInstanceScope()` (`:567`) and `GetAsyncInstanceScope()` (`:578`), each recording it in `_instanceScopes` (`:47`).
2. That object is passed as `IAmALifetime` on every `Create`: the subscriber's own handler at `:191`/`:236`, and each attribute decorator through `BuildPipeline` (`:272`), `BuildAsyncPipeline` (`:316`), `PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`), all of which thread the same `instanceScope`.
3. `ServiceProviderHandlerFactory` keys a DI scope on it: `_lifetimeScopes.GetOrAdd(lifetime, …)` over a `ConcurrentDictionary<IAmALifetime, ServiceProviderLifetimeScope>` (`:40`, `GetOrCreateLifetimeScope` `:127-131`).
4. `HandlerLifetimeScope` tracks each resolved handler (`Add`), and on `Dispose()` calls `factory.Release(handler, this)` for each.
5. `Release` (`:102-107`) delegates to `ReleaseLifetimeScope` (`:133-137`), which does `TryRemove` + `scope.Dispose()`. The **first** release disposes the DI scope; every later one silently finds nothing.
6. `PipelineBuilder.Dispose()` (`:269-270`) drains `_instanceScopes`, which is what starts step 4.

### The divergence ADR 0070 leaves

| | Handler pipeline (today) | Transform pipeline (ADR 0070) |
| --- | --- | --- |
| Per-pipeline object | `IAmALifetime` | the `TransformPipeline<TRequest>` itself |
| The DI scope is | created **inside the factory**, keyed on that object in a dictionary | created **by the factory** and handed out as an `IAmAScope` |
| Held by | nobody in core — core holds only the key | the pipeline |
| Disposed by | the factory, inside `Release` | the pipeline, in its drain |
| Cost per `Create` | a `ConcurrentDictionary` lookup | none |

Two mechanisms for one idea is the immediate cost. The larger one is ahead: **D2 fixes that a single option governs adoption for both handler pipelines and transform pipelines**, so ADR 0072 has to make a handler pipeline able to resolve from an ambient the host owns. Against the dictionary model that means teaching `GetOrCreateLifetimeScope` to sometimes not create, sometimes not own and sometimes not dispose — adoption implemented a second time, in a second shape. Against the handle model it is the same change 0072 already makes for transforms: `CreatePipelineScope()` returns a borrowed scope instead of an owned one.

### The forces

- **This must be behaviour-preserving.** FR-7 requires today's handler behaviour to be preserved and regression-guarded. One DI scope per handler pipeline, resolved from at the same points, disposed at the same point.
- **`Transient` is not only `Scoped`'s poor relation here.** The handler factory's per-pipeline `ServiceProviderLifetimeScope` serves `Transient` as well as `Scoped`, carrying `IsolateTransientHandlerScope` (`BrighterOptions.cs:37`) into it so each resolution gets its own inner DI scope (ADR 0067). C-6 forbids regressing that. So a handler pipeline takes a handle whenever its lifetime is **not** `Singleton` — where a transform pipeline takes one only under `Scoped`.
- **`IAmALifetime` is already threaded everywhere the scope is needed.** It reaches all six methods that resolve an artefact for a handler pipeline. Anything travelling beside it would travel beside it through every one of them.
- **`IAmALifetime` and `IAmAScope` must stay distinct** (NFR-8). One tracks handler instances so they can be released; the other is a DI scope handle. Neither becomes the other.
- **C-2 — the message pump is untouched.** `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder` and `ConsumerFactory` require no change.
- **Core must stay container-agnostic** (ADR 0014). `IAmAScope` names no container type, which is what lets it appear on a core interface at all.
- **Two more public interfaces break.** `netstandard2.0` has no default interface members. `IAmAHandlerFactory` is implemented by 19 classes in this repository (4 in `src/`, 15 test doubles); `IAmALifetime` by 7 (one in `src/`, internal, plus 6 test doubles).

## Decision

**A handler pipeline's DI scope is an `IAmAScope`, created by its handler factory, carried on the pipeline's `IAmALifetime`, and released when that lifetime scope is released.**

The per-pipeline object carries the scope. That is the same rule ADR 0070 states for transform pipelines; the two families differ only in *which* object plays the part, because a handler `Create` already receives one and a transform `Create` does not.

### Architecture Overview

```
                    Paramore.Brighter (core) — no container types
  ┌──────────────────────────────────────────────────────────────────────────┐
  │  PipelineBuilder<TRequest>.GetSyncInstanceScope / GetAsyncInstanceScope   │
  │                                                                          │
  │  1. scope = handlerFactory.CreatePipelineScope()          (may be null)   │
  │  2. new HandlerLifetimeScope(handlerFactory, scope)  ── holds it          │
  │  3. factory.Create(handlerType, instanceScope)      ── reads              │
  │     …and every decorator Create, unchanged             instanceScope     │
  │                                                        .PipelineScope    │
  │  4. HandlerLifetimeScope.Dispose():                                      │
  │        release tracked handlers, THEN dispose the scope                  │
  │     driven by PipelineBuilder.Dispose() — unchanged (:269)               │
  └──────────────────────────────────────────────────────────────────────────┘
                                   │  IAmAScope (opaque handle)
                                   ▼
        Paramore.Brighter.Extensions.DependencyInjection
  ┌──────────────────────────────────────────────────────────────────────────┐
  │  ServiceProviderPipelineScope : IAmAScope                                │
  │      owns one ServiceProviderLifetimeScope, configured with this          │
  │      factory's handler lifetime and its IsolateTransientHandlerScope      │
  └──────────────────────────────────────────────────────────────────────────┘
```

### Key Components

#### `IAmAHandlerFactory` gains the offer (core, public)

```csharp
namespace Paramore.Brighter
{
    public interface IAmAHandlerFactory
    {
        /// <summary>Creates a DI scope for one handler pipeline to resolve from, or null when this
        /// factory has none to offer. The caller owns the returned scope and must release it.</summary>
        IAmAScope? CreatePipelineScope();
    }
}
```

`IAmAHandlerFactory` (`IAmAHandlerFactory.cs:7`) is today a marker interface with no members, and both `IAmAHandlerFactorySync` (`:36`) and `IAmAHandlerFactoryAsync` (`:36`) derive from it. Putting the member there rather than on both twins means one declaration, one implementation in `ServiceProviderHandlerFactory` (which implements both), and no possibility of a factory answering the two twins differently. The cost is that `IAmAHandlerFactory` stops being a marker and becomes a contract — a fair description of what it now is.

This mirrors ADR 0070's `CreatePipelineScope()` exactly, including its contract: `null` means "no pipeline scope"; a throw is turned into `ConfigurationException` by the caller's existing guard.

#### `IAmALifetime` gains the handle (core, public)

```csharp
public interface IAmALifetime : IDisposable
{
    /// <summary>The DI scope this handler pipeline resolves from, or null when it has none.
    /// Owned by this lifetime scope and released when it is. Distinct from this interface's
    /// own job, which is tracking handler instances so they can be released.</summary>
    IAmAScope? PipelineScope { get; }

    void Add(IHandleRequests instance);        // unchanged
    void Add(IHandleRequestsAsync instance);   // unchanged
}
```

The scope rides on the object that already reaches every resolution site, so **no `Create` or `Release` signature changes** — the handler factory reads `lifetime.PipelineScope`.

The two responsibilities stay legible, and NFR-8's distinction survives: `IAmALifetime` *tracks handlers*; `IAmAScope` *is a DI scope*. The lifetime scope holds one, it does not become one. The XML documentation on both says so.

#### `HandlerLifetimeScope` — the ordering lives here (core, internal)

`HandlerLifetimeScope` (`:33`) takes the handle in its constructor, exposes it as `PipelineScope`, and in `Dispose()` releases every tracked handler **first** and disposes the handle **second**.

That ordering rule is the same one `TransformPipelineDrain` enforces for transform pipelines — artefacts go back to their factory before the DI scope they were resolved from dies, so a factory whose `Release` still has work to do is not left resolving against a dead scope. Putting it inside `HandlerLifetimeScope` means the one object that knows about both the handlers and the scope is the object that orders them, and `PipelineBuilder.Dispose()` (`:269-270`) needs no change at all.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| `Paramore.Brighter` | `IAmAHandlerFactory` (`:7`) | gains `IAmAScope? CreatePipelineScope()` |
| `Paramore.Brighter` | `IAmALifetime` (`:34`) | gains `IAmAScope? PipelineScope { get; }` |
| `Paramore.Brighter` | `HandlerLifetimeScope` (`:33`, `internal`) | takes and exposes the handle; disposes it after releasing tracked handlers |
| `Paramore.Brighter` | `PipelineBuilder<TRequest>` | `GetSyncInstanceScope()` (`:567`) and `GetAsyncInstanceScope()` (`:578`) ask the factory and pass the result to the `HandlerLifetimeScope` constructor. Nothing else |
| `Paramore.Brighter` | `SimpleHandlerFactorySync` (`:33`), `SimpleHandlerFactoryAsync` (`:33`) | `CreatePipelineScope()` returns `null` |
| `Paramore.Brighter.ServiceActivator` | `ControlBusHandlerFactorySync` (`:6`) | the same. It gains no container dependency — `IAmAScope` is a core type |
| `…DependencyInjection` | `ServiceProviderHandlerFactory` (`:34`) | implements `CreatePipelineScope()`; `Create` resolves through `lifetime.PipelineScope` |

Unchanged, and named so the omission is not read as an oversight: `IAmAHandlerFactorySync.Create`/`Release` and their async twins; `Pipelines<TRequest>` and `AsyncPipelines<TRequest>`; `BuildPipeline`, `BuildAsyncPipeline`, `PushOntoPipeline` and `AppendToPipeline`, which keep threading `IAmALifetime` and nothing beside it; `PipelineBuilder.Dispose()`; `CommandProcessor`; the pumps (C-2); `BrighterOptions`; and everything ADR 0070 decided for the transform family.

### Technology Choices

**Why the handle hangs off `IAmALifetime` rather than travelling as a second parameter.** A parameter is what ADR 0070 chose for the transform family, and the first instinct is to copy it exactly. It is the wrong fit here: `IAmALifetime` is already threaded through `BuildPipeline` (`:272`), `BuildAsyncPipeline` (`:316`), `PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`) as well as the two direct `Create` calls (`:191`, `:236`), so a scope parameter would travel beside it through all six, forever, as a second thing that is always passed with the first. Two parameters that are never apart are one parameter. Hanging the scope on the object that already makes the journey costs one property, changes no method signature, and keeps the rule identical in both families: *the per-pipeline object carries the scope*.

**Why `Create` and `Release` do not change.** The factory reads the scope off the argument it already has. `Release(handler, IAmALifetime)` then does what its own documentation already claims it does — nothing, for a `Singleton`, and for the rest, nothing either, because disposing the handler is the DI scope's job and the DI scope is now disposed by the lifetime scope. This is also why the transform family's `Release` was left alone in 0070: in both families the pipeline object owns the scope, so no `Release` may dispose it.

**`ServiceProviderPipelineScope` is configured by its creator.** ADR 0070 describes it as owning one `ServiceProviderLifetimeScope`. Here the handler factory constructs it with `new ServiceProviderLifetimeScope(_serviceProvider, _handlerLifetime, _isolateTransientHandlerScope)` — the same three arguments `GetOrCreateLifetimeScope` (`:127-131`) passes today. That is what makes `Transient` behaviour identical: the per-pipeline lifetime scope still isolates each resolution and still drains its outstanding inner scopes when the pipeline ends.

**The dictionary survives as the no-handle path.** `Create` may be called with a `lifetime` whose `PipelineScope` is `null` — a hand-rolled `IAmALifetime`, or a caller invoking the factory outside a `PipelineBuilder`. That case keeps `_lifetimeScopes` and today's `GetOrAdd`/`TryRemove` behaviour exactly, so no existing caller changes meaning. Brighter's own paths never take it, because `PipelineBuilder` always supplies a handle when the factory offers one.

### Implementation Approach

**1. Core.** Add the two members. `IAmAScope`'s XML documentation (ADR 0070) gains a sentence about handler pipelines; `IAmALifetime`'s gains the reciprocal one NFR-8 requires.

**2. `HandlerLifetimeScope`.** Constructor takes `IAmAScope? pipelineScope` after the factory arguments; the three existing constructors forward it. `Dispose()` becomes: release tracked handlers as today, then `PipelineScope?.Dispose()`, with the same hold-and-compose error handling ADR 0068 requires — a throwing handler release must not prevent the scope disposal, and neither failure may mask the other.

**3. `PipelineBuilder`.** In `GetSyncInstanceScope()` and `GetAsyncInstanceScope()`, ask the factory that is already in hand:

```csharp
private IAmALifetime GetSyncInstanceScope()
{
    if (_syncHandlerFactory is null)
        throw new NullReferenceException("HandlerFactorySync is null");

    var scope = new HandlerLifetimeScope(_syncHandlerFactory, _syncHandlerFactory.CreatePipelineScope());
    _instanceScopes.Add(scope);

    return scope;
}
```

Nothing else in `PipelineBuilder` changes. The scope is added to `_instanceScopes` inside the `HandlerLifetimeScope` it belongs to, so `Dispose()` (`:269-270`) already reaches it, and D10's release timing — every subscriber's scope drained together at end of publish, not tightened — is preserved by construction rather than by care.

**4. `ServiceProviderHandlerFactory`.** `CreatePipelineScope()` returns a new `ServiceProviderPipelineScope` when `_handlerLifetime` is not `Singleton`, and `null` when it is. Both `Create` overloads keep their `Singleton` branch on `_singletonScope` and, for the other two lifetimes, resolve through `lifetime.PipelineScope` when it is a `ServiceProviderPipelineScope`, falling back to `GetOrCreateLifetimeScope` when it is not.

**5. Behaviour by configured lifetime.** Nothing in this column changes; only where the scope comes from does.

| Handler lifetime | `CreatePipelineScope()` | Resolution and reclamation | Changed? |
| --- | --- | --- | --- |
| `Transient` | a handle over a `Transient` lifetime scope carrying `IsolateTransientHandlerScope` | each resolution gets its own inner DI scope, all drained when the pipeline ends — ADR 0067 unchanged | **No** |
| `Scoped` | a handle over a `Scoped` lifetime scope | one DI scope for the pipeline; one artefact per type; disposed when the pipeline ends | **No** |
| `Singleton` | `null` | the root provider, one artefact per process | **No** |

**6. Regression guards.** `FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs:36`) and its async twin (`:154`) drive `Create` with a hand-rolled `TestLifetimeScope`, so after this change they exercise the **no-handle** path and keep passing unchanged. That is useful — it pins the fallback — but it means they no longer guard the path Brighter itself takes. **Both must be duplicated onto the handle path**, resolving through a `TestLifetimeScope` whose `PipelineScope` is the factory's own, or FR-7's guarantee is guarded only where it no longer applies.

## Consequences

### Positive

- **One mechanism.** Both families now say the same thing: a factory offers a scope, the per-pipeline object holds it, and disposes it after its artefacts have gone back. One story to teach, one ordering rule, one handle type.
- **ADR 0072 builds adoption once.** A borrowed ambient becomes what `CreatePipelineScope()` returns, for handler pipelines and transform pipelines alike. Under the dictionary model the handler side would have needed its own "sometimes do not create, sometimes do not own, sometimes do not dispose" variant.
- **`Release` stops having a hidden second job.** Today the first `Release` on a pipeline disposes the DI scope and the rest silently find nothing — behaviour that is invisible at the call site and depends on iteration order inside `HandlerLifetimeScope.Dispose()`. Now disposal happens once, in one place, explicitly.
- **A dictionary lookup leaves the resolution path.** Every handler and every decorator resolution currently pays a `ConcurrentDictionary` `GetOrAdd` keyed on an object with reference identity; now the handle is a field read.
- **A latent leak closes.** `Create` populates `_lifetimeScopes` before it resolves, and only `Release` removes the entry. A pipeline whose handler fails to resolve — `Create` returns null and `PipelineBuilder` throws `ConfigurationException` (`:192-193`) — never tracks a handler, so `Release` is never called and that entry, with its `ServiceProviderLifetimeScope`, stays for the life of the process. Under this ADR `HandlerLifetimeScope.Dispose()` disposes the handle unconditionally.
- **No signature changes on `Create` or `Release`**, so the six methods that thread `IAmALifetime` are untouched and the diff stays small where the logic is dense.

### Negative

- **Two more public interfaces break at compile time.** `IAmAHandlerFactory` (19 implementations here: 4 in `src/`, 15 test doubles) and `IAmALifetime` (7: one internal `src/` class, 6 test doubles). On `netstandard2.0` there is no default interface member to absorb either. **Needs a release note** with the migration: `CreatePipelineScope()` returns `null`, `PipelineScope` returns `null`, unless you want pipeline scoping.
- **This ADR delivers no behaviour.** Nothing an application can observe changes. It is a structural change taken for the sake of the two ADRs after it, and it costs a breaking change to do it — a legitimate thing to argue about, and the reason *Alternatives* below states the do-nothing option first.
- **`IAmALifetime` now holds something that is not a handler.** The name was already close enough to `IAmAScope` to need NFR-8; putting an `IAmAScope` *on* it narrows the gap further. Mitigated only by documentation, which is a weaker mitigation than a better name would have been.
- **The no-handle path survives in `ServiceProviderHandlerFactory`.** `_lifetimeScopes`, `GetOrCreateLifetimeScope` and `ReleaseLifetimeScope` remain for callers that supply no handle, so the factory carries two resolution paths and two disposal paths where the goal was one. Brighter's own code never takes the second, which is exactly what makes it easy to leave rotting.
- **The FR-7 regression guards move to the path that no longer matters.** `FactoryLifetimeTests`' two tests keep passing precisely because they use the fallback; new tests on the handle path are required work, not a nicety.
- **`HandlerLifetimeScope.Dispose()` gains error-composition logic** it did not have — today it releases handlers and cannot fail meaningfully; now a handler release and a scope disposal can both throw and neither may mask the other.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| `Transient` handler behaviour drifts — the per-resolution isolation of ADR 0067, or `IsolateTransientHandlerScope` | `CreatePipelineScope()` constructs the `ServiceProviderLifetimeScope` with the same three arguments `GetOrCreateLifetimeScope` passes today, including the flag. A handler pipeline takes a handle for `Transient` as well as `Scoped`, unlike a transform pipeline. C-6 |
| Release timing changes, tightening or loosening D10 | The handle is disposed by `HandlerLifetimeScope.Dispose()`, which `PipelineBuilder.Dispose()` (`:269-270`) already drives at exactly the point the DI scope is disposed today. `PipelineBuilder` needs no new list and no new disposal call |
| `Publish` subscriber isolation regresses (ADR 0039) | One `HandlerLifetimeScope`, and therefore one handle, per subscriber, created in the same loop as today (`:190`, `:235`). Nothing is shared between subscribers |
| A decorator resolves from a different scope than its handler | Decorators resolve through the same `IAmALifetime` instance they do today (`:272`, `:316`, `:430`, `:499`), and the handle is a property of it. Same object, same scope, by construction |
| Double disposal of the handle | `HandlerLifetimeScope.Dispose()` is the only place that disposes it, and `IAmAScope`'s disposal is idempotent (ADR 0070). `Release` disposes nothing on this path |
| The surviving no-handle path silently diverges from the handle path over time | Both are exercised: `FactoryLifetimeTests`' existing pair pins the fallback, and the duplicated pair required above pins the handle path |
| Terminology drift between `IAmAScope`, `IAmALifetime`, `HandlerLifetimeScope`, `ServiceProviderLifetimeScope` and `TransformLifetimeScope` | NFR-8: XML documentation on `IAmALifetime` and `IAmAScope` states what each is for and how they relate, now including that one holds the other; `docs/guides/lifetimes-and-scoping.md` (FR-25) carries the same distinction |

## Alternatives Considered

**1. Do nothing — leave handler pipelines on the dictionary.** ADR 0070 stands on its own, the handler family keeps working, and no interface breaks. **Rejected**, but it is the serious alternative. It leaves two mechanisms for one idea in a codebase where the defect being fixed was itself a divergence between two factory families, and it pushes the cost to ADR 0072, which must then implement ambient adoption twice — once against a handle it is handed, once against a dictionary it owns — with `Publish` subscriber isolation (FR-8) to get right in both. The breaking change is cheaper now than the second adoption path is later.

**2. Copy ADR 0070 exactly: a scope parameter on `Create`.** `Create(Type, IAmALifetime, IAmAScope? scope = null)`, symmetric with the transform family. **Rejected**: the handler resolution path threads `IAmALifetime` through `BuildPipeline` (`:272`), `BuildAsyncPipeline` (`:316`), `PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`) as well as the two direct `Create` calls, so the scope would be a second parameter travelling beside the first through all six, permanently. Two parameters that are never apart should be one. The transform family takes a parameter only because its `Create(Type)` has no per-pipeline object to hang anything on — that absence is the problem ADR 0070 exists to solve, not a shape to imitate.

**3. Replace `IAmALifetime` with `IAmAScope`.** One per-pipeline type instead of two: delete `HandlerLifetimeScope`'s tracking and let the DI scope's disposal reclaim the handlers. Maximum alignment. **Rejected**: the two do genuinely different jobs, and only one of them is Brighter's to own. `IAmALifetime` exists so that a **user-supplied** handler factory gets a `Release` call per handler — a factory that pools handlers, or one over a container that requires explicit release, depends on it. A DI scope's disposal reclaims what *the container* created, not what someone else's factory did. NFR-8 keeps them distinct for the same reason, and ADR 0072 will implement `IAmAScope` over a borrowed request scope that has no business tracking anyone's handlers.

**4. Give the handler family a token and a dictionary, and make the transform family match it.** Converge in the other direction: leave `ServiceProviderHandlerFactory` as it is and have the mapper and transformer factories key their scopes on a shared per-pipeline token too. **Rejected — it cannot work.** A transform pipeline is served by *two* factories built at two different construction sites; a per-factory dictionary keyed on the same token hands each of them its own DI scope, which is Defect 1b, unfixed. A dictionary shared across both factories is a package-level table — the ambient rejected as ADR 0070's Alternative 2. Only the handle can carry a scope between two factories, so convergence can only run in this direction.

**5. Hold the handles in a second list on `PipelineBuilder`.** `_pipelineScopes` beside `_instanceScopes`, both drained in `Dispose()`. Keeps `IAmALifetime` unchanged, so one fewer interface breaks. **Rejected**: two lists that must stay index-aligned and be disposed in a fixed order relative to one another, with the ordering rule living in `PipelineBuilder` rather than in the object that knows about both handlers and scope. It also leaves the factory unable to reach the handle, so alternative 2's parameter comes back with it.

**6. Put `CreatePipelineScope()` on both handler factory twins rather than on `IAmAHandlerFactory`.** Symmetric with the transform family, where the four interfaces each declare it. **Rejected**: `IAmAHandlerFactory` already exists as the shared base (`IAmAHandlerFactory.cs:7`) and the transform family only declares it four times because it has no such base. Declaring it twice would let a factory implementing both twins answer them differently, which has no meaning — a pipeline has one scope.

## References

- Requirements: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md) — FR-7, NFR-4, NFR-5, NFR-6, NFR-7, NFR-8, C-1, C-2, C-6, D0, D2, D10
- Related ADRs (cited by slug — ADR numbers are not unique in this repo, C-16):
  - `0070-per-pipeline-di-scope-for-mapper-and-transform-factories` [Proposed] — `IAmAScope`, `CreatePipelineScope()` and the per-pipeline DI scope for transform pipelines; this ADR brings handler pipelines onto it
  - `0067-per-resolution-di-scope-for-transient-factory-instances` [Accepted] — the `Transient` per-resolution DI scope and `IsolateTransientHandlerScope`, preserved here
  - `0068-deterministic-disposal-finalizer-safety-net` [Accepted] — the error-composition rule `HandlerLifetimeScope.Dispose()` now follows
  - `0039-scoping-dependencies-inline-with-lifetime-scope` [Proposed] — a DI scope per registered subscriber on `Publish`; preserved, not reopened
  - `0033-lifetime-of-command-processor-and-mediator` [Proposed] — the `CommandProcessor` is a singleton; not reopened (C-5)
  - `0014-di-friendly-framework` [Accepted] — per-family factory interfaces rather than abstracting an IoC container; the principle that keeps `IAmAScope` container-free
  - `0005-support-async-pipelines` [Accepted] — why the sync/async handler factory twins exist, and why the shared base is the right home for a member that is not per-twin
- External references:
  - [Dependency injection guidelines — scope validation and captive dependencies](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
  - Wirfs-Brock & McKean, *Object Design: Roles, Responsibilities, and Collaborations* — the role/stereotype vocabulary that keeps `IAmALifetime` (a tracker) and `IAmAScope` (an information holder) apart
