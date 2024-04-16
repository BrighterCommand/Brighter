# 7. Aspect Oriented Programming

Date: 2019-08-01

## Status

Accepted

## Context

We want to ensure separation of concerns in the Brighter codebase, so that the logic of handling a command or event can 
be cleanly separated from cross-cutting concerns such as logging, exception handling, and retries.

We want to apply multiple cross-cutting concerns to the core business logic, and we want to be able to control the order 
in which they are applied.

We see two alternatives:

* The cross cutting concerns are configured when we configure the subscriptions.
* The cross cutting concerns are configured on the handler or mapper itself.

The former has the virtue of not-clouding the handler or mapper with code related concerns that are not directly related 
to its core purpose. However, it does mean that the configuration of the cross-cutting concerns is not visible in the 
handler or mapper code, and that the order of execution is not visible in the handler or mapper code. This results
in a loss of context when viewing the handler - you have to navigate back to the configuration to see the full picture.

With a pipeline there are two options:

* Orchestration: A chain of responsibility in which there is a linked list of handlers and the message flows through each step in the 
  chain with a manager controlling the flow.
* Choreography: A russian doll model in which each handler calls the next handler in the chain and can react to the 
* results of the next call.

The former is more complex, and requires a manager to control the flow. The latter is simpler, and allows each handler
to react to the results of the next call (including any exceptions).

## Decision

Brighter supports the separation of cross-cutting concerns, such as logging, exception handling, and retries from the
core business logic for dispatching or message serialization/deserialization. This is achieved through the use of
decorators, which wrap the core business logic in a pipeline of handlers.

For the handler pipeline, the decorators implement the same IAmARequestHandler interface as the core business logic. This
is because the flow passes a request in and out of the pipeline.

For the mapper pipeline, the decorators need to implement IAmAMessageTransformer, which is a different interface to the
core serializer/deserializer. This is because the flow differs for serialization and deserialization. We need to wrap
an outgoing message in a pipeline of serializers, but we need to wrap an incoming message in a pipeline of deserializers.

The order of execution is a Russian doll model which allows each handler to react to the results of the next call 
(including any exceptions). This is particularly useful for exception handling and retries; it allows us to use
Polly to encompass subsequent handler chain steps and provide a Fallback handler.

We use attributes on the handler or mapper to configure the cross-cutting concerns. This means that the configuration
context is visible in the handler or mapper code, and that the order of execution is visible in the handler or mapper code. 
We use .NET attributes because we believe that these are well-understood by .NET developers, due to their usage in 
ASP.NET MVC and Web API.

The use of attributes for cross-cutting concerns is a form of [Aspect Oriented Programming](https://en.wikipedia.org/wiki/Aspect-oriented_programming). 
For our <strong>Aspect</strong>: 
* We use deploy time weaving with the use of attributes as a <strong>Pointcut</strong>.
* We use IAmARequestHandler and IAmAMessageTransformer for the <strong>Advice</strong>.

## Consequences

We use Aspect Oriented Programming to configure cross-cutting concerns on handlers and mappers.