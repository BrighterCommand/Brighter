# 33. Optimise reads and write from Dynamo outbox

Date: 2025-09-03

## Status

Proposed

## Context

Load testing of APIs using Brighter with a Dynamo DB outbox has shown a performance bottleneck in high throughput scenarios, resulting in excessive CPU usage, high response times, and some HTTP 503 responses to clients. When running load testing locally with the API hooked up to LocalStack, we see an excessive number of `Query` operations being performed on the Dynamo outbox table. This suggests that the issue is some form of resource exhaustion cause by excessive requests being made over the network.

There are several places where we make inefficient use of the Dynamo DB client.

### `OutstandingMessages` operation

The `Outstanding` GSI in the Dynamo outbox uses the topic name as a primary key, which is then sharded according to the number of shards provided in config by the user in order to avoid hot partitions. When the outbox is queried to fetch all outstanding messages, it performs a large number of queries iterating over each topic it knows about, and each shard for each of those partitions. If the outbox was being used as part of publishing to five topics, each with 20 shards in the outbox table, that would mean performing 100 query operations even if the outstanding index was completely empty.

### `DispatchedMessages` operation & the `Delivered` GSI

Similar to the operation above. The `Delivered` index is partitioned by topic name, but unlike the `Outstanding` index it is _not_ sharded. This means that when the `DispatchedMessage` operation is performed, it only has to iterate over the topics it knows about (so following the scenario above, five queries even if the delivered index is empty).

As the `Delivered` index isn't sharded, it can fall victim to hot partitions. When writes to a GSI are throttled, [this also throttles writes to the base table that would affect the GSI](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/gsi-throttling.html). Since the `Delivered` index is a sparse index that isn't written to if the `DeliveryTime` of a message is null, this wouldn't affect the initial writing of messages to the outbox. It _would_, however, throttle the ability to mark messages as delivered, and lead to duplicate publishes as messages become "stuck" in the outbox.

### Batch get and write operations

There are a few operations where the outbox is provided with a collection of messages or message IDs, and instead of performing batch operations with those IDs it iterates through them and performs individual operations sequentially:

* When clearing a collection of message IDs from the outbox, it fetches each of these individually and tries to dispatch it before moving onto the next
* When marking a collection of messages as dispatched, the messages are written to sequentially with seperate requests
* When a collection of messages are deleted from the outbox they're worked through sequentially with separate requests

### Fetching outstanding message count

Every time one or more messages are cleared from the outbox, the `OutboxProducerMediator` checks whether it needs to update it's internal metric for how many outstanding messages currently sit in the outbox, based on when that last check was last run. If it determines a refresh is neccesary, it fetches all outstanding messages from the outbox into memory (but does so asynchronously). This comes with all the queries described above, and if there are a large number of messages outstanding, can eat up a problematic amount of memory. The count of outstanding messages only appears to be used for monitoring purposes.

## Decision

All of the inefficiencies above can be improved with non-breaking changes.

### `OutstandingMessages` operation

Given that the `Outstanding` index is a sparse index, and we wish to pull out the entirety of that index when we perform the operation, this can be a `Scan` operation on the index instead of a `Query`. This removes the need to iterate over topics and shards, and can instead be done as a single HTTP call if the number of outstanding messages allows it (with paging if it doesn't).

The one downside of this is that we cannot specify the ordering of results from a `Scan` operation. If the results are paged, we will not be able to specify that the oldest messages should be retrieved first. Given the performance issues using `Query` operations, and the limitations of Dynamo DB as a storage platform, this feels like a reasonable comprompise to make.

If a specific topic is provided in the `args` dictionary when performing the `OutstandingMessages` operation, then a `Query` would still need to be used, iterating over the shards for that topic.

### `DispatchedMessages` operation

As above - if no topic is provided in the `args` dictionary, use a `Scan` operation to fetch dispatched messages instead of a `Query` operation.

### The `Delivered` GSI

Introduce sharding to the `Delivered` index, using the same number of shards as configured for the `Outstanding` index.

### Getting messages for dispatch

Add overloads of both `Get` and `GetAsync` to the outbox interfaces that take a collection of message IDs instead of just one. For the Dynamo DB implementation, use a `BatchGet` operation to fetch all of them at once. For the other implementations, they can just iterate over their other `Get` methods for now.

If the `BatchGet` operation only returns a subset of the requested messages, throw an exception.

Update `OutboxProducerMediator` to use the new `Get` methods.

### Marking messages as dispatched

When marking a collection of messages as dispatched, us a `BatchWrite` operation to update all of them at once. If any of the updates fail, throw an exception.

Add a new overload of `MarkDispatched` which takes a collection of message IDs (currently the only bulk option is the async version).

### Deleting messages from the outbox

When deleting a collection of messages from the outbox, do so using a `BatchWrite` operation. If any of the deletes fail, throw an exception.

### Outstanding item count

It feels useful to have the number of outstanding messages available in a metric. Dynamo DB doesn't have a `Count` operation, but we can get this information without having to pull all messages into memory:

1. When messages are added to the Dynamo DB outbox, add a new binary attribute containing a single bit of data.
2. Add new methods to the outbox interfaces for `GetOutstandingMessageCount` and `GetOutstandingMessageCountAsync`.
3. In the Dynamo DB implementation, perform a `Scan` operation on the `Outstanding` index, with configuration to only retrieve the new binary attribute.
4. Page through results as required, and sum the total number of records returned.

This minimises the amount of data sent over the wire, minimises memory consumption, and maximises the number of records returned in each page.

Other outbox implementations can continue to use their implementations of `OutstandingMessages` for now.

## Consequences

* We will no longer be able to sort results of the `OutstandingMessages` or `DispatchedMessages` operations (when performed for all topics) to ensure the retrieval of oldest messages first from a Dynamo DB outbox, but will support high throughput scenarios instead. The results can still be sorted if queried on a topic by topic basis.
* The possibility of future improvements to other outbox implementations, to take advantage of the new bulk operation methods