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

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class ThrowingMediatorFailureThresholdTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Test]
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
            await Assert.That(firstFailed && secondFailed && thirdFailed && fourthFailed).IsTrue().Because("The sweeps did not all end");
            await Assert.That(afterTwo.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(afterTwo.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(afterTwo.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(afterTwo.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();

            await Assert.That(afterThree.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(afterThree.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(afterThree.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(afterThree.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });
            await Assert.That(afterThree.Description).Contains("3");
            await Assert.That(afterThree.Description).Contains("10s");

            await Assert.That(afterFour.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(afterFour.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(afterFour.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(afterFour.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });
            await Assert.That(afterFour.Description).Contains("4");
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static bool WaitForSweeps(SwitchableOutboxProducerMediator mediator, OutboxSweeperState state, int count) =>
        SpinWait.SpinUntil(() => mediator.SweepsAttempted >= count && !state.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
}
