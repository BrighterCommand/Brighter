using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Namotion.Reflection;
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
        TimeProvider timeProvider,
        Func<string> getOrCreateSchedulerId,
        Func<string, Guid> parseSchedulerId
        )
        : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync/*, IAmARequestSchedulerSync, IAmARequestSchedulerAsync*/
    {



        public async Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var id = getOrCreateSchedulerId();
            var request = JsonSerializer.Serialize(
                 new FireSchedulerMessage { Id = id, Async = true, Message = message },
                 JsonSerialisationOptions.Options);
            var ticker = new TimeTicker
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerMessageAsync),
                Request = TickerHelper.CreateTickerRequest<string>(request),
            };

            var result = await timeTickerManager.AddAsync(ticker, cancellationToken);

            return result.Result.Id.ToString();
        }

        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }
            return ScheduleAsync(message, timeProvider.GetUtcNow().Add(delay), cancellationToken);
        }

        public async Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var id = parseSchedulerId(schedulerId);
            var result = await timeTickerManager.UpdateAsync(id, ua => ua.ExecutionTime = at.UtcDateTime, cancellationToken);
            return result.IsSucceded;
        }

        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "Invalid delay, it can't be negative");
            }

            return ReSchedulerAsync(schedulerId, timeProvider.GetUtcNow().Add(delay));
        }

        public string Schedule(Message message, DateTimeOffset at)
        {
            var id = getOrCreateSchedulerId();
            var request = JsonSerializer.Serialize(
                 new FireSchedulerMessage { Id = id, Async = false, Message = message },
                 JsonSerialisationOptions.Options);
            var ticker = new TimeTicker
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerMessageAsync),
                Request = TickerHelper.CreateTickerRequest<string>(request),
            };
            var result = BrighterAsyncContext.Run(async () => await  timeTickerManager.AddAsync(ticker));
            return result.Result.Id.ToString();
        }

        public string Schedule(Message message, TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }
            return Schedule(message, timeProvider.GetUtcNow().Add(delay));
        }

        public bool ReScheduler(string schedulerId, DateTimeOffset at)
        {
            return BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, at));
        }

        public bool ReScheduler(string schedulerId, TimeSpan delay)
        {
            return BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, delay));
        }
        public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
        {
            var guid = parseSchedulerId(id);
            await timeTickerManager.DeleteAsync(guid);
        }

        public void Cancel(string id)
        {
            BrighterAsyncContext.Run(async () => await CancelAsync(id));
        }
    }
}
