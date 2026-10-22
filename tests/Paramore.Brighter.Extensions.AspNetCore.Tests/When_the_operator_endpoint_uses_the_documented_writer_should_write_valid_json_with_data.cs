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
using System.Text.Json;
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
/// Characterises that the operator endpoint, using the documented JSON writer, returns valid JSON that holds the overall status and
/// each selected check's name, status, tags, description and data, while liveness and readiness keep the default writer.
/// </summary>
public class OperatorEndpointDocumentedWriterTests
{
    private const string InvestigateName = "brighter-outbox-sweeper-investigate";

    [Fact]
    public async Task When_the_operator_endpoint_uses_the_documented_writer_should_write_valid_json_with_data()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterConsecutiveFailures(services, new FakeTimeProvider());
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        using var client = app.GetTestClient();
        var service = app.Services.GetRequiredService<HealthCheckService>();

        //Act
        var response = await client.GetAsync(BrighterHealthEndpoints.OperatorPath);
        var body = await response.Content.ReadAsStringAsync();
        var report = await service.CheckHealthAsync();

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        Assert.NotEqual(HealthStatus.Unhealthy, report.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, report.Entries[InvestigateName].Status);

        var entry = Assert.Single(root.GetProperty("entries").EnumerateArray());
        Assert.Equal(InvestigateName, entry.GetProperty("name").GetString());
        Assert.Equal("Degraded", entry.GetProperty("status").GetString());
        Assert.Equal([RemedyCheckConvention.InvestigateTag], entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
        Assert.False(string.IsNullOrEmpty(entry.GetProperty("description").GetString()));

        var data = entry.GetProperty("data");
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), data.GetProperty(RemedyCheckConvention.RemedyKindKey).GetString());
        Assert.Equal(["consecutive_failed_sweeps"], data.GetProperty(RemedyCheckConvention.ConditionsKey).EnumerateArray().Select(name => name.GetString()));

        var values = data.GetProperty("consecutive_failed_sweeps");
        Assert.Equal(JsonValueKind.Number, values.GetProperty("timer_interval_seconds").ValueKind);
        Assert.Equal(OutboxSweeperRecordScenario.TimerIntervalSeconds, values.GetProperty("timer_interval_seconds").GetDouble());
        Assert.Equal(OutboxSweeperRecordScenario.ConsecutiveFailures, values.GetProperty("count").GetInt32());
        Assert.Equal(OutboxSweeperRecordScenario.ConsecutiveFailures, values.GetProperty("threshold").GetInt32());
    }

    [Fact]
    public async Task When_a_topic_is_tripped_should_write_its_topics_as_a_json_array()
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
        var body = await client.GetStringAsync(BrighterHealthEndpoints.OperatorPath);
        var report = await service.CheckHealthAsync();

        //Assert
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        Assert.NotEqual(HealthStatus.Unhealthy, report.Entries[InvestigateName].Status);
        var data = Assert.Single(root.GetProperty("entries").EnumerateArray()).GetProperty("data");
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), data.GetProperty(RemedyCheckConvention.RemedyKindKey).GetString());
        var tripped = data.GetProperty("tripped_topics");
        Assert.Equal(1, tripped.GetProperty("count").GetInt32());
        Assert.Equal(["orders"], tripped.GetProperty("topics").EnumerateArray().Select(topic => topic.GetString()));
    }

    [Fact]
    public async Task When_a_sweep_is_hung_should_keep_the_default_writer_and_codes_on_liveness_and_readiness()
    {
        //Arrange
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            OutboxSweeperRecordScenario.RegisterHungSweep(services, new FakeTimeProvider());
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        });
        using var client = app.GetTestClient();
        var service = app.Services.GetRequiredService<HealthCheckService>();

        //Act
        var live = await client.GetAsync(BrighterHealthEndpoints.LivePath);
        var ready = await client.GetAsync(BrighterHealthEndpoints.ReadyPath);
        var operatorBody = await client.GetStringAsync(BrighterHealthEndpoints.OperatorPath);
        var report = await service.CheckHealthAsync();

        //Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, live.StatusCode);
        Assert.Equal("Degraded", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
        Assert.NotEqual(HealthStatus.Unhealthy, report.Status);
        using var document = JsonDocument.Parse(operatorBody);
        var root = document.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        var data = Assert.Single(root.GetProperty("entries").EnumerateArray()).GetProperty("data");
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), data.GetProperty(RemedyCheckConvention.RemedyKindKey).GetString());
        Assert.Empty(data.GetProperty(RemedyCheckConvention.ConditionsKey).EnumerateArray());
    }
}
