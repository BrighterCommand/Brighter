# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a Brighter user, I would like sample applications demonstrating the error handling actions (DeferMessageAction, RejectMessageAction, DontAckAction) for Kafka and RabbitMQ, so that I can quickly understand how to use this new functionality in my own applications.

### Background

Brighter's default behavior is to ack a message when an error leaves the handler pipeline. Errors come in two forms:

- **Transient** - retried a number of times using `UsePolicyAttribute` or `UseResilienceAttribute`
- **Non-transient** - fail and ack the message, relying on an operator to investigate from the logs

However, Brighter now supports three actions that change this flow by being thrown as exceptions:

| Action | Behavior | Use Case |
|--------|----------|----------|
| **DeferMessageAction** | Requeue the message with a delay | Transient failures that should retry later |
| **RejectMessageAction** | Reject the message and put a copy on a dead letter channel | Non-recoverable failures that need operator attention |
| **DontAckAction** | Nack the message, putting it back on the stream or queue | Feature switches, blocking scenarios, stream reprocessing |

In addition, Brighter provides middleware attributes that wrap a handler to trigger these exceptions when an unhandled exception bubbles out:

| Attribute | Throws | Pipeline Position |
|-----------|--------|-------------------|
| `[RejectMessageOnError(step: 0)]` / `[RejectMessageOnErrorAsync(step: 0)]` | `RejectMessageAction` | Outermost (step 0) |
| `[DontAckOnError(step: 0)]` / `[DontAckOnErrorAsync(step: 0)]` | `DontAckAction` | Outermost (step 0) |
| `[DeferMessageOnError(step: 0)]` / `[DeferMessageOnErrorAsync(step: 0)]` | `DeferMessageAction` | Outermost (step 0) - if available |

These attributes give users a way to select a different default for what happens when the handler exits via an exception (as opposed to falling through, which is treated as success).

Much of this functionality is new and covered by the following specs and ADRs in the main Brighter repository:

**RejectMessageAction:**
- Specs: 0001-kafka-dead-letter-queue, 0002-backstop-error-handler, 0010-aws-sqs-dead-letter-queue, 0011-redis-dead-letter-queue, 0012-mssql-dead-letter-queue, 0013-postgres-dead-letter-queue, 0014-rocketmq-dead-letter-queue, 0015-mqtt-dead-letter-queue
- ADRs: 0037, 0038, 0039, 0040, 0041, 0042, 0043, 0045, 0046, 0047

**DeferMessageAction (Requeue):**
- Specs: 0002-universal_scheduler_delay, 0004-transport-scheduler-wiring
- ADRs: 0037-universal-scheduler-delay, 0039-transport-scheduler-wiring

**DontAckAction:**
- Spec: 0020-DontAckAction

## Proposed Solution

Create a set of sample TaskQueue applications in this repository that demonstrate each error handling action for both Kafka and RabbitMQ. Each sample follows the established Brighter sample pattern (shared library + sender + receiver) and shows a single, focused error-handling scenario.

### Sample Structure

Six sample applications organized by transport and error action:

#### Kafka Samples
1. **KafkaRejectOnError** - Demonstrates `RejectMessageAction` with DLQ routing
2. **KafkaDeferOnError** - Demonstrates `DeferMessageAction` with requeue and delay
3. **KafkaDontAckOnError** - Demonstrates `DontAckAction` with nack behavior

#### RabbitMQ Samples
4. **RMQRejectOnError** - Demonstrates `RejectMessageAction` with DLQ routing
5. **RMQDeferOnError** - Demonstrates `DeferMessageAction` with requeue and delay
6. **RMQDontAckOnError** - Demonstrates `DontAckAction` with nack behavior

Each sample consists of:
- **Shared library** (`Greetings/`) - Event definitions, handlers with the relevant error attribute, message mappers
- **Sender** (`GreetingsSender/`) - Sends messages on a timer
- **Receiver** (`GreetingsReceiverConsole/`) - Consumes messages with the error handling behavior
- **DLQ Consumer** (`DlqConsole/`) - Only for RejectOnError samples; consumes rejected messages
- **README.md** - Explains what the sample demonstrates, how to run it, and what to expect

## Requirements

### Functional Requirements

