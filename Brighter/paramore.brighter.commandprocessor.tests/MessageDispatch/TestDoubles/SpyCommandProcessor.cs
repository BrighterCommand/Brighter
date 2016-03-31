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
using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.actions;

namespace paramore.commandprocessor.tests.MessageDispatch.TestDoubles
{
    enum CommandType
    {
        Send,
        Publish,
        Post,
        SendAsync,
        PublishAsync,
        PostAsync
    }

    internal class SpyCommandProcessor : IAmACommandProcessor
    {
        private readonly Queue<IRequest> _requests = new Queue<IRequest>();
        private readonly IList<CommandType> _commands = new List<CommandType>();
        public IList<CommandType> Commands { get { return _commands; } }

        public virtual void Send<T>(T command) where T : class, IRequest
        {
            _requests.Enqueue(command);
            _commands.Add(CommandType.Send);
        }

        public virtual async Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken? ct = null) where T : class, IRequest
        {
            _requests.Enqueue(command);
            _commands.Add(CommandType.SendAsync);
            await Task.Delay(0).ConfigureAwait(false);
        }

        public virtual void Publish<T>(T @event) where T : class, IRequest
        {
            _requests.Enqueue(@event);
            _commands.Add(CommandType.Publish);
        }

        public virtual async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken? ct = null) where T : class, IRequest
        {
            _requests.Enqueue(@event);
            _commands.Add(CommandType.PublishAsync);
            await Task.Delay(0).ConfigureAwait(false);
        }

        public virtual void Post<T>(T request) where T : class, IRequest
        {
            _requests.Enqueue(request);
            _commands.Add(CommandType.Post);
        }

        /// <summary>
        /// Posts the specified request with async/await support.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public virtual async Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken? ct = null) where T : class, IRequest
        {
            _requests.Enqueue(request);
            _commands.Add(CommandType.PostAsync);
            await Task.Delay(0);
        }

        public virtual T Observe<T>() where T : class, IRequest
        {
            return (T) _requests.Dequeue();
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