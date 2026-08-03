---
id: 0071-pipeline-scope-handle-for-handler-pipelines
title: "Handler pipelines take their DI scope as a pipeline scope handle"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-08-02
summary: "Handler pipelines obtain their per-pipeline DI scope as the same IAmAScope handle that ADR 0070 gives transform pipelines, carried on the per-pipeline object the handler factories already receive. IAmAHandlerFactory gains CreatePipelineScope() and IAmALifetime gains a PipelineScope property; no Create or Release signature changes. On the path Brighter itself takes, ServiceProviderHandlerFactory stops keying a DI scope on IAmALifetime in a dictionary and Release stops being the thing that disposes it; the dictionary survives as a fallback for callers that supply no handle. Behaviour-preserving: one DI scope per handler pipeline, released at the same point as today."
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

**Scope**: This ADR decides one thing — **a handler pipeline obtains and releases its DI scope through the same `IAmAScope` handle as a transform pipeline**. It is **behaviour-preserving**: it discharges no new requirement and changes nothing an application can observe. It exists to protect FR-7 while removing the divergence, and to give ADR 0072 and the ADRs after it one seam to build on instead of two.

It does **not** decide the *ambient* concept, `IAmAScopeProvider`, `ScopeAffinity`, adoption or borrowing, ASP.NET, the opt-in option on `IBrighterOptions`, `Publish`-subscriber ambient suppression, or the `ValidatePipelines()` rules of FR-22. Each is deferred, and to a different sibling: adoption is ADR 0072's, ASP.NET is 0073's, FR-22's rules are 0074's, suppression is 0075's, and the opt-in option is 0076's. It does not change **when** a handler pipeline has a DI scope, **which** lifetimes get one, or **when** it is released. `PipelineBuilder<TRequest>`'s eager per-subscriber resolution and its end-of-publish release stay exactly as they are (D10), and ADR 0039's DI scope per registered subscriber is preserved unchanged.

### Where this ADR sits

Seven ADRs deliver the parent requirement, one decision each. This is the second, and the only one that delivers no behaviour.

| ADR | Decides |
| --- | --- |
| 0070 | a transform pipeline takes one DI scope, carried **as a parameter** |
| **0071** *(this one)* | handler pipelines converge onto the **same handle**, carried on the object they already pass |
| 0072 | how a pipeline discovers an **ambient** DI scope the host owns |
| 0073 | the **ASP.NET Core package**, and the one line an application writes to opt in |
| 0074 | **where** the six scope-configuration rules are evaluated |
| 0075 | how a `Publish` subscriber **suppresses** adoption, for itself and everything nested beneath it |
| 0076 | the **affinity option**, and how one setting reaches all four registration paths in any order |

ADR 0070's rule is the one this ADR applies: **the per-pipeline object carries the DI scope.** The two families differ only in *which* object plays that part. A transform `Create(Type)` has no per-pipeline object, so 0070 had to add a parameter; a handler `Create(Type, IAmALifetime)` already receives one, so here the scope rides on it and no signature changes at all.

### How a handler pipeline reaches its DI scope today

The DI scope exists, and it is already per pipeline — but core never holds it. Core holds the *key*, the factory holds the scope in a dictionary keyed on that key, and `Release` is what disposes it:

```mermaid
sequenceDiagram
    autonumber
    participant Builder as PipelineBuilder
    participant Lifetime as HandlerLifetimeScope
    participant Factory as ServiceProviderHandlerFactory
    participant Dict as the _lifetimeScopes dictionary

    Builder->>Lifetime: new, one per subscriber
    Builder->>Factory: Create(handlerType, lifetime)
    Factory->>Dict: GetOrAdd, keyed on the IAmALifetime
    Dict-->>Factory: this pipeline's ServiceProviderLifetimeScope
    Factory-->>Builder: the handler
    Builder->>Lifetime: Add(handler), and again for every decorator

    Note over Builder,Dict: at end of publish
    Builder->>Lifetime: Dispose
    Lifetime->>Factory: Release(handler, this), once per tracked handler
    Factory->>Dict: TryRemove, then dispose the DI scope
    Note right of Dict: the FIRST Release disposes it —<br/>every later one silently finds nothing
```

