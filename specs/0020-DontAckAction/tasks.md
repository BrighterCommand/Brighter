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

- [x] **TEST + IMPLEMENT: When DontAckOnError attribute is applied and the handler throws an exception the exception is wrapped in DontAckAction**
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

- [x] **TEST + IMPLEMENT: When DontAckOnError async attribute is applied and the async handler throws an exception the exception is wrapped in DontAckAction**
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

- [x] **TEST + IMPLEMENT: When a feature switch is off with dontAck true the handler throws DontAckAction instead of silently returning**
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

- [x] **TEST + IMPLEMENT: When an async feature switch is off with dontAck true the handler throws DontAckAction instead of silently returning**
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

### Phase 6: Core Nack Infrastructure

- [x] **TEST + IMPLEMENT: When the Reactor catches DontAckAction it nacks the message on the channel making it available for redelivery**
  - **USE COMMAND**: `/test-first when the Reactor catches DontAckAction it nacks the message on the channel making it available for redelivery`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/`
  - Test file: `When_a_handler_throws_dont_ack_action_should_nack_the_message.cs`
  - Test should verify:
    - Handler throws `DontAckAction`
    - `Channel.Nack(message)` is called (message is nacked)
    - Message is NOT acknowledged
    - Message is re-enqueued to the bus (available for redelivery)
    - Unacceptable message count is incremented
    - Pump continues running (processes subsequent quit message)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `void Nack(Message message)` to `IAmAMessageConsumerSync`
    - Add `Task NackAsync(Message message, CancellationToken cancellationToken = default)` to `IAmAMessageConsumerAsync`
    - Add `void Nack(Message message)` to `IAmAChannelSync`
    - Add `Task NackAsync(Message message, CancellationToken cancellationToken = default)` to `IAmAChannelAsync`
    - Implement `Nack` in `Channel` (delegate to `_messageConsumer.Nack(message)`)
    - Implement `NackAsync` in `ChannelAsync` (delegate to `_messageConsumer.NackAsync(message, ct)`)
    - Implement `Nack` in `InMemoryMessageConsumer`: remove from `_lockedMessages`, re-enqueue to `_bus`
    - Implement `NackAsync` in `InMemoryMessageConsumer`: call sync `Nack` (in-memory is not truly async)
    - Update `Reactor.cs` DontAckAction catch block: add `Channel.Nack(message)` before the delay
    - Update `Reactor.cs` AggregateException DontAckAction handling: add `Channel.Nack(message)` before the delay

- [x] **TEST + IMPLEMENT: When the Proactor catches DontAckAction it nacks the message on the channel making it available for redelivery**
  - **USE COMMAND**: `/test-first when the Proactor catches DontAckAction it nacks the message on the channel making it available for redelivery`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/`
  - Test file: `When_a_handler_throws_dont_ack_action_should_nack_the_message_async.cs`
  - Test should verify:
    - Async handler throws `DontAckAction`
    - `Channel.NackAsync(message)` is called (message is nacked)
    - Message is NOT acknowledged
    - Message is re-enqueued to the bus (available for redelivery)
    - Unacceptable message count is incremented
    - Pump continues running (processes subsequent quit message)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `Proactor.cs` DontAckAction catch block: add `await Channel.NackAsync(message, ct)` before the delay
    - Update `Proactor.cs` AggregateException DontAckAction handling: add `await Channel.NackAsync(message, ct)` before the delay

### Phase 7: Queue Transport Nack Implementations

- [x] **IMPLEMENT: RabbitMQ consumer Nack calls BasicNack with requeue true**
  - Sync consumer: `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs`
    - `Nack(Message message)`: call `Channel.BasicNack(message.DeliveryTag, multiple: false, requeue: true)`
  - Async consumer: `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`
    - `NackAsync(Message message, CancellationToken ct)`: call `Channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true)`
  - Note: `BasicNack` is already used in `PullConsumer.cs` for shutdown; this follows the same pattern

- [x] **IMPLEMENT: SQS consumer Nack sets visibility timeout to zero**
  - Consumer: `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs`
    - `NackAsync(Message message, CancellationToken ct)`: call `ChangeMessageVisibilityAsync` with `VisibilityTimeout = 0` using the receipt handle from `message.Header.Bag["ReceiptHandle"]`
    - `Nack(Message message)`: call async version synchronously (following existing pattern in the consumer)

- [x] **IMPLEMENT: Azure Service Bus consumer Nack calls AbandonMessageAsync**
  - Consumer: `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs`
    - `NackAsync(Message message, CancellationToken ct)`: call `ServiceBusReceiver.AbandonMessageAsync(lockToken)` using the lock token from `message.Header.Bag`
    - `Nack(Message message)`: call async version synchronously (following existing pattern in the consumer)

### Phase 8: Stream/Other Transport Nack Implementations (No-op)

- [x] **IMPLEMENT: Stream and pub/sub transport consumers implement Nack as no-op**
  - All these transports implement `Nack` / `NackAsync` as empty methods (no-op):
    - `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs` — stream; don't commit offset is sufficient
    - `src/Paramore.Brighter.MessagingGateway.Redis/RedisMessageConsumer.cs` — LPOP is destructive; cannot un-pop
    - `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs` — pub/sub; no acknowledgment concept
    - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs` — stream; don't ack is sufficient

## Verification

### Phases 1-5 (Complete)
- [x] Run existing FeatureSwitch tests to confirm backward compatibility (dontAck defaults to false)
- [x] Run existing defer/reject message dispatch tests to confirm no regression
- [x] Run full test suite: `dotnet test`

### Phase 6-8 (Transport Nack)
- [x] Run Phase 6 Reactor/Proactor nack tests
- [x] Run existing DontAckAction tests to confirm no regression (Phases 1-5 behavior preserved)
- [x] Run existing defer/reject message dispatch tests to confirm no regression
- [x] Run full test suite: `dotnet test` (536 tests passed)
