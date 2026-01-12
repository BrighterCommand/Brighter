# 36. DLQ Topic Creation Strategy

Date: 2026-01-11

## Status

Proposed

## Context

**Parent Requirement**: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)

**Scope**: This ADR focuses on how dead letter queue (DLQ) and invalid message topics are created, configured, and managed in Kafka. It addresses topic naming conventions, creation strategies, and configuration defaults.

### The Problem

When implementing DLQ support, we need to decide:

1. **Topic Naming**: What naming convention should be used for DLQ and invalid message topics?
2. **Creation Strategy**: Should topics be created automatically, validated, or assumed to exist?
3. **Topic Configuration**: What default configuration should DLQ topics have (partitions, replication, retention)?
4. **Consistency**: How do we ensure DLQ topic creation aligns with data topic creation?
5. **User Control**: How much control do users have over topic naming and configuration?

### Requirements Context

From the requirements:
- "Usability: Automatic DLQ topic creation should match automatic creation for the data topic i.e. create, validate or assume."
- "DLQ topic naming should be configurable (e.g., `{original-topic}.dlq` or custom pattern)"

### Constraints

- Must support the existing `OnMissingChannel` enum (Create, Validate, Assume)
- ADR 0035 established that DLQ producer inherits `MakeChannels` from data topic
- Must follow Kafka's topic naming rules (alphanumeric, `.`, `_`, `-`)

## Decision

### Topic Naming

We use the presence, or absence, of `IUseBrighterDeadLetterSupport` to indicate that a `Subscription` supports a 
Brighter defined dead letter queue and the presence, or absence, of `IUseBrighterInvalidMessageSupport` to indicate 
that a `Subscription` supports a Brighter defined invalid message channel.

Both of these interfaces work by providing a `RoutingKey` that need to be passed from the `Subscription` to the 
`Channel`, usually via the `IAmAChannelFactory` (see `InMemoryChannelFactory` as an example).

This means that naming is done on the derived `Subscription` type and is under control of the user. We do not 
provide a default name, because that would create a DLQ and invalid message channel by default.

```csharp
// Example configuration with custom names
var subscription = new KafkaSubscription<OrderCommand>(
    name: new SubscriptionName("orders-consumer"),
    channelName: new ChannelName("orders"),
    routingKey: new RoutingKey("orders"),
    groupId: "order-service",
    makeChannels: OnMissingChannel.Create)
{
    // Custom DLQ topic name
    DeadLetterRoutingKey = new RoutingKey("failed-orders"),
    // Custom invalid message topic name
    InvalidMessageRoutingKey = new RoutingKey("invalid-orders")
};
```

