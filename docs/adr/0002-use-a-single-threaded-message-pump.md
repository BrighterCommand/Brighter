# 2. Use A Single Threaded Message Pump

Date: 2019-08-01

## Status

Accepted

## Context

Any service activator pattern will have a message pump, which reads from a queue. 

There are different strategies we could use, a common one for example is to use a BlockingCollection to hold messages read from the queue, and then use threads from the threadpool to process those messages.

However, a multi-threaded pump has the issue that it will de-order an otherwise ordered queue, as the threads will pull items from the blocking collection in parallel, not sequentially.

In addition, where we have multiple threads it becomes difficult to create resources used by the pump without protecting them from race conditions.

The alternative is to use a single-threaded message pump that reads from the queue, processes the message, and only when it has processed that message, processes the next item. This prevents de-ordering of the queue, because items are read in sequence.

If a higher throughput is desired with a single threaded pump, then you can create multiple pumps. In essence, this is the competing consumers pattern, each performer is its own message pump.

The message pump performs the usual sequence of actions:

 - GetMessage. Read Message From Queue
 - Translate Message. Translate Message from Wire Format to Type
 - Dispatch Message. Dispatch Message based on Type
 
 Prior art for this is the Windows Event Loop which uses this approach, and is used by COM for integration via the Single-Threaded Apartment model.


## Decision

Use a single-threaded message pump to preserve ordering and ensure sequential access to shared resources. Allow multiple pump instances for throughput.

## Consequences

This makes an async model harder, as it relies on a sequential processing strategy, which is the opposite of an async strategy which would allow a handler to yield when it was doing I/O, allowing another message to be consumed. Because this implicitly de-orders messages, this is not compatible with this approach.

it may imply that we should consider having a 'pluggable' pump that can use different strategies, asynchronous where you do not require ordering, and synchronous where you do.


