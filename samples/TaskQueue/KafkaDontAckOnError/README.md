# Kafka Don't Ack On Error Sample

This sample demonstrates how to use the `DontAckOnErrorAsync` attribute with Brighter to leave messages unacknowledged on Kafka when processing fails.

## What This Demonstrates

- `[DontAckOnErrorAsync(step: 0)]` catches unhandled exceptions and converts them to a `DontAckAction`
- On Kafka, "don't ack" means the consumer offset is **not committed** for the failed message
- The message pump pauses briefly, then Kafka re-delivers the message on the next poll

## How It Works

1. **GreetingEventHandlerAsync** uses `[DontAckOnErrorAsync(step: 0)]` to catch any unhandled exceptions
2. When an exception occurs (every 5th message), the attribute converts it to a `DontAckAction`
3. The Kafka consumer does **not** commit the offset for that message
4. On the next poll cycle, Kafka re-delivers the same message from the uncommitted offset
5. The message pump applies a configurable delay before re-reading to avoid tight retry loops

### Kafka vs RabbitMQ Nack Behavior

On **Kafka**, "don't ack" is effectively a no-op — not committing the offset is sufficient. The consumer will re-read from the last committed offset on the next poll. This means the failed message and any subsequent messages in the batch will be re-delivered.

On **RabbitMQ**, "don't ack" sends an explicit `BasicNack(deliveryTag, requeue: true)` which immediately returns the individual message to the queue. See the `RMQDontAckOnError` sample for RabbitMQ-specific behavior.

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
cd samples/TaskQueue/KafkaDontAckOnError/GreetingsReceiverConsole
dotnet run
```

### 3. Start the Sender

In another terminal:

```sh
cd samples/TaskQueue/KafkaDontAckOnError/GreetingsSender
dotnet run
```

## Expected Output

### GreetingsReceiverConsole

```
Received message #1: Hello # 1
  -> Successfully processed message #1
Received message #2: Hello # 2
  -> Successfully processed message #2
...
Received message #5: Hello # 5
  -> Simulating failure for message #5 (offset will not be committed)
```

Message #5 will be re-delivered on the next poll cycle.

## Key Code

- **Handler**: `Greetings/Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — `[DontAckOnErrorAsync(step: 0)]` with deterministic failure on every 5th message
- **Receiver subscription**: `GreetingsReceiverConsole/Program.cs` — `KafkaSubscription` with unique `groupId: "kafka-DontAckOnError-Sample"`

## Cleanup

```sh
docker compose -f docker-compose-kafka.yaml down -v
```
