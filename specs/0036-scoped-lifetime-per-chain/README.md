# Scoped Lifetime Per Chain

**Created:** 2026-07-30
**Tracking issue:** [BrighterCommand/Brighter#4256](https://github.com/BrighterCommand/Brighter/issues/4256)
**Branch:** `spec/scoped-lifetime-per-chain`

## Summary

`Scoped` does not mean the same thing across Brighter's factories. A `Scoped` handler already gets a
per-chain scope that is disposed when the chain completes, but `ServiceProviderMapperFactory` and
`ServiceProviderTransformerFactory[Async]` each hold **one** `ServiceProviderLifetimeScope` created in
their constructor (`ServiceProviderMapperFactory.cs:46`) and cache every instance by type for the
factory's whole life (`ServiceProviderLifetimeScope.cs:167`). Those factories are built once for the
singleton `Dispatcher` and once for the singleton `OutboxProducerMediator`, so `MapperLifetime.Scoped`
silently means *process lifetime*.

Separately, no chain ever adopts a caller-owned scope: `IAmACommandProcessor` is a singleton whose
handler factory captures the **root** provider, so under ASP.NET a `Scoped` handler is resolved from a
fresh child of root rather than from `HttpContext.RequestServices`.

This spec makes the scope unit explicit — **the chain** — and adds opt-in adoption of a caller's scope.

## Direction (agreed on the issue)

1. **The chain is the scope unit.** A handler chain (one `Send`, or one subscriber of a `Publish`) and
   a transform chain (a mapper plus its transforms, for one message) each take a scope and release it
   when the chain completes. The message is the *bound*, not the scope.
2. **Mapper/transform factories scope per transform chain**, using the mechanism
   `ServiceProviderHandlerFactory` already applies to `IAmALifetime`. The pump needs no change: it
   already builds and releases the pipeline per message.
3. **Opt-in adoption of a caller-owned scope**, configured in the producer context. Default is
   unchanged. `Publish` always creates a new scope per subscriber even under an ambient (ADR 0039).
   Brighter never disposes a scope it borrowed.
4. **Container-agnostic seam** in `Paramore.Brighter` (`IAmAScope` / `IAmAScopeProvider`, names
   provisional) implemented by the DI package, so the ambient stays private to that package and the
   change is additive — no signature changes to the public factory interfaces, which matters on
   `netstandard2.0`.
5. **The pump does not publish a per-message ambient** — decided; see
   [issue comment](https://github.com/BrighterCommand/Brighter/issues/4256#issuecomment-5124807305).

## Status

- [ ] Requirements (`/spec:requirements`)
- [ ] Design (`/spec:design`)
- [ ] Tasks (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`, interactive TDD via `/test-first`)
- [ ] PR

## Open questions for requirements

1. How is the ambient supplied under ASP.NET — an explicit `UseBrighterScope()` middleware, or an
   `IHttpContextAccessor`-backed provider (which pulls an ASP.NET dependency into the DI package or
   needs a third package)?
2. What does the opt-in look like on the options object, and what is it called?
3. Naming — `IAmAScope` / `IAmAScopeProvider` / `ScopeAffinity` are placeholders, and `IAmAScope` sits
   uncomfortably close to the existing `IAmALifetime`.
4. Does the opt-in apply to transform chains as well as handler chains? One rule says yes, which would
   mean two `Post`s in one HTTP request share the request scope.

## Notes

- Prior art: [ADR 0033](../../docs/adr/0033-lifetime-of-command-processor-and-mediator.md) (CommandProcessor
  is a singleton), [ADR 0039](../../docs/adr/0039-scoping-dependencies-inline-with-lifetime-scope.md)
  (per-subscriber lifetime scope — preserved by this design),
  [ADR 0066](../../docs/adr/0066-release-factory-instances-on-an-opaque-lease.md),
  [ADR 0067](../../docs/adr/0067-per-resolution-di-scope-for-transient-factory-instances.md) (the
  `Transient` per-resolution scope fix this builds on).
- Constraint verified by spike: Microsoft's DI scopes **do not nest**. `IServiceScopeFactory` resolved
  from a scoped provider is the same instance as from root, and creating a scope from it yields a
  fresh root-parented scope. So adopting a scope can only mean *resolving from the caller's provider*,
  and a per-subscriber scope cannot be nested inside a per-message one.
- Implementation detail not to lose: the failed-build path (`CleanUpAfterFailedBuild`,
  `TransformPipelineBuilderAsync.cs:122`, `:163`) must release the chain scope, or a mapper with a
  misconfigured dependency leaks one scope per attempt.
- `PublishAsync` builds every subscriber's chain before running any of them, then runs them
  concurrently (`CommandProcessor.cs:589-597`). Any ambient must be established inside each
  subscriber's own async flow around `HandleAsync`. Drive this with a test first.