**Same Topic for Both**: If users want to use the same topic for DLQ and invalid messages, they can set both routing keys to the same value. The producer will handle this correctly (ADR 0035 creates separate `Lazy<T>` instances but they'll point to the same topic).

#### Topic Naming Convention

We will provide help with topic naming through a class. The class should take a string templates in its constructor 
that can be used to provide the naming strategy for the dead-letter channel and the invalid-message channel. The 
class then provides methods to produce these, i.e. `MakeDeadLetterChannelName` and `MakeInvalidMessageChannelName`. 
These methods take a `RoutingKey` as a parameter, allowing the user to create these names from their data channel's 
`RoutingKey`. 

We should provide a default naming template that can be used, so that it is not necessary to provide a template.

**Default Naming Pattern**:
- **Dead Letter Queue**: `{original-topic}.dlq`
- **Invalid Message Channel**: `{original-topic}.invalid`

**Examples**:
- Data topic: `orders` → DLQ: `orders.dlq`, Invalid: `orders.invalid`
- Data topic: `customer.events` → DLQ: `customer.events.dlq`, Invalid: `customer.events.invalid`

### Creation Strategy: Match Data Topic Behavior

The DLQ topic creation strategy **MUST match** the data topic's `MakeChannels` setting, as established in ADR 0035.

| Data Topic Setting | DLQ Topic Behavior |
|-------------------|-------------------|
| `OnMissingChannel.Create` | Create DLQ topic automatically if missing |
| `OnMissingChannel.Validate` | Verify DLQ topic exists; throw if missing |
| `OnMissingChannel.Assume` | Assume DLQ topic exists; let Kafka fail if missing |

**Rationale**: If a user has chosen automatic topic creation for their data topics, they expect the same for error handling topics. If they've chosen to pre-create topics (Validate/Assume), they'll do the same for DLQ topics.

**Implementation**: The `KafkaPublication` for DLQ producers will use the same `MakeChannels` value as the consumer's configuration:

```csharp
var publication = new KafkaPublication
{
    Topic = _deadLetterSupport.DeadLetterRoutingKey,
    MakeChannels = _configuration.MakeChannels // Inherit from data topic
};
```

### Topic Configuration Defaults

When automatically creating DLQ topics (`OnMissingChannel.Create`), we will use the following defaults:

**Partitions**: `1` (single partition)
- Rationale: DLQ is typically low-volume; ordering within DLQ is less critical
- Users can manually create with more partitions if needed

**Replication Factor**: Match cluster default (or `min.insync.replicas` if configured)
- Rationale: DLQ should be as durable as data topics
- Kafka will use broker's `default.replication.factor` setting

**Retention**: `7 days` (168 hours)
- Rationale: Longer than typical data topics (often 1-3 days) to allow investigation
- Users can override via manual topic creation or broker defaults

**Compression**: Match data topic compression (inherit from producer config)
- Rationale: Consistency with data topic behavior

**Cleanup Policy**: `delete` (time-based retention)
- Rationale: DLQ messages should eventually be removed; not a compacted log

```csharp
// Conceptual - topic configuration when creating
var topicSpec = new TopicSpecification
{
    Name = dlqTopicName,
    NumPartitions = 1,
    ReplicationFactor = -1, // Use broker default
    Configs = new Dictionary<string, string>
    {
        ["retention.ms"] = "604800000", // 7 days
        ["cleanup.policy"] = "delete"
    }
};
```

### Configuration API Design

Extend the `Subscription` class with DLQ topic configuration:

```csharp
public abstract class Subscription
{
    // Existing properties...
    public RoutingKey RoutingKey { get; set; }
    public OnMissingChannel MakeChannels { get; set; }

    // New DLQ properties (via interfaces)
}

public interface IUseBrighterDeadLetterSupport
{
    /// <summary>
    /// The routing key (topic name) for the dead letter queue.
    /// If null, DLQ is disabled.
    /// Default: null (disabled)
    /// </summary>
    RoutingKey? DeadLetterRoutingKey { get; }
}

public interface IUseBrighterInvalidMessageSupport
{
    /// <summary>
    /// The routing key (topic name) for invalid messages that cannot be deserialized.
    /// If null, invalid messages go to DeadLetterRoutingKey if available.
    /// Default: null (fallback to DLQ)
    /// </summary>
    RoutingKey? InvalidMessageRoutingKey { get; }
}
```

**Usage Example**:

```csharp
// Example 1: Automatic topic creation with default names
var subscription = new KafkaSubscription<OrderCommand>(
    name: new SubscriptionName("orders-consumer"),
    channelName: new ChannelName("orders"),
    routingKey: new RoutingKey("orders"),
    groupId: "order-service",
    makeChannels: OnMissingChannel.Create)
{
    DeadLetterRoutingKey = new RoutingKey("orders.dlq"),
    InvalidMessageRoutingKey = new RoutingKey("orders.invalid")
};
// Topics "orders.dlq" and "orders.invalid" will be auto-created

// Example 2: Pre-created topics with validation
var subscription = new KafkaSubscription<PaymentCommand>(
    name: new SubscriptionName("payments-consumer"),
    channelName: new ChannelName("payments"),
    routingKey: new RoutingKey("payments"),
    groupId: "payment-service",
    makeChannels: OnMissingChannel.Validate)
{
    DeadLetterRoutingKey = new RoutingKey("payments.dlq")
    // InvalidMessageRoutingKey not set - falls back to DeadLetterRoutingKey
};
// Will verify "payments.dlq" exists; throw if missing

// Example 3: Same topic for DLQ and invalid messages
var subscription = new KafkaSubscription<EventCommand>(
    name: new SubscriptionName("events-consumer"),
    channelName: new ChannelName("events"),
    routingKey: new RoutingKey("events"),
    groupId: "event-service",
    makeChannels: OnMissingChannel.Create)
{
    DeadLetterRoutingKey = new RoutingKey("events.errors"),
    InvalidMessageRoutingKey = new RoutingKey("events.errors") // Same topic
};
```

### Topic Creation Flow

```
┌─────────────────────────────────────────────────────────────┐
│           Consumer Initialization with DLQ                   │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
            ┌─────────────────────────────┐
            │ Create KafkaMessageConsumer │
            │ with Subscription           │
            └─────────────────────────────┘
                          │
                          ▼
            ┌─────────────────────────────┐
            │ Check MakeChannels setting  │
            └─────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
   [Create]          [Validate]        [Assume]
        │                 │                 │
        ▼                 ▼                 ▼
┌───────────────┐ ┌───────────────┐ ┌─────────────┐
│ Data topic:   │ │ Data topic:   │ │ Data topic: │
│ Auto-create   │ │ Verify exists │ │ Assume      │
│ if missing    │ │ (throw if not)│ │ exists      │
└───────────────┘ └───────────────┘ └─────────────┘
        │                 │                 │
        │                 │                 │
        ▼                 ▼                 ▼
┌───────────────────────────────────────────────────┐
│   Handler throws RejectMessageAction or           │
│   InvalidMessageAction (first time)               │
└───────────────────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────────────────┐
│   Lazy<T> triggers DLQ producer creation          │
└───────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────┐
│  Create KafkaPublication with:                      │
│  - Topic = DeadLetterRoutingKey                     │
│  - MakeChannels = _configuration.MakeChannels       │
└─────────────────────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────────────────────┐
│  KafkaMessageProducer initialization applies        │
│  MakeChannels to DLQ topic                          │
└─────────────────────────────────────────────────────┘
        │
  ┌─────┴─────┐
  ▼           ▼
[Create]  [Validate/Assume]
  │           │
  ▼           ▼
┌────────┐ ┌──────────┐
│ Create │ │ Validate │
│ DLQ    │ │ or       │
│ topic  │ │ Assume   │
└────────┘ └──────────┘
```

### Error Handling for Topic Creation

**OnMissingChannel.Create**:
- Attempt to create DLQ topic
- If creation fails (permissions, broker issues): Log error, let exception propagate
- This will increment UnacceptableMessageCount and potentially shut down consumer

**OnMissingChannel.Validate**:
- Check if DLQ topic exists
- If missing: Throw `ChannelFailureException` with clear message
- Exception propagates on first rejection

**OnMissingChannel.Assume**:
- Don't check topic existence
- On first produce, if topic missing: Kafka client will fail
- Error logged and offset committed (per ADR 0035 error handling)

## Consequences

### Positive

- **Consistent behavior**: DLQ topics follow same creation strategy as data topics
- **Flexible naming**: Users can use defaults or custom names
- **Simple configuration**: Just specify routing key, creation strategy is inherited
- **Fail-fast validation**: Issues with DLQ topics detected early (on first rejection)
- **Single topic option**: Can use same topic for DLQ and invalid messages
- **Reasonable defaults**: 1 partition, 7-day retention suitable for most cases

### Negative

- **Manual configuration needed for advanced scenarios**: Custom partition counts, retention policies require manual topic creation
- **No automatic partition matching**: DLQ won't automatically match data topic partition count
- **Limited topic config control**: Can't specify replication factor or retention via Brighter API
- **Naming convention not enforced**: Users can choose any names, losing discoverability

### Risks and Mitigations

**Risk**: DLQ topic has too few partitions, becomes bottleneck
- **Mitigation**: Document that high-volume DLQ scenarios should pre-create topics with more partitions

**Risk**: Users forget to create DLQ topics with `OnMissingChannel.Validate`
- **Mitigation**: Fail fast on first rejection with clear error message

**Risk**: DLQ topic fills up with long retention
- **Mitigation**: 7-day retention is reasonable; document monitoring DLQ depth

**Risk**: Users don't realize DLQ and data topic share creation strategy
- **Mitigation**: Document clearly; it's actually intuitive behavior

**Risk**: Permission issues creating DLQ topics
- **Mitigation**: Consumer fails fast; clear error message guides user to grant permissions

## Alternatives Considered

### Alternative 1: Always Require Manual Topic Creation

Force users to pre-create DLQ topics; never auto-create.

**Rejected because**:
- Requirements explicitly state "we MUST create if required"
- Inconsistent with data topic behavior (which can auto-create)
- Poor developer experience for local development and testing
- Makes simple scenarios harder than necessary

### Alternative 2: Independent DLQ Creation Strategy

Allow DLQ topics to have different `MakeChannels` setting from data topics.

```csharp
DeadLetterMakeChannels = OnMissingChannel.Create
```

**Rejected because**:
- Adds configuration complexity
- Users would need to think about two creation strategies
- ADR 0035 already established inheritance pattern
- Can achieve same result by manually creating/validating DLQ topic

### Alternative 3: Automatic Partition Matching

Automatically create DLQ topics with same partition count as data topic.

**Rejected because**:
- Requires querying data topic metadata (extra Kafka calls)
- DLQ typically has much lower volume than data topic
- Over-partitioning DLQ wastes resources
- Users with high DLQ volume can manually create with more partitions

### Alternative 4: Enforce Naming Convention

Require DLQ topics to follow strict naming: `{topic}.dlq` and `{topic}.invalid`.

**Rejected because**:
- Reduces flexibility for users with existing DLQ topics
- Some environments have naming policies
- Users might want different naming schemes
- ADR 0034 already allows custom routing keys via interfaces

### Alternative 5: Default to Longer Retention (30 days)

Use 30-day retention instead of 7-day default.

**Rejected because**:
- Most DLQ issues should be investigated within a week
- 30 days increases storage costs
- Users with compliance requirements can manually set retention
- 7 days aligns with common practices

## References

- Requirements: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)
- Related ADRs:
  - [ADR 0034: Provide DLQ Where Missing](0034-provide-dlq-where-missing.md) - Establishes overall DLQ strategy
  - [ADR 0035: Kafka DLQ Producer for Requeue](0035-kafka-dlq-producer-for-requeue.md) - Producer lifecycle and configuration
- External references:
  - [Kafka Topic Configuration](https://kafka.apache.org/documentation/#topicconfigs)
  - [Kafka AdminClient API](https://kafka.apache.org/documentation/#adminclient)
