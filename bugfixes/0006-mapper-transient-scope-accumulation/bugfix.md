# Bugfix: Mapper factory `_transientScopes` accumulates without bound after PR #4254 fix

**Linked Issue**: #4254 (reviewer blocking comment — regression introduced by the original fix)
**Status**: Verified

## Symptom
After the fix for #4252 landed (PR #4254), `ServiceProviderLifetimeScope.GetTransient<T>` creates a
fresh `IServiceScope` per call and stores `instance → scope` in the new `_transientScopes`
`ConcurrentDictionary`. `Release` removes the entry and disposes the scope — **but mapper factories
expose no `Release` method**, so their transient scopes are never removed.

`MessageMapperRegistry.Get<TRequest>()` calls `_messageMapperFactory.Create(...)` on every message
(it caches the mapper *type*, not the instance — `MessageMapperRegistry.cs:87,109`). With the
default `MapperLifetime = Transient` (`BrighterOptions.cs:35`), every message adds one `IServiceScope`
to `_transientScopes` that is never removed until the factory is disposed at process shutdown.

Net effect: **every processed message now permanently retains one `IServiceScope` + strong key reference
in `_transientScopes`**, cleared only at process shutdown. Before PR #4254, mapper factories reused a
single `_scope` and — because `IAmAMessageMapper(Async)` is not `IDisposable` — retained nothing per
message. This is a genuine regression introduced by the PR fix.