Step by step, with the code:

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
- **`IAmALifetime` is already threaded everywhere the scope is needed.** It reaches every site that resolves an artefact for a handler pipeline — the two direct `Create` calls and the four decorator resolutions — and it does so unavoidably, because both `Create` signatures take a non-nullable `IAmALifetime` (`IAmAHandlerFactorySync.cs:44`). No resolution site can exist without one. Anything travelling beside it would travel beside it through every one of them.
- **`IAmALifetime` and `IAmAScope` must stay distinct** (NFR-8). One tracks handler instances so they can be released; the other is a DI scope handle. Neither becomes the other.
- **C-2 — the message pump is untouched.** `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder` and `ConsumerFactory` require no change.
- **NFR-4 — what the `ConcurrentDictionary` is currently buying, and what replaces it.** Today the per-pipeline DI scope is created by `_lifetimeScopes.GetOrAdd` and reclaimed by `TryRemove` (`ServiceProviderHandlerFactory.cs:129`, `:135`), and that `TryRemove` is what makes a concurrent double-`Release` dispose exactly once. Removing the dictionary removes that guarantee, so the design has to supply confinement instead of atomicity, and it does: one `HandlerLifetimeScope` is constructed per subscriber and never leaves the `PipelineBuilder` that made it, `CreatePipelineScope()` reads no shared state and shares nothing between calls, and `PipelineScope` is fixed at construction so every reader sees the same value without coordinating. Disposal is issued from exactly one place — `PipelineBuilder.Dispose()` (`:269-270`) — on one thread, and `IAmAScope`'s idempotent disposal (ADR 0070) covers the rest. `Publish`'s `Parallel.ForEach` (`CommandProcessor.cs:481`) runs *subscribers* concurrently, and each has its own handle, so concurrency is between pipelines and never within one.
- **D0 and C-1 — the unit is the pipeline, and nesting is a new pipeline, not a nested scope.** A handler that issues a `Send`, a `Post` or a `Publish` builds a fresh `PipelineBuilder` with its own `HandlerLifetimeScope` and its own handle. Microsoft's DI scopes do not nest (C-1) — a scope created from a scoped provider is root-parented — so a nested pipeline's DI scope is a sibling of its caller's, never a child, and its disposal is independent. That is true today and this ADR does not change it; it is stated because the handle makes the relationship look hierarchical when it is not.
- **Core must stay container-agnostic** (ADR 0014, NFR-1). `IAmAScope` names no container type, which is what lets it appear on a core interface at all — and it is what keeps both new members implementable over Autofac or SimpleInjector as readily as over Microsoft's container (NFR-7).
- **Two more public interfaces break.** `netstandard2.0` has no default interface members. `IAmAHandlerFactory` is implemented by 21 classes in this repository (5 in `src/`, 16 test doubles); `IAmALifetime` by 7 (one in `src/`, internal, plus 6 test doubles). One of the 21 implements the **bare marker** and has no body at all — `sealed class DummyHandlerFactory : IAmAHandlerFactory;` — so it gains one.

## Decision

**A handler pipeline's DI scope is an `IAmAScope`, created by its handler factory, carried on the pipeline's `IAmALifetime`, and released when that lifetime scope is released.**

The per-pipeline object carries the DI scope. That is the same rule ADR 0070 states for transform pipelines; the two families differ only in *which* object plays the part, because a handler `Create` already receives one and a transform `Create` does not.

### The mechanism, end to end

Compare this with the *today* diagram above. The same three things happen at the same three moments — the dictionary is gone, the handle is held by the object core already owns, and `Release` no longer disposes anything.

```mermaid
sequenceDiagram
    autonumber
    participant Builder as PipelineBuilder
    participant Factory as the handler factory
    participant Lifetime as HandlerLifetimeScope
    participant Scope as IAmAScope

    Note over Builder,Scope: ACQUIRE — once per subscriber
    Builder->>Factory: CreatePipelineScope()
    Factory-->>Builder: IAmAScope, or null when the lifetime is Singleton
    Builder->>Lifetime: new, holding the handle as PipelineScope

    Note over Builder,Scope: SHARE — no signature changes
    Builder->>Factory: Create(handlerType, lifetime)
    Factory->>Lifetime: read lifetime.PipelineScope
    Factory-->>Builder: the handler, resolved from that scope
    Builder->>Lifetime: Add(handler), and again for every decorator

    Note over Builder,Scope: RELEASE — driven by PipelineBuilder.Dispose(), unchanged
    Builder->>Lifetime: Dispose
    Lifetime->>Factory: Release(handler, this), once per tracked handler
    Lifetime->>Scope: dispose the handle, last and unconditionally
```

