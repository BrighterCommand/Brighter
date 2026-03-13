# Tasks: Fix Resilience Pipeline Reuse (#4042)

**Spec**: 0024-resilience_pipeline_bug
**ADR**: 0053
**Branch**: `resilience_pipeline`

## Overview

Fix `ResilienceExceptionPolicyHandlerAsync<TRequest>` to respect the `UseTypePipeline` flag, aligning it with the sync handler's behavior. This enables non-generic `ResiliencePipeline` instances to be shared across command handler types.

## Phase 1: Tidy — Align Async Handler Structure

- [x] **Task 1: TIDY — Add non-generic pipeline field and UseTypePipeline branching to async handler**
  - Structural change only — no behavioral change yet (existing tests still pass)
  - In `ResilienceExceptionPolicyHandlerAsync.cs`:
    - Add `private ResiliencePipeline _pipeline = ResiliencePipeline.Empty;`
    - Rename `_pipeline` → `_typePipeline` (it holds `ResiliencePipeline<TRequest>`)
    - In `InitializeFromAttributeParams`: read `UseTypePipeline` from `initializerList[1]` and branch
    - In `HandleAsync`: check `_pipeline != ResiliencePipeline.Empty` first, fall through to `_typePipeline`
  - Mirror the sync handler's pattern exactly
  - Run existing tests to verify no regressions

## Phase 2: Test + Fix Non-Generic Async Pipeline Path

- [x] **Task 2: TEST + IMPLEMENT — Async handler executes non-generic resilience pipeline**
  - **USE COMMAND**: `/test-first when sending an async command with a non-generic resilience pipeline then the handler executes through the pipeline`
  - Test location: `tests/Paramore.Brighter.Core.Tests/ExceptionPolicy/`
  - Test file: `When_Sending_An_Async_Command_That_Passes_ResiliencePipeline_Check.cs`
  - Test should verify:
    - A non-generic `ResiliencePipeline` is registered via `registry.TryAddBuilder("key", ...)`
    - An async handler with `[UseResiliencePipelineAsync("key", 1)]` (default `UseTypePipeline = false`) is used
    - `SendAsync` succeeds without exception
    - The handler receives the command
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - The tidy in Task 1 should already make this pass — this test characterizes the fix

- [x] **Task 3: TEST + IMPLEMENT — Async handler executes typed resilience pipeline**
  - **USE COMMAND**: `/test-first when sending an async command with a typed resilience pipeline then the handler executes through the typed pipeline`
  - Test location: `tests/Paramore.Brighter.Core.Tests/ExceptionPolicy/`
  - Test file: `When_Sending_An_Async_Command_That_Passes_TypeResiliencePipeline_Check.cs`
  - Test should verify:
    - A generic `ResiliencePipeline<MyCommand>` is registered via `registry.TryAddBuilder<MyCommand>("key", ...)`
    - An async handler with `[UseResiliencePipelineAsync("key", 1, UseTypePipeline = true)]` is used
    - `SendAsync` succeeds without exception
    - The handler receives the command
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This should already pass after Task 1 (existing typed path preserved) — this test characterizes the typed async path

- [x] **Task 4: TEST + IMPLEMENT — Non-generic pipeline is reusable across different command types**
  - **USE COMMAND**: `/test-first when multiple async handlers share the same non-generic resilience pipeline then all handlers execute successfully`
  - Test location: `tests/Paramore.Brighter.Core.Tests/ExceptionPolicy/`
  - Test file: `When_Sending_Different_Async_Commands_That_Share_A_ResiliencePipeline.cs`
  - Test should verify:
    - A single non-generic `ResiliencePipeline` registered with key `"SharedRetryPolicy"`
    - Two different command types (e.g., `MyCommand` and `MyEvent`) each have async handlers using `[UseResiliencePipelineAsync("SharedRetryPolicy", 1)]`
    - Both `SendAsync` calls succeed without `KeyNotFoundException`
    - Both handlers receive their respective commands
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Should pass after Task 1 — this test proves the bug from #4042 is fixed

## Phase 3: Regression Verification

- [x] **Task 5: Run full exception policy test suite**
  - Run: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~ExceptionPolicy"`
  - All existing tests must pass
  - Verify no regressions from the async handler changes
