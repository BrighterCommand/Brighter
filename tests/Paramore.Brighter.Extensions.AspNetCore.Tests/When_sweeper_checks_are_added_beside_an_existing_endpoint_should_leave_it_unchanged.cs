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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that adding the sweeper checks beside an existing health endpoint that excludes the remedy tags leaves its status and body
/// unchanged, and that the same endpoint mapped with no predicate selects the restart check.
/// </summary>
[Collection(DispatcherHealthCheckCollection.NAME)]
public class ExistingEndpointUnchangedTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_sweeper_checks_are_added_beside_an_existing_endpoint_should_leave_it_unchanged()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var precondition = await host.HealthCheck.CheckHealthAsync(new HealthCheckContext());

        await using var without = await ExistingHealthEndpoint.StartAsync(
            services => services.AddHealthChecks().Add(ConsumerRegistration(host)),
            endpoints => endpoints.MapExistingHealthEndpointExcludingRemedyChecks());
        await using var with = await ExistingHealthEndpoint.StartAsync(
            services =>
            {
                OutboxSweeperRecordScenario.RegisterHungSweep(services, new FakeTimeProvider());
                OutboxSweeperRecordScenario.RegisterTrippedTopics(services, "orders");
                services.AddHealthChecks().Add(ConsumerRegistration(host));
                services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
            },
            endpoints => endpoints.MapExistingHealthEndpointExcludingRemedyChecks());
        using var withoutClient = without.GetTestClient();
        using var withClient = with.GetTestClient();

        //Act
        var withoutResponse = await withoutClient.GetAsync(ExistingHealthEndpoint.Path);
        var withResponse = await withClient.GetAsync(ExistingHealthEndpoint.Path);
        var withoutBody = await withoutResponse.Content.ReadAsByteArrayAsync();
        var withBody = await withResponse.Content.ReadAsByteArrayAsync();

        //Assert
        Assert.Equal(HealthStatus.Healthy, precondition.Status);
        Assert.Equal("Healthy", System.Text.Encoding.UTF8.GetString(withoutBody));
        Assert.Equal(HttpStatusCode.OK, withoutResponse.StatusCode);
        Assert.Equal(withoutResponse.StatusCode, withResponse.StatusCode);
        Assert.Equal(withoutBody, withBody);
    }

    [Fact]
    public async Task When_the_existing_endpoint_has_no_predicate_should_select_the_restart_check()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var precondition = await host.HealthCheck.CheckHealthAsync(new HealthCheckContext());

        await using var app = await ExistingHealthEndpoint.StartAsync(
            services =>
            {
                OutboxSweeperRecordScenario.RegisterHungSweep(services, new FakeTimeProvider());
                services.AddHealthChecks().Add(ConsumerRegistration(host));
                services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
            },
            endpoints => endpoints.MapExistingHealthEndpointWithoutPredicate());
        using var client = app.GetTestClient();

        //Act
        var response = await client.GetAsync(ExistingHealthEndpoint.Path);
        var body = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.Equal(HealthStatus.Healthy, precondition.Status);
        Assert.Equal("Degraded", body);
    }

    private static HealthCheckRegistration ConsumerRegistration(MultiSubscriptionReceiveHealthTestHost host)
        => new("brighter-consumers", _ => host.HealthCheck, failureStatus: null, tags: null);

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }
}
