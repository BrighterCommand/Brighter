# 46. Kafka DLQ Producer for Requeue

Date: 2026-01-11

## Status

Approved

## Context

**Parent Requirement**: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)

**Scope**: This ADR focuses specifically on how the Kafka transport implements the producer for dead letter queue and invalid message channel functionality. It builds on [ADR 0045: Provide DLQ Where Missing](0045-provide-dlq-where-missing.md), which established the high-level decision to use `IAmAMessageProducer` directly for DLQ publishing.

### The Problem

ADR 0034 decided that the `IAmAMessageConsumer` (sync or async) must manage the lifetime of an `IAmAMessageProducer` (sync or async) to send messages to the Dead Letter Channel or Invalid Message Channel. For Kafka, we need to decide:

1. **Producer Creation**: When and how does `KafkaMessageConsumer` create the `IAmAMessageProducer` for DLQ?
2. **Producer Lifecycle**: Who owns the producer lifetime? When is it created and disposed?
3. **Topic Management**: How do we handle DLQ topic creation (create/validate/assume)?
4. **Producer Configuration**: What configuration does the DLQ producer need? Should it match the data topic producer?
5. **Error Handling**: What happens if producing to the DLQ fails? Do we retry? Log and continue?
6. **Offset Management**: When do we commit the offset - before or after DLQ production succeeds?

### Constraints

- Must follow ADR 0034's decision to use `IAmAMessageProducer` directly
- Sync consumer must use sync producer; async consumer must use async producer
- DLQ messages do NOT go through the Outbox (per ADR 0034)
- Must support `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport` interfaces
- Must handle topic auto-creation matching data topic strategy (create/validate/assume)

## Decision

### Kafka Support for Dead Letter and Invalid Message Channels

We will support both a dead letter channel and an invalid message channel for Kafka.

**Implementation**: Extend `KafkaSubscription` to support both `IUseBrighterDeadLetterSupport` and
`IUseBrighterInvalidMessageSupport`:

```csharp
 public class KafkaSubscription : Subscription, IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport
 {
     ...
 }
```

### Topic Naming

We use the presence, or absence, of `IUseBrighterDeadLetterSupport` to indicate that a `Subscription` supports a
Brighter defined dead letter queue and the presence, or absence, of `IUseBrighterInvalidMessageSupport` to indicate
that a `Subscription` supports a Brighter defined invalid message channel.

Both of these interfaces work by providing a `RoutingKey` that need to be passed from the `Subscription` to the
`Channel`, usually via the `IAmAChannelFactory` (see `InMemoryChannelFactory` as an example).

This means that naming is done by setting the property on the `KafkaSubscription` type. It is set by the user, if 
they want to use a dead letter channel or an invalid message channel. We do not provide a default name because that 
would create a DLQ and invalid message channel by default. The optionality is important as many users may not wish to use a dead letter channel with a stream.

**Implementation**: We should extend the `KafkaSubscription` constructor to support the relevant routing keys as
optional parameters (i.e., can be null)

```csharp
// Example configuration with custom names
var subscription = new KafkaSubscription<OrderCommand>(
    name: new SubscriptionName("orders-consumer"),
    channelName: new ChannelName("orders"),
    routingKey: new RoutingKey("orders"),
    groupId: "order-service",
    makeChannels: OnMissingChannel.Create),
    deadLetterRoutingKey: new RoutingKey("failed-orders"),
    invalidMessageRoutingKey: new RoutingKey("invalid-orders");
```

