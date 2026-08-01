---
id: 0070-per-pipeline-di-scope-for-mapper-and-transform-factories
title: "Per-pipeline DI scope shared by the mapper and transform factories"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-08-01
summary: "A transform pipeline takes exactly one DI scope, created and owned by the container package and entered by both the mapper factory and the transformer factory through a new additive core role (IAmAPipelineScopeParticipant) driven by the transform pipeline builders, with the scope handle (IAmAScope, both IDisposable and IAsyncDisposable) held by the pipeline and released when it is. Closes the defects where a Scoped mapper lived for the process and where the mapper and its transforms did not share a container-Scoped dependency, without changing any of the six factory interfaces."
tags:
  - "lifetime"
  - "di"
  - "pipeline"
  - "message-mapping"
---

# 70. Per-pipeline DI scope shared by the mapper and transform factories

Date: 2026-08-01

## Status

Proposed

## Context

A transform pipeline is built from two families of artefact: the message mapper, resolved through `IAmAMessageMapperFactory`/`IAmAMessageMapperFactoryAsync` by way of `MessageMapperRegistry`, and the `[WrapWith]`/`[UnwrapWith]` transforms, resolved through `IAmAMessageTransformerFactory`/`IAmAMessageTransformerFactoryAsync`. On current master those two families come from **two different DI scopes**, and under `ServiceLifetime.Scoped` neither DI scope is ever released until the host shuts down.

**Parent Requirement**: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md)

**Scope**: This ADR decides one thing — **a transform pipeline takes one DI scope, and the mapper factory and the transformer factory both resolve from it**. It discharges FR-1 … FR-7 and constraint C-19, and closes Defect 1 and Defect 1b.

It introduces `IAmAScope` as the core scope handle, settling the disposal half of C-8 and confirming the seam types' home. It does **not** decide `IAmAScopeProvider`, the *ambient* concept, `ScopeAffinity`, adoption or borrowing, ASP.NET (`IHttpContextAccessor`), the opt-in option on `IBrighterOptions`, `Publish`-subscriber ambient suppression, or the `ValidatePipelines()` rules of FR-22. Those are deferred to ADRs 0071 and 0072. This ADR is written so as not to foreclose them, and *Forward compatibility with ADRs 0071 and 0072* below says how each lands on top of what is decided here.

Handler pipelines are **not** re-implemented. FR-7 requires today's handler behaviour to be preserved and regression-guarded, and it is: nothing in `ServiceProviderHandlerFactory`, `PipelineBuilder<TRequest>`, `HandlerLifetimeScope` or `IAmALifetime` changes.

### The two factory families release completely differently

Every factory call is a `Create` matched by a `Release`, and there are six such interfaces. They divide into two families that reclaim on entirely different terms.

| Family | `Create` / `Release` | Its DI scope is keyed per | `Scoped` is reclaimed by |
| --- | --- | --- | --- |
| `IAmAHandlerFactorySync` (`:44`, `:51`), `IAmAHandlerFactoryAsync` (`:44`, `:51`) | `Create(Type, IAmALifetime)` / `Release(handler, IAmALifetime)` | **pipeline** — one `ServiceProviderLifetimeScope` per `IAmALifetime` (`ServiceProviderHandlerFactory.cs:127-131`) | `Release` — it disposes that pipeline's DI scope (`:102-107`, `:133-137`) |
| `IAmAMessageMapperFactory` (`:45`, `:60`), `IAmAMessageMapperFactoryAsync` (`:46`, `:62`), `IAmAMessageTransformerFactory` (`:44`, `:50`), `IAmAMessageTransformerFactoryAsync` (`:45`, `:54`) | `Create(Type) → Lease<T>?` / `Release(Lease<T>?)` | **factory** — one built in the constructor (`ServiceProviderMapperFactory.cs:46`, `ServiceProviderTransformerFactory.cs:46`) | **nothing** |

The handler family already does the right thing: a per-pipeline object travels on every call, so `ServiceProviderHandlerFactory` can key a DI scope on it and dispose that DI scope on `Release`. **This is the model this ADR copies.**

The mapper/transform family cannot. `Create(Type)` carries nothing that identifies a pipeline (ADR 0066 deliberately made the return an opaque `Lease<T>`), so those factories key one DI scope for their whole life and release *per resolution*. `ServiceProviderLifetimeScope.GetOrCreate<T>(Type, out object? releaseToken)` (`:126`) issues a release token in exactly one case — isolated `Transient` (`:139-140`, `GetTransient` `:259-261`). For `Scoped` the token is `null` (`:136`, documented at `:118-123`), so `Release(Lease)` is a no-op and the artefact is reclaimed only when the factory itself is disposed at shutdown (`ServiceProviderMapperFactory.cs:78`; its own remarks say so at `:61-65`).

Two defects follow, and they are the whole of this ADR's problem:

- **Defect 1 — a `Scoped` mapper or transform silently lives for the process.** The factories are constructed once for the singleton `Dispatcher` and once for the singleton `OutboxProducerMediator`, so `GetOrCreateScoped` (`:163-178`) caches every artefact by type for the host's life. Message N+1 sees message N's state, and an `IDisposable` mapper is never disposed.
- **Defect 1b — the mapper and transformer factories do not share a DI scope.** `ServiceProviderMapperFactory` and `ServiceProviderTransformerFactory[Async]` each build their own `ServiceProviderLifetimeScope`, hence their own `IServiceScope`. A container-`Scoped` dependency injected into a mapper *and* into its `[UnwrapWith]` transform is therefore two instances. Fixing Defect 1 factory-by-factory would leave this untouched — which is what C-19 records, and why FR-3 is the requirement that shapes the solution.

`ClaimCheckTransformer` (`src/Paramore.Brighter/Transforms/Transformers/ClaimCheckTransformer.cs:62`) is the concrete case: it takes `IAmAStorageProvider` and `IAmAStorageProviderAsync`, and is the one Brighter-shipped transform with constructor dependencies. A user's mapper and its claim-check transform sharing a `Scoped` unit of work is exactly FR-3's example.

