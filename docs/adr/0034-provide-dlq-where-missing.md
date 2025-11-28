# 34. Provide a Dead Letter Channel Where Native Support is Mising

Date: 2025-11-25

## Status

Proposed

## Context

A Dead Letter Channel holds undeliverable messages. Typically, when the channel consumer has tried, but failed, to process, a message a certain number of times, it will move it to a Dead Letter Channel, where it can be examined to determine why it failed, and if it needs to be re-processed. This avoids the issue of a "poision pill" message being repeatedly offered to a consumer, blocking them because it will always fail.

Kafka is a streaming solution, so it uses an append only log. Consumers do not delete records from the stream, they commit the offsets of those records that they have read. When a consumer encounters a record that it cannot process, the consumer can choose blocking retry (wait until it works), or load shedding (mark the offset as read, tackle the next record). The strategy chosen depends on whether it is possible to skip a record without corrupting downstream state.

When load-shedding, because a stream does not delete an undeliverable message, but instead marks it's offset as read, it is possible to go back and look at the record (until its TTL). Thus, theoretically, records can be reviewed in their original stream so a dead-letter channel would be redundant.

Why then would we need a Dead Letter Channel for a stream?

Normally the answer is process, with monitoring of Dead Letter Channels a process that a team adheres too. Reviewing the channel, because it has more than zero records is easier than looking for the offset of records that failed in the logs, and examining the instead. Replaying from the channel is easier than reprocessing the record in its original position in the stream.

The other answer is that developers are using Kafka as a queue, not a stream. Records can be processed individually, and partition assignment is random. Typically this occurs because developers are working in environments without a queue. We note that Kafka is adding queues to resolve this situation, but we also note that a Dead Letter Channel is not in the early releases.

Brighter has previously avoided supporting a Kafka Dead Letter Channel, feeling that it does not fit the notion of stream-processing. However, we have persistent requests for a Dead Letter Channel.

Some queues also lack provision of Dead Letter Channel. This implies that when they hit a requeue limit, we will simply drop the message. If we make it possible to add a Dead Letter Channel, this problem can be overcome. 

In addition, there is a related notion of an Invalid Message Channel. Technically a seperate concept, it allows us to divert messages that cannot be deserialied to a channel for inspection. This is because it may be a good message, but a configuration issue (versioning, naming, etc.) has prevented it from being deserialized.

In practice, a lot of implementations use the same physical queue or stream for these, to avoid having to inspect multiple channels.

Some middleware directly supports a Dead Letter Channel. We should defer to that implementation, where available, over offering a bespoke one. 

## Decision

Because there is no harm in adding a Dead Letter Channel for transports that don't have native support for one, we should add one.

The current use of Dead Letter Channel is triggered when the code throws a `DeferMessageAction` and the maximum number of retries is exceeded. In the case of a stream, there may be no desire to retry first. In addtion, to support usage for an Invalid Message Channel, we don't have that option. As such we will add a new exception `RejectMessageAction` that will call `Reject` on the `IAmAProducer`. We MUST advise that for most errors, you should simply throw a normal exception, you only want to use this option if you want to move the `Message` to the DLQ. The `RejectMessageAction` should support an enum that indicates whether you want to send to the Invalid Message Channel or Dead Letter Channel. For the Dead Letter Channel it adds an optional string that describes why you are Dead Lettering. We should record this information in logs and traces, but also add it into the `Bag` in the `MessageHeader`.

We will need to extend the `Subscription` to allow for the declaration of the Dead Letter `RoutingKey`; we should also allow an Invalid Message `RoutingKey`. Both can be the same. To allow for middleware that supports native Dead Letter Channels, alongside middleware that needs Brighter support, we should create an interface `IUseBrighterDeadLetterSupport` and an `IUseBrighterInvalidMessageSupport` to hold the declaration. The topic should be nullable, even if we support a Dead Letter Channel, users may choose not to use it, for reasons described above.

The most important design decision is how we produce the message, as we need a Producer. Two paths are possible:

- The first path is send via `IAmACommandProcessor`. For this approach, we add the Dead Letter Channel via `AddProducers` and just add the `Topic` name to the `Subscription`. The advantage of this approaach is that we already pass the `IAmACommandProcessor` to the `MessagePump` so it would be easily accessible at that point.
- The second path is to use a `IAmAMessageProducer` directly. This requires us to add a `KafkaPublication` that identifies the topic to the `Subscription` and create an `IAmAMesageProducer` on the KafkaMessageConsumer

The problem with the first option is that `IAmACommandProcessor` deals with a `IRequest` not a `Message`. As such we would need to deserialize the body back into an `IRequest` and then send it over the `IAmACommandProcessor`. This would mean that we could not use this solution for an Invalid Message Channel as it would create an error. It also results in serializing, just to deserialize. 

The latter approach is made easier, because we know which topic and producer to use, so unlike the `IAmACommandProcessor` path, which needs to look up the `IAmAMessageProducer` for the `RoutingKey` we don't, so we don't need to use an `IAmAProducerRegistry`, instead we just need an `IAmAMessageProducer`.

In Reject, The presence of the Dead Letter Channel or Invalid Message Channel topic indicates that we should produce the current message to the relevant channel, depending on the enum.

## Consequences

We will extend `Subscription` with a `Publication` for the Dead Letter Channel and the Invalid Message Channel. These will be provided by new interfaces that define these new roles. This is an additive and not a breaking change.

Note that we don't write dead letter or invalid to the `IAmAnOutbox`. We believe that this is accpetable, and that we don't require Transactional Messaging on the error path.

We will add a new exception We will change the behaviour when rejecting a message, where there is a dead letter channel or invalid message channel, as appropriate, if that is supported. We will support existing `Reject` behavious, such as committing the offset on a stream. 


