# Tasks: Defer Message On Error Backstop Handler

**Spec**: 0022-Defer-Message-Action-Backstop-Handler
**ADR**: [0052 — Defer Message On Error Backstop Handler](../../docs/adr/0052-defer-message-on-error-backstop-handler.md)
**Branch**: `error_examples` (or new feature branch)

## Prerequisites

- [x] Requirements approved
- [x] ADR 0052 accepted

## Task 1: Structural — Extend `DeferMessageAction` with constructors and `Delay` property

This is a **tidy-first structural change** — no new behavior, just adding constructors and a property to the existing exception class to match `RejectMessageAction` and `DontAckAction`.

- **File**: `src/Paramore.Brighter/Actions/DeferMessageAction.cs`
- Changes:
  - Add parameterless constructor
  - Add `(string? reason)` constructor
  - Add `(string? reason, Exception? innerException)` constructor
  - Add `(string? reason, Exception? innerException, int delayMilliseconds)` constructor
  - Add `TimeSpan? Delay { get; }` property (nullable — `null` means use subscription default)
- **No tests needed** — structural change only, backward compatible (existing `new DeferMessageAction()` still works)
- Commit separately as a structural (tidy) change

## Task 2: TEST + IMPLEMENT — Sync handler throws `DeferMessageAction` when exception occurs

- [ ] **TEST + IMPLEMENT: Sync defer handler catches exceptions and throws DeferMessageAction**
  - **USE COMMAND**: `/test-first when sync handler throws exception should defer message with delay`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Defer/`
  - Test file: `When_handler_throws_exception_should_defer_message.cs`
  - Test doubles needed:
    - `tests/Paramore.Brighter.Core.Tests/Defer/TestDoubles/MyFailingDeferHandler.cs` — decorated with `[DeferMessageOnError(step: 1, delayMilliseconds: 5000)]`, throws `InvalidOperationException`
  - Test should verify:
    - Handler was invoked (static `HandlerCalled` flag)
    - `DeferMessageAction` is thrown (not the original `InvalidOperationException`)
    - Original exception message is preserved on `DeferMessageAction.Message`
    - Original exception is preserved as `InnerException` (type `InvalidOperationException`)
    - `DeferMessageAction.Delay` equals `TimeSpan.FromMilliseconds(5000)` (delay from attribute flows through)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - Create `src/Paramore.Brighter/Defer/Attributes/DeferMessageOnErrorAttribute.cs`
      - Constructor: `(int step, int delayMilliseconds = 0)`, base `(step, HandlerTiming.Before)`
      - `InitializerParams()` returns `[_delayMilliseconds]`
      - `GetHandlerType()` returns `typeof(DeferMessageOnErrorHandler<>)`
    - Create `src/Paramore.Brighter/Defer/Handlers/DeferMessageOnErrorHandler.cs`
      - `InitializeFromAttributeParams` reads `_delayMilliseconds` from `initializerList[0]`
      - `Handle()`: try `base.Handle(request)`, catch `Exception` → log → throw `new DeferMessageAction(ex.Message, ex, _delayMilliseconds)`
      - Source-generated `Log.UnhandledExceptionDeferringMessage` partial method

## Task 3: TEST + IMPLEMENT — Async handler throws `DeferMessageAction` when exception occurs

- [ ] **TEST + IMPLEMENT: Async defer handler catches exceptions and throws DeferMessageAction**
  - **USE COMMAND**: `/test-first when async handler throws exception should defer message with delay`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Defer/`
  - Test file: `When_async_handler_throws_exception_should_defer_message.cs`
  - Test doubles needed:
    - `tests/Paramore.Brighter.Core.Tests/Defer/TestDoubles/MyFailingDeferHandlerAsync.cs` — decorated with `[DeferMessageOnErrorAsync(step: 1, delayMilliseconds: 5000)]`, throws `InvalidOperationException`
  - Test should verify:
    - Handler was invoked (static `HandlerCalled` flag)
    - `DeferMessageAction` is thrown (not the original `InvalidOperationException`)
    - Original exception message is preserved on `DeferMessageAction.Message`
    - Original exception is preserved as `InnerException` (type `InvalidOperationException`)
    - `DeferMessageAction.Delay` equals `TimeSpan.FromMilliseconds(5000)` (delay from attribute flows through)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - Create `src/Paramore.Brighter/Defer/Attributes/DeferMessageOnErrorAsyncAttribute.cs`
      - Same as sync but `GetHandlerType()` returns `typeof(DeferMessageOnErrorHandlerAsync<>)`
    - Create `src/Paramore.Brighter/Defer/Handlers/DeferMessageOnErrorHandlerAsync.cs`
      - `HandleAsync()`: try `await base.HandleAsync(command, cancellationToken)`, catch `Exception` → log → throw `new DeferMessageAction(ex.Message, ex, _delayMilliseconds)`

## Task 4: TEST + IMPLEMENT — Sync handler passes through when no exception occurs

