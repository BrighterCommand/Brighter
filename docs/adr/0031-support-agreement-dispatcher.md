# 31.  Support Agreement Dispatcher

Date: 2025-07-07

## Status

Draft

## Context

Brighter has support for dispatching message to a handler based on the message type. This is a common default, where a particular command or event has just one handler and we want to dispatch it to that handler. The main value of the dispatcher in this case, comes from the middleware which acts as a command processor. However another goal of a dispatcher is to allow us to choose the hander that we route to. This is not possible with the current dispatcher, as it only supports a single handler for a given message type.

The current dispatcher does not support the case where we want to route a message to a handler based on some other criteria, such as the content of the request or some external state in the context. This is a common requirement in many systems, and it is not currently supported by Brighter.

One scenario that is typical here is Martin Fowler's [Agreement Dispatcher](https://martinfowler.com/eaaDev/AgreementDispatcher.html) which is a pattern for routing messages to different handlers depending on time - as new rules come into force, we want to route to different destinations. 

## Decision

We will change SubscriberRegistry to support different routing strategies. We will change the implementation of the `SubscriberRegistry` to take a lambda function that takes a `IRequest` and `IRequestContext` and returns a `List<Type>` of matching handlers. This allows users to register a method to determine the type to return. We will provide support for `Type` based routing by simply returning the `List<Type>` that you register as the return value from this method, and allow you to register others. 

Because SubscriberRegistry supports many types, we can't use a generic parameter for this Lambda. This creates a limitation of this approach, in that the end user will have to cast the `IRequest` into an appropriate type to access it's state, to make routing decisions. This is a reasonable trade-off, but one worth documenting in our writeup, in case it is not obvious to users how to access the state of their command or event.

Then we need to allow you to register a lambda explicitly, over using the default—just return this type approach—so that you can utilize the request and context to determine what handler(s) to return. As you can return one or more, we usually append new registrations for the same type. 

Because implementations of the `SubscriberRegistry` for `SeviceCollection` are needed to support `IAmAHandlerFactory` impelementations derived from `ServiceCollection`, the `SubscriberRegistry` method `Register` will take as a parameter the possible `IAmARequestHandler` handler types, which can be used to add them to the `ServiceCollection`, along with their lifetime.

When we do auto-registration, we are likely to register handlers that you want to provide an explicit factory for instead.  Because we append new handlers to the chain of possible handlers, for publishing, you would never override the simple route type to handler in this case. So for auto-registration we will need to support an exclusion list: don't auto-register these handlers, because you want to explicitly register how the handler is determined.

## Consequences

Providing explicit routing makes another benefit of the Dispatcher model clear, beyond middleware, which is the ability to route to a handler based on content. As we build it into the dispatcher, this will also be available for messaging scenarios, and thus external events might have different routing strategies based on their content and the message itself (which is available in the context).