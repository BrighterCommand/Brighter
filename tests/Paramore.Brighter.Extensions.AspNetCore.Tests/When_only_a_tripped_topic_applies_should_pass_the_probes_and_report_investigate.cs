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
/// Characterises that a tripped topic on an active sweeper fails neither probe and shows in the report as a Degraded investigate entry.
/// </summary>
public class TrippedTopicReportTests
{
    [Fact]
    public async Task When_only_a_tripped_topic_applies_should_pass_the_probes_and_report_investigate()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterActiveSweeper(services, new FakeTimeProvider());
            OutboxSweeperRecordScenario.RegisterTrippedTopics(services, "orders");
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        using var client = app.GetTestClient();
        var service = app.Services.GetRequiredService<HealthCheckService>();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);
        var report = await service.CheckHealthAsync();

        //Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var investigate = report.Entries["brighter-outbox-sweeper-investigate"];
        Assert.Equal(HealthStatus.Degraded, investigate.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), investigate.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Contains("tripped_topics", (string[])investigate.Data[RemedyCheckConvention.ConditionsKey]);
        Assert.All(report.Entries.Values, entry => Assert.NotEqual(HealthStatus.Unhealthy, entry.Status));
        Assert.NotEqual(HealthStatus.Unhealthy, report.Status);
    }
}
