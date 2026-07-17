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

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// Wraps an <see cref="IAmAChannelSync"/> and retries <see cref="Receive"/> calls
/// when the broker returns <see cref="MessageType.MT_NONE"/>.
/// Kafka on CI can be slow to deliver messages, so this avoids flaky test failures.
/// </summary>
public class RetryableChannelSync(IAmAChannelSync inner, int maxRetries = 5) : IAmAChannelSync
{
    public ChannelName Name => inner.Name;

    public RoutingKey RoutingKey => inner.RoutingKey;

    public void Acknowledge(Message message) => inner.Acknowledge(message);

    public void Purge() => inner.Purge();

    public Message Receive(TimeSpan? timeout)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var message = inner.Receive(timeout);
            if (message.Header.MessageType != MessageType.MT_NONE)
                return message;
        }

        return inner.Receive(timeout);
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null) =>
        inner.Reject(message, reason);

    public void Nack(Message message) => inner.Nack(message);

    public bool Requeue(Message message, TimeSpan? timeOut = null) =>
        inner.Requeue(message, timeOut);

    public void Enqueue(params Message[] message) => inner.Enqueue(message);

    public void Stop(RoutingKey topic) => inner.Stop(topic);

    public void Dispose() => inner.Dispose();
}
