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
    /// Class OutputChannel.
    /// An <see cref="IAmAChannel"/> for reading messages from a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    /// and acknowledging receipt of those messages
    /// </summary>
    public class OutputChannel : IAmAnOutputChannel
    {
        private readonly IAmAMessageProducer _messageProducer;
        private readonly ConcurrentQueue<Message> _queue = new ConcurrentQueue<Message>();
        private readonly bool _messageProducerSupportsDelay;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputChannel"/> class.
        /// </summary>
        /// <param name="messageConsumer">The messageConsumer.</param>
        public OutputChannel(IAmAMessageProducer messageProducer)
        {
            _messageProducer = messageProducer;
            _messageProducerSupportsDelay = _messageProducer is IAmAMessageProducerSupportingDelay && (_messageProducer as IAmAMessageGatewaySupportingDelay).DelaySupported;
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Send(Message message, int delayMilliseconds = 0)
        {
            if (delayMilliseconds > 0 && !_messageProducerSupportsDelay)
                Task.Delay(delayMilliseconds).Wait();

            if (_messageProducerSupportsDelay)
                (_messageProducer as IAmAMessageProducerSupportingDelay).SendWithDelay(message, delayMilliseconds);
            else
                _messageProducer.Send(message);
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

        ~OutputChannel()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _messageProducer.Dispose();
            }
        }
    }
}
