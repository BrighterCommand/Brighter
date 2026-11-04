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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperNoBreakerOverrunTimerTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_the_sweeper_has_no_breaker_should_create_no_overrun_timer()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        var clock = new TimerCountingTimeProvider(fake);
        using var outbox = new SweepableOutbox();
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new HeldDistributedLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 10, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory));

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);
        var waiting = clock.WaitForTimers(1, s_timeout);
        await Task.Delay(300);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(swept && waiting, "The sweeper did not sweep and wait for its next sweep");
        Assert.Equal(1, clock.TimersCreated);
    }
}
