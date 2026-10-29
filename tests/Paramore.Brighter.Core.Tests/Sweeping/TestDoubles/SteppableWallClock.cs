using System;
using System.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A <see cref="TimeProvider"/> whose wall clock can be stepped backwards, as an NTP correction or a resumed VM
/// does, while its elapsed time (timestamps) and timers only ever move forward with <see cref="Advance"/>.
/// </summary>
public sealed class SteppableWallClock : TimeProvider
{
    private readonly FakeTimeProvider _elapsed = new();
    private TimeSpan _wallClockOffset = TimeSpan.Zero;

    public void Advance(TimeSpan delta) => _elapsed.Advance(delta);

    public void StepWallClockBack(TimeSpan step) => _wallClockOffset -= step;

    public override DateTimeOffset GetUtcNow() => _elapsed.GetUtcNow() + _wallClockOffset;

    public override long GetTimestamp() => _elapsed.GetTimestamp();

    public override long TimestampFrequency => _elapsed.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _elapsed.CreateTimer(callback, state, dueTime, period);
}
