// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 12-18-2014
//
// Last Modified By : ian
// Last Modified On : 12-31-2014
// ***********************************************************************
// <copyright file="RestMsMessageConsumer.cs" company="">
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
using Common.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    /// <summary>
    /// Class RestMsMessageConsumer.
    /// </summary>
    public class RestMsMessageConsumer : IAmAMessageConsumer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageConsumer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMsMessageConsumer(ILog logger)
        {
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Message Receive(string queueName, string routingKey, int timeoutInMilliseconds)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Acknowledge(Message message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Reject(Message message, bool requeue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Purge(string queueName)
        {
            throw new NotImplementedException();
        }
    }
}