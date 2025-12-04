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


using TickerQ;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    /// <summary>
    /// Factory for creating TickerQ-based message and request schedulers.
    /// TickerQ is a time-based job scheduling library that manages scheduled tasks using time tickers.
    /// </summary>
    /// <param name="timeTickerManager"><see cref="System.TimeProvider"/>.</param>
    /// <param name="timeProvider">The time provider used for obtaining the current time and creating timers.</param>
    public class TickerQSchedulerFactory(
        ITimeTickerManager<TimeTickerEntity> timeTickerManager,
        ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity> tickerPersistenceProvider,
        ITickerQHostScheduler tickerQHostScheduler,
        TimeProvider timeProvider) : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
    {
        /// <summary>
        /// Gets a function that creates or retrieves a unique scheduler identifier as a string.
        /// </summary>
        public Func<string> GetOrCreateSchedulerId => () => Uuid.NewAsString();

        /// <summary>
        /// Gets a function that parses a string representation of a scheduler identifier into a Guid.
        /// </summary>
        public Func<string, Guid> ParseSchedulerId => (str) => Uuid.Parse(str);

        /// <inheritdoc />
        public IAmAMessageScheduler Create(IAmACommandProcessor processor)
        {
            return GetTickerQScheduler();
        }

        /// <inheritdoc />
        public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        {
            return GetTickerQScheduler();
        }

        /// <inheritdoc />
        public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        {
            return GetTickerQScheduler();
        }

        private TickerQScheduler GetTickerQScheduler()
        {
            return new TickerQScheduler(timeTickerManager, tickerPersistenceProvider, tickerQHostScheduler, timeProvider, GetOrCreateSchedulerId, ParseSchedulerId);
        }
    }
}
