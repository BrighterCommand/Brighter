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
        public readonly ConcurrentDictionary<Guid, IRequest> Dispatched = new ConcurrentDictionary<Guid, IRequest>();
        public readonly ConcurrentQueue<IRequest> Posted = new ConcurrentQueue<IRequest>();
        
        public void Send<T>(T command) where T : class, IRequest
        {
            Dispatched.TryAdd(command.Id, command);
        }

        public Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if (cancellationToken.IsCancellationRequested)
                tcs.SetCanceled();
            
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
                  tcs.SetCanceled();
         
              Publish(@event);

              return tcs.Task;
        }

        public void Post<T>(T request) where T : class, IRequest
        {
            DepositPost(request);
        }

        public Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
              var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
              
              if (cancellationToken.IsCancellationRequested)
                  tcs.SetCanceled();
              
              Post(request);

              return tcs.Task;
        }

        public Guid DepositPost<T>(T request) where T : class, IRequest
        {
            Dispatched.TryAdd(request.Id, request);
            return request.Id;
        }

        public Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled();
            
            DepositPost(request);
            
            tcs.SetResult(request.Id);

            return tcs.Task;

        }

        public void ClearOutbox(params Guid[] posts)
        {
            foreach (var post in posts)
            { 
                Posted.Enqueue(Dispatched[post]);
            }
        }

        public void ClearOutbox(int amountToClear = 100, int minimumAge = 5000)
        {
            //Note this does not take time into account
            var postedKeys = Posted.Select(m => m.Id);
            
            var messagesToDispatch =
                Dispatched.Where(m => !postedKeys.Contains(m.Key))
                    .Take(amountToClear)
                    .Select(m => m.Key)
                    .ToArray();
            
            ClearOutbox(messagesToDispatch);
        }

        public Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled();

            ClearOutbox(posts.ToArray());

            tcs.SetResult(Guid.Empty);
            
            return tcs.Task;
        }

        public void ClearAsyncOutbox(int amountToClear = 100, int minimumAge = 5000, bool useBulk = false)
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
}