Three consequences fall straight out of the diagram. `Create` and `Release` keep their signatures, because the scope travels on an argument they already take. The ordering rule — artefacts back to their factory *before* the DI scope they came from dies — is the same rule ADR 0070 adds to `TransformPipelineDrain` as its third drain step, and here it lives in the one object that knows about both. And the handle is disposed **unconditionally**, which closes a latent leak: today a pipeline whose handler fails to resolve never calls `Release`, so its dictionary entry survives for the life of the process.

### Where the pieces live

```mermaid
flowchart TB
    subgraph core["Paramore.Brighter — core, names no container type"]
        factory["IAmAHandlerFactory<br/>today a bare marker<br/>NEW: CreatePipelineScope"]
        lifetime["IAmALifetime<br/>tracks handler instances so they can be released<br/>NEW: also holds the pipeline's IAmAScope"]
        builder["PipelineBuilder<br/>GetSyncInstanceScope and GetAsyncInstanceScope<br/>ask the factory, pass the answer to the lifetime scope"]
        hls["HandlerLifetimeScope, internal<br/>owns the ordering: release handlers, THEN dispose the handle"]
        handle["IAmAScope, from ADR 0070"]
        builder --> hls
        builder -. "calls CreatePipelineScope()" .-> factory
        hls -- "implements" --> lifetime
        hls --> handle
    end

    subgraph di["Paramore.Brighter.Extensions.DependencyInjection"]
        sphf["ServiceProviderHandlerFactory<br/>CreatePipelineScope offers a handle unless the lifetime is Singleton<br/>Create resolves through lifetime.PipelineScope"]
        scope["ServiceProviderPipelineScope<br/>owns one ServiceProviderLifetimeScope, configured with this factory's<br/>handler lifetime and its IsolateTransientHandlerScope"]
        sphf --> scope
    end

    sphf -- "implements" --> factory
    scope -- "implements" --> handle
```

**Reading the edges**, on the same convention ADR 0070 uses: a solid arrow is a compile-time reference or an ownership, a dotted arrow is a runtime call. Both edges crossing the boundary are solid and run from the DI package into core, which is the real reference direction. The one call edge stays inside core and lands on the interface — the builder asks `IAmAHandlerFactory`, not the class implementing it, which is why core needs no knowledge that a container exists.

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Stereotype | Responsibility |
| --- | --- | --- | --- |
| Scope offerer | `IAmAHandlerFactory` (core) | **deciding** | Answers, for one handler pipeline, whether it has a DI scope to offer. `null` for `Singleton`; a handle for `Transient` and `Scoped` alike |
| Handler tracker **and** scope holder | `IAmALifetime` (core) | **knowing**, two things | Tracks handler instances so they can be released — its existing job — and now also carries the handle for the pipeline they were resolved from |
| Release ordering | `HandlerLifetimeScope` (core, internal) | **doing** | Releases every tracked handler, then disposes the handle. Never the other way round |
| Scope acquirer | `PipelineBuilder<TRequest>` (core) | **doing** (structurer) | Asks the factory once per subscriber and hands the answer to that subscriber's lifetime scope. Nothing else changes |
| Scope implementation | `ServiceProviderPipelineScope` (DI package) | **knowing** | Owns one `ServiceProviderLifetimeScope`, configured with the handler lifetime and the isolate flag, so `Transient` behaviour is identical |

Loading two responsibilities onto `IAmALifetime` is the cost this ADR pays, and it is paid deliberately: NFR-8 keeps `IAmALifetime` and `IAmAScope` distinct concepts, and the lifetime scope *holds* a scope rather than *becoming* one. *Consequences* records that the name gets no easier to read for it.

#### `IAmAHandlerFactory` gains the offer (core, public)

```csharp
namespace Paramore.Brighter
{
    public interface IAmAHandlerFactory
    {
        /// <summary>Creates a DI scope for one handler pipeline to resolve from, or null when this
        /// factory has none to offer. The caller must always release the returned handle; releasing it
        /// may or may not dispose an underlying scope, and the handle alone knows which.</summary>
        IAmAScope? CreatePipelineScope();
    }
}
```

