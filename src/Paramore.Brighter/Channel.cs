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
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter
{
    /// <summary>
    ///   Class Channel.
    ///   An <see cref="IAmAChannelSync" /> for reading messages from a
    ///   <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    ///   and acknowledging receipt of those messages
    /// </summary>
    public class Channel : IAmAChannelSync
    {
        private readonly IAmAMessageConsumerSync _messageConsumer;
        private ConcurrentQueue<Message> _queue = new();
        private readonly int _maxQueueLength;
        private static readonly Message s_noneMessage = new();
        
        /// <summary>
        /// The name of a channel is its identifier
        /// See Topic for the broker routing key
        /// May be used for the queue name, if known, on middleware that supports named queues
        /// </summary>
        /// <value>The channel identifier</value>
        public ChannelName Name { get; }
        
        /// <summary>
        /// The topic that this channel is for (how a broker routes to it)
        /// </summary>
        /// <value>The topic on the broker</value>
        public RoutingKey RoutingKey { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Channel" /> class.
        /// </summary>
        /// <param name="channelName">Name of the queue.</param>
        /// <param name="routingKey"></param>
        /// <param name="messageConsumer">The messageConsumer.</param>
        /// <param name="maxQueueLength">What is the maximum buffer size we will accept</param>
        public Channel(
            ChannelName channelName, 
            RoutingKey routingKey, 
            IAmAMessageConsumerSync messageConsumer,
            int maxQueueLength = 1
            )
        {
            Name = channelName;
            RoutingKey = routingKey;
            _messageConsumer = messageConsumer;

            if (maxQueueLength < 1 || maxQueueLength > 10)
            {
                throw new ConfigurationException(
                    "The channel buffer must have one item, and cannot have more than 10");
            }
            
            _maxQueueLength = maxQueueLength + 1; //+1 so you can fit the quit message on the queue as well 
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <remarks>
        /// When a message is acknowledged, another consumer should not process it
        /// </remarks>
        /// <param name="message">The<see cref="Message"/> to reject</param>
        public virtual void Acknowledge(Message message)
        {
            _messageConsumer.Acknowledge(message);
        }

        /// <summary>
        /// Nacks the specified message, releasing it back to the transport for redelivery.
        /// </summary>
        /// <param name="message">The <see cref="Message"/> to nack</param>
        public virtual void Nack(Message message)
        {
            _messageConsumer.Nack(message);
        }

        /// <summary>
        /// Inserts messages into the channel for consumption by the message pump.
        /// Note that there is an upperbound to what we can enqueue, although we always allow enqueueing a quit
        /// message. We will always try to clear the channel, when closing, as the stop message will get inserted
        /// after the queue
        /// </summary>
        /// <param name="messages">The messages to insert into the channel</param>
        public virtual void Enqueue(params Message[] messages)
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
        /// Purges the queue
        /// </summary>
        public virtual void Purge()
        {
            _messageConsumer.Purge();
            _queue = new ConcurrentQueue<Message>();
        }

        /// <summary>
        ///  The timeout to recieve wihtin.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan"/>"> timeout. If null default to 1s</param>
        /// <returns>Message.</returns>
        public virtual Message Receive(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(1);
            
            if (!_queue.TryDequeue(out Message? message))
            {
                Enqueue(_messageConsumer.Receive(timeout));
                if (!_queue.TryDequeue(out message))
                {
                    message = s_noneMessage; //Will be MT_NONE
                }
            }

            return message;
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// When a message is rejected, another consumer should not process it. If there is a dead letter, or invalid
        /// message channel, the message should be forwardedn to it
        /// <param name="message">The <see cref="Message"/> to reject</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explaines why we rejected the mes
        public virtual bool Reject(Message message, MessageRejectionReason? reason)
            => _messageConsumer.Reject(message, reason);

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="timeOut">How long should we delay before requeueing</param>
        /// <returns>True if the message was re-queued false otherwise </returns>
        public virtual bool Requeue(Message message, TimeSpan? timeOut = null)
        {
            return _messageConsumer.Requeue(message, timeOut);
        }

        /// <summary>
        ///  Stops this instance.
        /// </summary>
        public virtual void Stop(RoutingKey topic)
        {
            _queue.Enqueue(MessageFactory.CreateQuitMessage(topic));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
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
