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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

/// <summary>
/// A bulk-capable async producer that puts every message for its topic in one batch and can refuse to send it.
/// </summary>
/// <param name="topic">The topic this producer serves.</param>
/// <param name="fails">True if sending a batch should fail.</param>
public class TopicFailingBulkProducerAsync(RoutingKey topic, bool fails) : IAmABulkMessageProducerAsync
{
    /// <inheritdoc />
    public Publication Publication { get; } = new() { Topic = topic };

    /// <inheritdoc />
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <inheritdoc />
    public ValueTask<IEnumerable<IAmAMessageBatch>> CreateBatchesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken)
        => new([new MessageBatch(messages.ToArray())]);

    /// <inheritdoc />
    public Task SendAsync(IAmAMessageBatch batch, CancellationToken cancellationToken)
        => fails
            ? throw new InvalidOperationException("The broker refused the batch")
            : Task.CompletedTask;
}
