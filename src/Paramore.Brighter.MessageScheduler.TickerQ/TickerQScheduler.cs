using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;
using TickerQ.Utilities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    public class TickerQScheduler(
        ITimeTickerManager<TimeTicker> timeTickerManager,
        Func<Message, string> getOrCreateMessageSchedulerId
        )
        : /*IAmAMessageSchedulerSync,*/ IAmAMessageSchedulerAsync/*, IAmARequestSchedulerSync, IAmARequestSchedulerAsync*/
    {



        public async Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var id = getOrCreateMessageSchedulerId(message);
            var request = JsonSerializer.Serialize(
                 new FireSchedulerMessage { Id = id, Async = false, Message = message },
                 JsonSerialisationOptions.Options);
            var ticker = new TimeTicker
            {
                Id = Guid.NewGuid(),
                ExecutionTime = at.UtcDateTime,
                Function=nameof(BrighterTickerQSchedulerJob.FireSchedulerMessageAsync),
                Request = TickerHelper.CreateTickerRequest<string>(request),
            };

            var result = await timeTickerManager.AddAsync(ticker);

            return result.Result.Id.ToString();
        }

        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
        {
            var guid = Guid.Parse(id);
            await timeTickerManager.DeleteAsync(guid);
        }
    }
}
