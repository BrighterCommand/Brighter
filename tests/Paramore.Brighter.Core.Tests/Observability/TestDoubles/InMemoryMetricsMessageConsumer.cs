#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

internal sealed class InMemoryMetricsMessageConsumer(
    IEnumerable<Message?> receiveResults,
    MessagingSystem messagingSystem) : IAmAMessageConsumerSync, IAmAMessageConsumerAsync, IHaveAMessagingSystem
{
    private readonly Queue<Message?> _receiveResults = new(receiveResults);

    public MessagingSystem MessagingSystem { get; } = messagingSystem;
    public List<Id> AcknowledgedMessages { get; } = [];

    public Message[] Receive(TimeSpan? timeOut = null)
    {
        var message = _receiveResults.Dequeue();
        if (message is null)
            throw new ChannelFailureException("Broker receive failed", new InvalidOperationException("Broker unavailable"));
        return [message];
    }

    public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Receive(timeOut));

    public void Acknowledge(Message message) => AcknowledgedMessages.Add(message.Id);

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        Acknowledge(message);
        return Task.CompletedTask;
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null) => true;

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Reject(message, reason));

    public void Nack(Message message) => _receiveResults.Enqueue(message);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        Nack(message);
        return Task.CompletedTask;
    }

    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        _receiveResults.Enqueue(message);
        return true;
    }

    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Requeue(message, delay));

    public void Purge() => _receiveResults.Clear();

    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        Purge();
        return Task.CompletedTask;
    }

    public void Dispose() => Purge();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
