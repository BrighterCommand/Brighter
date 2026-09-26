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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

internal sealed class InMemoryCancellationMessageProducer(IAmABus bus, Publication publication)
    : IAmAMessageProducerAsync, IAmABulkMessageProducerAsync, IDisposable
{
    private readonly InMemoryMessageProducer _producer = new(bus, publication);

    public Publication Publication => _producer.Publication;
    public Activity? Span { get; set; }
    public IAmAMessageScheduler? Scheduler { get; set; }
    public List<CancellationToken> SendTokens { get; } = [];
    public Action? OnSending { get; set; }

    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        SendTokens.Add(cancellationToken);
        OnSending?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return _producer.SendAsync(message, cancellationToken);
    }

    public Task SendAsync(IAmAMessageBatch batch, CancellationToken cancellationToken)
    {
        SendTokens.Add(cancellationToken);
        OnSending?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return _producer.SendAsync(batch, cancellationToken);
    }

    public ValueTask<IEnumerable<IAmAMessageBatch>> CreateBatchesAsync(
        IEnumerable<Message> messages, CancellationToken cancellationToken)
        => _producer.CreateBatchesAsync(messages, cancellationToken);

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => _producer.SendWithDelayAsync(message, delay, cancellationToken);

    public ValueTask DisposeAsync() => _producer.DisposeAsync();

    public void Dispose() => _producer.Dispose();
}