- [ ] **TEST + IMPLEMENT: Sync defer handler passes through transparently on success**
  - **USE COMMAND**: `/test-first when sync handler succeeds should not defer message`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Defer/`
  - Test file: `When_handler_succeeds_should_not_defer_message.cs`
  - Test doubles needed:
    - `tests/Paramore.Brighter.Core.Tests/Defer/TestDoubles/MySucceedingDeferHandler.cs` — decorated with `[DeferMessageOnError(step: 1, delayMilliseconds: 5000)]`, completes successfully
  - Test should verify:
    - No exception is thrown
    - Handler was invoked (static `HandlerCalled` flag)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - Create `MySucceedingDeferHandler` test double
    - No production code changes needed (handler `try { base.Handle() }` already handles success path)

## Task 5: TEST + IMPLEMENT — Async handler passes through when no exception occurs

- [ ] **TEST + IMPLEMENT: Async defer handler passes through transparently on success**
  - **USE COMMAND**: `/test-first when async handler succeeds should not defer message`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Defer/`
  - Test file: `When_async_handler_succeeds_should_not_defer_message.cs`
  - Test doubles needed:
    - `tests/Paramore.Brighter.Core.Tests/Defer/TestDoubles/MySucceedingDeferHandlerAsync.cs` — decorated with `[DeferMessageOnErrorAsync(step: 1, delayMilliseconds: 5000)]`, completes successfully
  - Test should verify:
    - No exception is thrown
    - Handler was invoked (static `HandlerCalled` flag)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - Create `MySucceedingDeferHandlerAsync` test double
    - No production code changes needed

## Task 6: TEST + IMPLEMENT — Attribute returns correct handler type and configuration

- [ ] **TEST + IMPLEMENT: DeferMessageOnError attributes return correct handler types**
  - **USE COMMAND**: `/test-first when getting handler type from defer message on error attributes should return correct configuration`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Defer/`
  - Test file: `When_getting_handler_type_from_defer_message_on_error_attribute.cs`
  - Test should verify (sync attribute):
    - `GetHandlerType()` returns `typeof(DeferMessageOnErrorHandler<>)`
    - `Timing` is `HandlerTiming.Before`
    - `Step` preserves the specified value
    - `InitializerParams()` returns array containing the delay value
  - Test should verify (async attribute):
    - `GetHandlerType()` returns `typeof(DeferMessageOnErrorHandlerAsync<>)`
    - `Timing` is `HandlerTiming.Before`
    - `Step` preserves the specified value
    - `InitializerParams()` returns array containing the delay value
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation: No additional production code needed (attributes already created in Task 2/3)

## Task 7: TEST + IMPLEMENT — Pump reads delay from `DeferMessageAction` and passes to requeue

- [ ] **TEST + IMPLEMENT: Message pump uses DeferMessageAction delay when requeuing**
  - **USE COMMAND**: `/test-first when pump catches DeferMessageAction with delay should requeue with that delay`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/`
  - Test file: `When_a_command_handler_throws_a_defer_message_with_delay_Then_message_is_requeued_with_delay.cs`
  - Test should verify:
    - When handler throws `DeferMessageAction` with a `Delay` value, the pump passes that delay to `Channel.RequeueAsync`
    - When handler throws `DeferMessageAction` without a `Delay` (null), the pump falls back to subscription `RequeueDelay`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation:
    - In `Proactor.cs`: Change `catch (DeferMessageAction)` to `catch (DeferMessageAction deferAction)`, extract `deferAction.Delay`, pass to `RequeueMessage`
    - In `Reactor.cs`: Same change for the sync path
    - In both: Add optional `TimeSpan? delay = null` parameter to `RequeueMessage`, use `delay ?? RequeueDelay` when calling `Channel.Requeue`/`Channel.RequeueAsync`

## Task 8: Build verification

- [ ] **Build and run all core tests**
  - Run `dotnet build src/Paramore.Brighter/Paramore.Brighter.csproj` — must compile
  - Run `dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj` — all tests pass
  - Run existing DeferMessageAction pump tests to verify no regressions:
    - `When_a_command_handler_throws_a_defer_message_Then_message_is_requeued_until_rejected`
    - `When_a_command_handler_throws_a_defer_message_Then_message_is_requeued_until_rejected_async`

## Task Summary

| # | Type | Description | Depends On |
|---|------|-------------|------------|
| 1 | Structural | Extend `DeferMessageAction` constructors + `Delay` property | — |
| 2 | Test + Implement | Sync handler catches exceptions → `DeferMessageAction` | 1 |
| 3 | Test + Implement | Async handler catches exceptions → `DeferMessageAction` | 1 |
| 4 | Test + Implement | Sync handler passes through on success | 2 |
| 5 | Test + Implement | Async handler passes through on success | 3 |
| 6 | Test + Implement | Attribute returns correct handler type + config | 2, 3 |
| 7 | Test + Implement | Pump reads delay from exception and passes to requeue | 1 |
| 8 | Verification | Build + run all core tests | 1–7 |
