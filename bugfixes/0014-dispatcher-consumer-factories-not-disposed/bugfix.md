# Bugfix: The consumer-side Dispatcher never disposes the mapper registry and transform factories it owns

**Linked Issue**: PR #4254 review, Finding #1 (comment 5101242116)
**Status**: Fixed

## Symptom
The PR's "deterministic factory disposal at host shutdown" work made the four IoC-backed factories
"owned by the objects that use them; those owners now dispose them." That holds on the **producer** path —
`OutboxProducerMediator` disposes its registry and both transform factories — but not on the **consumer**
path.

`ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` `BuildDispatcher` news a
fresh `MessageMapperRegistry` plus **both** transform factories for the `Dispatcher`, hands them to
`DispatchBuilder.MessageMappers(...)`, and registers none of them in the container. But `Dispatcher` was
not `IDisposable`, and nothing else can reach those three objects. So the consumer-side `_transientScopes`,
the root `_scope` under `MapperLifetime.Scoped`, and both transform factories were retained for the process
lifetime — the same gap the producer-side disposal closes, on the side that actually runs the hot unwrap
path per message.

Per-message `Release` bounds the growth, so this is not the unbounded leak of #4252, but the release-note
claim that the container disposes all these owners was false for the Dispatcher.

## Suspected / Confirmed Location
`src/Paramore.Brighter.ServiceActivator/Dispatcher.cs` — the class held the registry and transform
factories but implemented no disposal. It is registered as a container singleton
(`ServiceCollectionExtensions.cs`, `TryAddSingleton<IDispatcher>(BuildDispatcher, Singleton)`), so the
container will dispose it at shutdown once it is `IDisposable`.

## Fix
Make `Dispatcher : IDisposable`, mirroring `OutboxProducerMediator.Dispose`:
- Claim `_disposed` up front with a single atomic `Interlocked.Exchange` so the body runs at most once even
  under a concurrent application-level dispose racing the container's.
- Dispose each owned factory independently through a `DisposeQuietly` helper (logs
  `FailedToDisposeOwnedResource`) so one factory's fault cannot skip the rest.
- Disposing the registry cascades to the two mapper factories it holds. On the DI path the sync and async
  registry are the same `MessageMapperRegistry` instance, so the async disposal is guarded on reference
  identity to avoid a redundant second dispose (the registry's own `Dispose` is idempotent regardless).

## RED proof
`tests/Paramore.Brighter.Core.Tests/MessageDispatch/When_disposing_the_dispatcher_it_disposes_its_factories.cs`
(`DispatcherDisposalTests`, 2 facts): a Dispatcher built with dispose-counting mapper/transform factories is
disposed (and disposed twice for the idempotency fact); each factory must be disposed exactly once. Proven
RED by neutralizing the `DisposeQuietly` cascade — both facts fail with `DisposeCount == 0`; GREEN once the
cascade runs.
