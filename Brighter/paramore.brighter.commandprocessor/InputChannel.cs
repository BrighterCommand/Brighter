// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class InputChannel.
    /// An <see cref="IAmAChannel"/> for reading messages from a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    /// and acknowledging receipt of those messages
    /// </summary>
    public class InputChannel : IAmAnInputChannel
    {
        private readonly string _queueName;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly ConcurrentQueue<Message> _queue = new ConcurrentQueue<Message>();
        private readonly bool _messageConsumerSupportsDelay;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputChannel"/> class.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="messageConsumer">The messageConsumer.</param>
        public InputChannel(string queueName, IAmAMessageConsumer messageConsumer)
        {
            _queueName = queueName;
            _messageConsumer = messageConsumer;
            _messageConsumerSupportsDelay = _messageConsumer is IAmAMessageConsumerSupportingDelay && (_messageConsumer as IAmAMessageGatewaySupportingDelay).DelaySupported;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public ChannelName Name { get { return new ChannelName(_queueName); } }

        /// <summary>
        /// Receives the specified timeout in milliseconds.
        /// </summary>
        /// <param name="timeoutinMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(int timeoutinMilliseconds)
        {
            Message message;
            if (!_queue.TryDequeue(out message))
            {
                message = _messageConsumer.Receive(timeoutinMilliseconds);
            }
            return message;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            _messageConsumer.Acknowledge(message);
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
        {
            _messageConsumer.Reject(message, true);
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            _queue.Enqueue(MessageFactory.CreateQuitMessage());
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        public void Requeue(Message message, int delayMilliseconds = 0)
        {
            if (delayMilliseconds > 0 && !_messageConsumerSupportsDelay)
                Task.Delay(delayMilliseconds).Wait();

            if (_messageConsumerSupportsDelay)
                (_messageConsumer as IAmAMessageConsumerSupportingDelay).Requeue(message, delayMilliseconds);
            else
                _messageConsumer.Requeue(message);

        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        /// <value>The length.</value>
        /// <exception cref="System.NotImplementedException"></exception>
        public int Length
        {
            get { return _queue.Count; }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~InputChannel()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _messageConsumer.Dispose();
            }
        }
    }
}
