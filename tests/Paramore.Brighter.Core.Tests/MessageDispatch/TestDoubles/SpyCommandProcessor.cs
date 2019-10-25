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

    internal class SpyCommandProcessor : IAmACommandProcessor
    {
        private readonly Queue<IRequest> _requests = new Queue<IRequest>();
        private readonly Dictionary<Guid, IRequest> _postBox = new Dictionary<Guid, IRequest>();

        public IList<CommandType> Commands { get; } = new List<CommandType>();

        public virtual void Send<T>(T command) where T : class, IRequest
        {
            _requests.Enqueue(command);
            Commands.Add(CommandType.Send);
        }

        public virtual async Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            _requests.Enqueue(command);
            Commands.Add(CommandType.SendAsync);
            await Task.Delay(0).ConfigureAwait(false);
        }

        public virtual void Publish<T>(T @event) where T : class, IRequest
        {
            _requests.Enqueue(@event);
            Commands.Add(CommandType.Publish);
        }

        public virtual async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            _requests.Enqueue(@event);
            Commands.Add(CommandType.PublishAsync);
            await Task.Delay(0).ConfigureAwait(false);
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
        public virtual async Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            _requests.Enqueue(request);
            Commands.Add(CommandType.PostAsync);
            await Task.Delay(0);
        }

        public Guid DepositPost<T>(T request) where T : class, IRequest
        {
            _postBox.Add(request.Id, request);
            return request.Id;
        }

        public async Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var tcs = new TaskCompletionSource<Guid>();
            _postBox.Add(request.Id, request);
            await Task.Delay(0, cancellationToken);
            tcs.SetResult(request.Id);
            return tcs.Task.Result;
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

        public async Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ClearOutbox(posts.ToArray());
            await Task.Delay(0);
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
    }
}
