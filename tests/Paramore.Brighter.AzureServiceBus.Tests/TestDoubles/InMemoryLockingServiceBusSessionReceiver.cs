#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public class InMemoryLockingServiceBusSessionReceiver(InMemoryLockingServiceBusReceiver receiver, TimeSpan lockDuration)
    : ServiceBusSessionReceiver
{
    private long _lockedUntilTicks = (DateTimeOffset.UtcNow + lockDuration).UtcTicks;
    private int _renewals;

    public override string SessionId { get; } = Guid.NewGuid().ToString();
    public override string EntityPath => receiver.EntityPath;

    public int RenewalCount => Volatile.Read(ref _renewals);
    public override DateTimeOffset SessionLockedUntil => new(Interlocked.Read(ref _lockedUntilTicks), TimeSpan.Zero);
    public override bool IsClosed => receiver.IsClosed;

    public override async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
        int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
    {
        var messages = await receiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken);
        receiver.RenewSessionLocks(SessionLockedUntil);
        return messages;
    }

    public override async Task RenewSessionLockAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        if (SessionLockedUntil <= DateTimeOffset.UtcNow || IsClosed)
            throw new ServiceBusException("Session lock lost.", ServiceBusFailureReason.SessionLockLost);

        var lockedUntil = DateTimeOffset.UtcNow + lockDuration;
        receiver.RenewSessionLocks(lockedUntil);
        Interlocked.Exchange(ref _lockedUntilTicks, lockedUntil.UtcTicks);
        Interlocked.Increment(ref _renewals);
    }

    public override Task RenewMessageLockAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Session messages require session lock renewal.");

    public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        => receiver.CompleteMessageAsync(message, cancellationToken);

    public override Task AbandonMessageAsync(ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        => receiver.AbandonMessageAsync(message, propertiesToModify, cancellationToken);

    public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message,
        string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default)
        => receiver.DeadLetterMessageAsync(message, deadLetterReason, deadLetterErrorDescription, cancellationToken);

    public override Task CloseAsync(CancellationToken cancellationToken = default) => receiver.CloseAsync(cancellationToken);
}