`IAmAHandlerFactory` (`IAmAHandlerFactory.cs:7`) is today a marker interface with no members, and both `IAmAHandlerFactorySync` (`:36`) and `IAmAHandlerFactoryAsync` (`:36`) derive from it. Putting the member there rather than on both twins means one declaration, one implementation in `ServiceProviderHandlerFactory` (which implements both), and no possibility of a factory answering the two twins differently. The cost is that `IAmAHandlerFactory` stops being a marker and becomes a contract — a fair description of what it now is.

The member's **shape** is ADR 0070's, and so is its throw behaviour — a throw is turned into `ConfigurationException` by the caller's existing guard. Its **null rule is not**, and the difference matters to an implementor. A transform factory offers nothing unless its configured lifetime is `Scoped`; a handler factory offers a handle for `Transient` too, because ADR 0067's per-resolution scope rides on the same `ServiceProviderLifetimeScope` object and would regress without one (C-6). Applying 0070's rule here — `null` for anything that is not `Scoped` — is the mistake this paragraph exists to prevent.

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

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `IAmAHandlerFactory.CreatePipelineScope()` | none | an `IAmAScope` the caller must release for `Transient` and `Scoped` alike; `null` for `Singleton`, and `null` from any factory that is not container-backed | May throw if the container cannot create a scope; the caller's existing guard turns that into `ConfigurationException` (`PipelineBuilder.cs:179-205`). It is called **once per pipeline**, by `PipelineBuilder` when it constructs the `HandlerLifetimeScope` |
| `IAmALifetime.PipelineScope` | none | the handle this lifetime scope was constructed with, or `null` | Never throws. It is a **stable** property: the value is fixed at construction and does not change between reads, so a factory may read it on every `Create` without coordinating. `null` under a non-`Singleton` lifetime is not an error — it is the no-handle path below. Reading it after the lifetime scope has been disposed is the caller's error, not this property's |

**A handle this factory does not recognise is ignored, not rejected** — the same rule ADR 0070 states for `Create(Type, IAmAScope?)`, and it is one rule for one design rather than two. Where `PipelineScope` is non-null but is not a `ServiceProviderPipelineScope`, `ServiceProviderHandlerFactory` resolves through `GetOrCreateLifetimeScope` exactly as it does today. The consequence is stated rather than buried: **two DI scopes then exist for that pipeline** — the unrecognised handle, disposed by `HandlerLifetimeScope` as it disposes any handle it holds, and Brighter's own, reclaimed by `ReleaseLifetimeScope` on the first `Release` (`ServiceProviderHandlerFactory.cs:133-137`) exactly as before this ADR. On that path the leak this ADR closes is **not** closed: a `Create` whose handler is never tracked leaves the dictionary entry keyed on a dead `IAmALifetime`.

None of Brighter's own paths can reach it. `PipelineBuilder` constructs the `HandlerLifetimeScope` with a handle from the same factory it then calls, and ADR 0072's ladder declines an unusable ambient before any handle is produced, so a foreign handle arrives only from outside the dispatch path — a caller invoking the public `Create(Type, IAmALifetime)` with an `IAmALifetime` of its own, or a lifetime scope built by one factory and passed to another. That is why NFR-5's and NFR-6's per-pipeline budgets are not breached by this rule: they bound what Brighter does, and Brighter does not do this.

The two responsibilities stay legible, and NFR-8's distinction survives: `IAmALifetime` *tracks handlers*; `IAmAScope` *is a DI scope*. The lifetime scope holds one, it does not become one. The XML documentation on both says so.

#### `HandlerLifetimeScope` — the ordering lives here (core, internal)

`HandlerLifetimeScope` (`:33`) takes the handle in its constructor, exposes it as `PipelineScope`, and in `Dispose()` releases every tracked handler **first** and disposes the handle **second**.

That ordering rule is the one ADR 0070 gives transform pipelines, where it lands as `TransformPipelineDrain`'s new third step — artefacts go back to their factory before the DI scope they were resolved from dies, so a factory whose `Release` still has work to do is not left resolving against a dead scope. Neither family enforces it today, because until 0070 neither pipeline object held a DI scope to order against. Putting it inside `HandlerLifetimeScope` means the one object that knows about both the handlers and the scope is the object that orders them, and `PipelineBuilder.Dispose()` (`:269-270`) needs no change at all.

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

`Paramore.Brighter.ServiceActivator` keeps its current dependency set — a single project reference to `Paramore.Brighter`, no package reference — because `ControlBusHandlerFactorySync`'s new member names only core types (NFR-3).

