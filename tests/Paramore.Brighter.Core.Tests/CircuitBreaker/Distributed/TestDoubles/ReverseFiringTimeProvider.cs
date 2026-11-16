#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

/// <summary>
/// A time provider whose clock stands still until advanced, and which fires the timers that fall due in the reverse of the order they were created.
/// </summary>
public sealed class ReverseFiringTimeProvider : TimeProvider
{
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(100);
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _now = s_start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_timers) return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_timers)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            return timer;
        }
    }

    public async Task AdvanceAsync(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_timers)
        {
            _now += by;
            due = _timers.Where(timer => timer.IsDueAt(_now)).Reverse().ToList();
        }

        foreach (var timer in due)
        {
            timer.Fire();
            await Task.Delay(s_settle);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        private bool _disposed;

        public bool IsDueAt(DateTimeOffset now) => !_disposed && dueAt <= now;

        public void Fire()
        {
            if (!_disposed)
                callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
