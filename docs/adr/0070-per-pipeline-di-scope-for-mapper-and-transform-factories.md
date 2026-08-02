---
id: 0070-per-pipeline-di-scope-for-mapper-and-transform-factories
title: "Per-pipeline DI scope shared by the mapper and transform factories"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-08-02
summary: "A transform pipeline takes exactly one DI scope. The scope handle (IAmAScope, both IDisposable and IAsyncDisposable) is created by whichever participating factory can offer one, passed as an argument to every Create the pipeline needs, held by the pipeline and released when the pipeline is released. The mapper and transformer factory interfaces, and the two mapper registry interfaces, gain the scope in their signatures — a breaking change on netstandard2.0, taken deliberately. Closes the defects where a Scoped mapper lived for the process and where the mapper and its transforms did not share a container-Scoped dependency."
tags:
  - "lifetime"
  - "di"
  - "pipeline"
  - "message-mapping"
---

# 70. Per-pipeline DI scope shared by the mapper and transform factories

Date: 2026-08-02

## Status

Proposed

## Context

A transform pipeline is a message mapper plus the `[WrapWith]`/`[UnwrapWith]` transforms that decorate it. Both are resolved from the application's container, through Brighter's factory interfaces, once per message. On current master they are resolved from **two different DI scopes**, and under a configured lifetime of `Scoped` neither of those DI scopes is released until the host shuts down. So a mapper meant to serve one message serves the process, and a dependency injected into both a mapper and its transform is two objects where the application asked for one.

**Parent Requirement**: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md)

**Scope**: This ADR decides one thing — **a transform pipeline takes one DI scope, and the mapper factory and the transformer factory both resolve from it**. It discharges FR-1 … FR-7 and constraint C-19, and closes Defect 1 and Defect 1b.

It introduces `IAmAScope` as the core scope handle, settling the disposal half of C-8 and confirming the seam types' home. It does **not** decide `IAmAScopeProvider`, the *ambient* concept, `ScopeAffinity`, adoption or borrowing, ASP.NET (`IHttpContextAccessor`), the opt-in option on `IBrighterOptions`, `Publish`-subscriber ambient suppression, or the `ValidatePipelines()` rules of FR-22. Those are deferred to ADRs 0072 and 0073. Nor does it converge handler pipelines onto this mechanism — that is ADR 0071. This ADR is written so as not to foreclose any of them, and *Forward compatibility* below says how each lands on top of what is decided here.

### Where this ADR sits

Five ADRs deliver the parent requirement, one decision each. They are meant to be read in order; this is the first.

| ADR | Decides |
| --- | --- |
| **0070** *(this one)* | a transform pipeline takes one DI scope, carried **as a parameter** |
| 0071 | handler pipelines converge onto the **same handle**, carried on the object they already pass |
| 0072 | how a pipeline discovers an **ambient** DI scope the host owns, and where `Publish` suppression hangs |
| 0073 | the **opt-in** property, the ASP.NET package, and how that setting reaches all four registration paths |
| 0074 | **where** the lifetime and captive-dependency rules are evaluated |

One rule unifies the first two, and it is the sentence to carry into the rest: **the per-pipeline object carries the DI scope.** For a transform pipeline that object is the `TransformPipeline<TRequest>`, and the scope arrives as a parameter, because `Create(Type)` has no per-pipeline object to hang it on. For a handler pipeline (ADR 0071) it is the `IAmALifetime`, which every resolution site already receives, so the scope rides on it.

Handler pipelines are **not** touched here, and the two handler factory interfaces are **not** changed: nothing in `IAmAHandlerFactorySync`, `IAmAHandlerFactoryAsync`, `ServiceProviderHandlerFactory`, `PipelineBuilder<TRequest>`, `HandlerLifetimeScope` or `IAmALifetime` changes. They already have a per-pipeline object — `IAmALifetime` — and a working per-pipeline DI scope keyed on it, which is the model this ADR copies. **ADR 0071 then converges them onto the mechanism decided here**, so that one story serves both families; FR-7's requirement that handler *behaviour* be preserved holds across both.

ADR 0067's `Terms` block defines the two axes this ADR uses — Brighter's *configured lifetime* (`Transient`/`Scoped`/`Singleton`, which governs the artefact) and the container's *registration lifetime* (which governs the dependencies) — and keeps `IServiceScope`, `ServiceProviderLifetimeScope` and `IAmALifetime` distinct. This ADR does not restate it.

### The two factory families release completely differently

Every factory call is a `Create` matched by a `Release`. They divide into two families that reclaim on entirely different terms.

| Family | `Create` / `Release` | Its DI scope is keyed per | `Scoped` is reclaimed by |
| --- | --- | --- | --- |
| `IAmAHandlerFactorySync` (`:44`, `:51`), `IAmAHandlerFactoryAsync` (`:44`, `:51`) | `Create(Type, IAmALifetime)` / `Release(handler, IAmALifetime)` | **pipeline** — one `ServiceProviderLifetimeScope` per `IAmALifetime` (`ServiceProviderHandlerFactory.cs:127-131`) | `Release` — it disposes that pipeline's DI scope (`:102-107`, `:133-137`) |
| `IAmAMessageMapperFactory` (`:45`, `:60`), `IAmAMessageMapperFactoryAsync` (`:46`, `:62`), `IAmAMessageTransformerFactory` (`:44`, `:50`), `IAmAMessageTransformerFactoryAsync` (`:45`, `:54`) | `Create(Type) → Lease<T>?` / `Release(Lease<T>?)` | **factory** — one built in the constructor (`ServiceProviderMapperFactory.cs:46`, `ServiceProviderTransformerFactory.cs:46`) | **nothing** |

Two defects follow from the second row's *"nothing"*, and they are the whole of this ADR's problem:

- **Defect 1 — a `Scoped` mapper or transform silently lives for the process.** The factories are constructed once for the singleton `Dispatcher` and once for the singleton `OutboxProducerMediator`, so `GetOrCreateScoped` (`:163-178`) caches every artefact by type for the host's life. Message N+1 sees message N's state, and an `IDisposable` mapper is never disposed.
- **Defect 1b — the mapper and transformer factories do not share a DI scope.** `ServiceProviderMapperFactory` and `ServiceProviderTransformerFactory[Async]` each build their own `ServiceProviderLifetimeScope`, hence their own `IServiceScope`. A container-`Scoped` dependency injected into a mapper *and* into its `[UnwrapWith]` transform is therefore two instances. Fixing Defect 1 factory-by-factory would leave this untouched — which is what C-19 records, and why FR-3 is the requirement that shapes the solution.

