# Tasks: DontAckAction

**Spec**: 0020-DontAckAction
**ADR**: [0038 - Don't Ack Action](../../docs/adr/0038-dont-ack-action.md)

## Prerequisites

- [x] Requirements approved
- [x] ADR 0038 accepted

## Tasks

### Phase 1: Core Pump Behavior (Reactor)

- [x] **TEST + IMPLEMENT: When a handler throws DontAckAction the Reactor does not acknowledge the message and continues the loop**
  - **USE COMMAND**: `/test-first when a handler throws DontAckAction the Reactor does not acknowledge the message and continues the loop`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/`
  - Test file: `When_a_handler_throws_dont_ack_action_should_not_acknowledge.cs`
  - Test should verify:
    - Handler throws `DontAckAction`
    - Message is NOT acknowledged on the channel
    - Unacceptable message count is incremented
    - Pump continues running (processes subsequent quit message)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `DontAckAction` exception in `src/Paramore.Brighter/Actions/DontAckAction.cs` (follow `RejectMessageAction` pattern with parameterless, reason, and reason+innerException constructors)
    - Add `DontAckDelay` property to `src/Paramore.Brighter.ServiceActivator/MessagePump.cs` (default `TimeSpan.FromSeconds(1)`)
    - Add `catch (DontAckAction)` block in `Reactor.cs` between `DeferMessageAction` and `RejectMessageAction` catches — increment unacceptable count, log inner exception if present, apply `DontAckDelay`, `continue`
    - Add `DontAckAction` to `TranslateMessage` `TargetInvocationException` unwrapping in `Reactor.cs`
    - Derive a type from `SpyCommandProcessor`, see `SpyRequeueCommandProcessor` for an example, that throws `DontAckAction` on Send

### Phase 2: Core Pump Behavior (Proactor)

- [x] **TEST + IMPLEMENT: When a handler throws DontAckAction the Proactor does not acknowledge the message and continues the loop**
  - **USE COMMAND**: `/test-first when a handler throws DontAckAction the Proactor does not acknowledge the message and continues the loop`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/`
  - Test file: `When_a_handler_throws_dont_ack_action_should_not_acknowledge_async.cs`
  - Test should verify:
    - Async handler throws `DontAckAction`
    - Message is NOT acknowledged on the async channel
    - Unacceptable message count is incremented
    - Pump continues running (processes subsequent quit message)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `catch (DontAckAction)` block in `Proactor.cs` between `DeferMessageAction` and `RejectMessageAction` catches — increment unacceptable count, log inner exception if present, `await Task.Delay(DontAckDelay, ct)`, `continue`
    - Add `DontAckAction` to `TranslateMessageAsync` `TargetInvocationException` unwrapping in `Proactor.cs`

### Phase 3: AggregateException Handling

- [x] **TEST + IMPLEMENT: When an AggregateException containing DontAckAction is thrown the Reactor does not acknowledge the message**
  - **USE COMMAND**: `/test-first when an AggregateException containing DontAckAction is thrown the Reactor does not acknowledge the message`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/`
  - Test file: `When_aggregate_exception_containing_dont_ack_action_should_not_acknowledge.cs`
  - Test should verify:
    - Handler throws `AggregateException` wrapping a `DontAckAction`
    - Message is NOT acknowledged
    - Unacceptable message count is incremented
    - Pump continues running
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `DontAckAction` detection in the `AggregateException` handler in `Reactor.cs` (add `dontAck` flag alongside existing `defer`, `reject`, `invalidMessage` flags)
    - After the foreach loop, add `if (dontAck)` block that logs, increments count, applies delay, and continues

- [x] **TEST + IMPLEMENT: When an AggregateException containing DontAckAction is thrown the Proactor does not acknowledge the message**
  - **USE COMMAND**: `/test-first when an AggregateException containing DontAckAction is thrown the Proactor does not acknowledge the message`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/`
  - Test file: `When_aggregate_exception_containing_dont_ack_action_should_not_acknowledge_async.cs`
  - Test should verify:
    - Async handler throws `AggregateException` wrapping a `DontAckAction`
    - Message is NOT acknowledged
    - Unacceptable message count is incremented
    - Pump continues running
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `DontAckAction` detection in the `AggregateException` handler in `Proactor.cs` (same pattern as Reactor)

### Phase 4: DontAckOnError Pipeline Attribute

- [ ] **TEST + IMPLEMENT: When DontAckOnError attribute is applied and the handler throws an exception the exception is wrapped in DontAckAction**
  - **USE COMMAND**: `/test-first when DontAckOnError attribute is applied and the handler throws the exception is wrapped in DontAckAction`
  - Test location: `tests/Paramore.Brighter.Core.Tests/DontAck/`
  - Test file: `When_handler_throws_exception_should_dont_ack_message.cs`
  - Test should verify:
    - Handler with `[DontAckOnError(step: 1)]` attribute that throws `InvalidOperationException`
    - `DontAckAction` is thrown from the pipeline
    - `DontAckAction.InnerException` is the original `InvalidOperationException`
    - `DontAckAction.Message` contains the original exception message
    - Handler was called (static flag)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `DontAckOnErrorAttribute` in `src/Paramore.Brighter/DontAck/Attributes/DontAckOnErrorAttribute.cs` (follow `RejectMessageOnErrorAttribute` pattern)
    - Create `DontAckOnErrorHandler<TRequest>` in `src/Paramore.Brighter/DontAck/Handlers/DontAckOnErrorHandler.cs` (follow `RejectMessageOnErrorHandler` pattern — catch Exception, throw `new DontAckAction(ex.Message, ex)`)

- [ ] **TEST + IMPLEMENT: When DontAckOnError async attribute is applied and the async handler throws an exception the exception is wrapped in DontAckAction**
  - **USE COMMAND**: `/test-first when DontAckOnError async attribute is applied and the async handler throws the exception is wrapped in DontAckAction`
  - Test location: `tests/Paramore.Brighter.Core.Tests/DontAck/`
  - Test file: `When_async_handler_throws_exception_should_dont_ack_message.cs`
  - Test should verify:
    - Async handler with `[DontAckOnErrorAsync(step: 1)]` attribute that throws `InvalidOperationException`
    - `DontAckAction` is thrown from the async pipeline
    - `DontAckAction.InnerException` is the original `InvalidOperationException`
    - Handler was called (static flag)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `DontAckOnErrorAsyncAttribute` in `src/Paramore.Brighter/DontAck/Attributes/DontAckOnErrorAsyncAttribute.cs`
    - Create `DontAckOnErrorHandlerAsync<TRequest>` in `src/Paramore.Brighter/DontAck/Handlers/DontAckOnErrorHandlerAsync.cs`

### Phase 5: FeatureSwitch DontAck Integration

- [ ] **TEST + IMPLEMENT: When a feature switch is off with dontAck true the handler throws DontAckAction instead of silently returning**
  - **USE COMMAND**: `/test-first when a feature switch is off with dontAck true the handler throws DontAckAction instead of silently returning`
  - Test location: `tests/Paramore.Brighter.Core.Tests/FeatureSwitch/`
  - Test file: `When_A_Handler_Is_Feature_Switch_Off_With_DontAck.cs`
  - Test should verify:
    - Handler with `[FeatureSwitch(typeof(MyHandler), FeatureSwitchStatus.Off, 1, dontAck: true)]`
    - Sending command throws `DontAckAction`
    - `DontAckAction.Message` contains the handler type name
    - Target handler was NOT called
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `dontAck` parameter to `FeatureSwitchAttribute` constructor (default `false`) in `src/Paramore.Brighter/FeatureSwitch/Attributes/FeatureSwitchAttribute.cs`
    - Pass `_dontAck` through `InitializerParams()`
    - Update `FeatureSwitchHandler.InitializeFromAttributeParams()` to read the dontAck flag
    - Update `FeatureSwitchHandler.Handle()`: when off and `_dontAck` is true, throw `DontAckAction`
    - Existing tests with `dontAck: false` (default) must continue to pass unchanged

- [ ] **TEST + IMPLEMENT: When an async feature switch is off with dontAck true the handler throws DontAckAction instead of silently returning**
  - **USE COMMAND**: `/test-first when an async feature switch is off with dontAck true the handler throws DontAckAction instead of silently returning`
  - Test location: `tests/Paramore.Brighter.Core.Tests/FeatureSwitch/`
  - Test file: `When_A_Handler_Is_Feature_Switch_Off_With_DontAck_Async.cs`
  - Test should verify:
    - Async handler with `[FeatureSwitchAsync(typeof(MyHandler), FeatureSwitchStatus.Off, 1, dontAck: true)]`
    - Sending async command throws `DontAckAction`
    - `DontAckAction.Message` contains the handler type name
    - Target handler was NOT called
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `dontAck` parameter to `FeatureSwitchAsyncAttribute` constructor (default `false`)
    - Pass `_dontAck` through `InitializerParams()`
    - Update `FeatureSwitchHandlerAsync.InitializeFromAttributeParams()` to read the dontAck flag
    - Update `FeatureSwitchHandlerAsync.HandleAsync()`: when off and `_dontAck` is true, throw `DontAckAction`

## Verification

After all tasks complete:
- [ ] Run existing FeatureSwitch tests to confirm backward compatibility (dontAck defaults to false)
- [ ] Run existing defer/reject message dispatch tests to confirm no regression
- [ ] Run full test suite: `dotnet test`
