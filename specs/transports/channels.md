# Channels

A `channel` is a virtual pipe that connects the message pump to the broker. We use the interface as an abstraction to allow the message pump to talk to multiple implementations of the channel for different brokers.

## Channels are Brighter Implemented

You MUST NOT to implement `IAmAChannel`, `IAmAChannelSync` or `IAmAChannelAsync` as these are provided by `Paramore.Brighter` through it's `Channel` class. 

You MUST implement `IAmAChannelFactory` and configure an instance of `Channel`. In particular, you need to pass the `Channel` an instance of an implementation of `IAmAMessageConsumer` for your middleware. See for example [ChannelFactory](../../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync/ChannelFactory.cs) which implements the `ChannelFactory` for Rabbit MQ (RMQ) and passes a [RmqMessageConsumer](../../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs) into the `Channel` having created one from the [RmqMessageConsumerFactory](../../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumerFactory.cs).

## Key Interfaces

The key interfaces are:

- `IAmAChannel` the base interface for talking to a channel. `IAmAChannelSync` and `IAmAChannelAsync` derive from it.
  - `Name` an internal identifier for the channel, may be used to name a queue if required.
  - `RoutingKey` lets us identify the channel
  - `Enqueue` adds a message from the middleware into the messagepump's buffer.
  - `Stop` stops reading from the channel, by posting a `WM-QUIT`message to the message pump
  - along with the ability to enqueue messages read from the middleware or stop the channel when we are done reading from it.
- `IAmAChannelSync` and `IAmAChannelAsync` provide the main lifecycle events for the `MessagePump`.
  - `Acknowledge` or `AcknowledgeAsync` indicates that a handler is done with a message and it can be ack'd (which may result in the middleware deleting the message from a queue or updating the offset into a stream).
  - `Purge` or `PurgeAsync` which drains a queue, discarding messages, or advances a stream to the latest message. Used for load-shedding or removing the results of testing.
  - `Receive` or `ReceiveAsync` which consumes a message from the middleware and returns it to the caller.
  - `Reject` or `RejectAsync` which rejects a message because it cannot be processed. It send to a dead-letter queue (DLQ) if one is available.
  - `Requeue` and `RequeueAsync` are used with transient errors to place them back in the queue or stream, after a delay.
