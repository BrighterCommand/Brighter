# 22. Reactor and Nonblocking IO, Proactor and Blocking IO

Date: 2019-08-01

## Status

Accepted

## Context

As [outlined](0002-use-a-single-threaded-message-pump.md), Brighter offers two concurrency models, a Reactor model and a Proactor model. 

The Reactor model uses a single-threaded message pump to read from the queue, and then dispatches the message to a handler. If a higher throughput is desired with a single threaded pump, then you can create multiple pumps (Peformers in Brighter's taxonomy). For a queue this is the competing consumers pattern, each Performer is its own message pump and another consumer; for a stream this is the partition worker pattern, each Performer is a single thread reading from one of your stream's partitions. 

The Proactor model uses the same single-threaded message pump, or Performer, but uses non-blocking I/O. As the message pump waits for the non-blocking I/O to complete, it will not process additional messages whilst waiting for the I/O to complete. It will yield to other Peformers, which will process messages whilst the I/O is waiting to complete. But the main benefit here is lower CPU usage, as the Performer shares resources better with others on the same host. 

The trade-off here is the Reactor model offers better performance, as it does not require the overhead of the thread pool, and the context switching that occurs when using the thread pool. The Reactor model is also more predictable, as it does not rely on the thread pool, which can be unpredictable in terms of the number of threads available. 

But this trade-off assumes that Brighter can implement the Reactor and Proactor preference for blocking I/O vs non-blocking I/O top-to-bottom. What happens for the Proactor model the underlying SDK does not support non-blocking I/O or the Proactor model if the underlying SDK does not support non-blocking I/O.

## Decision

If the underlying SDK does not support non-blocking I/O, then the Proactor model is forced to use blocking I/O. If the underlying SDK does not support blocking I/O, then the Reactor model is forced to use non-blocking I/O.

We support both the Reactor and Proactor models across all of our transports. We do this to avoid forcing a concurrency model onto users of Brighter. As we cannot know your context, we do not want to make decisions for you: the performace of blocking i/o or the throughput of non-blocking I/O.

To provide a common programming model, within our setup code our API uses blocking I/O. Where the underlying SDK only supports non-blocking I/O, we use non-blocking I/O and then use GetAwaiter().GetResult() to block on that. We prefer GetAwaiter().GetResult() to .Wait() as it will rework the stack trace to take all the asynchronous context into account.

Although this uses an extra thread, the impact for an application starting up on the thread pool is minimal. We will not starve the thread pool and deadlock during start-up.

For the Performer, within the message pump, we use non-blocking I/O if the transport supports it. 

Brighter has a custom SynchronizationContext, BrighterSynchronizationContext, that forces continuations to run on the message pump thread. This prevents non-blocking i/o waiting on the thread pool, with potential deadlocks. This synchronization context is used within the Performer for both the non-blocking i/o of the message pump and the non-blocking i/o in the transformer pipeline. Because our performer's only thread processes a single message at a time, there is no danger of this synchronization context deadlocking.

## Consequences

Because setup is only run at application startup, the performance impact of blocking on non-blocking i/o is minimal, using .GetAwaiter().GetResult() normally an additional thread from the pool.

For the Reactor model there is a cost to using non-blocking I/O, that is an additional thread will be needed to run the continuation. This is because the message pump thread is blocked on I/O, and cannot run the continuation. As our message pump is single-threaded, this will be the maximum number of threads required though for the Reactor model. With the message pump thread suspended, awaiting, during non-blocking I/O, there will be no additional messages processed, until after the I/O completes.

This is not a significant issue but if you use an SDK that does not support blocking I/O natively (Azure Service Bus, SNS/SQS, RabbitMQ), then you need to be aware of the additional cost for those SDKs (an additional thread pool thread). You may be better off explicity using the Proactor model with these transports, unless your own application cannot support that concurrency model.

Brighter offers you explicit control, through the number of Performers you run, over how many threads are required, instead of implicit scaling through the pool. This has significant advantages for messaging consumers, as it allows you to maintain ordering, such as when consuming a stream instead of a queue.

For the Proactor model this is less cost in using a transport that only supports blocking I/O.  
