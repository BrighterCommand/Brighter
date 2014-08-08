// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="RMQInputChannel.cs" company="">
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
using paramore.brighter.serviceactivator;

/// <summary>
/// The rmq namespace.
/// </summary>
namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RMQInputChannel.
    /// </summary>
    public class RMQInputChannel : IAmAnInputChannel
    {
        private readonly string queueName;
        private readonly IAmAReceiveMessageGateway gateway;
        private readonly ConcurrentQueue<Message> queue = new ConcurrentQueue<Message>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RMQInputChannel"/> class.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="gateway">The gateway.</param>
        public RMQInputChannel(string queueName, IAmAReceiveMessageGateway gateway)
        {
            this.queueName = queueName;
            this.gateway = gateway;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public ChannelName Name {get { return new ChannelName(queueName); } }

        /// <summary>
        /// Receives the specified timeoutin milliseconds.
        /// </summary>
        /// <param name="timeoutinMilliseconds">The timeoutin milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(int timeoutinMilliseconds)
        {
            Message message;
            if (!queue.TryDequeue(out message))
            {
                message = gateway.Receive(queueName, timeoutinMilliseconds);
            }
            return message;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            gateway.Acknowledge(message);
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
        {
            gateway.Reject(message, false);
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            queue.Enqueue(MessageFactory.CreateQuitMessage());
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        /// <value>The length.</value>
        /// <exception cref="System.NotImplementedException"></exception>
        public int Length {
            get { return queue.Count; }
            set { throw new NotImplementedException(); } 
        }


    }
}
