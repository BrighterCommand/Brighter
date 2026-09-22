# Bugfix: Disposable message mappers have no release path — transient scopes accumulate per message

**Linked Issue**: PR #4254 review comment 5064585481 ("Part B" follow-up to bugfix 0006)
**Status**: Verified

## Symptom

**Observed.** A user-defined message mapper that implements `IDisposable`, resolved under the
default `MapperLifetime = ServiceLifetime.Transient`
(`src/Paramore.Brighter.Extensions.DependencyInjection/BrighterOptions.cs:35`), causes one
`IServiceScope` to be retained in `ServiceProviderLifetimeScope._transientScopes` for **every
message mapped**, plus a strong reference to the mapper instance itself (the dictionary key). Those
entries are only drained when the factory is disposed at process shutdown
(`ServiceProviderLifetimeScope.cs:151-164`), so a long-running consumer/producer grows without bound.

The mapper instance is *also* never `Dispose()`d before then, so any resource it holds (a
`SHA256`, a pooled buffer, a connection) is held for the process lifetime.

**Expected.** A mapper instance should be released back to its factory once the pipeline that owns
it is finished, exactly as transformers are: `TransformLifetimeScope.ReleaseTrackedObjects()` calls
`IAmAMessageTransformerFactory.Release(...)` (`src/Paramore.Brighter/TransformLifetimeScope.cs:37-44`),
which reaches `ServiceProviderTransformerFactory.Release`
(`src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderTransformerFactory.cs:65-68`)
and drains the per-instance scope in `ServiceProviderLifetimeScope.Release`
(`ServiceProviderLifetimeScope.cs:133-146`).

**Reproduction (hypothesised, to be executed in /bugfix:confirm).** Register a mapper that
implements `IAmAMessageMapper<T>` *and* `IDisposable` as `AddTransient`; set
`MapperLifetime = ServiceLifetime.Transient`; call `ServiceProviderMapperFactory.Create(...)` N
times (equivalent to N messages via `MessageMapperRegistry.Get<TRequest>()`). Expect: N scopes
created and 0 disposed before `factory.Dispose()`, and `DisposeCount == 0` on every mapper
instance. Contrast with `ServiceProviderTransformerFactory`, where `Release` brings this to 0
retained.

**Why the parent fix does not cover this.** Bugfix 0006 changed the store guard in `GetTransient<T>`
from `instance != null` to `instance is IDisposable`
(`ServiceProviderLifetimeScope.cs:119-122`). That disposes the scope immediately for
*non*-disposable instances — which is every in-tree mapper — but it deliberately keeps tracking
disposable ones, precisely because they need a `Release` that mappers do not have. See scope note
"(b) Disposable mappers — latent gap without Part B" in
`bugfixes/0006-mapper-transient-scope-accumulation/bugfix.md:87-90`.

## Suspected Location

**Creation sites — one mapper instance per call, no instance cache (only the *type* is cached):**
- `src/Paramore.Brighter/MessageMapperRegistry.cs:87` — `_messageMapperFactory.Create(messageMapperType)` in `Get<TRequest>()`
- `src/Paramore.Brighter/MessageMapperRegistry.cs:109` — `_messageMapperFactoryAsync.Create(messageMapperType)` in `GetAsync<TRequest>()`

**Callers of `Get`/`GetAsync` (the only four in `src/`):**
- `src/Paramore.Brighter/TransformPipelineBuilder.cs:274` — `FindMessageMapper<TRequest>()`, instance handed to the pipeline
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs:194` — same, async
- `src/Paramore.Brighter/TransformPipelineBuilder.cs:153` — `HasPipeline<TRequest>()`, **instance created then discarded** (only null-checked)
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs:153` — same, async

**Where the instance ends up (and is never released):**
- `src/Paramore.Brighter/TransformPipeline.cs:10` — `protected IAmAMessageMapper<TRequest> MessageMapper`
- `src/Paramore.Brighter/TransformPipelineAsync.cs:10` — async equivalent
- `src/Paramore.Brighter/TransformPipeline.cs:17-34` — `Dispose()`/finalizer call `ReleaseUnmanagedResources()`, which does **only** `InstanceScope?.Dispose()` (line 33). `InstanceScope` is a `TransformLifetimeScope`, which tracks **transforms only** (`TransformLifetimeScope.cs:13,31-35`). The mapper is not tracked and not released.
- `src/Paramore.Brighter/WrapPipeline.cs:62-63`, `src/Paramore.Brighter/UnwrapPipeline.cs:52-53`, `src/Paramore.Brighter/WrapPipelineAsync.cs:66-67`, `src/Paramore.Brighter/UnwrapPipelineAsync.cs:54-55` — each constructs `InstanceScope` and adds only the transforms.

**Pipeline consumers — note none of them dispose the pipeline deterministically:**
- `src/Paramore.Brighter/OutboxProducerMediator.cs:505-518` (`CreateRequestFromMessage`), `:1168-1173` (`MapMessage`), `:1196-1201` (`MapMessageAsync`) — build-and-use, no `using`, no `Dispose()`; every path is preceded by `HasPipeline<TRequest>()`, so **two** mappers are created per operation
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs:470-490` (`MakeUnwrapPipeline`) and `src/Paramore.Brighter.ServiceActivator/Proactor.cs:456-477` — reflective `BuildUnwrapPipeline` invoke, result returned as `object?` and never disposed

**Missing `Release` — interfaces:**
- `src/Paramore.Brighter/IAmAMessageMapperFactory.cs:37-45` — only `Create`
- `src/Paramore.Brighter/IAmAMessageMapperFactoryAsync.cs:37-45` — only `Create`
- Contrast: `src/Paramore.Brighter/IAmAMessageTransformerFactory.cs:36-49` — has both `Create` and `void Release(IAmAMessageTransform transformer)`

**Missing `Release` — implementations (complete in-tree blast radius, 5 classes / 5 files; a repo-wide `grep -rln --include="*.cs" "IAmAMessageMapperFactory"` returns exactly 9 files, all under `src/`, of which 4 are interface/consumer files — there are ZERO custom implementations in `tests/`, `samples/`, or `benchmarks/`):**
1. `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs:34` (`Create` at `:55-58`, `Dispose` at `:63-67`)
2. `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactoryAsync.cs:34` (`Create` at `:55-58`, `Dispose` at `:63`)
3. `src/Paramore.Brighter/SimpleMessageMapperFactory.cs:34` (`Create` at `:52-55`)
4. `src/Paramore.Brighter/SimpleMessageMapperFactoryAsync.cs:34`
5. `src/Paramore.Brighter.ServiceActivator/ControlBusMessageMapperFactory.cs:31` (`Create` at `:38-53`)

Consumers that construct these factories/registries and would be touched by a signature change:
`src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:777-781`,
`src/Paramore.Brighter/CommandProcessorBuilder.cs`,
`src/Paramore.Brighter/ControlBusSenderFactory.cs:56`,
`src/Paramore.Brighter.ServiceActivator/ControlBus/ControlBusReceiverBuilder.cs:159,165`.

**Registry interfaces that would need a release entry point:**
- `src/Paramore.Brighter/IAmAMessageMapperRegistry.cs:43` — `IAmAMessageMapper<T>? Get<T>()`
- `src/Paramore.Brighter/IAmAMessageMapperRegistryAsync.cs:43` — `IAmAMessageMapperAsync<T>? GetAsync<T>()`
- Sole in-tree implementation: `src/Paramore.Brighter/MessageMapperRegistry.cs:39`

**Existing release machinery that already has the right shape:**
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:133` — `public void Release(object? instance)`. It takes `object?`, not `IAmAMessageTransform`, so it can be called with a mapper unchanged. Semantics (`:135-146`): no-op for `Singleton`; for `Transient`, `_transientScopes.TryRemove(instance, out var scope)` then `scope.Dispose()` (disposing the DI scope is what disposes the instance, exactly once); otherwise (Scoped, or Transient with no tracked scope) falls through to `if (instance is IDisposable disposal) disposal.Dispose()`. Note the fall-through: calling `Release` on a *non*-disposable transient mapper is a harmless no-op, but calling it twice on a **Scoped** disposable mapper would double-dispose.

