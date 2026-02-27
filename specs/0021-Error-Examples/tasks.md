# Tasks — Error Handling Example Applications

**Spec**: 0021-Error-Examples
**Design**: [ADR 0051](../../docs/adr/0051-error-handling-examples.md)
**Reference Sample**: `samples/TaskQueue/KafkaTaskQueueWithDLQ/`

## Overview

Five new sample applications plus an update to the existing Kafka DLQ sample. Each sample demonstrates one error handling action for one transport. Tasks are ordered so that each new sample builds on patterns established by the previous one.

**Key configuration note**: All consumer subscriptions must set `unacceptableMessageLimitWindow: TimeSpan.Zero`. Because these samples intentionally produce errors, the unacceptable message count would accumulate and eventually shut down the message pump. Setting the window to zero causes the count to reset immediately on every pump cycle, preventing the sample from exiting. A code comment should explain this to users.

---

## Phase 1: Update Existing Kafka RejectOnError Sample

- [x] **1. Update KafkaTaskQueueWithDLQ subscriptions to set unacceptableMessageLimitWindow**
  - In `samples/TaskQueue/KafkaTaskQueueWithDLQ/GreetingsReceiverConsole/Program.cs`:
    - Add `unacceptableMessageLimitWindow: TimeSpan.Zero` to the `KafkaSubscription<GreetingEvent>` constructor
    - Add a comment above the subscription explaining: this sample intentionally throws on every 5th message; setting the window to zero resets the unacceptable message count on every pump cycle so the pump does not shut down
  - In `samples/TaskQueue/KafkaTaskQueueWithDLQ/DlqConsole/Program.cs`:
    - Add `unacceptableMessageLimitWindow: TimeSpan.Zero` to the DLQ subscription
    - Add the same explanatory comment
  - Verify: `dotnet build` succeeds for all projects in `samples/TaskQueue/KafkaTaskQueueWithDLQ/`

---

## Phase 2: RabbitMQ RejectOnError with DLQ

- [x] **2. RMQTaskQueueWithDLQ sample demonstrates rejected messages routed to a dead letter queue on RabbitMQ**
  - Create `samples/TaskQueue/RMQTaskQueueWithDLQ/` with the standard structure:
    - `Greetings/` class library with:
      - `Ports/Commands/GreetingEvent.cs` — inherits `Event`, `Greeting` string property, `Id.Random()`
      - `Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — `[RejectMessageOnErrorAsync(step: 0)]`, throws `InvalidOperationException` on every 5th message (static counter with `Interlocked.Increment`)
      - `Ports/Mappers/GreetingEventMessageMapperAsync.cs` — `System.Text.Json` via `JsonSerialisationOptions.Options`
    - `GreetingsSender/` console app with:
      - `Program.cs` — `RmqProducerRegistryFactory`, `amqp://guest:guest@localhost:5672`, exchange `paramore.brighter.exchange`, `InMemorySchedulerFactory`
      - `TimedMessageGenerator.cs` — `IHostedService` posting `GreetingEvent` every 500ms
    - `GreetingsReceiverConsole/` console app with:
      - `Program.cs` — `RmqSubscription<GreetingEvent>` with `MessagePumpType.Proactor`, `isDurable: true`, `highAvailability: true`, `deadLetterChannelName` and `deadLetterRoutingKey` configured, `unacceptableMessageLimitWindow: TimeSpan.Zero` with comment
    - `DlqConsole/` console app with:
      - `Program.cs` — `RmqSubscription<GreetingEvent>` subscribing to the DLQ channel, `unacceptableMessageLimitWindow: TimeSpan.Zero` with comment
      - `DlqGreetingEventHandlerAsync.cs` — displays rejection metadata from `Context.Bag` (OriginalTopic, RejectionReason)
    - `README.md` following the standard structure (What This Demonstrates, How It Works, Prerequisites, Running the Sample with `docker-compose-rmq.yaml`, Expected Output, Key Code)
  - All `.csproj` files target `net9.0` with project references (not NuGet), MIT license headers on all `.cs` files
  - Verify: `dotnet build` succeeds for all four projects

