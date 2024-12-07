# 2. Use A Single Threaded Message Pump

Date: 2019-08-01

## Status

Accepted

## Context

Any service activator pattern will have a message pump, which reads from a queue.

There are different strategies we could use, a common one for example is to use a BlockingCollection to hold messages read from the queue, and then use threads from the thread pool to process those messages. However, a multithreaded pump has the issue that it will de-order an otherwise ordered queue, as the threads will pull items from the blocking collection in parallel, not sequentially. In addition, where we have multiple threads it becomes difficult to create resources used by the pump without protecting them from race conditions.

The other option would be to use the thread pool to service requests, creating a thread for each incoming message. This would not scale, as we would quickly run out of threads in the pool. To avoid this issue, solutions that rely on the thread pool typically have to govern the number of thread pool threads that can be used for concurrent requests. The problem becomes that at scale the semaphore that governs the number of threads becomes a bottleneck.

The alternative to these multithreaded approaches is to use a single-threaded message pump that reads from the queue, processes the message, and only when it has processed that message, processes the next item. This prevents de-ordering of the queue, because items are read in sequence.

This approach is the [Reactor](https://en.wikipedia.org/wiki/Reactor_pattern) pattern. The [Reactor](http://reactors.io/tutorialdocs//reactors/why-reactors/index.html) pattern uses a single thread to read from the queue, and then dispatches the message to a handler. If a higher throughput is desired with a single threaded pump, then you can create multiple pumps. In essence, this is the competing consumers pattern, each performer is its own message pump. 

To give the Reactor pattern higher throughput, we can choose not to block on I/O by using asynchronous handlers. This is the [Proactor](https://en.wikipedia.org/wiki/Proactor_pattern) pattern. Brighter provides a SynchronizationContext so that asynchronous handlers can be used, and the message pump will not block on I/O, whilst still preserving ordering. Using an asynchronous handler switches you to a Proactor approach from Reactor for [performance](https://www.artima.com/articles/comparing-two-high-performance-io-design-patterns#part2).

Note, that the Reactor pattern may be more performant, because it does not require the overhead of the thread pool, and the context switching that occurs when using the thread pool. The Reactor pattern is also more predictable, as it does not rely on the thread pool, which can be unpredictable in terms of the number of threads available. The Proactor pattern however may offer greater throughput because it does not block on I/O.

The message pump performs the usual sequence of actions:

- GetMessage. Read Message From Queue
- Translate Message. Translate Message from Wire Format to Type
- Dispatch Message. Dispatch Message based on Type

 Prior art for this is the Windows Event Loop which uses this approach, and is used by COM for integration via the Single-Threaded Apartment model.

## Decision

Use a single-threaded message pump to preserve ordering and ensure sequential access to shared resources. Allow multiple pump instances for throughput. Allow asynchronous handlers to prevent blocking on I/O.

## Consequences

This makes an async model harder, as it relies on a sequential processing strategy which implies that we must use the message pump thread for callbacks. This is the opposite of an async strategy that uses the thread pool for callbacks, which would prevent queueing of callbacks at the cost of potentially running those callbacks out of order.

It may imply that we should consider having a 'pluggable' pump that can use different strategies, asynchronous where you do not require ordering, and synchronous where you do.
