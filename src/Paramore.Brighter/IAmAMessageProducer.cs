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

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmASendMessageGateway
    /// Abstracts away the Application Layer used to push messages onto a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a>
    /// Usually clients do not need to instantiate as access is via an <see cref="IAmAChannel"/> derived class.
    /// We provide the following default gateway applications
    /// <list type="bullet">
    /// <item>AMQP</item>
    /// <item>RESTML</item>
    /// </list>
    /// </summary>
    public interface IAmAMessageProducer : IDisposable
    {
        /// <summary>
        /// How many outstanding messages may the outbox have before we terminate the programme with an OutboxLimitReached exception?
        /// -1 => No limit, although the Outbox may discard older entries which is implementation dependent
        /// 0 => No outstanding messages, i.e. throw an error as soon as something goes into the Outbox
        /// 1+ => Allow this number of messages to stack up in an Outbox before throwing an exception (likely to fail fast)
        /// </summary>
        int MaxOutStandingMessages { get; set; }

        /// <summary>
        /// At what interval should we check the number of outstanding messages has not exceeded the limit set in MaxOutStandingMessages
        /// We spin off a thread to check when inserting an item into the outbox, if the interval since the last insertion is greater than this threshold
        /// If you set MaxOutStandingMessages to -1 or 0 this property is effectively ignored
        /// </summary>
       int MaxOutStandingCheckIntervalMilliSeconds { get; set; }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        void Send(Message message);
        
        /// <summary>
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        void SendWithDelay(Message message, int delayMilliseconds = 0);
     }
}