## Root-Cause Hypothesis

**Hypothesis (falsifiable).** The mapper half of Brighter's factory contract is asymmetric with the
transformer half: `IAmAMessageMapperFactory` / `IAmAMessageMapperFactoryAsync` declare only
`Create` and no `Release`, and `MessageMapperRegistry` never releases the instance it obtains from
`Create`. Consequently `ServiceProviderLifetimeScope.Release` — the only code path that removes an
entry from `_transientScopes` outside of factory disposal — is unreachable for mapper instances.
For any mapper implementing `IDisposable` under the default `Transient` lifetime, each
`MessageMapperRegistry.Get<TRequest>()` (`MessageMapperRegistry.cs:87`) permanently adds one
`IServiceScope` + one instance reference to `_transientScopes`
(`ServiceProviderLifetimeScope.cs:117-120`), and the mapper is never disposed until factory
shutdown.

Falsification would look like: finding an existing code path that already disposes the mapper or
its scope per message (e.g. an unnoticed `Dispose` on `TransformPipeline` that reaches the mapper),
or showing `_transientScopes` is drained by some other mechanism.

**Proposed fix — approach B (user's chosen direction). UNVERIFIED — to be proven or refuted in
/bugfix:confirm.**
Add a `Release` method to `IAmAMessageMapperFactory` and `IAmAMessageMapperFactoryAsync`, implement
it on all five in-tree factories (delegating to `ServiceProviderLifetimeScope.Release` for the two
DI factories, no-op for `Simple*` and `ControlBus*`), and add a release path through
`MessageMapperRegistry` (e.g. `Release<TRequest>(IAmAMessageMapper<TRequest>)` /
`ReleaseAsync<TRequest>(IAmAMessageMapperAsync<TRequest>)`) so callers can return the instance.
Wire the call sites so mappers are released exactly as transformers are.

**Open design questions the Confirm step must settle:**

1. **Source-breaking interface change.** `src/Directory.Build.props:43` sets
   `BrighterTargetFrameworks = netstandard2.0;net8.0;net9.0;net10.0`. `netstandard2.0` has no
   runtime support for **default interface members**, so `Release` cannot be added with a default
   body — it is a hard source-breaking change for any third-party `IAmAMessageMapperFactory`
   implementer. In-tree blast radius is small and fully enumerated (5 classes, 5 files, zero in
   tests/samples/benchmarks), but external implementers exist by design (the interface is the
   documented DI extension point — `docs/adr/0014-di-friendly-framework.md`). Confirm must decide
   between: (a) breaking the interface (matches `IAmAMessageTransformerFactory`); (b) a separate
   opt-in interface (e.g. `IAmAMessageMapperFactoryWithRelease`) probed via `is`; (c) deferring to
   a major version. Compare with how `IAmAMessageTransformerFactory` (9 in-tree implementations)
   already carries `Release`.

2. **Where does the release call site belong, and is it deterministic?** The natural mirror is
   `TransformPipeline`/`TransformPipelineAsync` tracking the mapper alongside `InstanceScope`
   (`TransformPipeline.cs:12,31-34`). But `TransformPipeline` today has no reference to the mapper
   factory or registry — the builder (`TransformPipelineBuilder.cs:51`) holds `_mapperRegistry`, so
   the pipeline constructors (`WrapPipeline.cs:52-63`, `UnwrapPipeline.cs:47-53`, and the two async
   equivalents) would need a new dependency. **More importantly, no caller disposes the pipeline**:
   `OutboxProducerMediator.cs:505-518,1168-1173,1196-1201` and
   `Reactor.cs:488`/`Proactor.cs:475` all drop the pipeline on the floor and rely on the finalizer
   `~TransformPipeline()` (`TransformPipeline.cs:26-29`). Routing mapper release through
   `Dispose()` therefore inherits GC-timing non-determinism — which for a leak fix may be
   inadequate. Confirm must decide whether approach B also requires making those call sites
   deterministically dispose the pipeline (a wider behavioural change).

3. **The `HasPipeline` discard sites.** `TransformPipelineBuilder.cs:151-154` and
   `TransformPipelineBuilderAsync.cs:151-154` create a mapper purely to null-check it and never
   hand it to a pipeline, so nothing can ever own or release it. `OutboxProducerMediator` calls
   `HasPipeline` on every send and every receive (`:505,513,1168,1196`) — at least one extra
   orphaned mapper per operation, two on the sync-fallback path. Confirm must decide whether
   approach B releases these instances explicitly, or whether `HasPipeline` should instead be
   refactored to the already-existing type-only lookups
   `MessageMapperRegistry.ResolveMapperInfo(Type)` (`MessageMapperRegistry.cs:117-129`) and
   `ResolveAsyncMapperInfo(Type)` (`:136-148`), which avoid the allocation entirely. This overlaps
   scope note (a) of bugfix 0006.

4. **Double-release / double-dispose risk.** `ServiceProviderLifetimeScope.Release`
   (`:137-146`) falls through to `disposal.Dispose()` when no tracked transient scope is found. If
   a mapper is released twice, or released under `Scoped` lifetime, the instance is disposed more
   than once. Confirm must decide whether the fix needs idempotence (the existing transformer test
   `tests/Paramore.Brighter.Extensions.Tests/When_releasing_a_transient_transformer_the_factory_should_not_retain_it.cs:26-32`
   asserts exactly this "released then factory-disposed must not double-dispose" property).

5. **Async factory shape.** `IAmAMessageMapperFactoryAsync.Create` is synchronous
   (`IAmAMessageMapperFactoryAsync.cs:44`), and `TransformLifetimeScopeAsync` exists as a separate
   type. Confirm must decide whether the async `Release` is `void Release(IAmAMessageMapperAsync)`
   or something awaitable, and whether `IAsyncDisposable` mappers are in scope (note
   `ServiceProviderLifetimeScope.GetTransient` guards on `IDisposable` only —
   `ServiceProviderLifetimeScope.cs:119` — so an `IAsyncDisposable`-only mapper is untracked
   today).

6. **Regression-test vehicle.** `tests/Paramore.Brighter.Extensions.Tests/When_creating_transient_non_disposable_mappers_the_factory_should_not_accumulate_scopes.cs`
   already contains a reusable `ScopeTracker : IServiceScopeFactory` (`:53-87`, counts scope
   disposals) and `TrackingServiceProvider : IServiceProvider` (`:91-104`, redirects
   `IServiceScopeFactory` resolution), plus a `NonDisposableMapper` test double (`:45-50`). A
   disposable-mapper variant of this test plus a dispose-counting mapper double (mirroring
   `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/DisposeCountingTransform.cs`) is the
   obvious vehicle. Confirm must settle whether the test asserts at the factory level (scope
   disposed on `Release`) or end-to-end through `MessageMapperRegistry`/pipeline.

## Confirmed Root Cause

**Verdict: CONFIRMED.**

`ServiceProviderLifetimeScope.GetTransient<T>` (`ServiceProviderLifetimeScope.cs:115-124`)
deliberately retains `instance → scope` in `_transientScopes` when the resolved instance implements
`IDisposable`, **on the contract that the owner will call `Release`**. That contract is honoured for
transformers (`TransformLifetimeScope.ReleaseTrackedObjects` → `IAmAMessageTransformerFactory.Release`
→ `ServiceProviderTransformerFactory.Release` → `ServiceProviderLifetimeScope.Release`) and is
**not implementable** for mappers, because neither `IAmAMessageMapperFactory` nor
`IAmAMessageMapperFactoryAsync` declares a `Release`, no mapper factory implements one, and
`MessageMapperRegistry` has no release entry point.

The only two drains of `_transientScopes` are `Release` (`:138`) and `Dispose` (`:155-157`); the
first is unreachable for a mapper. The mapper factory is a process-lifetime object created once in
`ServiceCollectionExtensions.MessageMapperRegistry` (`:777-786`), so "factory dispose" == process
shutdown.

**Aggravating factor found during Confirm: two mappers are created per message operation, not one.**
`HasPipeline<TRequest>()` creates one purely to null-check it and drops it on the floor, then
`Build*Pipeline` creates a second.

## Evidence

- [x] **Code-trace** (no live infra needed; every citation re-read and verified)
- [ ] Red repro — not yet written; the exact failing assertion is specified below and is written by
      `/bugfix:test`

**(a) `GetTransient<T>` stores `IDisposable` instances in `_transientScopes`** —
`ServiceProviderLifetimeScope.cs:115-124`. The store is at `:119-120` (the triage's `:117-120` was
off; `:117-118` are `CreateScope()`/`GetService`). `:121-122` disposes the scope immediately for
non-disposables.

**(b) `Release` is the only non-`Dispose` drain** — `ServiceProviderLifetimeScope.cs:133-146` holds
the sole `TryRemove` (`:138`); `Dispose` (`:151-164`) drains the rest. Whole-file read: no other
mutation of `_transientScopes`.

**(c) No caller ever reaches it for a mapper**
- Interfaces declare only `Create`: `IAmAMessageMapperFactory.cs:37-45`,
  `IAmAMessageMapperFactoryAsync.cs:37-46`. Contrast `IAmAMessageTransformerFactory.cs` and
  `IAmAMessageTransformerFactoryAsync.cs:43-48` (`Create` + `void Release(...)`).
- `ServiceProviderMapperFactory.cs:55-58` (`Create`), `:63-67` (`Dispose`) — no `Release`.
  `ServiceProviderMapperFactoryAsync.cs:55-58`, `:63-67` — identical.
- `MessageMapperRegistry.cs:87`, `:109` call `Create` per invocation with no instance cache (only
  the *type* dict at `:78-82`, `:100-104`).
- Only four `Get`/`GetAsync` call sites exist in `src/` (zero in `tests/`, `samples/`,
  `benchmarks/`): `TransformPipelineBuilder.cs:153` (`HasPipeline`), `:274` (`FindMessageMapper`),
  `TransformPipelineBuilderAsync.cs:153`, `:194`.
- The instance lands on `TransformPipeline.cs:10` / `TransformPipelineAsync.cs:10`.
  `Dispose`/`~TransformPipeline` (`:17-34`) call `ReleaseUnmanagedResources`, which does **only**
  `InstanceScope?.Dispose()` (`:33`). `InstanceScope` is a `TransformLifetimeScope` whose tracked
  list is `IList<IAmAMessageTransform>` (`TransformLifetimeScope.cs:13`), populated only from
  `Transforms` (`WrapPipeline.cs:62-63`, `UnwrapPipeline.cs:52-53`, and the two async equivalents).
  **The mapper is not tracked.** Worse, `InstanceScope` is only constructed when
  `messageTransformerFactory != null` (`WrapPipeline.cs:60`, `UnwrapPipeline.cs:50`), so with no
  transformer factory the pipeline's `Dispose` is a complete no-op.

**(d) Default `MapperLifetime` is `Transient`** — `BrighterOptions.cs:35`.
(`ServiceProviderMapperFactory.cs:45` falls back to `Singleton` only when `IBrighterOptions` is
absent from the container — 0006 scope note (d) still stands.)

**(e) The mapper is never `Dispose()`d per message** — `IAmAMessageMapper` is a bare marker
interface with no base (`IAmAMessageMapper.cs:32`), so nothing can call `Dispose` without a cast.
Repo-wide grep for a mapper `.Dispose(`/`Release(<mapper>)` across `src/` returns **zero** hits.

**Falsification attempts, all negative:** no `using`, `Dispose()` or ownership transfer at any
pipeline consumption site (table below); no other `_transientScopes` drain; `MessageMapperRegistry.cs:39`
is the only `IAmAMessageMapperRegistry` implementation in the repo, so no alternative registry
could be releasing.

**Triage citation corrections** (all minor, all verified):

| Triage said | Actual |
|---|---|
| `ServiceProviderLifetimeScope.cs:117-120` (store) | `:119-120` |
| `src/Directory.Build.props:43` (TFMs) | `:42` |
| `Reactor.cs:488` / `Proactor.cs:475` "drop the pipeline" | those lines *return* it; the dropping consumer is `Reactor.cs:530-534` / `Proactor.cs:537-544` |
| `UnwrapPipeline.cs:47-53` | ctor `:44-55`, `InstanceScope` at `:52-53` |
| "consumers touched by a signature change" (`ServiceCollectionExtensions.cs:777-781`, `CommandProcessorBuilder.cs`, `ControlBusSenderFactory.cs:56`, `ControlBusReceiverBuilder.cs:159,165`) | **Wrong — none of these need to change.** Adding a member to an interface does not affect call sites that construct concrete factories. `CommandProcessorBuilder.cs:60` is an XML doc comment only. |

**Red repro (to be written by `/bugfix:test`).** Level: end-to-end through `TransformPipelineBuilder`
+ `MessageMapperRegistry` + `ServiceProviderMapperFactory`, in `tests/Paramore.Brighter.Extensions.Tests`.
No live infra. Mapper implements `IAmAMessageMapper<MinimalCommand>` **and** `IDisposable`, counting
`DisposeCount`:

```csharp
using (builder.BuildWrapPipeline<MinimalCommand>()) { }
Assert.Equal(1, mapper.DisposeCount);          // today 0  → RED
Assert.Equal(1, scopeTracker.DisposedCount);   // today 0  → RED
```

Reusable infrastructure, all cited from the existing suite:
- `ScopeTracker : IServiceScopeFactory` — `tests/Paramore.Brighter.Extensions.Tests/When_creating_transient_non_disposable_mappers_the_factory_should_not_accumulate_scopes.cs:53-87`
- `TrackingServiceProvider : IServiceProvider` — same file, `:91-104`
- `NonDisposableMapper` / `MinimalCommand` shape — same file, `:39-50`
- Dispose-counting double — `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/DisposeCountingTransform.cs:11-22`
- Deterministic-teardown test shape — `tests/Paramore.Brighter.Core.Tests/MessageSerialisation/When_Wrapping_Clean_Up_The_Pipeline.cs:32-59`
  (`MyReleaseTrackingTransformFactory` is the exact double to mirror as `MyReleaseTrackingMapperFactory`)

A factory-level companion test (`factory.Release(mapper)` then assert `DisposeCount == 1`, then
`factory.Dispose()` and assert still `1`) is a *compile-time* red today (CS1061), so it lands with
the fix rather than before it. Per PR #4254 review finding #4, assert `Equal(1, …)` — not merely
"unchanged across `Dispose`", which passes vacuously at 0.

A `SimpleMessageMapperFactory`-based test cannot be the vehicle: its `Release` is a no-op by design,
so such a test would stay red after the fix.

## Scope Notes

**Suggested-fix assessment: PARTIAL.** Approach B is the right direction but, as scoped in triage,
would not actually fix the leak, for two independent reasons — both must be folded in.

### (1) No production caller disposes the pipeline — release would be finalizer-driven

Verified individually; the triage's claim holds. Not one production caller disposes a pipeline:

| Site | Evidence |
|---|---|
| `OutboxProducerMediator.CreateRequestFromMessage` | `OutboxProducerMediator.cs:505-518` — `.BuildUnwrapPipeline<TRequest>().UnwrapAsync(...)` / `.Unwrap(...)`, fluent, result never stored, no `using` |
| `OutboxProducerMediator.MapMessage` | `:1168-1173` — same shape |
| `OutboxProducerMediator.MapMessageAsync` | `:1196-1201` — same shape |
| `Reactor.TranslateMessage` | `Reactor.cs:530-534` — `object? pipeline = MakeUnwrapPipeline(...)`, reflective `Unwrap` invoke, never disposed (built at `:470-490`) |
| `Proactor.TranslateMessage` | `Proactor.cs:537-544` — same (built at `:456-477`) |

`grep` over `src/`, `samples/`, `benchmarks/` finds no other `Build*Pipeline` / `new WrapPipeline` /
`new UnwrapPipeline` call sites. In `tests/` there are ~50 `Build*Pipeline` calls but **zero** direct
`new WrapPipeline(...)`/`new UnwrapPipeline(...)`, so changing the pipeline constructors has zero
in-tree call-site cost outside the two builders.

**Therefore approach B requires making those five sites deterministically dispose the pipeline.**
There is no narrower deterministic call site — the mapper is used across the whole `Wrap`/`Unwrap`
call and the pipeline is the only object holding a reference to it. Relying on `~TransformPipeline()`
would make the leak GC-bounded rather than unbounded, but would still hold the mapper's resources
for an arbitrary time **and dispose a DI `IServiceScope` from the finalizer thread** — unacceptable.

The `Reactor`/`Proactor` sites are trivial to make deterministic despite `pipeline` being `object?`,
because `TransformPipeline<T>`/`TransformPipelineAsync<T>` implement `IDisposable` non-generically
(`TransformPipeline.cs:8`, `TransformPipelineAsync.cs:8`): `try/finally { (pipeline as IDisposable)?.Dispose(); }`.

### (2) The `HasPipeline` orphans — a prerequisite, not an optional extra

`TransformPipelineBuilder.cs:151-154` and `TransformPipelineBuilderAsync.cs:151-154` are literally
`return _mapperRegistry.Get<TRequest>() != null;` — the instance is created and immediately
unreachable. All four callers are in `OutboxProducerMediator` (grep confirms zero others, including
tests):

- `MapMessage` (`:1168`) → 1 orphan + 1 owned = **2 sync mapper creations per outbound message**
- `MapMessageAsync` (`:1196`) → **2 async mapper creations per outbound message**
- `CreateRequestFromMessage` (`:505`, `:513`) → 1 async orphan; if the async probe is false, a second
  sync orphan at `:513`, then 1 owned → **2 on the async path, 3 on the sync-fallback path**

With DI defaults this is always the worst case: `ServiceCollectionMessageMapperRegistryBuilder.cs:49-51`
sets `DefaultMessageMapper = typeof(JsonMessageMapper<>)` and `:114-118` registers it `Transient`, so
`Get<TRequest>()` essentially never returns null. Without touching `HasPipeline`, approach B fixes at
best half the disposable-mapper leak and the remainder is still unbounded.

**Do NOT fix it by swapping in `ResolveMapperInfo`** — `ResolveMapperInfo(t).MapperType != null` is
**not** semantically equivalent to `Get<TRequest>() != null`. Three concrete divergences:

1. **Null-factory guard.** `Get<TRequest>()` returns `null` when `_messageMapperFactory is null`
   (`MessageMapperRegistry.cs:75-76`); `ResolveMapperInfo` (`:117-129`) never consults the factory. A
   registry with only an async factory is legal (`:64-65`) and is exactly the configuration the
   `CreateRequestFromMessage` `:505`/`:513` fallback exists to handle.
2. **Container resolvability.** `Get` returns `null` when `Create` returns null — and
   `ServiceProviderMapperFactory.Create` → `GetOrCreate` → `GetService(objectType)` returns null for a
   type not registered in the container. `ResolveMapperInfo` returns a non-null `Type` there, so
   `HasPipeline` would return `true` and `Build*Pipeline` would throw `ConfigurationException` where
   the mediator currently takes the fallback branch. **Genuine behaviour-regression risk.**
3. **The `IsGenericTypeDefinition` branch (`:125-128`) is the reverse of equivalent.** When
   `_defaultMessageMapper` is non-null but *closed* (allowed —
   `ServiceCollectionMessageMapperRegistryBuilder.cs:125-129` does not validate it),
   `ResolveMapperInfo` returns `(null, true)` → `HasPipeline` false; whereas `Get` calls
   `MakeGenericType` at `:80` unconditionally, which **throws `InvalidOperationException`**.

**Recommendation:** keep `HasPipeline`'s semantics byte-for-byte and just release the probe:
```csharp
var mapper = _mapperRegistry.Get<TRequest>();
try { return mapper is not null; }
finally { if (mapper is not null) _mapperRegistry.Release(mapper); }
```
Track the `ResolveMapperInfo` refactor (avoiding the allocation entirely, plus the double-probe on
the `CreateRequestFromMessage` path) as a **separate** change with its own behavioural analysis and
tests. This supersedes scope note (a) of bugfix 0006.

### (3) `ServiceProviderLifetimeScope.Release` needs idempotence, and the `Scoped` branch is unsafe

Exhaustive analysis of `Release` (`:133-146`):

| Lifetime | Instance | Behaviour |
|---|---|---|
| Singleton | any | `:135` returns — safe, idempotent |
| Transient | disposable, tracked | `:137-141` `TryRemove` + `scope.Dispose()` → disposed exactly once |
| Transient | disposable, **already released** | `TryRemove` misses → falls to `:144-145` → **`Dispose()` a second time** |
| Transient | non-disposable | never tracked (`:121-122`) → `TryRemove` misses → `:144` false → harmless no-op |
| **Scoped** | disposable | falls to `:144-145` → disposes the instance **while it remains cached in `_scopedInstances`** (`:103-105`, a `Lazy` never invalidated). The *next* `GetOrCreate` for that type returns a **disposed** instance, and `Dispose()` (`:159`) re-disposes it via `_scope`. |

`MapperLifetime = Scoped` is a supported configuration (`GetOrCreate`, `:79`), where a scoped mapper
is effectively a per-factory singleton. A per-message `Release` that disposes it would hand out a
disposed mapper on message #2 — **approach B would activate this latent defect.** Today only
transformers can reach it and no test covers it.

Recommended shape (this also implements review finding #2 on PR #4254):
```csharp
if (_lifetime == ServiceLifetime.Singleton) return;   // container owns it
if (_lifetime == ServiceLifetime.Scoped)   return;    // _scope owns it; disposed in Dispose()
if (instance != null && _transientScopes.TryRemove(instance, out var scope))
    scope.Dispose();                                   // disposing the scope disposes the instance, once
```
Idempotent under Transient, no-op under Scoped/Singleton — no path can double-dispose. It is a
behaviour change for `Scoped` transformers (no longer disposed on `Release`, only at factory
`Dispose`), but that is the correct semantics and no in-tree test asserts the current behaviour
(checked `FactoryLifetimeTests.cs`, `TransformerFactoryTests.cs` — the `Scoped` assertions there are
on the *handler* factory).

Pipeline side must guard too: `TransformPipeline.Dispose()` calls `GC.SuppressFinalize` (`:20`) so
the finalizer won't re-run, but an explicit double-`Dispose()` would double-release. A
`bool`/`Interlocked.Exchange` flag in `ReleaseUnmanagedResources` covers both.

### (4) Async release shape: synchronous `void`

Use `void Release(IAmAMessageMapperAsync mapper);` — mirroring the existing transformer convention
exactly: `IAmAMessageTransformerFactoryAsync.cs:48` declares `void Release(IAmAMessageTransformAsync)`;
`TransformLifetimeScopeAsync.cs:37-44` `ReleaseTrackedObjects()` is synchronous, called from
`Dispose()` (`:20-24`) and the finalizer (`:26-29`); `TransformLifetimeScopeAsync` is `IDisposable`,
not `IAsyncDisposable`. `IAmAMessageMapperFactoryAsync.Create` is already synchronous
(`IAmAMessageMapperFactoryAsync.cs:44`) and `TransformPipelineAsync.Dispose()` (`:17-21`) is
synchronous. Making async release awaitable would force `TransformPipelineAsync` to
`IAsyncDisposable`, cascading into `Reactor`/`Proactor`'s reflective `object?` handling and the
mediator — out of proportion and inconsistent with the transformer half.

### (5) Verified blast radius of the interface change — triage's "exactly 5 classes, all in `src/`" is CORRECT

`grep -rn --include="*.cs" "IAmAMessageMapperFactory"` (excluding `bin`/`obj`) → 9 files, all under `src/`:
1. `ServiceProviderMapperFactory.cs:34` — implementer
2. `ServiceProviderMapperFactoryAsync.cs:34` — implementer
3. `ControlBusMessageMapperFactory.cs:31` — implementer
4. `SimpleMessageMapperFactory.cs:34` — implementer
5. `SimpleMessageMapperFactoryAsync.cs:34` — implementer
6-7. the two interface files; 8. `MessageMapperRegistry.cs` (consumer); 9. `CommandProcessorBuilder.cs:60` (XML doc comment only).

Cases a name-grep would miss, all checked: **derived classes** — zero (no class inherits an
implementation); **mocks/test doubles/inline shims** — zero in `tests/`, `samples/`, `benchmarks/`
(C# has no anonymous interface implementation, and every `Func`-based shim goes through
`SimpleMessageMapperFactory(Func<Type, IAmAMessageMapper>)`, whose *constructor* signature is
unchanged, so those call sites keep compiling); **registry interfaces** — `MessageMapperRegistry.cs:39`
is the sole implementation of `IAmAMessageMapperRegistry`/`IAmAMessageMapperRegistryAsync`, blast
radius exactly one class; **docs/samples** — only `docs/adr/0014-di-friendly-framework.md:52` mentions
the interface in prose. TFMs are `netstandard2.0;net8.0;net9.0;net10.0` at `src/Directory.Build.props:42`,
so default interface members are indeed unavailable — consistent with the accepted decision to break
the interface.

### (6) `Simple*` / `ControlBus*` no-op `Release` is correct

`ControlBusMessageMapperFactory.Create` (`:38-53`) unconditionally `new`s one of three mappers, none
`IDisposable`, none cached — nothing to release. `SimpleMessageMapperFactory*.Create` (`:52-55`)
delegates to a user `Func<Type, IAmAMessageMapper>` whose ownership semantics are unknown to Brighter
and which may legitimately return a shared singleton — `ControlBusSenderFactory.cs:57` does exactly
that (`new SimpleMessageMapperFactory(_ => new MonitorEventMessageMapper())`) — so disposing on
release would be wrong. Mirrors `SimpleMessageTransformerFactory.cs:42`, whose `Release` is likewise
a no-op. (Contrast `EmptyMessageTransformerFactory.cs:38`, which *does* dispose — safe only because
it owns what it created.) Document the no-op in XML comments so implementers understand the contract.

### Follow-up (b) — `IAsyncDisposable`-only instances throw in `GetTransient` — FIXED, folded in

Originally recorded here as deferred. Folded into this bugfix at the user's direction once it was
shown that the candidate one-line fix was **not sufficient**.

**Confirmed.** 0006's guard is `instance is IDisposable` (`:119`); an `IAsyncDisposable`-only
instance falls to the `else` and the scope is disposed **synchronously**. MS DI's
`ServiceProviderEngineScope.Dispose()` throws `InvalidOperationException` ("only implements
IAsyncDisposable. Use DisposeAsync to dispose the container.") when it holds such a service — so an
`IAsyncDisposable`-only mapper or transform under `Transient` threw on every `Create`. Verified
directly against `Microsoft.Extensions.DependencyInjection` on net9.0, and reproduced by the
regression test below failing at `ServiceProviderLifetimeScope.cs:122`.

**Why the candidate fix was insufficient.** Widening `:119` to
`instance is IDisposable or IAsyncDisposable` only moves the instance onto the *tracked* path. The
scope it is tracked with is then disposed at `Release` (`:146`) and at factory shutdown (`:157`) —
both of which called `scope.Dispose()` and so threw the identical `InvalidOperationException`, just
later. All three call sites had to change together.

**Fix applied.** New `private static void DisposeScope(IServiceScope)` prefers `IAsyncDisposable` on
the *scope* (MS DI's scope implements it on every TFM where the instance can), falling back to
`scope.Dispose()`. It awaits the `ValueTask` synchronously — `Release` and `IDisposable.Dispose` are
both `void` by contract, so there is no async signature to reach for; the `ValueTask` completes inline
unless a user's `DisposeAsync` performs real I/O. The `netstandard2.0` `#if` lives here: that TFM has
no `Microsoft.Bcl.AsyncInterfaces` reference in
`Paramore.Brighter.Extensions.DependencyInjection.csproj`, so `IAsyncDisposable` is not a visible type
and the synchronous path is the only one available. All three `scope.Dispose()` sites — the
`GetTransient` `else`, `Release`, and `Dispose` — route through it.

> **Superseded detail.** (b) initially also widened the `GetTransient` tracking guard to
> `instance is IDisposable or IAsyncDisposable` under an `#if NETSTANDARD2_0`. Follow-up (c) below
> then replaced that guard with `instance != null` outright, so the type check — and its `#if` — no
> longer exist. `DisposeScope` and its `#if !NETSTANDARD2_0` remain, and are still required: `Release`
> and `Dispose` drain scopes that may hold `IAsyncDisposable`-only instances.

**Deliberately NOT changed:** `_scope?.Dispose()` in `Dispose()` (the *Scoped*-lifetime scope). It
has the same latent failure for an `IAsyncDisposable`-only scoped instance, but no test covers it
and it is outside the agreed scope. Recorded below as a still-open adjacent defect.

Test: `tests/Paramore.Brighter.Extensions.Tests/When_releasing_a_transient_async_disposable_only_mapper_should_dispose_it.cs`
— class `TransientAsyncDisposableMapperReleaseTests`, two facts pinning the `Release` and factory
`Dispose` sites respectively. RED (both failing at `:122`) → GREEN.

### Follow-up (c) — 0006 regression: `Transient` instances get a disposed `IServiceProvider` — FIXED

Found while verifying (b). Fixed here rather than deferred, because (b) and (c) touch the same method
and shipping (b) alone would have left the branch in a worse state than it started.

**Confirmed by bisect over this branch** — `tests/Paramore.Brighter.Validation.FluentValidation.Tests`:

| Commit | Result |
|---|---|
| `c73e93221` (before the bugfix series) | 12 passed |
| `e6a6d4aa0` | 12 passed |
| **`2d5a9c1c5`** — 0006 Part A, *"dispose transient scope immediately for non-IDisposable instances"* | **6 failed** |

```
ObjectDisposedException : Cannot access a disposed object. Object name: 'IServiceProvider'.
  at FluentValidationRequestHandler`1.Validate(TRequest) in FluentValidationRequestHandler.cs:line 52
```

**Root cause.** `FluentValidationRequestHandler<TRequest>(IServiceProvider serviceProvider)`
(`FluentValidationRequestHandler.cs:45`) captures the *scoped* provider by constructor injection and
resolves the validator from it lazily in `Validate()` (`:52`). The handler is not `IDisposable`, so
0006 Part A's `else` branch disposed its scope at `Create`, killing the captured provider before first
use. This generalises to **any `Transient` handler, mapper, or transform that holds anything from its
own scope beyond `Create`** — a far wider blast radius than the FluentValidation package.

**Why Part A was wrong in the first place.** Its premise was *"mappers have no `Release`, so dispose
their scope eagerly."* But the guard lives in `ServiceProviderLifetimeScope`, shared by all three
factories — so it also applied to *handlers*, which have had a working `Release` all along
(`ServiceProviderHandlerFactory.Release` → `ReleaseLifetimeScope` → `scope.Dispose()`). And Part B
(this bugfix) then gave mappers a `Release` too, dissolving the premise entirely. A DI scope owns more
than the instance — it owns whatever the instance captured from it — so scope lifetime must follow
*instance* lifetime, not the instance's disposability.

**Fix.** Revert Part A: `GetTransient`'s guard goes from `instance is IDisposable` back to
`instance != null`. Every transient instance is tracked; only an unresolved (null) instance has
nothing to release and disposes its scope immediately. Release coverage is now complete, which is what
makes this safe:

| Consumer | Release path | Bounded by |
|---|---|---|
| Handlers | `ServiceProviderHandlerFactory.Release` → `ReleaseLifetimeScope` | per-`IAmALifetime` (request) |
| Mappers | `TransformPipeline.cs:47`, `TransformPipelineBuilder.cs:161` → `MessageMapperRegistry.cs:123` → factory `Release` | pipeline disposal (Part B) |
| Transformers | `TransformLifetimeScope.ReleaseTrackedObjects` → factory `Release` | pipeline disposal |

**0006's regression test was rewritten**, deliberately. `TransientMapperScopeAccumulationTests`
asserted 10 `Create` calls → 10 disposals *with no `Release`* — the exact premise being reverted. It
now drives 10 `Create`+`Release` pairs → 10 disposals, preserving its intent (nothing retained between
messages) under the contract Part B established, plus a second fact
(`When_creating_a_transient_mapper_the_scope_should_survive_until_release`) pinning the other half:
the scope must *not* be disposed before `Release`.

Test: `tests/Paramore.Brighter.Extensions.Tests/When_a_transient_handler_captures_the_service_provider_should_resolve_after_create.cs`
— class `TransientHandlerCapturedProviderTests`. Reproduces the failure against
`ServiceProviderHandlerFactory` alone, with no FluentValidation dependency. RED → GREEN.

### Follow-up (d) — mapper leaked on the `Build*Pipeline` exception path — FIXED

Previously deferred (see the last line of the Verification section). Restoring full tracking in (c)
turned it from masked into live: if `BuildTransformPipeline` throws after `FindMessageMapper` has
succeeded (`TransformPipelineBuilder.cs:97-99`), no pipeline is ever constructed to take ownership of
the mapper, so its scope was retained until factory disposal.

**Fix.** Hoist `messageMapper` out of the `try` and release it in the `catch` before wrapping the
exception, at all four sites: `BuildWrapPipeline` / `BuildUnwrapPipeline` in both
`TransformPipelineBuilder` and `TransformPipelineBuilderAsync`. Releasing a mapper the pipeline has
already released is a safe no-op (`_transientScopes.TryRemove` simply misses), so the ordering is not
delicate.

Test: `tests/Paramore.Brighter.Extensions.Tests/When_building_a_pipeline_throws_should_release_the_mapper.cs`
— class `TransformPipelineBuilderFailureReleaseTests`, four facts (one per site), each driving a
mapper whose transform attribute names a type the transformer factory refuses to create, and asserting
via `ScopeTracker` that the scope was disposed. All four RED (0 disposed, expected 1) → GREEN.

### Follow-up (e) — `Scoped`-path `_scope` disposal not async-aware — FIXED

The `Scoped` sibling of (b): `Dispose()` called `_scope?.Dispose()` directly, so a scoped
`IAsyncDisposable`-only instance threw `InvalidOperationException` at factory shutdown. `Dispose()`
now routes `_scope` through `DisposeScope` too, closing the last synchronous scope-disposal site.

Test: `When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it.cs`
— class `ScopedAsyncDisposableMapperDisposalTests`. RED → GREEN.

### Follow-up (f) — stale `ResolveMapperInfo.IsDefault`, and a spurious registration conflict — FIXED

`Get`/`GetAsync` cached the *resolved default* mapper type into `_messageMappers` /
`_asyncMessageMappers` — the same dictionaries that record *explicit registrations*. Two symptoms
from the one cause:

1. **Stale `IsDefault`.** `ResolveMapperInfo`'s first branch (`:147-148`) returns `(mapperType, false)`
   for anything in `_messageMappers`, so after a single `Get` a default mapper reported
   `IsDefault: false`. `TransformPipelineBuilder.DescribeTransforms` therefore described a default
   mapper as custom — an order-dependent answer to a question that should not depend on call order.
2. **Spurious registration conflict** (not previously recorded). `Register` throws
   `ArgumentException("… already has a mapper …")` when `_messageMappers.ContainsKey` (`:186`), so
   calling `Get` before `Register` for the same request type made the registration fail.

**Fix.** Two new dictionaries, `_resolvedDefaultMappers` / `_resolvedDefaultAsyncMappers`, hold the
default-resolution cache. `Get`/`GetAsync` still consult explicit registrations first, then fall back
to the default cache, so the per-message `MakeGenericType` saving is kept. `ResolveMapperInfo` and
`ResolveAsyncMapperInfo` are unchanged — separating the caches makes their existing logic correct.

Test: `tests/Paramore.Brighter.Core.Tests/Validation/When_describing_a_default_mapper_after_use_should_still_identify_as_default.cs`
— class `MessageMapperRegistryDefaultResolutionTests`, one fact per symptom. RED → GREEN.

### Follow-up (g) — `ServiceProviderHandlerFactory` double-dispose — FIXED

`Release(handler, lifetime)` disposed the handler directly **and** then disposed the whole lifetime
scope, which disposes the instance again — via `_transientScopes` for `Transient`, via `_scope` for
`Scoped`. Follow-up (c) makes this reliably reachable for `Transient`, since every transient instance
is now tracked.

**Fix.** Drop the direct `handler.Dispose()` from both `Release` overloads. The scope owns the
instance and disposing the scope disposes it exactly once; a singleton still returns early untouched.

Test: `When_releasing_a_transient_disposable_handler_should_dispose_it_once.cs` — class
`HandlerFactoryReleaseDisposalTests`, two facts (`Transient` and `Scoped`), counting disposals rather
than latching a bool so a second `Dispose` is visible. Both RED (2, expected 1) → GREEN.

### Follow-up (h) — `Create` racing `Dispose` — FIXED

`Dispose()` drained and cleared `_transientScopes`, but `GetOrCreate` had no `_disposed` check. A
`Create` arriving after disposal tracked a scope into a dictionary nobody would ever drain again, and
handed back an instance whose factory was gone.

**Fix.** `_disposed` becomes `volatile` and is set *before* draining, so a concurrent `GetOrCreate`
sees it. `GetOrCreate` throws `ObjectDisposedException` up front; `GetTransient` re-checks after
adding to `_transientScopes` and, if a `Dispose` drained past it, removes and disposes the scope it
just tracked before throwing. That covers both the ordinary case and the interleaving.

Test: `When_creating_a_mapper_after_the_factory_is_disposed_should_throw.cs` — class
`MapperFactoryDisposedCreateTests`. RED → GREEN. **Behaviour change**: create-after-dispose used to
succeed silently and leak; it now throws. No suite depended on the old behaviour.

### Adjacent defects found — still deferred

Nothing outstanding from the original list — (b) through (h) cleared it. The remaining known gap is
the one (6) already documents: `Simple*` / `ControlBus*` `Release` is deliberately a no-op, because
those factories do not own what their user-supplied `Func` returns.

## Regression Test

`tests/Paramore.Brighter.Extensions.Tests/When_disposing_a_wrap_pipeline_should_release_the_transient_disposable_mapper.cs`
— class `TransformPipelineMapperReleaseTests`.

Drives the public API end-to-end: `ServiceProviderMapperFactory` → `MessageMapperRegistry` →
`TransformPipelineBuilder.BuildWrapPipeline<MinimalCommand>()`, with a mapper that implements
`IAmAMessageMapper<MinimalCommand>` **and** `IDisposable` under `MapperLifetime = Transient`.

Because `TransformPipeline.MessageMapper` is `protected` and the mapper is transient, the test can
never hold a reference to the instance the pipeline created. Disposal is instead made observable
through the public API by injecting a shared `MapperDisposalLog` singleton into the mapper — no
reflection, no `InternalsVisibleTo`.

Two assertions, pinning two distinct properties:
- `Assert.Equal(1, disposalsAfterPipelineDisposed)` — the mapper is released when the pipeline is
  disposed (the leak fix).
- `Assert.Equal(1, disposals.Count)` after `mapperFactory.Dispose()` — and not disposed a second
  time at factory shutdown. Per PR #4254 review finding #4 this is a literal `1`, not "unchanged
  across dispose", so it cannot pass vacuously at 0. (On its own this second assertion passes
  today; only together do the two specify "released on pipeline dispose, exactly once".)

**RED confirmed** (net9.0): fails at the first assertion, `Expected: 1 / Actual: 0` — the mapper is
disposed zero times after pipeline disposal because it is still held in `_transientScopes` awaiting
a `Release` no mapper factory can offer.

Two further behaviours from the approved scope have their own tests. Both were written after the fix
was already in place, so each was proved a genuine RED by *temporarily* reverting the single
production hunk it pins, running the test, then restoring:

**2. `tests/Paramore.Brighter.Extensions.Tests/When_checking_for_a_pipeline_should_release_the_probe_mapper.cs`**
— class `TransformPipelineBuilderProbeReleaseTests`. Calls `HasPipeline<MinimalCommand>()` ten times
and asserts ten disposals. RED against the old `return _mapperRegistry.Get<TRequest>() != null;`:
`Expected: 10 / Actual: 0` — ten probe mappers created, none released, none releasable by anything
else because no pipeline owns them.

**3. `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Post/When_posting_a_message_should_release_every_mapper_it_creates.cs`**
— class `CommandProcessorPostMapperReleaseTests`. Drives a real `CommandProcessor.Post` over
`OutboxProducerMediator` + `InMemoryOutbox` + `InMemoryMessageProducer` with a
`ReleaseTrackingMessageMapperFactory` counting `Create` against `Release`, and asserts
`CreateCount == ReleaseCount` with `CreateCount > 0`. **No `GC.Collect` is forced** — that is the
point: release must be driven by the mediator disposing the pipeline, not by `~TransformPipeline()`.

Asserting `CreateCount == ReleaseCount` rather than a literal count keeps the test honest if the
deferred `ResolveMapperInfo` refactor later removes the probe allocation entirely.

RED against the old fluent `BuildWrapPipeline<TRequest>().Wrap(...)` in `MapMessage`:
`Expected: 2 / Actual: 1` — precisely diagnostic, showing the probe mapper released by fix (2) while
the pipeline's own mapper is orphaned because the mediator never disposed it.

## Fix

Mappers now have the same release path transformers have, and the pipelines that own them are
disposed deterministically. 18 files changed.

**Release contract on the factories (breaking interface change, accepted by the user — no external
implementers expected; `netstandard2.0` rules out a default interface member)**
- `src/Paramore.Brighter/IAmAMessageMapperFactory.cs` — added `void Release(IAmAMessageMapper mapper)`
- `src/Paramore.Brighter/IAmAMessageMapperFactoryAsync.cs` — added `void Release(IAmAMessageMapperAsync mapper)`;
  synchronous, mirroring `IAmAMessageTransformerFactoryAsync.Release`
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs`,
  `ServiceProviderMapperFactoryAsync.cs` — delegate to `_lifetimeScope.Release(mapper)`
- `src/Paramore.Brighter/SimpleMessageMapperFactory.cs`, `SimpleMessageMapperFactoryAsync.cs`,
  `src/Paramore.Brighter.ServiceActivator/ControlBusMessageMapperFactory.cs` — documented no-ops
  (the caller's `Func` owns what it returns and may hand back a shared instance; the control-bus
  mappers hold no resources)

**Release path through the registry**
- `src/Paramore.Brighter/IAmAMessageMapperRegistry.cs` — added `void Release(IAmAMessageMapper)`
- `src/Paramore.Brighter/IAmAMessageMapperRegistryAsync.cs` — added `void ReleaseAsync(IAmAMessageMapperAsync)`
  (`Async` marks the async mapper variant, not an awaitable result — consistent with `GetAsync`)
- `src/Paramore.Brighter/MessageMapperRegistry.cs` — both implemented, forwarding to the matching factory

**Pipeline takes ownership of the mapper**
- `src/Paramore.Brighter/TransformPipeline.cs`, `TransformPipelineAsync.cs` — optional trailing
  `mapperRegistry` ctor parameter; `ReleaseUnmanagedResources` now releases the mapper **outside**
  `InstanceScope` (that scope only exists when a transformer factory was supplied, so releasing
  through it would miss the no-transformer case), guarded by an `Interlocked.Exchange` flag so an
  explicit `Dispose` followed by another cannot double-release
- `src/Paramore.Brighter/WrapPipeline.cs`, `UnwrapPipeline.cs`, `WrapPipelineAsync.cs`,
  `UnwrapPipelineAsync.cs` — optional trailing `mapperRegistry` parameter threaded to the base.
  Optional, so external direct constructor calls are not source-broken (there are none in-tree
  outside the two builders)
- `src/Paramore.Brighter/TransformPipelineBuilder.cs`, `TransformPipelineBuilderAsync.cs` — pass the
  registry when constructing pipelines

**`HasPipeline` orphans (scope note 2)**
- Both builders now release the probe mapper in a `finally`. Semantics kept **byte-for-byte** — the
  `ResolveMapperInfo` refactor was deliberately NOT taken, for the three divergences documented in
  the Scope Notes.

**Deterministic pipeline disposal (scope note 1)**
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — `CreateRequestFromMessage` (both branches),
  `MapMessage`, `MapMessageAsync` now `using var pipeline = …`
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs`, `Proactor.cs` — `using var pipelineLifetime =
  pipeline as IDisposable;` works despite the reflective `object?` handle, because
  `TransformPipeline<T>`/`TransformPipelineAsync<T>` implement `IDisposable` non-generically

**`ServiceProviderLifetimeScope.Release` rewrite (scope note 3)**
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs` — now returns
  early for any non-`Transient` lifetime and drains only via `TryRemove`. This removes the
  fall-through direct `Dispose()` that made a double-release double-dispose (PR #4254 review finding
  #2) and closes the `Scoped` hole where releasing disposed an instance still cached in
  `_scopedInstances`, which a per-message mapper release would otherwise have activated.

**Verification** — all three regression tests green, and every test project that exercises the core
without container infrastructure is clean on **both** net9.0 and net10.0:

| Project | net9.0 | net10.0 |
|---|---|---|
| `Paramore.Brighter.Core.Tests` | 863 passed / 7 skipped | 863 passed / 7 skipped |
| `Paramore.Brighter.Extensions.Tests` | 109 passed | 109 passed |
| `Paramore.Brighter.InMemory.Tests` | 144 passed | 144 passed |
| `Paramore.Brighter.Testing.Tests` | 85 passed | 85 passed |
| `Paramore.Brighter.AsyncAPI.Tests` | 46 passed | 46 passed |
| `Paramore.Brighter.Transforms.Adaptors.Tests` | 30 passed | 30 passed |

**Not run**: the transport/backend integration suites (Kafka, RMQ, AWS, Azure, MSSQL, PostgreSQL,
MySQL, Sqlite, DynamoDB, MongoDb, Redis, RocketMQ, MQTT, scheduler suites) — all require a container
runtime. They exercise this code path through `CommandProcessor`, so they are worth a CI run, but the
change is transport-agnostic.

Full-solution build clean apart from 32
pre-existing `CS0579` errors in an untracked stray `tests/Paramore.Brighter.RocketMQ.Tests/Paramore.Brighter.RocketMQ.Tests/obj/`
build artifact, unrelated to this change.

**Deliberately not done** (recorded in Scope Notes as separate bugs): the stale
`ResolveMapperInfo.IsDefault` flag; the `ServiceProviderHandlerFactory` double-dispose wart; and the
`_disposed` guard against `Create` racing `Dispose`. **All of these were subsequently folded in** —
see follow-ups (b) through (h) below.

---

## Follow-ups (b)–(h) — verification

Seven follow-ups across two batches. (b)–(d) were verified together because (b) and (c) touch the same
method and (c) activates (d); (e)–(h) cleared the remaining deferred list.

**Source changed**
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs` — new
  `DisposeScope` helper with the `#if !NETSTANDARD2_0` async-aware path, used at **all four**
  scope-disposal sites including the `Scoped` `_scope` (b, e); `GetTransient` guard reverted to
  `instance != null` (c); `volatile _disposed`, set before draining, with a `GetOrCreate` guard and a
  post-add re-check in `GetTransient` (h).
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderHandlerFactory.cs` — direct
  `handler.Dispose()` dropped from both `Release` overloads (g).
- `src/Paramore.Brighter/TransformPipelineBuilder.cs`,
  `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs` — mapper released in the `catch` of
  `BuildWrapPipeline` / `BuildUnwrapPipeline` (d, 4 sites).
- `src/Paramore.Brighter/MessageMapperRegistry.cs` — default-resolution cache split into
  `_resolvedDefaultMappers` / `_resolvedDefaultAsyncMappers` (f).

**Tests added**

| File | Follow-up | Facts |
|---|---|---|
| `When_releasing_a_transient_async_disposable_only_mapper_should_dispose_it.cs` | (b) | 2 |
| `When_a_transient_handler_captures_the_service_provider_should_resolve_after_create.cs` | (c) | 1 |
| `When_building_a_pipeline_throws_should_release_the_mapper.cs` | (d) | 4 |
| `When_disposing_a_factory_holding_a_scoped_async_disposable_only_mapper_should_dispose_it.cs` | (e) | 1 |
| `Validation/When_describing_a_default_mapper_after_use_should_still_identify_as_default.cs` (Core.Tests) | (f) | 2 |
| `When_releasing_a_transient_disposable_handler_should_dispose_it_once.cs` | (g) | 2 |
| `When_creating_a_mapper_after_the_factory_is_disposed_should_throw.cs` | (h) | 1 |

**Test rewritten**
- `When_creating_transient_non_disposable_mappers_the_factory_should_not_accumulate_scopes.cs` — 0006's
  regression test, moved to the `Create`+`Release` contract, plus a new survives-until-release fact.

**RED evidence**

| Follow-up | Failure before the fix |
|---|---|
| (b) | `InvalidOperationException: 'AsyncDisposableOnlyMapper' type only implements IAsyncDisposable. Use DisposeAsync to dispose the container.` at `ServiceProviderLifetimeScope.cs:122` — 2 failed |
| (c) | `ObjectDisposedException: Cannot access a disposed object.` at `HandlerCapturingServiceProvider.ResolveDependency()` — 1 failed |
| (d) | `Assert.Equal() Failure: Values differ` — 0 scopes disposed, 1 expected — 4 failed |
| (e) | same `InvalidOperationException` as (b), from the `Scoped` `_scope` — 1 failed |
| (f) | `Assert.True() Failure` on `IsDefaultMapper`; `ArgumentException` from `Register` — 2 failed |
| (g) | `Assert.Equal() Failure` — 2 disposals, 1 expected — 2 failed |
| (h) | no `ObjectDisposedException` thrown — 1 failed |

**GREEN — every container-free suite, both TFMs**

| Project | net9.0 | net10.0 |
|---|---|---|
| `Paramore.Brighter.Core.Tests` | 865 passed / 7 skipped | 865 passed / 7 skipped |
| `Paramore.Brighter.Extensions.Tests` | 121 passed | 121 passed |
| `Paramore.Brighter.InMemory.Tests` | 144 passed | 144 passed |
| `Paramore.Brighter.Testing.Tests` | 85 passed | 85 passed |
| `Paramore.Brighter.AsyncAPI.Tests` | 46 passed | 46 passed |
| `Paramore.Brighter.Transforms.Adaptors.Tests` | 30 passed | 30 passed |
| `Paramore.Brighter.Validation.FluentValidation.Tests` | **12 passed** (was 6/12) | **12 passed** |
| `Paramore.Brighter.Validation.DataAnnotations.Tests` | 10 passed | 10 passed |

`Extensions.Tests` 109 → 121; `Core.Tests` 863 → 865.

The `netstandard2.0` `#if` branch in `DisposeScope` has no test coverage (test TFMs are
`net9.0;net10.0` per `tests/Directory.Build.props:4`), so it is proven by compilation instead:
`Paramore.Brighter`, `Paramore.Brighter.Extensions.DependencyInjection` and
`Paramore.Brighter.ServiceActivator` all build clean on **all four** TFMs — `netstandard2.0`,
`net8.0`, `net9.0`, `net10.0` — with zero CS warnings.

**Not run**: transport/backend integration suites requiring a container runtime.
