# Bugfix: Transformer/Mapper factory ServiceProviderLifetimeScope never disposed under Transient lifetime (unbounded memory growth)

**Linked Issue**: #4252
**Status**: Verified

## Symptom
For an application using the .NET DI integration (`Paramore.Brighter.Extensions.DependencyInjection`) with the default `TransformerLifetime`/`MapperLifetime` of `ServiceLifetime.Transient`, memory grows without bound in proportion to the number of messages processed whenever an `IAmAMessageTransform`/`IAmAMessageTransformAsync` implementation also holds resources tracked by the DI container (both interfaces already extend `IDisposable`).

Expected: each per-message transform instance resolved from the container is fully released — including removal from the DI scope's internal tracked-disposables list — so steady-state memory is flat.

Observed: every `Create(...)` resolves from a single, never-disposed `IServiceScope`. The built-in .NET container appends each resolved `IDisposable` to that scope's internal disposables list, which is only drained when the scope is disposed. Since the scope is only disposed when the (effectively process-lifetime) factory is disposed, the list grows by one entry per message — a leak. Calling `Release(instance)` disposes the individual instance but does not remove the container's own reference from the scope's list.

Reproduction (conceptual, to be formalised in the test phase): register a disposable transform, run with `TransformerLifetime = Transient`, process many messages, observe the scope's tracked-disposables list (or heap) grow monotonically.

## Suspected Location
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:42` — single `private IServiceScope? _scope;` field for the whole lifetime-scope object.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:107-111` — `GetTransient<T>`: `_scope ??= _serviceProvider.CreateScope();` creates the scope once and reuses it for every resolution; never disposes it per call.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:118-124` — `Release(object?)`: disposes only the individual instance, not the scope.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:129-138` — `Dispose()`: only place `_scope?.Dispose()` is called.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderTransformerFactory.cs:36,46,55-58,65-68` — one `_lifetimeScope` for the factory lifetime; `Create` and `Release` delegate straight to it.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderTransformerFactoryAsync.cs:36,46,55-58,65-68` — same pattern.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs:36,46,55-58` — one `_lifetimeScope`; `Create` delegates; note there is **no** `Release` method (only `Dispose`).
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactoryAsync.cs:36,46,55-58` — same pattern, no `Release`.

