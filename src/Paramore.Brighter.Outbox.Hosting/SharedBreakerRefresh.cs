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

namespace Paramore.Brighter.Outbox.Hosting;

// Asks a shared breaker to refresh its view on each sweeper tick, and from an overrun timer while a sweep is held
internal sealed class SharedBreakerRefresh : IAmASharedBreakerRefresh
{
    private readonly Action _startRefresh;
    private readonly TimeSpan _interval;
    private readonly Func<TimeSpan> _elapsed;
    private readonly object _refreshLock = new();
    private readonly ITimer _overrunTimer;
    private readonly IDisposable _sweepingScope;
    private TimeSpan? _lastRefreshRequest;
    private int _sweeping;

    // startRefresh must not throw
    public SharedBreakerRefresh(Action startRefresh, TimeSpan interval, TimeProvider timeProvider, Func<TimeSpan> elapsed)
    {
        _startRefresh = startRefresh;
        _interval = interval;
        _elapsed = elapsed;
        _sweepingScope = new SweepingScope(this);
        _overrunTimer = timeProvider.CreateTimer(_ => RefreshWhileSweeping(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void RequestRefresh(TimeSpan now)
    {
        lock (_refreshLock)
        {
            if (_lastRefreshRequest is { } last && now - last < TimeSpan.FromTicks(_interval.Ticks / 2))
                return;

            _lastRefreshRequest = now;
        }

        _startRefresh();
    }

    public IDisposable WhileSweeping()
    {
        Volatile.Write(ref _sweeping, 1);
        ChangeOverrunTimer(_interval);
        return _sweepingScope;
    }

    public void Dispose() => _overrunTimer.Dispose();

    private void EndSweeping()
    {
        Volatile.Write(ref _sweeping, 0);
        ChangeOverrunTimer(Timeout.InfiniteTimeSpan);
    }

    private void RefreshWhileSweeping()
    {
        if (Volatile.Read(ref _sweeping) == 0)
            return;

        RequestRefresh(_elapsed());
        ChangeOverrunTimer(_interval);
    }

    private void ChangeOverrunTimer(TimeSpan dueTime)
    {
        try { _overrunTimer.Change(dueTime, Timeout.InfiniteTimeSpan); }
        catch (ObjectDisposedException) { }
    }

    private sealed class SweepingScope(SharedBreakerRefresh refresh) : IDisposable
    {
        public void Dispose() => refresh.EndSweeping();
    }
}
