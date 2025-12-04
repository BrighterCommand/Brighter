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
using Paramore.Brighter.Tasks;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;

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
        ITimeTickerManager<TimeTickerEntity> timeTickerManager,
        ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity> tickerPersistenceProvider,
        TimeProvider timeProvider,
        Func<string> getOrCreateSchedulerId,
        Func<string, Guid> parseSchedulerId
        )
        : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSync, IAmARequestSchedulerAsync
    {
        /// <inheritdoc />
        public async Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            var ticker = CreateTimeTicker(message, at, true);

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
            if (at < timeProvider.GetUtcNow())
            {
                throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
            }
            var id = parseSchedulerId(schedulerId);
            var ticker = await tickerPersistenceProvider.GetTimeTickerById(id);

            if (ticker.Status == TickerStatus.Idle || ticker.Status == TickerStatus.Queued)
            {
                ticker.ExecutionTime = at.UtcDateTime;
                var result = await timeTickerManager.UpdateAsync(ticker, cancellationToken);
                return result.IsSucceeded;
            }
            return false;
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
            var ticker = CreateTimeTicker(message, at, false);
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
            await timeTickerManager.DeleteAsync(guid, cancellationToken);
        }

        /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel"/>
        public void Cancel(string id)
        {
            BrighterAsyncContext.Run(async () => await CancelAsync(id));
        }

        /// <inheritdoc />
        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
                 where TRequest : class, IRequest
        {
            var ticker = CreateTimeTicker(request, type, at, false);

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
        public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken = default)
            where TRequest : class, IRequest
        {
            var ticker = CreateTimeTicker(request, type, at, true);

            var result = await timeTickerManager.AddAsync(ticker, cancellationToken);

            return result.Result.Id.ToString();
        }

        /// <inheritdoc />
        public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken = default)
            where TRequest : class, IRequest
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
            }

            return await ScheduleAsync(request, type, timeProvider.GetUtcNow().Add(delay), cancellationToken);
        }

        private TimeTickerEntity CreateTimeTicker<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, bool isAsync) where TRequest : class, IRequest
        {
            var id = getOrCreateSchedulerId();
            var tickerRequest = JsonSerializer.Serialize(
                 new FireSchedulerRequest
                 {
                     Id = id,
                     Async = isAsync,
                     SchedulerType = type,
                     RequestType = typeof(TRequest).FullName!,
                     RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options),
                 },
                 JsonSerialisationOptions.Options);

            var ticker = new TimeTickerEntity
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerRequestAsync),
                Request = TickerHelper.CreateTickerRequest<string>(tickerRequest),
            };
            return ticker;
        }
        private TimeTickerEntity CreateTimeTicker(Message message, DateTimeOffset at, bool isAsync)
        {
            var id = getOrCreateSchedulerId();
            var tickerRequest = JsonSerializer.Serialize(
                 new FireSchedulerMessage { Id = id, Async = isAsync, Message = message },
                 JsonSerialisationOptions.Options);
            var ticker = new TimeTickerEntity
            {
                Id = parseSchedulerId(id),
                ExecutionTime = at.UtcDateTime,
                Function = nameof(BrighterTickerQSchedulerJob.FireSchedulerMessageAsync),
                Request = TickerHelper.CreateTickerRequest<string>(tickerRequest),
            };
            return ticker;
        }
    }
}