**Same Topic for Both**: If users want to use the same topic for DLQ and invalid messages, they can set both routing keys to the same value. The producer will handle this correctly (ADR 0035 creates separate `Lazy<T>` instances but they'll point to the same topic).

#### Topic Naming By Convention

We should provide help with topic naming through a class. 

The class should take a string template in its constructor that can be used to provide the naming strategy for the 
dead-letter channel and the invalid-message channel. The class then provides methods to produce these, i.e. 
`MakeChannelName`. These methods take a `RoutingKey` as a parameter, 
allowing the user to create these names from their data channel's `RoutingKey`.

We should provide a default naming template that can be used so that it is not necessary to provide a template.

```csharp
public class DeadLetterNamingConvention
{
    public DeadLetterNamingConvention(string? template = null)
    {
        _template = template;
        if (_template is null)
            _template = "{0}.dlq"
    }
    
    public RoutingKey MakeChannelName(RoutingKey dataTopic)
    {
        return new RoutingKey(string.Format(_template, dataTopic.Value))
    }
}

public class InvalidMessageNamingConvention
{
    public InvalidMessageNamingConvention(string? template = null)
    {
        _template = template;
        if (_template is null)
            _template = "{0}.invalid"
    }
    
    public RoutingKey MakeChannelName(RoutingKey dataTopic)
    {
        return new RoutingKey(string.Format(_template, dataTopic.Value))
    }
}

```

**Default Naming Pattern**:
- **Dead Letter Queue**: `{original-topic}.dlq`
- **Invalid Message Channel**: `{original-topic}.invalid`

**Examples**:
- Data topic: `orders` → DLQ: `orders.dlq`, Invalid: `orders.invalid`
- Data topic: `customer.events` → DLQ: `customer.events.dlq`, Invalid: `customer.events.invalid`

### Producer Creation and Lifecycle

### Producer Configuration

The dead letter channel, or invalid message channel, producer will inherit most configuration from the consumer's 
`KafkaMessagingGatewayConfiguration`:
- **Bootstrap servers**: Same as consumer
- **Security settings**: Same as consumer (SASL, SSL, etc.)
- **Serialization**: Same serializer as data messages
- **Make channels strategy**: Matches the data topic strategy (create/validate/assume)

The dead letter channel, or invalid message channel, producer will use **different** settings for:
- **Topic**: The DLQ or invalid message routing key (not the data topic)
- **Partitioning**: Will use default partitioning (no partition key preservation)
- **Acks**: Will use `acks=all` for reliability (even if data topic uses `acks=1`)

The DLQ topic creation strategy **MUST match** the data topic's `MakeChannels` setting, as established in ADR 0035.

| Data Topic Setting | DLQ Topic Behavior |
|-------------------|-------------------|
| `OnMissingChannel.Create` | Create DLQ topic automatically if missing |
| `OnMissingChannel.Validate` | Verify DLQ topic exists; throw if missing |
| `OnMissingChannel.Assume` | Assume DLQ topic exists; let Kafka fail if missing |

**Rationale**: If a user has chosen automatic topic creation for their data topics, they expect the same for error handling topics. If they've chosen to pre-create topics (Validate/Assume), they'll do the same for DLQ topics.

**Implementation**: The `KafkaPublication` for DLQ producers will use the same `MakeChannels` value as the 
consumer's configuration:

```csharp
var publication = new KafkaPublication
{
    DeadLetterRoutingKey  = _deadLetterSupport.DeadLetterRoutingKey,
    MakeChannels = _configuration.MakeChannels // Inherit from data topic
};
```

#### Producer Creation

The `KafkaMessageConsumer` and `KafkaMessageConsumerAsync` will create their own `KafkaMessageProducer` or `KafkaMessageProducerAsync` instances for DLQ/invalid message publishing.

The `KafkaMessageProducer` will act on the `MakeChannels` setting of the `KafkaPublication` when its `Init` method
is called. We typically call this via the `KafkaMessageProducerFactory` calling its `Create` method. We do not need
the `KafkaProducerRegistryFactory` in this case, as we do not intend to look up the producer for a given message, as
we already have the message to send.

**Implementation**: We should use the `KafkaMessageProducer` to build the producers for the dead letter
and invalid message channel, passing it a `KafkaPublication` derived from the `KafkaPublication` of the data topic.
**Implementation** We must call the `KafkaMessageProducer`'s `Init` method to ensure that any topics are 
created/validated in accordance with the `MakeChannels` parameter.

**Creation Trigger**: We only create the producer for the DLQ or the invalid message channel, if the routing key for
them is not null.
- We need to add the paramters for `deadLetterTopic` and `invalidMessageTopic` to the constructor
- We need to assign them to `_deadLetterTopic` and `_invalidMessageTopic`

**Creation Timing**: The DLQ producer will be created **lazily** on first rejection:
- Not created in constructor (avoid unnecessary producer if never used)
- Created when first `RejectMessageAction` or `InvalidMessageAction` is encountered
- Cached for subsequent rejections
- May be null if there is no `_deadLetterTopic` or `_invalidMessageTopic`

**Lifetime Management**: The consumer owns the DLQ producer:
- Consumer creates the producer when needed
- Consumer disposes the producer in its own `Dispose()` method
- Producer shares the consumer's lifetime

**Thread Safety**: Uses `Lazy<T>` for thread-safe lazy initialization to handle concurrent first-rejection scenarios.

```csharp
        ... //existing KafkaMessageConsumerCode
        
        private readonly Lazy<IAmAMessageProducerSync> _deadLetterProducer;
        private readonly Lazy<IAmAMessageProducerSync> _invalidMessageProducer;
        private readonly RoutingKey? _deadLetterTopic = null;
        private readonly RoutingKey? _invalidMessageTopic = null;
       
       public KafkaMessageConsumer(
            KafkaMessagingGatewayConfiguration configuration,
            RoutingKey routingKey,
            string? groupId,
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            TimeSpan? sessionTimeout = null,
            TimeSpan? maxPollInterval = null,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            long commitBatchSize = 10,
            TimeSpan? sweepUncommittedOffsetsInterval = null,
            TimeSpan? readCommittedOffsetsTimeout = null,
            int numPartitions = 1,
            PartitionAssignmentStrategy partitionAssignmentStrategy = PartitionAssignmentStrategy.RoundRobin,
            short replicationFactor = 1,
            TimeSpan? topicFindTimeout = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            Action<ConsumerConfig>? configHook = null,
            RoutingKey? deadLetterTopic = null,
            RoutingKey? invalidMessageTopic = null
            )
        {
           ... //existing constructor code
               
           _deadLetterTopic = deadLetterTopic;
           _invalidMessageTopic = invalidMessageTopic; 
           
           // Thread-safe lazy initialization
           _deadLetterProducer = new Lazy<IAmAMessageProducerSync>(CreateDeadLetterProducer);
           _invalidMessageProducer = new Lazy<IAmAMessageProducerSync>(CreateInvalidMessageProducer);
           
         }
        
       private IAmAMessageProducerSync CreateDeadLetterProducer()
       {
          if (_deadLetterRoutingKey == null)
              return (IAmAMessageProducerSync )null; 

          var publication = new KafkaPublication
          {
              Topic = _deadLetterRoutingKey,
              MakeChannels = _configuration.MakeChannels // Match data topic strategy
          };

          var producer = new KafkaMessageProducer(_configuration, publication);
          producer.Init();
          return producer;
       }

       private IAmAMessageProducerSync CreateInvalidMessageProducer()
       {
          if (_invalidMessageRoutingKey == null)
              return (IAmAMessageProducerSync) null;

          var publication = new KafkaPublication
          {
              Topic = _invalidMessageSupport.InvalidMessageRoutingKey,
              MakeChannels = _configuration.MakeChannels
          };

          var producer = new KafkaMessageProducer(_configuration, publication);
          producer.Init();
          return producer;
       }
       
       public void Dispose()
       {
          // Only dispose if producer was created
          if (_deadLetterProducer.IsValueCreated)
              _deadLetterProducer.Value.Dispose();

          if (_invalidMessageProducer.IsValueCreated)
              _invalidMessageProducer.Value.Dispose();
      }
       
       ... //existing class code

```

### Sync vs Async Producers

Following ADR 0034:
- `KafkaMessageConsumer` (sync) uses `KafkaMessageProducer` (sync)
- `KafkaMessageConsumerAsync` (async) uses `KafkaMessageProducerAsync` (async)

Both implement the same logic, but with appropriate sync/async semantics.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                  KafkaMessageConsumer                    │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Message Processing Loop                            │ │
│  │                                                    │ │
│  │  1. Poll() from Kafka                             │ │
│  │  2. Deserialize message                           │ │
│  │     └─ Failure? → InvalidMessageAction            │ │
│  │  3. Pass to handler                               │ │
│  │     └─ RejectMessageAction thrown?                │ │
│  │        └─ Yes: Produce to DLQ                     │ │
│  │  4. Commit offset                                 │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ DLQ Producer (lazy)                                │ │
│  │                                                    │ │
│  │  _deadLetterProducer: KafkaMessageProducer        │ │
│  │  _invalidMessageProducer: KafkaMessageProducer    │ │
│  │                                                    │ │
│  │  Created on first rejection                       │ │
│  │  Shares consumer configuration                    │ │
│  │  Uses acks=all for reliability                    │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
                         │
                         ├─ Produce to DLQ Topic
                         ├─ Produce to Invalid Message Topic
                         └─ (bypasses Outbox)
```

## Consequences

### Positive

- **Simple lifecycle**: Consumer owns producer, no complex factory patterns needed
- **Lazy creation**: No overhead if DLQ never used (via `Lazy<T>`)
- **Thread-safe**: `Lazy<T>` ensures thread-safe initialization without explicit locking
- **Configuration consistency**: DLQ producer inherits consumer settings
- **Clean disposal**: `IsValueCreated` check ensures we only dispose created producers

### Negative

- **No retry on DLQ failure**: If DLQ production fails, message metadata is lost
- **Additional connections**: Each consumer creates its own DLQ producer (more connections to Kafka)
- **Resource usage**: DLQ producers consume memory/threads even if rarely used

### Risks and Mitigations

**Risk**: DLQ topic doesn't exist and `MakeChannels = Validate/Assume`
- **Mitigation**: Fail fast on first rejection with clear error message

**Risk**: DLQ producer creation fails (network, auth)
- **Mitigation**: Log error, let exception propagate to message pump for UnacceptableMessageCount handling

**Risk**: DLQ production consistently fails (topic full, broker down)
- **Mitigation**: Alerting on DLQ production failures via logging/metrics

**Risk**: Too many DLQ producers (one per consumer)
- **Mitigation**: Acceptable tradeoff for simplicity; Kafka handles many connections well

**Risk**: Message loss if DLQ production fails
- **Mitigation**: Log full message content in error log; add OpenTelemetry trace (per ADR 0034)

## Alternatives Considered

### Alternative 1: Shared DLQ Producer via Factory

Create a shared `IAmAMessageProducer` instance via a factory or registry, shared by all consumers.

**Rejected because**:
- Adds complexity (factory, registry, thread-safety)
- Lifetime management becomes unclear (who disposes?)
- Sync/async mismatch issues (factory needs to know sync vs async)
- Minimal benefit (Kafka handles many connections efficiently)

### Alternative 2: Use IAmACommandProcessor for DLQ

Send via `IAmACommandProcessor.Post()` instead of `IAmAMessageProducer`.

**Rejected because**:
- ADR 0034 already decided against this
- Requires deserialize → serialize round-trip
- Cannot handle InvalidMessageAction (deserialization already failed)
- More complex lookup via producer registry

### Alternative 3: Write Failed DLQ Messages to Local File

If DLQ production fails, write message to local file for later replay.

**Rejected because**:
- Adds complexity (file I/O, rotation, replay mechanism)
- Operational burden (monitoring disk space, manual replay)
- Out of scope for initial implementation
- Can be added later as enhancement

## References

- Requirements: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)
- Related ADRs:
  - [ADR 0045: Provide DLQ Where Missing](0045-provide-dlq-where-missing.md) - Establishes overall DLQ strategy
- External references:
  - [Kafka Producer API](https://kafka.apache.org/documentation/#producerapi)
  - [Dead Letter Queue Pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DeadLetterChannel.html)
