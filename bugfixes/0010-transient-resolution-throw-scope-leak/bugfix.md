# Bugfix: A throwing transient resolution orphans its scope

**Linked Issue**: PR #4254 round-9 review — item 1
**Status**: Verified

## Symptom
`ServiceProviderLifetimeScope.GetTransient<T>` creates a fresh `IServiceScope`, then resolves the
instance from it:

```csharp
var scope = _serviceProvider.CreateScope();
var instance = (T?)scope.ServiceProvider.GetService(objectType);   // can throw
```

If `GetService` throws — or the `(T?)` cast throws `InvalidCastException` — the scope has been
created but not yet pushed onto `_transientScopes` (the push happens two lines later). The
exception escapes with the scope neither tracked nor disposed. Nothing can reclaim it: `Release`
has no key to find it, and `Dispose` only drains `_transientScopes` and `_scope`.
`ServiceProviderEngineScope` has no finalizer, so any disposable dependency the container already
resolved into that scope before the failure leaks undisposed, permanently.

`GetService` **throws** (it does not return null) for the most common DI misconfiguration there
is — the mapper/transform/handler is registered but a constructor dependency is not:

```csharp
services.AddTransient<MyMapper>();   // MyMapper(ISomethingUnregistered dep)
// -> InvalidOperationException: Unable to resolve service for type ... while attempting to activate MyMapper
```

`MapperLifetime`/`TransformerLifetime` default to `Transient` and those factories are
process-lifetime singletons, so a Proactor pumping against a misconfigured mapper leaks one scope
per message — this PR's own failure mode, on the failure path. It also reaches the handler factory
(default `HandlerLifetime = Transient`), where the orphan survives `ReleaseLifetimeScope`.

Expected: a failed resolution disposes the scope it created before the exception propagates, so
`CreatedCount == DisposedCount` on the scope tracker.

## Suspected Location
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderLifetimeScope.cs:166-167` —
  `CreateScope()` then `GetService(objectType)`, the throwing gap before the scope is tracked.
- `.../ServiceProviderLifetimeScope.cs:168-172` — the existing null-branch (`DisposeScope(scope)`),
  the pattern the fix mirrors.

## Root-Cause Hypothesis (confirmed)
The scope is created before resolution, but tracked only after it. Any throw from resolution lands
in the gap, leaving the scope reachable by nothing. This is **new with this PR**: on `master`,
`GetTransient` reused one shared `_scope` (`_scope ??= CreateScope()`), so a throwing resolution
created nothing extra to leak.

## Fix
Wrap the resolve in `try { ... } catch { DisposeScope(scope); throw; }`, mirroring the existing
null branch — dispose the scope, then rethrow so the caller still sees the original
misconfiguration exception. Applied at `ServiceProviderLifetimeScope.GetTransient<T>`.

## Test
`tests/Paramore.Brighter.Extensions.Tests/When_a_transient_mapper_resolution_throws_it_should_not_leak_a_scope.cs`
— registers a mapper whose constructor dependency is unregistered so the container throws while
activating it, drives it through the real `ServiceProviderMapperFactory.Create` → `GetTransient`,
and asserts the resolution threw, one scope was created, and `CreatedCount == DisposedCount`.

Proven RED against the pre-fix code: `created=1, disposed=0` (the leaked scope). GREEN after the
`try/catch`. Regression-clean: `Paramore.Brighter.Extensions.Tests` 131/131 on both net9.0 and
net10.0; production build clean on all TFMs including netstandard2.0.

## Base commit
Branch `memory-leak`, HEAD `6190d0b8e` at time of fix.
