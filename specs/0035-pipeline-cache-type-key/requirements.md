# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4192

## Problem Statement

**As** a developer or operator running Brighter inside a modular host (for example a modular monolith) where one shared request or event type is handled by code drawn from several modules,
**I want** each distinctly-typed handler and message mapper registered for that request type to run with its own pipeline metadata — its own decorator attributes and its own wrap/unwrap transforms —
**so that** a handler or mapper does not silently inherit another type's filter, logging, retry, declared-inbox, or transform configuration merely because the two types happen to share the same simple class name in different namespaces.

Today the three pipeline builders memoise reflection-derived pipeline metadata in process-global caches keyed by the simple class name (`GetType().Name`, with the namespace dropped). When two types share a simple name across namespaces — for instance `Acme.Orders.Handlers.AuditHandler` and `Acme.Billing.Handlers.AuditHandler`, or `Acme.Orders.Mappers.EventMapper` and `Acme.Billing.Mappers.EventMapper` — they collide on a single cache slot. The first type built ("the winner") populates the slot; every later same-named type silently reuses the winner's cached metadata.

Because routing and handler/mapper instantiation are resolved by the full runtime `Type`, the handler or mapper **body** that executes is always the correct class. Only the **decorators** (request-handler attributes) and **transforms** wrapping that body are taken from the cache, so the losing type executes its own body while wearing the winner's attribute arguments and transforms. There is no exception and no log line: the wrong behaviour persists for the life of the process.

## Proposed Solution

Pipeline metadata that Brighter caches per handler or per message mapper must be associated with that handler's or mapper's full runtime type identity, not its simple name. After the fix, two handlers (or two mappers) that share a simple class name but live in different namespaces, both registered for the same request type, each resolve and run their own decorator attributes and their own transforms. Existing single-type behaviour, caching, performance, public API, `ClearPipelineCache()`, and the description/diagnostic paths are unchanged. Users who previously worked around the collision by enforcing unique simple names no longer need to.

## Requirements

### Functional Requirements

- **FR-1 — Handler decorator cache disambiguates by runtime type (sync).**
  When `PipelineBuilder<TRequest>.BuildPipeline` builds the synchronous handler pipeline, the pre- and post-handler `RequestHandlerAttribute` metadata it caches and retrieves must be specific to the handler's runtime type, not its simple name.
  *Example:* `OrderHandler` in `Acme.Orders` carries `[RequestLogging(step:1)]`; `OrderHandler` in `Acme.Billing` carries `[RequestLogging(step:1)] [FallbackPolicy]`, both registered for request `T`. Building `Acme.Billing.OrderHandler` must apply the fallback decorator even when `Acme.Orders.OrderHandler` was built first into the same `PipelineBuilder<T>` cache.

- **FR-2 — Handler decorator cache disambiguates by runtime type (async).**
  `PipelineBuilder<TRequest>.BuildAsyncPipeline` must behave as FR-1 for the asynchronous pipeline (`IHandleRequestsAsync`), using the same async pre/post attribute caches.
  *Example:* with the two `OrderHandler` types from FR-1 implemented as async handlers, building the second-registered type applies its own async decorators, not the first type's.

- **FR-3 — Wrap-transform cache disambiguates by runtime type (sync and async builders).**
  `TransformPipelineBuilder.FindWrapTransforms` and `TransformPipelineBuilderAsync.FindWrapTransforms` each maintain their **own** static wrap-transform cache (separate static fields on separate classes); **both** must cache and retrieve wrap-transform metadata specific to the message mapper's runtime type.
  *Example:* `EventMapper` in `Acme.Orders` declares `[CompressPayload]` on its map-to-message method; `EventMapper` in `Acme.Billing` declares `[EncryptPayload]`. The Billing mapper's wrap pipeline must contain its own encrypt transform regardless of which mapper was built first.
  *Note:* the two builders populate their caches by different mechanisms (sync `TryGetValue`/`TryAdd`, async `GetOrAdd`); that difference affects only first-access scan count (see C-6), not the per-runtime-type disambiguation outcome this FR requires, which is identical for both.

