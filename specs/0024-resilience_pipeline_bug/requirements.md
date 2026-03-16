# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4042

## Problem Statement

As a developer using Brighter, I would like to register a single non-generic `ResiliencePipeline` and reuse it across multiple command handlers, so that I don't have to register duplicate pipeline builders for every command type.

Currently, Brighter's resilience pipeline resolution has two bugs that prevent pipeline reuse:

1. **Async handler ignores `UseTypePipeline` flag**: `ResilienceExceptionPolicyHandlerAsync<TRequest>.InitializeFromAttributeParams()` always calls `GetPipeline<TRequest>(key)` (the generic/typed lookup) regardless of the `UseTypePipeline` attribute setting. It never reads `initializerList[1]` and has no non-generic `_pipeline` field. This means async handlers always require a per-command-type generic pipeline registration.

2. **No fallback from generic to non-generic lookup**: When `UseTypePipeline = true` is set on the sync handler (`ResilienceExceptionPolicyHandler`), it calls `GetPipeline<TRequest>(key)` which throws `KeyNotFoundException` if only a non-generic pipeline was registered with that key. There is no fallback to check for a non-generic pipeline.

The reporter's scenario: they register a non-generic `ResiliencePipeline` (e.g., a timeout pipeline named `"MyNonGenericTimeoutPipeline"`) and reference it from a handler via `[UseResiliencePipeline("MyNonGenericTimeoutPipeline", 1)]`. The async handler always uses the generic lookup path, causing `KeyNotFoundException`.

## Proposed Solution

Fix the resilience pipeline handlers so that:

1. Both sync and async handlers respect the `UseTypePipeline` flag
2. When `UseTypePipeline = false` (the default), use `GetPipeline(key)` to get a non-generic pipeline that can be shared across command types
3. When `UseTypePipeline = true`, use `GetPipeline<TRequest>(key)` to get a type-specific pipeline
4. Optionally: when `UseTypePipeline = true` and the generic lookup fails, fall back to the non-generic lookup (graceful degradation)

## Requirements

### Functional Requirements

- **FR1**: `ResilienceExceptionPolicyHandlerAsync` must respect the `UseTypePipeline` attribute parameter, matching the behavior of the sync handler
- **FR2**: When `UseTypePipeline = false` (default), both sync and async handlers must look up a non-generic `ResiliencePipeline` via `GetPipeline(key)`
- **FR3**: When `UseTypePipeline = true`, both sync and async handlers must look up a generic `ResiliencePipeline<TRequest>` via `GetPipeline<TRequest>(key)`
- **FR4**: A non-generic `ResiliencePipeline` registered once must be usable across multiple different command handler types without re-registration

### Non-functional Requirements

- No breaking changes to existing API surface
- No performance regression in pipeline resolution
- Existing tests for typed pipelines must continue to pass

### Constraints and Assumptions

- The `UseResiliencePipelineAttribute` and `UseResiliencePipelineAsyncAttribute` already pass `UseTypePipeline` via `InitializerParams()` — no attribute changes needed
- Polly's `ResiliencePipelineRegistry<string>` supports both `GetPipeline(key)` and `GetPipeline<T>(key)` natively
- The sync handler (`ResilienceExceptionPolicyHandler`) already handles both paths correctly; it serves as the reference implementation

### Out of Scope

- Changes to the `UseResiliencePipelineAttribute` or `UseResiliencePipelineAsyncAttribute` classes
- Adding new attribute types
- Changes to Polly's registry behavior
- Automatic fallback from generic to non-generic lookup (nice-to-have, not required)

## Acceptance Criteria

- **AC1**: A handler using `[UseResiliencePipeline("key", 1)]` (default `UseTypePipeline = false`) works with a non-generic `ResiliencePipeline` registered via `registry.TryAddBuilder("key", ...)` — for both sync and async handlers
- **AC2**: A handler using `[UseResiliencePipeline("key", 1, UseTypePipeline = true)]` works with a generic `ResiliencePipeline<TRequest>` registered via `registry.TryAddBuilder<TRequest>("key", ...)` — for both sync and async handlers
- **AC3**: The same non-generic pipeline key can be used by handlers for different command types without `KeyNotFoundException`
- **AC4**: All existing resilience pipeline tests continue to pass
- **AC5**: New tests cover the async handler's non-generic pipeline path

## Additional Context

- Reporter's reproduction repo: https://github.com/ravriel/BrighterResilience
- Brighter version affected: 10.3.0
- The sync handler at `ResilienceExceptionPolicyHandler.cs:56-79` is the correct reference implementation
- The async handler at `ResilienceExceptionPolicyHandlerAsync.cs:59-70` is the primary bug location
