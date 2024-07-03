using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    /// <summary>
    /// Used for Sweeper tests, will not clear the message!!!
    /// </summary>
    public class FakeCommandProcessor(TimeProvider timeProvider) : IAmACommandProcessor
    {
        /// <summary>
        /// Message has been dispatched (to the bus or directly to the handler)
        /// </summary>
        public readonly ConcurrentDictionary<string, IRequest> Dispatched = new ConcurrentDictionary<string, IRequest>();
        /// <summary>
        /// Message has been placed into the outbox but not sent or dispatched
        /// </summary>
        public readonly ConcurrentQueue<DepositedMessage> Deposited = new ConcurrentQueue<DepositedMessage>();
        
        public void Send<T>(T command, RequestContext requestContext = null) where T : class, IRequest
        {
            Dispatched.TryAdd(command.Id, command);
        }

        public Task SendAsync<T>(T command, RequestContext requestContext = null, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if (cancellationToken.IsCancellationRequested)
                tcs.SetCanceled(cancellationToken);
            
            Send(command);

            return tcs.Task;
        }

        public void Publish<T>(T @event, RequestContext requestContext = null) where T : class, IRequest
        {
            Dispatched.TryAdd(@event.Id, @event);
        }

        public Task PublishAsync<T>(T @event, RequestContext requestContext = null, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default) where T : class, IRequest
        {
              var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
              
              if (cancellationToken.IsCancellationRequested)
                  tcs.SetCanceled(cancellationToken);
         
              Publish(@event);

              return tcs.Task;
        }

        public void Post<T>(T request, RequestContext requestContext = null, Dictionary<string, object> args = null) where T : class, IRequest
        {
            var post = DepositPost(request, (IAmABoxTransactionProvider<Transaction>)null, requestContext, args);
            ClearOutbox([post], requestContext, args);
        }
        
        public Task PostAsync<T>(T request, RequestContext requestContext = null, Dictionary<string, object> args = null, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default) where T : class, IRequest
        {
              var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
              
              if (cancellationToken.IsCancellationRequested)
                  tcs.SetCanceled(cancellationToken);
              
              Post(request);

              return tcs.Task;
        }
        
        public string DepositPost<T>(T request, RequestContext requestContext = null, Dictionary<string, object> args = null) where T : class, IRequest
        {
            Deposited.Enqueue(new DepositedMessage(request, timeProvider));
            return request.Id;
        }
        
        public string DepositPost<T, TTransaction>(
            T request, 
            IAmABoxTransactionProvider<TTransaction> provider,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            string batchId = null) where T : class, IRequest
        {
            return DepositPost(request);
        }

        public string[] DepositPost<T>(IEnumerable<T> request, RequestContext requestContext = null, Dictionary<string, object> args = null) where T : class, IRequest
        {
            var ids = new List<string>();
            foreach (T r in request)
            {
                ids.Add(DepositPost(r, (IAmABoxTransactionProvider<Transaction>)null, requestContext, args));
            }

            return ids.ToArray();
        }
        
        public string[] DepositPost<T, TTransaction>(
            IEnumerable<T> request, 
            IAmABoxTransactionProvider<TTransaction> provider, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null) 
            where T : class, IRequest
        {
            return DepositPost(request, requestContext, args);
        }

        public Task<string> DepositPostAsync<T>(
            T request,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false, 
            CancellationToken cancellationToken = default) 
            where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled(cancellationToken);
            
            DepositPost(request, (IAmABoxTransactionProvider<Transaction>)null, requestContext, args);
            
            tcs.SetResult(request.Id);

            return tcs.Task;

        }
        
        public Task<string> DepositPostAsync<T, TTransaction>(
            T request,
            IAmABoxTransactionProvider<TTransaction> provider, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false, 
            CancellationToken cancellationToken = default,
            string batchId = null) 
            where T : class, IRequest
        {
            return DepositPostAsync(request, requestContext, args, continueOnCapturedContext, cancellationToken);
        }

        public async Task<string[]> DepositPostAsync<T>(
            IEnumerable<T> requests,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            var ids = new List<string>();
            foreach (T r in requests)
            {
                ids.Add(await DepositPostAsync(r, requestContext, cancellationToken: cancellationToken));
            }

            return ids.ToArray();
        }
        
        public async Task<string[]> DepositPostAsync<T, TTransaction>(
            IEnumerable<T> requests, 
            IAmABoxTransactionProvider<TTransaction> provider, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            return await DepositPostAsync(requests, requestContext, args, continueOnCapturedContext, cancellationToken);
        }

        public void ClearOutbox(string[] posts, RequestContext requestContext = null, Dictionary<string, object> args = null)
        {
            foreach (var post in posts)
            {
                var msg = Deposited.First(m => m.Request.Id == post);
                Dispatched.TryAdd(post, msg.Request);
            }
        }

        public Task ClearOutboxAsync(
            IEnumerable<string> posts, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null, 
            bool continueOnCapturedContext = false, 
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled(cancellationToken);

            ClearOutbox(posts.ToArray(), requestContext);

            tcs.SetResult(string.Empty);
            
            return tcs.Task;
        }

        public void ClearOutstandingFromOutbox(
            int amountToClear = 100, 
            int minimumAge = 5000, 
            bool useBulk = false, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null)
        {
            var depositedMessages = Deposited.Where(m =>
                    m.EnqueuedTime < timeProvider.GetUtcNow().DateTime.AddMilliseconds(-1 * minimumAge) &&
                    !Dispatched.ContainsKey(m.Request.Id))
                .Take(amountToClear)
                .Select(m => m.Request.Id)
                .ToArray();

            ClearOutbox(depositedMessages, requestContext);
        }

        public TResponse Call<T, TResponse>(T request, RequestContext requestContext = null, int timeOutInMilliseconds = 500) where T : class, ICall where TResponse : class, IResponse
        {
            return null;
        }
    }

    public class DepositedMessage
    {
        public IRequest Request { get; }
        public DateTime EnqueuedTime { get; }

        public DepositedMessage(IRequest request, TimeProvider timeProvider)
        {
            Request = request;
            EnqueuedTime = timeProvider.GetUtcNow().DateTime;
        }
    }
}
