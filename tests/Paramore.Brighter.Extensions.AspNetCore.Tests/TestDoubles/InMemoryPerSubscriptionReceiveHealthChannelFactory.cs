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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Creates one <see cref="InMemoryReceiveHealthConsumer"/> per performer, grouped by subscription, each with its own channel-failure count.
/// </summary>
internal sealed class InMemoryPerSubscriptionReceiveHealthChannelFactory(
    InternalBus bus, IReadOnlyCollection<ReceiveHealthSubscription> subscriptions) : IAmAChannelFactory
{
    private readonly Dictionary<string, List<InMemoryReceiveHealthConsumer>> _consumers =
        subscriptions.ToDictionary(subscription => subscription.Name, _ => new List<InMemoryReceiveHealthConsumer>());

    public IReadOnlyList<InMemoryReceiveHealthConsumer> ConsumersFor(string subscription)
    {
        lock (_consumers)
            return _consumers[subscription].ToArray();
    }

    public IReadOnlyList<InMemoryReceiveHealthConsumer> AllConsumers()
    {
        lock (_consumers)
            return _consumers.Values.SelectMany(consumers => consumers).ToArray();
    }

    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        => new Channel(subscription.ChannelName, subscription.RoutingKey, CreateConsumer(subscription));

    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        => new ChannelAsync(subscription.ChannelName, subscription.RoutingKey, CreateConsumer(subscription));

    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken cancellationToken = default)
        => Task.FromResult(CreateAsyncChannel(subscription));

    private InMemoryReceiveHealthConsumer CreateConsumer(Subscription subscription)
    {
        var name = subscription.Name.Value;
        var definition = subscriptions.Single(candidate => candidate.Name == name);
        lock (_consumers)
        {
            var created = _consumers[name];
            var consumer = new InMemoryReceiveHealthConsumer(subscription.RoutingKey, bus,
                definition.FailuresFor(created.Count));
            created.Add(consumer);
            return consumer;
        }
    }
}
