# Writing a Transport

A transport is an assembly that implements the required interfaces to expose specific messaging middleware to Brighter. For example, the assembly [Paramore.Brighter.MessagingGateway.Kafka](../../../src/Paramore.Brighter.MessagingGateway.Kafka/) exposes Kafka to Brighter.

When using messaging, Brighter itself acts as a [Messaging Gateway](https://www.enterpriseintegrationpatterns.com/patterns/messaging/MessagingGateway.html). Within the Gateway, a transport is a [Channel Adapter](https://www.enterpriseintegrationpatterns.com/patterns/messaging/ChannelAdapter.html) as it abstracts a specific channel type (i.e. broker) to Brighter.

## Naming of Assemblies

We name the assembly `Paramore.Brighter.MessagingGateway.X` where `X` is the name of the messaging middleware or broker we are adapting. For example, `Paramore.Brighter.MessagingGateway.Kafka` is the name of the assembly that acts as an adapter for `Kafka`.

We name the test assembly for `Paramore.Brighter.MessagingGateway.X` as `Paramore.Brighter.X.Tests`. You MUST put all tests for the messaging gateway in that assembly. We add a Docker Compose file to enable us to create the middleware locally. We name the Docker Compose file for the middleware. so `docker-compose-X.yaml` where X is the name of the middleware, for example  These tests use I/O to the middleware over test doubles. You SHOULD NOT write tests using test doubles here, unless it provides some specificity or clarity.

## Modularity

- You MUST only write code in the assemblies that you create for these transports.
- The assemblies you SHOULD write code in will be `Paramore.Brighter.MessagingGateway.X` and `Paramore.Brighter.X.Tests` where X is the name of the middleware that you are writing the messaging gateway for.
- You MUST NEVER write code in `Paramore.Brighter` or `Paramore.Brighter.ServiceActivator`. If you need changes in those assemblies, write a specification of what you need and request help by raising a GitHub issue.

## Overview of a Transport With Links

The following section provides an overview of writing a transport, with links to more detailed specifications.

### Required Interfaces

The following interfaces are required to implement a transport.

- `IAmAMessageProducer` allows us to send messages via the middleware. There are derived interfaces for sync and async producers. See [producers](./producers.md) for more information on how to implement.
- You SHOULD derive a type from [`Publication`](../../../src/Paramore.Brighter/Publication.cs) to store any configuration details needed to integrate with your implementation of `IAmAMessageProducer`. See [Publication](publication.md) for more details on how to implement.
- `IAmAMessageConsumer` allows us to read messages from the middleware (via a queue or stream). There are derived interfaces for sync and async producers. See [consumers](./consmers.md) for more information on how to implement.
- You SHOULD derive a type from [`Subscription`](../../../src/Paramore.Brighter/Subscription.cs) to store any configuration details needed to integrate with your implementation of `IAmAMessageConsumer`
- `IAmAChannelFactory` is a factory that allows Brighter to create instances of a `IAmAChannel` for a specific middleware. The channel factory injects a middleware-specific instance of `IAmAMessageConsumer` into the channel.
  - You MUST implement `IAmAChannelFactory` see [channel factory](./channelfactory.md) for more information on how to implement.
    - A channel factory returns a `Channel`, which implements`IAmAChannel`. The `IAmAChannel` interface allows our message pump to consume messages from a channel. There are derived interfaces for sync and async producers. See [channels](./channels.md) for more information on channels, but you MUST NOT implement `IAmAChannel` as Brighter implements this interface for you in `Channel`.
