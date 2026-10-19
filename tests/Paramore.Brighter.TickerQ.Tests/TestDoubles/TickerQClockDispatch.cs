#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jay Sen

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

using Paramore.Brighter.MessageScheduler.TickerQ;
using TickerQ.Utilities.Base;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class TickerQClockDispatch(InMemoryTickerClock clock, TickerQClockDispatchState state,
    BrighterTickerQSchedulerJob job)
{
    [TickerFunction(nameof(DispatchWithCustomClock))]
    public async Task DispatchWithCustomClock(TickerFunctionContext<string> context, CancellationToken cancellationToken)
    {
        clock.SetUtcNow(context.ScheduledFor.AddHours(-1));
        clock.ObserveWait();
        var dispatch = state.Message
            ? job.FireSchedulerMessageAsync(context, cancellationToken)
            : job.FireSchedulerRequestAsync(context, cancellationToken);
        state.Invocation.TrySetResult((context, dispatch));
        await dispatch;
    }
}
