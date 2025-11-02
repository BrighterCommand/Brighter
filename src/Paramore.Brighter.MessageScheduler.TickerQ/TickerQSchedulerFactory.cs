using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    public class TickerQSchedulerFactory(ITimeTickerManager<TimeTicker> timeTickerManager, TimeProvider timeProvider) : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
    {

        public Func<string> GetOrCreateSchedulerId => () => Uuid.NewAsString();
        public Func<string, Guid> ParseSchedulerId => (str) => Uuid.Parse(str);



        public IAmAMessageScheduler Create(IAmACommandProcessor processor)
        {
            return GetTickerQScheduler();
        }

        public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        {
            throw new NotImplementedException();
        }

        public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        {
            throw new NotImplementedException();
        }

        public TickerQScheduler GetTickerQScheduler()
        {
         return   new TickerQScheduler(timeTickerManager, timeProvider, GetOrCreateSchedulerId, ParseSchedulerId);
        }
    }
}