## Suspected Location
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:117-118`
  — `_transientScopes[instance] = scope` stores every non-null instance unconditionally, with no
  check for `IDisposable`.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs`
  — exposes only `Create` + `Dispose`; no `Release` method.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactoryAsync.cs`
  — same; no `Release` method.
- `src/Paramore.Brighter/IAmAMessageMapperFactory.cs`
  — interface has no `Release` declaration.
- `src/Paramore.Brighter/IAmAMessageMapperFactoryAsync.cs`
  — interface has no `Release` declaration.

## Root-Cause Hypothesis
`ServiceProviderLifetimeScope.GetTransient<T>` (`:113-122`) stores every resolved non-null
instance in `_transientScopes[instance] = scope` regardless of whether the instance is
`IDisposable`. `Release` only drains an entry if `_transientScopes.TryRemove(instance, ...)` finds
it (`:135-140`). Since `ServiceProviderMapperFactory` / `ServiceProviderMapperFactoryAsync` have no
`Release` method and their callers (`MessageMapperRegistry`) never release mapper instances, the
`_transientScopes` dictionary grows by one entry per message and is only cleared at factory dispose.

The two-part fix agreed with the user:
- **Part A (immediate guard)**: in `GetTransient`, only add to `_transientScopes` when `instance is
  IDisposable`; otherwise dispose the scope immediately and return the instance untracked. Non-disposable
  mappers then create-and-destroy a scope per message, retaining nothing.
- **Part B (symmetry)**: add `Release` to `IAmAMessageMapperFactory` / `IAmAMessageMapperFactoryAsync`
  and all implementations (`ServiceProviderMapperFactory`, `ServiceProviderMapperFactoryAsync`,
  `SimpleMessageMapperFactory`, `SimpleMessageMapperFactoryAsync`,
  `ControlBusMessageMapperFactory`), plus `Release<TRequest>` / `ReleaseAsync<TRequest>` on
  `MessageMapperRegistry` so callers have a release path.

## Confirmed Root Cause
**Verdict: CONFIRMED.**

`ServiceProviderLifetimeScope.GetTransient<T>` (`ServiceProviderLifetimeScope.cs:113-122`) stores
every non-null resolved instance in `_transientScopes[instance] = scope` with no guard for whether
the instance is `IDisposable`. Because `IAmAMessageMapperFactory` / `IAmAMessageMapperFactoryAsync`
have no `Release` method, and `MessageMapperRegistry` never calls one, `ServiceProviderLifetimeScope.Release`
is never invoked for mapper instances. Consequently, `_transientScopes` accumulates one `IServiceScope`
per mapper creation call — one per message processed — and those entries are only disposed when the
factory itself is `Dispose`d at process shutdown.

## Evidence
- [x] **Code-trace** (all assertions code-traced, no live infra required):
  - **Unconditional store**: `ServiceProviderLifetimeScope.cs:117-118` — `_transientScopes[instance] = scope`
    with only a null guard; no `is IDisposable` check. `else` at line 119-120 disposes the scope only when
    `GetService` returns null.
  - **`Release` drains only via `TryRemove`**: `ServiceProviderLifetimeScope.cs:135-139` — `_transientScopes.TryRemove(instance, out var scope)` is the sole removal path; never invoked for mappers.
  - **Mapper factory has no `Release`**: `ServiceProviderMapperFactory.cs:55-67` — only `Create` and `Dispose`. `ServiceProviderMapperFactoryAsync.cs:55-67` — identical. Contrast with `ServiceProviderTransformerFactory.cs:65-68` which does implement `Release`.
  - **Interfaces have no `Release`**: `IAmAMessageMapperFactory.cs:37-45` — only `Create`. `IAmAMessageMapperFactoryAsync.cs:37-45` — only `Create`.
  - **`MessageMapperRegistry` calls `Create` every message**: `MessageMapperRegistry.cs:87,109` — type dict lookup then `_messageMapperFactory.Create(mapperType)` per call; no instance cache.
  - **Default lifetime = Transient**: `BrighterOptions.cs:35` — `MapperLifetime = ServiceLifetime.Transient`.
  - **`MessageMapperRegistry` never calls `Release`**: grep returns zero hits.
  - **`Simple*` and `ControlBus*` factories have no `Release`**: `SimpleMessageMapperFactory.cs`, `SimpleMessageMapperFactoryAsync.cs`, `ControlBusMessageMapperFactory.cs` — only `Create`.
  - **Handler factory (working comparison)**: `ServiceProviderHandlerFactory.cs:94-117` implements `Release(handler, lifetime)` and drains via `ReleaseLifetimeScope`. The mapper pipeline uniquely omits this release path.
  - **`IAmAMessageMapper` is not `IDisposable`**: `IAmAMessageMapper.cs:32` — bare interface, no base. No in-tree mapper implements `IDisposable`.

## Scope Notes
- **(a) `HasPipeline` leak** — `TransformPipelineBuilder.cs:153` and `TransformPipelineBuilderAsync.cs:153`
  call `_mapperRegistry.Get<TRequest>()` / `GetAsync<TRequest>()` solely for a null check, creating and
  discarding a mapper instance each time (and leaking a scope before Part A). `OutboxProducerMediator.cs:505,513,1168,1196`
  calls `HasPipeline` on every send/receive path — at least two extra leaked scopes per message.
  `MessageMapperRegistry.ResolveMapperInfo(Type)` and `ResolveAsyncMapperInfo(Type)` (lines 117-148)
  already exist for type-only lookup; `HasPipeline` should use these instead. Part A fixes the symptom
  for non-disposable mappers; the refactor avoids the allocation entirely.
- **(b) Disposable mappers — latent gap without Part B** — Part A (IDisposable guard) fully fixes the
  regression for current mappers (which are non-disposable). But if a user-defined mapper implements
  `IDisposable`, Part A still stores it in `_transientScopes` and without Part B that scope accumulates
  until factory shutdown. Part B closes this gap by giving mappers a release path.
- **(c) Singleton/Scoped branches not affected** — `GetOrCreateSingleton` and `GetOrCreateScoped` do
  not touch `_transientScopes`; the accumulation is Transient-only.
- **(d) Fallback default inconsistency** — `ServiceProviderMapperFactory.cs:45` and `...Async.cs:45`
  fall back to `ServiceLifetime.Singleton` when `IBrighterOptions` is absent, but the configured
  default in `BrighterOptions.cs:35` is `Transient`. The leak does not manifest in tests that use the
  factory directly without registering options.

## Regression Test

`tests/Paramore.Brighter.Extensions.Tests/When_creating_transient_non_disposable_mappers_the_factory_should_not_accumulate_scopes.cs`

Uses a `TrackingServiceProvider` that intercepts `IServiceScopeFactory` to count scope disposals
without reflection. After 10 `Create` calls on a non-`IDisposable` transient mapper:
- **Before fix**: `disposedAfterCreates == 0` (scopes pile up in `_transientScopes`)
- **After Part A fix**: `disposedAfterCreates == 10` (each scope disposed immediately)

Both net9.0 and net10.0 fail as expected.

## Fix

**File changed**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs`

> ⚠️ **Part A was later REVERTED.** See follow-up (c) in
> `bugfixes/0007-mapper-factory-release-disposable-mapper/bugfix.md`. The guard below caused a
> regression: it also applied to *handlers* and *transforms*, disposing the DI scope of any
> non-`IDisposable` `Transient` instance at `Create` — which breaks anything that captured something
> from that scope, such as an injected `IServiceProvider`
> (`ObjectDisposedException` in `FluentValidationRequestHandler`). Part A's premise ("mappers have no
> `Release`") was dissolved by Part B, which gave mappers a release path; `GetTransient` is now back to
> `instance != null` and the accumulation this bugfix reports is prevented by `Release` instead.

**Change**: In `GetTransient<T>` (line 117), replaced `if (instance != null)` with `if (instance is IDisposable)`.

Non-disposable instances now have their scope disposed immediately rather than accumulated in
`_transientScopes`. The `is IDisposable` pattern also implicitly handles the `null` case
(null is not IDisposable), so behaviour for unresolved types is unchanged.
