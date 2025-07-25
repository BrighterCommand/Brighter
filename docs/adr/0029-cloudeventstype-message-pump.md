# 29.  Use CloudEventsType for Message Pump

Date: 2025-07-14

## Status

Accepted

## Context

We assume that the message pump listens to a [DataType Channel](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DatatypeChannel.html), which is a channel that can receive messages of a specific type. The message pump will then process these messages and send them to the appropriate destination.

What happens then if a producer decides to send multiple types of messages to the same channel? In this case, we need a way to differentiate between the different types of messages. We can use the [CloudEvents](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md) `type` field for deciding what the actual type of the message is. This allows us to use the same channel for multiple types of messages, while still being able to differentiate between them. From the documentation: "This attribute contains a value describing the type of event related to the originating occurrence. Often this attribute is used for routing, observability, policy enforcement, etc. SHOULD be prefixed with a reverse-DNS name. The prefixed domain dictates the organization which defines the semantics of this event type."

In this case then the metadata of the message decides how it is routed. Our exissting message pump implementation does not support this as we configure the type of the channel via a generic parameter, yet in this case we do not know the type of the message in advance. We need to change the implementation of the message pump to support this.

## Decision


## Consequences
