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

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that sweeper conditions route to the liveness endpoint or the operator endpoint, and never to readiness.
/// </summary>
public class SweeperConditionRoutingTests
{
    [Fact]
    public async Task When_a_sweep_is_hung_should_fail_liveness_and_pass_readiness()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterHungSweep(services, new FakeTimeProvider());
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task When_only_failures_apply_should_pass_liveness_and_readiness()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterConsecutiveFailures(services, new FakeTimeProvider());
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task When_only_a_tripped_topic_applies_on_an_active_sweeper_should_pass_liveness_and_readiness()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterActiveSweeper(services, new FakeTimeProvider());
            OutboxSweeperRecordScenario.RegisterTrippedTopics(services, "orders");
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        using var client = app.GetTestClient();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);

        //Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task When_the_endpoints_select_checks_should_keep_the_restart_and_investigate_checks_apart()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterHungSweep(services, new FakeTimeProvider());
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        var service = app.Services.GetRequiredService<HealthCheckService>();

        //Act
        var live = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.RestartTag));
        var ready = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.TakeOutOfServiceTag));
        var operatorReport = await service.CheckHealthAsync(registration => registration.Tags.Contains(RemedyCheckConvention.InvestigateTag));

        //Assert
        Assert.Equal(["brighter-outbox-sweeper-restart"], live.Entries.Keys);
        Assert.Empty(ready.Entries);
        Assert.Equal(["brighter-outbox-sweeper-investigate"], operatorReport.Entries.Keys);
        Assert.NotEqual(HealthStatus.Unhealthy, live.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, operatorReport.Status);
    }
}
