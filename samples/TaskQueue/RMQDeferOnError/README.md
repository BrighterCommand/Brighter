# RabbitMQ Defer On Error Sample

This sample demonstrates how to use `[DeferMessageOnErrorAsync]` with Brighter to requeue messages with a delay on RabbitMQ, showing how transient failures can be retried automatically.

## What This Demonstrates

- Using the `[DeferMessageOnErrorAsync]` attribute to declaratively convert unhandled exceptions into deferred messages
- The attribute catches exceptions and throws `DeferMessageAction`, which causes the message pump to requeue the message with a delay
- This is the same declarative pattern as `[RejectMessageOnErrorAsync]` and `[DontAckOnErrorAsync]`

## How It Works

1. **GreetingEventHandlerAsync** is decorated with `[DeferMessageOnErrorAsync(step: 0)]`
2. Every 5th message throws an `InvalidOperationException` to simulate a transient failure
3. The `DeferMessageOnErrorHandler` catches the exception and converts it to a `DeferMessageAction`
4. The message pump requeues the message with the configured delay (from the subscription's `RequeueDelay`)
5. On the next delivery, the message is retried

On RabbitMQ, deferred messages are requeued using the scheduler (by default `InMemorySchedulerFactory`), which delays re-delivery.

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for running RabbitMQ)

## Running the Sample

### 1. Start RabbitMQ Infrastructure

From the repository root directory:

```sh
docker compose -f docker-compose-rmq.yaml up -d
```

### 2. Start the Receiver

```sh
cd samples/TaskQueue/RMQDeferOnError/GreetingsReceiverConsole
dotnet run
```

### 3. Start the Sender

In another terminal:

```sh
cd samples/TaskQueue/RMQDeferOnError/GreetingsSender
dotnet run
```

## Expected Output

### GreetingsReceiverConsole

```
Received message #1: Hello # 1
  -> Successfully processed message #1
...
Received message #5: Hello # 5
  -> Simulating failure for message #5 (message will be requeued with delay)
Received message #5: Hello # 5
  -> Successfully processed message #5
```

## Key Code

- **Handler**: `Greetings/Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — uses `[DeferMessageOnErrorAsync(step: 0)]` to convert exceptions to deferred messages
- **Receiver subscription**: `GreetingsReceiverConsole/Program.cs` — `RmqSubscription` with `InMemorySchedulerFactory` for requeue delay support

## Cleanup

```sh
docker compose -f docker-compose-rmq.yaml down -v
```
