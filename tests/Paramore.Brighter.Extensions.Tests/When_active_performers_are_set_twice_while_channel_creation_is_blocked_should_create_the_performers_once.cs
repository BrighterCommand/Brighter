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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(DispatcherHealthCheckCollection.NAME)]
public class OverlappingSetActivePerformersTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_active_performers_are_set_twice_while_channel_creation_is_blocked_should_create_the_performers_once()
    {
        //Arrange
        InMemoryGatedReceiveHealthChannelFactory? gate = null;
        await using var host = new MultiSubscriptionReceiveHealthTestHost(
            [new ReceiveHealthSubscription("orders", 2), new ReceiveHealthSubscription("billing", 1)],
            decorateChannelFactory: factory => gate = new InMemoryGatedReceiveHealthChannelFactory(factory));
        var dispatcher = (Dispatcher)host.Dispatcher;

        try
        {
            var receive = Task.Run(() => dispatcher.Receive());
            await gate!.CreationBlocked.Task.WaitAsync(s_guard);
            gate.Release();
            await receive.WaitAsync(s_guard);
            await OrdersRunning(dispatcher, 2);
            host.StopPerformer("orders", 0);
            host.StopPerformer("orders", 1);
            await OrdersRunning(dispatcher, 0);
            gate.Reset();

            //Act
            var first = Task.Run(() => dispatcher.SetActivePerformers("orders", 2));
            await gate.CreationBlocked.Task.WaitAsync(s_guard);
            var second = Task.Run(() => dispatcher.SetActivePerformers("orders", 2));
            await second.WaitAsync(s_guard);
            gate.Release();
            await first.WaitAsync(s_guard);

            //Assert
            Assert.Equal(2, dispatcher.Consumers.Count(consumer => consumer.Subscription.Name == "orders"));
        }
        finally
        {
            gate?.Release();
        }
    }

    private static async Task OrdersRunning(Dispatcher dispatcher, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (dispatcher.Consumers.Count(consumer => consumer.Subscription.Name == "orders") != expected)
            await Task.Delay(5, timeout.Token);
    }
}
