# Implementation Tasks

This document outlines the tasks for implementing the Reject Message On Error Handler feature as specified in the requirements and ADR 0037.

## TDD Workflow

Each task follows a strict Test-Driven Development workflow:

1. **RED**: Write a failing test that specifies the behavior
2. **APPROVAL**: Get approval for the test before proceeding
3. **GREEN**: Implement minimum code to make the test pass
4. **REFACTOR**: Improve design while keeping tests green

Tests are written in `tests/Paramore.Brighter.Core.Tests/Reject/`.

## Task List

### Phase 1: Synchronous Handler ✅

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorAttribute returns correct handler type**
  - **USE COMMAND**: `/test-first when getting handler type from RejectMessageOnErrorAttribute should return RejectMessageOnErrorHandler`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_getting_handler_type_from_reject_message_on_error_attribute.cs`
  - Test should verify:
    - Attribute can be instantiated with a step parameter
    - `GetHandlerType()` returns `typeof(RejectMessageOnErrorHandler<>)`
    - Attribute timing is `HandlerTiming.Before`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Reject/Attributes/RejectMessageOnErrorAttribute.cs`
    - Inherit from `RequestHandlerAttribute`
    - Constructor accepts `int step` and passes to base with `HandlerTiming.Before`
    - `GetHandlerType()` returns `typeof(RejectMessageOnErrorHandler<>)`

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorHandler catches exception and throws RejectMessageAction**
  - **USE COMMAND**: `/test-first when handler throws exception should catch and throw RejectMessageAction`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_handler_throws_exception_should_reject_message.cs`
  - Test should verify:
    - Create a handler that throws `InvalidOperationException`
    - Decorate it with `[RejectMessageOnError(step: 1)]`
    - Execute via `CommandProcessor.Send()`
    - Verify `RejectMessageAction` is thrown
    - Verify `RejectMessageAction.InnerException` is the original exception
    - Verify `RejectMessageAction.Message` contains the original exception message
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Reject/Handlers/RejectMessageOnErrorHandler.cs`
    - Inherit from `RequestHandler<TRequest>`
    - Override `Handle()` with try/catch wrapping `base.Handle()`
    - Catch `Exception`, log it, throw `new RejectMessageAction(ex.Message, ex)`

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorHandler passes through when no exception**
  - **USE COMMAND**: `/test-first when handler succeeds should pass through without rejection`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_handler_succeeds_should_not_reject_message.cs`
  - Test should verify:
    - Create a handler that completes successfully
    - Decorate it with `[RejectMessageOnError(step: 1)]`
    - Execute via `CommandProcessor.Send()`
    - Verify no exception is thrown
    - Verify handler was called (via static flag)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify existing implementation handles success path correctly
    - No new code needed if previous test implementation is correct

### Phase 2: Asynchronous Handler ✅

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorAsyncAttribute returns correct handler type**
  - **USE COMMAND**: `/test-first when getting handler type from RejectMessageOnErrorAsyncAttribute should return RejectMessageOnErrorHandlerAsync`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_getting_handler_type_from_reject_message_on_error_async_attribute.cs`
  - Test should verify:
    - Attribute can be instantiated with a step parameter
    - `GetHandlerType()` returns `typeof(RejectMessageOnErrorHandlerAsync<>)`
    - Attribute timing is `HandlerTiming.Before`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Reject/Attributes/RejectMessageOnErrorAsyncAttribute.cs`
    - Inherit from `RequestHandlerAttribute`
    - Constructor accepts `int step` and passes to base with `HandlerTiming.Before`
    - `GetHandlerType()` returns `typeof(RejectMessageOnErrorHandlerAsync<>)`

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorHandlerAsync catches exception and throws RejectMessageAction**
  - **USE COMMAND**: `/test-first when async handler throws exception should catch and throw RejectMessageAction`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_async_handler_throws_exception_should_reject_message.cs`
  - Test should verify:
    - Create an async handler that throws `InvalidOperationException`
    - Decorate it with `[RejectMessageOnErrorAsync(step: 1)]`
    - Execute via `CommandProcessor.SendAsync()`
    - Verify `RejectMessageAction` is thrown
    - Verify `RejectMessageAction.InnerException` is the original exception
    - Verify `RejectMessageAction.Message` contains the original exception message
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter/Reject/Handlers/RejectMessageOnErrorHandlerAsync.cs`
    - Inherit from `RequestHandlerAsync<TRequest>`
    - Override `HandleAsync()` with try/catch wrapping `await base.HandleAsync()`
    - Catch `Exception`, log it, throw `new RejectMessageAction(ex.Message, ex)`

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorHandlerAsync passes through when no exception**
  - **USE COMMAND**: `/test-first when async handler succeeds should pass through without rejection`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_async_handler_succeeds_should_not_reject_message.cs`
  - Test should verify:
    - Create an async handler that completes successfully
    - Decorate it with `[RejectMessageOnErrorAsync(step: 1)]`
    - Execute via `CommandProcessor.SendAsync()`
    - Verify no exception is thrown
    - Verify handler was called (via static flag)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify existing implementation handles success path correctly
    - No new code needed if previous test implementation is correct

