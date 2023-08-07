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
        Call
    }

    public class ClearParams
    {
        public int AmountToClear;
        public int MinimumAge;
        public Dictionary<string, object> Args;
    }

    internal class SpyCommandProcessor : Paramore.Brighter.IAmACommandProcessor
    {
        private readonly Queue<IRequest> _requests = new Queue<IRequest>();
        private readonly Dictionary<Guid, IRequest> _postBox = new Dictionary<Guid, IRequest>();

        public IList<CommandType> Commands { get; } = new List<CommandType>();
        public List<ClearParams> ClearParamsList { get; } = new List<ClearParams>();

        public virtual void Send<T>(T command) where T : class, IRequest
        {
            _requests.Enqueue(command);
            Commands.Add(CommandType.Send);
        }

        public virtual async Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default) where T : class, IRequest
        {
            _requests.Enqueue(command);
            Commands.Add(CommandType.SendAsync);
            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public virtual void Publish<T>(T @event) where T : class, IRequest
        {
            _requests.Enqueue(@event);
            Commands.Add(CommandType.Publish);
        }

        public virtual async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default) where T : class, IRequest
        {
            _requests.Enqueue(@event);
            Commands.Add(CommandType.PublishAsync);

            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public virtual void Post<T>(T request) where T : class, IRequest
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.Post);
        }

        /// <summary>
        /// Posts the specified request with async/await support.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public virtual async Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default) where T : class, IRequest
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.PostAsync);

            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public Guid DepositPost<T>(T request) where T : class, IRequest
        {
            _postBox.Add(request.Id, request);
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

        public async Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where T : class, IRequest
        {
            _postBox.Add(request.Id, request);

            var tcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(request.Id);
            return await tcs.Task;
        }

        public async Task<Guid[]> DepositPostAsync<T>(IEnumerable<T> requests, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where T : class, IRequest
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
            foreach (var messageId in posts)
            {
                if (_postBox.TryGetValue(messageId, out IRequest request))
                {
                    _requests.Enqueue(request);
                }
            }
        }

        public void ClearOutbox(int amountToClear = 100, int minimumAge = 5000, Dictionary<string, object> args = null)
        {
            Commands.Add(CommandType.Clear);
            ClearParamsList.Add(new ClearParams { AmountToClear = amountToClear, MinimumAge = minimumAge, Args = args });
        }

        public async Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
        {
            ClearOutbox(posts.ToArray());

            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            completionSource.SetResult(null);
            await completionSource.Task;
        }

        public void ClearAsyncOutbox(int amountToClear = 100, int minimumAge = 5000, bool useBulk = false, Dictionary<string, object> args = null)
        {
            Commands.Add(CommandType.Clear);
            ClearParamsList.Add(new ClearParams { AmountToClear = amountToClear, MinimumAge = minimumAge, Args = args });
        }

        public Task BulkClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
        {
            return ClearOutboxAsync(posts, continueOnCapturedContext, cancellationToken);
        }

        public TResponse Call<T, TResponse>(T request, int timeOutInMilliseconds) where T : class, ICall where TResponse : class, IResponse
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.Call);
            return default (TResponse);
        }

        public virtual T Observe<T>() where T : class, IRequest
        {
            return (T) _requests.Dequeue();
        }

        public bool ContainsCommand(CommandType commandType)
        {
            return Commands.Any(ct => ct == commandType);
        }
    }

    internal class SpyRequeueCommandProcessor : SpyCommandProcessor
    {
        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyRequeueCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public override void Send<T>(T command)
        {
            base.Send(command);
            SendCount++;
            throw new DeferMessageAction();
        }

        public override void Publish<T>(T @event)
        {
            base.Publish(@event);
            PublishCount++;

            var exceptions = new List<Exception> {new DeferMessageAction()};

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }
        public override async Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default)
        {
            await base.SendAsync(command, continueOnCapturedContext, cancellationToken);
            SendCount++;
            throw new DeferMessageAction();
        }

        public override async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default)
        {
            await base.PublishAsync(@event, continueOnCapturedContext, cancellationToken);
            PublishCount++;

            var exceptions = new List<Exception> { new DeferMessageAction() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

    }

    internal class SpyExceptionCommandProcessor : SpyCommandProcessor
    {
        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyExceptionCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public override void Send<T>(T command)
        {
            base.Send(command);
            SendCount++;
            throw new Exception();
        }

        public override void Publish<T>(T @event)
        {
            base.Publish(@event);
            PublishCount++;

            var exceptions = new List<Exception> { new Exception() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }
        public override async Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default)
        {
            await base.SendAsync(command, continueOnCapturedContext, cancellationToken);
            SendCount++;
            throw new Exception();
        }

        public override async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default)
        {
            await base.PublishAsync(@event, continueOnCapturedContext, cancellationToken);
            PublishCount++;

            var exceptions = new List<Exception> { new Exception() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

    }
}
