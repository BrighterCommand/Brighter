#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

namespace Paramore.Brighter.Kafka.Tests.TestDoubles;

internal sealed class InMemoryFaultingDisposalTimer(ITimer inner) : ITimer
{
    public bool FailOnDispose { get; set; }

    public int DisposeCalls { get; private set; }

    public Exception Failure { get; } = new InvalidOperationException("The timer failed during disposal.");

    public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

    public void Dispose()
    {
        DisposeCalls++;
        inner.Dispose();
        if (FailOnDispose)
            throw Failure;
    }

    public async ValueTask DisposeAsync()
    {
        DisposeCalls++;
        await inner.DisposeAsync().ConfigureAwait(false);
        if (FailOnDispose)
            throw Failure;
    }
}
