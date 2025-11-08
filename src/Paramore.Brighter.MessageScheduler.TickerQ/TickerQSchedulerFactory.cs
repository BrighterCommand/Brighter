using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    /// <summary>
    /// Factory for creating TickerQ-based message and request schedulers.
    /// TickerQ is a time-based job scheduling library that manages scheduled tasks using time tickers.
    /// </summary>
    /// <param name="timeTickerManager"><see cref="System.TimeProvider"/>.</param>
    /// <param name="timeProvider">The time provider used for obtaining the current time and creating timers.</param>
    public class TickerQSchedulerFactory(ITimeTickerManager<TimeTicker> timeTickerManager, TimeProvider timeProvider) : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
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
            return new TickerQScheduler(timeTickerManager, timeProvider, GetOrCreateSchedulerId, ParseSchedulerId);
        }
    }
}
