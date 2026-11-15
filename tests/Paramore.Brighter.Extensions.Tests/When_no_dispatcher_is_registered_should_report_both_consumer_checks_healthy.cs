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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Verifies a publisher-only host that opts in to the consumer checks still reports both of them Healthy.
/// </summary>
public class ConsumerChecksNoDispatcherTests
{
    [Test]
    public async Task When_no_dispatcher_is_registered_should_report_both_consumer_checks_healthy()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddBrighterConsumerHealthChecks();
        using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        //Act
        HealthReport? report = null;
        var exception = await TestExceptionRecorder.CaptureAsync(async () => report = await healthCheckService.CheckHealthAsync());

        //Assert
        await Assert.That(exception).IsNull();
        await Assert.That(report).IsNotNull();
        await AssertHealthyWithNoDispatcher(report.Entries["brighter-consumer-take-out-of-service"], RemedyKind.TakeOutOfService);
        await AssertHealthyWithNoDispatcher(report.Entries["brighter-consumer-restart"], RemedyKind.Restart);
    }

    private static async Task AssertHealthyWithNoDispatcher(HealthReportEntry entry, RemedyKind kind)
    {
        await Assert.That(entry.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(entry.Description).Contains("No dispatcher is registered");
        await Assert.That(entry.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(kind));
        await Assert.That((((IEnumerable<string>)(((IEnumerable<string>)(await Assert.That(entry.Data["conditions"]).IsAssignableTo<IEnumerable<string>>())))))).IsEmpty();
    }
}
