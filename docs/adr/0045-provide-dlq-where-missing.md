# 45. Provide a Dead Letter Channel Where Native Support is Missing

Date: 2025-11-25

## Status

Accepted

## Context

A Dead Letter Channel holds undeliverable messages. Typically, when the channel consumer has tried, but failed, to process, a message a certain number of times, it will move the message to a Dead Letter Channel, where it can be examined to determine why it failed, and if it needs to be re-processed. This avoids the issue of a "poison pill" message being repeatedly offered to a consumer, blocking them because it will always fail.

Kafka is a streaming solution, so it uses an append only log. Consumers do not delete records from the stream, they commit the offsets of those records that they have read. When a consumer encounters a record that it cannot process, the consumer can choose blocking retry (wait until it works), or load shedding (mark its offset as read, tackle the next record). The strategy chosen depends on whether it is possible to skip a record without corrupting downstream state.

When load-shedding, because a stream does not delete an undeliverable message, but instead marks it's offset as read, it is possible to go back and look at the record (until its TTL). Thus, theoretically, records can be reviewed in their original stream so a dead-letter channel would be redundant.

Why then would we need a Dead Letter Channel for a stream?

Normally the answer is process, with monitoring of Dead Letter Channels a process that a team adheres too. Reviewing the channel when it contains records is easier than searching logs for failed record offsets and examining them individually. Replaying from the Dead Letter Channel is easier than reprocessing the record in its original position in the stream. Teams may automatic alerts set to any Dead Letter Channel in their system. If there is a Dead Letter Channel and it has entries - the team has red lights in their dashboard and inboxes.

The other answer is that developers are using Kafka as a queue, not a stream. Records can be processed individually, and partition assignment is random. Typically this occurs because developers are working in environments without a queue. We note that Kafka is adding queues to resolve this situation, but we also note that a Dead Letter Channel is not in the early releases.