### The forces

- **NFR-1 forbids a parameter.** None of the six factory interfaces may gain a member or change a signature, and Brighter targets `netstandard2.0`, so there are no default interface members to lean on. The shared DI scope cannot ride on `Create`. Nor may any file under `src/Paramore.Brighter/` reference `ServiceLifetime`, `IServiceCollection`, `IServiceProvider` or `ServiceDescriptor` — and because `Microsoft.Extensions.DependencyInjection` is already on core's compile closure transitively through `Microsoft.Extensions.Logging`, that source-level rule is the one that bites; a project-file check alone is vacuous.
- **ADR 0014 is the principle NFR-1 protects.** Brighter offers per-family factory interfaces rather than abstracting an IoC container. Anything this ADR adds to core must be container-agnostic in the same way.
- **The two factories are constructed at different sites.** In `Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`: `new ServiceProviderMapperFactory(provider)` and `new ServiceProviderMapperFactoryAsync(provider)` at `:807`/`:808`, inside the `MessageMapperRegistry`; `new ServiceProviderTransformerFactory(provider)` at `:945` and `new ServiceProviderTransformerFactoryAsync(provider)` at `:957`, from separate public helpers. Each takes only an `IServiceProvider`. Nothing today connects them.
- **NFR-4 — thread safety.** The factories are singletons shared across concurrent pipelines: several `Post` calls on one `OutboxProducerMediator`, several performers consuming concurrently.
- **NFR-5 / NFR-6 — bounded, cheap.** Zero Brighter-created DI scopes live after the Nth message, and **at most one DI scope begin/release per pipeline**, never one per resolution.
- **NFR-8 — `IAmAScope` must be documented as distinct from `IAmALifetime`.** `IAmALifetime` (`src/Paramore.Brighter/IAmALifetime.cs`) is `IDisposable` with `Add(IHandleRequests)`/`Add(IHandleRequestsAsync)`: it *tracks handler instances* for a handler pipeline and is implemented by `HandlerLifetimeScope`. It is not a DI scope and is not being replaced.
- **C-1 — Microsoft's DI scopes do not nest.** A child scope created from a scoped provider is root-parented. This is why the unit has to be the pipeline (D0) and why there is no "scope within a scope" available.
- **C-3 stands.** On the consumer a transform pipeline's DI scope ends before the handler pipeline's begins (`Proactor.cs:239` then `:241`). A `Scoped` dependency used by an unwrap transform and by the handler is two instances. That is intended and is not fixed here.
- **D3 — a clean break.** `MapperLifetime.Scoped` stops caching across messages, with no compatibility flag (OOS-8). This is a deliberate behavioural change requiring a release note.
- **D12 — participation is structural.** For a transform pipeline the mapper factory *and* the transformer factory both participate, **whether or not the mapper declares any transform**.

## Decision

**A transform pipeline takes one DI scope, created and owned by the container package, entered by every factory that serves that pipeline, and released when the pipeline is released.**

The hand-off is a **new, additive role interface** in `Paramore.Brighter`, implemented by the container-backed factories alongside the six existing factory interfaces, and driven by `TransformPipelineBuilder`/`TransformPipelineBuilderAsync`. None of the six factory interfaces changes.

### Architecture Overview

```
                    Paramore.Brighter (core) — no container types
  ┌──────────────────────────────────────────────────────────────────────────┐
  │  TransformPipelineBuilder[Async].BuildWrapPipeline / BuildUnwrapPipeline  │
  │                                                                          │
  │  1. scope = CreatePipelineScope()   ── asked of the mapper registry,      │
  │                                        then the transformer factory;      │
  │                                        first non-null wins (may be null)  │
  │  2. EnterPipelineScope(scope)       ── told to BOTH participants (D12)    │
  │  3. registry.Get<TRequest>()        ── mapper Create   (sync builder)     │
  │     registry.GetAsync<TRequest>()   ── mapper Create   (async builder)    │
  │     transformerFactory.Create(...)  ── transform Creates                  │
  │  4. ExitPipelineScope(scope)        ── finally; BOTH participants         │
  │  5. pipeline holds `scope`; pipeline.Dispose()/DisposeAsync() ends it     │
  └──────────────────────────────────────────────────────────────────────────┘
                                   │  IAmAScope (opaque handle)
                                   ▼
        Paramore.Brighter.Extensions.DependencyInjection
  ┌──────────────────────────────────────────────────────────────────────────┐
  │  ServiceProviderPipelineScope : IAmAScope, IAmAResolutionSource           │
  │      owns exactly one IServiceScope                                      │
  │                                                                          │
  │  ServiceProviderMapperFactory           ─┐                                │
  │  ServiceProviderMapperFactoryAsync       │  all four read the SAME        │
  │  ServiceProviderTransformerFactory       │  IServiceScope out of the      │
  │  ServiceProviderTransformerFactoryAsync ─┘  handle                        │
  └──────────────────────────────────────────────────────────────────────────┘
```

One `IServiceScope` per transform pipeline, reached by every participating factory, disposed exactly once when the pipeline is released. That is FR-1, FR-2 and FR-3 in one mechanism, and it is the same shape `ServiceProviderHandlerFactory` already uses for handlers — with `IAmAScope` playing, for transform pipelines, the per-pipeline-object part that `IAmALifetime` already plays for handler pipelines.

### Key Components

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

