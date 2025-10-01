# 32. Remove Semaphore from Explicit Clear 

Date: 2025-08-28

## Status

Accepted

## Context

We want to avoid publishing a message twice from an outbox. This may happen because we fail to update the Outbox when a message is sent, and we cannot avoid that. However, we create the risk that we publish twice if we run two overlapping publish operations, at the same time.

For this reason `OutboxProducerMediator` has `SemaphoreSlim` 's_backgroundClearSemaphoreToken'. We attempt  to signal the semaphore, but use a TimeSpan.Zero wait, so that if another clear is running, we give up. For a background process, this is fine, as we assume another run of the background process to clear items from the Outbox will run soon, and pick up anything that the last run missed. Latency may increase a little for some messages, but we don't dual publish.

We can also have an explicit clear for a range of messages. There is a risk that if this runs during a background publish, it could clear messages that the background publish is already clearing. However, as the background clear only clears message that have been waiting for a configured period of time, it is likely that the explicit clear list will not be old enough to be in any background process that is running at the same time. So we consider the risk of a dual publish to be an acceptable one here.

Prior to the introduction of `IDistributedLock` we had no facility for running multiple sweepers for resilience, but only having one active. For this reason we added another `SemaphoreSlim` to `OutboxProducerMediator`. Instead of testing for the lock with a `WaitAsync(TimeSpan.Zero)` this lock blocked waiting for the lock to become free (even if async so a thread was not blocked). 

```csharp
internal async Task ClearOutboxAsync(
    IEnumerable<Guid> posts, 
    bool continueOnCapturedContext = false,
    CancellationToken cancellationToken = default)
{

    if (!HasAsyncOutbox())
        throw new InvalidOperationException("No async outbox defined.");

    await _clearSemaphoreToken.WaitAsync(cancellationToken);
    try
    {
        foreach (var messageId in posts)
        {
            var message = await AsyncOutbox.GetAsync(messageId, OutboxTimeout, cancellationToken);
            if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

            await DispatchAsync(new[] {message}, continueOnCapturedContext, cancellationToken);
        }
    }
    finally
    {
        _clearSemaphoreToken.Release();
    }

    CheckOutstandingMessages();
}
```

At scale, this proves problematic as you now have sequential `Clear` operations on the outbox, even though the range of messages to clear is not sequential. In pratice, this means that you will back up HTTP API requests that write to the Outbox, behind this semaphore. Once enough requests queue you up, you will end up with a Bad Gateway error.

## Decision

Drop the usage of `_clearSemaphoreToken` as `IDistributedLock` now protects us against dual publish by Sweepers. 

## Consequences

There is a low risk that we get a dual publish where a background clear runs over the same rage as an explicit clear, if the age of a row to clear was set too low.