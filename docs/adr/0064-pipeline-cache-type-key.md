---
id: 0064-pipeline-cache-type-key
title: "Key pipeline-builder metadata caches by runtime Type"
status: Accepted
author:
  - "Brighter Team"
created: 2026-06-18
summary: "Fixes a pipeline-cache collision bug in PipelineBuilder, TransformPipelineBuilder, and TransformPipelineBuilderAsync by replacing the simple class-name string cache key with the runtime System.Type, so same-named handler or mapper types in different namespaces each receive their own correct decorators and transforms."
tags:
  - "pipeline"
  - "middleware"
  - "performance"
---

# 0064. Key pipeline-builder metadata caches by runtime Type

Date: 2026-06-18

## Status

Accepted

## Context

Brighter's three pipeline builders memoise reflection-derived pipeline metadata in process-global static caches so the (relatively expensive) attribute/transform reflection scan runs once per identity and is reused thereafter. Each cache's responsibility is to *know the reflection-derived pipeline metadata for a given handler or mapper identity*. The defect is that the cache uses an **impoverished identity** — the simple class name (`GetType().Name`, namespace dropped) — as its key.

In a modular host where one shared request/event type is handled by code drawn from several modules, two distinct types can share a simple name across namespaces (for example `Acme.Orders.Handlers.AuditHandler` and `Acme.Billing.Handlers.AuditHandler`, or `Acme.Orders.Mappers.EventMapper` and `Acme.Billing.Mappers.EventMapper`). These collide on a single cache slot. Routing and instantiation resolve by full runtime `Type`, so the correct **body** always executes — but the **decorators** (request-handler attributes) and **transforms** are read from the cache, so the second type silently wears the first type's attribute arguments and transforms. There is no exception and no log line; the wrong behaviour persists for the life of the process (#4192).

Verified collision sites in source:

