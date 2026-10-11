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

/// <summary>
/// Creates real channels around tracked empty transports.
/// </summary>
internal sealed class InMemoryUnopenedConsumerChannelFactory(bool throwOnDispose) : IAmAChannelFactory, IDisposable
{
    private readonly ManualResetEventSlim _resumeCreation = new(true);
    private int _pauseNextCreation;

    public List<InMemoryUnopenedConsumer> Transports { get; } = [];
    public int? FailOnCreationNumber { get; set; }
    public TaskCompletionSource<bool> CreationPaused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void PauseNextCreation()
    {
        _resumeCreation.Reset();
        Interlocked.Exchange(ref _pauseNextCreation, 1);
    }

    public void ResumeCreation() => _resumeCreation.Set();

    public void Dispose() => _resumeCreation.Dispose();

    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        => new Channel(subscription.ChannelName, subscription.RoutingKey, CreateConsumer(), subscription.BufferSize);

    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        => new ChannelAsync(subscription.ChannelName, subscription.RoutingKey, CreateConsumer(), subscription.BufferSize);

    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
        => Task.FromResult(CreateAsyncChannel(subscription));

    private InMemoryUnopenedConsumer CreateConsumer()
    {
        if (Transports.Count + 1 == FailOnCreationNumber)
            throw new InvalidOperationException("Channel creation failed.");
        var consumer = new InMemoryUnopenedConsumer(throwOnDispose);
        Transports.Add(consumer);
        if (Interlocked.Exchange(ref _pauseNextCreation, 0) == 1)
        {
            CreationPaused.TrySetResult(true);
            if (!_resumeCreation.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Channel creation was not resumed.");
        }
        return consumer;
    }
}