### Technology Choices

**Why the handle hangs off `IAmALifetime` rather than travelling as a second parameter.** A parameter is what ADR 0070 chose for the transform family, and the first instinct is to copy it exactly. It is the wrong fit here: `IAmALifetime` is already threaded through `BuildPipeline` (`:272`), `BuildAsyncPipeline` (`:316`), `PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`) as well as the two direct `Create` calls (`:191`, `:236`), so a scope parameter would travel beside it through all six, forever, as a second thing that is always passed with the first. Two parameters that are never apart are one parameter. Hanging the scope on the object that already makes the journey costs one property, changes no method signature, and keeps the rule identical in both families: *the per-pipeline object carries the DI scope*.

**Why `Create` and `Release` do not change.** The factory reads the scope off the argument it already has. `Release(handler, IAmALifetime)` then does what its own documentation already claims it does — nothing, for a `Singleton`, and for the rest, nothing either, because disposing the handler is the DI scope's job and the DI scope is now disposed by the lifetime scope. This is also why the transform family's `Release` was left alone in 0070: in both families the pipeline object owns the scope, so no `Release` may dispose it.

**`ServiceProviderPipelineScope` is configured by its creator.** ADR 0070 describes it as owning one `ServiceProviderLifetimeScope`. Here the handler factory constructs it with `new ServiceProviderLifetimeScope(_serviceProvider, _handlerLifetime, _isolateTransientHandlerScope)` — the same three arguments `GetOrCreateLifetimeScope` (`:127-131`) passes today. That is what makes `Transient` behaviour identical: the per-pipeline lifetime scope still isolates each resolution and still drains its outstanding inner scopes when the pipeline ends.

**The dictionary survives as the no-handle path.** `Create` may be called with a `lifetime` whose `PipelineScope` is `null` — a hand-rolled `IAmALifetime`, or a caller invoking the factory outside a `PipelineBuilder`. That case keeps `_lifetimeScopes` and today's `GetOrAdd`/`TryRemove` behaviour exactly, so no existing caller changes meaning. Brighter's own paths never take it, because `PipelineBuilder` always supplies a handle when the factory offers one.

### Implementation Approach

**1. Core.** Add the two members. `IAmAScope`'s XML documentation (ADR 0070) gains a sentence about handler pipelines; `IAmALifetime`'s gains the reciprocal one NFR-8 requires.

**2. `HandlerLifetimeScope`.** Constructor takes `IAmAScope? pipelineScope` after the factory arguments; the three existing constructors forward it. `Dispose()` is rewritten to the hold-and-compose shape ADR 0068 requires, and the fault tolerance runs **handler to handler**, not merely between the releases and the disposal:

- release every tracked sync handler, then every tracked async handler, catching per item and holding the failure rather than letting it abort the loop — today a throw from the first tracked handler skips every remaining `Release` *and* both `Clear()` calls (`HandlerLifetimeScope.cs:74-93`, no `try`/`catch` anywhere);
- clear both tracking lists unconditionally, so the scope does not outlive its disposal holding references;
- dispose `PipelineScope` last and unconditionally, holding any failure;
- if anything was held, throw them composed as an `AggregateException`, so no failure masks another.

This is the shape `TransformPipelineDrain` already has on the transform side, and it is the reason step 6 adds a mirror of that family's regression test.

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

**4. `ServiceProviderHandlerFactory`.** `CreatePipelineScope()` returns a new `ServiceProviderPipelineScope` when `_handlerLifetime` is not `Singleton`, and `null` when it is. Both `Create` overloads keep their `Singleton` branch on `_singletonScope` and, for the other two lifetimes, resolve through `lifetime.PipelineScope` when it is a `ServiceProviderPipelineScope`. Where it is `null`, or is a handle this factory does not recognise, they fall back to `GetOrCreateLifetimeScope` — today's behaviour exactly, on today's terms, with the consequences the contract above sets out.

**5. Behaviour by configured lifetime.** Nothing in this column changes; only where the scope comes from does.

