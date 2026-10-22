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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class ThrowingMediatorFailureThresholdTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Fact]
    public async Task When_the_outbox_mediator_throws_on_every_sweep_should_degrade_at_the_third_failure()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var mediator = new SwitchableOutboxProducerMediator();
        mediator.Throw("every sweep fails");
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddLogging();
                services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();
                var state = provider.GetRequiredService<OutboxSweeperState>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            //Act
            await sweeper.StartAsync(CancellationToken.None);
            var firstFailed = WaitForSweeps(mediator, state, 1);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var secondFailed = WaitForSweeps(mediator, state, 2);
            var afterTwo = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var thirdFailed = WaitForSweeps(mediator, state, 3);
            var afterThree = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var fourthFailed = WaitForSweeps(mediator, state, 4);
            var afterFour = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            //Assert
            Assert.True(firstFailed && secondFailed && thirdFailed && fourthFailed, "The sweeps did not all end");
            Assert.Equal(HealthStatus.Healthy, afterTwo.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, afterTwo.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), afterTwo.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Empty(Assert.IsType<string[]>(afterTwo.Data[RemedyCheckConvention.ConditionsKey]));

            Assert.Equal(HealthStatus.Degraded, afterThree.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, afterThree.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), afterThree.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "consecutive_failed_sweeps" }, Assert.IsType<string[]>(afterThree.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("3", afterThree.Description);
            Assert.Contains("10s", afterThree.Description);

            Assert.Equal(HealthStatus.Degraded, afterFour.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, afterFour.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), afterFour.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "consecutive_failed_sweeps" }, Assert.IsType<string[]>(afterFour.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("4", afterFour.Description);
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static bool WaitForSweeps(SwitchableOutboxProducerMediator mediator, OutboxSweeperState state, int count) =>
        SpinWait.SpinUntil(() => mediator.SweepsAttempted >= count && !state.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
}
