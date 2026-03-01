# RabbitMQ Task Queue with Dead Letter Queue (DLQ) Sample

This sample demonstrates how to use the `RejectMessageOnErrorAsync` attribute with Brighter to handle message processing failures and route rejected messages to a Dead Letter Queue (DLQ) on RabbitMQ.

## What This Demonstrates

- `[RejectMessageOnErrorAsync(step: 0)]` catches unhandled exceptions and rejects the message
- Configuring `deadLetterChannelName` and `deadLetterRoutingKey` on an `RmqSubscription` routes rejected messages to a DLQ
- A separate DLQ consumer processes rejected messages and displays rejection metadata

## How It Works

1. **GreetingEventHandlerAsync** uses `[RejectMessageOnErrorAsync(step: 0)]` to catch any unhandled exceptions
2. When an exception occurs (every 5th message), the attribute converts it to a reject action
3. The RabbitMQ consumer rejects the message via `BasicNack`
4. Brighter routes the rejected message to the configured DLQ queue (`greeting.event.dlq`)
5. The DLQ consumer reads from `greeting.event.dlq` and logs the rejected message details

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for running RabbitMQ)

## Running the Sample

### 1. Start RabbitMQ Infrastructure

From the repository root directory:

```sh
docker compose -f docker-compose-rmq.yaml up -d
```

This starts RabbitMQ with the management plugin:
- AMQP: localhost:5672
- Management UI: http://localhost:15672 (guest/guest)

### 2. Start the Receiver (Main Consumer)

This creates the queues and starts consuming messages from `greeting.event`:

```sh
cd samples/TaskQueue/RMQTaskQueueWithDLQ/GreetingsReceiverConsole
dotnet run
```

### 3. Start the DLQ Consumer

In a new terminal, start the DLQ consumer to process rejected messages:

```sh
cd samples/TaskQueue/RMQTaskQueueWithDLQ/DlqConsole
dotnet run
```

### 4. Start the Sender

In another terminal, start sending messages:

```sh
cd samples/TaskQueue/RMQTaskQueueWithDLQ/GreetingsSender
dotnet run
```

## Expected Output

### GreetingsReceiverConsole

Messages 1-4 are processed successfully, message 5 fails and is rejected:

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

### DlqConsole

Every 5th message (5, 10, 15, ...) appears in the DLQ:

```
=== DEAD LETTER MESSAGE ===
  Message ID: <guid>
  Greeting: Hello # 5
  Original Topic: greeting.event
  Rejection Reason: DeliveryError
===========================
```

## Key Code

- **Handler**: `Greetings/Ports/CommandHandlers/GreetingEventHandlerAsync.cs` — `[RejectMessageOnErrorAsync(step: 0)]` with deterministic failure on every 5th message
- **Receiver subscription**: `GreetingsReceiverConsole/Program.cs` — `RmqSubscription` with `deadLetterChannelName` and `deadLetterRoutingKey`
- **DLQ handler**: `DlqConsole/DlqGreetingEventHandlerAsync.cs` — reads rejection metadata from `Context.Bag`

## Cleanup

To stop RabbitMQ:

```sh
docker compose -f docker-compose-rmq.yaml down
```

To remove all data (queues, messages):

```sh
docker compose -f docker-compose-rmq.yaml down -v
```
