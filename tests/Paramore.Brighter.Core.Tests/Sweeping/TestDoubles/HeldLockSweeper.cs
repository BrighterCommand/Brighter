using System;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// Builds a <see cref="TimedOutboxSweeper"/> whose every sweep finds the lock held, and whose measurements a test can read.
/// </summary>
public static class HeldLockSweeper
{
    public static TimedOutboxSweeper Create(
        SweepableOutbox outbox,
        RecordedSweeperMeasurements measurements,
        TimeProvider clock,
        int timerInterval,
        IAmAnOutboxCircuitBreaker? circuitBreaker = null) =>
        new(
            outbox.ScopeFactory,
            new HeldDistributedLock(),
            new TimedOutboxSweeperOptions { TimerInterval = timerInterval, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory),
            circuitBreaker: circuitBreaker);
}
