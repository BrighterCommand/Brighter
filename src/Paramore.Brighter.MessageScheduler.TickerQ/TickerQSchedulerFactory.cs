using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    public class TickerQSchedulerFactory(ITimeTickerManager<TimeTicker> timeTickerManager) : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
    {

        public Func<Message, string> GetOrCreateSchedulerId { get; set; } = _ => Uuid.NewAsString();


        public IAmAMessageScheduler Create(IAmACommandProcessor processor)
        {
            return new TickerQScheduler(timeTickerManager, GetOrCreateSchedulerId);
        }

        public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        {
            throw new NotImplementedException();
        }

        public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        {
            throw new NotImplementedException();
        }
    }
}
