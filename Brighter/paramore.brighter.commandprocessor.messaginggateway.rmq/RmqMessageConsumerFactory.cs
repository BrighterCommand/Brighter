﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 01-02-2015
//
// Last Modified By : toby
// Last Modified On : 01-02-2015
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2014 Toby Henderson 

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

using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RmqMessageConsumerFactory.
    /// </summary>
    public class RmqMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageConsumerFactory"/> class.
        /// </summary>
        public RmqMessageConsumerFactory()
            :this(LogProvider.GetCurrentClassLogger())
        {}
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageConsumerFactory"/> class, with a test logger.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RmqMessageConsumerFactory(ILog logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">Is the consumer target durable i.e. channel stores messages between restarts of consumer</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(string queueName, string routingKey, bool isDurable)
        {
            return new RmqMessageConsumer(queueName, routingKey, isDurable, _logger);
        }
    }
}