- **Home** — the `Paramore.Brighter` namespace and assembly, alongside `IAmALifetime`. This confirms C-8's assumption. It references no container type, so the source-level half of NFR-1 holds.
- **Both `IDisposable` and `IAsyncDisposable`** (C-8, settled). The precedent is exact: `src/Paramore.Brighter/IAmATransformLifetimeAsync.cs` is `internal interface IAmATransformLifetimeAsync : IDisposable, IAsyncDisposable`. This costs no new dependency — `src/Paramore.Brighter/Paramore.Brighter.csproj:24` already carries a `netstandard2.0`-conditional `PackageReference Include="Microsoft.Bcl.AsyncInterfaces"`, whose comment states it is there precisely because `ReleaseAsync`/`IAsyncDisposable` are on the public async surface. Both members are needed because the sync pipeline releases through `Dispose()` and the async pipeline through `DisposeAsync()`, and the async path must not block the Proactor's single-threaded synchronization context.
- **Error conditions** — `Dispose()` and `DisposeAsync()` are **idempotent**; a second call of either, in either order, is a no-op and must not throw (AC-8). A disposal that fails throws to its caller; the existing release call sites (`OutboxProducerMediator.ReleasePipeline` `:1269-1279` and `ReleasePipelineAsync` `:1281-1291`) already log at `Error` and swallow, so a successful `Post` is not failed by a teardown fault.
- **No members beyond disposal.** It is a handle: core's only responsibility toward it is *holding* it and *ending* it. Adding a "which scope is this?" accessor would put container knowledge in core.

#### `IAmAPipelineScopeParticipant` — the role (new, core, public)

```csharp
namespace Paramore.Brighter
{
    /// <summary>
    /// A factory (or a registry over factories) that can resolve one pipeline's artefacts from a DI
    /// scope shared with the other factories serving that pipeline. Implemented alongside — never
    /// instead of — the mapper and transformer factory interfaces, which are unchanged.
    /// </summary>
    public interface IAmAPipelineScopeParticipant
    {
        /// <summary>Creates a pipeline scope this participant can resolve from, or null if it has
        /// none to offer. The caller owns the returned scope and must release it.</summary>
        IAmAScope? CreatePipelineScope();

        /// <summary>Makes <paramref name="scope"/> the scope this participant resolves from on the
        /// current flow, until the matching <see cref="ExitPipelineScope"/>.</summary>
        void EnterPipelineScope(IAmAScope scope);

        /// <summary>Stops resolving from <paramref name="scope"/>. Never releases it — the scope
        /// outlives the build and is released with the pipeline.</summary>
        void ExitPipelineScope(IAmAScope scope);
    }
}
```

**Contract.**

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `CreatePipelineScope()` | none | a new, owned `IAmAScope`, or `null` when this participant has no pipeline scope to offer (its configured lifetime is not `Scoped`, or it is not container-backed) | may throw if the container cannot create a scope; the builder's existing `catch` turns that into `ConfigurationException` on the failed-build path |
| `EnterPipelineScope(scope)` | a non-null scope, possibly created by a *different* participant | none | must not throw for a scope it did not create and does not recognise — it ignores it. Re-entrant: an inner enter saves and an exit restores the outer value |
| `ExitPipelineScope(scope)` | the scope passed to the matching enter | none | must be safe to call when no enter succeeded (the `finally` path), and must **never** dispose the scope |

**Why `Enter`/`Exit` and not `Begin`/`End`.** `End` reads as "the scope is over", and it is not: the artefacts resolved inside the bracket, and their container-`Scoped` dependencies, must stay alive until the pipeline is released, which happens long after the build returns. Exiting the bracket only stops the factory *resolving* from that scope; **releasing** it is `IAmAScope.Dispose()`, done by the pipeline. Separating the two verbs is what makes the mechanism explainable.

**Why not a returned disposable bracket.** `IDisposable EnterPipelineScope(IAmAScope)` gives a tidier `using`, but there are two participants per pipeline, so it is two nested `using`s and two allocations per message on the hot path, and it puts a *second* disposable next to `IAmAScope` whose disposal means something different. NFR-8 already warns about one pair of close-sounding types; a second would be worse. The builders already have explicit `try`/`catch` for the failed-build path, so a `try`/`finally` costs nothing in readability.

**Why one role and not two.** Splitting creation ("knowing how to open a pipeline scope") from participation ("resolving within one") was considered. It is rejected because there is no implementor of one that is not an implementor of the other — today, under 0071, and for a third-party container under NFR-7 — and "do not add new types without necessity" applies. The calling protocol makes the asymmetry explicit instead: **`CreatePipelineScope()` is asked of participants in order until one answers; `EnterPipelineScope`/`ExitPipelineScope` are told to every participant.**

**Why no sync/async twins.** `IAmATransformLifetime`/`IAmATransformLifetimeAsync` are twinned because their `Add` carries a request-shaped payload that differs (`Lease<IAmAMessageTransform>` vs `Lease<IAmAMessageTransformAsync>`) and their disposal differs. Neither applies here: no member of this role carries a transform- or mapper-typed payload, and the only sync/async asymmetry — disposal — lives on `IAmAScope`, which carries both. One role therefore serves `TransformPipelineBuilder` and `TransformPipelineBuilderAsync` alike. ADR 0005's twinning rationale is about async I/O surfaces; this role has none.

**Why public and not internal.** `IAmATransformLifetime` is `internal` because it has exactly one implementation, in the same assembly. This role is implemented in a *different* assembly (`Paramore.Brighter.Extensions.DependencyInjection`) and, under NFR-7, must be implementable over Autofac or SimpleInjector by a third party. It has to be public. That is a real, permanent addition to core's public surface and is recorded under Negative.

#### `IAmAResolutionSource` — the non-core hand-off (new, DI package, public)

```csharp
namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>The contract by which an <see cref="IAmAScope"/> exposes the provider its pipeline
    /// resolves from. Deliberately outside Paramore.Brighter: NFR-1 forbids IServiceProvider on any
    /// core interface.</summary>
    public interface IAmAResolutionSource
    {
        IServiceProvider ServiceProvider { get; }
    }
}
```