`ClaimCheckTransformer` (`src/Paramore.Brighter/Transforms/Transformers/ClaimCheckTransformer.cs:62`) is the concrete case: it takes `IAmAStorageProvider` and `IAmAStorageProviderAsync`, and is the one Brighter-shipped transform with constructor dependencies. A user's mapper and its claim-check transform sharing a `Scoped` unit of work is exactly FR-3's example.

#### Why one family reclaims and the other does not

The handler family already does the right thing: **a per-pipeline object travels on every call**, so `ServiceProviderHandlerFactory` can key a DI scope on it and dispose that DI scope on `Release`. This is the model this ADR copies, and copies literally.

The mapper/transform family cannot, as its interfaces stand. `Create(Type)` carries nothing that identifies a pipeline (ADR 0066 deliberately made the return an opaque `Lease<T>`), so those factories key one DI scope for their whole life and release *per resolution*. `ServiceProviderLifetimeScope.GetOrCreate<T>(Type, out object? releaseToken)` (`:126`) issues a release token in exactly one case — isolated `Transient` (`:139-140`, `GetTransient` `:259-261`). For `Scoped` the token is `null` (`:136`, documented at `:118-123`), so `Release(Lease)` is a no-op and the artefact is reclaimed only when the factory itself is disposed at shutdown (`ServiceProviderMapperFactory.cs:78`; its own remarks say so at `:61-65`).

### The forces

- **Core must stay container-agnostic.** ADR 0014 is the principle: Brighter offers per-family factory interfaces rather than abstracting an IoC container, and the *application* supplies the implementation. So no type in `Paramore.Brighter` may name `IServiceProvider`, `IServiceCollection`, `ServiceLifetime` or `ServiceDescriptor`, and core may take no direct dependency on `Microsoft.Extensions.DependencyInjection`. That rule needs enforcing at the level of core's *source*, not its project file: `Microsoft.Extensions.DependencyInjection` is already on core's compile closure transitively through `Microsoft.Extensions.Logging`, so those types compile in core today. Whatever this ADR adds to a core signature must therefore be a core, container-free type — and it must stay implementable over Autofac or SimpleInjector as readily as over Microsoft's container (NFR-7).
- **The interfaces are alterable, and the cost is understood.** `netstandard2.0` has no default interface members, so any member added to an interface breaks every implementation at compile time. Within this repository that is 12 classes in `src/` and 67 test doubles. Outside it, these interfaces have no known public implementations: `IAmAMessageMapperRegistry`'s own documentation says "the default implementation `MessageMapperRegistry` is suitable for most purposes and the interface is provided for testing" (`IAmAMessageMapperRegistry.cs:34`). The change is therefore a **deliberate breaking change**, weighed against a design that would otherwise have to reach the factories by ambient state.
- **The registry sits between the builder and the mapper factory.** `TransformPipelineBuilder` holds an `IAmAMessageMapperRegistry` (`:51`) and resolves through `_mapperRegistry.Get<TRequest>()` (`:332`, inside `FindMessageMapper<TRequest>` `:330`); the async builder holds an `IAmAMessageMapperRegistryAsync` (`:50`) and calls `_mapperRegistryAsync.GetAsync<TRequest>()` (`:255`). Neither builder calls a mapper factory directly. Anything the mapper factory needs must therefore travel through the registry interface as well.
- **The two factories are constructed at different sites.** In `Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`: `new ServiceProviderMapperFactory(provider)` and `new ServiceProviderMapperFactoryAsync(provider)` at `:807`/`:808`, inside the `MessageMapperRegistry`; `new ServiceProviderTransformerFactory(provider)` at `:945` and `new ServiceProviderTransformerFactoryAsync(provider)` at `:957`, from separate public helpers. Each takes only an `IServiceProvider`. Nothing today connects them.
- **NFR-4 — thread safety.** The factories are singletons shared across concurrent pipelines: several `Post` calls on one `OutboxProducerMediator`, several performers consuming concurrently.
- **NFR-5 / NFR-6 — bounded, cheap.** Zero Brighter-created DI scopes live after the Nth message, and **at most one DI scope begin/release per pipeline**, never one per resolution.
- **NFR-8 — `IAmAScope` must be documented as distinct from `IAmALifetime`.** `IAmALifetime` (`src/Paramore.Brighter/IAmALifetime.cs`) is `IDisposable` with `Add(IHandleRequests)`/`Add(IHandleRequestsAsync)`: it *tracks handler instances* for a handler pipeline and is implemented by `HandlerLifetimeScope`. It is not a DI scope and is not being replaced.
- **C-1 — Microsoft's DI scopes do not nest.** A child scope created from a scoped provider is root-parented. This is why the unit has to be the pipeline (D0) and why there is no "scope within a scope" available.
- **C-3 stands.** On the consumer a transform pipeline's DI scope ends before the handler pipeline's begins (`Proactor.cs:239` then `:241`). A `Scoped` dependency used by an unwrap transform and by the handler is two instances. That is intended and is not fixed here.
- **D3 — a clean break.** `MapperLifetime.Scoped` stops caching across messages, with no compatibility flag (OOS-8). This is a deliberate behavioural change requiring a release note.
- **D12 — participation is structural.** For a transform pipeline the mapper factory *and* the transformer factory both participate, **whether or not the mapper declares any transform**.

## Decision

**A transform pipeline takes one DI scope, created by whichever participating factory can offer one, passed as an argument to every `Create` that serves the pipeline, and released when the pipeline is released.**

The scope travels the way the handler family's scope already travels: **as a parameter**. The four mapper and transformer factory interfaces, and the two mapper registry interfaces, take the scope on the call that creates an artefact, and each gains a member that offers to create one.

### The mechanism, end to end

Three things happen, in this order, once per pipeline. **Acquire**: the builder asks the participating factories for a scope and takes the first one offered — a `null` from all of them means no pipeline scope, and behaviour is exactly as today. **Share**: that one handle is passed to every `Create` the pipeline needs, so the mapper and every transform resolve from the same `IServiceScope`. **Release**: the pipeline owns the handle from the moment it is constructed, and its existing release-once drain ends it — after the leases have gone back to their factories, never before.

```mermaid
sequenceDiagram
    autonumber
    participant Builder as TransformPipelineBuilder
    participant Registry as IAmAMessageMapperRegistry
    participant Transforms as IAmAMessageTransformerFactory
    participant Pipeline as TransformPipeline

    Note over Builder,Transforms: ACQUIRE — ask each participant, and take the first non-null
    Builder->>Registry: CreatePipelineScope()
    Registry-->>Builder: IAmAScope, or null
    Builder->>Transforms: CreatePipelineScope(), only if the registry offered nothing
    Transforms-->>Builder: IAmAScope, or null

    Note over Builder,Transforms: SHARE — the same handle on every Create
    Builder->>Registry: Get for TRequest, passing the scope
    Registry-->>Builder: mapper lease
    Builder->>Transforms: Create each transform, passing the same scope
    Transforms-->>Builder: transform leases

    Builder->>Pipeline: construct, handing the scope over
    Note over Pipeline: from here the pipeline owns the scope

    Note over Registry,Pipeline: RELEASE — once, and in this order
    Pipeline->>Transforms: release the transform leases
    Pipeline->>Registry: release the mapper lease
    Pipeline->>Pipeline: dispose the IAmAScope last
```

