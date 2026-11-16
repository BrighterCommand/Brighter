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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(DispatcherHealthCheckCollection.NAME)]
public class DispatcherAddedConsumerCompletionTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public async Task When_a_newly_added_consumer_stops_should_remove_it_while_existing_consumers_keep_running(
        MessagePumpType messagePumpType)
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost(
            [new ReceiveHealthSubscription("orders", 1)], messagePumpType);
        host.Dispatcher.Receive();
        var original = Assert.Single(host.Dispatcher.Consumers);
        await host.Transports("orders")[0].FailuresObserved.WaitAsync(s_guard);

        //Act
        ((Dispatcher)host.Dispatcher).SetActivePerformers("orders", 2);
        var added = Assert.Single(host.Dispatcher.Consumers, consumer => consumer != original);
        await host.Transports("orders")[1].FailuresObserved.WaitAsync(s_guard);
        host.StopPerformer("orders", 1);
        await added.Job!.WaitAsync(s_guard);
        var elapsed = Stopwatch.StartNew();
        while (host.Dispatcher.Consumers.Contains(added) && elapsed.Elapsed < s_guard)
            await Task.Delay(5);

        //Assert
        Assert.DoesNotContain(added, host.Dispatcher.Consumers);
        Assert.Same(original, Assert.Single(host.Dispatcher.Consumers));
        Assert.False(original.Job!.IsCompleted);
    }
}