`ServiceProviderPipelineScope` implements `IAmAScope` and `IAmAResolutionSource` over one `IServiceScope`. A participating factory reads the provider back out with a type test; a handle it does not recognise is ignored and the factory falls back to its existing behaviour. This is exactly the division FR-10 requires — core sees a handle, the container package sees a provider — established here for Brighter's own owned scopes only.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| `Paramore.Brighter` | `IAmAScope` | **new** |
| `Paramore.Brighter` | `IAmAPipelineScopeParticipant` | **new** |
| `Paramore.Brighter` | `TransformPipelineBuilder` | brackets `BuildWrapPipeline<TRequest>()` (`:93`) and `BuildUnwrapPipeline<TRequest>()` (`:134`); `CleanUpAfterFailedBuild<TRequest>` (`:231`) releases an owned scope |
| `Paramore.Brighter` | `TransformPipelineBuilderAsync` | the same two builds and the same cleanup path (throwing paths `:122`, `:163`) |
| `Paramore.Brighter` | `TransformPipeline<TRequest>`, `TransformPipelineAsync<TRequest>` | hold the pipeline scope; release it in the drain |
| `Paramore.Brighter` | `WrapPipeline<TRequest>`, `UnwrapPipeline<TRequest>`, `WrapPipelineAsync<TRequest>`, `UnwrapPipelineAsync<TRequest>` | optional trailing constructor parameter, forwarded to the base |
| `Paramore.Brighter` | `TransformPipelineDrain` (`internal static`, `:38`) | a third drain step alongside today's `disposeScope`/`releaseMapper` (`Drain` `:46`, `DrainAsync` `:85`), same hold-and-compose error handling |
| `Paramore.Brighter` | `MessageMapperRegistry` (`:41`) | implements `IAmAPipelineScopeParticipant` by forwarding to the factories it owns |
| `…DependencyInjection` | `ServiceProviderPipelineScope`, `IAmAResolutionSource` | **new** |
| `…DependencyInjection` | `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory`, `ServiceProviderTransformerFactoryAsync` | implement `IAmAPipelineScopeParticipant` |
| `…DependencyInjection` | `ServiceProviderLifetimeScope` | may bind its `Scoped` path to an externally supplied `IServiceScope` it does not own |

Unchanged, and named so the omission is not read as an oversight: all six factory interfaces; `ServiceProviderHandlerFactory`; `IAmALifetime` and `HandlerLifetimeScope`; `PipelineBuilder<TRequest>` and `Pipelines<TRequest>`; `CommandProcessor`; `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder`, `ConsumerFactory` (C-2); `BrighterOptions`; `IAmATransformLifetime[Async]` and `TransformLifetimeScope[Async]`; and the public registry interfaces `IAmAMessageMapperRegistry` and `IAmAMessageMapperRegistryAsync`, both of which `MessageMapperRegistry` implements (`MessageMapperRegistry.cs:41`) and neither of which gains a member.

### Technology Choices

**Where a `Scoped` factory keeps "the scope I am currently resolving from".** Each container-backed factory holds a private `AsyncLocal<IAmAScope?>`, written by `EnterPipelineScope` and restored by `ExitPipelineScope`. Three properties make this safe rather than ambient-by-the-back-door:

1. **It is opened and closed explicitly by core**, not published implicitly by the container package. A reader of `BuildWrapPipeline` can see the bracket.
2. **It never has to survive an `await`.** Every `Create` call for a transform pipeline happens inside `BuildWrapPipeline`/`BuildUnwrapPipeline`, which are ordinary synchronous methods on *both* builders — `TransformPipelineBuilderAsync.BuildUnwrapPipeline<TRequest>()` returns `UnwrapPipelineAsync<TRequest>` synchronously. Enter and Exit are the same synchronous call frame on the same thread. The `ExecutionContext`-per-worker-task behaviour of `Parallel.ForEach` that complicates the `Publish` path (FR-9, ADR 0072's problem) cannot reach this bracket.
3. **It is per-factory, not shared or static.** What the four factories share is the `IAmAScope` **handle** they are all handed, and through it one `IServiceScope`. There is no package-level table, no static, and no cross-factory coupling — which is what makes it thread-safe under concurrent pipelines (NFR-4): two concurrent builds on two threads have two independent async-local values and two independent handles.

`Enter` saves the previous value and `Exit` restores it, so a nested build (a mapper constructor that itself posts, say) cannot strand the outer pipeline's scope.

**Artefact identity stays Brighter's** (C-17). Every mapper and transform type is still registered `ServiceLifetime.Transient`; the `IServiceScope` supplies *dependency* identity and disposal, and `ServiceProviderLifetimeScope`'s per-type cache supplies *artefact* identity, now per pipeline instead of per factory. Each factory keeps its own artefact cache for the pipeline — a mapper and a transform are different types, so nothing is lost by not sharing the cache, and sharing the `IServiceScope` is precisely and only what FR-3 asks for.

**Why not put the pipeline scope on `IAmALifetime`.** `IAmALifetime` is the handler pipeline's instance tracker, with `Add(IHandleRequests)` on it, and it does not exist on the transform path at all. Reusing it would conflate two units of work and would break NFR-8 the moment it was documented.

### Implementation Approach

**1. Core types.** Add `IAmAScope` and `IAmAPipelineScopeParticipant` to `src/Paramore.Brighter/`. XML documentation on both states, per NFR-8, what each is and how `IAmAScope` differs from `IAmALifetime`, and `IAmALifetime`'s own documentation gains the reciprocal sentence.

**2. The builder protocol.** In `TransformPipelineBuilder` and `TransformPipelineBuilderAsync`, both `BuildWrapPipeline<TRequest>()` and `BuildUnwrapPipeline<TRequest>()` — four methods, and the wrap and unwrap paths are symmetric — acquire and bracket the scope inside the existing `try`:

```csharp
var scope = CreatePipelineScope();
try
{
    EnterPipelineScope(scope);
    messageMapperLease = FindMessageMapper<TRequest>();
    transformLeases = BuildTransformPipeline<TRequest>(FindWrapTransforms(messageMapperLease.Instance));
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
finally
{
    ExitPipelineScope(scope);
}
```

The private helpers hold the type tests and the ordering:

- `CreatePipelineScope()` asks the builder's mapper registry first — `_mapperRegistry as IAmAPipelineScopeParticipant` in the sync builder (`TransformPipelineBuilder.cs:51`), `_mapperRegistryAsync as IAmAPipelineScopeParticipant` in the async one (`TransformPipelineBuilderAsync.cs:50`) — then `_messageTransformerFactory as IAmAPipelineScopeParticipant` (which may be null — the v9 compatibility path, `TransformPipelineBuilder.cs:180`), and returns the **first non-null** handle, or null. Order is fixed and documented: the mapper is the mandatory half of a transform pipeline.
- `EnterPipelineScope(scope)` / `ExitPipelineScope(scope)` are no-ops when `scope` is null, and otherwise call **both** participants. This is D12: the transformer factory is entered even when the mapper declares no transforms, so `TransformerLifetime = Scoped` behaves identically whether or not a `[WrapWith]` is present.

**3. Failed build — FR-5.** `CleanUpAfterFailedBuild<TRequest>` (`TransformPipelineBuilder.cs:231`, and the async twin whose throwing paths are `TransformPipelineBuilderAsync.cs:122` and `:163`) gains the scope. When a pipeline object was constructed it already owns the scope and `pipeline.Dispose()` (`:239`) releases it; when it was not, the cleanup releases the scope directly, after releasing whatever leases were taken. Release failures are caught and logged by the existing guard (`TransformPipelineBuilder.cs:116-125` for wrap, `:157-166` for unwrap), so the `ConfigurationException` carrying the original build error is still what the caller sees (AC-5, AC-6).

**4. Pipeline release — FR-6.** `TransformPipeline<TRequest>` and `TransformPipelineAsync<TRequest>` store the scope in a `protected readonly IAmAScope?` alongside the existing `protected TransformLifetimeScope? InstanceScope` (`TransformPipeline.cs:16`), taken as an optional trailing constructor parameter threaded through `WrapPipeline<TRequest>`, `UnwrapPipeline<TRequest>`, `WrapPipelineAsync<TRequest>` and `UnwrapPipelineAsync<TRequest>`. `TransformPipelineDrain.Drain`/`DrainAsync` — today `Drain(Action disposeScope, Action releaseMapper)` (`:46`) and `DrainAsync(Func<ValueTask> disposeScopeAsync, Func<ValueTask> releaseMapperAsync)` (`:85`) — gain a third step, ordered **after** the transform scope disposal and the mapper release: leases go back to their factories first, then the DI scope is released, so a factory that still needs its `Release` to run (a `Transient` transformer under a mixed configuration) is not resolving against a dead scope. The existing hold-and-compose error handling extends to the third step, so no failure masks another and both surface as an `AggregateException` — conforming to ADR 0068, whose rule is that an explicit `Dispose` surfaces failures and the finalizer only retries best-effort and swallows. The pipeline's existing release-once guard (`Interlocked.Exchange(ref _released, 1)`, `TransformPipeline.cs:65`) already makes the whole drain, and therefore the scope release, happen exactly once (FR-6); `IAmAScope`'s own idempotence is belt and braces for AC-8.

**5. The container package.**

- `ServiceProviderPipelineScope` wraps one `IServiceScope` created from the root provider, exposes it via `IAmAResolutionSource`, and disposes it exactly once under either `Dispose()` or `DisposeAsync()`, claimed with a single atomic exchange, preferring the scope's `IAsyncDisposable` where offered — the same shape `ServiceProviderLifetimeScope.DisposeScope` (`:406`) and `DisposeScopeAsync` (`:449`) already use, including the `SynchronizationContext` suppression (`:384-388`) that keeps a blocking release off the Proactor pump.
- Each of `ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`, `ServiceProviderTransformerFactory` and `ServiceProviderTransformerFactoryAsync` implements the role. `CreatePipelineScope()` returns a new `ServiceProviderPipelineScope` **only when its configured lifetime is `Scoped`**, and `null` otherwise. `Create(Type)` resolves through the entered scope's provider when one is current and the lifetime is `Scoped`; otherwise it takes exactly today's path.
- `MessageMapperRegistry` implements the role by forwarding to the (up to two) factories it was built with: `CreatePipelineScope()` returns the first non-null answer from the sync then the async factory; enter and exit forward to both. This is consistent with ADR 0069 — the registry owns those factories, so it is the right object to speak for them — and because `MessageMapperRegistry` implements both `IAmAMessageMapperRegistry` and `IAmAMessageMapperRegistryAsync` (`:41`), one change serves both builders while **neither public registry interface gains a member**, so a user's own registry implementation is unaffected.
- **No change to the four construction sites** (`ServiceCollectionExtensions.cs:807`, `:808`, `:945`, `:957`) or to `:712`. The shared scope reaches both families through the handle the builder passes, not through construction — which is why factories built at different sites need no new wiring.

**6. Behaviour by configured lifetime.** All three are stated, and two of them do not change (C-6, OOS-7):

| Configured lifetime | `CreatePipelineScope()` | Enter/Exit | Resolution and reclamation | Changed? |
| --- | --- | --- | --- | --- |
| `Transient` | `null` | ignored | a fresh DI scope per resolution, released by `Release(Lease)` — ADR 0067 unchanged, and `IsolateTransientHandlerScope` (`BrighterOptions.cs:37`) untouched | **No** |
| `Scoped` | a new owned `IAmAScope` | engaged | the pipeline's single `IServiceScope`; one artefact per type per pipeline; both artefact and its container-`Scoped` dependencies disposed when the pipeline is released | **Yes — this ADR** |
| `Singleton` | `null` | ignored | the root provider, one artefact per process | **No** |

All three of `HandlerLifetime`, `MapperLifetime` and `TransformerLifetime` still default to `Transient` (`BrighterOptions.cs:20`, `:52`, `:69`), so an application that changes nothing sees no behavioural change from this ADR.

**7. Both sides, both builders.** FR-4 requires the producer side to behave as the consumer does. The producer's wrap pipeline is built and released per `Post`/`DepositPost` in `OutboxProducerMediator` — sync at `:1248` with `ReleasePipeline` at `:1258`, async at `:1312` with `ReleasePipelineAsync` at `:1321` — and the consumer's unwrap pipeline is built and released per message in `Reactor.TranslateMessage` (build `:531`) and `Proactor.TranslateMessage` (build `:538`), each releasing in its `finally`. `OutboxProducerMediator` also builds unwrap pipelines at `:569` and `:587`. Because the scope is created inside the builder and released by the pipeline's disposal, **every one of these six call sites is correct without being touched**, which is also what keeps C-2 intact.

**8. Factories that do not implement the role.** The type test simply fails and no pipeline scope is taken; behaviour is exactly as today. Enumerated, these are the six core factories — `SimpleMessageMapperFactory`, `SimpleMessageMapperFactoryAsync`, `SimpleMessageTransformerFactory`, `SimpleMessageTransformerFactoryAsync`, `EmptyMessageTransformerFactory`, `EmptyMessageTransformerFactoryAsync` — plus any user or third-party factory, and any registry implementation that is not `MessageMapperRegistry`. A mixed configuration (a container-backed mapper factory with a hand-rolled transformer factory) is determined too: the mapper factory supplies the scope, the transformer factory is offered it and ignores it.

**9. Out-of-bracket `Create`.** A `Scoped` factory asked to `Create` with no pipeline scope current falls back to today's factory-wide DI scope. In Brighter's own paths this is unreachable — the only mapper-registry resolutions in `src/` are `_mapperRegistry.Get<TRequest>()` (`TransformPipelineBuilder.cs:332`) and `_mapperRegistryAsync.GetAsync<TRequest>()` (`TransformPipelineBuilderAsync.cs:255`), both inside `FindMessageMapper<TRequest>`, and the only caller of the transformer factories' `Create` is `TransformerFactory<TRequest>`/`TransformerFactoryAsync<TRequest>` from inside `BuildTransformPipeline` — but a third party calling a factory directly gets a total, non-throwing answer rather than an exception.

### Forward compatibility with ADRs 0071 and 0072

Nothing here decides adoption, and nothing here blocks it:

- **Adoption (0071)** changes only what `CreatePipelineScope()` returns. A borrowed request scope is an `IAmAScope` implementation over `HttpContext.RequestServices` whose `Dispose`/`DisposeAsync` are no-ops (FR-12, C-7). Core is unchanged: it holds a handle and releases it, and "releasing" a borrowed handle does nothing. No member is added to `IAmAScope`, so making it minimal now costs 0071 nothing.
- **`IAmAScopeProvider` (0071)** is a *different* role from anything here, and the distinction is D11's: the provider answers "is there an ambient a pipeline may adopt?", while the container package always creates and owns Brighter's own pipeline scopes. `CreatePipelineScope()` is where the container package will consult it and where `ScopeAffinity` will be carried.
- **`IAmAResolutionSource`** is public in the DI package from the outset so that the ASP.NET package or a third party can expose a borrowed provider on the same terms (FR-10, NFR-7).
- **Handler pipelines and `Publish` (0072)** are untouched. `PipelineBuilder<TRequest>`'s eager per-subscriber resolution and its end-of-publish release stay exactly as they are (D10); the ambient suppression FR-8 needs is a separate mechanism this ADR neither adds nor precludes.

This ADR **supersedes no prior ADR.** It extends the 0066–0069 sequence.

## Consequences

### Positive

- **Defect 1 is closed.** A `Scoped` mapper or transform is per transform pipeline. Message N's instance is disposed before message N+1's is constructed, on the consumer (FR-1, FR-2) and per `Post`/`DepositPost` on the producer (FR-4).
- **Defect 1b is closed.** One `IServiceScope` serves the mapper and every transform in a pipeline, so a container-`Scoped` dependency injected into both is one instance (FR-3, C-19) — and it is one instance whether or not the mapper declares a transform (D12).
- **Bounded resources.** Steady-state consumption leaves zero Brighter-created DI scopes live (NFR-5); the `_scopedInstances` cache that grew for the host's life now dies with the pipeline.
- **Cost is per pipeline.** Exactly one DI scope is begun and released per transform pipeline that has a `Scoped` participating factory, and none per resolution (NFR-6). A pipeline with no `Scoped` participant creates no DI scope at all and pays two failed type tests.
- **The two factory families converge.** Mappers and transforms now scope the way handlers already do. There is one story to teach.
- **No ambient state, no cast in the message path's hot loop, no container leak into core.** The mechanism is visible at the call site in `BuildWrapPipeline`/`BuildUnwrapPipeline`, and a test double implementing `IAmAPipelineScopeParticipant` in an assembly that does not reference `Microsoft.Extensions.DependencyInjection` can assert the whole protocol.
- **NFR-1 holds in both halves.** No factory interface changes; no new direct package reference on `Paramore.Brighter`; and the new core types name no container type, so the source-level scan stays clean.
- **`Transient` and `Singleton` are untouched** (C-6, OOS-7), including ADR 0067's per-resolution scopes and `IsolateTransientHandlerScope`.
- **Handler behaviour is preserved, not re-implemented** (FR-7). `FactoryLifetimeTests.Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs:36`) and its async twin `AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope` (`:154`) assert *within-pipeline* handler identity — two `Create` calls against the same `TestLifetimeScope` returning the same instance — and must keep passing unchanged as the regression guard.