| Handler lifetime | `CreatePipelineScope()` | Resolution and reclamation | Changed? |
| --- | --- | --- | --- |
| `Transient` | a handle over a `Transient` lifetime scope carrying `IsolateTransientHandlerScope` | each resolution gets its own inner DI scope, all drained when the pipeline ends — ADR 0067 unchanged | **No** |
| `Scoped` | a handle over a `Scoped` lifetime scope | one DI scope for the pipeline; one artefact per type; disposed when the pipeline ends | **No** |
| `Singleton` | `null` | the root provider, one artefact per process | **No** |

**6. Regression guards.** `FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs:36`) and its async twin (`:154`) drive `Create` with a hand-rolled `TestLifetimeScope`, so after this change they exercise the **no-handle** path and keep passing unchanged. That is useful — it pins the fallback — but it means they no longer guard the path Brighter itself takes. The requirements designate exactly this pair as the regression guards for **AC-9**, FR-7's acceptance criterion, and require them to keep passing unchanged; they will, and that is the problem. **Both must be duplicated onto the handle path**, resolving through a `TestLifetimeScope` whose `PipelineScope` is the factory's own, and the "regression guard for AC-9" designation attaches to the duplicated pair — otherwise FR-7's guarantee is guarded only where it no longer applies.

A second test is required work, not a nicety: the mirror of `When_a_transform_release_throws_the_scope_still_releases_the_rest` for the handler family. A `HandlerLifetimeScope` holding three tracked handlers, whose factory's `Release` throws on the first, must still release the other two, still clear both tracking lists, still dispose the handle, and surface the failure composed. Nothing today asserts any of that, which is why the defect step 2 repairs has survived.

## Consequences

### Positive

- **One mechanism, with one stated exception.** Both families now say the same thing: a factory offers a scope, the per-pipeline object holds it, and disposes it after its artefacts have gone back. One story to teach, one ordering rule, one handle type. They differ in *how* the disposal is issued — see the asymmetry recorded under *Negative* — but not in what is disposed, when, or in what order.
- **ADR 0072 builds adoption once.** A borrowed ambient becomes what `CreatePipelineScope()` returns, for handler pipelines and transform pipelines alike. Under the dictionary model the handler side would have needed its own "sometimes do not create, sometimes do not own, sometimes do not dispose" variant.
- **`Release` stops having a hidden second job.** Today the first `Release` on a pipeline disposes the DI scope and the rest silently find nothing — behaviour that is invisible at the call site and depends on iteration order inside `HandlerLifetimeScope.Dispose()`. Now disposal happens once, in one place, explicitly.
- **A dictionary lookup leaves the resolution path.** Every handler and every decorator resolution currently pays a `ConcurrentDictionary` `GetOrAdd` keyed on an object with reference identity; now the handle is a field read. The atomicity that `GetOrAdd`/`TryRemove` supplied is replaced by confinement rather than dropped — one handle per subscriber, never shared, disposed from one place — which the forces bullet on NFR-4 sets out.
- **A latent leak closes.** `Create` populates `_lifetimeScopes` before it resolves, and only `Release` removes the entry. A pipeline whose handler fails to resolve — `Create` returns null and `PipelineBuilder` throws `ConfigurationException` (`:192-193`) — never tracks a handler, so `Release` is never called and that entry, with its `ServiceProviderLifetimeScope`, stays for the life of the process. Under this ADR `HandlerLifetimeScope.Dispose()` disposes the handle unconditionally.
- **No signature changes on `Create` or `Release`**, so the six methods that thread `IAmALifetime` are untouched and the diff stays small where the logic is dense.

### Negative

