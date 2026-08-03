# Scoped Lifetime Per Pipeline

**Created:** 2026-07-30
**Tracking issue:** [BrighterCommand/Brighter#4256](https://github.com/BrighterCommand/Brighter/issues/4256)
**Branch:** `spec/scoped-lifetime-per-pipeline`

## Summary

`Scoped` does not mean the same thing across Brighter's factories. A `Scoped` handler already gets a
per-pipeline scope that is disposed when the pipeline completes, but `ServiceProviderMapperFactory` and
`ServiceProviderTransformerFactory[Async]` each hold **one** `ServiceProviderLifetimeScope` created in
their constructor (`ServiceProviderMapperFactory.cs:46`) and cache every instance by type for the
factory's whole life (`ServiceProviderLifetimeScope.cs:167`). Those factories are built once for the
singleton `Dispatcher` and once for the singleton `OutboxProducerMediator`, so `MapperLifetime.Scoped`
silently means *process lifetime*.

Separately, no pipeline ever adopts a caller-owned scope: `IAmACommandProcessor` is a singleton whose
handler factory captures the **root** provider, so under ASP.NET a `Scoped` handler is resolved from a
fresh child of root rather than from `HttpContext.RequestServices`.

This spec makes the scope unit explicit — **the pipeline** — and adds opt-in adoption of a caller's scope.

## Direction (agreed on the issue)

1. **The pipeline is the scope unit.** A handler pipeline (one `Send`, or one subscriber of a `Publish`) and
   a transform pipeline (a mapper plus its transforms, for one message) each take a scope and release it
   when the pipeline completes. The message is the *bound*, not the scope.
2. **Mapper/transform factories scope per transform pipeline**, using the mechanism
   `ServiceProviderHandlerFactory` already applies to `IAmALifetime`. The pump needs no change: it
   already builds and releases the pipeline per message.
3. **Opt-in adoption of a caller-owned scope**, configured in the producer context. Default is
   unchanged. `Publish` always creates a new scope per subscriber even under an ambient (ADR 0039).
   Brighter never disposes a scope it borrowed.
4. **Container-agnostic seam** in `Paramore.Brighter` (`IAmAScope` / `IAmAScopeProvider`, names
   provisional; the provider's single member is the ambient query `IAmAScope? GetAmbient(ScopeAffinity)`
   — D17). The container package always creates and owns the pipeline scope (D11); the provider only
   answers whether an adoptable ambient exists, and is asked exactly once per scoped pipeline even when the
   affinity is `AlwaysNew` (D16). That obligation is enforced on both sides: a conforming provider returns
   nothing for an `AlwaysNew` ask, *and* Brighter ignores an ambient returned for one, so a third-party
   provider cannot defeat `Publish` subscriber isolation (FR-24.4). The ambient stays outside `Paramore.Brighter` — core never sees a
   service provider. The six factory interfaces are **not** frozen: NFR-1's signature freeze was
   withdrawn at revision 14, so a per-pipeline parameter is available to the mapper and transformer
   factories, at the cost of a compile break for any hand-rolled implementation on `netstandard2.0`.
5. **The pump does not publish a per-message ambient** — decided; see
   [issue comment](https://github.com/BrighterCommand/Brighter/issues/4256#issuecomment-5124807305).

## Status

- [x] Requirements (`/spec:requirements`) — **APPROVED at revision 15** (27 FRs / 10 NFRs / 49 ACs), after eleven adversarial review rounds (20, 17, 11, 16, 13, 14, 9, 7, 4, 4, then 2 findings). The rev-12 review found all four rev-11 findings Fixed and closed with "on content, the requirements have converged"; its two remaining findings were editorial and were applied in revision 13. **Revision 14** withdraws NFR-1's signature freeze on the six factory interfaces at the requirement owner's direction — no review round; container-agnosticism (no `IServiceProvider` on a core interface, no container dependency in core) is unchanged. **Revision 15** aligns FR-17 with the design's repeated-opt-in case (AC-49). **Revision 16 is written and awaiting its own review round** (27 / 10 / 50): the design's registration-extension rename lands in the requirements, and FR-22 gains a fourth rule (AC-50) for an opt-in defeated by a pre-existing `IBrighterOptions` registration — new scope, so unlike revision 15 it is reviewed before re-approval.
- [ ] Design (`/spec:design`) — `0070` (transform pipeline DI scope) and `0071` (handler pipelines onto the same
  handle) written, both `Proposed`; outstanding are `0072` (the seam), `0073` (the opt-in) and `0074` (where the
  FR-22 validation rules are evaluated)
- [ ] Tasks (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`, interactive TDD via `/test-first`)
- [ ] PR

## Decisions taken during requirements

1. **Ambient supply (D1)** — a new package (working name `Paramore.Brighter.Extensions.AspNetCore`)
   ships an `IHttpContextAccessor`-backed `IAmAScopeProvider`. No middleware; no ASP.NET dependency in
   the DI package. Registering the provider is the opt-in.
2. **Opt-in reach (D2)** — one flag governs handler pipelines and transform pipelines alike. Two `Post`s in
   one HTTP request therefore share the request scope.
3. **Naming (D4)** — settled as the placeholders: `IAmAScope`, `IAmAScopeProvider`,
   `ScopeAffinity{JoinAmbient, AlwaysNew}`. Documentation must disambiguate `IAmAScope` from the
   existing `IAmALifetime` (NFR-8).
4. **Compatibility (D3)** — clean break on `MapperLifetime.Scoped`; migrate to `Singleton`; release
   note required. No compatibility flag.
5. **Affinity applies to `Scoped` only (D5)** — adoption is a property of the `Scoped` lifetime, since
   only `Scoped` binds instances to the pipeline's scope. Because all three lifetimes default to
   `Transient`, an inert opt-in is reported by `ValidatePipelines()` rather than silently ignored
   (FR-21, FR-22).
6. **Subscriber isolation propagates (D6)** — a `Publish` subscriber's scope isolation extends to
   pipelines nested inside it, via an `AsyncLocal` suppression flag. This partially amends OOS-2:
   suppression is in scope, a general `AsyncLocal` ambient *source* is not (FR-8, FR-9).
7. **The borrowed scope owns the artefact (D7)** — under adoption, instance identity belongs to the
   borrowed scope, not the pipeline, so two `Post`s in one request share one mapper. `Scoped` identity
   comes from Brighter's own cache rather than the container (every artefact is registered
   `Transient`), so this needs per-scope association — bounded by FR-26 (C-17).
8. **No mixing `Transient` and `Scoped` (D8)** — discarding any lifetime set to `Singleton`, the
   remaining handler/mapper/transform lifetimes must all be equal; a mixed pair is a validation
   **error** under either affinity setting (FR-22.2). Rationale: only `Scoped` adopts, so a mixed
   pipeline joins the caller's scope on one side and not the other. `Singleton` is excluded because
   it resolves from root regardless, and FR-20 prescribes it as the migration path.
9. **Captive-dependency warning (D9)** — a `Singleton` artefact whose constructor requires a
   container-`Scoped` service is reported as a warning at validation, since it resolves from the root
   provider (FR-22.3).

## Still open, for the ADR

- **C-9** — the exact name and shape of the opt-in property on `IBrighterOptions`. Written against the
  working name `ScopeAffinity DefaultScopeAffinity = ScopeAffinity.AlwaysNew`; a `bool` is the
  alternative. Behaviour is fixed either way.
- **C-8** — whether `IAmAScope` implements `IDisposable` only or both `IDisposable` and
  `IAsyncDisposable`, and confirmation that the seam types live in `Paramore.Brighter`.
- **C-11's three remaining working names** — the package name `Paramore.Brighter.Extensions.AspNetCore`
  (D1), the ambient-query member `GetAmbient(ScopeAffinity)` on `IAmAScopeProvider` (contract fixed by
  FR-10, D17) and the registration extension `AddBrighterAspNetCoreScopes(...)` (contract fixed by
  FR-17, D13/D18). Only the spellings are open; the type names in D4 and all three contracts are settled.
- How the seam expresses ambient suppression, given it must work on a subscriber pipeline that takes no
  pipeline scope at all (FR-27.3 settled that suppression is a subscriber property, not an affinity value,
  so it cannot be an argument to, or a return value of, the ambient query).
- The shape of the **non-core** contract by which an ambient exposes a resolution source to the
  container package. NFR-1 forbids `IServiceProvider` on any `Paramore.Brighter` interface, and C-1
  fixes that adoption means resolving from the caller's provider — so the hand-off type must live
  outside core and be implementable by the ASP.NET package and by a third party alike (FR-10, AC-35).

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
  `TransformPipelineBuilderAsync.cs:122`, `:163`) must release the pipeline scope, or a mapper with a
  misconfigured dependency leaks one scope per attempt.
- Both publish paths **fully resolve** every subscriber's pipeline before running any of them, then run
  them concurrently — sync `CommandProcessor.cs:474` then `:481` (`Parallel.ForEach`), async `:581` then
  `:591-599` and `:601` (`Task.WhenAll`). `PipelineBuilder.Build` resolves the handler and every
  decorator per subscriber (`PipelineBuilder.cs:183-187`), so scoping and suppression need **two**
  brackets: one per subscriber inside the build, one around each subscriber's own invocation (D10,
  FR-9). Drive this with a test first.
