#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

/// <summary>
/// An async producer that reports every send attempt to an observer before it succeeds or fails.
/// </summary>
/// <param name="topic">The topic the producer publishes to.</param>
/// <param name="fails">True if every send should fail.</param>
/// <param name="onSend">Called with each message at the moment it is sent.</param>
public class ObservingProducerAsync(RoutingKey topic, bool fails, Action<Message> onSend) : IAmAMessageProducerAsync
{
    public Publication Publication { get; } = new() { Topic = topic };
    public Activity? Span { get; set; }
    public IAmAMessageScheduler? Scheduler { get; set; }

    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        onSend(message);
        return fails
            ? throw new InvalidOperationException("The broker refused the message")
            : Task.CompletedTask;
    }

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => SendAsync(message, cancellationToken);

    public ValueTask DisposeAsync() => default;
}
