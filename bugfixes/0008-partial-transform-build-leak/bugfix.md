# Bugfix: Partial-failure transform leak in BuildTransformPipeline

**Linked Issue**: PR #4254 review — point 1
**Status**: Verified

**Scope decision (user-approved):** fix **all three windows** — primary mid-loop leak (sync +
async), window 2 (post-return construction), and window 3 (transformer leaked when
`Initialize...FromAttributeParams` throws inside `TransformerFactory`/`TransformerFactoryAsync`).

## Symptom
When building a wrap/unwrap pipeline, `BuildTransformPipeline` creates transforms one at a time
from the transformer factory. If a *later* transform fails to be created — e.g. the factory
returns `null` and the loop raises `InvalidOperationException` — every transform already created
in that loop is leaked: it is never released back to the transformer factory.

The enclosing `BuildWrapPipeline`/`BuildUnwrapPipeline` `catch` releases only the *mapper*. The
already-created transforms were never handed to a `WrapPipeline`/`UnwrapPipeline` (the pipeline
that would own them via its `TransformLifetimeScope`/`InstanceScope` was never constructed,
because `BuildTransformPipeline` threw before returning), so nothing calls
`_messageTransformerFactory.Release(...)` on them. Under `Transient` transformer lifetime this
leaks one DI scope per created transform, per failed build — the exact class of defect this PR
set out to close.

Expected: a build failure releases *both* the mapper and any transforms already created, leaving
`CreateCount == ReleaseCount`.

Reproduction (conceptual): register a mapper whose wrap/unwrap pipeline declares two transforms
where the factory creates the first successfully but returns `null` (or throws) for the second;
build the pipeline; observe the first transform is never released.

## Suspected Location
- `src/Paramore.Brighter/TransformPipelineBuilder.cs:173-204` — `BuildTransformPipeline<TRequest>`
  builds a `List<IAmAMessageTransform>` and throws at `:194` when `CreateMessageTransformer()`
  returns null; the partially-filled `transforms` list is lost on throw.
- `src/Paramore.Brighter/TransformPipelineBuilder.cs:114-120` / `:150-156` — the `catch` in
  `BuildWrapPipeline`/`BuildUnwrapPipeline` releases only `messageMapper`, not the transforms.
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs:173-201` — async `BuildTransformPipeline`,
  throws at `:194`; same gap.
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs:114-120` / `:150-156` — async `catch`
  releases only the mapper.
- Release mechanism for reference: `TransformLifetimeScope.ReleaseTrackedObjects`
  (`src/Paramore.Brighter/TransformLifetimeScope.cs:38-45`) calls `_factory.Release(trackedItem)`
  — the same `IAmAMessageTransformerFactory.Release` we must call on the partial list.

## Root-Cause Hypothesis
`BuildTransformPipeline` accumulates created transforms in a local list but has no `try/catch`
around the creation loop, so a mid-loop failure abandons the already-created transforms without
releasing them. Ownership never transfers to a pipeline (the pipeline is constructed *after*
`BuildTransformPipeline` returns), and the outer `catch` only knows about the mapper.

**Proposed fix (UNVERIFIED — to be proven or refuted in /bugfix:confirm):** wrap the creation
loop in `BuildTransformPipeline` (both sync and async) in a `try/catch` that releases every
transform already added via `_messageTransformerFactory.Release(...)` before rethrowing — so the
partial list is drained at the point that owns it, keeping the outer mapper-release logic
untouched.

## Confirmed Root Cause
**CONFIRMED.** `BuildTransformPipeline<TRequest>` builds a method-local
`List<IAmAMessageTransform>` one item at a time (`TransformPipelineBuilder.cs:188-201`).
Transforms are handed to a `TransformLifetimeScope` (the only thing that eventually calls
`_factory.Release`) **only in the `WrapPipeline`/`UnwrapPipeline` constructor**, invoked at
`TransformPipelineBuilder.cs:102`/`:138` — strictly *after* `BuildTransformPipeline` returns. If
the creation loop throws on transform N, transforms 1..N-1 exist only in the method-local list; no
pipeline, and therefore no `InstanceScope`, was ever constructed to own them. The enclosing
`catch` (`:114-120`/`:150-156`) releases only `messageMapper` and has no reference to that local
list, so `_messageTransformerFactory.Release(...)` is never called on the already-created
transforms. Identical defect in the async builder.