- `src/Paramore.Brighter/PipelineBuilder.cs:49-50` — `s_preAttributesMemento` / `s_postAttributesMemento` are `ConcurrentDictionary<string, IOrderedEnumerable<RequestHandlerAttribute>>`, keyed via `implicitHandler.Name.ToString()` at the read/write sites in `BuildPipeline` (lines 275, 284, 292, 301) and `BuildAsyncPipeline` (lines 319, 327, 334, 342).
- `src/Paramore.Brighter/TransformPipelineBuilder.cs:57-61` — `s_wrapTransformsMemento` / `s_unWrapTransformsMemento` (`ConcurrentDictionary<string, ...>`), keyed via `var key = messageMapper.GetType().Name` in `FindWrapTransforms` (line 238) and `FindUnwrapTransforms` (line 253), populated with `TryGetValue` / `TryAdd`.
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs:57-61` — the same two mementos, keyed via `messageMapper.GetType().Name` in `FindWrapTransforms` (line 201) and `FindUnwrapTransforms` (line 209), populated with `GetOrAdd`.

The handler key reduces to the simple name even though it is sourced from `HandlerName`: `RequestHandler.Name` is `new HandlerName(GetType().Name)` (`src/Paramore.Brighter/RequestHandler.cs:68`) and `HandlerName.ToString()` returns that bare string unchanged (`src/Paramore.Brighter/HandlerName.cs:51-54`). So `implicitHandler.Name.ToString()` is exactly `GetType().Name`.

Forces:

- The fix must restore correct identity without changing observable behaviour anywhere else: caching must stay on, hot-path lookups must stay O(1) (NFR-1), thread-safety must be preserved (NFR-2), and the public API must not move (FR-7).
- All three builders must be corrected in one change so none is left keyed by simple name (NFR-3).
- `HandlerName` / `RequestHandler.Name` are display/logging/tracing concerns and must not be co-opted to carry cache identity (C-1, C-4, OOS-3).
- The global-inbox `UseInbox` attribute is built fresh from the live runtime type *after* the cache read (`PipelineBuilder.cs:363, 383`, via `implicitHandler.GetType()`) and never flows through the memento; its immunity must be preserved (FR-6, C-2).

There is precedent for shipping pipeline-cache changes without an ADR (PRs #4061, #4100/#4101); this ADR exists because the team chose the full `/spec` flow for this fix.

**Parent Requirement**: [specs/0035-pipeline-cache-type-key/requirements.md](../../specs/0035-pipeline-cache-type-key/requirements.md)

**Scope**: This ADR focuses specifically on the cache-key identity used by the three pipeline builders. It is the only ADR for this requirement.

## Decision

Key all three builders' static mementos by the runtime `System.Type` of the handler/mapper — the value returned by `GetType()` — replacing the simple-name `string` key. This restores the cache's responsibility to "know the metadata for a given *type* identity" rather than for a given *name*.

`System.Type` instances are reference-unique per loaded type within a runtime, implement value equality and `GetHashCode` based on that identity, and are therefore safe and correct `ConcurrentDictionary` keys. Crucially, a `Type` distinguishes two same-simple-named types in different namespaces — and, unlike a name string, also across assemblies — which is exactly the identity the colliding scenario requires (C-3).

This is recorded as a single corrected behaviour, not a configurable one. The collision is a latent correctness bug; there is no "old behaviour" worth preserving behind a toggle, so **no feature flag is introduced**.

### Architecture Overview

The change is one dimension of three near-identical caches. No new types, no new collaborators, no control-flow change — only the key expression and the dictionary's key type change. A diagram would add nothing.

Before: `ConcurrentDictionary<string, …>` keyed by `GetType().Name`
After:  `ConcurrentDictionary<Type, …>` keyed by `GetType()`

### Key Components

- `PipelineBuilder<TRequest>` (`src/Paramore.Brighter/PipelineBuilder.cs`) — handler pre/post `RequestHandlerAttribute` caches (`s_preAttributesMemento`, `s_postAttributesMemento`), used by both `BuildPipeline` (sync) and `BuildAsyncPipeline` (async).
- `TransformPipelineBuilder` (`src/Paramore.Brighter/TransformPipelineBuilder.cs`) — sync wrap/unwrap caches (`s_wrapTransformsMemento`, `s_unWrapTransformsMemento`).
- `TransformPipelineBuilderAsync` (`src/Paramore.Brighter/TransformPipelineBuilderAsync.cs`) — async wrap/unwrap caches (its own separate static fields of the same names).

All three are converted in one change (FR-3, FR-4, NFR-3, AC-11).

### Technology Choices

- **Key type: `System.Type`.** Truest available identity for a loaded type: non-nullable, reference-comparable, assembly-aware, and already in hand at every call site (`GetType()`), so it costs nothing to obtain. Its `Equals`/`GetHashCode` give correct dictionary semantics with no custom comparer.
- **`ConcurrentDictionary` retained.** Thread-safety semantics are unchanged (NFR-2). The sync builders keep `TryGetValue` / `TryAdd`; the async transform builder keeps `GetOrAdd`. The async `GetOrAdd` first-access double-scan (C-6) is pre-existing and explicitly **not** addressed here — re-keying does not alter it.

### Implementation Approach

1. Re-type each of the six static dictionaries from `ConcurrentDictionary<string, …>` to `ConcurrentDictionary<Type, …>` (`PipelineBuilder.cs:49-50`; `TransformPipelineBuilder.cs:57-61`; `TransformPipelineBuilderAsync.cs:57-61`).
2. Change the key expression at each read/write site from the simple-name string to the runtime `Type`:
   - `PipelineBuilder.cs` — replace `implicitHandler.Name.ToString()` with `implicitHandler.GetType()` at lines 275, 284, 292, 301 (sync) and 319, 327, 334, 342 (async).
   - `TransformPipelineBuilder.cs` / `TransformPipelineBuilderAsync.cs` — change `var key = messageMapper.GetType().Name` to `var key = messageMapper.GetType()` at lines 238/253 and 201/209.
3. Leave everything else untouched: `ClearPipelineCache()` (`PipelineBuilder.cs:253`; `TransformPipelineBuilder.cs:223`; `TransformPipelineBuilderAsync.cs:186`), `Describe`/`Describe()` (which do not read the cache, `PipelineBuilder.cs:106-137, 144-155`), `DescribeTransforms` (`TransformPipelineBuilder.cs:197-221`), the `AddGlobalInboxAttributes`/`AddGlobalInboxAttributesAsync` `UseInbox` construction (`PipelineBuilder.cs:350-389`), `HandlerName`, and `RequestHandler.Name` — no public-API change (FR-7, AC-8, AC-9, OOS-3).
4. Update the one white-box test that inspects memento keys, `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Pipeline/When_Building_A_Pipeline_Post_Attributes_Are_Cached.cs`: the helper `GetPostAttributesCacheKeys` reflects `s_postAttributesMemento` and returns `cache.Keys.Cast<string>()` as `IEnumerable<string>`, asserted via `Assert.Contains(nameof(MyPreAndPostDecoratedHandler), …)`. After the change the keys are `Type`, so the helper's return type moves to `IEnumerable<Type>` and its body to `cache.Keys.Cast<Type>()`, and each assertion moves to `Assert.Contains(typeof(MyPreAndPostDecoratedHandler), …)` (and the async variant). Re-typing the helper is mandatory, not optional: `Cast<string>()` over `Type` keys is not a compile error — it throws `InvalidCastException` at runtime. This is a test internal-detail update only; it is permitted because it asserts the cache's internal representation, which C-5 leaves unconstrained.

The pre-existing mis-typed logger generic in the async builder (`ApplicationLogging.CreateLogger<TransformPipelineBuilder>` at `TransformPipelineBuilderAsync.cs:50`) is deliberately left untouched (OOS-2).

## Consequences

### Positive

- Two same-simple-named handlers or mappers in different namespaces, both registered for the same request type, each resolve their own decorators and transforms regardless of build order (AC-1..AC-5).
- Correctness becomes order-independent and namespace-aware; it also extends to the cross-assembly case that a name string could not distinguish.
- Single-type caching is preserved: the cache still holds exactly one entry per type and steady-state cache hits remain O(1) reflection-free lookups (FR-5, NFR-1, AC-6a/b/c).
- All three builders converge on one identity rule; none is left keyed by simple name (NFR-3, AC-11).
- `UseInbox` immunity and the public/diagnostic surface are untouched (FR-6, FR-7, AC-7, AC-8, AC-9).
- Users who previously enforced unique simple names as a workaround no longer need to (OOS-1 mitigation becomes redundant).

### Negative

- The static caches now hold a `Type` reference per built handler/mapper for process lifetime. This pins each such `Type` (and transitively its defining assembly) against unload. In Brighter's typical hosting — handler/mapper types registered at startup and resident for the process — this is effectively the same retention the simple-name cache already implied via its cached attribute/transform graph, so it is not a new leak in practice. It *is* a consideration for collectible `AssemblyLoadContext` scenarios (dynamic load/unload of handler assemblies): a retained `Type` key would block collection of its `AssemblyLoadContext`. Brighter does not target that hosting model for handler/mapper registration, and `ClearPipelineCache()` already releases all entries, so this is assessed as a non-issue for supported hosting; it is noted here so a future collectible-assembly use case revisits it.
- The change touches a hot, historically-cached path. The new key is `GetType()` (already called at every site) plus `Type` hashing instead of `string` hashing; both are O(1) and the `Type` comparison is reference-based. No measurable regression is expected relative to the string key (NFR-1).

### Risks and Mitigations

- **Risk:** a reader assumes `HandlerName`/`RequestHandler.Name` still drives cache identity. **Mitigation:** the cache no longer reads `Name` at all; identity is sourced directly from `GetType()`, decoupling display name from cache key (C-1, C-4).
- **Risk:** the white-box test breaks because keys change from `string` to `Type`. **Mitigation:** this is expected and handled in step 4; it is the intended signal that the key type changed, and the behavioural test suite (`Describe`/`DescribeTransforms`/`ClearPipelineCache`) must continue to pass unmodified (AC-8).
- **Risk:** the async `GetOrAdd` first-access double-scan (C-6) is mistaken for something this change should fix. **Mitigation:** explicitly out of scope; re-keying neither introduces nor removes it, and concurrency correctness is still guaranteed (NFR-2, AC-10).

## Alternatives Considered

- **`Type.FullName` (string) as key.** Rejected. `FullName` is nullable — it returns `null` for open generic parameters and certain constructed generic types — which forces null-handling on the key path, and it is not guaranteed unique across assemblies (two assemblies may declare the same namespace-qualified name). It can therefore reintroduce a collision while adding complexity. `Type` itself carries the same namespace-qualified information with no nullability and with assembly identity built in.
- **`AssemblyQualifiedName` (string) as key.** Rejected. Unique, but verbose and allocation-heavy to compute per build, and sensitive to assembly version/culture/public-key-token, making the key brittle across routine version bumps. `Type` gives equal-or-better uniqueness with no string cost.
- **Simple name (`GetType().Name`) — the status quo.** Rejected: it is the defect.
- **Namespacing `HandlerName` / `RequestHandler.Name` to disambiguate.** Rejected. It conflates a display/logging/tracing concern with cache identity and would change observable log and trace output for no functional gain. Explicitly out of scope (C-1, C-4, OOS-3, OOS-4).
- **Per-instance (non-static) caches on each `PipelineBuilder<TRequest>`.** Rejected. It would sidestep the global collision, but it changes cache lifetime and performance characteristics: the cache would no longer be shared process-wide, so warm-up reflection cost would recur per builder instance. That is a larger behavioural change than this correctness bug warrants, and it is out of scope.

## References

- Requirements: [specs/0035-pipeline-cache-type-key/requirements.md](../../specs/0035-pipeline-cache-type-key/requirements.md)
- Related ADRs: none (sole ADR for this requirement)
- External references:
  - Linked issue: #4192
  - Michael Nygard, "Documenting Architecture Decisions" (ADR format)
  - .NET API docs: `System.Type` (identity, `Equals`/`GetHashCode` semantics) and `System.Type.FullName` (documented to return `null` for certain generic types)