Two invariants are worth reading off the diagram, because everything else follows from them. The transformer factory is asked and passed the scope **whether or not the mapper declares a transform** (D12), so `TransformerLifetime` behaves the same either way. And the scope is disposed **last**, so a factory whose `Release` still has work to do is never left resolving against a dead scope.

### Where the pieces live

```mermaid
flowchart TB
    subgraph core["Paramore.Brighter — core, names no container type"]
        direction TB
        handle["IAmAScope<br/>an opaque handle: IDisposable and IAsyncDisposable, nothing more"]
        builder["TransformPipelineBuilder and its async twin<br/>acquires the scope, threads it, hands it to the pipeline"]
        pipe["TransformPipeline and its async twin<br/>holds the scope, releases it in the drain"]
        builder --> pipe
    end

    subgraph di["Paramore.Brighter.Extensions.DependencyInjection"]
        direction TB
        scope["ServiceProviderPipelineScope<br/>owns one ServiceProviderLifetimeScope,<br/>which owns the IServiceScope"]
        facs["ServiceProviderMapperFactory and its async twin<br/>ServiceProviderTransformerFactory and its async twin<br/>all four resolve through the SAME handle they are given"]
        facs --> scope
    end

    builder -- "CreatePipelineScope(), then Create with the scope" --> facs
    scope -. "implements" .-> handle
```

One `IServiceScope` per transform pipeline, reached by every participating factory, disposed exactly once when the pipeline is released. That is FR-1, FR-2 and FR-3 in one mechanism, and it is the same shape `ServiceProviderHandlerFactory` already uses for handlers — with `IAmAScope` playing, for transform pipelines, the per-pipeline-object part that `IAmALifetime` already plays for handler pipelines.

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Stereotype | Responsibility |
| --- | --- | --- | --- |
| Pipeline scope handle | `IAmAScope` (core) | **knowing** (information holder) | *Is* the DI scope a pipeline resolves from. Says nothing about where it came from, who owns it, or how to resolve anything |
| Scope offerer | the four factory interfaces and the two registry interfaces (core) | **deciding** | Answers, for one pipeline, whether it has a DI scope to offer. `null` means it has none, and behaviour is exactly today's |
| Scope acquirer | `TransformPipelineBuilder[Async]` (core) | **doing** (structurer) | Asks the participants in a fixed order, threads the first non-null handle through every `Create`, and releases it itself if the build fails |
| Scope owner | `TransformPipeline[Async]` (core) | **doing** | Holds the handle for the pipeline's life and ends it exactly once, after the leases have gone back to their factories |
| Scope implementation | `ServiceProviderPipelineScope` (DI package) | **knowing** | Owns one `ServiceProviderLifetimeScope`, and so one `IServiceScope`, for this pipeline |

The split between the last two is what makes the design work: the object that *acquires* the scope is not the object that *owns* it, because the build can fail after the scope exists and before a pipeline does — which is FR-5, and why the builder's failed-build path releases it directly.

#### `IAmAScope` — the pipeline scope handle (new, core, public)

```csharp
namespace Paramore.Brighter
{
    /// <summary>
    /// A handle to the DI scope one pipeline resolves from. Brighter core neither creates nor inspects
    /// the scope behind it; it holds the handle for the life of the pipeline and releases it when the
    /// pipeline is released. Distinct from <see cref="IAmALifetime"/>, which tracks handler instances
    /// for a handler pipeline and is not a DI scope.
    /// </summary>
    public interface IAmAScope : IDisposable, IAsyncDisposable
    {
    }
}
```

- **Home** — the `Paramore.Brighter` namespace and assembly, alongside `IAmALifetime`. This confirms C-8's assumption. It names no container type.
- **Both `IDisposable` and `IAsyncDisposable`** (C-8, settled). The precedent is exact: `src/Paramore.Brighter/IAmATransformLifetimeAsync.cs` is `internal interface IAmATransformLifetimeAsync : IDisposable, IAsyncDisposable`. This costs no new dependency — `src/Paramore.Brighter/Paramore.Brighter.csproj:24` already carries a `netstandard2.0`-conditional `PackageReference Include="Microsoft.Bcl.AsyncInterfaces"`, whose comment states it is there precisely because `ReleaseAsync`/`IAsyncDisposable` are on the public async surface. Both members are needed because the sync pipeline releases through `Dispose()` and the async pipeline through `DisposeAsync()`, and the async path must not block the Proactor's single-threaded synchronization context.
- **Error conditions** — `Dispose()` and `DisposeAsync()` are **idempotent**; a second call of either, in either order, is a no-op and must not throw (AC-8). A disposal that fails throws to its caller; the existing release call sites (`OutboxProducerMediator.ReleasePipeline` `:1269-1279` and `ReleasePipelineAsync` `:1281-1291`) already log at `Error` and swallow, so a successful `Post` is not failed by a teardown fault.
- **No members beyond disposal.** It is a handle: core's only responsibility toward it is *holding* it and *ending* it. Adding a "which scope is this?" accessor would put container knowledge in core, and keeping it empty is what lets ADR 0072 implement it over a borrowed request scope whose disposal is a no-op.

#### The changed signatures

Six interfaces change. Each gains `CreatePipelineScope()`; the four that create an artefact also take the scope on the call that creates it. **No `Release` signature changes** — the pipeline owns the scope and disposes it, so release needs nothing new.

```csharp
public interface IAmAMessageMapperFactory
{
    /// <summary>Creates a DI scope for one pipeline to resolve from, or null when this factory has
    /// none to offer — it is not container-backed, or its configured lifetime is not Scoped. The
    /// caller owns the returned scope and must release it.</summary>
    IAmAScope? CreatePipelineScope();

    Lease<IAmAMessageMapper>? Create(Type messageMapperType, IAmAScope? scope = null);
    void Release(Lease<IAmAMessageMapper>? lease);          // unchanged
}

public interface IAmAMessageMapperRegistry
{
    IAmAScope? CreatePipelineScope();
    Lease<IAmAMessageMapper<T>>? Get<T>(IAmAScope? scope = null) where T : class, IRequest;
    // ResolveMapperInfo, Release, Register: unchanged
}
```

`IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync` and `IAmAMessageMapperRegistryAsync` change the same way — `Create(Type, IAmAScope?)`, `GetAsync<T>(IAmAScope?)`, and `CreatePipelineScope()` on each.

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `CreatePipelineScope()` | none | a new, owned `IAmAScope`, or `null` when this participant has none to offer | may throw if the container cannot create a scope; the builder's existing `catch` turns that into `ConfigurationException` on the failed-build path |
| `Create(Type, IAmAScope?)` | a scope from *any* participant, or `null` | as before | a scope this implementation does not recognise is **ignored**, not rejected: the implementation falls back to exactly its current behaviour. It must not throw |
| `Get<T>(IAmAScope?)` | as above | as before | forwards the scope to the factory it owns; otherwise unchanged |

**Why a defaulted parameter.** `IAmAScope? scope = null` keeps every existing *call site* compiling — `factory.Create(type)` and `registry.Get<T>()` still bind. It does nothing for *implementers*, who must still declare the parameter; the break is theirs alone. The default is `null` and must stay `null`: a default parameter value is compiled into the call site, so changing it later would not reach already-built callers.

**Why `CreatePipelineScope()` is on the interface rather than discovered by a type test.** A factory that does not answer the question cannot compile, so there is no such thing as a container-backed factory that silently keeps Defect 1. That is the whole reason to spend a breaking change here rather than probe for a capability at runtime.

**Why `Release` is untouched.** The handler family disposes its per-pipeline DI scope inside `Release(handler, IAmALifetime)`. Here the *pipeline* owns the `IAmAScope` and disposes it in its drain, so the two paths would fight for ownership if `Release` also took it. Leases still return to their factories exactly as today: for `Scoped` the release token is `null` and the call is already a no-op (`ServiceProviderLifetimeScope.cs:136`), and reclamation happens when the pipeline scope is disposed.

**No sync/async twins beyond the ones that already exist.** No member added here carries a request-shaped payload, and the only sync/async asymmetry — disposal — lives on `IAmAScope`, which carries both. The interfaces are twinned already for the reasons ADR 0005 gives; this change simply follows the existing twinning.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| `Paramore.Brighter` | `IAmAScope` | **new** |
| `Paramore.Brighter` | `IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync` | `CreatePipelineScope()`; scope parameter on `Create` |
| `Paramore.Brighter` | `IAmAMessageMapperRegistry`, `IAmAMessageMapperRegistryAsync` | `CreatePipelineScope()`; scope parameter on `Get<T>`/`GetAsync<T>` |
| `Paramore.Brighter` | `MessageMapperRegistry` (`:41`) | implements both new members by forwarding to the factories it owns |
| `Paramore.Brighter` | `TransformPipelineBuilder` | acquires and threads the scope in `BuildWrapPipeline<TRequest>()` (`:93`) and `BuildUnwrapPipeline<TRequest>()` (`:134`); `FindMessageMapper<TRequest>` (`:330`) and `BuildTransformPipeline<TRequest>` (`:173`) carry it; `CleanUpAfterFailedBuild<TRequest>` (`:231`) releases an owned scope |
| `Paramore.Brighter` | `TransformPipelineBuilderAsync` | the same, on `:93`, `:134`, `:255`, `:231`; note its transformer field is `_messageTransformerFactoryAsync` (`:52`) |
| `Paramore.Brighter` | `TransformerFactory<TRequest>` (`:32`), `TransformerFactoryAsync<TRequest>` (`:30`) | `internal`; take the scope and pass it to `factory.Create` (`TransformerFactory.cs:42`, `TransformerFactoryAsync.cs:40`) |
| `Paramore.Brighter` | `TransformPipeline<TRequest>`, `TransformPipelineAsync<TRequest>` | hold the pipeline scope; release it in the drain |
| `Paramore.Brighter` | `WrapPipeline<TRequest>`, `UnwrapPipeline<TRequest>`, `WrapPipelineAsync<TRequest>`, `UnwrapPipelineAsync<TRequest>` | optional trailing constructor parameter, forwarded to the base |
| `Paramore.Brighter` | `TransformPipelineDrain` (`internal static`, `:38`) | a third drain step after today's `disposeScope`/`releaseMapper` (`Drain` `:46`, `DrainAsync` `:85`), same hold-and-compose error handling |
| `Paramore.Brighter` | `SimpleMessageMapperFactory[Async]`, `SimpleMessageTransformerFactory[Async]`, `EmptyMessageTransformerFactory[Async]` | `CreatePipelineScope()` returns `null`; `Create` ignores the scope |
| `Paramore.Brighter.ServiceActivator` | `ControlBusMessageMapperFactory` (`:31`) | the same two no-op changes. It gains no container dependency — `IAmAScope` is a core type |
| `…DependencyInjection` | `ServiceProviderPipelineScope` | **new** |
| `…DependencyInjection` | `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`, `ServiceProviderTransformerFactoryAsync` | implement both new members |

Unchanged, and named so the omission is not read as an oversight: `IAmAHandlerFactorySync` and `IAmAHandlerFactoryAsync`; `ServiceProviderHandlerFactory`; `IAmALifetime` and `HandlerLifetimeScope`; `PipelineBuilder<TRequest>` and `Pipelines<TRequest>`; `CommandProcessor`; `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder`, `ConsumerFactory` (C-2); `BrighterOptions`; `IAmATransformLifetime[Async]` and `TransformLifetimeScope[Async]`; and `ResolveMapperInfo`, which resolves a mapper *type* without creating an instance (`TransformPipelineBuilder.cs:171`) and so needs no scope.

### Technology Choices

**Where the per-pipeline DI scope and the per-pipeline artefact cache live.** `ServiceProviderPipelineScope` owns exactly one `ServiceProviderLifetimeScope`, configured `Scoped`. That class already owns, publishes once (`EnsureRootScopePublished`) and drains the `IServiceScope` its configured lifetime implies, so the handle needs no second thing to hold. The four container-backed factories resolve through it, so:

- the `IServiceScope` supplies **dependency** identity and disposal — one container-`Scoped` `DbContext` for the mapper and every transform in the pipeline, which is FR-3;
- the `ServiceProviderLifetimeScope`'s per-type `_scopedInstances` cache (`:163-178`) supplies **artefact** identity, now per pipeline instead of per factory, which is C-17 preserved.

Sharing one artefact cache between the mapper and the transforms is harmless — a mapper and a transform are different types.

**What this cache does and does not give.** Because the handle is constructed once per pipeline, a cache held by the handle gives artefact identity **per pipeline**. That is exactly what this ADR requires, and all of it: the pipeline is the unit (D0), and FR-1 and FR-2 ask for one artefact per type per pipeline. It is **not** enough for adoption. Under `JoinAmbient` the artefact must follow the *borrowed* DI scope, which may span several pipelines in one request — two `Post`s sharing one mapper (FR-16, D7) — and a handle constructed per pipeline cannot express that however it holds its cache. Adoption therefore needs the cache to belong to the DI scope rather than to the handle, and **ADR 0072 supplies that**, as a container-`Scoped` service. Nothing in this ADR changes when it does: the owned case still resolves one cache per pipeline, because a Brighter-created scope is per pipeline.

