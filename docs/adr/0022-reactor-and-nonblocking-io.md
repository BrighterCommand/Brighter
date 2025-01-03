# 22. Reactor and Nonblocking IO, Proactor and Blocking IO

Date: 2019-08-01

## Status

Accepted

## Context

### Reactor and Proactor

As [outlined](0002-use-a-single-threaded-message-pump.md), Brighter offers two concurrency models, a Reactor model and a Proactor model. 

The Reactor model uses a single-threaded message pump to read from the queue, and then dispatches the message to a handler. If a higher throughput is desired with a single threaded pump, then you can create multiple pumps (Peformers in Brighter's taxonomy). For a queue this is the competing consumers pattern, each Performer is its own message pump and another consumer; for a stream this is the partition worker pattern, each Performer is a single thread reading from one of your stream's partitions. 

The Proactor model uses the same single-threaded message pump, or Performer, but uses non-blocking I/O. As the message pump waits for the non-blocking I/O to complete, it will not process additional messages whilst waiting for the I/O to complete; instead it will yield to other Peformers, which will process messages whilst the I/O is waiting to complete. 

The benefit of the Proactor approach is throughput, as the Performer shares resources better. If you run multiple performers, each in their own thread, such as competing consumers of a queue, or consumers of individual partitions of a stream, the Proactor model ensures that when one is awaiting I/O, the others can continue to process messages.

The trade-off here is the Reactor model can offer better performance, as it does not require the overhead of waiting for I/O completion. 

For the Reactor model there is a cost to using transports and stores that do non-blocking I/O, that is an additional thread will be needed to run the continuation. This is because the message pump thread is blocked on I/O, and cannot run the continuation. As our message pump is single-threaded, this will be the maximum number of threads required though for the Reactor model. With the message pump thread suspended, awaiting, during non-blocking I/O, there will be no additional messages processed, until after the I/O completes.

This is not a significant issue but if you use an SDK that does not support blocking I/O natively (Azure Service Bus, SNS/SQS, RabbitMQ), then you need to be aware of the additional cost for those SDKs (an additional thread pool thread). You may be better off explicity using the Proactor model with these transports, unless your own application cannot support that concurrency model.

For the Proactor model there is a cost to using blocking I/O, as the Performer cannot yield to other Performers whilst waiting for I/O to complete. This means the Proactor has both the throughput issue of the Reactor, but does not gain the performance benefit of the Reactor, as it is forced to use an additional thread to provide sync over async. 

In versions before V10, the Proactor message pump already supports user code in transformers and handlers running asynchronously (including the Outbox and Inbox), so we can take advantage of non-blocking I/O in the Proactor model. However, prior to V10 it did not take advantage of asynchronous code in a transport SDK, where the SDK supported non-blocking I/O. Brighter treated all transports as having blocking I/O, and so we blocked on the non-blocking I/O.

### Thread Pool vs. Long Running Threads

A web server, receiving HTTP requests can schedule an incoming request to a pool of threads, and threads can be returned to the pool to service other requests whilst I/O is occuring. This allows it to make the most efficient usage of resources because non-blocking I/O returns threads to the pool to service new requests. 

If I want to maintain ordering, I need to use a single-threaded message pump. Nothing else guarantees that I will read and process those messages in sequence. This is particularly important if I am doing stream processing, as I need to maintain the order of messages in the stream.

A consumer of a stream has a constrained choice if it needs to maintain its sequence. In that case, only one thread can consume the stream at a time. When a consumer is processing a record, we block consuming other records on the same stream so that we process them sequentially. To scale, we partition the stream, and allow up to as many threads as we have partitions. Kafka is a good example of this, where the consumer can choose to read from multiple partitions, but within a partition, it can only use a single thread to process the stream.

When consuming messages from a queue, where we do not care about ordering, we can use the competing consumers pattern, where each consumer is a single-threaded message pump. However, we do want to be able to throttle the rate at which we read from the queue, in order to be able to apply backpressure, and slow the rate of consumption. So again, we only tend to use a limited number of threads, and we can find value in being able to explicitly choose that value. 

For our Performer, which runs the message pump, we choose to have threads are long-running, and not thread pool threads. The danger of a thread pool thread, for long-running work, is that work could become stuck in a message pump thread's local queue, and not be processed. As we do not use the thread pool for our Performers and those threads are never returned to the pool. So the only thread pool threads we use are for non-blocking I/O. 

For us then, non-blocking I/O in either user code, a handler or tranfomer, or transport code, retrieving or acknowledging work, that is called by the message pump thread performs I/O, mainly has the benefit that we can yield to another Performer.

### Synchronization Context

Brighter has a custom SynchronizationContext, BrighterSynchronizationContext, that forces continuations to run on the message pump thread. This prevents non-blocking I/O waiting on the thread pool, with potential deadlocks. This synchronization context is used within the Performer for both the non-blocking I/O of the message pump. Because our performer's only thread processes a single message at a time, there is no danger of this synchronization context deadlocking.

However, if someone uses .ConfigureAwait(false) on their call, which is advice for library code, then the continuation will run on a thread pool thread. Now, this won't tend to exhaust the pool, as we only process a single message at a time, and any given task is unlikely to require enough additional threads to exhaust the pool. But it does mean that we have not control over the order in which continuations run. This is a problem for any stream scenario where it is important to process work in sequence.

## Decision

### Reactor and Proactor

We have chosen to support both the Reactor and Proactor models across all of our transports. We do this to avoid forcing a concurrency model onto users of Brighter. As we cannot know your context, we do not want to make decisions for you: the performace of blocking i/o or the throughput of non-blocking I/O.

To make the two models more explicit, within the code, we have decided to rename the derived message pump classes to Proactor and Reactor, from Blocking and NonBlocking.

In addition, within a Subscription, rather than the slightly confusing runAsync flag, we have decided to use the more explicit MessagePumpType flag. This makes it clear whether the Reactor or Proactor model is being used, and that non-blocking I/O or blocking I/O should be used.

Within the Subscription for a specific transport, we set the default to the type that the transport natively supports, Proactor if it supports both.

| Transport | Supports Reactor Natively | Supports Proactor Natively |
| ------------- | ------------- |-------------| 
| Azure Service Bus | Sync over Async  | Native |
| AWS (SNS/SQS)| Sync over Async  | Native |
| Kafka| Native  | Async over Sync (either thread pool thread or exploiting no wait calls) |
| MQTT | Sync over Async/Event Based  | Event Based |
| MSSQL | Native | Native |
| Rabbit MQ (AMQP 0-9-1) | After V6, Sync over Async  | Native from V7|
| Redis | Native | Native |

### In Setup accept Blocking I/O

Within our setup code our API can safely perovide a common abstraction using blocking I/O. Where the underlying SDK only supports non-blocking I/O, we should author an asynchronous version of the method and then use our SynchronizationContext to ensure that the continuation runs on the message pump thread. This class BrighterSynchronizationContext helper will be modelled on [AsyncEx's AsyncContext](https://github.com/StephenCleary/AsyncEx/blob/master/doc/AsyncContext.md) with its Run method, which ensures that the continuation runs on the message pump thread.
                                    
### Use Blocking I/O  in Reactor

For the Performer, within the Proactor message pump, we want to use blocking I/O if the transport supports it. This should be the most performant way to use the transport, although it comes at the cost of not yielding to other Performers whilst waiting for I/O to complete. This has less impact when running in a container in production environments.

If the underlying SDK does not support blocking I/O, then the Reactor model is forced to use non-blocking I/O. In Reactor code paths we should avoid blocking constructs such as ```Wait()```, ```.Result```, ```.GetAwaiter().GetResult()``` and so on, for wrapping anywhere we need to be sync-over-async, because there is no sync path. So how do we do sync-over-async? By using ```BrighterSynchronizationHelper.Run``` we can run an async method, without bubbling up await (see below). This helps us to avoid deadlocks from thread pool exhaustion, caused by no threads being available for the continuation - instead we just queue the continuations onto our single-threaded Performer. Whilst the latter isn't strictly necessary, as we only process a single message at a time, it does help us to avoid deadlocks, and to ensure that we process work in the order it was received.

### Use Non-Blocking I/O in Proactor

For the Performer, within the Proactor message pump, we want to use non-blocking I/O if the transport supports it. This will allow us to yield to other Performers whilst waiting for I/O to complete. This allows us to share resources better.

In the Proactor code paths production code we should avoid blocking constructs such as ```Wait()```, ```.Result```, ```.GetAwaiter().GetResult()``` and so on. These will prevent us from yielding to other threads. Our Performers don't run on Thread Pool threads, so the issue here is not thread pool exhaustion and resulting deadlocks. However, the fact that we will not be able to yield to another Performer (or other work on the same machine). This is an inefficient usage of resources; if someone has chosen to use the Proactor model to share resources better. In a sense, it's violating the promise that our Proactor makes. So we should avoid these calls in production code that would be exercised by the Proactor.

If the underlying SDK does not support non-blocking I/O then we will need to use ```Thread.Run(() => //...async method)```. Although this uses an extra thread, the impact is minimal as we only process a single message at a time. It is unlikely we will hit starvation of the thread pool.

### Improve the Single-Threaded Synchronization Context with a Task Scheduler

Our custom SynchronizationContext, BrighterSynchronizationContext, can ensure that continuations run on the message pump thread, and not on the thread pool. 

in V9, we have only use the synchronization context for user code: the transformer and handler calls. From V10 we want to extend the support to calls to the transport, whist we are waiting for I/O.

At this point we have chosen to adopt Stephen Cleary's [AsyncEx's AsyncContext](https://github.com/StephenCleary/AsyncEx/blob/master/doc/AsyncContext.md) project over further developing our own. However, AsyncEx is not strong named, making it difficult to use directly. In addition, we want to modify it. So we will create our own internal fork of AsyncEx - it is MIT licensed so we can do this - and then add any bug fixes we need for our context to that. This class BrighterSynchronizationContext helper will be modelled on [AsyncEx's AsyncContext](https://github.com/StephenCleary/AsyncEx/blob/master/doc/AsyncContext.md) with its Run method, which ensures that the continuation runs on the message pump thread. 

This allows us to simplify running the Proactor message pump, and to take advantage of non-blocking I/O where possible. In particular, we can write an async EventLoop method, that means the Proactor can take advantage of non-blocking I/O in the transport SDKs, transformers and user defined handlers where they support it. Then in our Proactors's Run method we just wrap the call to EventLoop in ```BrighterSunchronizationContext.Run```, to terminate the async path, bubble up exceptions etc. This allows a single path for both ```Performer.Run``` and ```Consumer.Open``` regardless of whether they are working with a Proactor or Reactor.

This allows to simplify working with sync-over-async for the Reactor. We can just author an async method and then use ```BrigherSynchronizationContext.Run``` to run it. This will ensure that the continuation runs on the message pump thread, and that we do not deadlock.

 However, the implication of Stephen Toub's article [here](https://devblogs.microsoft.com/dotnet/configureawait-faq/)  that there is no route around ConfigureAwait(false), so it looks we will have to document the risks of using ConfigureAwait(false) in our code (out of order handling). See Consequences, for more on this. 

### Extending Transport Support for Async

We will need to make changes to the Proactor to use async code within the pump, and to use the non-blocking I/O where possible. Currently, Brighter only supports an IAmAMessageConsumer interface and does not support an IAmAMessageConsumerAsync interface. This means that within a Proactor, we cannot take advantage of the non-blocking I/O; we are forced to block on the non-blocking I/O. We will address this by adding an IAmAMessageConsumerAsync interface, which will allow a Proactor to take advantage of non-blocking I/O. To do this we need to add an async version of the IAmAChannel interface, IAmAChannelAsync. This also means that we need to implement a ChannelAsync which derives from that.

As a result we will have a different Channel for the Proactor and Reactor models. As a result we need to extend the ChannelFactory to create both Channel and ChannelAsync, reflecting the needs of the chosen pipeline. However, there is underlying commonality that we can factor into a base class. This helps the Performer. It only needs to be able to Stop the Channel. By extracting that into a common interface we can avoid having to duplicate the Peformer code.

### Tests for Proactor and Reactor, Async Transport Paths

As we will have additional interfaces, we will need to duplicate some tests, to exercise those interfaces. In addition, we will need to ensure that the coverage of Proactor and Reactor is complete, and that we have tests for the async paths in the Proactor.

## Consequences

### Reactor and Proactor

Brighter offers you explicit control, through the number of Performers you run, over how many threads are required, instead of implicit scaling through the pool. This has significant advantages for messaging consumers, as it allows you to maintain ordering, such as when consuming a stream instead of a queue.

### Synchronization Context

The BrighterSynchronizationContext will lead to some complicated debugging issues where we interact with the async/await pattern. This code is not easy, and errors may manifest in new ways when they propogate through the context. We could decide that as we cannot control ConfigureAwait, we should just choose to queue on the threadpool and use the default synchronizationcontext and task scheduler. 

Another alternative would be to use the [Visual Studio Synchronization Context](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.threading.singlethreadedsynchronizationcontext?view=visualstudiosdk-2022) instead of modelling from Stephen Cleary's AsyncEx.  