---

## Phase 3: Kafka DeferOnError

- [x] **3. KafkaDeferOnError sample demonstrates deferred messages requeued with delay and eventual success on Kafka**
  - Create `samples/TaskQueue/KafkaDeferOnError/` with:
    - `Greetings/` class library with:
      - `Ports/Commands/GreetingEvent.cs` — same as other samples
      - `Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — **no error attribute** (throws `DeferMessageAction` directly); static counter + static `ConcurrentDictionary<string, int>` tracking retry count per message ID; every 3rd message defers on first 2 attempts, succeeds on 3rd; comment explaining: `DeferMessageOnErrorAttribute` does not exist yet, so we throw `DeferMessageAction` directly to demonstrate the requeue behavior
      - `Ports/Mappers/GreetingEventMessageMapperAsync.cs` — same pattern, with `PartitionKey` set to request ID
    - `GreetingsSender/` console app — same Kafka producer pattern as KafkaTaskQueueWithDLQ
    - `GreetingsReceiverConsole/` console app with:
      - `KafkaSubscription<GreetingEvent>` with `MessagePumpType.Proactor`, unique `groupId: "kafka-DeferOnError-Sample"`, `unacceptableMessageLimitWindow: TimeSpan.Zero` with comment
      - `InMemorySchedulerFactory` for requeue delay support
    - No `DlqConsole/` (defer samples don't use DLQ)
    - `README.md` — explains DeferMessageAction requeues with delay, shows the retry-then-succeed pattern, notes that `DeferMessageOnErrorAttribute` is planned for a future release
  - All `.csproj` files target `net9.0` with project references, MIT license headers
  - Verify: `dotnet build` succeeds for all three projects

---

## Phase 4: RabbitMQ DeferOnError

- [x] **4. RMQDeferOnError sample demonstrates deferred messages requeued with delay and eventual success on RabbitMQ**
  - Create `samples/TaskQueue/RMQDeferOnError/` with:
    - `Greetings/` class library — same handler pattern as KafkaDeferOnError (throws `DeferMessageAction` directly, retry counter per message ID, every 3rd message defers then succeeds)
    - `GreetingsSender/` console app — RMQ producer pattern (`RmqProducerRegistryFactory`, `amqp://guest:guest@localhost:5672`, exchange `paramore.brighter.exchange`)
    - `GreetingsReceiverConsole/` console app with:
      - `RmqSubscription<GreetingEvent>` with `MessagePumpType.Proactor`, `isDurable: true`, `highAvailability: true`, `unacceptableMessageLimitWindow: TimeSpan.Zero` with comment
      - `InMemorySchedulerFactory` for requeue delay support
    - No `DlqConsole/`
    - `README.md` — same structure, notes RabbitMQ-specific requeue behavior
  - All `.csproj` files target `net9.0` with project references, MIT license headers
  - Verify: `dotnet build` succeeds for all three projects

---

## Phase 5: Kafka DontAckOnError

