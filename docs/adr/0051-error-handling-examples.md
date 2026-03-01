# 51. Error Handling Example Applications

Date: 2026-02-23

## Status

Accepted

## Context

**Parent Requirement**: [specs/0021-Error-Examples/requirements.md](../../specs/0021-Error-Examples/requirements.md)

**Scope**: This ADR covers the design of six sample TaskQueue applications that demonstrate Brighter's error handling actions (RejectMessageAction, DeferMessageAction, DontAckAction) for Kafka and RabbitMQ transports.

Brighter now provides three exception-based actions that change how the message pump handles errors:

- **RejectMessageAction** — reject the message, route a copy to a dead letter channel
- **DeferMessageAction** — requeue the message with a delay for later retry
- **DontAckAction** — leave the message unacknowledged so the transport re-delivers it

Each action has a corresponding middleware attribute (`[RejectMessageOnErrorAsync]`, `[DontAckOnErrorAsync]`) that wraps the handler pipeline and converts unhandled exceptions into the appropriate action. The `DeferMessageOnError` attribute does not yet exist, so the defer samples will throw `DeferMessageAction` directly.

Users need concrete, runnable examples to understand how to configure and use each action. The existing `KafkaTaskQueueWithDLQ` sample demonstrates reject-on-error for Kafka. We need four additional samples (Kafka defer, Kafka dont-ack, RMQ reject, RMQ defer, RMQ dont-ack) and should preserve the existing Kafka DLQ sample as the sixth.

### Forces

- Samples must be simple enough for a new user to understand in minutes
- Each sample must focus on a single error-handling behavior
- Kafka and RabbitMQ have different transport semantics for nack, DLQ, and requeue
- The `DeferMessageOnError` attribute does not exist yet
- Samples live in the main Brighter repository and use project references
- Existing samples establish conventions that must be followed

## Decision

We will create **four new sample applications** under `samples/TaskQueue/` and keep the existing `KafkaTaskQueueWithDLQ` sample as-is, giving us five focused error-handling examples. A sixth (RMQRejectOnError) completes the matrix.

### Sample Matrix

```
┌──────────────────┬──────────────────────────┬──────────────────────────┐
│ Error Action      │ Kafka                    │ RabbitMQ                 │
├──────────────────┼──────────────────────────┼──────────────────────────┤
│ RejectOnError     │ KafkaTaskQueueWithDLQ    │ RMQTaskQueueWithDLQ      │
│                   │ (exists)                 │ (new)                    │
├──────────────────┼──────────────────────────┼──────────────────────────┤
│ DeferOnError      │ KafkaDeferOnError        │ RMQDeferOnError          │
│                   │ (new)                    │ (new)                    │
├──────────────────┼──────────────────────────┼──────────────────────────┤
│ DontAckOnError    │ KafkaDontAckOnError      │ RMQDontAckOnError        │
│                   │ (new)                    │ (new)                    │
└──────────────────┴──────────────────────────┴──────────────────────────┘
```

### Architecture Overview

Each sample follows the same structural pattern established by existing Brighter samples:

```
samples/TaskQueue/{SampleName}/
├── Greetings/                              # Shared library (class library)
│   ├── Greetings.csproj
│   └── Ports/
│       ├── Commands/
│       │   └── GreetingEvent.cs            # Event definition
│       ├── CommandHandlers/
│       │   └── GreetingEventHandlerAsync.cs # Handler with error attribute
│       └── Mappers/
│           └── GreetingEventMessageMapperAsync.cs  # Serialization
├── GreetingsSender/                        # Producer (console app)
│   ├── GreetingsSender.csproj
│   ├── Program.cs                          # Host configuration + producer setup
│   └── TimedMessageGenerator.cs            # IHostedService that sends messages
├── GreetingsReceiverConsole/               # Consumer (console app)
│   ├── GreetingsReceiverConsole.csproj
│   └── Program.cs                          # Host configuration + subscription setup
├── DlqConsole/                             # DLQ consumer (RejectOnError only)
│   ├── DlqConsole.csproj
│   ├── Program.cs
│   └── DlqGreetingEventHandlerAsync.cs
└── README.md
```

