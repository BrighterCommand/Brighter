# 29.  Use CloudEventsType for Message Pump

Date: 2025-07-14

## Status

Accepted

## Context

We assume that the message pump listens to a [DataType Channel](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DatatypeChannel.html), which is a channel that can receive messages of a specific type. The message pump will then process these messages and send them to the appropriate destination.

What happens then if a producer decides to send multiple types of messages to the same channel? In this case, we need a way to differentiate between the different types of messages. We can use the [CloudEvents](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md) `type` field for deciding what the actual type of the message is. This allows us to use the same channel for multiple types of messages, while still being able to differentiate between them. From the documentation: "This attribute contains a value describing the type of event related to the originating occurrence. Often this attribute is used for routing, observability, policy enforcement, etc. SHOULD be prefixed with a reverse-DNS name. The prefixed domain dictates the organization which defines the semantics of this event type."

In this case then the metadata of the message decides how it is routed. Our existing message pump implementation does not support this as we configure the type of the channel via a generic parameter, yet in this case we do not know the type of the message in advance. We need to change the implementation of the message pump to support this.

## Decision

We need to remove the generic parameter from Reactor and Proactor and instead rely on runtime determination of the type. To do this we will use the [Strategy Pattern](https://en.wikipedia.org/wiki/Strategy_pattern) passing in a `Func<Message, Type>` to the message pump. This function will be used to determine the type of the `IRequest` at runtime based on the metadata of the message.  The `Subscription` will then set this, allowing the user to use strategies other than DataType Channel, for example, reading the Cloud Events type. We will make the `Func<Message, Type>` an otional parameter of the `Subscription` and default to returning the `DataType` of the `Subscriiption` if not set, which is the current behavior. This will allow us to keep the existing behavior for users that do not need to differentiate between message types.

## Consequences

There is likely to be an impact on performance as we move from a compile time decision, and one which optimizes by pre-building its meaage mapper middleware pipeline, to one which needs to calculate the type at runtime. This is a trade-off we are willing to make in order to support the use case of multiple message types on the same channel. However, we should look at options to optimize this in the future, such as caching the type of the message based on the metadata.

An easy first optimization would be to cache the unwrap pipeline for a type after creating it, so that when we receive the same type again, we can reuse the existing pipeline instead of creating a new one. This would reduce the overhead of creating the pipeline for each message and improve performance.

On a Subscription, we cam make the DataType optional as use the <see cref="Func{Message, Type}"/> to determine the type of the message at runtime. If you are not using the DataType Channel, you do not need this. However, we should assert that the DataType is not null if the strategy for mapping Message to Request is null, as it implies defaulting to the DataType Channel.
