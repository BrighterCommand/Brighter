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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperRefreshOnOverrunTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_a_sweep_overruns_the_timer_interval_should_refresh_the_view_from_the_overrun_timer()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var shared = new InMemoryTrippedTopicStore();
        var observedStore = new SignallingTrippedTopicStore(shared);
        var instanceA = new DistributedOutboxCircuitBreaker(shared, s_options, timeProvider: clock);
        var instanceB = new DistributedOutboxCircuitBreaker(observedStore, s_options, timeProvider: clock);
        var orders = new RoutingKey("orders");
        var distributedLock = new SwitchableDistributedLock();
        distributedLock.Grant();
        using var mediator = new GatedOutboxProducerMediator();
        var services = SweeperContainer.Create(distributedLock, clock, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddSingleton<IAmAnOutboxCircuitBreaker>(instanceB);
        using var provider = services.BuildServiceProvider();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();
        var state = provider.GetRequiredService<OutboxSweeperState>();

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var held = mediator.WaitForSweepToStart(s_timeout);
        var tickRead = SpinWait.SpinUntil(() => observedStore.ReadCount >= 1, s_timeout);
        clock.Advance(TimeSpan.FromSeconds(5));
        instanceA.TripTopic(orders);
        var written = SpinWait.SpinUntil(() => shared.ReadLiveAsync(new TripWindow(clock.GetUtcNow(), s_options.CooldownDuration)).GetAwaiter().GetResult().Count > 0, s_timeout);
        clock.Advance(TimeSpan.FromSeconds(5));
        var listed = SpinWait.SpinUntil(() => instanceB.TrippedTopics.Contains(orders), s_timeout);
        var stillHeld = state.Read().IsSweepInProgress;
        var stopping = sweeper.StopAsync(CancellationToken.None);
        mediator.Open();
        await stopping;
        sweeper.Dispose();
        var readsAtStop = observedStore.ReadCount;
        clock.Advance(TimeSpan.FromSeconds(30));
        await Task.Delay(300);

        // Assert
        Assert.True(held, "The sweep was not held by the dispatch double");
        Assert.True(tickRead && written, "The tick did not refresh, or instance A did not share its trip");
        Assert.True(listed, "The overrun timer did not refresh the view while the sweep was held");
        Assert.True(stillHeld, "The sweep ended before the view was refreshed");
        Assert.Equal(readsAtStop, observedStore.ReadCount);
    }
}
