#region License
/* The MIT License (MIT)
Copyright © 2025 Aboubakr Nasef <aboubakrnasef@gmail.com>

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

using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    /// <summary>
    /// The TickerQ Message scheduler Job
    /// </summary>
    public class BrighterTickerQSchedulerJob
    {
        private readonly IAmACommandProcessor _processor;
        private readonly ITickerClock _clock;

        /// <summary>
        /// Creates a scheduler job using the system clock.
        /// </summary>
        /// <param name="processor">The command processor used to dispatch scheduled work.</param>
        public BrighterTickerQSchedulerJob(IAmACommandProcessor processor)
            : this(processor, new SystemTickerClock())
        {
        }

        /// <summary>
        /// Creates a scheduler job using TickerQ's configured clock.
        /// </summary>
        /// <param name="processor">The command processor used to dispatch scheduled work.</param>
        /// <param name="clock">The clock used to determine when scheduled work is due.</param>
        [TickerQConstructor]
        public BrighterTickerQSchedulerJob(IAmACommandProcessor processor, ITickerClock clock)
        {
            _processor = processor;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        [TickerFunction(nameof(FireSchedulerMessageAsync))]
        public async Task FireSchedulerMessageAsync(TickerFunctionContext<string> tickerContext, CancellationToken cancellationToken)
        {
            await WaitUntilScheduledAsync(tickerContext, cancellationToken);
            var schedulerRequest = JsonSerializer.Deserialize<FireSchedulerMessage>(tickerContext.Request, JsonSerialisationOptions.Options)!;
            await _processor.SendAsync(schedulerRequest, cancellationToken: cancellationToken);
        }

        [TickerFunction(nameof(FireSchedulerRequestAsync))]
        public async Task FireSchedulerRequestAsync(TickerFunctionContext<string> tickerContext, CancellationToken cancellationToken)
        {
            await WaitUntilScheduledAsync(tickerContext, cancellationToken);
            var schedulerRequest = JsonSerializer.Deserialize<FireSchedulerRequest>(tickerContext.Request, JsonSerialisationOptions.Options)!;
            await _processor.SendAsync(schedulerRequest, cancellationToken: cancellationToken);
        }

        private async Task WaitUntilScheduledAsync(TickerFunctionContext tickerContext, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = tickerContext.ScheduledFor - _clock.UtcNow;
                if (remaining <= TimeSpan.Zero) return;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50)
                    ? remaining : TimeSpan.FromMilliseconds(50), cancellationToken);
            }
        }

        private sealed class SystemTickerClock : ITickerClock
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }
    }
}