**No ambient state anywhere.** The scope is an argument. There is no `AsyncLocal`, no static, no package-level table, and nothing per-flow: two concurrent builds on two threads pass two different handles down two different call stacks. That is what satisfies NFR-4, and it is the main thing the parameter buys over the alternatives below.

**Artefact identity stays Brighter's** (C-17). Every mapper and transform type is still registered `ServiceLifetime.Transient`; nothing about how the container is populated changes.

**Why not put the pipeline scope on `IAmALifetime`.** `IAmALifetime` is the handler pipeline's instance tracker, with `Add(IHandleRequests)` on it, and it does not exist on the transform path at all. Reusing it would conflate two units of work and would break NFR-8 the moment it was documented.

### Implementation Approach

**1. Core type.** Add `IAmAScope` to `src/Paramore.Brighter/`. XML documentation states, per NFR-8, what it is and how it differs from `IAmALifetime`, and `IAmALifetime`'s own documentation gains the reciprocal sentence.

**2. The interfaces.** Add `CreatePipelineScope()` and the scope parameter as above, then move every implementation in the repository in the same change: 12 classes in `src/` (four container-backed factories, six core factories, `ControlBusMessageMapperFactory`, `MessageMapperRegistry`) and 67 test doubles — 61 factory doubles across 34 test files, plus six registry doubles in three more. Every non-container implementation gets the same two-line treatment: return `null`, ignore the parameter.

**3. The builders.** In `TransformPipelineBuilder` and `TransformPipelineBuilderAsync`, both `BuildWrapPipeline<TRequest>()` and `BuildUnwrapPipeline<TRequest>()` — four methods, wrap and unwrap symmetric — acquire the scope first and thread it:

```csharp
var scope = CreatePipelineScope();
try
{
    messageMapperLease = FindMessageMapper<TRequest>(scope);
    transformLeases = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapperLease.Instance), scope);
    pipeline = new WrapPipeline<TRequest>(
        messageMapperLease, _messageTransformerFactory, transformLeases,
        _instrumentationOptions, _mapperRegistry, scope);
    ...
    return pipeline;
}
catch (Exception e)
{
    try { CleanUpAfterFailedBuild(pipeline, transformLeases, messageMapperLease, scope); }
    catch (Exception cleanupException) { Log.FailedToCleanUpAfterFailedBuild(s_logger, cleanupException); }
    throw new ConfigurationException("Error building wrap pipeline for outgoing message, see inner exception for details", e);
}
```

The private `CreatePipelineScope()` helper asks the mapper registry first — `_mapperRegistry` in the sync builder (`:51`), `_mapperRegistryAsync` in the async one (`:50`) — then the transformer factory, and returns the **first non-null** handle, or `null`. Order is fixed and documented: the mapper is the mandatory half of a transform pipeline. The transformer factory is allowed to be null (the v9 compatibility path, `TransformPipelineBuilder.cs:180`), so the second ask is null-conditional.

`BuildTransformPipeline<TRequest>` passes the scope into `new TransformerFactory<TRequest>(attribute, _messageTransformerFactory)` (`:193`) and thence to `factory.Create(transformerType, scope)`. This is D12: the transformer factory is handed the scope even when the mapper declares no transform, so `TransformerLifetime = Scoped` behaves identically whether or not a `[WrapWith]` is present.

