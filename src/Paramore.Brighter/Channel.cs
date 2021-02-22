﻿#region Licence

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
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter
{
    /// <summary>
    ///   Class Channel.
    ///   An <see cref="IAmAChannel" /> for reading messages from a
    ///   <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    ///   and acknowledging receipt of those messages
    /// </summary>
    public class Channel : IAmAChannel
    {
        private readonly string _channelName;
        private readonly IAmAMessageConsumer _messageConsumer;
        private ConcurrentQueue<Message> _queue = new ConcurrentQueue<Message>();
        private readonly int _maxQueueLength;
        private static readonly Message s_NoneMessage = new Message();

        /// <summary>
        ///     Initializes a new instance of the <see cref="Channel" /> class.
        /// </summary>
        /// <param name="channelName">Name of the queue.</param>
        /// <param name="messageConsumer">The messageConsumer.</param>
        /// <param name="maxQueueLength">What is the maximum buffer size we will accelt</param>
        public Channel(string channelName, IAmAMessageConsumer messageConsumer, int maxQueueLength = 1)
        {
            _channelName = channelName;
            _messageConsumer = messageConsumer;

            if (maxQueueLength < 1 || maxQueueLength > 10)
            {
                throw new ConfigurationException(
                    "The channel buffer must have one item, and cannot have more than 10");
            }
            
            _maxQueueLength = maxQueueLength + 1; //+1 so you can fit the quit message on the queue as well 
        }

        /// <summary>
        ///  Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            _messageConsumer.Acknowledge(message);
        }

        /// <summary>
        /// Inserts messages into the channel for consumption by the message pump.
        /// Note that there is an upperbound to what we can enqueue, although we always allow enqueing a quit
        /// message. We will always try to clear the channel, when closing, as the stop message will get inserted
        /// after the queue
        /// </summary>
        /// <param name="messages">The messages to insert into the channel</param>
        public void Enqueue(params Message[] messages)
        {
            var currentLength = _queue.Count;
            var messagesToAdd = messages.Length;
            var newLength = currentLength + messagesToAdd;

            if (newLength > _maxQueueLength)
            {
                throw new InvalidOperationException($"You cannot enqueue {newLength} items which larger than the buffer length {_maxQueueLength}"); 
            }
            
            messages.Each((message) => _queue.Enqueue(message));
        }

       /// <summary>
        ///   Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public ChannelName Name => new ChannelName(_channelName);

       /// <summary>
        /// Purges the queue
        /// </summary>
        public void Purge()
        {
            _messageConsumer.Purge();
            _queue = new ConcurrentQueue<Message>();
        }

        /// <summary>
        ///  Receives the specified timeout in milliseconds.
        /// </summary>
        /// <param name="timeoutinMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(int timeoutinMilliseconds)
        {
            if (!_queue.TryDequeue(out Message message))
            {
                Enqueue(_messageConsumer.Receive(timeoutinMilliseconds));
                if (!_queue.TryDequeue(out message))
                {
                    message = s_NoneMessage; //Will be MT_NONE
                }
            }

            return message;
        }

        /// <summary>
        ///  Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
        {
            _messageConsumer.Reject(message);
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">How long should we delay before requeueing</param>
        public void Requeue(Message message, int delayMilliseconds = 0)
        {
            _messageConsumer.Requeue(message, delayMilliseconds);
        }

        /// <summary>
        ///  Stops this instance.
        /// </summary>
        public void Stop()
        {
            _queue.Enqueue(MessageFactory.CreateQuitMessage());
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _messageConsumer.Dispose();
            }
        }

        ~Channel()
        {
            Dispose(false);
        }
    }
}
