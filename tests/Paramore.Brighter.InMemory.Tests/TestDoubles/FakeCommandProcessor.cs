using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    /// <summary>
    /// Used for Sweeper tests, will not clear the message!!!
    /// </summary>
    public class FakeCommandProcessor : IAmACommandProcessor
    {
        /// <summary>
        /// Message has been dispatched (to the bus or directly to the handler)
        /// </summary>
        public readonly ConcurrentDictionary<Guid, IRequest> Dispatched = new ConcurrentDictionary<Guid, IRequest>();
        /// <summary>
        /// Message has been placed into the outbox but not sent or dispatched
        /// </summary>
        public readonly ConcurrentQueue<DepositedMessage> Deposited = new ConcurrentQueue<DepositedMessage>();
        
        public void Send<T>(T command) where T : class, IRequest
        {
            Dispatched.TryAdd(command.Id, command);
        }

        public Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if (cancellationToken.IsCancellationRequested)
                tcs.SetCanceled(cancellationToken);
            
            Send(command);

            return tcs.Task;
        }

        public void Publish<T>(T @event) where T : class, IRequest
        {
            Dispatched.TryAdd(@event.Id, @event);
        }

        public Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
              var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
              
              if (cancellationToken.IsCancellationRequested)
                  tcs.SetCanceled(cancellationToken);
         
              Publish(@event);

              return tcs.Task;
        }

        public void Post<T>(T request) where T : class, IRequest
        {
            ClearOutbox(DepositPost(request));
        }

        public Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
              var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
              
              if (cancellationToken.IsCancellationRequested)
                  tcs.SetCanceled(cancellationToken);
              
              Post(request);

              return tcs.Task;
        }

        public Guid DepositPost<T>(T request) where T : class, IRequest
        {
            Deposited.Enqueue(new DepositedMessage(request));
            return request.Id;
        }

        public Guid[] DepositPost<T>(IEnumerable<T> request) where T : class, IRequest
        {
            var ids = new List<Guid>();
            foreach (T r in request)
            {
                ids.Add(DepositPost(r));
            }

            return ids.ToArray();
        }

        public Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled(cancellationToken);
            
            DepositPost(request);
            
            tcs.SetResult(request.Id);

            return tcs.Task;

        }

        public async Task<Guid[]> DepositPostAsync<T>(IEnumerable<T> requests, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var ids = new List<Guid>();
            foreach (T r in requests)
            {
                ids.Add(await DepositPostAsync(r, cancellationToken: cancellationToken));
            }

            return ids.ToArray();
        }

        public void ClearOutbox(params Guid[] posts)
        {
            foreach (var post in posts)
            {
                var msg = Deposited.First(m => m.Request.Id == post);
                Dispatched.TryAdd(post, msg.Request);
            }
        }

        public void ClearOutbox(int amountToClear = 100, int minimumAge = 5000, Dictionary<string, object> args = null)
        {
            var depositedMessages = Deposited.Where(m =>
                m.EnqueuedTime < DateTime.Now.AddMilliseconds(-1 * minimumAge) &&
                !Dispatched.ContainsKey(m.Request.Id))
                .Take(amountToClear)
                .Select(m => m.Request.Id)
                .ToArray();

            ClearOutbox(depositedMessages);
        }

        public Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled(cancellationToken);

            ClearOutbox(posts.ToArray());

            tcs.SetResult(Guid.Empty);
            
            return tcs.Task;
        }

        public void ClearAsyncOutbox(int amountToClear = 100, int minimumAge = 5000, bool useBulk = false, Dictionary<string, object> args = null)
        {
            ClearOutbox(amountToClear, minimumAge);
        }

        public Task BulkClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ClearOutboxAsync(posts, continueOnCapturedContext, cancellationToken);
        }

        public TResponse Call<T, TResponse>(T request, int timeOutInMilliseconds) where T : class, ICall where TResponse : class, IResponse
        {
            return null;
        }
    }

    public class DepositedMessage
    {
        public IRequest Request { get; }
        public DateTime EnqueuedTime { get; }

        public DepositedMessage(IRequest request)
        {
            Request = request;
            EnqueuedTime = DateTime.UtcNow;
        }
    }
}
