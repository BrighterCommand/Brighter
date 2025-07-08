# 28.  Support Agreement Dispatcher

Date: 2025-07-07

## Status

Draft

## Context

Brighter has support for dispatching message to a handler based on the message type. This is a common default, where a particular command or event has just one handler and we want to dispatch it to that handler. The main value of the dispatcher in this case, comes from the middleware which acts as a command processor. However another goal of a dispatcher is to allow us to choose the hander that we route to. This is not possible with the current dispatcher, as it only supports a single handler for a given message type.

The current dispatcher does not support the case where we want to route a message to a handler based on some other criteria, such as the content of the request or some external state in the context. This is a common requirement in many systems, and it is not currently supported by Brighter.

One scenario that is typical here is Martin Fowler's [Agreement Dispatcher](https://martinfowler.com/eaaDev/AgreementDispatcher.html) which is a pattern for routing messages to different handlers depending on time - as new rules come into force, we want to route to different destinations. 

## Decision

We will change SubscriberRegistry to support different routing strategies. We will change the implementation of the `SubscriberRegistry` to take a lambda function that takes a `Request` and `RequestContext` and returns the `Type` of the handler. This allows users to register a method to determine the type to return. We will provide support for `Type` based routing by simply returning the `Type` that you register as the return value from this method, and allow you to register others. 

## Consequences

Providing explicit routing makes another benefit of the Dispatcher model clear, beyond middleware, which is the ability to route to a handler based on content. As we build it into the dispatcher, this will also be available for messaging scenarios, and thus external events might have different routing strategies based on their content and the message itself (which is available in the context).