1. **FR-1**: Each sample MUST demonstrate exactly one error handling action (Reject, Defer, or DontAck) for one transport (Kafka or RabbitMQ)
2. **FR-2**: Each sample MUST use the corresponding middleware attribute (`[RejectMessageOnErrorAsync]`, `[DontAckOnErrorAsync]`, or defer equivalent) on the handler at step 0
3. **FR-3**: RejectOnError samples MUST include a DLQ consumer application that reads rejected messages and displays rejection metadata (original topic, rejection reason)
4. **FR-4**: DeferOnError samples MUST show the message being requeued with a visible delay, and the handler eventually succeeding on retry
5. **FR-5**: DontAckOnError samples MUST show the message remaining unacknowledged and being re-delivered by the transport
6. **FR-6**: Each handler MUST simulate a deterministic failure (e.g., every Nth message, or a configurable flag) so the user can observe both success and error paths
7. **FR-7**: Each sample MUST use the `IHostedService` / Generic Host pattern consistent with existing Brighter samples
8. **FR-8**: Each sample MUST use `System.Text.Json` for serialization via `JsonSerialisationOptions.Options`
9. **FR-9**: Kafka samples MUST use `MessagePumpType.Proactor` (async); RabbitMQ samples MAY use either `Reactor` or `Proactor`
10. **FR-10**: Each sample MUST include a README.md explaining: what the sample demonstrates, prerequisites, how to run it (including Docker), and expected output

### Non-functional Requirements

1. **NFR-1**: Samples MUST be simple and focused - a new user should understand the pattern within 5 minutes of reading the code
2. **NFR-2**: Samples MUST target `net9.0`
3. **NFR-3**: Samples SHOULD use Serilog with console sinks for logging
4. **NFR-4**: Samples MUST reference Brighter packages via NuGet (not project references), using the latest stable version
5. **NFR-5**: All source files MUST include the MIT license header per Brighter conventions
6. **NFR-6**: Docker Compose files MUST be provided for running the required infrastructure (Kafka/Zookeeper, RabbitMQ)

### Constraints and Assumptions

- Samples run against local Docker infrastructure (Kafka on localhost:9092, RabbitMQ on localhost:5672)
- Samples reference Brighter NuGet packages (not source project references, since this is a separate repository)
- The `DeferMessageOnError` attribute may or may not exist yet - if not available, the DeferOnError samples should throw `DeferMessageAction` directly from the handler to demonstrate the requeue behavior
- Each sample is self-contained and independently runnable
- Follows the established Brighter sample naming and structural conventions

### Out of Scope

- Samples for transports other than Kafka and RabbitMQ (e.g., AWS SQS, Redis, MQTT)
- Production deployment guidance
- Performance testing or benchmarking
- Custom retry policies (samples use simple defaults)
- Web API or ASP.NET Core integration (console apps only)
- Samples for `InvalidMessageAction` (deserialization-level concern, not handler-level)

## Acceptance Criteria

1. **AC-1**: All six sample applications build successfully with `dotnet build`
2. **AC-2**: Each Kafka sample runs correctly against a local Kafka instance (via Docker Compose)
3. **AC-3**: Each RabbitMQ sample runs correctly against a local RabbitMQ instance (via Docker Compose)
4. **AC-4**: RejectOnError samples: rejected messages appear in the DLQ consumer with correct metadata
5. **AC-5**: DeferOnError samples: deferred messages are requeued and eventually processed after the delay
6. **AC-6**: DontAckOnError samples: nacked messages are re-delivered by the transport and visible in logs
7. **AC-7**: Each README.md provides clear, accurate instructions for running the sample
8. **AC-8**: A top-level README.md provides an overview of all samples and links to each

## Additional Context

### Existing Samples for Reference

The main Brighter repository contains these relevant samples:
- `samples/TaskQueue/KafkaTaskQueue/` - Basic Kafka send/receive
- `samples/TaskQueue/KafkaTaskQueueWithDLQ/` - Kafka with RejectMessageOnError and DLQ consumer
- `samples/TaskQueue/RMQTaskQueue/` - Basic RabbitMQ send/receive

### Transport Behavior Differences

| Behavior | Kafka | RabbitMQ |
|----------|-------|----------|
| **Nack** | No-op (not committing offset is sufficient) | `BasicNack` with requeue=true |
| **DLQ** | Brighter-managed (produce to DLQ topic) | Can use native DLX or Brighter-managed |
| **Requeue with delay** | Via scheduler/producer delegation | Via scheduler/producer delegation |

These transport differences should be highlighted in each sample's README.
