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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that the operator endpoint, using the documented JSON writer, returns valid JSON that holds the overall status and
/// each selected check's name, status, tags, description and data, while liveness and readiness keep the default writer.
/// </summary>
public class OperatorEndpointDocumentedWriterTests
{
    private const string InvestigateName = "brighter-outbox-sweeper-investigate";

    [Test]
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("status").GetString()).IsEqualTo("Degraded");
        await Assert.That(report.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        await Assert.That(report.Entries[InvestigateName].Status).IsNotEqualTo(HealthStatus.Unhealthy);

        var entry = await Assert.That(root.GetProperty("entries").EnumerateArray()).HasSingleItem();
        await Assert.That(entry.GetProperty("name").GetString()).IsEqualTo(InvestigateName);
        await Assert.That(entry.GetProperty("status").GetString()).IsEqualTo("Degraded");
        await Assert.That(entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString())).IsEqualTo([RemedyCheckConvention.InvestigateTag]);
        await Assert.That(string.IsNullOrEmpty(entry.GetProperty("description").GetString())).IsFalse();

        var data = entry.GetProperty("data");
        await Assert.That(data.GetProperty(RemedyCheckConvention.RemedyKindKey).GetString()).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That(data.GetProperty(RemedyCheckConvention.ConditionsKey).EnumerateArray().Select(name => name.GetString())).IsEqualTo(["consecutive_failed_sweeps"]);

        var values = data.GetProperty("consecutive_failed_sweeps");
        await Assert.That(values.GetProperty("timer_interval_seconds").ValueKind).IsEqualTo(JsonValueKind.Number);
        await Assert.That(values.GetProperty("timer_interval_seconds").GetDouble()).IsEqualTo(OutboxSweeperRecordScenario.TimerIntervalSeconds);
        await Assert.That(values.GetProperty("count").GetInt32()).IsEqualTo(OutboxSweeperRecordScenario.ConsecutiveFailures);
        await Assert.That(values.GetProperty("threshold").GetInt32()).IsEqualTo(OutboxSweeperRecordScenario.ConsecutiveFailures);
    }

    [Test]
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
        await Assert.That(root.GetProperty("status").GetString()).IsEqualTo("Degraded");
        await Assert.That(report.Entries[InvestigateName].Status).IsNotEqualTo(HealthStatus.Unhealthy);
        var data = (await Assert.That(root.GetProperty("entries").EnumerateArray()).HasSingleItem()).GetProperty("data");
        await Assert.That(data.GetProperty(RemedyCheckConvention.RemedyKindKey).GetString()).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        var tripped = data.GetProperty("tripped_topics");
        await Assert.That(tripped.GetProperty("count").GetInt32()).IsEqualTo(1);
        await Assert.That(tripped.GetProperty("topics").EnumerateArray().Select(topic => topic.GetString())).IsEqualTo(["orders"]);
    }

    [Test]
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
        await Assert.That(live.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
        await Assert.That(await live.Content.ReadAsStringAsync()).IsEqualTo("Degraded");
        await Assert.That(ready.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await ready.Content.ReadAsStringAsync()).IsEqualTo("Healthy");
        await Assert.That(report.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        using var document = JsonDocument.Parse(operatorBody);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("status").GetString()).IsEqualTo("Healthy");
        var data = (await Assert.That(root.GetProperty("entries").EnumerateArray()).HasSingleItem()).GetProperty("data");
        await Assert.That(data.GetProperty(RemedyCheckConvention.RemedyKindKey).GetString()).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That(data.GetProperty(RemedyCheckConvention.ConditionsKey).EnumerateArray()).IsEmpty();
    }
}
