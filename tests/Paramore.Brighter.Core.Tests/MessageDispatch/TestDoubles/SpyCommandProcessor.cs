#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    enum CommandType
    {
        Send,
        Publish,
        Post,
        SendAsync,
        PublishAsync,
        PostAsync,
        Deposit,
        DepositAsync,
        Clear,
        ClearAsync,
        Call,
        Scheduler,
        SchedulerAsync
    }

    public class ClearParams
    {
        public int AmountToClear;
        public TimeSpan MinimumAge;
        public Dictionary<string, object> Args;
    }

    internal class SpyCommandProcessor : IAmACommandProcessor
    {
        private readonly Queue<IRequest> _requests = new Queue<IRequest>();
        private readonly Queue<IRequest> _scheduler = new Queue<IRequest>();
        private readonly Dictionary<string, IRequest> _postBox = new();

        public IList<CommandType> Commands { get; } = new List<CommandType>();
        public List<ClearParams> ClearParamsList { get; } = new();

        public virtual void Send<T>(T command, RequestContext requestContext = null) where T : class, IRequest
        {
            _requests.Enqueue(command);
            Commands.Add(CommandType.Send);
        }

        public string Send<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(command);
            return Guid.NewGuid().ToString();
        }

        public string Send<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(command);
            return Guid.NewGuid().ToString();
        }

        public virtual async Task SendAsync<TRequest>(
            TRequest command, 
            RequestContext requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) 
            where TRequest : class, IRequest
        {
            _requests.Enqueue(command);
            Commands.Add(CommandType.SendAsync);
            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public Task<string> SendAsync<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(command);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<string> SendAsync<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(command);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public virtual void Publish<TRequest>(TRequest @event, RequestContext requestContext = null) where TRequest : class, IRequest
        {
            _requests.Enqueue(@event);
            Commands.Add(CommandType.Publish);
        }

        public string Publish<TRequest>(DateTimeOffset at, TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(@event);
            return Guid.NewGuid().ToString();
        }

        public string Publish<TRequest>(TimeSpan delay, TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(@event);
            return Guid.NewGuid().ToString();
        }

        public virtual async Task PublishAsync<TRequest>(
            TRequest @event, 
            RequestContext requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) 
            where TRequest : class, IRequest
        {
            _requests.Enqueue(@event);
            Commands.Add(CommandType.PublishAsync);

            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public Task<string> PublishAsync<TRequest>(DateTimeOffset at, TRequest @event, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(@event);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<string> PublishAsync<TRequest>(TimeSpan delay, TRequest @event, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(@event);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public string SchedulerPost<TRequest>(TRequest request, TimeSpan delay, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(request);
            Commands.Add(CommandType.Scheduler);
            return Guid.NewGuid().ToString();
        }

        public string SchedulerPost<TRequest>(TRequest request, DateTimeOffset at, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(request);
            Commands.Add(CommandType.Scheduler);
            return Guid.NewGuid().ToString();
        }

        public Task<string> SchedulerPostAsync<TRequest>(TRequest request, TimeSpan delay, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(request);
            Commands.Add(CommandType.SchedulerAsync);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<string> SchedulerPostAsync<TRequest>(TRequest request, DateTimeOffset at, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            _scheduler.Enqueue(request);
            Commands.Add(CommandType.SchedulerAsync);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public virtual void Post<TRequest>(TRequest request, RequestContext requestContext = null, Dictionary<string, object> args = null) 
            where TRequest : class, IRequest
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.Post);
        }

        public string Post<TRequest>(DateTimeOffset at, TRequest request, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null) where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public string Post<TRequest>(TimeSpan delay, TRequest request, RequestContext? requestContext = null, Dictionary<string, object>? args = null) where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public virtual async Task PostAsync<TRequest>(
            TRequest request, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) 
            where TRequest : class, IRequest
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.PostAsync);

            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public Task<string> PostAsync<TRequest>(DateTimeOffset at, TRequest request, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public Task<string> PostAsync<TRequest>(TimeSpan delay, TRequest request, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            throw new NotImplementedException();
        }

        public Id DepositPost<TRequest>(
            TRequest request, 
            RequestContext requestContext = null,
            Dictionary<string, object> args = null) 
            where TRequest : class, IRequest
        {
            _postBox.Add(request.Id, request);
            return request.Id;
        }

        public Id DepositPost<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction> provider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            string? batchId = null) 
            where TRequest : class, IRequest
        {
            return DepositPost(request);
        }


        public Id[] DepositPost<TRequest>(
            IEnumerable<TRequest> request, 
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null) 
            where TRequest : class, IRequest
        {
            var ids = new List<Id>();
            foreach (TRequest r in request)
            {
                ids.Add(DepositPost(r));
            }

            return ids.ToArray();
        }

        public Id[] DepositPost<TRequest, TTransaction>(
            IEnumerable<TRequest> request, 
            IAmABoxTransactionProvider<TTransaction> provider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null)
            where TRequest : class, IRequest
        {
            return DepositPost(request);
        }

        public async Task<Id> DepositPostAsync<TRequest>(
            TRequest request,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) 
            where TRequest : class, IRequest
        {
            _postBox.Add(request.Id, request);

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(request.Id);
            return await tcs.Task;
        }

        public async Task<Id> DepositPostAsync<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction> provider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default,
            string? batchId = null)
            where TRequest : class, IRequest
        {
            _postBox.Add(request.Id, request);

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(request.Id);
            return await tcs.Task;
        }

        public async Task<Id[]> DepositPostAsync<TRequest>(
            IEnumerable<TRequest> requests,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) 
            where TRequest : class, IRequest
        {
            var ids = new List<Id>();
            foreach (TRequest r in requests)
            {
                ids.Add(await DepositPostAsync(r, cancellationToken: cancellationToken));
            }

            return ids.ToArray();
        }

        public async Task<Id[]> DepositPostAsync<TRequest, TTransaction>(
            IEnumerable<TRequest> requests,
            IAmABoxTransactionProvider<TTransaction> provider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            return await DepositPostAsync(requests, cancellationToken: cancellationToken);
        }

        public void ClearOutbox(Id[] posts, RequestContext? requestContext = null, Dictionary<string, object>? args = null)
        {
            foreach (var messageId in posts)
            {
                if (_postBox.TryGetValue(messageId, out IRequest? request))
                {
                    _requests.Enqueue(request);
                }
            }
        }
 
        public async Task ClearOutboxAsync(
            IEnumerable<Id> posts, 
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            ClearOutbox(posts.ToArray());

            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public void ClearOutstandingFromOutbox(
            int amountToClear = 100, 
            TimeSpan? minimumAge = null, 
            bool useBulk = false,
            RequestContext requestContext = null,
            Dictionary<string, object> args = null,
            bool runOnBackgroundThread = true)
        {
            Commands.Add(CommandType.Clear);
            ClearParamsList.Add(new ClearParams
            {
                AmountToClear = amountToClear, MinimumAge = minimumAge ?? TimeSpan.FromMilliseconds(5000), Args = args
            });
        }

        public TResponse Call<T, TResponse>(
            T request, 
            RequestContext requestContext = null,
            TimeSpan? timeOut = null)
            where T : class, ICall where TResponse : class, IResponse
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.Call);
            return default(TResponse);
        }

        public virtual T Observe<T>() where T : class, IRequest
        {
            return (T)_requests.Dequeue();
        }

        public bool ContainsCommand(CommandType commandType)
        {
            return Commands.Any(ct => ct == commandType);
        }
    }

    internal sealed class SpyRequeueCommandProcessor : SpyCommandProcessor
    {
        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyRequeueCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public override void Send<T>(T command, RequestContext requestContext = null)
        {
            base.Send(command, requestContext);
            SendCount++;
            throw new DeferMessageAction();
        }

        public override void Publish<T>(T @event, RequestContext requestContext = null)
        {
            base.Publish(@event, requestContext);
            PublishCount++;

            var exceptions = new List<Exception> { new DeferMessageAction() };

            throw new AggregateException(
                "Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

        public override async Task SendAsync<T>(
            T command, 
            RequestContext requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            )
        {
            await base.SendAsync(command, requestContext, continueOnCapturedContext, cancellationToken);
            SendCount++;
            throw new DeferMessageAction();
        }

        public override async Task PublishAsync<T>(
            T @event, 
            RequestContext requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            )
        {
            await base.PublishAsync(@event, requestContext, continueOnCapturedContext, cancellationToken);
            PublishCount++;

            var exceptions = new List<Exception> { new DeferMessageAction() };

            throw new AggregateException(
                "Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }
    }

    internal sealed class SpyExceptionCommandProcessor : SpyCommandProcessor
    {
        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyExceptionCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public override void Send<T>(T command, RequestContext requestContext = null)
        {
            base.Send(command, requestContext);
            SendCount++;
            throw new Exception();
        }

        public override void Publish<T>(T @event, RequestContext requestContext = null)
        {
            base.Publish(@event, requestContext);
            PublishCount++;

            var exceptions = new List<Exception> { new Exception() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }
        public override async Task SendAsync<T>(T command, RequestContext requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        {
            await base.SendAsync(command, requestContext, continueOnCapturedContext, cancellationToken);
            SendCount++;
            throw new Exception();
        }

        public override async Task PublishAsync<T>(T @event, RequestContext requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        {
            await base.PublishAsync(@event, requestContext, continueOnCapturedContext, cancellationToken);
            PublishCount++;

            var exceptions = new List<Exception> { new Exception() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

    }
}