- [x] **5. KafkaDontAckOnError sample demonstrates unacknowledged messages re-delivered by Kafka**
  - Create `samples/TaskQueue/KafkaDontAckOnError/` with:
    - `Greetings/` class library with:
      - `Ports/Commands/GreetingEvent.cs` — same as other samples
      - `Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — `[DontAckOnErrorAsync(step: 0)]`, throws `InvalidOperationException` on every 5th message (same pattern as RejectOnError handler but with `DontAckOnErrorAsync` attribute); comment explaining that DontAck means the offset is not committed, so Kafka will re-deliver the message on next poll
      - `Ports/Mappers/GreetingEventMessageMapperAsync.cs` — same pattern with `PartitionKey`
    - `GreetingsSender/` console app — same Kafka producer pattern
    - `GreetingsReceiverConsole/` console app with:
      - `KafkaSubscription<GreetingEvent>` with `MessagePumpType.Proactor`, unique `groupId: "kafka-DontAckOnError-Sample"`, `unacceptableMessageLimitWindow: TimeSpan.Zero` with comment
    - No `DlqConsole/` (DontAck samples don't use DLQ)
    - `README.md` — explains DontAck behavior for Kafka (not committing offset is a no-op, message pump pauses then re-reads), contrasts with RabbitMQ nack behavior
  - All `.csproj` files target `net9.0` with project references, MIT license headers
  - Verify: `dotnet build` succeeds for all three projects

---

## Phase 6: RabbitMQ DontAckOnError

- [ ] **6. RMQDontAckOnError sample demonstrates unacknowledged messages re-delivered by RabbitMQ**
  - Create `samples/TaskQueue/RMQDontAckOnError/` with:
    - `Greetings/` class library — same handler pattern as KafkaDontAckOnError (`[DontAckOnErrorAsync(step: 0)]`, throws on every 5th message); comment explaining that DontAck causes `BasicNack(deliveryTag, requeue: true)` so RabbitMQ immediately returns the message to the queue
    - `GreetingsSender/` console app — RMQ producer pattern
    - `GreetingsReceiverConsole/` console app with:
      - `RmqSubscription<GreetingEvent>` with `MessagePumpType.Proactor`, `isDurable: true`, `highAvailability: true`, `unacceptableMessageLimitWindow: TimeSpan.Zero` with comment
    - No `DlqConsole/`
    - `README.md` — explains RabbitMQ nack with requeue, contrasts with Kafka nack behavior
  - All `.csproj` files target `net9.0` with project references, MIT license headers
  - Verify: `dotnet build` succeeds for all three projects

---

## Phase 7: Documentation and Verification

- [ ] **7. Top-level samples README provides an overview of all error handling examples**
  - Create or update `samples/TaskQueue/README.md` with:
    - Overview of the error handling sample matrix (3 actions x 2 transports)
    - Brief description of each error action (RejectMessageAction, DeferMessageAction, DontAckAction)
    - Links to each sample directory
    - Links to the relevant ADRs (0037, 0045, 0047, 0051)
    - Note about `unacceptableMessageLimitWindow: TimeSpan.Zero` and why it's used in these samples

- [ ] **8. All six sample applications build successfully**
  - Run `dotnet build` from the repository root to verify all samples compile
  - Verify each sample's `.csproj` targets `net9.0`
  - Verify each sample has MIT license headers on all `.cs` files
  - Verify each sample has a `README.md`

---

## Dependencies

```
Task 1 (Update KafkaTaskQueueWithDLQ) → no dependencies
Task 2 (RMQTaskQueueWithDLQ) → depends on Task 1 (establishes RMQ DLQ pattern)
Task 3 (KafkaDeferOnError) → depends on Task 1 (establishes defer pattern on known transport)
Task 4 (RMQDeferOnError) → depends on Task 3 (replicates defer pattern for RMQ)
Task 5 (KafkaDontAckOnError) → depends on Task 1 (establishes dont-ack pattern on known transport)
Task 6 (RMQDontAckOnError) → depends on Task 5 (replicates dont-ack pattern for RMQ)
Task 7 (README) → depends on Tasks 1-6
Task 8 (Build verification) → depends on Tasks 1-7
```

## Risk Mitigations

- **Drift from existing patterns**: Task 1 updates the reference sample first, establishing the `unacceptableMessageLimitWindow` pattern that all new samples follow
- **DeferOnError workaround**: Tasks 3 and 4 include explicit comments in both the handler code and README explaining that `DeferMessageAction` is thrown directly because the attribute doesn't exist yet
- **Consumer group collisions**: Each sample uses a unique, descriptive `groupId` (e.g., `kafka-DeferOnError-Sample`, `kafka-DontAckOnError-Sample`)
- **Build breakage**: Task 8 verifies all samples compile from the repository root
