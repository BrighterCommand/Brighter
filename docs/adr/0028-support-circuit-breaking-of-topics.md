# ADR: Support Circuit Breaking for `Topics` in Outbox Sweeper

Date: 2025-07-04

## Status

Proposed

---

## Context

The outbox sweeper currently attempts to dispatch the top X messages every Y seconds. If the top X messages are all destined for a failing topic (e.g., due to misconfiguration or service issues), the sweeper repeatedly retries the same messages, blocking dispatch of other valid messages.

### Problem

This behavior causes the outbox to stall, especially when a topic is misconfigured or unavailable.  
For example, if TopicA has 200 failing messages and TopicB has 100 valid ones, and the sweeper processes 100 messages per tick, TopicB's messages are never reached.

### Impact

- Dispatching stalls for all topics.
- Azure Service Bus topics can fill up (e.g., 1GB limit reached).
- Operational overhead increases due to repeated failures and lack of progress.

---

## Decision

### Summary

Implement a circuit breaker mechanism for topics that fail dispatch with an In-Memory collection of tripped topics

### Details

- Track dispatch failures per topic.
- If a topic exceeds a configurable failure threshold within a time window, mark it as "tripped".
- Skip dispatch attempts for tripped topics for a configurable cooldown period.
- After cooldown, allow retry and reset failure count on success.
- Ensure metrics and logs are available for observability.

---

## Implementation Options

### Option 1: Persistence-Backed Tripped Topics

- Store tripped topic state in a persistent store (e.g., Redis, SQL, or distributed cache).
- Ensures state is retained across service restarts or deployments.
- Suitable for distributed or scaled-out deployments.
- Adds some latency and complexity due to external I/O.

### Option 2: In-Memory Tripped Topics

- Maintain tripped topic state in memory within the sweeper process.
- Fast and simple to implement.
- State is lost on restart; not suitable for multi-instance deployments without coordination.
- Best for single-node or stateless environments where persistence is not critical.

#### Proposed In Memory Implementation

A circuit breaker registered as a singleton implementing IAmCircuitBreaker containing an in memory collection of tripped topics implementing IAmCircuitBreaker

```csharp
public interface IAmACircuitBreaker
{
    public void OnTick();
    public void TripTopic(string topic);
    public ReadOnlyCollection<string> TrippedTopics { get; }
}
```

Both the `OutboxProducerMediator` and the `RelationDatabaseOutbox` have the circuit breaker injected in by constructor. This allows backwards compatibility with non relational stores and support for them to be added at a later date

On each sweep the OnTick method will be called to decrement the `CoolDownCount` allowing for a retry to that topic when it gets to zero.

Extend `CreatePagedOutstandingCommand` to accept an array of tripped topics resultant sql might look something like the below

```sql
SELECT *
FROM (
        SELECT ROW_NUMBER() OVER(
                ORDER BY [Timestamp] DESC
            ) AS NUMBER,
            *
        FROM { 0 }
    ) AS TBL
WHERE [Dispatched] IS NOT NULL
    AND [Dispatched] < @DispatchedSince
    AND NUMBER BETWEEN ((@PageNumber -1) * @PageSize + 1) AND (@PageNumber * @PageSize)
    AND [Topic] NOT IN (@TrippedTopics)
ORDER BY [Timestamp] DESC

```

On failure to publish a message the topic will be tripped and the `CoolDownCount` set to what has been configured for that topic.

#### Bulk dispatch implementation

Currently only `AzureServiceBusMessageProducer` and `InMemoryProduce` implement `IAmABulkMessageProducerAsync`. 

The current behaviour batches messages per topic and batch size then publishes the batch. If one batch fails an exception is raised and successful batches are not recorded. This therefore renders the `MarkDispatchedAsync` on successful messages useless.

Proposal is to bring the Bulk dispatch inline with the single dispatch implementation so that it can be wrapped in a retry and make use of `IAmAnOutboxCircuitBreaker`. A further benefit is individual batches can be retried as opposed to the entire list of messages and better control over implementation specific batching, asb can be quite funny.

Changes to IAmABulkMessageProducerAsync interface as follows. 

```c#
    public interface IAmABulkMessageProducerAsync : IAmAMessageProducer
    {
        /// <summary>
        /// Creates batches from messages
        /// </summary>
        /// <param name="messages">The messages to batch</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        IEnumerable<MessageBatch> CreateBatch(IEnumerable<Message> messages, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a batch of messages.
        /// </summary>
        /// <param name="batch">A batch of messages to send</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        Task SendAsync(MessageBatch batch, CancellationToken cancellationToken);
    }

```

---

## Consequences

### Positive

- Prevents repeated failures from blocking the queue.
- Allows healthy topics to continue dispatching.
- Improves system resilience and throughput.

### Negative

- Slight increase in complexity and state management.
- Risk of skipping recoverable topics if thresholds are too aggressive.
- Trade-offs between persistence and performance depending on implementation choice.

---

## Alternatives Considered

- Increase batch size or randomize message selection (less targeted, may still retry failing topics).
- Manual intervention to remove or reconfigure failing messages (not scalable).

---

## Notes

- This ADR is inspired by real-world issues encountered with Azure Service Bus and permission misconfigurations.
- Implementation should be configurable and observable.
