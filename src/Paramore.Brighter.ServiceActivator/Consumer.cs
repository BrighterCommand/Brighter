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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Enum ConsumerState
    /// Identifies the state of a consumer: Open indicates that a consumer is reading messages from a channel, Shut means that a consumer is not reading messages
    /// from a channel
    /// </summary>
    public enum ConsumerState
    {
        /// <summary>
        /// The consumer is shut and won't read messages from the task queue
        /// </summary>
        Shut = 0,
        /// <summary>
        /// The consumer is open and will receive messages from the task queue
        /// </summary>
        Open = 1
    }

    /// <summary>
    /// Class Consumer.
    /// Manages the message pump used to read messages for a channel. Creation establishes the message pump for a given connection and channel. Open runs the
    /// message pump, which begins consuming messages from the channel; it returns the TPL Task used to run the message pump thread so that it can be
    /// Waited on by callers. Shut closes the message pump.
    /// 
    /// </summary>
    public class Consumer : IAmAConsumer
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public ConnectionName Name { get; }

        /// <summary>
        /// Gets the performer.
        /// </summary>
        /// <value>The performer.</value>
        public IAmAPerformer Performer { get; }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public ConsumerState State { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Consumer"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="channel">The channel.</param>
        /// <param name="messagePump">The message pump.</param>
        public Consumer(ConnectionName name, IAmAChannel channel, IAmAMessagePump messagePump)
        {
            Name = name;
            Performer = new Performer(channel, messagePump);
            State = ConsumerState.Shut;
        }

        private Task _job;

        /// <summary>
        /// Opens the task queue and begin receiving messages.
        /// </summary>
        public Task Open(CancellationToken cancellationToken)
        {
            if (State == ConsumerState.Shut)
            {
                State = ConsumerState.Open;
                _job = Performer.Run(cancellationToken);
            }

            return _job ?? Task.CompletedTask;
        }

        /// <summary>
        /// Shuts the task, which will not receive messages.
        /// </summary>
        public void Shut()
        {
            if (State == ConsumerState.Open)
            {
                Performer.Stop();
                State = ConsumerState.Shut;
            }
        }

        /// <summary>
        /// Shuts the consumer when the consumer is being released
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Performer.Dispose();
            }
        }
    }
}