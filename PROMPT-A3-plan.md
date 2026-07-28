# A#3 implementation plan (design LOCKED with user, 2026-07-28)

B#2 committed `67e4c5973`. Now A#3. Branch `memory-leak`.

## Locked design (AskUserQuestion answered)
- **Lease = `sealed class Lease<T> where T : class`** (core, `src/Paramore.Brighter/Lease.cs`): opaque DATA only.
  `public Lease(T instance, object? releaseToken = null)`; `public T Instance {get;}`;
  `public object? ReleaseToken {get;}`. Public token (no InternalsVisibleTo core→DI). Reference type; `null`
  return = not-found. Add `public static implicit operator Lease<T>(T instance) => new(instance);` to ease
  no-DI test construction.
- **`Create`/`Get` return the lease directly.** `factory.Release(lease)` / `ReleaseAsync(lease)` do the release
  (Release stays on the interfaces). All DI-specific disposal stays in the DI layer.

## Interface changes (6 files, src/Paramore.Brighter)
- `IAmAMessageMapperFactory`: `Lease<IAmAMessageMapper>? Create(Type)`; `void Release(Lease<IAmAMessageMapper>)`.
- `IAmAMessageMapperFactoryAsync`: `Lease<IAmAMessageMapperAsync>? Create(Type)`; `void Release(Lease<IAmAMessageMapperAsync>)`; `ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>)`.
- `IAmAMessageTransformerFactory`: `Lease<IAmAMessageTransform>? Create(Type)`; `void Release(Lease<IAmAMessageTransform>)`.
- `IAmAMessageTransformerFactoryAsync`: `Lease<IAmAMessageTransformAsync>? Create(Type)`; `void Release(Lease<IAmAMessageTransformAsync>)`; `ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>)`.
- `IAmAMessageMapperRegistry`: `Lease<IAmAMessageMapper<T>>? Get<T>()`; `void Release<T>(Lease<IAmAMessageMapper<T>>) where T:class,IRequest`. (Generic Release<T> symmetric w/ Get<T>; the lease's generic arg makes the round-14 dual-interface misroute a compile error by TYPE now, so these can be plain public — the type system disambiguates on the concrete registry. Keep explicit-interface if simpler, but plain public is fine.)
- `IAmAMessageMapperRegistryAsync`: `Lease<IAmAMessageMapperAsync<T>>? GetAsync<T>()`; `void Release<T>(Lease<IAmAMessageMapperAsync<T>>)`; `ValueTask ReleaseAsync<T>(Lease<IAmAMessageMapperAsync<T>>)`.

## Core rewrite — `ServiceProviderLifetimeScope` (DI)
Replace instance-keyed `_transientScopes` stack + `InstanceComparer` + `CollectScopesToRelease` re-home with a
**set of outstanding scopes** keyed by the scope reference:
`ConcurrentDictionary<IServiceScope, byte> _outstandingScopes = new();` (default ref-equality comparer).
- `GetOrCreate<T>(Type) => GetOrCreate<T>(Type, out _)` overload (handler factory path — UNCHANGED callers).
- `GetOrCreate<T>(Type objectType, out object? releaseToken)`: Singleton/Scoped/TransientShared → token=null.
  Transient(isolate) → `GetTransient(objectType, out releaseToken)`.
- `GetTransient<T>(Type, out object? token)`: create scope; resolve; on throw/null dispose scope, token=null;
  else `_outstandingScopes.TryAdd(scope,0)`; disposed-recheck: if `_disposed`, TryRemove+dispose that scope,
  throw; `token = scope`; return instance.
- `Release(object? token)`: `if (token is IServiceScope s && _outstandingScopes.TryRemove(s, out _)) DisposeScope(s);`
  — **TryRemove makes over-release an idempotent no-op** (RED-test requirement). Async twin `ReleaseAsync`.
- `Dispose()`: drain `_outstandingScopes.Keys` (per-scope try/catch + Log.FailedToDisposeScope), then root `_scope`.
- DELETE: InstanceComparer, CollectScopesToRelease, the GetTransient stacking/disposed-recheck-drain essay.
- KEEP: EnsureRootScopePublished, GetTransientShared (flag path), DisposeScope context-suppression, Disposed().

## DI factory impls (4) — Extensions.DependencyInjection
Each `Create` → `var i = _lifetimeScope.GetOrCreate<IFace>(type, out var token); return i is null ? null : new Lease<IFace>(i, token);`
Each `Release(Lease<IFace> lease)` → `_lifetimeScope.Release(lease.ReleaseToken);` Async twin uses `ReleaseAsync(lease.ReleaseToken)`.
Handler factory: UNCHANGED (uses parameterless GetOrCreate overload; never per-instance Release).

## MessageMapperRegistry
- `Get<T>`: `var lease = _messageMapperFactory.Create(type)` → `Lease<IAmAMessageMapper>?`. null→null. Cast-check
  `lease.Instance`: wrong type → `_messageMapperFactory.Release(lease)` then throw InvalidCast. Match → return
  `new Lease<IAmAMessageMapper<T>>((IAmAMessageMapper<T>)lease.Instance, lease.ReleaseToken)`.
- `Release<T>(Lease<IAmAMessageMapper<T>> lease)`: `_messageMapperFactory?.Release(new Lease<IAmAMessageMapper>(lease.Instance, lease.ReleaseToken));`
- Async twins. Keep the ConfigException/null-factory guards + ResolveMapperInfo (UNCHANGED).

## No-op factories (4): SimpleMessageMapperFactory[Async], EmptyMessageTransformerFactory[Async]
`Create` returns `new Lease<IFace>(_factoryMethod(type))` (null token). `Release(Lease<IFace>)` = no-op. ReleaseAsync = default.

## Transform lease plumbing (the big test ripple)
- `TransformerFactory<T>.CreateMessageTransformer()` → returns `Lease<IAmAMessageTransform>` (create returns lease;
  on init-throw `factory.Release(lease)`). Async twin `Lease<IAmAMessageTransformAsync>`.
- `TransformLifetimeScope[Async]`: track `List<Lease<IAmAMessageTransform[Async]>>`; `Add(Lease<...>)`;
  drain calls `factory.Release(lease)` / `ReleaseAsync(lease)`. (KEEP B#2 per-item try/catch + AggregateException.)
- Pipelines `TransformPipeline[Async]<T>` base + Wrap/Unwrap[Async]: ctors take
  `Lease<IAmAMessageMapper[Async]<T>> mapperLease` + `IEnumerable<Lease<IAmAMessageTransform[Async]>> transformLeases`.
  Store `MapperLease`; `MessageMapper => MapperLease.Instance`; `Transforms` = materialized `.Instance` list.
  Release: `mapperRegistry?.Release(MapperLease)` / InstanceScope disposes transform leases.
- Builders `TransformPipelineBuilder[Async]`: `FindMessageMapper` returns the lease; `BuildTransformPipeline`
  returns `List<Lease<...>>`; `CleanUpAfterFailedBuild` releases mapper lease + transform leases.

## Callers to check: pump (Reactor/Proactor) + OutboxProducerMediator release the PIPELINE (IDisposable), not
mapper/transform directly → likely UNAFFECTED. Verify no direct registry.Release(instance) calls remain.

## Test doubles (large): every impl of the 6 interfaces across Core.Tests + Extensions.Tests + others. Sweep by
compile errors after production is green. Also every test that `new`s a WrapPipeline/UnwrapPipeline[Async] with a
bare mapper + bare transforms (implicit op covers the mapper; transform lists need `.Select(t => (Lease<..>)t)`).

## RED test (bugfix 0016): two resolutions of a SHARED instance (Singleton-registered mapper under default
Transient MapperLifetime) → release FIRST lease → SECOND resolution's instance/scope stays usable; over-release of
a lease is an idempotent no-op. Drive `ServiceProviderLifetimeScope`/factory directly. RED-prove by reverting to
instance-keyed stack.

## Docs: release_notes.md + PR body — replace "release-exactly-once / over-release hazard / narrowed-not-closed"
with the lease contract; note the shared-instance bug class is designed out. Reply to bot supersedes deferrals.

## Sequence: Lease.cs → interfaces → scope rewrite → DI factories → registry → no-op → transformer helper →
pipelines/builders → compile core+DI+ServiceActivator → test-double sweep → RED test → docs → commit.

---
## STATUS (resume point)
**PRODUCTION COMPLETE + GREEN** all 4 src projects (core, Extensions.DI, ServiceActivator, +SA.Extensions.DI)
build clean net9.0. Committed as WIP checkpoint. Files done: Lease.cs (new), 6 interfaces,
ServiceProviderLifetimeScope (set-based `_outstandingScopes`, out-token GetOrCreate + parameterless overload,
Release(token) idempotent TryRemove, Dispose drain, InstanceComparer/CollectScopesToRelease DELETED), 4 DI
factories, MessageMapperRegistry (Get/GetAsync return leases, Release<T>/ReleaseAsync<T> generic public),
6 no-op factories (Simple+Empty mapper/transform sync/async, ControlBusMessageMapperFactory),
TransformerFactory[Async] (return lease), TransformLifetimeScope[Async] (track leases, Add(Lease), KEEP B#2
AggregateException), TransformPipeline[Async] base (MapperLease + TransformLeases + materialised Transforms;
explicit ctor, _mapperRegistry field), Wrap/Unwrap[Async] pipelines (lease ctors), both builders
(FindMessageMapper→lease, BuildTransformPipeline→List<Lease>, ReleaseTransforms/CleanUpAfterFailedBuild→leases),
IAmATransformLifetime[Async].Add(Lease).

**REMAINING: test-double sweep** (~31 files; all CS0535/CS0738). Mechanical contract per test double:
- transformer factory: `Lease<IAmAMessageTransform>? Create(Type)` → `new Lease<..>(instance)`; `Release(Lease<..> lease)` body uses `lease.Instance`; async adds `ReleaseAsync(Lease<..> lease)`.
- mapper factory: same shape with IAmAMessageMapper[Async].
- registry double: `Lease<IAmAMessageMapper<T>>? Get<T>()`/`GetAsync`; `Release<T>(Lease<IAmAMessageMapper<T>>)` generic (+ async `ReleaseAsync<T>`). NO more explicit-interface Release (now type-disambiguated).
- Doubles tracking Created/Released instance lists: keep lists as instances, populate via `lease.Instance` — preserves identity assertions.
- Direct pipeline construction `new WrapPipeline<T>(mapper, factory, transforms, ...)`: mapper auto-converts via `implicit operator Lease<T>(T)`; the transforms `IEnumerable<IAmAMessageTransform>` must become `IEnumerable<Lease<IAmAMessageTransform>>` — wrap each: `.Select(t => new Lease<IAmAMessageTransform>(t))` or a `Lease<..>[]`.
Files done so far: When_Wrapping_Clean_Up_The_Pipeline[.Async]. Remaining list = run
`dotnet build tests/Paramore.Brighter.Core.Tests -f net9.0 2>&1 | grep ': error CS' | sed -E 's/\(.*//' | sort -u`
then Extensions.Tests. **SEMANTIC WATCH:** `When_releasing_a_dual_interface_mapper_via_the_registry` (round-14
protection is now TYPE-based, not explicit-interface — rewrite to assert lease-typed routing); the two
`When_a_transform_release_throws...`/`..._scope_disposal...` already B#2-updated need lease-double updates only.

**THEN:** RED test bugfix 0016 (shared instance, two resolutions, release first lease → second usable;
over-release no-op — drive ServiceProviderLifetimeScope/factory directly). Docs: release_notes.md + PR body.
Commit final. Reply to bot superseding B#2/A#3 deferrals.