### Phase 3: Pipeline Ordering Verification ✅

- [x] **TEST + IMPLEMENT: RejectMessageOnErrorHandler at step 0 catches exceptions from inner handlers**
  - **USE COMMAND**: `/test-first when at step 0 should catch exceptions from higher step handlers`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Reject`
  - Test file: `When_reject_handler_at_step_zero_catches_inner_exceptions.cs`
  - Test should verify:
    - Handler with `[RejectMessageOnError(step: 0)]` and another policy at higher step
    - Inner handler throws exception
    - Exception is caught by RejectMessageOnErrorHandler (outermost)
    - `RejectMessageAction` is thrown with original exception details
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This tests existing behavior - no new code needed
    - Verifies the step ordering works correctly

### Phase 4: Regression Testing ✅

- [x] **Run existing Brighter.Core test suite**
  - Execute: `dotnet test tests/Paramore.Brighter.Core.Tests/`
  - Verify all existing tests pass
  - Verify no breaking changes to existing behavior
  - Result: 519 passed, 0 failed, 7 skipped (net9.0 + net10.0)

### Phase 5: Documentation ✅

- [x] **Add XML documentation to all new types**
  - Add XML docs to `RejectMessageOnErrorAttribute`
  - Add XML docs to `RejectMessageOnErrorAsyncAttribute`
  - Add XML docs to `RejectMessageOnErrorHandler<T>`
  - Add XML docs to `RejectMessageOnErrorHandlerAsync<T>`
  - Include usage examples in documentation
  - Recommend step 0 for outermost placement

## Task Dependencies

Each phase builds on the previous:

```
Phase 1: Synchronous Handler (attribute + handler)
    ↓
Phase 2: Asynchronous Handler (attribute + handler)
    ↓
Phase 3: Pipeline Ordering Verification
    ↓
Phase 4: Regression Testing
    ↓
Phase 5: Documentation
```

Each TEST task must be approved before implementation begins.

## Risk Mitigation

- **Risk**: Incorrect pipeline ordering causes exceptions to bypass handler
  - **Mitigation**: Test with step 0 (lowest/outermost) to verify catch-all behavior

- **Risk**: Breaking changes to existing fallback behavior
  - **Mitigation**: Run existing FallbackPolicy tests to verify no regressions

- **Risk**: Original exception details lost in translation
  - **Mitigation**: Preserve exception message and inner exception in RejectMessageAction

## Notes

- No `InitializerParams()` override needed - handler has no configuration
- Always use `HandlerTiming.Before` - must wrap subsequent handlers
- Logging should use structured logging via source generators
- Follow MIT license header convention used in codebase
