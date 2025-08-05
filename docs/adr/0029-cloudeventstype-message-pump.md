# 29.  Use CloudEventsType for Message Pump

Date: 2025-07-14

## Status

Accepted

## Context

We assume that the message pump listens to a [DataType Channel](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DatatypeChannel.html), which is a channel that can receive messages of a specific type. The message pump will then process these messages and send them to the appropriate destination.

What happens then if a producer decides to send multiple types of messages to the same channel? In this case, we need a way to differentiate between the different types of messages. We can use the [CloudEvents](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md) `type` field for deciding what the actual type of the message is. This allows us to use the same channel for multiple types of messages, while still being able to differentiate between them. From the documentation: "This attribute contains a value describing the type of event related to the originating occurrence. Often this attribute is used for routing, observability, policy enforcement, etc. SHOULD be prefixed with a reverse-DNS name. The prefixed domain dictates the organization which defines the semantics of this event type."

In this case then the metadata of the message decides how it is routed. Our existing message pump implementation does not support this as we configure the type of the channel via a generic parameter, yet in this case we do not know the type of the message in advance. We need to change the implementation of the message pump to support this.

## Decision

We need to remove the generic parameter from Reactor and Proactor and instead rely on runtime determination of the type. To do this we will use the [Strategy Pattern](https://en.wikipedia.org/wiki/Strategy_pattern) passing in a `Func<Message, Type>` to the message pump. This function will be used to determine the type of the `IRequest` at runtime based on the metadata of the message.  The `Subscription` will then set this, allowing the user to use strategies other than DataType Channel, for example, reading the Cloud Events type. We will make the `Func<Message, Type>` an otional parameter of the `Subscription` and default to returning the `DataType` of the `Subscription` if not set, which is the current behavior. This will allow us to keep the existing behavior for users that do not need to differentiate between message types.

In order to send multiple types of messages to the same channel, we need to break the alignment between a `Producer` and a DataType channel as well.  At this point we only allow a single `Producer` per `RoutingKey` in our `ProducerRegisttry` which means that we cannot have multiple producers for the same channel. We need to change this to allow multiple producers for the same channel. To do this we will create a `ProducerKey` which allows the `ProducerRegistry` to look up a `Producer` by `RoutingKey` and `CloudEventsType`. This will allow us to have multiple producers for the same channel, each with a different `CloudEventsType`.  As `FindPublicationsByPublicationTopicOrRequestType` looks up a `Producer` by default using `IRequest` then we will find the correct `Producer` based on that, as each `Producer` is for a unique `IRequest`.

However, `FindPublicationsByPublicationTopicOrRequestType`  also allows us to set a `PublicationTopic` on an `IRequest` to indicate which topic it belongs to, overridding the `Publication` we will also allow the `PublicatonTopic` to set the `CloudEventsType` of the message. This will allow us to use the same `PublicationTopic` for multiple messages to the same `RoutingKey`, and cache those results, while still being able to differentiate between them.

In addition `FindPublicationsByPublicationTopicOrRequestType` allows us to use the `RequestContex` to override the `RoutingKey` we will adjust the existing `FindPublicationsByPublicationTopicOrRequestType` to disambiguate multiple producers matching the same `RoutingKey` by using the `CloudEventsType` as well.

## Consequences

There is likely to be an impact on performance as we move from a compile time decision, and one which optimizes by pre-building its meaage mapper middleware pipeline, to one which needs to calculate the type at runtime. This is a trade-off we are willing to make in order to support the use case of multiple message types on the same channel. However, we should look at options to optimize this in the future, such as caching the type of the message based on the metadata.

An easy first optimization would be to cache the unwrap pipeline for a type after creating it, so that when we receive the same type again, we can reuse the existing pipeline instead of creating a new one. This would reduce the overhead of creating the pipeline for each message and improve performance. Note that we use a transient cache that is destroyed when the Proactor/Reactor is disposed. The trade-off is that it is unlikely that handler pipelines will be shared between pumps, and making it static creates problems of cache invalidation, such as with tests.

On a Subscription, we can make the DataType optional as use the <see cref="Func{Message, Type}"/> to determine the type of the message at runtime. If you are not using the DataType Channel, you do not need this. However, we should assert that the DataType is not null if the strategy for mapping Message to Request is null, as it implies defaulting to the DataType Channel.

We should add an attribute to the `iAmARequestHander` `Handle` method which can be used to indicate tht we dynamically route to that type and it should be excluded from auto assembly registration. It can also indicate the value of Cloud Events type to store, and the corresponding `Request` type to build a lookup table so that you can configure the routing to a handler via metadata.
