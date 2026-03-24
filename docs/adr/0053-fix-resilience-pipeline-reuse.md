# 53. Fix Resilience Pipeline Reuse Across Command Handlers

Date: 2026-03-11

## Status

Accepted

## Context

**Parent Requirement**: [specs/0024-resilience_pipeline_bug/requirements.md](../../specs/0024-resilience_pipeline_bug/requirements.md)

**Scope**: This ADR addresses the bug where non-generic `ResiliencePipeline` instances cannot be shared across multiple command handler types (#4042).

Brighter's resilience pipeline support uses Polly's `ResiliencePipelineRegistry<string>` to look up pipelines by key. The `UseResiliencePipelineAttribute` has a `UseTypePipeline` property that controls whether to look up a non-generic `ResiliencePipeline` (shared across all command types) or a generic `ResiliencePipeline<TRequest>` (scoped per command type).

The sync handler (`ResilienceExceptionPolicyHandler<TRequest>`) correctly implements both paths:

```
UseTypePipeline = false → GetPipeline(key)        → ResiliencePipeline (shared)
UseTypePipeline = true  → GetPipeline<TRequest>(key) → ResiliencePipeline<TRequest> (typed)
```

The async handler (`ResilienceExceptionPolicyHandlerAsync<TRequest>`) has two defects:

1. It does not read the `UseTypePipeline` parameter from `initializerList[1]`
2. It only has a `ResiliencePipeline<TRequest> _pipeline` field — no non-generic field
3. It always calls `GetPipeline<TRequest>(key)`, forcing per-command-type registration

This means any handler using the async path with a non-generic pipeline throws `KeyNotFoundException`.

## Decision

Align `ResilienceExceptionPolicyHandlerAsync<TRequest>` with the sync handler's existing implementation pattern.

### Responsibility Analysis

The `ResilienceExceptionPolicyHandlerAsync<TRequest>` class has two responsibilities:

1. **Knowing**: Which pipeline to use (resolved during `InitializeFromAttributeParams`)
2. **Doing**: Wrapping the downstream handler call in the resolved pipeline (during `HandleAsync`)

The bug is entirely in the "knowing" responsibility — the handler resolves the wrong pipeline type. The "doing" responsibility needs a minor adjustment to execute the correct pipeline based on which was resolved.

### Implementation Approach

**Changes to `ResilienceExceptionPolicyHandlerAsync<TRequest>`** (`src/Paramore.Brighter/Policies/Handlers/ResilienceExceptionPolicyHandlerAsync.cs`):

1. Add a non-generic field: `private ResiliencePipeline _pipeline = ResiliencePipeline.Empty;`
2. Rename existing field for clarity: `private ResiliencePipeline<TRequest> _typePipeline = ResiliencePipeline<TRequest>.Empty;`
3. In `InitializeFromAttributeParams`:
   - Read `UseTypePipeline` from `initializerList[1]`
   - Branch on the flag: call `GetPipeline(key)` or `GetPipeline<TRequest>(key)`
4. In `HandleAsync`:
   - Check `_pipeline != ResiliencePipeline.Empty` first (non-generic path)
   - Fall through to `_typePipeline` for the typed path

The resulting code mirrors `ResilienceExceptionPolicyHandler<TRequest>` line-for-line in its initialization logic, just using `ExecuteAsync` instead of `Execute` in the handle method.

### Key Design Choices

- **No fallback logic**: If `UseTypePipeline = true` and the generic pipeline isn't found, we let Polly throw `KeyNotFoundException`. This is explicit and debuggable. Fallback would mask misconfiguration.
- **No attribute changes**: Both `UseResiliencePipelineAttribute` and `UseResiliencePipelineAsyncAttribute` already pass `UseTypePipeline` via `InitializerParams()`. The attributes are correct; only the async handler is broken.
- **No new types**: This is a bug fix in an existing type. No new classes, interfaces, or abstractions are needed.

## Consequences

### Positive

- Non-generic resilience pipelines can be shared across handlers of different command types
- Async and sync handlers have consistent behavior
- No breaking changes — existing code using `UseTypePipeline = true` with properly registered generic pipelines continues to work
- The fix is minimal and focused

### Negative

- None significant. The change is purely corrective.

### Risks and Mitigations

- **Risk**: Existing users who worked around the bug by registering generic pipelines per command type could be affected if they switch to non-generic.
  - **Mitigation**: No behavioral change for `UseTypePipeline = true` — only the default (`false`) path is fixed. Existing workarounds continue to work.

## Alternatives Considered

### 1. Add Fallback from Generic to Non-Generic

When `UseTypePipeline = true` and `GetPipeline<TRequest>(key)` fails, fall back to `GetPipeline(key)`. Rejected because:
- Masks configuration errors
- The `UseTypePipeline` flag already gives users explicit control
- Adds complexity for a scenario that isn't the reported bug

### 2. Remove `UseTypePipeline` and Always Use Non-Generic

Simplify by only supporting non-generic pipelines. Rejected because:
- Breaking change for users who rely on typed pipelines
- Typed pipelines have legitimate use cases (e.g., per-command circuit breakers)

## References

- Requirements: [specs/0024-resilience_pipeline_bug/requirements.md](../../specs/0024-resilience_pipeline_bug/requirements.md)
- Issue: #4042
- Sync handler (reference implementation): `src/Paramore.Brighter/Policies/Handlers/ResilienceExceptionPolicyHandler.cs`
- Async handler (bug location): `src/Paramore.Brighter/Policies/Handlers/ResilienceExceptionPolicyHandlerAsync.cs`
