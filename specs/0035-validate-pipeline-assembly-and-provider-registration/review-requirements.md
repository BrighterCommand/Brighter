# Review: requirements — validate-pipeline-assembly-and-provider-registration

**Date**: 2026-06-16
**Threshold**: 60
**Verdict**: PASS (converged)

Final independent convergence review: 0 findings at any severity. Every load-bearing codebase claim independently re-verified against source.

> This file records the latest (convergence) wave. The requirements went through multiple adversarial waves with different lenses — grounding/testability, post-discovery realignment, clean-slate grounding, and Ian-philosophy/issue-fidelity — each iterated to clean. See the wave log below.

## Wave log

| Wave | Lens | Verdict | Key findings → resolution |
|------|------|---------|---------------------------|
| 1 | grounding/testability | NEEDS WORK (3≥60) | transforms gap, FR-7 observability, sync/async "registered" → fixed |
| 2 | grounding | NEEDS WORK (2≥60) | transform identity undefined, mapper-precondition → fixed |
| 3 | grounding | PASS | — |
| 4 | post-discovery realignment | NEEDS WORK (3≥60) | default mapper masks mapper-missing; handler-missing already exists; placement → scope reshaped to transforms + provider |
| 5 | realignment factual | NEEDS WORK (3≥60) | "default declares no transforms" false ([CloudEvents]); DescribeTransforms sync-only; (A) narrowing → fixed (C-9/C-10) |
| 6 | clean-slate grounding | NEEDS WORK (2≥60) | `Subscription.DataType` phantom (→ RequestType); (A) probe-vs-build (→ C-11) → fixed |
| 7 | clean-slate verify | PASS | — |
| 8 | Ian philosophy + issue fidelity | NEEDS WORK (2≥60) | severity justification ("heuristic" misapplied → C-2 honest rationale); (B) hardcoded attrs → GetHandlerType per ADR 0053 §7 → fixed |
| 9 | philosophy verify | PASS (2 low nits) | AC vocabulary, AC-10 impl-detail → fixed |
| 10 | final independent convergence | **PASS (0 of any severity)** | — |

## Convergence verification (wave 10)

Every FR-1..FR-12 maps to a deterministic AC; no orphan FR/AC. Independently re-verified: `Subscription.RequestType` (Type?, nullable; **no `DataType` member** in src); `ResolveMapperInfo`/`ResolveAsyncMapperInfo` signature + default fallback; `DescribeTransforms` sync-only (FR-5 union → extend, C-5); `[Compress]`/`[Decompress]`.GetHandlerType()→`CompressPayloadTransformer`; `ValidateRequestAttribute`/`ValidateRequestAsyncAttribute`.GetHandlerType()→`ValidateRequestHandler<>`/`Async<>`; three providers register those open generics; `ConsumerValidationRules.HandlerRegistered`=Error (OOS-8); `AttributeAsyncConsistency`=Error (C-2 precedent); mixed Error/Warning precedent makes C-2's "deliberate deviation" honest; `ProducerValidationRules` only checks RequestType today; default mapper `JsonMessageMapper<>`→`[CloudEvents]`→`CloudEventsTransformer` registered by default (FR-4/AC-4); `BuildWrapPipeline`/`BuildUnwrapPipeline` throw at build/dispatch time (C-2 deferred-throw + C-11 probe-not-build accurate); `ValidatePipelines` does not pass a mapper registry today (C-12); `RegisterHandlersFromAssembly` adds handlers to the subscriber registry (C-7).

**Issue fidelity:** honest narrowing (Scope note, C-1, C-10, OOS-7/OOS-8); (B) faithfully implements Ian's "also check whether you have registered UseValidation for a provider." No overclaim.

## Summary
| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 0 |

**Total findings**: 0
**Findings at or above threshold (60)**: 0