### Negative

- **Core gains two public types**, `IAmAScope` and `IAmAPipelineScopeParticipant`, that most users will never implement, and one of them is close enough in name to the existing `IAmALifetime` to need documentation to keep them apart (NFR-8). Public surface in core is permanent.
- **Core gains type tests.** `TransformPipelineBuilder` and `TransformPipelineBuilderAsync` each ask "is my registry, or my transformer factory, a participant?". Two `as` casts per pipeline build on the message path. Cheap, but it is coupling by capability rather than by contract, and it means a factory can be *silently* non-participating: a hand-rolled container-backed factory that forgets the role keeps Defect 1 with no diagnostic.
- **The container package carries per-flow state.** Each factory holds an `AsyncLocal<IAmAScope?>`, set and restored inside one synchronous call. It is bracketed and never crosses an `await`, but it is state that a debugger does not show you next to the `Create` call.
- **D3 is a behavioural break.** `MapperLifetime.Scoped` stops caching mappers across messages, with no compatibility flag (OOS-8). An application that relied on that — deliberately or not — migrates to `Singleton`. **This needs a release note.**
- **Three tests encode the old contract and must change**, all in `tests/Paramore.Brighter.Extensions.Tests/`:
  - `When_releasing_a_scoped_mapper_it_should_stay_usable_for_later_resolutions.cs`
  - `When_two_threads_first_resolve_a_scoped_mapper_concurrently_it_should_not_leak_a_scope.cs`
  - `When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it.cs`

  The first asserts precisely the cross-pipeline reuse that FR-1 removes. The second and third are about a factory-wide scope that a `Scoped` factory no longer keeps for the pipeline path; their invariants move onto the pipeline scope.