**4. Failed build — FR-5.** `CleanUpAfterFailedBuild<TRequest>` (`:231` on both builders; the async builder's throwing paths are `:122` and `:163`) gains the scope. When a pipeline object was constructed it already owns the scope and `pipeline.Dispose()` releases it; when it was not, the cleanup releases the scope directly, after releasing whatever leases were taken. Release failures are caught and logged by the existing guard (`TransformPipelineBuilder.cs:116-125` for wrap, `:157-166` for unwrap), so the `ConfigurationException` carrying the original build error is still what the caller sees (AC-5, AC-6).

**5. Pipeline release — FR-6.** `TransformPipeline<TRequest>` and `TransformPipelineAsync<TRequest>` store the scope alongside the existing `protected TransformLifetimeScope? InstanceScope` (`TransformPipeline.cs:16`), taken as an optional trailing constructor parameter — the shape `IAmAMessageMapperRegistry? mapperRegistry = null` already uses (`:24`) — and threaded through `WrapPipeline<TRequest>`, `UnwrapPipeline<TRequest>`, `WrapPipelineAsync<TRequest>` and `UnwrapPipelineAsync<TRequest>`. `TransformPipelineDrain.Drain`/`DrainAsync` — today `Drain(Action disposeScope, Action releaseMapper)` (`:46`) and `DrainAsync(Func<ValueTask>, Func<ValueTask>)` (`:85`) — gain a third step, ordered **after** the transform scope disposal and the mapper release: leases go back to their factories first, then the DI scope is released, so a factory that still needs its `Release` to run (a `Transient` transformer under a mixed configuration) is not resolving against a dead scope. The existing hold-and-compose error handling extends to the third step, so no failure masks another and both surface as an `AggregateException` — conforming to ADR 0068, whose rule is that an explicit `Dispose` surfaces failures and the finalizer only retries best-effort and swallows. The pipeline's existing release-once guard (`Interlocked.Exchange(ref _released, 1)`, `TransformPipeline.cs:65`) already makes the whole drain, and therefore the scope release, happen exactly once (FR-6); `IAmAScope`'s own idempotence is belt and braces for AC-8.

**6. The container package.**

- `ServiceProviderPipelineScope` wraps one `ServiceProviderLifetimeScope` and disposes it exactly once under either `Dispose()` or `DisposeAsync()`, claimed with a single atomic exchange, preferring the scope's `IAsyncDisposable` where offered — the same shape `ServiceProviderLifetimeScope.DisposeScope` (`:406`) and `DisposeScopeAsync` (`:449`) already use, including the `SynchronizationContext` suppression (`:384-388`) that keeps a blocking release off the Proactor pump.
- Each of the four container-backed factories returns a new `ServiceProviderPipelineScope` from `CreatePipelineScope()` **only when its configured lifetime is `Scoped`**, and `null` otherwise. `Create(Type, IAmAScope?)` resolves through the handle when it is a `ServiceProviderPipelineScope` and the lifetime is `Scoped`; otherwise it takes exactly today's path.
- `MessageMapperRegistry` forwards both members to the (up to two) factories it was built with: `CreatePipelineScope()` returns the first non-null answer from the sync then the async factory; `Get<T>`/`GetAsync<T>` pass the scope straight through. This is consistent with ADR 0069 — the registry owns those factories, so it is the right object to speak for them — and because `MessageMapperRegistry` implements both `IAmAMessageMapperRegistry` and `IAmAMessageMapperRegistryAsync` (`:41`), one implementation serves both builders.
- **No change to the four construction sites** (`ServiceCollectionExtensions.cs:807`, `:808`, `:945`, `:957`). The shared scope reaches both families through the argument the builder passes, not through construction — which is why factories built at different sites need no new wiring.

**7. Behaviour by configured lifetime.** All three are stated, and two of them do not change (C-6, OOS-7):

| Configured lifetime | `CreatePipelineScope()` | Scope argument | Resolution and reclamation | Changed? |
| --- | --- | --- | --- | --- |
| `Transient` | `null` | ignored | a fresh DI scope per resolution, released by `Release(Lease)` — ADR 0067 unchanged, and `IsolateTransientHandlerScope` (`BrighterOptions.cs:37`) untouched | **No** |
| `Scoped` | a new owned `IAmAScope` | resolved from | the pipeline's single `IServiceScope`; one artefact per type per pipeline; both artefact and its container-`Scoped` dependencies disposed when the pipeline is released | **Yes — this ADR** |
| `Singleton` | `null` | ignored | the root provider, one artefact per process | **No** |

All three of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` still default to `Transient` (`BrighterOptions.cs:20`, `:52`, `:69`), so an application that changes nothing sees no behavioural change from this ADR.

**8. Both sides, both builders.** FR-4 requires the producer side to behave as the consumer does. The producer's wrap pipeline is built and released per `Post`/`DepositPost` in `OutboxProducerMediator` — sync at `:1248` with `ReleasePipeline` at `:1258`, async at `:1312` with `ReleasePipelineAsync` at `:1321` — and the consumer's unwrap pipeline is built and released per message in `Reactor.TranslateMessage` (build `:531`) and `Proactor.TranslateMessage` (build `:538`), each releasing in its `finally`. `OutboxProducerMediator` also builds unwrap pipelines at `:569` and `:587`. Because the scope is created inside the builder and released by the pipeline's disposal, **every one of these six call sites is correct without being touched**, which is also what keeps C-2 intact.

**9. Out-of-bracket `Create`.** A third party calling a factory's `Create(type)` directly, with the defaulted `null` scope, gets today's behaviour rather than an exception. In Brighter's own paths this does not arise: the only mapper resolutions in `src/` are `_mapperRegistry.Get<TRequest>()` (`TransformPipelineBuilder.cs:332`) and `_mapperRegistryAsync.GetAsync<TRequest>()` (`TransformPipelineBuilderAsync.cs:255`), both inside `FindMessageMapper<TRequest>`, and the only callers of the transformer factories' `Create` are `TransformerFactory<TRequest>` (`:42`) and `TransformerFactoryAsync<TRequest>` (`:40`) from inside `BuildTransformPipeline`.

### Forward compatibility with ADRs 0071, 0072 and 0073

Nothing here decides handler convergence or adoption, and nothing here blocks either:

- **Handler convergence (0071)** applies this ADR's rule — the per-pipeline object carries the scope — to the handler family, where that object already exists: `IAmAHandlerFactory` gains `CreatePipelineScope()` and `IAmALifetime` gains a `PipelineScope` property, so `ServiceProviderHandlerFactory` stops keying a DI scope on `IAmALifetime` in a dictionary. No handler `Create` or `Release` signature changes, because the handler call already receives a per-pipeline object and a transform `Create` does not — which is the whole reason this ADR needs a parameter. That is why `ServiceProviderPipelineScope` is described above as owning a `ServiceProviderLifetimeScope` rather than an `IServiceScope`: the handler family needs the handle for `Transient` as well as `Scoped`, and `ServiceProviderLifetimeScope` is the type that already knows what each lifetime implies.

- **Adoption (0072)** changes only what `CreatePipelineScope()` returns. A borrowed request scope is a `ServiceProviderPipelineScope` built over `HttpContext.RequestServices` whose disposal does not dispose the borrowed scope (FR-12, C-7). Core is unchanged: it holds a handle and releases it, and "releasing" a borrowed handle does nothing. No member is added to `IAmAScope`, so making it minimal now costs 0072 nothing. **One thing adoption does need beyond this ADR is a home for the artefact cache.** FR-16's per-scope artefact association — two `Post`s in one request sharing one mapper (D7) — does not follow from this ADR's per-pipeline handle, because a borrowed handle is still constructed per pipeline over the one request scope; see *Technology Choices* above. ADR 0072 supplies it by making the cache a container-`Scoped` service rather than a field of the handle, which leaves the owned case here unchanged.
- **`IAmAScopeProvider` (0072)** is a *different* role from anything here, and the distinction is D11's: the provider answers "is there an ambient a pipeline may adopt?", while the container package always creates and owns Brighter's own pipeline scopes. `CreatePipelineScope()` is where the container package will consult it and where `ScopeAffinity` will be carried.
- **The non-core hand-off (FR-10)** — the contract by which an ambient owner exposes its resolution source to the container package — is **not** introduced here, because nothing here has an ambient to hand off. It is 0072's to design, and `ServiceProviderPipelineScope` is where it lands.
- **`Publish` and the opt-in (0073)** are untouched. `PipelineBuilder<TRequest>`'s eager per-subscriber resolution and its end-of-publish release stay exactly as they are (D10), under this ADR and under 0071; the ambient suppression FR-8 needs is a separate mechanism this ADR neither adds nor precludes — and because this ADR introduces no per-flow state of its own, suppression will be the only such mechanism in play.

This ADR **supersedes no prior ADR.** It extends the 0066–0069 sequence.

## Consequences

### Positive

- **Defect 1 is closed.** A `Scoped` mapper or transform is per transform pipeline. Message N's instance is disposed before message N+1's is constructed, on the consumer (FR-1, FR-2) and per `Post`/`DepositPost` on the producer (FR-4).
- **Defect 1b is closed.** One `IServiceScope` serves the mapper and every transform in a pipeline, so a container-`Scoped` dependency injected into both is one instance (FR-3, C-19) — and it is one instance whether or not the mapper declares a transform (D12).
- **Bounded resources.** Steady-state consumption leaves zero Brighter-created DI scopes live (NFR-5); the `_scopedInstances` cache that grew for the host's life now dies with the pipeline.
- **Cost is per pipeline.** Exactly one DI scope is begun and released per transform pipeline that has a `Scoped` participating factory, and none per resolution (NFR-6). A pipeline with no `Scoped` participant creates no DI scope at all and pays two null returns.
- **The two factory families converge.** Mappers and transforms now scope the way handlers already do, by the same means — a per-pipeline object on the call. There is one story to teach, and the asymmetry that made the mapper family the odd one out is gone.
- **No hidden state.** The scope is an argument on the stack. Nothing is per-flow, per-thread or static, so there is no `ExecutionContext` behaviour to reason about, nothing a debugger cannot show you next to the `Create` call, and nothing for a future change to accidentally move across an `await`.
- **A container-backed factory cannot silently opt out.** `CreatePipelineScope()` is a required member, so an implementation that ignores pipeline scoping does so visibly, in source, rather than by failing a runtime capability probe.
- **The seam is testable without a container.** A test double implementing the six interfaces in an assembly that does not reference `Microsoft.Extensions.DependencyInjection` can assert the whole protocol.
- **Core stays container-agnostic** (ADR 0014). `IAmAScope` names no container type, `Paramore.Brighter` gains no direct container package reference, and `Paramore.Brighter.ServiceActivator` keeps its single project reference.
- **`Transient` and `Singleton` are untouched** (C-6, OOS-7), including ADR 0067's per-resolution scopes and `IsolateTransientHandlerScope`.
- **Handler behaviour is untouched** (FR-7). `FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs:36`) and its async twin `AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`:154`) assert *within-pipeline* handler identity — two `Create` calls against the same `TestLifetimeScope` returning the same instance — and must keep passing unchanged as the regression guard.

