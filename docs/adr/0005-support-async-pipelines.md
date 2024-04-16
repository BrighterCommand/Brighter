# 5. Support Async Pipelines 

Date: 2019-08-01

## Status

Accepted

## Context

Give we have decided to use a reactor pattern (see [Single Threaded Message Pump](0002-use-a-single-threaded-message-pump.md), 
we need to decide how to support asynchronous pipelines. There are three requirements:

* We need to be able to support asynchronous handlers
* We need to be able to support asynchronous message mappers
* We need to provide our own synchronization context so that the thread on which callbacks are invoked is the message pump thread.

Why do handlers need to be asynchronous? Because they may need to perform I/O, such as talking to a database or a web service, 
and we do not want to block the message pump thread.

Why do message mappers need to be asynchronous? Because they may need to perform I/O, such as talking to a schema registry or 
using a Claim Check transform and we do not want to block the message pump thread.

Why do we need to provide our own synchronization context? Because we want to ensure that the thread on which callbacks are invoked
is the message pump thread. This is important because we want to ensure that the message pump thread is not blocked by I/O.

## Decision

### Synchronization Context
Implement a custom synchronization context that will invoke callbacks on the message pump thread. The synchronization context
is based on the [Stephen Toub Single Threaded Synchronization Context](https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/),
and will run callbacks in order on the message pump thread.

### Asynchronous Handlers
Asynchronous handlers implement IHandleRequestsAsync<T> and return a Task. The message pump will await the task. Any callback after
the code returns from the handler will be invoked on the thread that invoked the handler.

#### Homogenous Handler Pipelines

A handler pipeline needs to be all asynchronous or all synchronous. If a handler pipeline contains both synchronous and asynchronous
then we would be forced to either block asynchronous handlers on a synchronous pipeline, or invoke synchronous handlers on a thread
other than the message pump thread.

### Asynchronous Message Mappers
Asynchronous message mappers implement IAmAMessageMapperAsync<T> and return a Task. The message pump will await the task. Any callback after
the code returns from the handler will be invoked on the thread that invoked the handler.

A message mapper pipeline terminates with an IAmAMessageMapper<T> or an IAmAMessageMapperAsync<T>. A pipeline may have transforms that
implement IAmAMessageTransform<T> or IAmAMessageTransformAsync<T>. (We do not use the message mapper interface for the pipeline, 
by contrast to handlers, because whereas each handler step attempts to handle the message a transform is either wrapping a message built
by a message mapper or unwrapping a message to be passed to a message mapper.) 

#### Homogenous Message Mapper Pipelines

A message mapper pipeline needs to be all asynchronous or all synchronous. If a message mapper pipeline contains both synchronous and asynchronous
then we would be forced to either block asynchronous message mappers on a synchronous pipeline, or invoke synchronous message mappers on a thread
other than the message pump thread.

### Posting a Message

When posting a message we assume that the async command processor Post methods will be for asynchronous message mappers and we search
for registered asynchonous message mappers before searching for synchronous message mappers. If the message mapper is synchronous then
we wrap it in a TaskCompletionSource and return the Task.

When posting a message we assume that the sync command processor Post methods will be for synchronous message mappers and we search
for registered synchronous message mappers before searching for asynchronous message mappers. If the message mapper is asynchronous then
we block on the Task.

### Receiving a Message

When receiving a message we assume that you will use an async message pump for an asynchronous message mapper and we search for registered
asynchronous message mappers before searching for synchronous message mappers. If the message mapper is synchronous then we wrap it in a TaskCompletionSource
and return the Task. We then use the synchronization context to ensure that the callback is invoked on the message pump thread.

When receiving a message we assume that you will use a sync message pump for a synchronous message mapper and we search for registered synchronous 
message mappers before searching for asynchronous message mappers. If the message mapper is asynchronous then we block on the Task.

## Consequences

* We can support asynchronous handlers
* We can support asynchronous message mappers
* We provide our own synchronization context so that the thread on which callbacks are invoked is the message pump thread.