Brighter has previously avoided supporting a Kafka Dead Letter Channel, feeling that it does not fit the notion of stream-processing. However, we have persistent requests for a Dead Letter Channel. It is possible to workaround this through the use of a [Fallback handler](https://brightercommand.gitbook.io/paramore-brighter-documentation/brighter-request-handlers-and-middleware-pipelines/policyfallback) to write directly to a `RoutingKey` 

Some queues also lack provision of Dead Letter Channel. This implies that when they hit a requeue limit, we will simply drop the message. If we make it possible to add a Dead Letter Channel, this problem can be overcome. 

In addition, there is a related notion of an Invalid Message Channel. Technically a separate concept, it allows us to divert messages that cannot be deserialied to a channel for inspection. This is because it may be a good message, but a configuration issue (versioning, naming, etc.) has prevented it from being deserialized.

In practice, a lot of implementations use the same physical queue or stream for these, to avoid having to inspect multiple channels.

Some middleware directly supports a Dead Letter Channel. We should defer to that implementation, where available, over offering a bespoke one. 

## Decision

Because there is no harm in adding a Dead Letter Channel for transports that don't have native support for one, we should add one.

The current use of Dead Letter Channel is triggered when the code throws a `DeferMessageAction` and the maximum number of retries is exceeded. In the case of a stream, there may be no desire to retry first. As such we will add a new exception `RejectMessageAction` that will call `Reject` on the `IAmAProducer`. 

We MUST advise that for most errors, you should simply throw a normal exception, you only want to use this option if you want to move the `Message` to the DLQ. To be specific:

- Use DeferMessageAction: Transient failures (database timeouts, network issues), retryable errors 
- Use RejectMessageAction: Transient failures that exceed retry count, malformed messages, messages with wrong schema versions, explicit monitoring of failed messages due to inability to process, skipping over a message in a stream 
- Avoid use RejectMessageAction: messages that violate business invariants in a way that retry won't fix, prefer application logging

The `RejectMessageAction` should take an optional string that indicates why the message was rejected. If present the reason should be added to the `MessageHeader.Bag' property under a key of `RejectionReason`.

In addtion, to support usage for an Invalid Message Channel, the framework should throw `InvalidMessageAction` for a failed deserialization of a message, to force the message into any supported Invalid Message Channel.   

We MUST advise that for most errors, you should simply throw a normal exception, you only want to use this option if you want to move the `Message` to the DLQ.

We will need to extend the `Subscription` to allow for the declaration of the Dead Letter `RoutingKey`; we should also allow an Invalid Message `RoutingKey`. Both can be the same. To allow for middleware that supports native Dead Letter Channels, alongside middleware that needs Brighter support, we should create an interface `IUseBrighterDeadLetterSupport` and an `IUseBrighterInvalidMessageSupport` to hold the declaration. The topic should be nullable, even if we support a Dead Letter Channel, users may choose not to use it, for reasons described above.

The most important design decision is how we produce the message, as we need a Producer. Two paths are possible:

- The first path is send via `IAmACommandProcessor`. For this approach, we add the Dead Letter Channel via `AddProducers` and just add the `Topic` name to the `Subscription`. The advantage of this approaach is that we already pass the `IAmACommandProcessor` to the `MessagePump` so it would be easily accessible at that point.
- The second path is to use a `IAmAMessageProducer` directly. This requires us to add a `KafkaPublication` that identifies the topic to the `Subscription` and create an `IAmAMesageProducer` on the KafkaMessageConsumer

The problem with the first option is that `IAmACommandProcessor` deals with a `IRequest` not a `Message`. As such we would need to deserialize the body back into an `IRequest` and then send it over the `IAmACommandProcessor`. This would mean that we could not use this solution for an Invalid Message Channel as it would create an error. It also results in serializing, just to deserialize. 

The latter approach is made easier, because we know which topic and producer to use, so unlike the `IAmACommandProcessor` path, which needs to look up the `IAmAMessageProducer` for the `RoutingKey` we don't, so we don't need to use an `IAmAProducerRegistry`, instead we just need an `IAmAMessageProducer`.

```csharp
public interface IUseBrighterDeadLetterSupport
{
    RoutingKey? DeadLetterRoutingKey { get; }
}

public interface IUseBrighterInvalidMessageSupport  
{
    RoutingKey? InvalidMessageRoutingKey { get; }
}
```

The `IAmAMessageConsumerSync` or `IAmAMessageConsumerAsync` must manage the lifetime of the `IAmAMessageProducerSync` or `IAmAMessageProducerAsync` needed to post the the Dead Letter Channel or Invalid Message Channel. The 'sync' or 'async' of the producer should match the consumer i.e. `IAmAMessageConsumerSync` shold match up to an `IAmAMessageProducerSync`.

Note that messages sent to the Dead Letter Channel or Invalid Message Channel are produced directly via IAmAMessageProducer and do NOT go through the Outbox. This means they are not part of transactional messaging guarantees. We believe this is acceptable for the error path. We should extend Brighter's Open Telemetry support to ensure that the message contents can be read from the trace, for investigation.

In `Reject`, the presence of the Dead Letter Channel indicates that we should produce the current message to the relevant channel. When translating a message the presence of an Invalid Message Channel indicates that messages that we cannot deserialize should be moved to the Invalid Message Channel.

Tests should be written against an `InMemoryConsumer` and `InternalBus` to prove the flow, and within individual transports.

## Consequences

We will add a new exception `RejectMessageAction`.

We will extend `Subscription` with a `Publication` for the Dead Letter Channel and the Invalid Message Channel. These will be provided by new interfaces that define these new roles. This is an additive and not a breaking change.

Note that we don't write dead letter or invalid to the `IAmAnOutbox`. We believe that this is accpetable, and that we don't require Transactional Messaging on the error path.

We will change the behaviour when rejecting a message, where there is a dead letter channel or invalid message channel, as appropriate, if that is supported.  We will support existing `Reject` behavious, such as committing the offset on a stream.

We maintain an UnacceptableMessageCount which is incremented when we reject a message. Once we exceed the limit, we terminate the consumer. The intent is to prevent good messages being moved to a dead letter channel in bulk, due to a systemic issue (such as db failure). If we increase our propensity to reject messages by allowing code to throw a `RejectMessageAction` then we need to ensure that we document this behavior. We do not shut down if the UnacceptableMessageCount is zero; we should alter this to zero or less. In addition, we should add a time window for the UnacceptableMessageCount, to ensure that we shut down for a high number of rejections within that window, not in the programme lifetime.  

Teams should consider whether messages in DLQ/Invalid Message channels contain sensitive data that requires special handling (encryption, access controls, retention policies). 

Observability: DLQ operations should emit appropriate logs, traces, and metrics to enable monitoring and alerting on DLQ depth.

Retention policies for DLQ messages should be defined at the transport level. 

We will want to add a migration guide for users currently using fallback handlers for DLQ.

We will want to add to our documentation examples of the new RejectMessageAction usage. We will want to show configuration examples for each supported transport.







