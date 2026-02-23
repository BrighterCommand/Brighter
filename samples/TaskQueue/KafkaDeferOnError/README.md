# Kafka Defer On Error Sample

This sample demonstrates how to use `DeferMessageAction` with Brighter to requeue messages with a delay on Kafka, showing how transient failures can be retried until the message eventually succeeds.

## What This Demonstrates

- Throwing `DeferMessageAction` directly from a handler to trigger message requeue with delay
- Tracking retry attempts per message ID using a `ConcurrentDictionary`
- Messages that are deferred eventually succeed after a configurable number of retries

> **Note**: A `DeferMessageOnErrorAttribute` is planned for a future Brighter release. Once available, it will provide the same declarative pattern as `[RejectMessageOnErrorAsync]` and `[DontAckOnErrorAsync]`. Until then, handlers throw `DeferMessageAction` directly.

## How It Works

1. **GreetingEventHandlerAsync** receives messages and tracks attempts per message ID
2. Every 3rd message is designated as a "fail" message
3. On the first two attempts, the handler throws `DeferMessageAction` to requeue the message
4. On the 3rd attempt, the message succeeds — demonstrating eventual success after transient failures
5. Non-"fail" messages (messages 1, 2, 4, 5, 7, 8, ...) are processed successfully on the first attempt

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for running Kafka)

## Running the Sample

### 1. Start Kafka Infrastructure

From the repository root directory:

```sh
docker compose -f docker-compose-kafka.yaml up -d
```

### 2. Start the Receiver

```sh
cd samples/TaskQueue/KafkaDeferOnError/GreetingsReceiverConsole
dotnet run
```

### 3. Start the Sender

In another terminal:

```sh
cd samples/TaskQueue/KafkaDeferOnError/GreetingsSender
dotnet run
```

## Expected Output

### GreetingsReceiverConsole

```
Received message #1: Hello # 1 (message ID: abc123, attempt: 1)
  -> Successfully processed message #1
Received message #2: Hello # 2 (message ID: def456, attempt: 1)
  -> Successfully processed message #2
Received message #3: Hello # 3 (message ID: ghi789, attempt: 1)
  -> Deferring message #3 (attempt 1 of 3)
Received message #3: Hello # 3 (message ID: ghi789, attempt: 2)
  -> Deferring message #3 (attempt 2 of 3)
Received message #3: Hello # 3 (message ID: ghi789, attempt: 3)
  -> Message #3 succeeded after 3 attempts
```

## Key Code

- **Handler**: `Greetings/Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — throws `DeferMessageAction` directly with retry tracking via `ConcurrentDictionary<string, int>`
- **Receiver subscription**: `GreetingsReceiverConsole/Program.cs` — `KafkaSubscription` with `InMemorySchedulerFactory` for requeue delay support

## Cleanup

```sh
docker compose -f docker-compose-kafka.yaml down -v
```
