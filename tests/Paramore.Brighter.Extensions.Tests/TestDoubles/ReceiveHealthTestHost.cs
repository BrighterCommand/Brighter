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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

internal sealed class ReceiveHealthTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public InMemoryReceiveHealthConsumer Transport { get; }

    public IDispatcher Dispatcher { get; }

    public BrighterServiceActivatorHealthCheck HealthCheck =>
        ActivatorUtilities.GetServiceOrCreateInstance<BrighterServiceActivatorHealthCheck>(_provider);

    public ReceiveHealthTestHost(MessagePumpType messagePumpType, int failures = 5, bool brokenCircuit = false,
        bool registerHealthCheck = true)
    {
        var routingKey = new RoutingKey("receive-health");
        Transport = new InMemoryReceiveHealthConsumer(routingKey, new InternalBus(), failures, brokenCircuit);
        var services = new ServiceCollection();
        services.AddConsumers(options =>
        {
            options.DefaultChannelFactory = new InMemoryReceiveHealthChannelFactory(Transport);
            options.Subscriptions =
            [
                new Subscription(new SubscriptionName("receive-health"), new ChannelName("receive-health:in-memory"),
                    routingKey, typeof(ReceiveHealthCommand), messagePumpType: messagePumpType,
                    channelFailureDelay: TimeSpan.FromMilliseconds(10), timeOut: TimeSpan.FromMilliseconds(10),
                    emptyChannelDelay: TimeSpan.FromMilliseconds(1))
            ];
        }).MapperRegistry(_ => { });
        if (registerHealthCheck)
            services.AddSingleton<BrighterServiceActivatorHealthCheck>();

        _provider = services.BuildServiceProvider();
        Dispatcher = _provider.GetRequiredService<IDispatcher>();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            var jobs = Dispatcher.Consumers.Select(consumer => consumer.Job).OfType<Task>().ToArray();
            var shutdown = Dispatcher.End();
            Transport.Recover();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(jobs).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Transport.Recover();
            await _provider.DisposeAsync();
        }
    }
}
