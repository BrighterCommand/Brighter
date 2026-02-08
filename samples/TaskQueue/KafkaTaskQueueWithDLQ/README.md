# Kafka Task Queue with Dead Letter Queue (DLQ) Sample

This sample demonstrates how to use the `RejectMessageOnErrorAsync` attribute with Brighter to handle message processing failures and route rejected messages to a Dead Letter Queue (DLQ) in Kafka.

## Purpose

- Shows how to use `[RejectMessageOnErrorAsync(step: 0)]` to catch exceptions and reject messages
- Demonstrates configuring a `deadLetterRoutingKey` on a Kafka subscription
- Provides a DLQ consumer application to process rejected messages
- Uses deterministic failure (every 5th message) for predictable demonstration

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for running Kafka)

## Running the Sample

### 1. Start Kafka Infrastructure

From the repository root directory:

```sh
docker compose -f docker-compose-kafka.yaml up -d
```

This starts:
- Zookeeper
- Kafka broker (localhost:9092)
- Schema Registry
- Confluent Control Center (http://localhost:9021)

### 2. Start the Receiver (Main Consumer)

This creates the topics and starts consuming messages from `greeting.event`:

```sh
cd samples/TaskQueue/KafkaTaskQueueWithDLQ/GreetingsReceiverConsole
dotnet run
```

### 3. Start the DLQ Consumer

In a new terminal, start the DLQ consumer to process rejected messages:

```sh
cd samples/TaskQueue/KafkaTaskQueueWithDLQ/DlqConsole
dotnet run
```

### 4. Start the Sender

In another terminal, start sending messages:

```sh
cd samples/TaskQueue/KafkaTaskQueueWithDLQ/GreetingsSender
dotnet run
```

## Expected Behavior

### GreetingsReceiverConsole Output

Messages 1, 2, 3, 4 are processed successfully, message 5 fails and is rejected:

```
Received message #1: Hello # 1
  -> Successfully processed message #1
Received message #2: Hello # 2
  -> Successfully processed message #2
Received message #3: Hello # 3
  -> Successfully processed message #3
Received message #4: Hello # 4
  -> Successfully processed message #4
Received message #5: Hello # 5
  -> Simulating failure for message #5
```

This pattern repeats: messages 6-9 succeed, message 10 fails, etc.

### DlqConsole Output

Every 5th message (5, 10, 15, ...) appears in the DLQ:

```
=== DEAD LETTER MESSAGE ===
  Message ID: <guid>
  Greeting: Hello # 5
  Original Topic: greeting.event
  Rejection Reason: DeliveryError
===========================
```

## How It Works

1. **GreetingEventHandlerAsync** uses `[RejectMessageOnErrorAsync(step: 0)]` to catch any unhandled exceptions
2. When an exception occurs (every 5th message), the handler converts it to a reject action
3. The Kafka consumer rejects the message with `RejectionReason.DeliveryError`
4. Brighter routes the rejected message to the configured DLQ topic (`greeting.event.dlq`)
5. The DLQ consumer reads from `greeting.event.dlq` and logs the rejected message details

## Configuration Details

### Main Consumer Subscription

```csharp
new KafkaSubscription<GreetingEvent>(
    // ... other params
    deadLetterRoutingKey: new RoutingKey("greeting.event.dlq")
)
```

### DLQ Consumer Subscription

```csharp
new KafkaSubscription<GreetingEvent>(
    routingKey: new RoutingKey("greeting.event.dlq"),
    makeChannels: OnMissingChannel.Assume  // DLQ created by main consumer
)
```

## Cleanup

To stop Kafka:

```sh
docker compose -f docker-compose-kafka.yaml down
```

To remove all data (topics, offsets):

```sh
docker compose -f docker-compose-kafka.yaml down -v
```
