# Channel Factory

A channel factory is used to create instances of Brighter's `Channel` and `ChanngeAsync` classes which are [Channel Adapter](https://www.enterpriseintegrationpatterns.com/patterns/messaging/ChannelAdapter.html)s that abstract away details of how we consume from a transport (a queue or stream).

The channel factory creates the `IAmAMessageConsumer' for the transport and passes it into the channel to allow it to read messages from the consumer.

## Implementation

You MUST create an implementation of `IAmAChannelFactory`. Your implementation of [`IAmAChannelFactory`](../../src/Paramore.Brighter/IAmAChannelFactory.cs) MUST use `Channel` or `ChannelAsync` as appropriate. 

The factory MUST create an instance of `Channel` in response to a call to `CreateSyncChannel` or an instance of `ChannelAsync` in response to a call to `CreateAsyncChannel` or `CreateAsyncChannelAsync`. You MUST pass the `Channel` or `ChannelAsync` an instance of the implementation of `IAmAMessageConsumer` for the matching middleware, see [Consumers](./consumers.md) for more.

See for example [ChannelFactory](../../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync/ChannelFactory.cs) which implements the `ChannelFactory` for Rabbit MQ (RMQ) creates and passes an [RmqMessageConsumer](../../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs) as  a parameter the `Channel` having created one from the [RmqMessageConsumerFactory](../../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumerFactory.cs).