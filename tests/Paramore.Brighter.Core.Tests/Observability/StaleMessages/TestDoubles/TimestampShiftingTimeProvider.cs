#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// A <see cref="TimerCountingTimeProvider"/> whose timestamps a test can move ahead without moving <see cref="TimeProvider.GetUtcNow"/>
/// or firing a timer, and which signals the first timestamp read after one of its timers is re-armed with <see cref="ITimer.Change"/>.
/// </summary>
public class TimestampShiftingTimeProvider(DateTimeOffset startDateTime) : TimerCountingTimeProvider(startDateTime)
{
    private readonly TaskCompletionSource<bool> _readAfterChange = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _shift;
    private int _changed;

    /// <summary>Completes once a timestamp has been read after a timer was re-armed, after that read has computed its value.</summary>
    public Task ReadAfterChange => _readAfterChange.Task;

    /// <summary>Moves every timestamp read from now on ahead by the given time.</summary>
    /// <param name="by">How far ahead the timestamps move.</param>
    public void ShiftTimestamps(TimeSpan by) =>
        Interlocked.Add(ref _shift, by.Ticks * TimestampFrequency / TimeSpan.TicksPerSecond);

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        var timestamp = base.GetTimestamp() + Volatile.Read(ref _shift);
        if (Interlocked.Exchange(ref _changed, 0) == 1)
            _readAfterChange.TrySetResult(true);
        return timestamp;
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        new ChangeSignallingTimer(base.CreateTimer(callback, state, dueTime, period), this);

    private void TimerChanged() => Volatile.Write(ref _changed, 1);

    private sealed class ChangeSignallingTimer(ITimer timer, TimestampShiftingTimeProvider clock) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock.TimerChanged();
            return timer.Change(dueTime, period);
        }

        public void Dispose() => timer.Dispose();

        public ValueTask DisposeAsync() => timer.DisposeAsync();
    }
}
