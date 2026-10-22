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
#pragma warning disable CS0618

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Hosts a real <c>Dispatcher</c> over several named in-memory subscriptions. It is built awaiting; nothing receives until the dispatcher is told to.
/// A consumer can be made to fail, a performer can be stopped, and an empty subscription list leaves the dispatcher with no consumers.
/// </summary>
internal sealed class MultiSubscriptionReceiveHealthTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly InMemoryPerSubscriptionReceiveHealthChannelFactory _factory;

    public IDispatcher Dispatcher { get; }

    public BrighterServiceActivatorHealthCheck HealthCheck =>
        ActivatorUtilities.GetServiceOrCreateInstance<BrighterServiceActivatorHealthCheck>(_provider);

    public MultiSubscriptionReceiveHealthTestHost(IReadOnlyCollection<ReceiveHealthSubscription> subscriptions,
        MessagePumpType messagePumpType = MessagePumpType.Reactor, IAmAChannelFactory? channelFactory = null,
        bool registerHealthCheck = true, Func<IAmAChannelFactory, IAmAChannelFactory>? decorateChannelFactory = null)
    {
        _factory = new InMemoryPerSubscriptionReceiveHealthChannelFactory(new InternalBus(), subscriptions);
        var services = new ServiceCollection();
        services.AddConsumers(options =>
        {
            options.DefaultChannelFactory = channelFactory ?? decorateChannelFactory?.Invoke(_factory) ?? _factory;
            options.Subscriptions = subscriptions
                .Select(subscription => (Subscription)new Subscription(new SubscriptionName(subscription.Name),
                    new ChannelName($"{subscription.Name}:in-memory"), new RoutingKey(subscription.Name),
                    typeof(ReceiveHealthCommand), noOfPerformers: subscription.Performers,
                    messagePumpType: messagePumpType, channelFailureDelay: TimeSpan.FromMilliseconds(10),
                    timeOut: TimeSpan.FromMilliseconds(10), emptyChannelDelay: TimeSpan.FromMilliseconds(1)))
                .ToArray();
        }).MapperRegistry(_ => { });
        if (registerHealthCheck)
            services.AddSingleton<BrighterServiceActivatorHealthCheck>();

        _provider = services.BuildServiceProvider();
        Dispatcher = _provider.GetRequiredService<IDispatcher>();
    }

    public IReadOnlyList<InMemoryReceiveHealthConsumer> Transports(string subscription)
        => _factory.ConsumersFor(subscription);

    public void StopPerformer(string subscription, int performerIndex)
        => _factory.ConsumersFor(subscription)[performerIndex].Quit();

    public async ValueTask DisposeAsync()
    {
        try
        {
            var jobs = Dispatcher.Consumers.Select(consumer => consumer.Job).OfType<Task>().ToArray();
            var shutdown = Dispatcher.End();
            RecoverAll();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(jobs).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            RecoverAll();
            await _provider.DisposeAsync();
        }
    }

    private void RecoverAll()
    {
        foreach (var consumer in _factory.AllConsumers())
            consumer.Recover();
    }
}
