using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;
using TickerQ.Utilities;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    /// <summary>
    /// A message and request scheduler implementation using TickerQ for time-based delayed execution
    /// </summary>
    /// <param name="timeTickerManager">The TickerQ time ticker manager for scheduling tasks</param>
    /// <param name="timeProvider"><see cref="System.TimeProvider"/></param>
    /// <param name="getOrCreateSchedulerId">Function to generate or retrieve a scheduler identifier </param>
    /// <param name="parseSchedulerId">Function to parse a string scheduler ID into a Guid</param>
    public class TickerQScheduler(
        ITimeTickerManager<TimeTicker> timeTickerManager,
        TimeProvider timeProvider,
        Func<string> getOrCreateSchedulerId,
        Func<string, Guid> parseSchedulerId
        )
        : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSync, IAmARequestSchedulerAsync
    {
        #region MessageScheduler
        /// <inheritdoc />
        public async Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var id = getOrCreateSchedulerId();
            var tickerRequest = JsonSerializer.Serialize(
                 new FireSchedulerMessage { Id = id, Async = true, Message = message },
                 JsonSerialisationOptions.Options);
            var ticker = new TimeTicker
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerMessageAsync),
                Request = TickerHelper.CreateTickerRequest<string>(tickerRequest),
            };

            var result = await timeTickerManager.AddAsync(ticker, cancellationToken);

            return result.Result.Id.ToString();
        }

        /// <inheritdoc />
        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }
            return ScheduleAsync(message, timeProvider.GetUtcNow().Add(delay), cancellationToken);
        }

        /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
        public async Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var id = parseSchedulerId(schedulerId);
            var result = await timeTickerManager.UpdateAsync(id, ua => ua.ExecutionTime = at.UtcDateTime, cancellationToken);
            return result.IsSucceded;
        }

        /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "Invalid delay, it can't be negative");
            }

            return ReSchedulerAsync(schedulerId, timeProvider.GetUtcNow().Add(delay));
        }

        /// <inheritdoc />
        public string Schedule(Message message, DateTimeOffset at)
        {
            var id = getOrCreateSchedulerId();
            var tickerRequest = JsonSerializer.Serialize(
                 new FireSchedulerMessage { Id = id, Async = false, Message = message },
                 JsonSerialisationOptions.Options);
            var ticker = new TimeTicker
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerMessageAsync),
                Request = TickerHelper.CreateTickerRequest<string>(tickerRequest),
            };
            var result = BrighterAsyncContext.Run(async () => await timeTickerManager.AddAsync(ticker));
            return result.Result.Id.ToString();
        }

        /// <inheritdoc />
        public string Schedule(Message message, TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }
            return Schedule(message, timeProvider.GetUtcNow().Add(delay));
        }

        /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)"/>
        public bool ReScheduler(string schedulerId, DateTimeOffset at)
        {
            return BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, at));
        }

        /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)" />
        public bool ReScheduler(string schedulerId, TimeSpan delay)
        {
            return BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, delay));
        }

        /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync"/>
        public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
        {
            var guid = parseSchedulerId(id);
            await timeTickerManager.DeleteAsync(guid);
        }

        /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel"/>
        public void Cancel(string id)
        {
            BrighterAsyncContext.Run(async () => await CancelAsync(id));
        }


        #endregion

        #region RequestScheduler
        /// <inheritdoc />
        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
                 where TRequest : class, IRequest
        {
            var id = getOrCreateSchedulerId();
            var tickerRequest = JsonSerializer.Serialize(
                 new FireSchedulerRequest
                 {
                     Id = id,
                     Async = false,
                     SchedulerType = type,
                     RequestType = typeof(TRequest).FullName!,
                     RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options),
                 },
                 JsonSerialisationOptions.Options);

            var ticker = new TimeTicker
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerRequestAsync),
                Request = TickerHelper.CreateTickerRequest<string>(tickerRequest),
            };

            var result = BrighterAsyncContext.Run(async () => await timeTickerManager.AddAsync(ticker));
            return result.Result.Id.ToString();
        }

        /// <inheritdoc />
        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
            where TRequest : class, IRequest
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }

            return Schedule(request, type, timeProvider.GetUtcNow().Add(delay));
        }

        /// <inheritdoc />
        public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken)
            where TRequest : class, IRequest
        {
            var id = getOrCreateSchedulerId();
            var tickerRequest = JsonSerializer.Serialize(
                 new FireSchedulerRequest
                 {
                     Id = id,
                     Async = true,
                     SchedulerType = type,
                     RequestType = typeof(TRequest).FullName!,
                     RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options),
                 },
                 JsonSerialisationOptions.Options);

            var ticker = new TimeTicker
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerRequestAsync),
                Request = TickerHelper.CreateTickerRequest<string>(tickerRequest),
            };

            var result = await timeTickerManager.AddAsync(ticker, cancellationToken);

            return result.Result.Id.ToString();
        }

        /// <inheritdoc />
        public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken)
            where TRequest : class, IRequest
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }

            return await ScheduleAsync(request, type, timeProvider.GetUtcNow().Add(delay), cancellationToken);
        }
        #endregion
    }
}