### Key Components and Their Responsibilities

#### GreetingEvent (Information Holder)
Carries the greeting payload. Identical across all samples — inherits from `Event`, has a `Greeting` string property. Uses `Id.Random()` for message identity.

#### GreetingEventHandlerAsync (Service Provider + Controller)
The focal point of each sample. Responsible for:
- **Knowing**: Which error action this sample demonstrates
- **Doing**: Processing messages and simulating deterministic failures
- **Deciding**: When to fail (every Nth message) to show the error path

The handler's attribute decoration is what differs across samples:

| Sample Type | Attribute | Failure Simulation |
|------------|-----------|-------------------|
| RejectOnError | `[RejectMessageOnErrorAsync(step: 0)]` | Throw exception on every 5th message |
| DeferOnError | None (throws `DeferMessageAction` directly) | Throw `DeferMessageAction` on every 3rd message, succeed after N retries |
| DontAckOnError | `[DontAckOnErrorAsync(step: 0)]` | Throw exception on every 5th message |

#### GreetingEventMessageMapperAsync (Interfacer)
Maps between `GreetingEvent` and Brighter's `Message` type using `System.Text.Json`. Kafka samples set `PartitionKey`; RabbitMQ samples do not.

#### TimedMessageGenerator (Coordinator)
`IHostedService` that sends a `GreetingEvent` every 500ms via `IAmACommandProcessor.PostAsync`. Identical across all samples.

#### DlqGreetingEventHandlerAsync (Service Provider — RejectOnError only)
Processes rejected messages from the DLQ. Displays rejection metadata from `Context.Bag` (OriginalTopic, RejectionReason).

### Transport-Specific Configuration

#### Kafka Samples

**Producer** (`GreetingsSender/Program.cs`):
- `KafkaProducerRegistryFactory` with `localhost:9092`
- `KafkaPublication<GreetingEvent>` with 3 partitions
- `InMemorySchedulerFactory` for scheduler support

**Consumer** (`GreetingsReceiverConsole/Program.cs`):
- `KafkaSubscription<GreetingEvent>` with `MessagePumpType.Proactor`
- `groupId` unique per sample to avoid consumer group conflicts
- `offsetDefault: AutoOffsetReset.Earliest`
- RejectOnError: `deadLetterRoutingKey: new RoutingKey("{topic}.dlq")`
- DeferOnError: `InMemorySchedulerFactory` for requeue delay support

**Nack behavior**: Kafka nack is a no-op — not committing the offset is sufficient. The message pump applies a configurable delay before re-reading.

#### RabbitMQ Samples

**Producer** (`GreetingsSender/Program.cs`):
- `RmqProducerRegistryFactory` with `amqp://guest:guest@localhost:5672`
- Exchange: `paramore.brighter.exchange`
- `InMemorySchedulerFactory` for scheduler support

**Consumer** (`GreetingsReceiverConsole/Program.cs`):
- `RmqSubscription<GreetingEvent>` with `MessagePumpType.Proactor`
- `isDurable: true`, `highAvailability: true`
- RejectOnError: `deadLetterChannelName` and `deadLetterRoutingKey` configured
- DeferOnError: `InMemorySchedulerFactory` for requeue delay support
- Uses `Paramore.Brighter.MessagingGateway.RMQ.Async` gateway

**Nack behavior**: RabbitMQ performs `BasicNack(deliveryTag, requeue: true)` — the message is immediately returned to the queue.

### DeferOnError Handler Design

Since `DeferMessageOnErrorAttribute` does not exist, the defer samples throw `DeferMessageAction` directly from the handler. To demonstrate "eventual success after retry", the handler uses a static counter per message ID:

