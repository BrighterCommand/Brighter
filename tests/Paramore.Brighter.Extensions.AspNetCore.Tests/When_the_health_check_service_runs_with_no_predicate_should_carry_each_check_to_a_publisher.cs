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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that the report from a health check service with no predicate, and a registered publisher, carry every check's
/// status, description and data without a response writer.
/// </summary>
public class UnfilteredReportAndPublisherTests
{
    [Test]
    public async Task When_a_sweep_is_hung_should_carry_each_check_to_the_report_and_a_publisher()
    {
        //Arrange
        var publisher = new RecordingHealthCheckPublisher();

        //Act
        var (report, published) = await RunAsync(publisher,
            services => OutboxSweeperRecordScenario.RegisterHungSweep(services, new FakeTimeProvider()));

        //Assert
        await AssertEveryCheckCarried(report, published);
        await Assert.That(report.Entries["brighter-outbox-sweeper-restart"].Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(report.Entries["brighter-outbox-sweeper-investigate"].Status).IsEqualTo(HealthStatus.Healthy);
    }

    [Test]
    public async Task When_only_failures_apply_should_carry_each_check_to_the_report_and_a_publisher()
    {
        //Arrange
        var publisher = new RecordingHealthCheckPublisher();

        //Act
        var (report, published) = await RunAsync(publisher,
            services => OutboxSweeperRecordScenario.RegisterConsecutiveFailures(services, new FakeTimeProvider()));

        //Assert
        await AssertEveryCheckCarried(report, published);
        await Assert.That(report.Entries["brighter-outbox-sweeper-restart"].Status).IsEqualTo(HealthStatus.Healthy);
        var investigate = report.Entries["brighter-outbox-sweeper-investigate"];
        await Assert.That(investigate.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That((string[])investigate.Data[RemedyCheckConvention.ConditionsKey]).Contains("consecutive_failed_sweeps");
    }

    [Test]
    public async Task When_only_a_tripped_topic_applies_should_carry_each_check_to_the_report_and_a_publisher()
    {
        //Arrange
        var publisher = new RecordingHealthCheckPublisher();

        //Act
        var (report, published) = await RunAsync(publisher, services =>
        {
            OutboxSweeperRecordScenario.RegisterActiveSweeper(services, new FakeTimeProvider());
            OutboxSweeperRecordScenario.RegisterTrippedTopics(services, "orders");
        });

        //Assert
        await AssertEveryCheckCarried(report, published);
        await Assert.That(report.Entries["brighter-outbox-sweeper-restart"].Status).IsEqualTo(HealthStatus.Healthy);
        var investigate = report.Entries["brighter-outbox-sweeper-investigate"];
        await Assert.That(investigate.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That((string[])investigate.Data[RemedyCheckConvention.ConditionsKey]).Contains("tripped_topics");
    }

    private static async Task<(HealthReport Report, HealthReport Published)> RunAsync(
        RecordingHealthCheckPublisher publisher, Action<IServiceCollection> register)
    {
        await using var app = await BrighterHealthEndpoints.StartAsync(services =>
        {
            register(services);
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
            services.AddSingleton<IHealthCheckPublisher>(publisher);
            services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Delay = TimeSpan.FromMilliseconds(1);
                options.Period = TimeSpan.FromHours(1);
            });
        });
        var service = app.Services.GetRequiredService<HealthCheckService>();

        var report = await service.CheckHealthAsync();
        var published = await publisher.Published.WaitAsync(TimeSpan.FromSeconds(10));
        return (report, published);
    }

    private static async Task AssertEveryCheckCarried(HealthReport report, HealthReport published)
    {
        await Assert.That(report.Entries.Keys.OrderBy(name => name, StringComparer.Ordinal)).IsEqualTo(["brighter-outbox-sweeper-investigate", "brighter-outbox-sweeper-restart"]);
        await Assert.That(report.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        foreach (var entry in report.Entries.Values)
{
    await Assert.That(entry.Status).IsNotEqualTo(HealthStatus.Unhealthy);
    await Assert.That(string.IsNullOrEmpty(entry.Description)).IsFalse();
    await Assert.That(entry.Data).IsNotEmpty();
}
        await Assert.That(report.Entries["brighter-outbox-sweeper-restart"].Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That(report.Entries["brighter-outbox-sweeper-investigate"].Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));

        await Assert.That(published.Entries.Keys.OrderBy(name => name)).IsEqualTo(report.Entries.Keys.OrderBy(name => name));
        foreach (var (name, entry) in report.Entries)
        {
            var seen = published.Entries[name];
            await Assert.That(seen.Status).IsEqualTo(entry.Status);
            await Assert.That(seen.Description).IsEqualTo(entry.Description);
            await Assert.That(seen.Data.OrderBy(pair => pair.Key).Select(pair => pair.Key)).IsEqualTo(entry.Data.OrderBy(pair => pair.Key).Select(pair => pair.Key));
            await Assert.That(seen.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(entry.Data[RemedyCheckConvention.RemedyKindKey]);
        }
    }
}
