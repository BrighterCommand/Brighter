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
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that consumer conditions route to the readiness endpoint (channel failures at the threshold) or the liveness endpoint
/// (missing consumers), and never to both.
/// </summary>
[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerConditionRoutingTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_consumer_is_at_the_failure_threshold_should_fail_readiness_and_pass_liveness()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 1, [3])]);
        host.Dispatcher.Receive();
        await FailuresObserved(host);
        await using var app = await StartAsync(host);
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    [Fact]
    public async Task When_one_of_two_consumers_is_missing_should_fail_liveness_and_pass_readiness()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        host.StopPerformer("orders", 1);
        await ConsumersRunning(host, 1);
        await using var app = await StartAsync(host);
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task When_no_consumer_is_running_while_the_dispatcher_runs_should_fail_liveness_and_pass_readiness()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        host.StopPerformer("orders", 0);
        host.StopPerformer("orders", 1);
        await ConsumersRunning(host, 0);
        await using var app = await StartAsync(host);
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task When_the_dispatcher_has_not_started_should_pass_liveness_and_readiness()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        await using var app = await StartAsync(host);
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task When_a_consumer_is_missing_should_select_only_the_restart_check_for_liveness()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        host.StopPerformer("orders", 1);
        await ConsumersRunning(host, 1);
        await using var app = await StartAsync(host);
        var service = app.Services.GetRequiredService<HealthCheckService>();

        //Act
        var live = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.RestartTag));
        var ready = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.TakeOutOfServiceTag));

        //Assert
        Assert.Equal(["brighter-consumer-restart"], live.Entries.Keys);
        Assert.Equal(["brighter-consumer-take-out-of-service"], ready.Entries.Keys);
        Assert.Equal(HealthStatus.Degraded, live.Status);
        Assert.Equal(HealthStatus.Healthy, ready.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), live.Entries["brighter-consumer-restart"].Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.TakeOutOfService),
            ready.Entries["brighter-consumer-take-out-of-service"].Data[RemedyCheckConvention.RemedyKindKey]);
    }

    [Fact]
    public async Task When_a_consumer_is_at_the_failure_threshold_should_select_only_the_take_out_of_service_check_for_readiness()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 1, [3])]);
        host.Dispatcher.Receive();
        await FailuresObserved(host);
        await using var app = await StartAsync(host);
        var service = app.Services.GetRequiredService<HealthCheckService>();

        //Act
        var live = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.RestartTag));
        var ready = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.TakeOutOfServiceTag));

        //Assert
        Assert.Equal(["brighter-consumer-restart"], live.Entries.Keys);
        Assert.Equal(["brighter-consumer-take-out-of-service"], ready.Entries.Keys);
        Assert.Equal(HealthStatus.Healthy, live.Status);
        Assert.Equal(HealthStatus.Degraded, ready.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), live.Entries["brighter-consumer-restart"].Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.TakeOutOfService),
            ready.Entries["brighter-consumer-take-out-of-service"].Data[RemedyCheckConvention.RemedyKindKey]);
    }

    private static Task<WebApplication> StartAsync(MultiSubscriptionReceiveHealthTestHost host)
        => BrighterHealthEndpoints.StartAsync(services =>
        {
            services.AddSingleton(host.Dispatcher);
            services.AddHealthChecks().AddBrighterConsumerHealthChecks();
        });

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }

    private static async Task FailuresObserved(MultiSubscriptionReceiveHealthTestHost host)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Transports("orders").Count < 1)
            await Task.Delay(5, timeout.Token);
        await Task.WhenAll(host.Transports("orders").Select(transport => transport.FailuresObserved)).WaitAsync(s_guard);
    }
}
