#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

internal sealed class InMemoryDisposalFailureChannelFactory(
    Exception failure, int failingConsumerCount, bool failAcknowledgment) : IAmAChannelFactory
{
    private readonly InternalBus _bus = new();

    public List<InMemoryDisposalFailureConsumer> Consumers { get; } = [];

    public void Publish(Message message) => _bus.Enqueue(message);

    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        => new Channel(subscription.ChannelName, subscription.RoutingKey, CreateConsumer(subscription));

    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        => new ChannelAsync(subscription.ChannelName, subscription.RoutingKey, CreateConsumer(subscription));

    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
        => Task.FromResult(CreateAsyncChannel(subscription));

    private InMemoryDisposalFailureConsumer CreateConsumer(Subscription subscription)
    {
        var inner = new InMemoryMessageConsumer(subscription.RoutingKey, _bus, TimeProvider.System);
        var consumer = new InMemoryDisposalFailureConsumer(
            inner, Consumers.Count < failingConsumerCount ? failure : null, failAcknowledgment);
        Consumers.Add(consumer);
        return consumer;
    }
}
