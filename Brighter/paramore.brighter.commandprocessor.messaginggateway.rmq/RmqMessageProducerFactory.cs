// ***********************************************************************
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
    /// Class RmqMessageProducerFactory.
    /// </summary>
    public class RmqMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly ILog _logger;
        private readonly string _connectionName;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageProducerFactory"/> class.
        /// </summary>
        public RmqMessageProducerFactory(string connectionName = "")
            :this(LogProvider.GetCurrentClassLogger(), connectionName)
        {}


        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageProducerFactory"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RmqMessageProducerFactory(ILog logger, string connectionName = "")
        {
            _logger = logger;
            _connectionName = connectionName;
        }

        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <returns>IAmAMessageProducer.</returns>
        public IAmAMessageProducer Create()
        {
            return new RmqMessageProducer(_logger, _connectionName);
        }
    }
}