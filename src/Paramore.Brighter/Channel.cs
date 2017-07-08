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

namespace Paramore.Brighter
{
    /// <summary>
    ///     Class Channel.
    ///     An <see cref="IAmAChannel" /> for reading messages from a
    ///     <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    ///     and acknowledging receipt of those messages
    /// </summary>
    public class Channel : IAmAChannel
    {
        private readonly IAmAMessageConsumer _messageConsumer;

        /// <summary>
        ///     Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public ChannelName Name { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Channel" /> class.
        /// </summary>
        /// <param name="channelName">Name of the queue.</param>
        /// <param name="messageConsumer">The messageConsumer.</param>
        public Channel(ChannelName channelName, IAmAMessageConsumer messageConsumer)
        {
            Name = channelName;
            _messageConsumer = messageConsumer;
        }

        /// <summary>
        ///     Receives the specified timeout in milliseconds.
        /// </summary>
        /// <param name="timeoutinMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public async Task<Message> ReceiveAsync(int timeoutinMilliseconds)
        {
            return await _messageConsumer.ReceiveAsync(timeoutinMilliseconds);
        }

        /// <summary>
        ///     Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public async Task AcknowledgeAsync(Message message)
        {
            await _messageConsumer.AcknowledgeAsync(message);
        }

        /// <summary>
        ///     Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public async Task RejectAsync(Message message)
        {
            await _messageConsumer.RejectAsync(message, true);
        }

        /// <summary>
        ///     Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">How long should we delay before requeueing</param>
        public async Task RequeueAsync(Message message, int delayMilliseconds = 0)
        {
            var messageConsumerSupportingDelay = _messageConsumer as IAmAMessageConsumerSupportingDelay;
            if (messageConsumerSupportingDelay != null && messageConsumerSupportingDelay.DelaySupported)
            {
                await messageConsumerSupportingDelay.RequeueAsync(message, delayMilliseconds);
            }
            else
            {
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds);
                }

                await _messageConsumer.RequeueAsync(message);
            }
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
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