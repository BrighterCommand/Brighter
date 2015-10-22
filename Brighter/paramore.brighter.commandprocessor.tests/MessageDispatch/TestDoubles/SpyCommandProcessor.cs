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

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.actions;

namespace paramore.commandprocessor.tests.MessageDispatch.TestDoubles
{
    internal class SpyCommandProcessor : IAmACommandProcessor
    {
        readonly Queue<IRequest> _requests = new Queue<IRequest>();

        public bool SendHappened { get; private set; }
        public bool PublishHappened { get; private set; }
        public bool PostHappened { get; set; }
        public bool RepostHappened { get; set; }


        /// <summary>
        /// Sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        public virtual void Send<T>(T command) where T : class, ICommand
        {
            _requests.Enqueue(command);
            SendHappened = true;
        }

        /// <summary>
        /// Publishes the specified event. Throws an aggregate exception on failure of a pipeline but executes remaining
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        public virtual void Publish<T>(T @event) where T : class, IEvent
        {
            _requests.Enqueue(@event);
            PublishHappened = true;
        }

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        public virtual void Post<T>(T request) where T : class, IRequest
        {
            _requests.Enqueue(request);
            PostHappened = true;
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

            var exceptions = new List<Exception>();
            exceptions.Add(new DeferMessageAction());
            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);


            throw new DeferMessageAction();
        }
    }
}