# Kafka Dead Letter Queue Usage Guide

This guide shows how to configure and use Dead Letter Queue (DLQ) and Invalid Message Channel support in Brighter's Kafka implementation.

## Table of Contents

1. [Basic DLQ Configuration](#basic-dlq-configuration)
2. [Using Naming Conventions](#using-naming-conventions)
3. [Invalid Message Channel](#invalid-message-channel)
4. [Rejecting Messages from Handlers](#rejecting-messages-from-handlers)
5. [Message Metadata](#message-metadata)
6. [Advanced Scenarios](#advanced-scenarios)

---

## Basic DLQ Configuration

The simplest way to configure a dead letter queue is to explicitly specify the DLQ topic when creating a subscription:

```csharp
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

// Create Kafka subscription with DLQ support
var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName("order-service-consumer"),
    routingKey: new RoutingKey("orders.events"),
    groupId: "order-service-group",
    numOfPartitions: 3,
    replicationFactor: 2,
    messagePumpType: MessagePumpType.Reactor,
    makeChannels: OnMissingChannel.Create,
    // Configure DLQ - messages that can't be processed go here
    deadLetterRoutingKey: new RoutingKey("orders.events.dlq")
);

// Create the consumer
var factory = new KafkaMessageConsumerFactory(
    new KafkaMessagingGatewayConfiguration
    {
        Name = "OrderService Kafka Consumer",
        BootStrapServers = new[] { "localhost:9092" }
    });

var consumer = factory.Create(subscription);
```

**What happens:**
- When a message fails processing after retries, it's sent to `orders.events.dlq`
- The DLQ topic is automatically created if `makeChannels` is set to `Create`
- Messages include metadata about the original topic, rejection reason, and timestamp

---

## Using Naming Conventions

Instead of manually specifying DLQ topic names, use naming conventions for consistent naming across your application:

```csharp
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

// Define your data topic
var dataTopic = new RoutingKey("orders.events");

// Use naming convention to create DLQ topic name
var dlqConvention = new DeadLetterNamingConvention(); // Defaults to "{0}.dlq"
var dlqTopic = dlqConvention.MakeChannelName(dataTopic);
// Result: "orders.events.dlq"

var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName("order-service-consumer"),
    routingKey: dataTopic,
    groupId: "order-service-group",
    numOfPartitions: 3,
    replicationFactor: 2,
    messagePumpType: MessagePumpType.Reactor,
    makeChannels: OnMissingChannel.Create,
    deadLetterRoutingKey: dlqTopic  // Use convention-based name
);
```

### Custom Naming Template

You can customize the naming convention:

```csharp
// Custom template: "failed-{0}"
var dlqConvention = new DeadLetterNamingConvention("failed-{0}");
var dlqTopic = dlqConvention.MakeChannelName(new RoutingKey("orders.events"));
// Result: "failed-orders.events"

// Custom template: "{0}-errors"
var dlqConvention = new DeadLetterNamingConvention("{0}-errors");
var dlqTopic = dlqConvention.MakeChannelName(new RoutingKey("orders.events"));
// Result: "orders.events-errors"
```

---

## Invalid Message Channel

In addition to the DLQ (for processing failures), you can configure an Invalid Message Channel for messages that can't be deserialized:

```csharp
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

var dataTopic = new RoutingKey("orders.events");

// Use naming conventions for both DLQ and invalid message channel
var dlqConvention = new DeadLetterNamingConvention();
var invalidConvention = new InvalidMessageNamingConvention(); // Defaults to "{0}.invalid"

var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName("order-service-consumer"),
    routingKey: dataTopic,
    groupId: "order-service-group",
    numOfPartitions: 3,
    replicationFactor: 2,
    messagePumpType: MessagePumpType.Reactor,
    makeChannels: OnMissingChannel.Create,
    deadLetterRoutingKey: dlqConvention.MakeChannelName(dataTopic),          // orders.events.dlq
    invalidMessageRoutingKey: invalidConvention.MakeChannelName(dataTopic)  // orders.events.invalid
);
```

**Routing Behavior:**
- **Deserialization failures** → Invalid Message Channel (`orders.events.invalid`)
- **Processing failures** (retries exhausted) → Dead Letter Queue (`orders.events.dlq`)
- **If invalid channel not configured** → Falls back to DLQ for deserialization failures
- **If no channels configured** → Message is acknowledged and warning logged

---

## Rejecting Messages from Handlers

You can explicitly reject messages from your message handlers using `RejectMessageAction`:

### Automatic Rejection (Retry Exhaustion)

When a handler throws an exception, Brighter automatically retries based on your policy. After retries are exhausted, the message is rejected to the DLQ:

```csharp
public class OrderCreatedEventHandler : RequestHandler<OrderCreatedEvent>
{
    public override OrderCreatedEvent Handle(OrderCreatedEvent command)
    {
        try
        {
            // Process the order
            _orderService.CreateOrder(command);
            return command;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", command.OrderId);
            throw; // After retries exhausted, goes to DLQ
        }
    }
}
```

### Explicit Rejection

Force immediate rejection to the DLQ without retries:

```csharp
using Paramore.Brighter;

public class OrderCreatedEventHandler : RequestHandler<OrderCreatedEvent>
{
    public override OrderCreatedEvent Handle(OrderCreatedEvent command)
    {
        // Validate the message
        if (!IsValidOrder(command))
        {
            _logger.LogWarning("Invalid order received: {OrderId}", command.OrderId);

            // Explicitly reject to DLQ - no retries
            throw new RejectMessageAction("Order validation failed: missing required fields");
        }

        // Process valid orders
        _orderService.CreateOrder(command);
        return command;
    }

    private bool IsValidOrder(OrderCreatedEvent command)
    {
        return !string.IsNullOrEmpty(command.CustomerId) &&
               !string.IsNullOrEmpty(command.OrderId) &&
               command.TotalAmount > 0;
    }
}
```

### Defer with Rejection on Exhaustion

Use `DeferMessageAction` to retry with delays, then reject to DLQ when exhausted:

```csharp
public class OrderCreatedEventHandler : RequestHandler<OrderCreatedEvent>
{
    private readonly int _maxRetries = 3;

    public override OrderCreatedEvent Handle(OrderCreatedEvent command)
    {
        try
        {
            // Try to process the order
            _orderService.CreateOrder(command);
            return command;
        }
        catch (TemporaryServiceException ex)
        {
            // Service temporarily unavailable - retry with delay
            var currentRetry = command.RetryCount; // Track retries in message

            if (currentRetry < _maxRetries)
            {
                _logger.LogWarning(
                    "Service unavailable, deferring order {OrderId} (attempt {Attempt}/{Max})",
                    command.OrderId, currentRetry + 1, _maxRetries);

                // Defer for 5 seconds - message will be retried
                throw new DeferMessageAction(TimeSpan.FromSeconds(5));
            }
            else
            {
                // Max retries reached - send to DLQ
                _logger.LogError(
                    "Service unavailable after {MaxRetries} retries, rejecting order {OrderId}",
                    _maxRetries, command.OrderId);

                throw new RejectMessageAction($"Service unavailable after {_maxRetries} retries");
            }
        }
    }
}
```

---

## Message Metadata

When a message is sent to the DLQ or invalid message channel, Brighter automatically adds metadata to help with troubleshooting:

```csharp
// When consuming from DLQ, you can access the metadata:

var dlqSubscription = new KafkaSubscription<MyCommand>(
    subscriptionName: new SubscriptionName("DLQMonitor"),
    channelName: new ChannelName("dlq-monitor"),
    routingKey: new RoutingKey("orders.events.dlq"),
    groupId: "dlq-monitor-group",
    messagePumpType: MessagePumpType.Reactor
);

var dlqConsumer = factory.Create(dlqSubscription);
var messages = dlqConsumer.Receive(TimeSpan.FromSeconds(1));

foreach (var message in messages)
{
    // Access rejection metadata from message headers
    var originalTopic = message.Header.Bag["OriginalTopic"];           // "orders.events"
    var rejectionReason = message.Header.Bag["RejectionReason"];       // "DeliveryError" or "Unacceptable"
    var rejectionTimestamp = message.Header.Bag["RejectionTimestamp"]; // ISO 8601 format
    var rejectionMessage = message.Header.Bag["RejectionMessage"];     // Custom error message
    var originalType = message.Header.Bag["OriginalType"];             // "MT_COMMAND"

    _logger.LogWarning(
        "DLQ Message - Original Topic: {Topic}, Reason: {Reason}, Time: {Time}, Error: {Error}",
        originalTopic, rejectionReason, rejectionTimestamp, rejectionMessage);

    // Optionally replay the message or log for manual intervention
}
```

### Available Metadata Fields

| Field | Header Key | Description | Example |
|-------|-----------|-------------|---------|
| Original Topic | `OriginalTopic` | The topic the message was originally sent to | `orders.events` |
| Rejection Timestamp | `RejectionTimestamp` | When the message was rejected (ISO 8601) | `2026-01-18T10:30:00.000Z` |
| Rejection Reason | `RejectionReason` | Why the message was rejected | `DeliveryError`, `Unacceptable` |
| Rejection Message | `RejectionMessage` | Custom error description from handler | `Order validation failed` |
| Original Type | `OriginalType` | Original message type | `MT_COMMAND`, `MT_EVENT` |

---

## Advanced Scenarios

### Scenario 1: Different DLQ Per Environment

```csharp
var environment = _configuration["Environment"]; // "dev", "staging", "prod"
var dataTopic = new RoutingKey("orders.events");

// Include environment in DLQ name
var dlqConvention = new DeadLetterNamingConvention($"{{0}}.dlq.{environment}");
var dlqTopic = dlqConvention.MakeChannelName(dataTopic);
// Result: "orders.events.dlq.prod"

var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName($"order-service-{environment}"),
    routingKey: dataTopic,
    groupId: $"order-service-{environment}",
    deadLetterRoutingKey: dlqTopic
);
```

### Scenario 2: No DLQ (Acknowledge and Log Only)

```csharp
// Create subscription WITHOUT DLQ or invalid message routing keys
var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName("order-service-consumer"),
    routingKey: new RoutingKey("orders.events"),
    groupId: "order-service-group",
    messagePumpType: MessagePumpType.Reactor
    // No deadLetterRoutingKey or invalidMessageRoutingKey specified
);

// When a message is rejected:
// 1. Warning is logged: "NoChannelsConfiguredForRejection"
// 2. Message is acknowledged (removed from topic)
// 3. No DLQ message is produced
```

**Use case:** When you want to discard failed messages rather than preserve them.

### Scenario 3: DLQ Only (No Invalid Message Channel)

```csharp
// Configure DLQ but not invalid message channel
var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName("order-service-consumer"),
    routingKey: new RoutingKey("orders.events"),
    groupId: "order-service-group"),
    deadLetterRoutingKey: new RoutingKey("orders.events.dlq")
    // No invalidMessageRoutingKey - falls back to DLQ for invalid messages
);

// Behavior:
// - Processing failures → orders.events.dlq
// - Deserialization failures → orders.events.dlq (fallback)
```

### Scenario 4: Async Consumer with DLQ

```csharp
var subscription = new KafkaSubscription<OrderCreatedEvent>(
    subscriptionName: new SubscriptionName("OrderService"),
    channelName: new ChannelName("order-service-consumer"),
    routingKey: new RoutingKey("orders.events"),
    groupId: "order-service-group",
    messagePumpType: MessagePumpType.Proactor,  // Async!
    deadLetterRoutingKey: new RoutingKey("orders.events.dlq"),
    invalidMessageRoutingKey: new RoutingKey("orders.events.invalid")
);

// Use CreateAsync for async consumers
var consumer = factory.CreateAsync(subscription);

// RejectAsync is used automatically by the async message pump
// Same metadata and routing logic as sync version
```

### Scenario 5: Custom Rejection Routing Strategy

If you need to route different rejection types to different topics, you can implement custom logic in your handler:

```csharp
public class OrderCreatedEventHandler : RequestHandler<OrderCreatedEvent>
{
    public override OrderCreatedEvent Handle(OrderCreatedEvent command)
    {
        try
        {
            ValidateOrder(command);
            _orderService.CreateOrder(command);
            return command;
        }
        catch (ValidationException ex)
        {
            // Validation failures - reject as unacceptable
            // These go to invalid message channel if configured
            throw new RejectMessageAction(ex.Message);
        }
        catch (ServiceException ex)
        {
            // Service failures - allow retries, then DLQ
            // These go to dead letter queue
            throw; // Will retry and eventually reject
        }
    }
}
```

---

## Summary

**Key Points:**

1. **Configure DLQ** by setting `deadLetterRoutingKey` on your `KafkaSubscription`
2. **Use Naming Conventions** for consistent naming: `DeadLetterNamingConvention` and `InvalidMessageNamingConvention`
3. **Configure Invalid Message Channel** by setting `invalidMessageRoutingKey` for deserialization failures
4. **Reject Explicitly** using `RejectMessageAction` in your handlers
5. **Defer with Retry** using `DeferMessageAction` before final rejection
6. **Access Metadata** from `message.Header.Bag` when processing DLQ messages
7. **Automatic Routing**: DeliveryError → DLQ, Unacceptable → Invalid (with DLQ fallback), Unknown → DLQ
8. **No Config Fallback**: If no channels configured, messages are acknowledged and logged

**References:**
- ADR 0035: Kafka DLQ Producer for Requeue
- ADR 0036: Message Rejection Routing Strategy
