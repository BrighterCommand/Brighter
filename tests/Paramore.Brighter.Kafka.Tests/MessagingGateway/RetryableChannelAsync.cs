#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// Wraps an <see cref="IAmAChannelAsync"/> and retries <see cref="ReceiveAsync"/> calls
/// when the broker returns <see cref="MessageType.MT_NONE"/>.
/// Kafka on CI can be slow to deliver messages, so this avoids flaky test failures.
/// </summary>
public class RetryableChannelAsync(IAmAChannelAsync inner, int maxRetries = 5) : IAmAChannelAsync
{
    public ChannelName Name => inner.Name;

    public RoutingKey RoutingKey => inner.RoutingKey;

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default) =>
        inner.AcknowledgeAsync(message, cancellationToken);

    public Task PurgeAsync(CancellationToken cancellationToken = default) =>
        inner.PurgeAsync(cancellationToken);

    public async Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var message = await inner.ReceiveAsync(timeout, cancellationToken);
            if (message.Header.MessageType != MessageType.MT_NONE)
                return message;
        }

        return await inner.ReceiveAsync(timeout, cancellationToken);
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default) =>
        inner.RejectAsync(message, reason, cancellationToken);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default) =>
        inner.NackAsync(message, cancellationToken);

    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null,
        CancellationToken cancellationToken = default) =>
        inner.RequeueAsync(message, timeOut, cancellationToken);

    public void Enqueue(params Message[] message) => inner.Enqueue(message);

    public void Stop(RoutingKey topic) => inner.Stop(topic);

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() =>
        inner is IAsyncDisposable asyncDisposable
            ? asyncDisposable.DisposeAsync()
            : ValueTask.CompletedTask;
}