```
Handler receives message
  → Is this a "fail" message (every 3rd)?
    → Has this message been seen fewer than N times?
      YES → throw new DeferMessageAction("Transient failure, will retry")
      NO  → Process successfully (simulating transient error clearing)
    → Not a "fail" message → Process normally
```

This shows the user that deferred messages come back and eventually succeed.

### Project References

All samples use project references to Brighter source projects (not NuGet packages), consistent with existing samples:

- `Paramore.Brighter` — core framework
- `Paramore.Brighter.ServiceActivator` — message pump
- `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` — DI integration
- `Paramore.Brighter.ServiceActivator.Extensions.Hosting` — `ServiceActivatorHostedService`
- `Paramore.Brighter.Extensions.DependencyInjection` — `AddBrighter()` extension
- `Paramore.Brighter.MessagingGateway.Kafka` — Kafka transport
- `Paramore.Brighter.MessagingGateway.RMQ.Async` — RabbitMQ async transport

### Infrastructure

Samples use existing Docker Compose files already in the repository:
- **Kafka**: `docker-compose-kafka.yaml` (Zookeeper + Kafka broker on port 9092)
- **RabbitMQ**: `docker-compose-rmq.yaml` (RabbitMQ with management on ports 5672/15672)

No new Docker Compose files are needed.

### README Structure

Each sample's README follows this structure:
1. **What This Demonstrates** — one-sentence summary of the error action
2. **How It Works** — brief explanation of the error flow with a diagram
3. **Prerequisites** — Docker, .NET 9 SDK
4. **Running the Sample** — step-by-step commands (docker compose up, dotnet run for each app)
5. **Expected Output** — what the user should see in each terminal
6. **Key Code** — pointer to the handler file with the relevant attribute/throw

A top-level `samples/TaskQueue/README.md` will be added (or updated) linking to all error-handling samples as a group.

## Consequences

### Positive

- Users have a clear, focused example for each error action on each transport
- Following existing sample conventions means users familiar with one sample can navigate all of them
- Deterministic failure patterns make the behavior observable and reproducible
- Samples serve as integration tests for the error-handling features

### Negative

- Five new project directories add maintenance burden (code must stay in sync with framework changes)
- The DeferOnError samples use a workaround (direct throw) since the attribute doesn't exist yet — these will need updating when `DeferMessageOnErrorAttribute` is added
- Some code duplication across samples (GreetingEvent, mapper, TimedMessageGenerator) — this is intentional for sample independence but increases maintenance surface

### Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Samples drift out of sync with framework | Include samples in CI build; README links to relevant ADRs |
| DeferOnError workaround confuses users | README clearly documents that the attribute is planned; handler code comments explain the direct throw |
| Consumer group ID collisions between samples | Each sample uses a unique, descriptive groupId |

## Alternatives Considered

### Single multi-action sample per transport
One sample that demonstrates all three actions via configuration. Rejected because it violates the "simple and focused" requirement — users would need to understand all three actions simultaneously.

### NuGet package references instead of project references
Since this is the main Brighter repo, using NuGet would mean samples lag behind source. Project references keep samples in sync with the framework code and are consistent with all existing samples.

### Separate error-examples repository
Creating a standalone repo was considered but rejected because the existing samples already live in the main repo, and project references provide immediate consistency.

## References

- Requirements: [specs/0021-Error-Examples/requirements.md](../../specs/0021-Error-Examples/requirements.md)
- Related ADRs:
  - [0037 - Reject Message on Error Handler](0037-reject-message-on-error-handler.md)
  - [0045 - Provide DLQ Where Missing](0045-provide-dlq-where-missing.md)
  - [0047 - Message Rejection Routing Strategy](0047-message-rejection-routing-strategy.md)
- Existing sample: `samples/TaskQueue/KafkaTaskQueueWithDLQ/`
- Existing sample: `samples/TaskQueue/RMQTaskQueue/`