- **Six pipeline constructors and one internal drain helper change shape.** `WrapPipeline<TRequest>`, `UnwrapPipeline<TRequest>`, `WrapPipelineAsync<TRequest>`, `UnwrapPipelineAsync<TRequest>` and the two abstract bases take an optional trailing parameter. Source-compatible for callers who use the existing parameters, binary-breaking for anyone who constructed one without recompiling — which in practice is only Brighter's two builders.
- **A residual out-of-bracket path.** A `Scoped` factory called outside a build bracket still resolves from the factory-wide DI scope, i.e. still exhibits Defect 1. Unreachable from Brighter's own code, but it exists.
- **`TransformLifetimeScope`/`TransformLifetimeScopeAsync` are now one of three things a pipeline drains** (transform leases, then the mapper lease, then the DI scope). They are neither extended nor subsumed — they track *leases*, this tracks a *DI scope*, and the ordering between them is load-bearing — but a reader has to hold three release steps in mind instead of two.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| A pipeline scope outlives its pipeline (leak) or is released early (use-after-dispose) because the bracket and the release are in different places | The scope is created in the builder and released by the pipeline's existing release-once drain, which every call site already invokes in a `finally` — six sites, all unchanged. The failed-build path releases it explicitly (FR-5). AC-5's 1,000-failure case is the regression guard |
| A release failure masks a `ConfigurationException` | The existing guard in both builders' `catch` blocks catches and logs cleanup failures before rethrowing the build error (`TransformPipelineBuilder.cs:116-125`, `:157-166`); the third drain step composes with the existing hold-and-compose handling rather than replacing it (ADR 0068). AC-6 |
| Double release, or one pipeline's release affecting another's live scope | The pipeline's `Interlocked.Exchange` release-once guard, plus `IAmAScope`'s own idempotent disposal claimed with a single atomic exchange, plus per-pipeline handles with no shared table. AC-8 |
| Concurrent pipelines interfering | Each factory's current-scope slot is per flow and per factory; the shared object is the immutable handle. No static, no package-level registry. NFR-4 |
| A container-backed factory that forgets the role silently keeps Defect 1 | All four in-repo implementations are changed together and enumerated in this ADR; a test asserts each of the four implements the role |
| The `Enter`/`Exit` bracket is later moved somewhere that crosses an `await`, reintroducing the `ExecutionContext` hazard | Documented as a load-bearing invariant on the role: every `Create` for a transform pipeline happens inside the synchronous `Build*Pipeline` call, on both builders |
| Terminology drift between `IAmAScope`, `IAmALifetime`, `HandlerLifetimeScope`, `ServiceProviderLifetimeScope` and `TransformLifetimeScope` | NFR-8: XML documentation on `IAmAScope` and on `IAmALifetime` states what each is for and how they relate; `docs/guides/lifetimes-and-scoping.md` (FR-25) carries the same distinction |
| A shared `MessageMapperRegistry` disposed by one owner while another builds a pipeline | Unchanged by this ADR — ADR 0069's ownership rules and `ServiceProviderLifetimeScope`'s targeted `ObjectDisposedException` message (`:320`) still govern |

## Alternatives Considered

**1. Add a per-pipeline token to the factory signatures, as the handler factories carry `IAmALifetime`.** This is the most direct expression of the idea, and it is what makes the handler family work. **Rejected — forbidden by NFR-1**, which names all six interfaces, and unavailable in any case: `netstandard2.0` has no default interface members, so the member could not be added without breaking every existing implementation. ADR 0014's principle — per-family factory interfaces, container-agnostic — is exactly what NFR-1 is protecting.

**2. A container-package-private ambient.** Put an `AsyncLocal<ServiceProviderLifetimeScope>` inside `Paramore.Brighter.Extensions.DependencyInjection`, publish it wherever a pipeline begins, and have both factories read it. No core change at all, no new core type, no type test, and the scope is trivially shared between the two factories. **Rejected on three counts.** It is invisible coupling: nothing at the call site in `BuildWrapPipeline` says a scope is in play, so the mechanism cannot be read, cannot be unit-tested without a container, and cannot be implemented by a non-Microsoft container (NFR-7). It has no explicit end, so the failed-build release of FR-5 has nowhere natural to live. And it needs a publication point that only core knows — the start of a pipeline — which means core must call *something* anyway, at which point the honest version is a role. It also introduces a second ambient mechanism alongside the suppression flag FR-8 requires, and two independent ambients interacting is the kind of thing that needs an explicit `ExecutionContext` restore under `Parallel.ForEach`, which restores per worker task rather than per body invocation. The chosen design uses an async-local *inside* the participants, but as a bracketed implementation detail of an explicit protocol, never as the protocol.