### Negative

- **Six public interfaces break at compile time.** `netstandard2.0` has no default interface members, so every implementation of `IAmAMessageMapperFactory`, `IAmAMessageMapperFactoryAsync`, `IAmAMessageTransformerFactory`, `IAmAMessageTransformerFactoryAsync`, `IAmAMessageMapperRegistry` and `IAmAMessageMapperRegistryAsync` must be edited to compile. This is the price of the design and it is not recoverable later. **It needs a release note** naming each interface and the migration: implement `CreatePipelineScope()` as `return null;` and add an ignored `IAmAScope? scope = null` parameter, unless the implementation is container-backed and wants pipeline scoping. Call sites are unaffected, because the parameter is defaulted.
- **A large mechanical edit.** 12 classes in `src/` and 67 test doubles change in one commit. Mechanical, but it is a wide diff in which a genuine change is easy to lose, and it must land as one commit or the build is broken in between.
- **Core gains one public type**, `IAmAScope`, close enough in name to the existing `IAmALifetime` to need documentation to keep them apart (NFR-8). Public surface in core is permanent.
- **The scope parameter is on interfaces most implementations will ignore.** A hand-rolled `SimpleMessageMapperFactory`-style factory now declares a parameter it never reads and a method that always returns `null` — noise on the interface, paid by every implementer to serve the container-backed ones.
- **The defaulted parameter is a small versioning trap.** `IAmAScope? scope = null` compiles the default into each call site, so it can never be changed to a non-null default without recompiling callers. Documented on the members.
- **D3 is a behavioural break.** `MapperLifetime.Scoped` stops caching mappers across messages, with no compatibility flag (OOS-8). An application that relied on that — deliberately or not — migrates to `Singleton`. **This needs a release note.**
- **Three tests encode the old contract and must change**, all in `tests/Paramore.Brighter.Extensions.Tests/`:
  - `When_releasing_a_scoped_mapper_it_should_stay_usable_for_later_resolutions.cs`
  - `When_two_threads_first_resolve_a_scoped_mapper_concurrently_it_should_not_leak_a_scope.cs`
  - `When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it.cs`

  The first asserts precisely the cross-pipeline reuse that FR-1 removes. The second and third are about a factory-wide scope that a `Scoped` factory no longer keeps for the pipeline path; their invariants move onto the pipeline scope.
- **Six pipeline constructors and one internal drain helper change shape.** `WrapPipeline<TRequest>`, `UnwrapPipeline<TRequest>`, `WrapPipelineAsync<TRequest>`, `UnwrapPipelineAsync<TRequest>` and the two abstract bases take an optional trailing parameter. Source-compatible for callers who use the existing parameters, binary-breaking for anyone who constructed one without recompiling — which in practice is only Brighter's two builders.
- **`TransformLifetimeScope`/`TransformLifetimeScopeAsync` are now one of three things a pipeline drains** (transform leases, then the mapper lease, then the DI scope). They are neither extended nor subsumed — they track *leases*, this tracks a *DI scope*, and the ordering between them is load-bearing — but a reader has to hold three release steps in mind instead of two.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| A pipeline scope outlives its pipeline (leak) or is released early (use-after-dispose) because creation and release are in different places | The scope is created in the builder and released by the pipeline's existing release-once drain, which every call site already invokes in a `finally` — six sites, all unchanged. The failed-build path releases it explicitly (FR-5). AC-5's 1,000-failure case is the regression guard |
| A release failure masks a `ConfigurationException` | The existing guard in both builders' `catch` blocks catches and logs cleanup failures before rethrowing the build error (`TransformPipelineBuilder.cs:116-125`, `:157-166`); the third drain step composes with the existing hold-and-compose handling rather than replacing it (ADR 0068). AC-6 |
| Double release, or one pipeline's release affecting another's live scope | The pipeline's `Interlocked.Exchange` release-once guard, plus `IAmAScope`'s own idempotent disposal claimed with a single atomic exchange, plus a distinct handle per pipeline with no shared table. AC-8 |
| Concurrent pipelines interfering | There is no shared mutable state to interfere with: the handle is an argument on each build's own stack. NFR-4 |
| The mechanical edit across 79 implementations hides a real change, or misses one | The compiler finds every missed implementation — that is the point of putting `CreatePipelineScope()` on the interface. The edit lands as one commit; the four container-backed factories are the only ones whose bodies are not `return null;` / ignore-the-parameter, and a test asserts each of the four returns a scope under `Scoped` and `null` otherwise |
| A user's hand-rolled factory fails to compile after upgrade with no explanation | The release note names all six interfaces and gives the two-line migration; the compiler error points at the member |
| Terminology drift between `IAmAScope`, `IAmALifetime`, `HandlerLifetimeScope`, `ServiceProviderLifetimeScope` and `TransformLifetimeScope` | NFR-8: XML documentation on `IAmAScope` and on `IAmALifetime` states what each is for and how they relate; `docs/guides/lifetimes-and-scoping.md` (FR-25) carries the same distinction |
| A shared `MessageMapperRegistry` disposed by one owner while another builds a pipeline | Unchanged by this ADR — ADR 0069's ownership rules and `ServiceProviderLifetimeScope`'s targeted `ObjectDisposedException` message (`:320`) still govern |

## Alternatives Considered

