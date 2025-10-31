using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    public class TickerQScheduler : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSync, IAmARequestSchedulerAsync
    {
        public string Schedule(Message message, TimeSpan delay)
        {
            throw new NotImplementedException();
        }
        public void Cancel(string id)
        {
            throw new NotImplementedException();
        }
        public bool ReScheduler(string schedulerId, DateTimeOffset at)
        {
            throw new NotImplementedException();
        }

        public bool ReScheduler(string schedulerId, TimeSpan delay)
        {
            throw new NotImplementedException();
        }

        public string Schedule(Message message, DateTimeOffset at)
        {
            throw new NotImplementedException();
        }

        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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

        public Task CancelAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
             where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
             where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken)
             where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken)
             where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }
    }
}