## Evidence
- `TransformPipelineBuilder.cs:176` — `var transforms = new List<IAmAMessageTransform>();` (method-local).
- `TransformPipelineBuilder.cs:188-201` — the `transformAttributes.Each(...)` creation loop; `:191`
  calls `CreateMessageTransformer()`, `:199` `transforms.Add(...)`. No `try/catch` around the loop.
- Mid-loop throw trigger: `TransformerFactory.cs:35-44` `CreateMessageTransformer()` throws
  `ConfigurationException` when `factory.Create(...)` returns null (`:39-40`) and can also throw
  from `InitializeWrap/UnwrapFromAttributeParams` (`:41-42`). (The builder's own `if (transformer
  is null) throw InvalidOperationException` at `:192-196` is effectively unreachable for the
  null-create case because `CreateMessageTransformer` throws first — conclusion unchanged.)
- Ownership transfer happens only in the pipeline ctor, after the builder returns:
  `WrapPipeline.cs:62-66` (`InstanceScope = new TransformLifetimeScope(...); Transforms.Each(t =>
  InstanceScope.Add(t));`), `UnwrapPipeline.cs:52-56`, `WrapPipelineAsync.cs:66-70`,
  `UnwrapPipelineAsync.cs:54-58`.
- A throw at `TransformPipelineBuilder.cs:100` means `:102` (ctor) never runs → no `InstanceScope`.
- Never-reached release path: `TransformLifetimeScope.cs:37-44` `ReleaseTrackedObjects()` →
  `_factory.Release(trackedItem)`, driven from `TransformPipeline.cs:38-48` via `InstanceScope?.Dispose()`.
- Adversarial check — no finalizer rescue: `TransformLifetimeScope`'s finalizer only releases
  objects that were `Add`ed; nothing was added, so no GC path either. Genuine leak.

## Scope Notes
The proposed "release the partial list inside `BuildTransformPipeline`" guard is **complete and
correct for the primary leak** (no double-release: these transforms were never added to any
`InstanceScope`, so the builder is the only release path; the outer `catch` still releases only
the mapper). Sub-agent rated it **PARTIAL** because it leaves two adjacent windows:

1. **Async parity (in scope — must fix):** `TransformPipelineBuilderAsync.cs:173-201` has the
   identical gap; `catch` blocks `:114-120`/`:150-156` release only the mapper. Fix both files.
   Async release must honour the async factory contract (`IAmAMessageTransformerFactoryAsync`),
   not the sync `Release`.

2. **Post-return construction window (decision needed):** if `BuildTransformPipeline` returns
   successfully but the pipeline ctor or the subsequent trace/logging throws
   (`TransformPipelineBuilder.cs:102-110`), control lands in the same outer `catch` that releases
   only the mapper; the built transforms sit in a local the `catch` never releases. Closing this
   requires the outer `catch` to also release the returned transforms (capture the list outside
   the pipeline construction). Symmetric class of leak to item 1; narrow (requires ctor/trace to
   throw). **Recommend closing it in the same fix** since it is the same defect class.

3. **In-flight transform inside `CreateMessageTransformer` (recommend OUT of scope):** if
   `factory.Create(...)` succeeds but `InitializeWrap/UnwrapFromAttributeParams` throws
   (`TransformerFactory.cs:41-42`), the just-created transformer was never returned → never in the
   builder's list → a builder-level guard cannot reach it. This is a separate, narrower leak
   inside `TransformerFactory`/`TransformerFactoryAsync` itself, not the reviewer's item 1.

Callers are consumers, not additional defect sites: `OutboxProducerMediator.cs:507,515,1169,1196`
use `using var pipeline = ...BuildWrap/UnwrapPipeline<TRequest>()`; `Reactor.cs:480` /
`Proactor.cs:467` invoke the same builders reflectively. No independent transform-building loop
exists elsewhere.

## Regression Test
Six RED tests in `tests/Paramore.Brighter.Core.Tests/MessageSerialisation/`, each asserting the
recording transformer factory's `Created` transforms all get `Released` after the build throws
`ConfigurationException`. All six fail today with `Released == []` (the leak); the other 76
MessageSerialisation tests stay green.

**Primary — mid-loop create failure (sync + async):**
- `When_A_Later_Wrap_Transform_Cannot_Be_Created_Earlier_Transforms_Are_Released.cs`
- `When_A_Later_Wrap_Transform_Cannot_Be_Created_Earlier_Transforms_Are_Released_Async.cs`
  (mapper declares two wrap transforms; factory builds the first, returns null for the second.)

**Window 3 — transform Initialize throws inside `TransformerFactory` (sync + async):**
- `When_A_Wrap_Transform_Fails_To_Initialize_It_Is_Released.cs`
- `When_A_Wrap_Transform_Fails_To_Initialize_It_Is_Released_Async.cs`
  (factory creates the transform; `InitializeWrapFromAttributeParams` throws before it is returned.)

**Window 2 — post-construction failure (sync + async):**
- `When_Building_A_Wrap_Pipeline_Fails_After_Construction_Transforms_Are_Released.cs`
- `When_Building_A_Wrap_Pipeline_Fails_After_Construction_Transforms_Are_Released_Async.cs`
  (mapper's `MapToRequest` is an explicit interface impl → unwrap discovery throws *after* the wrap
  pipeline + its transform are constructed; asserts release is deterministic, not finalizer-driven.)

Each test uses nested private doubles (recording factory + purpose-built mapper), reusing existing
shared attributes/transforms — matching the `MessageWrapCleanupTests` precedent. RED confirmed on
net9.0: `Failed: 6, Passed: 76`.

## Fix
All three windows closed, sync + async symmetric. `MessageSerialisation` suite green:
`Failed: 0, Passed: 82` on net9.0 (the 6 new tests + 76 pre-existing).

**Window 3 — Initialize throws inside the transformer factory:**
- `src/Paramore.Brighter/TransformerFactory.cs` — `CreateMessageTransformer` now wraps the
  `Initialize*FromAttributeParams` calls in `try/catch`; on failure it `factory.Release(...)` the
  just-created transformer before rethrowing.
- `src/Paramore.Brighter/TransformerFactoryAsync.cs` — same.

**Primary — mid-loop create failure:**
- `src/Paramore.Brighter/TransformPipelineBuilder.cs` — `BuildTransformPipeline` wraps the creation
  loop in `try/catch`; on failure it releases the already-created partial list (new private
  `ReleaseTransforms` helper) before rethrowing.
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs` — same (async `ReleaseTransforms`).

**Window 2 — post-construction failure:**
- Both builders: `BuildWrapPipeline`/`BuildUnwrapPipeline` now track the constructed `pipeline` and
  the returned `transforms`. The outer `catch` delegates to a new private `CleanUpAfterFailedBuild`
  helper: if the pipeline was constructed it is `Dispose()`d (releasing mapper + transforms exactly
  once and suppressing the finalizer); otherwise the returned transforms (if any) and the mapper
  are released directly. `BuildTransformPipeline` releasing its own partial list means `transforms`
  is only non-null in the helper when it returned successfully — so no double-release.

No default changes, no behavioural change on the success path. The `InvalidOperationException`
null-transform guard in `BuildTransformPipeline` is retained (unreachable for the null-create case,
but harmless and out of scope to remove).