**1. An additive capability role — `IAmAPipelineScopeParticipant`, discovered by a type test.** Add a new public core interface with "create a scope" and "resolve from this scope until I say stop", implement it on the container-backed factories alongside the unchanged factory interfaces, and have the builders probe for it with `as`. No interface breaks at all. **Rejected on three counts.** It cannot carry the scope on the call, because the call signature is what it is deliberately not changing — so the scope has to reach `Create` by per-flow state, an `AsyncLocal` on each factory, bracketed by the builder. That state is safe only for as long as the bracket never crosses an `await`, which is true today and is exactly the kind of invariant a later change breaks silently. It makes participation *optional at runtime*: a container-backed factory that omits the role keeps Defect 1 with no diagnostic, in a codebase where the whole point is that the defect is silent. And it adds a second public core type whose only purpose is to avoid editing six interfaces whose implementations are, in practice, Brighter's own.

**2. A container-package-private ambient.** Put an `AsyncLocal<ServiceProviderLifetimeScope>` inside `Paramore.Brighter.Extensions.DependencyInjection`, publish it wherever a pipeline begins, and have both factories read it. No core change at all, no new core type, and the scope is trivially shared between the two factories. **Rejected.** It is invisible coupling: nothing at the call site in `BuildWrapPipeline` says a scope is in play, so the mechanism cannot be read, cannot be unit-tested without a container, and cannot be implemented by a non-Microsoft container (NFR-7). It has no explicit end, so the failed-build release of FR-5 has nowhere natural to live. And it needs a publication point that only core knows — the start of a pipeline — which means core must call *something* anyway, at which point the honest version is the parameter.

**3. Construction-only: hand both factories the same collaborator at their construction sites.** Change `ServiceCollectionExtensions.cs:807`, `:808`, `:945` and `:957` to pass a shared object. Smallest possible surface, no interface change. **Rejected**: a collaborator shared for the *lifetime of the factories* is not a per-pipeline scope. To get one DI scope per pipeline out of it, the collaborator still needs a per-pipeline key on every `Create` — which is either the parameter this ADR adds, or the ambient of alternative 2 — while additionally coupling the four construction sites.

**4. Overloads rather than changed signatures.** Keep `Create(Type)` and `Get<T>()` and add `Create(Type, IAmAScope?)` and `Get<T>(IAmAScope?)` beside them. **Rejected**: on an interface an added overload is still a required member, so it breaks every implementation exactly as the parameter does, while doubling the surface and leaving each implementation free to answer the two overloads differently. The defaulted parameter achieves the only thing the overload would have bought — call-site source compatibility — without the ambiguity.

**5. Put the scope on `Release` as well, mirroring the handler family exactly.** `Release(Lease<T>?, IAmAScope?)`, with the factory disposing the pipeline's DI scope on release as `ServiceProviderHandlerFactory` does (`:133-137`). **Rejected**: a transform pipeline has *two* factories sharing one scope, so "the factory disposes it on release" has no single owner and would double-dispose or race. The pipeline is the one object that corresponds to the scope's life, so the pipeline owns it. The handler family gets away with the simpler rule only because a handler pipeline has exactly one factory.

**6. Release the scope when the build ends rather than when the pipeline is released.** A `using` around the build would be tidier and would need no state on the pipeline. **Rejected**: the artefacts resolved during the build, and their container-`Scoped` dependencies, must live until the pipeline has been *used*, not merely built. Releasing at end of build would push the release out to all six call sites — `OutboxProducerMediator` ×4 and the two pumps — and the pumps are exactly what C-2 forbids changing.

**7. `IAmAChainScope`, `IAmAPipelineScope`, `IAmAUnitOfWorkScope` as the handle name.** Considered and **rejected** by D4; the name is `IAmAScope`. `IAmAPipelineScope` was the closest, but the seam is used for handler pipelines too under 0071 and 0072, and "chain" is not a term of art in this codebase — the unit is a pipeline, and `PipelineBuilder<TRequest>.Build` returns `Pipelines<TRequest>`.

**8. `IAmAScope : IDisposable` only.** Halves the surface. **Rejected** by the settled decision on C-8, and for a concrete reason: the async pipeline releases through `DisposeAsync`, and on the Proactor's single-threaded synchronization context an `IAsyncDisposable`-only mapper released through a blocking synchronous path is a stall at best (see the guidance in `ServiceProviderLifetimeScope`, `:384-388`). `IAmATransformLifetimeAsync` already carries both interfaces for the same reason, and `Microsoft.Bcl.AsyncInterfaces` is already referenced on `netstandard2.0` (`Paramore.Brighter.csproj:24`), so this costs nothing.

## References

- Requirements: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md) — FR-1 … FR-7, FR-20, NFR-4, NFR-5, NFR-6, NFR-7, NFR-8, C-1, C-3, C-6, C-8, C-17, C-19, D0, D3, D4, D10, D12
- Related ADRs (cited by slug — ADR numbers are not unique in this repo, C-16):
  - `0066-release-factory-instances-on-an-opaque-lease` [Accepted] — why `Create` returns a `Lease<T>` carrying an opaque token, and therefore why it carries no pipeline identity of its own
  - `0067-per-resolution-di-scope-for-transient-factory-instances` [Accepted] — `Transient`'s per-resolution DI scope, unchanged here; its `Terms` block defines the configured-lifetime and registration-lifetime axes this ADR uses
  - `0068-deterministic-disposal-finalizer-safety-net` [Accepted] — the release path this ADR's third drain step conforms to
  - `0069-factory-registry-ownership-and-disposal-cascade` [Accepted] — why `MessageMapperRegistry` is the right object to speak for the factories it owns
  - `0039-scoping-dependencies-inline-with-lifetime-scope` [Proposed] — a DI scope per registered subscriber on `Publish`; not reopened
  - `0033-lifetime-of-command-processor-and-mediator` [Proposed] — the `CommandProcessor` is a singleton; not reopened (C-5)
  - `0014-di-friendly-framework` [Accepted] — per-family factory interfaces rather than abstracting an IoC container; the principle that keeps `IAmAScope` container-free
  - `0064-pipeline-cache-type-key` [Accepted] — the type-keyed metadata caches in these same builders
  - `0007-aspect-oriented-programming` [Accepted] and `0004-use-an-envelope-wrapper-with-transports` [Proposed] — the wrap/unwrap transform pipeline this ADR scopes
  - `0005-support-async-pipelines` [Accepted] — why the sync/async twins exist that this change follows
- External references:
  - [Dependency injection guidelines — scope validation and captive dependencies](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
  - [Default interface members and `netstandard2.0`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/default-interface-methods)
  - Wirfs-Brock & McKean, *Object Design: Roles, Responsibilities, and Collaborations* — the role/stereotype vocabulary used to allocate `IAmAScope` as an information holder