- **Two more public interfaces break at compile time.** `IAmAHandlerFactory` (21 implementations here: 5 in `src/`, 16 test doubles) and `IAmALifetime` (7: one internal `src/` class, 6 test doubles). On `netstandard2.0` there is no default interface member to absorb either. **Needs a release note** with the migration: `CreatePipelineScope()` returns `null`, `PipelineScope` returns `null`, unless you want pipeline scoping.
- **This ADR delivers no behaviour.** Nothing an application can observe changes. It is a structural change taken for the sake of the two ADRs after it, and it costs a breaking change to do it — a legitimate thing to argue about, and the reason *Alternatives* below states the do-nothing option first.
- **`IAmALifetime` now holds something that is not a handler.** The name was already close enough to `IAmAScope` to need NFR-8; putting an `IAmAScope` *on* it narrows the gap further. Mitigated only by documentation, which is a weaker mitigation than a better name would have been.
- **The handler pipeline releases its scope synchronously, and the transform pipeline does not.** `IAmALifetime` is `IDisposable` (`IAmALifetime.cs:34`), both builder interfaces are `IDisposable` (`IAmAPipelineBuilder.cs:36`, `IAmAnAsyncPipelineBuilder.cs:37`), and both **async** dispatch paths take the builder with `using`, not `await using` (`CommandProcessor.cs:394`, `:575`). So an async handler pipeline's `IAmAScope` can only be released through `Dispose()`, while ADR 0070's transform pipeline releases through `DrainAsync` and a genuinely asynchronous scope disposal. This is deliberate and it is not free.

  It is **safe** rather than merely tolerated: the synchronous drain in `ServiceProviderLifetimeScope.DisposeScope` suppresses the current `SynchronizationContext` for the duration (`:422-436`, marked a load-bearing invariant in the source and documented as existing precisely to stop an `IAsyncDisposable` artefact deadlocking the Proactor pump's single-threaded context). What it costs is occupancy: the disposal runs to completion on the calling thread, which on the Proactor is the pump thread. The alternative was to put `IAsyncDisposable` on `IAmALifetime` — a second break on a public interface with seven implementations — plus both internal builder interfaces and two `CommandProcessor` call sites, to remove a block the container already makes safe. That trade was declined here and can be revisited without disturbing anything this ADR decides, because the handle type already carries both members.

- **The no-handle path survives in `ServiceProviderHandlerFactory`.** `_lifetimeScopes`, `GetOrCreateLifetimeScope` and `ReleaseLifetimeScope` remain for callers that supply no handle, so the factory carries two resolution paths and two disposal paths where the goal was one. Brighter's own code never takes the second, which is exactly what makes it easy to leave rotting.
- **The FR-7 regression guards move to the path that no longer matters.** `FactoryLifetimeTests`' two tests — designated in the requirements as the regression guards for **AC-9** and required to keep passing unchanged — keep passing precisely because they use the fallback. That designation now has to attach to the duplicated handle-path pair as well, which is an amendment to how AC-9 is discharged, not merely extra test coverage.
- **`HandlerLifetimeScope.Dispose()` gains error-composition logic, and that is a repair, not an addition.** Today `Dispose()` (`HandlerLifetimeScope.cs:74-93`) is two bare `.Each(…)` loops calling `Release(trackedItem, this)` with no `try`/`catch` anywhere, followed by two `Clear()` calls. A user factory's `Release` can throw — `SimpleHandlerFactorySync.Release` calls `disposable?.Dispose()` — and a throw from the first tracked handler aborts the loop, skips every remaining `Release`, and skips both `Clear()` calls, leaving a disposed scope still holding references to handlers that were never released. The transform family already has this fixed and regression-guarded (`tests/Paramore.Brighter.Core.Tests/MessageSerialisation/When_a_transform_release_throws_the_scope_still_releases_the_rest.cs`). So step 2 is not merely adding composition between the handler releases and the new scope disposal; it is bringing handler-to-handler fault tolerance up to the standard the transform side already meets. The work is larger than "one new step", and the existing defect is real today.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| `Transient` handler behaviour drifts — the per-resolution isolation of ADR 0067, or `IsolateTransientHandlerScope` | `CreatePipelineScope()` constructs the `ServiceProviderLifetimeScope` with the same three arguments `GetOrCreateLifetimeScope` passes today, including the flag. A handler pipeline takes a handle for `Transient` as well as `Scoped`, unlike a transform pipeline. C-6 |
| Release timing changes, tightening or loosening D10 | The handle is disposed by `HandlerLifetimeScope.Dispose()`, which `PipelineBuilder.Dispose()` (`:269-270`) already drives at exactly the point the DI scope is disposed today. `PipelineBuilder` needs no new list and no new disposal call |
| `Publish` subscriber isolation regresses (ADR 0039) | One `HandlerLifetimeScope`, and therefore one handle, per subscriber, created in the same loop as today (`:190`, `:235`). Nothing is shared between subscribers |
| A decorator resolves from a different scope than its handler | Decorators resolve through the same `IAmALifetime` instance they do today — the four threading sites are listed in *Technology Choices* — and the handle is a property of it. Same object, same scope, by construction |
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

- Requirements: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md) — FR-7, NFR-1, NFR-3, NFR-4, NFR-5, NFR-6, NFR-7, NFR-8, C-1, C-2, C-6, D0, D2, D10; AC-9
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