- **FR-4 — Unwrap-transform cache disambiguates by runtime type (sync and async builders).**
  `TransformPipelineBuilder.FindUnwrapTransforms` and `TransformPipelineBuilderAsync.FindUnwrapTransforms` — each with its own separate static unwrap-transform cache — must behave as FR-3 for unwrap transforms (message-to-request).
  *Example:* with the two `EventMapper` types from FR-3, the Billing mapper's unwrap pipeline must contain its own declared unwrap transforms, not the Orders mapper's.
  *Note:* as with FR-3, the sync/async cache-population mechanism difference is out of scope here (see C-6); only the per-runtime-type disambiguation outcome is required, and it is identical for both builders.

- **FR-5 — Single-type caching preserved.**
  When only one handler type (per closed `PipelineBuilder<TRequest>`) or one mapper type is registered, repeated builds for that type must continue to hit the cache and reuse previously computed metadata; the fix must not turn caching off. On the steady-state cache-hit path (single-threaded, post-warmup) the second build performs no further reflection scan. (The async transform builder's `GetOrAdd` may run its factory more than once under first-access contention — see C-6 — which is pre-existing behaviour this fix does not change.)
  *Example (handler):* building `Acme.Orders.OrderHandler` for request `T` twice in the same single-threaded process runs the pre- and post-attribute reflection scans once each; the second build is served from cache.
  *Example (mapper):* building the wrap and unwrap pipelines for `Acme.Orders.EventMapper` twice via the sync `TransformPipelineBuilder` runs each transform scan once; the second build is served from cache.

- **FR-6 — Global-inbox attribute immunity preserved.**
  The `UseInbox` attribute injected from a global `InboxConfiguration` must continue to be constructed fresh from the handler's live runtime type after the cache read (in `AddGlobalInboxAttributes` / its async equivalent) and must never be served from or written to the disambiguated decorator cache. Only **declared** attributes flow through the cache.
  *Example:* two same-named handlers with global inbox enabled each receive a `UseInbox` decorator built against their own runtime type, irrespective of cache state or build order.

- **FR-7 — No public API or behavioural change outside the cache key.**
  `ClearPipelineCache()` must retain its existing observable semantics (clearing it forces subsequent builds to recompute), and the `Describe` / `DescribeTransforms` reflection paths — which do not read the cache — must produce identical output to before the fix. `HandlerName` and `RequestHandler.Name` are unchanged.
  *Example:* a test asserting current `Describe` output and current post-`ClearPipelineCache()` recompute behaviour passes unmodified against the fixed builders.

### Non-functional Requirements

- **NFR-1 — No performance regression in the common case.** For the common single-type build, the fix must not introduce additional reflection scans per build beyond what occurs today; cache hits must remain O(1) lookups.
- **NFR-2 — Thread-safety preserved.** The caches must remain safe for concurrent reads and writes across threads building pipelines for the same or different types, as they are today (concurrent-dictionary semantics).
- **NFR-3 — Consistency across builders.** The disambiguation behaviour must be applied identically across all three builders (`PipelineBuilder<TRequest>`, `TransformPipelineBuilder`, `TransformPipelineBuilderAsync`) in a single change, so no builder is left keyed by simple name.

### Constraints and Assumptions

- **C-1** Handler and mapper routing/instantiation already resolve by full runtime `Type` (via `SubscriberRegistry` and the mapper registry); only the cached decorator/transform metadata is mis-keyed. The fix changes only the cache-key dimension. Note: the handler caches are presently keyed via `implicitHandler.Name.ToString()` (which returns the simple `GetType().Name`); the fix must re-source the key from the runtime `Type` **without** altering `HandlerName` / `RequestHandler.Name` (see OOS-3). Namespacing `HandlerName` to fix the collision is explicitly the wrong approach.
- **C-2** Only **declared** request-handler attributes pass through the decorator memento. The global-`InboxConfiguration` `UseInbox` attribute is built after the cache read and is therefore out of the collision's reach; this immunity is a constraint to preserve (see FR-6).
- **C-3** A type's full runtime identity (namespace + name, within its assembly) uniquely distinguishes the colliding types; simple name alone does not. Two distinct registered types are assumed to differ by runtime type identity.
- **C-4** `HandlerName` / `RequestHandler.Name` are retained as-is for logging and tracing; the fix must not alter them.
- **C-5** The mechanism by which runtime-type identity is used as the cache key is a design/ADR concern, not a requirement; this document constrains only the observable behaviour.
- **C-6** The async transform builder (`TransformPipelineBuilderAsync`) populates its caches via `ConcurrentDictionary.GetOrAdd`, whose value factory is not invoked under a lock; under concurrent first-access for the same key the reflection scan may run more than once (one result retained). This is pre-existing behaviour; the "no further scan" guarantee in FR-5 is scoped to the steady-state cache-hit path and excludes this race. Correctness under concurrency is still required (NFR-2, AC-10).

### Out of Scope

- **OOS-1** The per-module temporary mitigation already shipped by the consuming team (enforcing unique simple names plus an architecture test). The Brighter-side fix is independent of it.
- **OOS-2** The pre-existing mis-typed logger generic (`CreateLogger<TransformPipelineBuilder>` used inside `TransformPipelineBuilderAsync`). Its only observable effect is that async transform-builder log lines are categorised under `TransformPipelineBuilder` rather than `TransformPipelineBuilderAsync` — harmless, and deliberately not touched by this change.
- **OOS-3** Any change to `HandlerName` or `RequestHandler.Name` themselves.
- **OOS-4** Any change to routing, the subscriber registry, or mapper registry resolution.

## Acceptance Criteria

- **AC-1 (FR-1):** *Given* two handler types sharing a simple name in different namespaces, both registered for request type `T` with different declared decorator attributes, *and* the first is built into `PipelineBuilder<T>` before the second, *When* the synchronous pipeline for the second-registered type is built, *Then* its pipeline contains its own declared decorators with its own attribute arguments, not the first type's.

- **AC-2 (FR-1):** *Given* the same two handler types, *When* their synchronous pipelines are built in the opposite registration order, *Then* each still resolves its own decorators (the outcome is independent of build order).

- **AC-3 (FR-2):** *Given* two async handler types sharing a simple name across namespaces and registered for `T` with different declared decorators, *When* `BuildAsyncPipeline` is invoked for each, *Then* each async pipeline contains that type's own decorators regardless of build order.

- **AC-4 (FR-3):** *Given* two message mapper types sharing a simple name across namespaces with different declared wrap transforms, *When* the wrap pipeline is built for each (sync and async builders), *Then* each wrap pipeline contains that mapper's own wrap transforms regardless of build order.

- **AC-5 (FR-4):** *Given* the same two mapper types with different declared unwrap transforms, *When* the unwrap pipeline is built for each (sync and async builders), *Then* each unwrap pipeline contains that mapper's own unwrap transforms regardless of build order.

- **AC-6a (FR-5, handler builder):** *Given* a single handler type registered for `T`, *When* its synchronous pipeline is built twice in the same single-threaded process, *Then* the second build is served from cache rather than rescanning — observable as the pre- and post-attribute caches each holding exactly one entry keyed for that type after both builds, and the second build producing a decorator sequence equivalent to the first (same decorator types, order, steps, and timing — the `HandlerTiming` Before/After value). The cache's internal value representation is not constrained (per C-5); "served from cache" is verified observably via the cache entry count and output equivalence above, not by counting internal reflection scans.

- **AC-6b (FR-5, sync transform builder):** *Given* a single mapper type, *When* its wrap and unwrap pipelines are built twice via `TransformPipelineBuilder` in the same single-threaded process, *Then* each transform cache holds exactly one entry keyed for that mapper type after both builds, and the second build produces an equivalent transform sequence — verified by the same observable means as AC-6a (cache entry count plus output equivalence; internal value representation unconstrained, per C-5).

- **AC-6c (FR-5, async transform builder):** *Given* a single mapper type, *When* its wrap and unwrap pipelines are built twice via `TransformPipelineBuilderAsync` in the same single-threaded process (post-warmup, so the `GetOrAdd` factory has already run), *Then* the second build is served from the single retained cache entry for that mapper type and produces an equivalent transform sequence. Reference-equality of the retained value is not asserted, and concurrent first-access reuse is out of scope per C-6.

- **AC-7 (FR-6):** *Given* two same-named handler types registered for `T` with global inbox enabled via `InboxConfiguration`, *When* each pipeline is built in any order, *Then* each handler receives a `UseInbox` decorator constructed against its own runtime type, and no `UseInbox` attribute is read from or written to the declared-attribute cache.

- **AC-8 (FR-7):** *Given* the existing test suite for `ClearPipelineCache()` and for `Describe` / `DescribeTransforms`, *When* it is run against the fixed builders without modification, *Then* it passes: cache clearing still forces recomputation, and description output is structurally unchanged from before the fix — for `Describe`, the same handler step types, in the same order, with the same steps and timings; for `DescribeTransforms`, the same transform types, order, and steps (transform steps carry no timing). This is equivalence of described content, not a byte-for-byte string baseline.

- **AC-9 (FR-7):** *Given* the fixed builders, *When* their public surface is compared to the prior release, *Then* there is no change to public API signatures (including `ClearPipelineCache`, `Describe`, `DescribeTransforms`, `HandlerName`, `RequestHandler.Name`).

- **AC-10 (NFR-2 / FR-5):** *Given* concurrent builds for the same request type from multiple threads — both for two colliding types and for a single type — *When* they run together, *Then* no thread observes another type's metadata and no cache corruption or exception occurs. For a single type built concurrently, every thread observes that type's own metadata and the cache converges to exactly one entry for it. In particular, for the async transform builder's first-access `GetOrAdd` race (C-6), even though its value factory may run more than once, every thread ultimately observes that mapper type's own transforms and the cache converges to a single retained entry per type.

- **AC-11 (NFR-3):** *Given* the colliding same-simple-name mapper scenarios of AC-4/AC-5, *When* they are exercised through **both** `TransformPipelineBuilder` (sync) and `TransformPipelineBuilderAsync` (async), *Then* each builder independently resolves each mapper's own wrap/unwrap transforms — confirming all three builders (handler builder via AC-1..3, plus both transform builders) are converted and none is left keyed by simple name.

## Additional Context

### Glossary

- **Memento / cache:** A process-global static dictionary in a pipeline builder that stores reflection-derived pipeline metadata (decorator attributes or transforms) so it is computed once in the common case and reused (the async transform builder's `GetOrAdd` may run its factory more than once under first-access contention, retaining one result — see C-6). The defect is that the cache key is the simple class name rather than the runtime type identity.
- **Body vs decorator:** The **body** is the user's handler/mapper class that does the actual work; it is instantiated by full `Type` and is always correct. A **decorator** is a `RequestHandlerAttribute`-driven wrapper (filter, logging, retry, inbox, etc.) placed around the body using attribute arguments — these are taken from the cache and are what the defect corrupts.
- **Declared vs global-inbox attribute:** A **declared** attribute is one authored directly on the handler method and discovered by reflection; it passes through the cache. The **global-inbox** `UseInbox` attribute is synthesised at build time from a global `InboxConfiguration` against the live runtime type, after the cache read, and is therefore unaffected by the collision.
- **Wrap / unwrap transform:** A **wrap** transform runs when mapping an outgoing request to a message (e.g. compress, encrypt); an **unwrap** transform runs when mapping an incoming message back to a request (e.g. decompress, decrypt). Each direction has its own cache in the transform builders.
