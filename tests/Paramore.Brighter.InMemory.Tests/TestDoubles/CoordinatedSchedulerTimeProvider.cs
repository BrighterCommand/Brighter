#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles;

/// <summary>
/// Coordinates overlapping timer creation while retaining virtual time for delivery assertions.
/// </summary>
public sealed class CoordinatedSchedulerTimeProvider : TimeProvider, IDisposable
{
    /// <summary>
    /// Runs timer callbacks on a dedicated worker when enabled.
    /// </summary>
    public bool CallbackOnWorker { get; set; }

    /// <summary>
    /// Configures the ambient context seen by the timer callback.
    /// </summary>
    public Action BeforeCallback { get; set; } = () => { };

    private readonly FakeTimeProvider _inner = new();
    private readonly CountdownEvent _arrivals = new(2);
    private int _coordinatedTimers;
    private bool _coordinate;
    private int _activeTimerCount;
    private int _pauseNextActivation;
    private readonly ManualResetEventSlim _activationStarted = new();
    private readonly ManualResetEventSlim _resumeActivation = new();

    /// <summary>
    /// Pauses the next activation before applying its due time.
    /// </summary>
    public void PauseNextActivation() => Interlocked.Exchange(ref _pauseNextActivation, 1);

    /// <summary>
    /// Waits until an activation is paused.
    /// </summary>
    public bool WaitForActivation() => _activationStarted.Wait(TimeSpan.FromSeconds(10));

    /// <summary>
    /// Allows the paused activation to continue.
    /// </summary>
    public void ResumeActivation() => _resumeActivation.Set();

    /// <summary>
    /// Gets the number of timers that have not been disposed.
    /// </summary>
    public int ActiveTimerCount => Volatile.Read(ref _activeTimerCount);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

    /// <summary>
    /// Allows the next two timer creations to overlap. The bounded wait also permits a
    /// scheduler that serializes timer creation to make progress.
    /// </summary>
    public void CoordinateNextTwoTimers() => _coordinate = true;

    /// <summary>
    /// Advances virtual time and executes due callbacks.
    /// </summary>
    public void Advance(TimeSpan delta) => _inner.Advance(delta);

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = _inner.CreateTimer(s => InvokeCallback(callback, s), state, dueTime, period);
        if (_coordinate && Interlocked.Increment(ref _coordinatedTimers) <= 2)
        {
            _arrivals.Signal();
            _arrivals.Wait(TimeSpan.FromSeconds(5));
        }
        Interlocked.Increment(ref _activeTimerCount);
        return new TrackedSchedulerTimer(timer, () => Interlocked.Decrement(ref _activeTimerCount), BeforeChange);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _arrivals.Dispose();
        _activationStarted.Dispose();
        _resumeActivation.Dispose();
    }

    private void InvokeCallback(TimerCallback callback, object? state)
    {
        if (!CallbackOnWorker)
        {
            BeforeCallback();
            callback(state);
            return;
        }

        Task.Factory.StartNew(() =>
        {
            BeforeCallback();
            callback(state);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }

    private void BeforeChange(TimeSpan dueTime)
    {
        if (dueTime != Timeout.InfiniteTimeSpan && Interlocked.Exchange(ref _pauseNextActivation, 0) == 1)
        {
            _activationStarted.Set();
            if (!_resumeActivation.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Timer activation was not released");
            }
        }
    }
}
