#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

public class InMemoryClaimCheckChannel : IAmAChannelSync, IAmAChannelAsync
{
    private readonly Queue<Message> _messages = new();

    public ChannelName Name { get; } = new("claim-check-delivery");
    public RoutingKey RoutingKey { get; } = new("claim-check-delivery");
    public Action<Message>? OnRequeue { get; set; }
    public Action<Message>? OnAcknowledge { get; set; }
    public bool FailRequeue { get; set; }
    public bool FailAcknowledge { get; set; }
    public bool Redeliver { get; set; }
    public int RequeueCount { get; private set; }
    public int AcknowledgeCount { get; private set; }
    public List<Message> Rejected { get; } = [];
    public List<Message> Nacked { get; } = [];

    public void Enqueue(params Message[] messages)
    {
        foreach (var message in messages)
            _messages.Enqueue(message);
    }

    public Message Receive(TimeSpan? timeout) =>
        _messages.Count > 0 ? _messages.Dequeue() : MessageFactory.CreateQuitMessage(RoutingKey);

    public Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default) =>
        Task.FromResult(Receive(timeout));

    public bool Requeue(Message message, TimeSpan? timeOut = null)
    {
        RequeueCount++;
        OnRequeue?.Invoke(message);
        if (FailRequeue)
            throw new InvalidOperationException("Requeue failed");

        if (Redeliver)
            Enqueue(message);
        return true;
    }

    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Requeue(message, timeOut));

    public void Acknowledge(Message message)
    {
        OnAcknowledge?.Invoke(message);
        if (FailAcknowledge)
            throw new InvalidOperationException("Acknowledgement failed");
        AcknowledgeCount++;
    }

    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        Acknowledge(message);
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        Rejected.Add(message);
        return true;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reject(message, reason));

    public void Nack(Message message) => Nacked.Add(message);
    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        Nack(message);
        return Task.CompletedTask;
    }

    public void Purge() => _messages.Clear();
    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        Purge();
        return Task.CompletedTask;
    }

    public void Stop(RoutingKey topic) => Enqueue(MessageFactory.CreateQuitMessage(topic));
    public void Dispose() { }
    public ValueTask DisposeAsync() => default;
}