**3. Construction-only: hand both factories the same collaborator at their construction sites.** Change `ServiceCollectionExtensions.cs:807`, `:808`, `:945` and `:957` to pass a shared object. Smallest possible surface, no new role. **Rejected**: a collaborator shared for the *lifetime of the factories* is not a per-pipeline scope. To get one DI scope per pipeline out of it, the collaborator still needs a per-pipeline key on every `Create` — and `Create(Type)` has none. It therefore collapses into alternative 1 (a parameter, forbidden) or alternative 2 (an ambient, rejected above), while additionally coupling the four construction sites.

**4. Two roles: a scope source and a scope participant.** Separate "knowing how to open a pipeline scope" from "resolving within one". Cleaner stereotypes, and a test double could implement just the observing half. **Rejected**: there is no implementor of one that is not an implementor of the other — the same four container classes implement both today, under 0071, and for any third-party container under NFR-7 — so the split adds a type without adding optionality, against "do not add new types without necessity". The asymmetry that motivated it is captured instead in the calling protocol: `CreatePipelineScope()` is asked of participants in order until one answers; enter and exit are told to all of them.

**5. A per-pipeline factory facade returned from the bracket.** Have `EnterPipelineScope` return a facade over the factory that resolves from this pipeline's DI scope, and have the builder use the facade for the duration. No ambient at all, and the pipeline identity is carried by an object reference — the cleanest possible answer to "how does `Create(Type)` know which pipeline it is in?". **Rejected**: the builder resolves its mapper through `IAmAMessageMapperRegistry`/`IAmAMessageMapperRegistryAsync`, not through the mapper factory, so a facade over the factory is not enough — the registry would need one too, and both public registry interfaces would have to gain a member, breaking every user implementation. The pipeline also holds the registry and the transformer factory for *release* (`TransformPipeline.ReleaseUnmanagedResources`, `TransformLifetimeScope`), so the facades would have to be held by the pipeline as well. Two extra allocations per message and two more objects in the release graph, to avoid an async-local write that never crosses an `await`.

**6. `Begin`/`End` rather than `Enter`/`Exit`, with `End` releasing the scope.** **Rejected**: `End` would have to be called after the pipeline is *used*, not after it is *built*, because the artefacts and their scoped dependencies must live until then. That would push the bracket out of the builder and into all six call sites — `OutboxProducerMediator` ×4 and the two pumps — and the pumps are exactly what C-2 forbids changing. Splitting "stop resolving here" (`Exit`, in the builder) from "release" (`IAmAScope.Dispose()`, on the pipeline) keeps the change inside the builders and the pipelines.

**7. `IAmAChainScope`, `IAmAPipelineScope`, `IAmAUnitOfWorkScope` as the handle name.** Considered and **rejected** by D4; the name is `IAmAScope`. `IAmAPipelineScope` was the closest, but the seam is used for handler pipelines too under 0071/0072, and "chain" is not a term of art in this codebase — the unit is a pipeline, and `PipelineBuilder<TRequest>.Build` returns `Pipelines<TRequest>`.

**8. `IAmAScope : IDisposable` only.** Halves the surface. **Rejected** by the settled decision on C-8, and for a concrete reason: the async pipeline releases through `DisposeAsync`, and on the Proactor's single-threaded synchronization context an `IAsyncDisposable`-only mapper released through a blocking synchronous path is a stall at best (see the guidance in `ServiceProviderLifetimeScope`, `:384-388`). `IAmATransformLifetimeAsync` already carries both interfaces for the same reason, and `Microsoft.Bcl.AsyncInterfaces` is already referenced on `netstandard2.0` (`Paramore.Brighter.csproj:24`), so this costs nothing.

## References

- Requirements: [specs/0036-scoped-lifetime-per-pipeline/requirements.md](../../specs/0036-scoped-lifetime-per-pipeline/requirements.md) — FR-1 … FR-7, NFR-1, NFR-4, NFR-5, NFR-6, NFR-8, C-1, C-3, C-6, C-8, C-17, C-19, D0, D3, D4, D10, D12
- Related ADRs (cited by slug — ADR numbers are not unique in this repo, C-16):
  - `0066-release-factory-instances-on-an-opaque-lease` [Accepted] — why `Create` returns a `Lease<T>` carrying an opaque token, and therefore why it carries no pipeline identity
  - `0067-per-resolution-di-scope-for-transient-factory-instances` [Accepted] — `Transient`'s per-resolution DI scope, unchanged here; its `Terms` block defines the configured-lifetime and registration-lifetime axes this ADR uses
  - `0068-deterministic-disposal-finalizer-safety-net` [Accepted] — the release path this ADR's third drain step conforms to
  - `0069-factory-registry-ownership-and-disposal-cascade` [Accepted] — why `MessageMapperRegistry` is the right object to speak for the factories it owns
  - `0039-scoping-dependencies-inline-with-lifetime-scope` [Proposed] — a DI scope per registered subscriber on `Publish`; not reopened
  - `0033-lifetime-of-command-processor-and-mediator` [Proposed] — the `CommandProcessor` is a singleton; not reopened (C-5)
  - `0014-di-friendly-framework` [Accepted] — per-family factory interfaces rather than abstracting an IoC container; the principle NFR-1 protects
  - `0064-pipeline-cache-type-key` [Accepted] — the type-keyed metadata caches in these same builders
  - `0007-aspect-oriented-programming` [Accepted] and `0004-use-an-envelope-wrapper-with-transports` [Proposed] — the wrap/unwrap transform pipeline this ADR scopes
  - `0005-support-async-pipelines` [Accepted] — why sync/async twins exist, and the reason this role does not need them
- External references:
  - [Dependency injection guidelines — scope validation and captive dependencies](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
  - [`AsyncLocal<T>` and `ExecutionContext` flow](https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1)
  - Wirfs-Brock & McKean, *Object Design: Roles, Responsibilities, and Collaborations* — the role/stereotype vocabulary used to allocate `IAmAScope` (information holder) and `IAmAPipelineScopeParticipant` (service provider)
