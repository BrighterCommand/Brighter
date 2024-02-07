# 8. Default to payload type based routing, but offer context-based routing options as well

Date: 2019-08-01

## Status

Accepted

## Context

Brighter's current default routing mechanism is payload type-based. This means that the type of the message is used to
determine which handler to use. This is a common pattern in CQRS and event sourcing systems. However, there are times
when
context-based routing is more appropriate. For example, when a channel has multiple schemas, we may need to route based
on a header value so that it can be appropriately deserialized and then handled. Alternativel, we may need to route
to a handler based on the country.

The Spring Boot framework has a number of ways to route messages:

* Payload type-based routing
* Header-based routing
* Recipient List routing
* Dynamic recipient list routing
* Content (XPath)-based routing
* Routing Slip

For interop with systems that use Spring Boot, it would be useful to support more types that just Payload type-based
routing.

Note that in all these routing approaches, we assume that the payload of the message determines the type of the
handler. This is fundamentally how our CommandProcessor works. Our outcome is to vary the implementation of that
type. (This also relates to the idea of an agreement dispatcher, which chooses based on date to allow versioning).

We may also need to 'route' to a message mapper based on the context to support multiple schemas on a channel; this
is a related but slightly different problem. We assume that because we have a Datatype channel, so when we
deserialize we can predict the type of the message. This is not the case if there are multiple schemas on a channel.
Whilst we don't advocate for this approach, we do neet to support it for interop and to allow for this we need to enable
choosing a deserializer based on a header value.

## Decision

Our routing is handled by our Command Processor. Routing should be available both when using an internal bus and an
external bus, so it ought to be a feature of the Command Processor and not the ServiceActivator.

As a ServiceActivator uses one CommandProcessor configuration, this does mean that the ServiceActivator will only
support a single routing mechanism. This is not a problem as we expect that you will use a new ServiceActivator for
each different routing mechanism.

The only information that we pass into the CommandProcessor is the request, and we remove other information that we
might want to use to route, such as information in message headers. This is because the CommandProcessor knows
nothing about the message. We can correct for this by adding the information into the request context. Under this
approach all routing mechanisms that are not payload type-based will need to loop up a value in the context bag.

We could simply then have PayloadTypeBasedRouting and ContextBasedRouting. ContextBasedRouting would work against a
Context property and then ServiceActivator would populate the header properties into the Context bag (along with the
message body). This would allow us to route based on the context.

We need a keyed subscriber registry to allow you to select the handler from a range that implement the same interface. 
This allows us to find the concrete type of the implementation. We note that ServiceCollection now offers a key to 
disambiguate between services that implement the same interface. We only need to to use this if we are looking up by 
the interface type and not the concrete type. As our subscriber registry would give us the concrete type we can 
avoid depending on specific IoC container features.

It is a little more complex for transformations. We need to support "keyed" tansformation lookup, but we cannot 
assume the type of the message-to-type transformation, we will need to look up the associated handler and determine 
the type that we are transforming into at runtime.  

## Consequences

A first consequence is that we will become more explicit about how we route messages. This is a good thing as it 
will make it easier to understand how Brighter works. The second consequence is the outcome itself, we can support a 
context based routing approach to finding a matching handler.