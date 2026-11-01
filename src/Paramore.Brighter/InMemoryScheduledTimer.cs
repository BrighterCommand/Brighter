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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

internal sealed class InMemoryScheduledTimer : ITimer
{
    private readonly object _gate = new();
    private readonly ITimer _timer;
    private readonly TimerCallback _callback;
    private readonly object _state;
    private Action? _pendingCallback;

    public InMemoryScheduledTimer(TimeProvider timeProvider, TimerCallback callback, object state)
    {
        _callback = callback;
        _state = state;
        _timer = timeProvider.CreateTimer(OnElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start(Action install, TimeSpan delay)
    {
        try
        {
            lock (_gate)
            {
                install();
                _timer.Change(delay, Timeout.InfiniteTimeSpan);
            }
        }
        finally
        {
            DispatchPending();
        }
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        try
        {
            lock (_gate)
            {
                return _timer.Change(dueTime, period);
            }
        }
        finally
        {
            DispatchPending();
        }
    }

    public void Dispose() => _timer.Dispose();

    public ValueTask DisposeAsync() => _timer.DisposeAsync();

    private void OnElapsed(object? state)
    {
        var context = ExecutionContext.Capture();
        Action callback = context == null
            ? () => _callback(_state)
            : () => ExecutionContext.Run(context, _ => _callback(_state), null);
        Interlocked.Exchange(ref _pendingCallback, callback);
        if (!Monitor.IsEntered(_gate))
        {
            DispatchPending();
        }
    }

    // Timer callbacks must not wait for Change or invoke handlers while it holds the gate.
    private void DispatchPending()
    {
        do
        {
            if (!Monitor.TryEnter(_gate))
            {
                return;
            }

            Action? callback;
            try
            {
                callback = Interlocked.Exchange(ref _pendingCallback, null);
            }
            finally
            {
                Monitor.Exit(_gate);
            }

            callback?.Invoke();
        } while (Volatile.Read(ref _pendingCallback) != null);
    }
}