Correct comparison pattern (per-unit-of-work scope, disposed):
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderHandlerFactory.cs:39` — `ConcurrentDictionary<IAmALifetime, ServiceProviderLifetimeScope> _lifetimeScopes`.
- `.../ServiceProviderHandlerFactory.cs:119-123` — `GetOrCreateLifetimeScope` creates a scope per `IAmALifetime`.
- `.../ServiceProviderHandlerFactory.cs:125-129` — `ReleaseLifetimeScope`: `_lifetimeScopes.TryRemove(lifetime, out var scope)` then `scope.Dispose()`.
- `.../ServiceProviderHandlerFactory.cs:94-117` — `Release(handler, lifetime)` calls `ReleaseLifetimeScope(lifetime)` after disposing the instance.

Supporting facts verified:
- `IAmAMessageTransform` extends `IDisposable`: `src/Paramore.Brighter/IAmAMessageTransform.cs:36`.
- `IAmAMessageTransformAsync` extends `IDisposable`: `src/Paramore.Brighter/IAmAMessageTransformAsync.cs:38`.
- `IAmAMessageMapper` does **not** extend `IDisposable`: `src/Paramore.Brighter/IAmAMessageMapper.cs:32` (`public interface IAmAMessageMapper;`).
- `IAmAMessageMapperAsync` does **not** extend `IDisposable`: `src/Paramore.Brighter/IAmAMessageMapperAsync.cs:35` (`public interface IAmAMessageMapperAsync;`).
- Effective default lifetimes are Transient: `src/Paramore.Brighter.Extensions.DependencyInjection/BrighterOptions.cs:35` (`MapperLifetime ... = ServiceLifetime.Transient`) and `:52` (`TransformerLifetime ... = ServiceLifetime.Transient`). (The factory constructors' `?? ServiceLifetime.Singleton` fallback at e.g. `ServiceProviderTransformerFactory.cs:45` only applies if `IBrighterOptions` is unresolved; with the normal registration `BrighterOptions` supplies Transient.)
- Factory ownership / lifetime: the transformer factories are constructed once during bus build and handed to the singleton `OutboxProducerMediator` — `ServiceCollectionExtensions.cs:742-743` (`TransformFactory(serviceProvider)`, `TransformFactoryAsync(serviceProvider)`), with the factory methods at `ServiceCollectionExtensions.cs:918-920` and `:930-932`. The mapper factories are constructed once into `MessageMapperRegistry` at `ServiceCollectionExtensions.cs:782-783`. Per-message use goes `TransformPipelineBuilder` → `factory.Create` (`src/Paramore.Brighter/TransformPipelineBuilder.cs:174`) and release via `TransformLifetimeScope.ReleaseTrackedObjects` → `_factory.Release(trackedItem)` (`src/Paramore.Brighter/TransformLifetimeScope.cs:37-44`), i.e. `Create`/`Release` are per-message while the factory (and its single `_scope`) lives for the process.

## Root-Cause Hypothesis
The most likely cause: `ServiceProviderLifetimeScope.GetTransient` (`ServiceProviderLifetimeScope.cs:107-111`) lazily creates exactly one `IServiceScope` (`_scope`) and reuses it for every transient resolution for the life of the factory. Because the transformer/mapper factories hold a single `ServiceProviderLifetimeScope` for their (process-lifetime) duration and never dispose it between messages, and because `Release` (`:118-124`) only disposes the individual resolved instance — which does not remove the container's own reference from the scope's internal tracked-disposables list — the scope accumulates one tracked disposable per resolution. This is a genuine unbounded leak for disposable transforms; it is latent (not yet exploitable) for mappers only because `IAmAMessageMapper`/`IAmAMessageMapperAsync` do not currently extend `IDisposable`.

Falsifiable prediction: with `TransformerLifetime = Transient` and a disposable transform, the count of entries in the shared `_scope`'s internal disposables collection (or a heap-retained-instance count) increases by exactly one per processed message and never decreases while the process runs; forcing the per-resolution scope to be disposed (or keying scopes per `IAmALifetime`) makes that count return to baseline after each message.

Issue's suggested fix — **UNVERIFIED — to be proven or refuted in /bugfix:confirm**: create and dispose a short-lived `IServiceScope` per resolution (e.g. `using var scope = _serviceProvider.CreateScope();` inside `GetTransient`), or mirror `ServiceProviderHandlerFactory` by keying transient scopes on the per-message `IAmALifetime` and disposing them via a `ReleaseLifetimeScope(lifetime)` path. Note a wrinkle to check during confirmation: a per-resolution `using` scope would dispose the transform instance immediately on return from `Create`, before the pipeline uses it — so the naive `using` variant may be incorrect; the per-`IAmALifetime` scope approach (disposed at end of the unit of work) is the closer analogue to the working handler path. Also note the mapper factories currently expose no `Release` method and their `IAmAMessageMapperFactory` interface may need review before any per-unit-of-work disposal can be wired in.

## Confirmed Root Cause
**Verdict: CONFIRMED.**

`ServiceProviderLifetimeScope.GetTransient<T>` lazily creates exactly one `IServiceScope` (`_scope ??= _serviceProvider.CreateScope()`) and reuses it for every transient/scoped resolution. Each `Create` on the transformer factory resolves an `IAmAMessageTransform` (which is `IDisposable`) from that single scope's `ServiceProvider`; the built-in DI scope tracks the resolved disposable internally. `Release` disposes the individual instance but never removes it from the scope's tracked-disposables list, and the scope is disposed only when the factory itself is disposed. Because the transformer factory is a process-lifetime singleton (held by the singleton `IAmACommandProcessor`/`OutboxProducerMediator`), the scope — and its ever-growing disposables list — lives for the whole process. Default `TransformerLifetime`/`MapperLifetime` is `Transient`, so this is the default path. Mappers are latent because `IAmAMessageMapper(Async)` is not `IDisposable`.

## Evidence
- [x] **Code-trace** (executable red repro deferred to `/bugfix:test`):
  - **Single reused scope**: `ServiceProviderLifetimeScope.cs:42` `private IServiceScope? _scope;`; `:109` `_scope ??= _serviceProvider.CreateScope();` then `:110` resolves from `_scope.ServiceProvider.GetService(...)`. `GetOrCreateScoped` (`:98-100`) also funnels through `GetTransient`, so Scoped shares the same single-scope behaviour.
  - **Lifetime branching**: `GetOrCreate<T>` switch `:70-79` → Singleton→`GetOrCreateSingleton` (cached `Lazy` off root provider `:85-90`), Scoped→`GetOrCreateScoped` (`:96-101`), Transient→`GetTransient` (`:107-111`).
  - **Release disposes only the instance**: `:118-124` — `if (_lifetime == Singleton) return; if (instance is IDisposable disposal) disposal.Dispose();` — never touches `_scope`.
  - **Only disposer of `_scope`**: `Dispose()` `:129-138` — `_scope?.Dispose();`, the sole call site.
  - **Transform interfaces are `IDisposable`**: `IAmAMessageTransform.cs:36`, `IAmAMessageTransformAsync.cs:38`. Mappers are NOT: `IAmAMessageMapperAsync.cs:35` (bare interface), `IAmAMessageMapper.cs`.
  - **Default lifetime = Transient**: `BrighterOptions.cs:52` (`TransformerLifetime`), `:35` (`MapperLifetime`), `:20` (`HandlerLifetime`).
  - **Factory holds one scope for its lifetime**: `ServiceProviderTransformerFactory.cs:36,46` one `_lifetimeScope`; `Create` `:55-58` → `GetOrCreate`; `Release` `:65-68` → `_lifetimeScope.Release`. `ServiceProviderTransformerFactoryAsync.cs` is equivalent (`:36,46,57,67`).
  - **Factory is a process-lifetime singleton**: `ServiceCollectionExtensions.cs:742-743` construct `TransformFactory`/`TransformFactoryAsync` once inside `BuildOutBoxProducerMediator` (`:726`), passed to `OutboxProducerMediator`, built by `BuildCommandProcessor` (`:681`), registered **Singleton** at `:173`.
  - **Create/Release are per-message**: `TransformPipelineBuilder.cs:174` builds each transform; `WrapPipeline.cs:62-63`/`UnwrapPipeline.cs:52-53` add each to a per-pipeline `TransformLifetimeScope`; pipeline dispose (`TransformPipeline.cs:33`) → `TransformLifetimeScope.ReleaseTrackedObjects` (`TransformLifetimeScope.cs:37-44`) calls `_factory.Release(item)` per instance. Every message resolves+"releases" a transform, but the container reference is retained in the never-disposed scope → leak.
  - **DI-container claim (the crux) — HIGH confidence, correct.** A scope's `ServiceProvider` is a `ServiceProviderEngineScope` holding an internal disposables list; any resolved `IDisposable`/`IAsyncDisposable` is captured (`CaptureDisposable`) at resolution and released ONLY by the scope's `Dispose()`/`DisposeAsync()`. Disposing the resolved instance directly does not remove it from that list; the scope keeps a strong reference until the scope is disposed. Reusing one long-lived scope across N transient resolutions retains N instances for the process lifetime — the reported unbounded growth. (Also implies a latent double-dispose: `Release` disposes the instance, then eventual scope disposal disposes it again.) Stable, documented framework behaviour.
  - **Handler-factory comparison (working pattern)**: `ServiceProviderHandlerFactory.cs:39` `ConcurrentDictionary<IAmALifetime, ServiceProviderLifetimeScope>`; `:119-123` fresh scope per `IAmALifetime`; `:125-129` `ReleaseLifetimeScope` `TryRemove` + `scope.Dispose()`; `Release` `:94-117` disposes handler AND calls `ReleaseLifetimeScope`. Handler path bounds scope lifetime; transformer/mapper path does not.

## Scope Notes
- **(a) Mapper factories have no `Release` — latent leak.** `ServiceProviderMapperFactory.cs`/`...Async.cs` expose only `Create` (`:55-58`); interfaces `IAmAMessageMapperFactory.cs:44`/`IAmAMessageMapperFactoryAsync.cs:44` have no `Release`. Every mapper is resolved from the same single reused `_scope` and never released. Latent only because mappers aren't `IDisposable`. A per-instance-scope fix for mappers has nowhere to dispose the scope (no `Release` hook) — so mapper scopes would accumulate empty `IServiceScope` objects unless the fix either avoids allocating a scope when nothing disposable is tracked, or gives mappers a release path.
- **(b) Sync vs async transformer factories are identical** (`ServiceProviderTransformerFactory.cs` ≡ `ServiceProviderTransformerFactoryAsync.cs`; async pipeline mirror `TransformPipelineBuilderAsync.cs`, `TransformLifetimeScopeAsync.cs`). The fix must apply to both transformer factories AND both mapper factories, since all four share `ServiceProviderLifetimeScope`.
- **(c) `IAmALifetime` is unavailable at the transformer `Create` call site.** `IAmAMessageTransformerFactory.Create(Type)` (`IAmAMessageTransformerFactory.cs:43`) takes only a `Type`; `TransformPipelineBuilder.cs:174` has no lifetime to key on. So the handler-factory keying strategy **cannot be reused verbatim** — see Fix note below.
- **(d) Callers.** `GetTransient` is private; reached via `GetOrCreate` Transient branch and (indirectly) the Scoped branch (`ServiceProviderLifetimeScope.cs:99`). The four transformer/mapper factories are the only consumers of a `ServiceProviderLifetimeScope` constructed with a non-Singleton lifetime for a process-lifetime factory. The handler factory also uses `ServiceProviderLifetimeScope` but scopes it per `IAmALifetime` and disposes it — it is NOT affected; the fix must not regress that working path.

### Suggested-Fix Assessment
- **Naive per-resolution `using var scope = _serviceProvider.CreateScope();` in `GetTransient`: WRONG (use-after-dispose).** The transform is used long AFTER `Create` returns — added to the pipeline's `TransformLifetimeScope` (`WrapPipeline.cs:63`/`UnwrapPipeline.cs:53`), executed across the whole wrap/unwrap run, released at pipeline dispose. A `using` scope disposed at the end of `GetTransient` disposes the container-tracked transform itself, so the pipeline would run an already-disposed transform. Triage's suspicion confirmed.
- **Per-`IAmALifetime` variant (mirror handler factory): PARTIAL / not directly applicable** — `IAmALifetime` isn't available at the transformer `Create` site (Scope Note c). A viable minimal shape: create a fresh `IServiceScope` per `Create` and dispose THAT scope in `Release` (keyed by the returned instance), so scope disposal drops the container reference and disposes the instance exactly once, while preserving instance validity until `Release`. Mapper factories need a release path (or scope-avoidance) for this to close their latent leak — see Scope Note (a). **Exact fix shape to be finalized in `/bugfix:fix`.**

## Regression Test
**Status: RED — APPROVED by user (2026-07-23).** Deterministic (no GC/WeakReference). The leak is *retention*, not skipped disposal — `Release` already disposes the instance today. A retained transient is tracked by the factory's single, never-disposed `IServiceScope` and so is disposed a **second time** when the factory is finally disposed. The test asserts `factory.Dispose()` does **not** re-dispose an already-released transient.

Test double:
- `tests/Paramore.Brighter.Extensions.Tests/TestDoubles/DisposeCountingTransform.cs` — implements both `IAmAMessageTransform` and `IAmAMessageTransformAsync`; records `DisposeCount`.

Tests (one behaviour per file):
- `tests/Paramore.Brighter.Extensions.Tests/When_releasing_a_transient_transformer_the_factory_should_not_retain_it.cs` — sync `ServiceProviderTransformerFactory` (the exploitable bug).
- `tests/Paramore.Brighter.Extensions.Tests/When_releasing_a_transient_transformer_async_the_factory_should_not_retain_it.cs` — async `ServiceProviderTransformerFactoryAsync` (Scope Note b parity).

Both fail today with `Assert.Equal() Failure: Expected: 1, Actual: 2` (released instance disposed twice — once by `Release`, once by the retained scope at factory dispose). A fix that releases the per-message scope on `Release` makes the count 1 and both pass.

**Scope Notes not covered by a red test (deliberate):**
- **Mapper factories (Scope Note a)** — the latent mapper leak is *not observable today*: `IAmAMessageMapper(Async)` is not `IDisposable` and the mapper factories expose **no `Release`** hook, so there is no per-message retention behaviour a red test can pin without first choosing the mapper fix shape (give mappers a release path vs. avoid allocating a scope when nothing disposable is tracked). That decision belongs to `/bugfix:fix`. Because the fix targets the shared `ServiceProviderLifetimeScope`, the mapper factories are affected by construction — `/bugfix:fix` must decide and cover the mapper strategy, then this note should get its own regression coverage if the chosen shape is observable.

## Fix
**File changed**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs`

`GetTransient<T>` now creates a fresh `IServiceScope` per call and stores `instance → scope` in a new `ConcurrentDictionary<object, IServiceScope> _transientScopes` (keyed by reference identity via a private `InstanceComparer`). `Release` disposes the per-instance scope — which causes the DI container to drop its tracked reference and dispose the instance exactly once — then returns, skipping the previous direct `disposal.Dispose()` call. `Dispose` drains any remaining transient scopes before disposing the shared `_scope` used for Scoped resolutions. `GetOrCreateScoped` was decoupled from `GetTransient` to avoid routing scoped instances through the new per-call scope path.
