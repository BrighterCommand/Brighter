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

namespace Paramore.Brighter.InMemory.Tests.TestDoubles;

/// <summary>
/// Tracks timer disposal without changing the underlying virtual timer's behavior.
/// </summary>
public sealed class TrackedSchedulerTimer(ITimer inner, Action onDisposed, Action<TimeSpan> beforeChange) : ITimer
{
    private int _disposed;

    /// <inheritdoc />
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        beforeChange(dueTime);
        return inner.Change(dueTime, period);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        inner.Dispose();
        RecordDisposal();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        RecordDisposal();
    }

    private void RecordDisposal()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            onDisposed();
        }
    }
}
