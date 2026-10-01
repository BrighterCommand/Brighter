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

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class InMemoryFailingRequeueConsumer(IEnumerable<Message> messages, bool failNack)
    : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private readonly Queue<Message> _messages = new(messages);

    public Exception RequeueFailure { get; } = new InvalidOperationException("Broker unavailable during requeue");
    public Exception NackFailure { get; } = new InvalidOperationException("Broker unavailable during negative acknowledgment");
    public List<Id> RequeueAttempts { get; } = [];
    public List<Id> Acknowledged { get; } = [];
    public List<Id> Rejected { get; } = [];
    public List<Id> NegativelyAcknowledged { get; } = [];

    public Message[] Receive(TimeSpan? timeOut = null) => [_messages.Dequeue()];

    public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Receive(timeOut));

    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        RequeueAttempts.Add(message.Id);
        throw RequeueFailure;
    }

    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Requeue(message, delay);
    }

    public void Acknowledge(Message message) => Acknowledged.Add(message.Id);

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        Acknowledge(message);
        return Task.CompletedTask;
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        Rejected.Add(message.Id);
        return true;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Reject(message, reason));

    public void Nack(Message message)
    {
        NegativelyAcknowledged.Add(message.Id);
        if (failNack)
            throw NackFailure;
    }

    public async Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        Nack(message);
    }

    public void Purge() => _messages.Clear();

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
